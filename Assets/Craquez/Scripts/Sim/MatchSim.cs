using System.Collections.Generic;
using UnityEngine;

namespace Craquez.Sim
{
    public enum MatchState { Kickoff, Playing, Goal, Restart, Halftime, Fulltime }

    /// <summary>Evento de um passo (som, câmera, HUD, animação).</summary>
    public struct SimEvent
    {
        public string type;   // goal, kick, whistle, tackle, save, ...
        public string kind;   // detalhe: "shot", "pass", "lob", "kickoff", "corner"...
        public int team, id;
        public bool ok;
        public float power;
        public int skill;
    }

    /// <summary>Configuração de um time para a partida.</summary>
    public class TeamSetup
    {
        public TeamData data;
        public Tactics tactics = new Tactics();
    }

    /// <summary>
    /// Simulação completa de uma partida, em passo fixo (60 Hz).
    /// Nada aqui lê Input, tempo real ou Random: a única entrada externa é
    /// Step(frames), e toda aleatoriedade vem de Rng (semente). Mesma semente +
    /// mesmas entradas = mesma partida (replays e online por lockstep).
    /// </summary>
    public class MatchSim
    {
        public const float DT = 1f / 60f;
        public const float ChargeTime = 0.85f;
        public const float PendingWindow = 0.55f;
        public const float AutoSwitchEvery = 0.35f;

        public class Ctrl
        {
            public int player, prev, charging, pending;
            public float charge, pendingPower, pendingT, switchCd, autoT, helpT;
            public Vector2 aim, prevRs;
            public bool shootHeld, finesseHeld;
        }

        public ModeDef mode;
        public float halfLen = 52.5f, halfW = 34f, goalHalf = 3.66f, goalH = 2.44f;
        public SimBall ball = new SimBall();
        public readonly List<SimPlayer> players = new List<SimPlayer>();
        public TeamSetup[] teams = new TeamSetup[2];
        public Ctrl[] ctrl = { new Ctrl(), new Ctrl() };
        public int[] score = new int[2];
        public SimRng rng;

        public int tick;
        public float clock;
        public int half = 1;
        public float halfSeconds = 180f;
        public MatchState state = MatchState.Kickoff;
        public float stateTimer;
        public int kickoffTeam;
        public int protectTeam = -1;
        public float protectT;
        public int passReceiver = -1;
        int takeTick, passTick = -999;
        Vector2 passFrom;
        public float passReceiverT;
        public int penaltyTeam = -1, penaltyTaker = -1;
        public float penaltyT;
        public int offsideTeam = -1;
        public readonly List<int> offsideIds = new List<int>();
        public bool noOffsideKick;
        public int lastScorer = -1, lastGoalTeam = -1;
        public int[] statsShots = new int[2];
        public int[] teamMentality = { 1, 1 };
        public int[] teamPress = { 1, 1 };
        /// <summary>Ajuste de rolagem por clima (chuva = bola mais rápida).</summary>
        public float rollScale = 1f;
        /// <summary>Lados trocados no início (definir antes de Setup).</summary>
        public bool startSwapped;
        /// <summary>Tick do último chute (tempo de reação do goleiro).</summary>
        public int lastShotTick = -1000;

        public readonly List<SimEvent> events = new List<SimEvent>();

        public static readonly string[] FallbackNames =
        {
            "Zé Raio", "Dudu", "Tico", "Bira", "Neném", "Pepeu", "Juninho", "Careca", "Gaúcho", "Baixinho",
            "Toninho", "Rafinha", "Leléu", "Fumaça", "Pirulito", "Danilo", "Serginho", "Tatu", "Feijão", "Cacau",
        };

        public void Setup(string modeId, TeamSetup a, TeamSetup b, uint seed, float secondsPerHalf)
        {
            mode = GameModes.Get(modeId);
            mode.rollDecel *= rollScale;
            ball.radius = modeId == GameModes.Futsal ? 0.098f : 0.11f;   // futsal: bola tamanho 4
            halfLen = mode.length * 0.5f;
            halfW = mode.width * 0.5f;
            goalHalf = mode.goalWidth * 0.5f;
            goalH = mode.goalHeight;
            halfSeconds = secondsPerHalf;
            rng = new SimRng(seed);
            teams = new[] { a, b };
            players.Clear();
            ctrl = new[] { new Ctrl(), new Ctrl() };
            var forms = GameModes.Formations(modeId);
            for (int t = 0; t < 2; t++)
            {
                var tm = teams[t];
                float rating = tm.data != null ? tm.data.rating : 0.7f;
                var roster = tm.data != null ? tm.data.roster : new List<RosterEntry>();
                var used = new HashSet<int>();
                var tac = tm.tactics ?? new Tactics();
                var formation = forms.TryGetValue(tac.formation ?? "", out var f) ? f : mode.formation;
                teamMentality[t] = Mathf.Clamp(tac.mentality, 0, 2);
                teamPress[t] = Mathf.Clamp(tac.press, 0, 2);
                for (int i = 0; i < formation.Length; i++)
                {
                    var p = new SimPlayer
                    {
                        id = players.Count, team = t, index = i, basePos = formation[i],
                        isKeeper = mode.keeper && i == 0, number = i + 1,
                    };
                    p.name = FallbackNames[(int)(rng.NextU32() % (uint)FallbackNames.Length)];
                    p.pace = Mathf.Clamp(rating + rng.Gauss() * 0.08f, 0.35f, 0.99f);
                    p.accel = Mathf.Clamp(rating + rng.Gauss() * 0.08f, 0.35f, 0.99f);
                    p.shot = Mathf.Clamp(rating + rng.Gauss() * 0.1f + (p.basePos.x > -0.3f ? 0.06f : -0.05f), 0.3f, 0.99f);
                    p.passing = Mathf.Clamp(rating + rng.Gauss() * 0.08f, 0.35f, 0.99f);
                    p.control = Mathf.Clamp(rating + rng.Gauss() * 0.08f, 0.35f, 0.99f);
                    p.defense = Mathf.Clamp(rating + rng.Gauss() * 0.1f + (p.basePos.x < -0.6f ? 0.08f : -0.04f), 0.3f, 0.99f);
                    p.pos = GameModes.RoleFor(p.basePos, i, mode.keeper);
                    if (roster.Count > 0)
                    {
                        string want = p.pos;
                        RosterEntry e = null;
                        if (tac.lineup != null && tac.lineup.Length == formation.Length && tac.lineup[i] >= 0 &&
                            tac.lineup[i] < roster.Count && !used.Contains(tac.lineup[i]))
                        {
                            e = roster[tac.lineup[i]];
                            used.Add(tac.lineup[i]);
                        }
                        else e = PickFromRoster(roster, want, used, !mode.keeper);
                        if (e != null) ApplyRoster(p, e);
                    }
                    players.Add(p);
                }
            }
            kickoffTeam = 0;
            ResetForKickoff(0);
        }

        /// <summary>Clima mudou no meio do jogo: bola mais rápida na chuva, mais lenta na neve.</summary>
        public void SetRollScale(float s)
        {
            rollScale = s;
            mode.rollDecel = GameModes.Get(mode.id).rollDecel * s;
        }

        /// <summary>Troca formação, mentalidade e pressão no meio do jogo (menu de pausa).</summary>
        public void ApplyTactics(int t, Tactics tac)
        {
            if (tac == null) return;
            teams[t].tactics = tac;
            teamMentality[t] = Mathf.Clamp(tac.mentality, 0, 2);
            teamPress[t] = Mathf.Clamp(tac.press, 0, 2);
            var forms = GameModes.Formations(mode.id);
            if (!forms.TryGetValue(tac.formation ?? "", out var f)) return;
            int i = 0;
            foreach (var p in players)
            {
                if (p.team != t) continue;
                if (i < f.Length)
                {
                    p.basePos = f[i];
                    if (!p.isKeeper) p.pos = GameModes.RoleFor(p.basePos, i, mode.keeper);
                }
                i++;
            }
        }

        static void ApplyRoster(SimPlayer p, RosterEntry e)
        {
            p.name = e.n;
            p.number = e.num;
            p.nat = e.nat;
            p.face = e.f;
            if (e.a.Length >= 6)
            {
                p.pace = e.a[0]; p.accel = e.a[1]; p.shot = e.a[2];
                p.passing = e.a[3]; p.control = e.a[4]; p.defense = e.a[5];
            }
            foreach (var tn in e.t)
                if (tn != null && SimPlayer.TraitNames.TryGetValue(tn, out var tr)) p.traits |= tr;
            if (p.Has(Trait.Velocista))
            {
                p.pace = Mathf.Min(0.99f, p.pace + 0.04f);
                p.accel = Mathf.Min(0.99f, p.accel + 0.03f);
            }
        }

        /// <summary>Melhor atleta livre do elenco para a posição (média dos atributos).</summary>
        public static RosterEntry PickFromRoster(List<RosterEntry> roster, string want, HashSet<int> used, bool noKeepers)
        {
            RosterEntry best = null;
            int bestI = -1;
            float bestS = -1f;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    if (used.Contains(i)) continue;
                    var e = roster[i];
                    if (noKeepers && e.pos == "GK") continue;
                    if (pass == 0 && e.pos != want) continue;
                    float sc = 0f;
                    foreach (var v in e.a) sc += v;
                    if (sc > bestS) { bestS = sc; best = e; bestI = i; }
                }
                if (best != null) break;
            }
            if (bestI >= 0) used.Add(bestI);
            return best;
        }

        // ------------------------------------------------------------ utilidades
        public float TeamDir(int t)
        {
            float d = t == 0 ? 1f : -1f;
            if (startSwapped) d = -d;   // "trocar os campos": o mandante começa atacando para a esquerda
            return half == 1 ? d : -d;
        }

        public SimPlayer Controlled(int t) => players[ctrl[t].player];
        public SimPlayer Owner => ball.owner >= 0 ? players[ball.owner] : null;
        public bool TeamHasBall(int t) => ball.owner >= 0 && players[ball.owner].team == t;
        public float ChargeOf(int t) => ctrl[t].charging != 0 ? ctrl[t].charge : 0f;

        public SimPlayer NearestOpponent(SimPlayer p)
        {
            SimPlayer best = null;
            float bd = float.MaxValue;
            foreach (var o in players)
            {
                if (o.team == p.team) continue;
                float d = (o.position - p.position).sqrMagnitude;
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        public SimPlayer NearestToBall(int t, int exclude = -1, bool allowKeeper = false)
        {
            SimPlayer best = null;
            float bd = float.MaxValue;
            foreach (var p in players)
            {
                if (p.team != t || p.id == exclude || (p.isKeeper && !allowKeeper)) continue;
                float d = (p.position - ball.pos).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        public bool InOwnBox(SimPlayer p)
        {
            if (!mode.keeper) return false;
            float gx = -halfLen * TeamDir(p.team);
            return Mathf.Abs(p.position.x - gx) < mode.boxDepth + 0.5f && Mathf.Abs(p.position.y) < mode.boxHalf + 0.5f;
        }

        void Emit(string type, int team = -1, int id = -1, string kind = null, bool ok = false, float power = 0f, int skill = 0)
        {
            events.Add(new SimEvent { type = type, team = team, id = id, kind = kind, ok = ok, power = power, skill = skill });
        }

        // ------------------------------------------------------------ reinícios
        Vector2 FormationWorld(SimPlayer p)
            => new Vector2(p.basePos.x * halfLen * TeamDir(p.team), p.basePos.y * halfW * 0.9f);

        void ResetForKickoff(int t)
        {
            ball.pos = Vector2.zero; ball.vel = Vector2.zero; ball.z = 0; ball.vz = 0; ball.spin = 0; ball.slideT = 0; ball.owner = -1;
            passReceiver = -1;
            penaltyTeam = -1;
            offsideIds.Clear();
            offsideTeam = -1;
            foreach (var p in players)
            {
                p.position = FormationWorld(p);
                if (p.team != t && p.position.magnitude < mode.centerR + 0.5f)
                    p.position = p.position.magnitude > 0.1f ? p.position.normalized * (mode.centerR + 0.5f)
                        : new Vector2(-TeamDir(p.team) * (mode.centerR + 0.5f), 0);
                p.vel = Vector2.zero;
                p.facing = new Vector2(TeamDir(p.team), 0);
                p.stun = p.slide = p.hold = p.dive = p.kickCd = 0f;
                p.windup = 0f;
                p.skill = Skill.None;
            }
            SimPlayer kicker = null;
            foreach (var p in players)
                if (p.team == t && !p.isKeeper && (kicker == null || p.basePos.x > kicker.basePos.x)) kicker = p;
            kicker.position = new Vector2(-TeamDir(t) * 0.45f, 0);
            ball.owner = kicker.id;
            ball.lastTouchTeam = t;
            ctrl[t].player = kicker.id;
            ctrl[1 - t].player = NearestToBall(1 - t).id;
            foreach (var c in ctrl) { c.charging = 0; c.pending = 0; }
            protectTeam = t;
            protectT = 1.5f;
            state = MatchState.Kickoff;
            stateTimer = 0.9f;
        }

        void SetupRestart(int t, Vector2 spot, string kind)
        {
            spot.x = Mathf.Clamp(spot.x, -halfLen, halfLen);
            spot.y = Mathf.Clamp(spot.y, -halfW, halfW);
            ball.pos = spot; ball.vel = Vector2.zero; ball.z = 0; ball.vz = 0; ball.spin = 0; ball.slideT = 0;
            passReceiver = -1;
            SimPlayer taker = null;
            if (kind == "goalkick" && mode.keeper)
                foreach (var p in players) if (p.team == t && p.isKeeper) taker = p;
            if (taker == null)
            {
                float bd = float.MaxValue;
                foreach (var p in players)
                {
                    if (p.team != t || p.isKeeper) continue;
                    float d = (p.position - spot).sqrMagnitude;
                    if (d < bd) { bd = d; taker = p; }
                }
            }
            var inward = (new Vector2(TeamDir(t) * halfLen * 0.3f, 0) - spot).Norm();
            taker.position = spot - inward * 0.5f;
            taker.vel = Vector2.zero;
            taker.facing = inward;
            taker.stun = 0; taker.slide = 0;
            ball.owner = taker.id;
            ball.lastTouchTeam = t;
            if (taker.isKeeper) taker.hold = 1.6f;
            else ctrl[t].player = taker.id;
            // tiro de meta e escanteio: corte de cena, todos já nas posições do reinício
            bool cut = kind == "goalkick" || kind == "corner";
            if (cut)
                foreach (var p in players)
                {
                    if (p == taker) continue;
                    if (p.isKeeper) { p.position = new Vector2(-TeamDir(p.team) * (halfLen - 1f), 0f); }
                    else p.position = FormationTarget(p, p.team == t);
                    p.position.x = Mathf.Clamp(p.position.x, -halfLen + 0.5f, halfLen - 0.5f);
                    p.position.y = Mathf.Clamp(p.position.y, -halfW + 0.5f, halfW - 0.5f);
                    p.vel = Vector2.zero;
                    p.facing = (ball.pos - p.position).Norm();
                }
            foreach (var p in players)
            {
                if (p.team != t)
                {
                    var d = p.position - spot;
                    if (d.magnitude < mode.restartDist)
                        p.position = spot + (d.magnitude > 0.1f ? d.normalized : -inward) * mode.restartDist;
                }
                p.stun = Mathf.Min(p.stun, 0.2f);
            }
            protectTeam = t;
            protectT = 2.5f;
            offsideIds.Clear();
            offsideTeam = -1;
            noOffsideKick = kind == "throwin" || kind == "corner" || kind == "goalkick";
            state = MatchState.Restart;
            stateTimer = cut ? 1.1f : 0.7f;
            Emit("whistle", t, -1, kind);
        }

        // ------------------------------------------------------------ passo principal
        public void Step(InputFrame[] frames)
        {
            tick++;
            events.Clear();
            switch (state)
            {
                case MatchState.Kickoff:
                case MatchState.Restart:
                    stateTimer -= DT;
                    for (int t = 0; t < 2; t++) ctrl[t].prev = frames != null && t < frames.Length ? frames[t].buttons : 0;
                    AiAll(true);
                    foreach (var p in players) { p.intent *= 0.3f; MovePlayer(p); }
                    Dribble();
                    if (stateTimer <= 0f)
                    {
                        state = MatchState.Playing;
                        if (tick > 1 && clock == 0f) Emit("whistle", -1, -1, "kickoff");
                    }
                    break;
                case MatchState.Playing:
                    StepPlaying(frames);
                    break;
                case MatchState.Goal:
                    stateTimer -= DT;
                    AiAll(false);
                    foreach (var p in players) { p.intent *= 0.5f; MovePlayer(p); }
                    ball.Step(DT, mode);
                    KeepBallInNet();
                    if (stateTimer <= 0f) ResetForKickoff(kickoffTeam);
                    break;
                case MatchState.Halftime:
                    stateTimer -= DT;
                    if (stateTimer <= 0f)
                    {
                        half = 2;
                        clock = 0f;
                        kickoffTeam = 1;
                        ResetForKickoff(1);
                    }
                    break;
            }
        }

        void StepPlaying(InputFrame[] frames)
        {
            clock += DT;
            protectT = Mathf.Max(0f, protectT - DT);
            passReceiverT -= DT;
            if (passReceiverT <= 0f) passReceiver = -1;
            if (penaltyTeam >= 0)
            {
                penaltyT -= DT;
                if (penaltyT <= 0f && ball.owner == penaltyTaker)
                {
                    var tk = players[penaltyTaker];
                    Shoot(tk, new Vector2(TeamDir(tk.team), rng.Chance(0.5f) ? 0.7f : -0.7f), 0.55f);
                }
            }
            if (clock >= halfSeconds && !BallInDanger())
            {
                if (half == 1)
                {
                    state = MatchState.Halftime;
                    stateTimer = 3f;
                    Emit("whistle", -1, -1, "halftime");
                }
                else
                {
                    state = MatchState.Fulltime;
                    Emit("whistle", -1, -1, "fulltime");
                }
                return;
            }
            for (int t = 0; t < 2; t++)
                ProcessController(t, frames != null && t < frames.Length ? frames[t] : default);
            AiAll(false);
            foreach (var p in players) MovePlayer(p);
            ResolveCollisions();
            Slides();
            Dribble();
            Possession();
            BallPhysics();
        }

        bool BallInDanger() => ball.owner < 0 && ball.Speed > 10f && Mathf.Abs(ball.pos.x) > halfLen * 0.6f;

        // ------------------------------------------------------------ controle
        void ProcessController(int t, InputFrame f)
        {
            var c = ctrl[t];
            int pressed = f.buttons & ~c.prev;
            int released = c.prev & ~f.buttons;
            c.prev = f.buttons;
            c.switchCd = Mathf.Max(0f, c.switchCd - DT);
            c.helpT = Mathf.Max(0f, c.helpT - DT);
            c.shootHeld = f.Has(InputFrame.Shoot);

            if ((pressed & InputFrame.Switch) != 0 && c.switchCd <= 0f && !TeamHasBall(t)) SwitchPlayer(t, f.move);
            AutoSwitch(t);

            var p = players[c.player];
            p.intent = f.move.Limit(1f);
            p.wantSprint = f.Has(InputFrame.Sprint);
            p.shielding = f.Has(InputFrame.Shield);
            p.faceBall = false;
            foreach (var q in players) if (q.team == t && q.id != p.id) q.shielding = false;
            c.finesseHeld = f.Has(InputFrame.Finesse);
            if (f.move.magnitude > 0.25f) c.aim = f.move.normalized;
            else if (c.aim == Vector2.zero) c.aim = p.facing;
            if (p.windup > 0f) p.windupAim = c.aim;
            var rs = f.rstick;
            if (rs.magnitude > 0.7f && c.prevRs.magnitude < 0.45f && ball.owner == p.id && penaltyTeam < 0)
                StartSkill(p, rs.normalized);
            c.prevRs = rs;
            if (penaltyTeam >= 0)
            {
                p.intent = Vector2.zero;
                p.wantSprint = false;
                if (t != penaltyTeam) return;
            }
            bool keeperHold = ball.owner >= 0 && players[ball.owner].isKeeper && players[ball.owner].team == t && players[ball.owner].hold > 0f;
            foreach (int b in new[] { InputFrame.Pass, InputFrame.Shoot, InputFrame.Lob, InputFrame.Through })
            {
                if ((pressed & b) != 0)
                {
                    if (keeperHold && (b == InputFrame.Pass || b == InputFrame.Lob))
                    {
                        Pass(players[ball.owner], c.aim, 0.6f, false, b == InputFrame.Lob);   // Pass zera o "segurando" e protege a reposição
                    }
                    else OnPress(t, p, b);
                }
                if ((released & b) != 0 && c.charging == b) OnRelease(t, p, b);
            }
            if (c.charging != 0) c.charge = Mathf.Min(1f, c.charge + DT / ChargeTime);
            if (c.pending != 0)
            {
                c.pendingT -= DT;
                if (ball.owner == p.id)
                {
                    int kind = c.pending;
                    c.pending = 0;
                    DoKick(p, kind, c.pendingPower, c.aim, c.finesseHeld);
                }
                else if (c.pendingT <= 0f) c.pending = 0;
            }
        }

        void OnPress(int t, SimPlayer p, int b)
        {
            var c = ctrl[t];
            bool oppHas = ball.owner >= 0 && players[ball.owner].team != t;
            if (oppHas)
            {
                if (b == InputFrame.Pass) StandingTackle(p);
                else if (b == InputFrame.Shoot) StartSlide(p);
                else if (b == InputFrame.Through) c.helpT = 1.2f;
                return;
            }
            c.charging = b;
            c.charge = 0f;
        }

        void OnRelease(int t, SimPlayer p, int b)
        {
            var c = ctrl[t];
            float power = c.charge;
            c.charging = 0;
            c.charge = 0f;
            if (ball.owner == p.id) BeginWindup(p, b, power, c.aim, c.finesseHeld);
            else
            {
                c.pending = b;
                c.pendingPower = power;
                c.pendingT = PendingWindow;
            }
        }

        void SwitchPlayer(int t, Vector2 prefer)
        {
            var c = ctrl[t];
            SimPlayer best = null;
            float bestS = float.MaxValue;
            var target = ball.pos + ball.vel * 0.3f;
            foreach (var p in players)
            {
                if (p.team != t || p.isKeeper || p.id == c.player) continue;
                float s = (target - p.position).magnitude;
                if (prefer.magnitude > 0.3f)
                    s -= Vector2.Dot((p.position - players[c.player].position).Norm(), prefer.normalized) * 6f;
                if (s < bestS) { bestS = s; best = p; }
            }
            if (best != null)
            {
                c.player = best.id;
                c.switchCd = 0.15f;
                c.charging = 0;
                c.pending = 0;
                Emit("switch", t);
            }
        }

        void AutoSwitch(int t)
        {
            var c = ctrl[t];
            c.autoT -= DT;
            if (c.autoT > 0f || TeamHasBall(t) || c.charging != 0) return;
            c.autoT = AutoSwitchEvery;
            var cur = players[c.player];
            var n = NearestToBall(t);
            if (n != null && n.id != cur.id && cur.position.Dist(ball.pos) > n.position.Dist(ball.pos) + 7f)
            {
                c.player = n.id;
                Emit("switch", t);
            }
        }

        // ------------------------------------------------------------ ações com bola
        void BeginWindup(SimPlayer p, int kind, float power, Vector2 aim, bool finesse)
        {
            if (p.windup > 0f || p.skill != Skill.None) return;
            p.windup = kind == InputFrame.Shoot ? Mathf.Lerp(0.12f, 0.2f, power) : kind == InputFrame.Lob ? 0.13f : 0.08f;
            p.windupTotal = p.windup;
            p.windupKind = kind;
            p.kickKind = kind;
            p.windupPower = power;
            p.windupAim = aim;
            p.windupFinesse = finesse;
            Emit("windup", p.team, p.id, KindName(kind));
        }

        public static string KindName(int kind)
            => kind == InputFrame.Shoot ? "shot" : kind == InputFrame.Lob ? "lob" : kind == InputFrame.Through ? "through" : "pass";

        void DoKick(SimPlayer p, int kind, float power, Vector2 aim, bool finesse = false)
        {
            if (ball.owner != p.id) return;
            if (kind == InputFrame.Pass) Pass(p, aim, power, false, false);
            else if (kind == InputFrame.Through) Pass(p, aim, power, true, false);
            else if (kind == InputFrame.Lob)
            {
                if (TryChip(p, aim, power)) return;
                Pass(p, aim, power, false, true);
            }
            else if (kind == InputFrame.Shoot)
            {
                Shoot(p, aim, power, finesse);
                TryBlock(p);
            }
        }

        /// <summary>Cavadinha: passe alto mirando o gol, perto da área, com o goleiro adiantado.</summary>
        bool TryChip(SimPlayer p, Vector2 aim, float power)
        {
            if (!mode.keeper) return false;
            float dir = TeamDir(p.team);
            var goal = new Vector2(halfLen * dir, 0f);
            var toGoal = goal - ball.pos;
            float d = toGoal.magnitude;
            if (d > 28f || Vector2.Dot(aim.Norm(), toGoal / d) < 0.75f) return false;
            SimPlayer gk = null;
            foreach (var o in players) if (o.team != p.team && o.isKeeper) gk = o;
            if (gk == null || Mathf.Abs(gk.position.x - goal.x) < 2.5f) return false;
            p.kickKind = InputFrame.Lob;
            float tgtY = Mathf.Clamp(aim.y * goalHalf * 0.6f, -goalHalf * 0.6f, goalHalf * 0.6f);
            var target = new Vector2(goal.x, tgtY);
            var to = target - ball.pos;
            float dist = to.magnitude;
            var dirv = (to / dist).Rotated(rng.Gauss() * 0.04f * (1.3f - p.shot));
            // trajetória por cima do goleiro: tempo de voo maior em distâncias maiores
            float flight = 0.9f + dist / 22f + (1f - power) * 0.2f;
            ball.vel = dirv * (dist * 0.95f / flight);
            ball.vz = 0.5f * SimBall.G * flight * Mathf.Lerp(0.9f, 1.05f, power);
            ball.spin = 0f;
            statsShots[p.team]++;
            lastShotTick = tick;
            ReleaseBall(p);
            Emit("kick", p.team, p.id, "chip", power: power);
            return true;
        }

        void TryBlock(SimPlayer kicker)
        {
            var dirv = ball.vel.Norm();
            foreach (var o in players)
            {
                if (o.team == kicker.team || o.isKeeper || o.stun > 0f) continue;
                var to = o.position - kicker.position;
                float d = to.magnitude;
                if (d < 0.3f || d > 1.7f || Vector2.Dot(to.Norm(), dirv) < 0.82f) continue;
                float chance = 0.25f + 0.35f * o.defense + (o.Has(Trait.Muralha) ? 0.1f : 0f) - 0.15f * (d - 0.3f);
                if (rng.Chance(chance))
                {
                    float spd = ball.vel.magnitude;
                    var side = new Vector2(-dirv.y, dirv.x) * (rng.Chance(0.5f) ? 1f : -1f);
                    ball.vel = (-dirv * 0.35f + side * 0.65f).Norm() * spd * rng.Range(0.25f, 0.45f);
                    ball.vz = rng.Range(1f, 4f);
                    ball.spin = 0f;
                    ball.lastTouchTeam = o.team;
                    ball.lastTouchId = o.id;
                    Emit("block", o.team, o.id);
                }
                return;
            }
        }

        void ReleaseBall(SimPlayer p)
        {
            ball.owner = -1;
            ball.lastTouchTeam = p.team;
            ball.lastTouchId = p.id;
            p.kickCd = 0.28f;
            p.kickAnim = 0.25f;
            p.hold = 0f;
            protectT = 0f;
            MarkOffside(p);
            penaltyTeam = -1;
        }

        void Pass(SimPlayer p, Vector2 aim, float power, bool through, bool lofted)
        {
            p.kickKind = lofted ? InputFrame.Lob : through ? InputFrame.Through : InputFrame.Pass;
            if (aim.sqrMagnitude < 0.01f) aim = p.facing;
            aim = aim.Norm();
            float dir = TeamDir(p.team);
            SimPlayer best = null;
            float bestS = float.MinValue;
            float maxD = halfLen * (lofted ? 1.5f : 1.1f);
            // 1ª busca: no cone da mira; sem ninguém ali, abre o cone (passe não vai para o vazio)
            for (int pass2 = 0; pass2 < 2 && best == null; pass2++)
            foreach (var m in players)
            {
                if (m.team != p.team || m == p) continue;
                var to = m.position - ball.pos;
                float d = to.magnitude;
                if (d < (mode.id == GameModes.Field ? 2.5f : 1.6f) || d > maxD) continue;
                float cosang = Vector2.Dot(aim, to / d);
                if (cosang < (pass2 == 0 ? 0.55f : -1f)) continue;
                float s = cosang * 2.2f - d / maxD;
                if (m.isKeeper) s -= 1f;
                float lane = mode.id == GameModes.Field ? 1.8f : 1.1f;
                foreach (var o in players)
                {
                    if (o.team == p.team) continue;
                    // linha de passe fechada e companheiro marcado
                    if (!lofted && V2.ClosestOnSegment(o.position, ball.pos, m.position).Dist(o.position) < lane) s -= 0.9f;
                    if (o.position.Dist(m.position) < (mode.id == GameModes.Field ? 2.2f : 1.4f)) s -= 0.4f;
                }
                if (s > bestS) { bestS = s; best = m; }
            }
            float roll = mode.rollDecel;
            Vector2 target;
            if (best != null)
            {
                target = best.position;
                if (through)
                {
                    var runDir = (aim * 0.5f + new Vector2(dir, 0) * 0.5f).Norm();
                    target = best.position + runDir * Mathf.Lerp(4f, 12f, power);
                }
                else
                {
                    for (int k = 0; k < 2; k++)
                    {
                        float tt = ball.pos.Dist(target) / Mathf.Lerp(10f, 18f, power);
                        target = best.position + best.vel * tt;
                    }
                }
                passReceiver = best.id;
                passReceiverT = 3f;
                passTick = tick;
                passFrom = ball.pos;
            }
            else target = ball.pos + aim * Mathf.Lerp(8f, 32f, power);
            target.x = Mathf.Clamp(target.x, -halfLen - 2f, halfLen + 2f);
            target.y = Mathf.Clamp(target.y, -halfW + 0.5f, halfW - 0.5f);
            var toT = target - ball.pos;
            float dist = Mathf.Max(toT.magnitude, 0.5f);
            var dirv = toT / dist;
            float err = rng.Gauss() * 0.03f * (1.25f - p.passing) * (0.6f + power) * (p.sprinting ? 1.6f : 1f) * (p.Has(Trait.Maestro) ? 0.65f : 1f);
            dirv = dirv.Rotated(err);
            if (lofted)
            {
                float flight = 0.75f + dist / 30f + power * 0.25f;
                ball.vz = 0.5f * SimBall.G * flight;
                ball.vel = dirv * (dist * 0.86f / flight);
                ball.spin = rng.Gauss() * 0.8f;
            }
            else
            {
                // passe chega com força para o companheiro dominar (campo mais tenso; na quadra, sem sair pela lateral)
                bool small = mode.id != GameModes.Field;
                float vEnd = small ? 2.2f + power * 3f : 4.5f + power * 4.5f;
                float v0 = Mathf.Clamp(Mathf.Sqrt(vEnd * vEnd + 2f * roll * dist) * 1.04f, small ? 4f : 7f, 30f);
                ball.vel = dirv * v0;
                ball.vz = v0 > 22f ? 0.6f : 0f;
                ball.spin = 0f;
                ball.slideT = v0 > 14f ? 0.1f : 0f;
            }
            bool fromHands = p.hold > 0f;
            if (p.hold > 0f || ball.z > 0.5f) ball.z = Mathf.Max(ball.z, 0.3f);
            ReleaseBall(p);
            if (fromHands)
            {
                // reposição do goleiro: adversário não pode disputar a bola saindo da mão
                protectTeam = p.team;
                protectT = 0.7f;
            }
            Emit("kick", p.team, p.id, lofted ? "lob" : through ? "through" : "pass", power: power);
        }

        void Shoot(SimPlayer p, Vector2 aim, float power, bool finesse = false)
        {
            p.kickKind = InputFrame.Shoot;
            float dir = TeamDir(p.team);
            float gx = halfLen * dir;
            float aimY;
            if (Mathf.Abs(aim.y) > 0.2f) aimY = Mathf.Clamp(aim.y * 1.4f, -1f, 1f) * goalHalf * 0.8f;
            else aimY = -Mathf.Sign(Mathf.Abs(p.position.y) > 0.5f ? p.position.y : 1f) * goalHalf * 0.55f;
            if (finesse)
            {
                float side = Mathf.Abs(aimY) > 0.1f ? Mathf.Sign(aimY) : 1f;
                aimY = side * goalHalf * 0.84f;
            }
            var target = new Vector2(gx + dir * 0.5f, aimY);
            var to = target - ball.pos;
            float d = Mathf.Max(to.magnitude, 1f);
            var dirv = to / d;
            float spd = Mathf.Lerp(13f, 31f, power) * (0.78f + 0.3f * p.shot);
            if (finesse) spd *= 0.8f;
            var opp = NearestOpponent(p);
            float pressure = opp != null ? Mathf.Clamp01(1f - opp.position.Dist(p.position) / 3f) : 0f;
            float err = (0.02f + 0.07f * power * power) * (1.3f - p.shot) * (1f + pressure * 0.8f) * (p.sprinting ? 1.4f : 1f);
            if (p.Has(Trait.Finalizador)) err *= 0.8f;
            if (finesse && p.Has(Trait.Colocado)) err *= 0.75f;
            if (p.Has(Trait.LongaDistancia) && d > 20f) { spd *= 1.1f; err *= 0.85f; }
            float body = Mathf.Abs(p.facing.AngleTo(dirv));
            err *= 1f + Mathf.Max(0f, body - 0.6f) * 0.9f;
            if (finesse) err *= 0.65f;
            dirv = dirv.Rotated(rng.Gauss() * err);
            float curl = 0f;
            if (finesse)
            {
                var perp = new Vector2(-dirv.y, dirv.x);
                curl = Mathf.Abs(target.y) > 0.1f ? Mathf.Sign(Vector2.Dot(perp, new Vector2(gx, 0f) - target)) : 1f;
                dirv = dirv.Rotated(-curl * Mathf.Clamp(0.9f / d, 0.02f, 0.09f));
            }
            ball.vel = dirv * spd;
            float tFlight = d / (spd * 0.9f);
            float h = Mathf.Lerp(0.15f, 2.7f, Mathf.Pow(power, 1.8f)) + rng.Gauss() * 0.25f * power;
            if (finesse) h = Mathf.Lerp(0.6f, 1.9f, power) + rng.Gauss() * 0.15f;
            h = Mathf.Max(h, 0f);
            ball.vz = Mathf.Clamp((h + 0.5f * SimBall.G * tFlight * tFlight) / tFlight, 0f, 14f);
            ball.spin = rng.Gauss() * 1.2f;
            if (finesse) ball.spin = curl * (2.2f + 0.8f * p.shot) * (p.Has(Trait.Colocado) ? 1.15f : 1f);
            statsShots[p.team]++;
            lastShotTick = tick;
            ReleaseBall(p);
            Emit("kick", p.team, p.id, "shot", power: power);
        }

        // ------------------------------------------------------------ defesa
        void StandingTackle(SimPlayer p)
        {
            if (p.tackleAnim > 0f || p.stun > 0f || p.slide > 0f || p.offBalance > 0f) return;
            p.tackleAnim = 0.35f;
            p.vel += p.facing * 1.5f;
            Emit("tackle_try", p.team, p.id);
            if (ball.owner < 0) return;
            var o = players[ball.owner];
            if (o.team == p.team || (protectT > 0f && protectTeam == o.team) || o.hold > 0f) return;
            float d = p.position.Dist(ball.pos);
            if (d > 1.5f) { p.stun = 0.25f; return; }
            bool fromBehind = Vector2.Dot(o.facing, (p.position - o.position).Norm()) < -0.45f;
            if (fromBehind && rng.Chance(0.3f)) { Foul(p, o); return; }
            float chance = 0.42f + 0.4f * p.defense - 0.32f * o.control + (fromBehind ? 0f : 0.12f);
            if (p.Has(Trait.Muralha)) chance += 0.1f;
            if (o.Has(Trait.Driblador)) chance -= 0.06f;
            if (o.shielding) chance -= 0.12f + 0.12f * o.control;
            if (o.skill == Skill.Roulette) chance -= 0.3f;
            if (p.shielding) chance += 0.08f;
            if (rng.Chance(chance))
            {
                ball.owner = -1;
                var away = (p.facing * 0.6f + (ball.pos - o.position).Norm() * 0.4f).Norm();
                ball.vel = away * rng.Range(3f, 6.5f);
                ball.lastTouchTeam = p.team;
                ball.lastTouchId = p.id;
                o.stun = 0.3f;
                Emit("tackle", p.team, p.id, ok: true);
            }
            else
            {
                p.stun = 0.45f;
                Emit("tackle", p.team, p.id, ok: false);
            }
        }

        void StartSlide(SimPlayer p)
        {
            if (p.slide > 0f || p.stun > 0f || p.isKeeper || p.offBalance > 0f) return;
            p.slide = 0.6f;
            p.slideHitBall = false;
            p.vel = p.facing * Mathf.Max(p.vel.magnitude + 2f, 7.5f);
            Emit("slide", p.team, p.id);
        }

        void Slides()
        {
            foreach (var p in players)
            {
                if (p.slide <= 0f) continue;
                if (!p.slideHitBall && ball.z < 0.5f && p.position.Dist(ball.pos) < 0.95f)
                {
                    if (ball.owner >= 0) players[ball.owner].stun = 0.35f;
                    ball.owner = -1;
                    ball.vel = p.facing * 7f + p.vel * 0.3f;
                    ball.lastTouchTeam = p.team;
                    ball.lastTouchId = p.id;
                    p.slideHitBall = true;
                    Emit("tackle", p.team, p.id, ok: true);
                }
                foreach (var o in players)
                {
                    if (o.team == p.team || o.stun > 0f || (o.isKeeper && o.hold > 0f)) continue;
                    if (p.position.Dist(o.position) < 0.8f)
                    {
                        if (p.slideHitBall) o.stun = 0.8f;
                        else { Foul(p, o); return; }
                    }
                }
            }
        }

        void Foul(SimPlayer offender, SimPlayer victim)
        {
            offender.stun = 1f;
            offender.slide = 0f;
            victim.stun = 0.9f;
            Emit("foul", offender.team, offender.id);
            var spot = victim.position;
            spot.x = Mathf.Clamp(spot.x, -halfLen + 1f, halfLen - 1f);
            spot.y = Mathf.Clamp(spot.y, -halfW + 1f, halfW - 1f);
            float gx = -halfLen * TeamDir(offender.team);
            if (mode.penaltyDist > 0f && Mathf.Abs(spot.x - gx) < mode.boxDepth && Mathf.Abs(spot.y) < mode.boxHalf)
            {
                SetupPenalty(victim.team, victim);
                return;
            }
            SetupRestart(victim.team, spot, "freekick");
        }

        void SetupPenalty(int t, SimPlayer taker)
        {
            float dir = TeamDir(t);
            float gx = halfLen * dir;
            var spot = new Vector2(gx - dir * mode.penaltyDist, 0f);
            ball.pos = spot; ball.vel = Vector2.zero; ball.z = 0; ball.vz = 0; ball.spin = 0; ball.slideT = 0;
            ball.owner = taker.id;
            ball.lastTouchTeam = t;
            passReceiver = -1;
            offsideIds.Clear();
            offsideTeam = -1;
            float lineX = gx - dir * (mode.boxDepth + 2f);
            int k = 0;
            foreach (var p in players)
            {
                p.vel = Vector2.zero; p.stun = 0; p.slide = 0; p.hold = 0;
                if (p == taker)
                {
                    p.position = spot - new Vector2(dir * 0.7f, 0);
                    p.facing = new Vector2(dir, 0);
                }
                else if (p.isKeeper && p.team != t)
                {
                    p.position = new Vector2(gx - dir * 0.3f, 0);
                    p.facing = new Vector2(-dir, 0);
                }
                else if (p.isKeeper) continue;
                else
                {
                    int row = k / 6;
                    p.position = new Vector2(lineX - dir * (row * 1.5f + 0.5f), -halfW * 0.5f + (k % 6) * halfW * 0.2f);
                    p.facing = new Vector2(dir, 0);
                    k++;
                }
            }
            ctrl[t].player = taker.id;
            foreach (var c in ctrl) { c.charging = 0; c.pending = 0; }
            penaltyTeam = t;
            penaltyTaker = taker.id;
            penaltyT = 8f;
            protectTeam = t;
            protectT = 999f;
            state = MatchState.Restart;
            stateTimer = 1.4f;
            Emit("whistle", t, -1, "penalty");
        }

        void MarkOffside(SimPlayer p)
        {
            offsideIds.Clear();
            offsideTeam = -1;
            if (!mode.offside || noOffsideKick || penaltyTeam >= 0)
            {
                noOffsideKick = false;
                return;
            }
            float dir = TeamDir(p.team);
            var rels = new List<float>();
            foreach (var o in players) if (o.team != p.team) rels.Add(o.position.x * dir);
            rels.Sort();
            float secondLast = rels.Count >= 2 ? rels[rels.Count - 2] : halfLen;
            float line = Mathf.Max(secondLast, ball.pos.x * dir);
            foreach (var m in players)
            {
                if (m.team != p.team || m == p) continue;
                float r = m.position.x * dir;
                if (r > 0f && r > line + 0.3f) offsideIds.Add(m.id);
            }
            if (offsideIds.Count > 0) offsideTeam = p.team;
        }

        bool CheckOffside(SimPlayer p)
        {
            if (offsideTeam < 0) return false;
            if (p.team == offsideTeam && offsideIds.Contains(p.id))
            {
                Emit("offside", p.team, p.id);
                var spot = p.position;
                spot.x = Mathf.Clamp(spot.x, -halfLen + 1f, halfLen - 1f);
                spot.y = Mathf.Clamp(spot.y, -halfW + 1f, halfW - 1f);
                offsideIds.Clear();
                offsideTeam = -1;
                SetupRestart(1 - p.team, spot, "offside");
                return true;
            }
            offsideIds.Clear();
            offsideTeam = -1;
            return false;
        }

        // ------------------------------------------------------------ IA sem bola
        void AiAll(bool frozen)
        {
            foreach (var p in players)
            {
                bool human = ctrl[p.team].player == p.id;
                if (human && state == MatchState.Playing) continue;
                if (penaltyTeam >= 0 && !p.isKeeper)
                {
                    p.intent = Vector2.zero;
                    p.wantSprint = false;
                    continue;
                }
                if (p.isKeeper) AiKeeper(p, frozen);
                else AiOutfield(p, frozen);
            }
        }

        public Vector2 FormationTarget(SimPlayer p, bool attacking)
        {
            float dir = TeamDir(p.team);
            float bxRel = ball.pos.x * dir;
            float baseX = p.basePos.x * halfLen;
            // o bloco inteiro sobe e desce com a bola (ataque abre, defesa fecha)
            bool field = mode.id == GameModes.Field;
            float tx = baseX * (field ? 0.5f : 0.68f) + bxRel * (field ? 0.62f : 0.42f) + (attacking ? halfLen * 0.26f : -halfLen * 0.08f);
            tx += (teamMentality[p.team] - 1) * halfLen * 0.09f;
            if (!attacking) tx = Mathf.Min(tx, bxRel + 1f);
            tx = Mathf.Clamp(tx, -halfLen * 0.9f, halfLen * 0.86f);
            float ty = p.basePos.y * halfW * (attacking ? 0.9f : 0.55f) + ball.pos.y * (attacking ? 0.3f : 0.42f);
            ty = Mathf.Clamp(ty, -halfW * 0.92f, halfW * 0.92f);
            return new Vector2(tx * dir, ty);
        }

        void AiOutfield(SimPlayer p, bool frozen)
        {
            int t = p.team;
            bool attacking = TeamHasBall(t);
            var target = FormationTarget(p, attacking);
            bool urgent = false;
            if (!frozen && state == MatchState.Playing) target = SupportRun(p, target, attacking);
            if (!frozen && state == MatchState.Playing)
            {
                if (passReceiver == p.id && ball.owner < 0)
                {
                    target = InterceptPoint(p);
                    urgent = true;
                }
                else if (ball.owner < 0 && NearestToBall(t) == p && ctrl[t].player != p.id)
                {
                    var human = players[ctrl[t].player];
                    if (human.position.Dist(ball.pos) > p.position.Dist(ball.pos) + 3f)
                    {
                        target = InterceptPoint(p);
                        urgent = true;
                    }
                }
                else if (!attacking && ball.owner >= 0)
                {
                    var helper = NearestToBall(t, ctrl[t].player);
                    if (helper == p)
                    {
                        var ownGoal = new Vector2(-halfLen * TeamDir(t), 0);
                        if (ctrl[t].helpT > 0f) { target = ball.pos; urgent = true; }
                        else if (teamPress[t] == 2 && p.position.Dist(ball.pos) < halfLen * 0.45f) { target = ball.pos; urgent = true; }
                        else if (p.position.Dist(ball.pos) < halfLen * (0.3f + 0.15f * teamPress[t]))
                            target = ball.pos + (ownGoal - ball.pos).Norm() * (4.5f - 1.5f * teamPress[t]);
                    }
                }
            }
            // bola solta perto do gol é do goleiro: o zagueiro não disputa com ele
            if (!urgent || (ball.owner < 0 && KeeperClaims(t))) target = KeeperSpace(p, target);
            SteerTo(p, target, urgent);
        }

        /// <summary>
        /// Movimentação sem bola: atacantes atacam o espaço (até a linha do impedimento), meias
        /// se oferecem em diagonal para o portador e ninguém fica colado no companheiro.
        /// </summary>
        Vector2 SupportRun(SimPlayer p, Vector2 target, bool attacking)
        {
            float dir = TeamDir(p.team);
            if (attacking && ball.owner >= 0 && ball.owner != p.id)
            {
                var carrier = players[ball.owner];
                if (carrier.team == p.team)
                {
                    float d = p.position.Dist(carrier.position);
                    if (p.basePos.x > 0.25f)
                    {
                        // atacante: corre para frente, mas para na linha do penúltimo defensor
                        target.x += dir * Mathf.Lerp(2f, 6f, Mathf.Clamp01((halfLen * 0.7f - Mathf.Abs(carrier.position.x - halfLen * dir)) / (halfLen * 0.7f) + 0.5f));
                        if (mode.offside)
                        {
                            float line = OffsideLineX(p.team);
                            if ((target.x - line) * dir > -0.6f) target.x = line - dir * 0.6f;
                        }
                    }
                    else if (d < 16f && !p.isKeeper)
                    {
                        // linha de passe: em diagonal, do lado da posição de origem
                        float side = p.basePos.y >= 0f ? 1f : -1f;
                        var ang = new Vector2(dir, 0).Rotated(side * 0.75f);
                        var offer = carrier.position + ang * Mathf.Lerp(7f, 11f, p.passing);
                        target = Vector2.Lerp(target, offer, 0.55f);
                    }
                }
            }
            // espaço entre companheiros (sem bolo de jogadores)
            foreach (var m in players)
            {
                if (m == p || m.team != p.team || m.isKeeper) continue;
                var dv = target - m.position;
                float dd = dv.magnitude;
                float room = mode.id == GameModes.Field ? 6f : 4.2f;
                if (dd < room && dd > 0.01f) target += dv / dd * (room - dd) * 0.5f;
            }
            target.x = Mathf.Clamp(target.x, -halfLen + 0.5f, halfLen - 0.5f);
            target.y = Mathf.Clamp(target.y, -halfW + 0.5f, halfW - 0.5f);
            return target;
        }

        /// <summary>x do penúltimo defensor adversário (linha do impedimento) para o time t que ataca.</summary>
        float OffsideLineX(int t)
        {
            float dir = TeamDir(t);
            float first = -999f, second = -999f;
            foreach (var o in players)
            {
                if (o.team == t) continue;
                float x = o.position.x * dir;
                if (x > first) { second = first; first = x; }
                else if (x > second) second = x;
            }
            float line = Mathf.Max(second, ball.pos.x * dir, 0f);
            return line * dir;
        }

        /// <summary>Há companheiro livre bem mais adiante (para a CPU jogar para frente)?</summary>
        public bool HasForwardOption(SimPlayer p)
        {
            float dir = TeamDir(p.team);
            foreach (var m in players)
            {
                if (m.team != p.team || m == p || m.isKeeper) continue;
                float ahead = (m.position.x - p.position.x) * dir;
                if (ahead < (mode.id == GameModes.Field ? 7f : 3.5f)) continue;
                bool free = true;
                foreach (var o in players)
                    if (o.team != p.team && (o.position.Dist(m.position) < 2.5f || V2.ClosestOnSegment(o.position, p.position, m.position).Dist(o.position) < 1.3f)) { free = false; break; }
                if (free) return true;
            }
            return false;
        }

        bool KeeperClaims(int t)
        {
            foreach (var k in players)
                if (k.isKeeper && k.team == t && ball.pos.Dist(k.position) < 3f) return true;
            return false;
        }

        Vector2 InterceptPoint(SimPlayer p)
        {
            float mySpeed = p.TopSpeed();
            var best = ball.pos;
            var simPos = ball.pos;
            var v = ball.vel;
            float roll = mode.rollDecel;
            for (int i = 0; i < 30; i++)
            {
                float t = (i + 1) * 0.1f;
                float s = v.magnitude;
                if (s > 0f) v *= Mathf.Max(0f, s - roll * 0.1f) / s;
                simPos += v * 0.1f;
                best = simPos;
                if (p.position.Dist(simPos) / mySpeed <= t) break;
            }
            return best;
        }

        void SteerTo(SimPlayer p, Vector2 target, bool urgent)
        {
            var to = target - p.position;
            float d = to.magnitude;
            bool moving = p.vel.magnitude > 0.5f;
            if (d < (moving ? 0.3f : 0.6f))
            {
                p.intent = Vector2.zero;
                if (ball.pos.Dist(p.position) > 0.1f)
                    p.facing = V2.RotateToward(p.facing, (ball.pos - p.position).Norm(), 0.05f);
            }
            else p.intent = (to / d) * Mathf.Clamp(d / (urgent ? 1.2f : 4f), 0.15f, 1f);   // indo na bola: chega forte, não freia antes
            p.wantSprint = urgent || d > (p.sprinting ? 7f : 11f);
            p.faceBall = !urgent && d < 10f && ball.pos.Dist(p.position) < 35f;
        }

        void AiKeeper(SimPlayer p, bool frozen)
        {
            float dir = TeamDir(p.team);
            float gx = -halfLen * dir;
            var goal = new Vector2(gx, 0);
            if (penaltyTeam >= 0 && p.team != penaltyTeam)
            {
                SteerTo(p, new Vector2(gx + dir * 0.3f, 0), false);
                return;
            }
            if (ball.owner == p.id)
            {
                p.intent = Vector2.zero;
                p.facing = new Vector2(dir, 0);
                if (!frozen && p.hold > 0f)
                {
                    p.hold -= DT;
                    if (p.hold <= 0f) KeeperDistribute(p);
                }
                else if (!frozen && state == MatchState.Playing) Pass(p, new Vector2(dir, 0), 0.5f, false, true);
                return;
            }
            Vector2 target = goal;
            bool urgent = false;
            var toBall = ball.pos - goal;
            bool threat = false;
            // reação: o goleiro só lê a trajetória ~0,15 s depois do chute
            bool reacting = tick - lastShotTick < 9;
            if (ball.owner < 0 && ball.vel.x * dir < -5f && !reacting)
            {
                float tCross = (gx - ball.pos.x) / ball.vel.x;
                if (tCross > 0f && tCross < 2.2f)
                {
                    float yCross = ball.pos.y + ball.vel.y * tCross;
                    if (Mathf.Abs(yCross) < goalHalf + 1.5f)
                    {
                        threat = true;
                        target = new Vector2(gx + dir * 0.7f, Mathf.Clamp(yCross, -goalHalf, goalHalf));
                        urgent = true;
                        if (p.dive <= 0f && p.position.Dist(target) > 1.1f && tCross < 0.7f)
                        {
                            p.dive = 0.6f;
                            Emit("dive", p.team, p.id);
                        }
                    }
                }
            }
            if (!threat)
            {
                float outD = Mathf.Clamp(toBall.magnitude * 0.12f, 0.6f, mode.boxDepth * 0.3f);
                target = toBall.magnitude > 0.1f ? goal + toBall.normalized * outD : goal + new Vector2(dir, 0);
                target.y = Mathf.Clamp(target.y, -goalHalf - 0.5f, goalHalf + 0.5f);
                if (ball.owner < 0 && Mathf.Abs(ball.pos.x - gx) < mode.boxDepth && Mathf.Abs(ball.pos.y) < mode.boxHalf)
                {
                    var n = NearestToBall(1 - p.team);
                    if (n == null || n.position.Dist(ball.pos) > p.position.Dist(ball.pos) - 1f)
                    {
                        target = InterceptPoint(p);
                        urgent = true;
                    }
                }
            }
            SteerTo(p, target, urgent);
        }

        /// <summary>Goleiro solta a bola para o companheiro mais livre (curto se der, longo se não).</summary>
        void KeeperDistribute(SimPlayer p)
        {
            float dir = TeamDir(p.team);
            SimPlayer best = null;
            float bestS = float.MinValue;
            foreach (var m in players)
            {
                if (m.team != p.team || m == p) continue;
                float free = float.MaxValue;
                foreach (var o in players) if (o.team != p.team) free = Mathf.Min(free, o.position.Dist(m.position));
                float d = m.position.Dist(p.position);
                float ahead = (m.position.x - p.position.x) * dir;
                if (ahead < 2f) continue;   // nunca para trás nem para o lado colado no gol
                float sc = Mathf.Min(free, 10f) - d * 0.06f + ahead * 0.03f + rng.Range(0f, 1.5f);
                if (sc > bestS) { bestS = sc; best = m; }
            }
            if (best == null) { Pass(p, new Vector2(dir, rng.Range(-0.3f, 0.3f)), 0.75f, false, true); return; }
            float dist = best.position.Dist(p.position);
            bool lofted = dist > halfLen * 0.55f;
            float power = Mathf.Clamp(dist / (halfLen * (lofted ? 1.3f : 0.9f)), 0.35f, 0.9f);
            Pass(p, best.position + best.vel * 0.4f - p.position, power, false, lofted);
        }

        /// <summary>Defensores não invadem o espaço do goleiro (nem ficam colados nele).</summary>
        Vector2 KeeperSpace(SimPlayer p, Vector2 target)
        {
            foreach (var k in players)
            {
                if (!k.isKeeper || k.team != p.team) continue;
                float room = ball.owner == k.id ? 3f : 1.8f;
                var d = target - k.position;
                if (d.magnitude < room)
                    target = k.position + (d.magnitude > 0.05f ? d.normalized : new Vector2(TeamDir(p.team), p.basePos.y >= 0 ? 0.5f : -0.5f).Norm()) * room;
            }
            return target;
        }

        // ------------------------------------------------------------ movimento
        void MovePlayer(SimPlayer p)
        {
            p.kickCd = Mathf.Max(0f, p.kickCd - DT);
            p.tackleAnim = Mathf.Max(0f, p.tackleAnim - DT);
            p.kickAnim = Mathf.Max(0f, p.kickAnim - DT);
            p.dive = Mathf.Max(0f, p.dive - DT);
            p.skillCd = Mathf.Max(0f, p.skillCd - DT);
            p.burst = Mathf.Max(0f, p.burst - DT);
            p.offBalance = Mathf.Max(0f, p.offBalance - DT);
            p.touchT = Mathf.Max(0f, p.touchT - DT);
            if (p.windup > 0f)
            {
                p.windup -= DT;
                if (p.windup <= 0f)
                {
                    p.windup = 0f;
                    if (ball.owner == p.id && state == MatchState.Playing)
                        DoKick(p, p.windupKind, p.windupPower, p.windupAim, p.windupFinesse);
                }
            }
            if (p.skill != Skill.None)
            {
                if (p.stun > 0f || p.slide > 0f || (p.skill != Skill.Knock && ball.owner != p.id)) p.skill = Skill.None;
                else if (p.skill == Skill.Knock)
                {
                    p.skillT += DT;
                    if (p.skillT >= SimPlayer.SkillTime[(int)Skill.Knock])
                    {
                        p.skill = Skill.None;
                        p.skillCd = 0.25f;
                    }
                }
            }
            if (p.skill != Skill.None && p.skill != Skill.Knock)
            {
                SkillMove(p);
                ClampPlayer(p);
                return;
            }
            if (p.stun > 0f)
            {
                p.stun -= DT;
                p.vel = p.vel.MoveToward(Vector2.zero, 14f * DT);
                p.position += p.vel * DT;
                p.sprinting = false;
                ClampPlayer(p);
                return;
            }
            if (p.slide > 0f)
            {
                p.slide -= DT;
                p.vel = p.vel.MoveToward(Vector2.zero, 9f * DT);
                p.position += p.vel * DT;
                if (p.slide <= 0f) p.stun = 0.45f;
                ClampPlayer(p);
                return;
            }
            bool hasBall = ball.owner == p.id;
            var intent = p.intent.Limit(1f);
            p.sprinting = p.wantSprint && p.stamina > 0.05f && intent.magnitude > 0.3f;
            float maxS = p.sprinting ? p.TopSpeed() : p.JogSpeed();
            maxS *= 0.8f + 0.2f * p.stamina;
            if (hasBall)
            {
                maxS *= Mathf.Lerp(0.86f, 0.94f, p.control);
                if (p.shielding) maxS = Mathf.Min(maxS, p.JogSpeed() * 0.6f);
            }
            else if (p.shielding && ball.owner >= 0 && players[ball.owner].team != p.team)
                maxS = Mathf.Min(maxS, p.JogSpeed() * 0.78f);
            if (p.windup > 0f) maxS *= 0.55f;
            if (p.faceBall && !hasBall && intent.magnitude > 0.1f)
            {
                float back = Mathf.Clamp01(-Vector2.Dot(intent.normalized, p.facing));
                float side = 1f - Mathf.Abs(Vector2.Dot(intent.normalized, p.facing));
                maxS *= 1f - 0.4f * back - 0.15f * side;
            }
            if (p.burst > 0f) maxS *= 1.07f;
            if (p.offBalance > 0f) maxS *= 0.3f;
            if (p.isKeeper && p.dive > 0f) maxS = Mathf.Lerp(6f, 7.5f, p.defense);
            if (p.hold > 0f) maxS *= 0.4f;
            var targetV = intent * maxS;
            float spd = p.vel.magnitude;
            float acc = Mathf.Lerp(5f, 8.5f, p.accel) * (p.burst > 0f ? 1.6f : 1f) * (p.offBalance > 0f ? 0.4f : 1f) * (0.82f + 0.18f * p.stamina);
            float rate = acc;
            if (targetV.magnitude < spd - 0.2f || Vector2.Dot(targetV, p.vel) < 0f) rate = 12f;
            if (spd > 3f && targetV.magnitude > 0.1f)
            {
                float turn = Mathf.Abs(p.vel.AngleTo(targetV));
                if (turn > 0.8f) rate *= Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((spd - 3f) / 5f));
            }
            p.vel += (targetV - p.vel).Limit(rate * DT);
            p.position += p.vel * DT;
            var faceTarget = p.vel.magnitude > 0.4f ? p.vel : intent;
            if (p.faceBall && !hasBall && !p.sprinting && ctrl[p.team].player != p.id) faceTarget = ball.pos - p.position;
            float turnRate = Mathf.Lerp(11f, 4f, Mathf.Clamp01(p.vel.magnitude / 9f)) * (hasBall ? 0.8f : 1f);
            if (p.shielding && !p.isKeeper)
            {
                if (hasBall)
                {
                    var opp = NearestOpponent(p);
                    if (opp != null && opp.position.Dist(p.position) < 2f && intent.magnitude < 0.5f)
                    {
                        faceTarget = p.position - opp.position;
                        turnRate = 7f;
                    }
                }
                else if (ball.owner >= 0 && players[ball.owner].team != p.team)
                {
                    faceTarget = ball.pos - p.position;
                    turnRate = 12f;
                }
            }
            if (p.windup > 0f) turnRate *= 0.4f;
            var nf = V2.RotateToward(p.facing, faceTarget.Norm(), turnRate * DT).Norm();
            if (nf != Vector2.zero) p.facing = nf;
            if (p.sprinting) p.stamina = Mathf.Max(0f, p.stamina - 0.055f * DT * (p.Has(Trait.Resistencia) ? 0.65f : 1f));
            else p.stamina = Mathf.Min(1f, p.stamina + 0.03f * DT);
            p.runPhase += p.vel.magnitude * DT * 2.3f;
            ClampPlayer(p);
        }

        void ClampPlayer(SimPlayer p)
        {
            // ninguém encosta no goleiro adversário enquanto ele segura a bola
            if (ball.owner >= 0 && ball.owner != p.id)
            {
                var gk = players[ball.owner];
                if (gk.hold > 0f && gk.team != p.team)
                {
                    var d = p.position - gk.position;
                    if (d.magnitude < 3f) p.position = gk.position + (d.magnitude > 0.1f ? d.normalized : new Vector2(TeamDir(gk.team), 0)) * 3f;
                }
            }
            float m = mode.runMargin, r = SimPlayer.Radius;
            p.position.x = Mathf.Clamp(p.position.x, -halfLen - m + r, halfLen + m - r);
            p.position.y = Mathf.Clamp(p.position.y, -halfW - m + r, halfW + m - r);
        }

        void ResolveCollisions()
        {
            int n = players.Count;
            float minD = SimPlayer.Radius * 2f;
            for (int i = 0; i < n; i++)
            {
                var a = players[i];
                for (int j = i + 1; j < n; j++)
                {
                    var b = players[j];
                    var d = b.position - a.position;
                    float l = d.magnitude;
                    if (l < minD && l > 0.0001f)
                    {
                        var push = d / l * (minD - l) * 0.5f;
                        float wa = 0.5f + Mathf.Clamp((b.vel.magnitude - a.vel.magnitude) * 0.05f, -0.3f, 0.3f);
                        // trombada entre adversários: os dois perdem velocidade; quem vem mais fraco se desequilibra
                        if (a.team != b.team)
                        {
                            float closing = Vector2.Dot(b.vel - a.vel, -d / l);
                            if (closing > 2.5f)
                            {
                                float sa = a.defense + a.vel.magnitude * 0.05f + rng.Range(0f, 0.3f), sb = b.defense + b.vel.magnitude * 0.05f + rng.Range(0f, 0.3f);
                                var weak = sa < sb ? a : b;
                                a.vel *= 0.75f; b.vel *= 0.75f;
                                weak.offBalance = Mathf.Max(weak.offBalance, 0.3f);
                                // quem conduz pode perder a bola no choque
                                if (ball.owner == weak.id && closing > 4f && rng.Chance(0.45f * (1.2f - weak.control)) && !weak.isKeeper)
                                {
                                    ball.owner = -1;
                                    ball.vel = weak.vel + (d / l * (weak == a ? -1f : 1f)).Rotated(rng.Range(-0.6f, 0.6f)) * rng.Range(2f, 4f);
                                    weak.kickCd = 0.3f;
                                }
                            }
                        }
                        // goleiro (sobretudo com a bola) não é empurrado: quem sai é o outro
                        if (a.isKeeper && !b.isKeeper) wa = a.hold > 0f ? 0f : 0.2f;
                        else if (b.isKeeper && !a.isKeeper) wa = b.hold > 0f ? 1f : 0.8f;
                        a.position -= push * 2f * wa;
                        b.position += push * 2f * (1f - wa);
                    }
                }
            }
        }

        // ------------------------------------------------------------ bola
        void Dribble()
        {
            if (ball.owner < 0) return;
            var o = players[ball.owner];
            if (o.isKeeper && o.hold > 0f)
            {
                ball.pos = o.position + o.facing * 0.35f;
                ball.pos.x = Mathf.Clamp(ball.pos.x, -halfLen + ball.radius, halfLen - ball.radius);
                ball.z = 1f;
                ball.vz = 0f;
                ball.vel = o.vel;
                return;
            }
            float spd = o.vel.magnitude;
            float lose = spd > 5f ? 2.4f : 1.7f;
            if (o.stun > 0f || o.slide > 0f || o.position.Dist(ball.pos) > lose)
            {
                ball.owner = -1;
                return;
            }
            if (o.skill != Skill.None)
            {
                SkillBall(o);
                return;
            }
            // condução em toques: em velocidade a bola é empurrada à frente e o atleta corre atrás dela
            if (spd > 3.2f && !o.shielding && o.windup <= 0f && ball.z < 0.35f)
            {
                var fwd = o.facing;
                var perp = new Vector2(-fwd.y, fwd.x);
                var rel = ball.pos - o.position;
                float along = Vector2.Dot(rel, fwd), side = Vector2.Dot(rel, perp);
                if (along < 0.3f + spd * 0.012f)
                {
                    // toque: a bola sai um pouco à frente (mais no pique e para quem tem menos controle)
                    float lead = o.sprinting ? Mathf.Lerp(0.9f, 0.5f, o.control) : Mathf.Lerp(0.42f, 0.25f, o.control);
                    float decel = mode.rollDecel + mode.groundDrag * spd * spd;
                    float extra = Mathf.Sqrt(2f * decel * lead);
                    ball.vel = fwd * (spd + extra) - perp * side * 4f;
                    ball.z = 0f;
                    ball.vz = 0f;
                    ball.spin = 0f;
                    ball.slideT = 0f;
                    o.touchT = 0.18f;
                }
                else ball.vel -= perp * side * 5f * DT;   // entre os toques a bola rola solta (leve correção de rumo)
                return;
            }
            float ahead = 0.42f + spd * 0.07f + (o.sprinting ? 0.45f : 0f);
            if (o.shielding) ahead = 0.34f + spd * 0.03f;
            if (o.windup > 0f) ahead = 0.4f;
            var target = o.position + o.facing * ahead;
            float k = 6f + 6f * o.control;
            var desired = o.vel + (target - ball.pos) * k;
            float blend = Mathf.Clamp01((0.22f + 0.3f * o.control) * 60f * DT);
            ball.vel = Vector2.Lerp(ball.vel, desired, blend);
            if (ball.z < 0.35f) { ball.z = 0f; ball.vz = 0f; }
            ball.spin = 0f;
        }

        void Possession()
        {
            SimPlayer best = null;
            float bestD = float.MaxValue;
            float ownerD = ball.owner >= 0 ? players[ball.owner].position.Dist(ball.pos) : float.MaxValue;
            foreach (var p in players)
            {
                if (p.kickCd > 0f || p.stun > 0f || p.slide > 0f || p.id == ball.owner) continue;
                if (protectT > 0f && p.team != protectTeam) continue;
                bool keeperZone = p.isKeeper && InOwnBox(p);
                float reach = 0.55f + 0.25f * p.control;
                float maxZ = 0.9f;
                if (keeperZone)
                {
                    float gs = Mathf.Clamp(goalHalf / 3.66f, 0.6f, 1f);   // gol pequeno (futsal) = alcance proporcional
                    reach = (0.9f + (p.dive > 0f ? Mathf.Lerp(0.45f, 1.1f, p.dive / 0.6f) * (0.8f + 0.3f * p.defense) : 0f) + (p.Has(Trait.Reflexo) ? 0.15f : 0f)) * gs;
                    maxZ = 2.6f;
                }
                float d = p.position.Dist(ball.pos);
                if (d > reach) continue;
                // o marcador colado no passador não "pega" a bola no instante em que ela sai do pé
                if (tick - passTick < 8 && ball.owner < 0 && p.team != ball.lastTouchTeam && p.position.Dist(passFrom) < 1.3f) continue;
                // bola nas mãos do goleiro ou no pé de um companheiro: ninguém cabeceia/rouba
                if (ball.owner >= 0 && (players[ball.owner].hold > 0f || players[ball.owner].team == p.team)) continue;
                if (ball.z > maxZ)
                {
                    // entre a coxa e o peito: domina no peito (amortece e cai no pé)
                    float ownGx = -halfLen * TeamDir(p.team);
                    bool nearOwnGoal = Mathf.Abs(ball.pos.x - ownGx) < Mathf.Max(6f, mode.boxDepth) && Mathf.Abs(ball.pos.y) < Mathf.Max(goalHalf * 2.5f, mode.boxHalf);
                    if (ball.z < 1.25f && !keeperZone && ball.Speed < 16f && ball.owner < 0 && !nearOwnGoal)
                    {
                        ChestControl(p);
                        return;
                    }
                    // cabeceio: alcance depende da impulsão (atributo de defesa/velocidade e habilidade)
                    float jump = 2.25f + 0.3f * Mathf.Max(p.defense, p.pace) + (p.Has(Trait.Cabeceador) ? 0.3f : 0f);
                    if (ball.z < jump && !keeperZone)
                    {
                        Header(p);
                        return;
                    }
                    continue;
                }
                if (ball.owner >= 0)
                {
                    var o = players[ball.owner];
                    if (o.team == p.team || o.hold > 0f) continue;
                    if (d >= ownerD - 0.05f) continue;
                    if (tick - takeTick < 20) continue;   // quem acabou de dominar tem um instante
                }
                if (d < bestD) { bestD = d; best = p; }
            }
            if (best == null) return;
            if (ball.owner >= 0 && players[ball.owner].team != best.team && !best.isKeeper) Duel(best, players[ball.owner]);
            else Take(best);
        }

        /// <summary>
        /// Dividida: quem chega disputa com o dono da bola (marcação x controle, corpo, proteção).
        /// Vence: rouba. Empate: a bola espirra. Perde: o marcador fica para trás.
        /// </summary>
        void Duel(SimPlayer p, SimPlayer o)
        {
            float atk = p.defense * 0.55f + p.pace * 0.1f + rng.Range(0f, 0.5f) + (p.Has(Trait.Muralha) ? 0.12f : 0f);
            float def = o.control * 0.55f + (o.shielding ? 0.3f : 0f) + rng.Range(0f, 0.55f) - (o.sprinting ? 0.06f : 0f);
            float diff = atk - def;
            if (diff > 0.12f)
            {
                Take(p);
                o.offBalance = Mathf.Max(o.offBalance, 0.35f);
                Emit("steal", p.team, p.id);
            }
            else if (diff > -0.08f)
            {
                // bola espirra entre os dois
                var away = (ball.pos - (p.position + o.position) * 0.5f);
                var dirv = away.sqrMagnitude > 0.01f ? away.Norm() : new Vector2(rng.Range(-1f, 1f), rng.Range(-1f, 1f)).Norm();
                dirv = dirv.Rotated(rng.Range(-0.9f, 0.9f));
                ball.owner = -1;
                ball.vel = dirv * rng.Range(3f, 6f);
                ball.vz = rng.Chance(0.3f) ? rng.Range(1f, 2.5f) : 0f;
                ball.lastTouchTeam = p.team;
                ball.lastTouchId = p.id;
                p.kickCd = 0.3f;
                o.kickCd = 0.25f;
                Emit("block", p.team, p.id);
            }
            else
            {
                p.kickCd = 0.6f;
                p.offBalance = Mathf.Max(p.offBalance, 0.3f);
                p.vel *= 0.6f;
            }
        }

        void Take(SimPlayer p)
        {
            if (CheckOffside(p)) return;
            float rel = (ball.vel - p.vel).magnitude;
            bool keeperZone = p.isKeeper && InOwnBox(p);
            float limit = 9f + 12f * p.control;
            if (keeperZone)
            {
                float catchP = Mathf.Clamp(1.25f - rel / 24f + p.defense * 0.15f + (p.Has(Trait.GoleiroSeguro) ? 0.12f : 0f) - (p.dive > 0f ? 0.15f : 0f), 0.1f, 0.97f);
                if (rel > 8f && !rng.Chance(catchP))
                {
                    // espalmada: a bola volta para o campo e abre para o lado (escanteio ou rebote longe do gol)
                    float sideY = Mathf.Abs(ball.pos.y) > 0.3f ? Mathf.Sign(ball.pos.y) : (rng.Chance(0.5f) ? 1f : -1f);
                    ball.vel = new Vector2(-Mathf.Sign(ball.vel.x) * Mathf.Max(Mathf.Abs(ball.vel.x) * 0.3f, 4f), ball.vel.y * 0.3f + sideY * rng.Range(4f, 8f));
                    ball.vz = Mathf.Abs(ball.vz) * 0.2f + 0.8f;
                    ball.lastTouchTeam = p.team;
                    ball.lastTouchId = p.id;
                    p.kickCd = 0.35f;
                    ball.owner = -1;
                    Emit("save", p.team, p.id, ok: false);
                    return;
                }
                ball.owner = p.id;
                p.hold = 2.2f;
                p.vel *= 0.3f;
                // goleiro com a bola nas mãos: adversários se afastam (não podem disputar)
                foreach (var o in players)
                {
                    if (o.team == p.team) continue;
                    var d = o.position - p.position;
                    if (d.magnitude < 4f) o.position = p.position + (d.magnitude > 0.1f ? d.normalized : new Vector2(TeamDir(p.team), 0)) * 4f;
                }
                ball.lastTouchTeam = p.team;
                ball.lastTouchId = p.id;
                passReceiver = -1;
                if (rel > 8f) Emit("save", p.team, p.id, ok: true);
                return;
            }
            if (rel > limit)
            {
                // bola rápida demais: bate no corpo e desvia conforme o ângulo (domínio falho)
                var nrm = (ball.pos - p.position).Norm();
                if (nrm == Vector2.zero) nrm = p.facing;
                var vrel = ball.vel - p.vel;
                float vn = Vector2.Dot(vrel, nrm);
                var refl = vn < 0f ? vrel - nrm * vn * 1.35f : vrel;
                ball.vel = p.vel + refl * 0.45f + p.facing * 0.8f;
                ball.slideT = 0.12f;
                ball.vz = Mathf.Abs(ball.vz) * 0.3f;
                ball.lastTouchTeam = p.team;
                ball.lastTouchId = p.id;
                p.kickCd = 0.2f;
                if (ball.owner >= 0) ball.owner = -1;
                Emit("touch", p.team, p.id);
                return;
            }
            int prevOwner = ball.owner;
            ball.owner = p.id;
            takeTick = tick;
            var want = p.intent;
            if (p.shielding) ball.vel = p.vel * 0.5f;
            else if (want.magnitude > 0.35f && rel > 2f)
            {
                float miss = Mathf.Clamp((rel - 6f) / 18f, 0f, 0.7f) * (1.15f - p.control);
                var d = want.normalized.Rotated(rng.Gauss() * miss * 0.9f);
                ball.vel = p.vel * 0.5f + d * (2f + (p.wantSprint ? 2.6f : 0.6f));
                p.facing = V2.RotateToward(p.facing, d, 1.2f);
            }
            else ball.vel = p.vel * 0.85f + (ball.vel - p.vel) * 0.12f;
            if (rel > 2f) p.touchT = 0.3f;
            ball.vz = 0f;
            ball.lastTouchTeam = p.team;
            ball.lastTouchId = p.id;
            passReceiver = -1;
            if (ctrl[p.team].player != p.id)
            {
                ctrl[p.team].player = p.id;
                ctrl[p.team].charging = 0;
            }
            if (prevOwner >= 0 && players[prevOwner].team != p.team) Emit("steal", p.team, p.id);
        }

        // ------------------------------------------------------------ dribles
        void StartSkill(SimPlayer p, Vector2 rs)
        {
            if (p.skill != Skill.None || p.skillCd > 0f || p.windup > 0f || p.hold > 0f || p.stun > 0f) return;
            var fwd = p.facing.Norm();
            var perp = new Vector2(-fwd.y, fwd.x);
            float f = Vector2.Dot(rs, fwd);
            float side = Vector2.Dot(rs, perp);
            Skill kind;
            if (f > 0.6f) kind = Skill.Knock;
            else if (f < -0.6f) kind = p.shielding ? Skill.Roulette : Skill.Drag;
            else kind = p.shielding ? Skill.Stepover : Skill.Roll;
            p.skill = kind;
            p.skillT = 0f;
            p.skillDir = fwd;
            p.skillSide = Mathf.Abs(side) > 0.1f ? Mathf.Sign(side) : 1f;
            Emit("skill", p.team, p.id, skill: (int)kind);
            if (kind == Skill.Knock)
            {
                ball.owner = -1;
                ball.vel = fwd * (p.vel.magnitude + 4.2f + 1.6f * p.control);
                ball.lastTouchTeam = p.team;
                ball.lastTouchId = p.id;
                p.kickCd = 0.16f;
                p.kickAnim = 0.25f;
                p.burst = 0.75f;
            }
            else if (kind == Skill.Stepover)
            {
                foreach (var o in players)
                {
                    if (o.team == p.team || o.isKeeper || o.stun > 0f) continue;
                    var to = o.position - p.position;
                    if (to.magnitude < 3.4f && Vector2.Dot(to, fwd) > 0f)
                    {
                        float chance = 0.3f + 0.45f * p.control - 0.3f * o.defense + (p.Has(Trait.Driblador) ? 0.15f : 0f);
                        if (rng.Chance(chance))
                        {
                            o.offBalance = 0.65f;
                            o.vel += perp * p.skillSide * 2.2f;
                            Emit("fooled", o.team, o.id);
                        }
                    }
                }
            }
        }

        void SkillMove(SimPlayer p)
        {
            p.skillT += DT;
            float dur = SimPlayer.SkillTime[(int)p.skill];
            float u = Mathf.Clamp01(p.skillT / dur);
            var fwd = p.skillDir;
            var perp = new Vector2(-fwd.y, fwd.x) * p.skillSide;
            var v = Vector2.zero;
            switch (p.skill)
            {
                case Skill.Drag:
                    v = -fwd * 1.6f * Mathf.Sin(u * Mathf.PI);
                    p.facing = V2.RotateToward(p.facing, u < 0.5f ? perp : -fwd, 10f * DT).Norm();
                    break;
                case Skill.Roll:
                    v = perp * Mathf.Lerp(1.2f, 4.6f, Mathf.Sin(u * Mathf.PI * 0.5f)) + fwd * 1.2f;
                    if (u > 0.55f) p.facing = V2.RotateToward(p.facing, (fwd + perp).Norm(), 7f * DT).Norm();
                    break;
                case Skill.Stepover:
                    v = fwd * 1.3f + perp * Mathf.Sin(u * Mathf.PI * 2f) * 0.9f;
                    break;
                case Skill.Roulette:
                    v = perp * 2.6f * Mathf.Sin(u * Mathf.PI) + fwd * 0.8f;
                    p.facing = fwd.Rotated(-p.skillSide * Mathf.PI * 2f * u);
                    break;
            }
            p.vel = Vector2.Lerp(p.vel, v, Mathf.Min(1f, 18f * DT));
            p.position += p.vel * DT;
            if (u >= 1f)
            {
                var exit = fwd;
                if (p.skill == Skill.Drag) exit = -fwd;
                else if (p.skill == Skill.Roll) exit = (fwd + perp).Norm();
                else if (p.skill == Skill.Stepover)
                {
                    exit = (fwd - perp * 0.9f).Norm();
                    p.burst = 0.5f;
                }
                p.facing = exit;
                p.vel = exit * Mathf.Max(p.vel.magnitude, 3.2f);
                p.skill = Skill.None;
                p.skillCd = 0.3f;
            }
        }

        void SkillBall(SimPlayer o)
        {
            float dur = SimPlayer.SkillTime[(int)o.skill];
            float u = Mathf.Clamp01(o.skillT / dur);
            var fwd = o.skillDir;
            var perp = new Vector2(-fwd.y, fwd.x) * o.skillSide;
            var target = o.position + o.facing * 0.42f;
            switch (o.skill)
            {
                case Skill.Drag: target = o.position + fwd * Mathf.Lerp(0.45f, -0.5f, u); break;
                case Skill.Roll: target = o.position + fwd * 0.35f + perp * Mathf.Lerp(-0.15f, 0.6f, u); break;
                case Skill.Stepover: target = o.position + fwd * 0.45f; break;
                case Skill.Roulette: target = o.position + fwd * 0.3f * Mathf.Cos(u * Mathf.PI) + perp * 0.35f * Mathf.Sin(u * Mathf.PI); break;
            }
            ball.vel = o.vel + (target - ball.pos) * 14f;
            ball.z = 0f;
            ball.vz = 0f;
            ball.spin = 0f;
        }

        /// <summary>Domínio no peito: a bola perde velocidade e cai à frente do atleta.</summary>
        void ChestControl(SimPlayer p)
        {
            if (CheckOffside(p)) return;
            var dir = p.intent.magnitude > 0.3f ? p.intent.normalized : p.facing;
            // nunca amortece em direção ao próprio gol
            var toOwn = (new Vector2(-halfLen * TeamDir(p.team), 0f) - ball.pos).Norm();
            if (Vector2.Dot(dir, toOwn) > 0f) dir = (dir - toOwn * Vector2.Dot(dir, toOwn) * 1.5f).Norm();
            if (dir == Vector2.zero) dir = new Vector2(TeamDir(p.team), 0f);
            float soft = Mathf.Lerp(0.2f, 0.08f, p.control);
            ball.vel = dir * (1.2f + ball.Speed * soft + Mathf.Max(0f, Vector2.Dot(p.vel, dir)) * 0.7f);
            ball.vz = -0.5f;
            ball.lastTouchTeam = p.team;
            ball.lastTouchId = p.id;
            p.kickCd = 0.15f;
            p.touchT = 0.35f;
            if (ball.owner >= 0 && players[ball.owner].team != p.team) Emit("steal", p.team, p.id);
            ball.owner = -1;
            passReceiver = p.id;
            passReceiverT = 0.8f;
            Emit("touch", p.team, p.id, "chest");
        }

        void Header(SimPlayer p)
        {
            if (CheckOffside(p)) return;
            var c = ctrl[p.team];
            float dir = TeamDir(p.team);
            Vector2 outV;
            bool nearGoal = Mathf.Abs(ball.pos.x - halfLen * dir) < halfLen * 0.35f;
            float power;
            if (c.shootHeld || c.pending == InputFrame.Shoot || (nearGoal && c.player != p.id))
            {
                // cabeceio para o gol: mira imperfeita (pode sair), mais precisa para quem cabeceia bem
                float spread = goalHalf * (p.Has(Trait.Cabeceador) ? 0.85f : 1.1f);
                // colado na linha de fundo: só dá para empurrar para o meio do gol
                if (Mathf.Abs(ball.pos.x - halfLen * dir) < 1.5f) spread = goalHalf * 0.4f;
                outV = (new Vector2(halfLen * dir, rng.Range(-spread, spread)) - ball.pos).Norm();
                // corpo virado para longe do gol = cabeçada fraca e torta
                float body = Mathf.Abs(p.facing.AngleTo(outV));
                outV = outV.Rotated(rng.Gauss() * (0.05f + body * 0.12f));
                ball.vz = rng.Range(-1.5f, 1.2f);
                power = Mathf.Lerp(0.65f, 0.4f, Mathf.Clamp01(body / 1.6f)) * (p.Has(Trait.Cabeceador) ? 1.15f : 1f);
                c.pending = 0;
            }
            else
            {
                outV = p.facing;
                ball.vz = 1.5f;
                power = 0.55f;
                // perto do próprio gol: o cabeceio afasta (para frente e para o lado), nunca na direção do gol
                float ownGx = -halfLen * dir;
                var toOwn = new Vector2(ownGx, 0f) - ball.pos;
                if (Mathf.Abs(ball.pos.x - ownGx) < halfLen * 0.4f || Vector2.Dot(outV, toOwn.Norm()) > 0.2f)
                {
                    var away = new Vector2(dir, Mathf.Sign(ball.pos.y == 0f ? 1f : ball.pos.y) * 0.8f).Norm();
                    outV = (away + p.facing * 0.3f).Norm();
                    if (Vector2.Dot(outV, toOwn.Norm()) > 0f) outV = away;
                    ball.vz = 2.5f;
                    power = 0.7f;
                }
            }
            float run = Mathf.Max(0f, Vector2.Dot(p.vel, outV)) * 0.6f;
            ball.vel = outV * Mathf.Clamp(ball.Speed * power + run + 3f, 7f, 21f);
            ball.owner = -1;
            ball.lastTouchTeam = p.team;
            ball.lastTouchId = p.id;
            p.kickCd = 0.4f;
            Emit("header", p.team, p.id);
        }

        void BallPhysics()
        {
            if (ball.owner >= 0 && players[ball.owner].hold > 0f) return;
            var prev = ball.pos;
            if (ball.Step(DT, mode) && ball.Speed > 4f) Emit("bounce");
            Posts();
            GoalAndBounds(prev);
        }

        void Posts()
        {
            if (ball.z > goalH) return;
            foreach (float side in new[] { -1f, 1f })
                foreach (float py in new[] { -goalHalf, goalHalf })
                {
                    var post = new Vector2(halfLen * side, py);
                    var d = ball.pos - post;
                    float minD = ball.radius + 0.06f;
                    if (d.magnitude < minD && d.magnitude > 0.0001f)
                    {
                        var nrm = d.normalized;
                        ball.pos = post + nrm * minD;
                        float vn = Vector2.Dot(ball.vel, nrm);
                        if (vn < 0f)
                        {
                            ball.vel -= nrm * vn * 1.75f;
                            Emit("post");
                        }
                    }
                }
        }

        void GoalAndBounds(Vector2 prev)
        {
            float r = ball.radius;
            foreach (float side in new[] { -1f, 1f })
            {
                float line = halfLen * side;
                if (ball.pos.x * side >= halfLen && (prev.x * side < halfLen || Mathf.Abs(ball.pos.y) >= goalHalf))
                {
                    bool insidePosts = Mathf.Abs(ball.pos.y) < goalHalf - r;
                    if (insidePosts && ball.z < goalH - r)
                    {
                        GoalScored(TeamDir(0) * side < 0f ? 1 : 0);
                        return;
                    }
                    if (insidePosts && ball.z < goalH + r * 2f)
                    {
                        ball.pos.x = line - side * r;
                        ball.vel.x *= -0.45f;
                        ball.vz = -Mathf.Abs(ball.vz) * 0.5f;
                        Emit("post", kind: "bar");
                        continue;
                    }
                    if (mode.walls)
                    {
                        ball.pos.x = line - side * r;
                        ball.vel.x *= -mode.wallBounce;
                        Emit("wall");
                        continue;
                    }
                    OutGoalLine(side);
                    return;
                }
            }
            if (Mathf.Abs(ball.pos.y) > halfW + (mode.walls ? 0f : r))
            {
                if (mode.walls)
                {
                    float s = Mathf.Sign(ball.pos.y);
                    ball.pos.y = s * (halfW - r);
                    ball.vel.y *= -mode.wallBounce;
                    Emit("wall");
                }
                else
                {
                    int t = ball.lastTouchTeam >= 0 ? 1 - ball.lastTouchTeam : 0;
                    Emit("out");
                    SetupRestart(t, new Vector2(ball.pos.x, Mathf.Sign(ball.pos.y) * (halfW - 0.05f)), "throwin");
                }
            }
        }

        void OutGoalLine(float side)
        {
            int defending = TeamDir(0) * side < 0f ? 0 : 1;
            Emit("out");
            if (ball.lastTouchTeam == defending)
            {
                float cy = Mathf.Abs(ball.pos.y) > 0.01f ? Mathf.Sign(ball.pos.y) * (halfW - 0.3f) : halfW - 0.3f;
                SetupRestart(1 - defending, new Vector2(side * (halfLen - 0.3f), cy), "corner");
            }
            else
            {
                float gkX = side * (halfLen - Mathf.Min(5.5f, mode.boxDepth * 0.5f + 1f));
                SetupRestart(defending, new Vector2(gkX, 0f), "goalkick");
            }
        }

        void GoalScored(int t)
        {
            score[t]++;
            lastGoalTeam = t;
            lastScorer = ball.lastTouchId;
            bool ownGoal = ball.lastTouchTeam != t;
            ball.owner = -1;
            state = MatchState.Goal;
            stateTimer = 3f;
            kickoffTeam = 1 - t;
            passReceiver = -1;
            foreach (var c in ctrl) { c.charging = 0; c.pending = 0; }
            Emit("goal", t, lastScorer, ownGoal ? "own" : null);
        }

        void KeepBallInNet()
        {
            float depth = mode.goalDepth;
            if (Mathf.Abs(ball.pos.x) > halfLen + depth - ball.radius)
            {
                ball.pos.x = Mathf.Sign(ball.pos.x) * (halfLen + depth - ball.radius);
                ball.vel *= 0.2f;
            }
            if (Mathf.Abs(ball.pos.x) > halfLen)
            {
                ball.pos.y = Mathf.Clamp(ball.pos.y, -goalHalf + ball.radius, goalHalf - ball.radius);
                ball.z = Mathf.Min(ball.z, goalH - ball.radius);
            }
        }
    }
}
