using System.Collections.Generic;
using UnityEngine;

namespace Craquez.Sim
{
    public enum MatchState { Kickoff, Playing, Goal, Restart, Halftime, Fulltime, Foul }

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
        /// <summary>Saída de bola: o batedor só pode tocar para o companheiro ao lado (nada de sair conduzindo ou chutar).</summary>
        public bool kickoffPass;
        /// <summary>Cobranças rápidas (true) ou cena curta antes da cobrança: barreira montada, todos posicionados (false).</summary>
        public bool quickRestarts;
        /// <summary>O último reinício teve corte de cena (a câmera/HUD fazem a transição).</summary>
        public bool restartScene;
        /// <summary>Falta marcada e ainda não cobrada (o lance segue alguns instantes com a vítima caída).</summary>
        public int foulTeam = -1, foulVictim = -1, foulOffender = -1;
        public Vector2 foulSpot;
        bool _foulPenalty;
        public readonly List<int> wallIds = new List<int>();
        /// <summary>
        /// Bola parada (falta): o cobrador fica na bola parada, adversários a distância e ninguém
        /// toma a bola até a cobrança. Opções: passe, cruzamento na área e, perto do gol, chute direto.
        /// </summary>
        public bool setPiece;
        /// <summary>Lateral cobrado com as mãos (futebol de campo): o cobrador segura a bola acima da cabeça.</summary>
        public bool throwIn;
        /// <summary>Futsal e society: lateral com os pés, bola parada na linha e sempre um passe para um companheiro.</summary>
        public bool kickIn;
        /// <summary>Escanteio: bola parada no canto; cruzamento na área ou toque curto para um companheiro.</summary>
        public bool cornerKick;
        /// <summary>
        /// Cena da cobrança: o cobrador caminha até a bola parada (escanteio, falta, tiro de meta) antes de
        /// assumi-la. Enquanto isso a bola fica sem dono, no lugar.
        /// </summary>
        public int walkUpTaker = -1;
        public Vector2 walkUpTo, walkUpFacing;
        /// <summary>Antes de ir para a bola, o cobrador espera parado os companheiros se posicionarem.</summary>
        public float walkUpWait;
        bool walkUpFast;
        /// <summary>Faltas do time no tempo (futsal: da 6ª em diante, tiro livre sem barreira).</summary>
        public int[] teamFouls = new int[2];
        /// <summary>Quem fez o último passe com o pé (regra do recuo: o goleiro não pega com as mãos).</summary>
        int lastPasser = -1;
        bool noWallKick;
        public int kickInTimeouts;
        public int throwInTaker = -1;
        public Vector2 throwInSpot;
        float _throwT;
        public int setPieceTaker = -1, setPieceTeam = -1;
        public Vector2 setPieceSpot;
        /// <summary>A falta é perto o bastante para o chute direto.</summary>
        public bool setPieceShot;
        float _setPieceT;
        public int kickoffKicker = -1, kickoffMate = -1;
        public Vector2 kickoffMateSpot;
        float kickoffT;
        public float passReceiverT;
        public int penaltyTeam = -1, penaltyTaker = -1;
        public float penaltyT;
        public int offsideTeam = -1;
        public readonly List<int> offsideIds = new List<int>();
        public bool noOffsideKick;
        public int lastScorer = -1, lastGoalTeam = -1;
        public int[] statsShots = new int[2];
        /// <summary>Posse de bola em ticks e substituições restantes por time.</summary>
        public int[] statsPoss = new int[2];
        public int[] subsLeft = { 5, 5 };
        public bool rollingSubs;
        /// <summary>Índices do elenco que já saíram de campo (não voltam).</summary>
        public readonly HashSet<int>[] subbedOut = { new HashSet<int>(), new HashSet<int>() };
        /// <summary>Índice do elenco por atleta, para trocas no intervalo/pausa.</summary>
        public int[] teamMentality = { 1, 1 };
        public int[] teamPress = { 1, 1 };
        /// <summary>Ajuste de rolagem por clima (chuva = bola mais rápida).</summary>
        public float rollScale = 1f;
        /// <summary>Lados trocados no início (definir antes de Setup).</summary>
        public bool startSwapped;
        /// <summary>Rua: jogadores por time (3, 4 ou 5; definir antes de Setup).</summary>
        public int streetPlayers = 3;
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
            mode = GameModes.Get(modeId, streetPlayers);
            mode.rollDecel *= rollScale;
            ball.radius = modeId == GameModes.Futsal ? 0.098f : 0.11f;   // futsal: bola tamanho 4
            halfLen = mode.length * 0.5f;
            halfW = mode.width * 0.5f;
            goalHalf = mode.goalWidth * 0.5f;
            goalH = mode.goalHeight;
            halfSeconds = secondsPerHalf;
            rng = new SimRng(seed);
            teams = new[] { a, b };
            bool rolling = modeId != GameModes.Field;   // futsal, society e rua: substituições à vontade (quem sai pode voltar)
            subsLeft = new[] { rolling ? 99 : 5, rolling ? 99 : 5 };
            rollingSubs = rolling;
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
                var formation = forms.TryGetValue(tac.formation ?? "", out var f) && f.Length == mode.formation.Length ? f : mode.formation;
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
                        if (e != null) { ApplyRoster(p, e); p.rosterIndex = roster.IndexOf(e); }
                    }
                    Personality(p);
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
            mode.rollDecel = GameModes.Get(mode.id, streetPlayers).rollDecel * s;
        }

        /// <summary>Troca formação, mentalidade e pressão no meio do jogo (menu de pausa).</summary>
        public void ApplyTactics(int t, Tactics tac)
        {
            if (tac == null) return;
            teams[t].tactics = tac;
            teamMentality[t] = Mathf.Clamp(tac.mentality, 0, 2);
            teamPress[t] = Mathf.Clamp(tac.press, 0, 2);
            var forms = GameModes.Formations(mode.id);
            if (!forms.TryGetValue(tac.formation ?? "", out var f) || f.Length != mode.formation.Length) return;
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

        /// <summary>
        /// Jeito de jogar de cada atleta (determinístico pela semente): disposição para correr,
        /// ousadia com a bola, disciplina tática, rapidez de leitura e um "corredor" pessoal no campo.
        /// </summary>
        void Personality(SimPlayer p)
        {
            p.work = Mathf.Clamp01(0.45f + (p.pace - 0.7f) * 0.6f + (p.Has(Trait.Resistencia) ? 0.15f : 0f) + rng.Gauss() * 0.15f + (p.pos == "MF" ? 0.08f : 0f));
            p.flair = Mathf.Clamp01(0.35f + (p.control - 0.7f) * 1.2f + (p.Has(Trait.Driblador) ? 0.25f : 0f) + rng.Gauss() * 0.15f + (p.pos == "FW" ? 0.1f : p.pos == "DF" ? -0.12f : 0f));
            p.discipline = Mathf.Clamp01(0.6f + (p.defense - 0.7f) * 0.8f + (p.Has(Trait.Lider) ? 0.15f : 0f) + rng.Gauss() * 0.12f + (p.pos == "DF" ? 0.12f : 0f));
            p.think = Mathf.Clamp(Mathf.Lerp(0.55f, 0.22f, (p.passing + p.control) * 0.5f) + rng.Range(0f, 0.12f), 0.15f, 0.7f);
            p.thinkT = rng.Range(0f, p.think);
            p.lane = new Vector2(rng.Gauss() * 1.4f, rng.Gauss() * 1.8f) * (1.2f - p.discipline);
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

        /// <summary>
        /// Substituição: o atleta <paramref name="playerId"/> sai e entra o jogador do elenco
        /// <paramref name="rosterIdx"/> (que não pode estar em campo nem ter saído). Goleiro só por goleiro.
        /// </summary>
        public bool Substitute(int playerId, int rosterIdx)
        {
            if (playerId < 0 || playerId >= players.Count) return false;
            var p = players[playerId];
            int t = p.team;
            var roster = teams[t].data != null ? teams[t].data.roster : null;
            if (roster == null || rosterIdx < 0 || rosterIdx >= roster.Count || subsLeft[t] <= 0) return false;
            if (subbedOut[t].Contains(rosterIdx)) return false;
            foreach (var q in players) if (q.team == t && q.rosterIndex == rosterIdx) return false;
            var e = roster[rosterIdx];
            if (mode.keeper && p.isKeeper != (e.pos == "GK")) return false;
            if (p.rosterIndex >= 0 && !rollingSubs) subbedOut[t].Add(p.rosterIndex);
            p.traits = 0;
            ApplyRoster(p, e);
            p.rosterIndex = rosterIdx;
            p.stamina = 1f;
            Personality(p);
            subsLeft[t]--;
            return true;
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
        /// <summary>
        /// Fase de posse: o time continua "atacando" com a bola viajando num passe ou solta depois de
        /// um toque seu — só muda quando o adversário domina (antes, a cada passe todos recuavam).
        /// </summary>
        public int possTeam = -1;
        public bool TeamInPossession(int t)
        {
            if (ball.owner >= 0) return players[ball.owner].team == t;
            if (passReceiver >= 0 && players[passReceiver].team == t) return true;
            return possTeam == t;
        }
        public float ChargeOf(int t) => ctrl[t].charging != 0 ? ctrl[t].charge : 0f;

        public SimPlayer NearestOpponent(SimPlayer p)
        {
            SimPlayer best = null;
            float bd = float.MaxValue;
            foreach (var o in players)
            {
                if (o.team == p.team || o.sentOff) continue;
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
                if (p.team != t || p.id == exclude || (p.isKeeper && !allowKeeper) || p.sentOff) continue;
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
            walkUpTaker = -1;
            cornerKick = false;
            ball.pos = Vector2.zero; ball.vel = Vector2.zero; ball.z = 0; ball.vz = 0; ball.spin = 0; ball.slideT = 0; ball.owner = -1;
            passReceiver = -1;
            penaltyTeam = -1;
            offsideIds.Clear();
            offsideTeam = -1;
            foreach (var p in players)
            {
                if (p.sentOff) { ParkSentOff(p); continue; }
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
            SimPlayer kicker = null, mate = null;
            foreach (var p in players)
                if (p.team == t && !p.isKeeper && !p.sentOff && (kicker == null || p.basePos.x > kicker.basePos.x)) kicker = p;
            // companheiro da saída: o mais adiantado depois do batedor
            foreach (var p in players)
                if (p.team == t && !p.isKeeper && !p.sentOff && p != kicker && (mate == null || p.basePos.x > mate.basePos.x)) mate = p;
            kicker.position = new Vector2(-TeamDir(t) * 0.5f, 0);
            kickoffKicker = kicker.id;
            kickoffMate = mate != null ? mate.id : -1;
            kickoffPass = mate != null;
            kickoffT = 0f;
            if (mate != null)
            {
                // ao lado da bola, no próprio campo, de frente para o batedor
                float side = mate.basePos.y >= 0f ? 1f : -1f;
                float gap = mode.id == GameModes.Field ? 3.2f : 2.2f;
                kickoffMateSpot = new Vector2(-TeamDir(t) * 0.9f, side * gap);
                mate.position = kickoffMateSpot;
                mate.facing = (ball.pos - mate.position).Norm();
            }
            kicker.facing = mate != null ? (kickoffMateSpot - kicker.position).Norm() : new Vector2(TeamDir(t), 0);
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

        /// <summary>Reinício forçado (testes e roteiro de imagens).</summary>
        public void ForceRestart(int t, Vector2 spot, string kind) => SetupRestart(t, spot, kind);

        void SetupRestart(int t, Vector2 spot, string kind)
        {
            spot.x = Mathf.Clamp(spot.x, -halfLen, halfLen);
            spot.y = Mathf.Clamp(spot.y, -halfW, halfW);
            ball.pos = spot; ball.vel = Vector2.zero; ball.z = 0; ball.vz = 0; ball.spin = 0; ball.slideT = 0;
            passReceiver = -1;
            kickoffPass = false;
            SimPlayer taker = null;
            if (kind == "goalkick" && mode.keeper)
                foreach (var p in players) if (p.team == t && p.isKeeper) taker = p;
            if (taker == null)
            {
                float bd = float.MaxValue;
                foreach (var p in players)
                {
                    if (p.team != t || p.isKeeper || p.sentOff) continue;
                    float d = (p.position - spot).sqrMagnitude + (p.down > 0f ? 1e4f : 0f);   // quem está caído não cobra
                    if (d < bd) { bd = d; taker = p; }
                }
            }
            var inward = (new Vector2(TeamDir(t) * halfLen * 0.3f, 0) - spot).Norm();
            var takerBefore = taker.position;
            taker.position = spot - inward * 0.5f;
            taker.vel = Vector2.zero;
            taker.facing = inward;
            taker.stun = 0; taker.slide = 0;
            ball.owner = taker.id;
            ball.lastTouchTeam = t;
            if (taker.isKeeper) taker.hold = 1.6f;
            else ctrl[t].player = taker.id;
            // tiro de meta e escanteio: corte de cena, todos já nas posições do reinício
            bool cut = kind == "goalkick" || kind == "corner" || (!quickRestarts && (kind == "freekick" || kind == "offside"));
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
            // ninguém fica fora do campo no reinício (quem caiu ou correu para fora volta para dentro)
            foreach (var p in players)
            {
                if (p == taker || p.sentOff) continue;
                p.position.x = Mathf.Clamp(p.position.x, -halfLen + 0.4f, halfLen - 0.4f);
                p.position.y = Mathf.Clamp(p.position.y, -halfW + 0.4f, halfW - 0.4f);
            }
            wallIds.Clear();
            if (cut && kind == "freekick" && !noWallKick) FreeKickWall(t, spot);
            foreach (var p in players)
            {
                if (p.team != t && !wallIds.Contains(p.id))
                {
                    var d = p.position - spot;
                    if (d.magnitude < mode.restartDist)
                        p.position = spot + (d.magnitude > 0.1f ? d.normalized : -inward) * mode.restartDist;
                }
                p.stun = Mathf.Min(p.stun, 0.2f);
                if (cut) { p.down = 0f; p.stun = 0f; }
            }
            restartScene = cut;
            kickIn = kind == "throwin" && (mode.id == GameModes.Futsal || mode.id == GameModes.Society);
            cornerKick = kind == "corner";
            setPiece = kind == "freekick" || kind == "offside" || kickIn || cornerKick;
            possTeam = t;
            // campo: lateral com as mãos (nos outros modos, cobrado com os pés como um passe)
            throwIn = kind == "throwin" && mode.id == GameModes.Field;
            if (throwIn)
            {
                throwInTaker = taker.id;
                throwInSpot = spot;
                _throwT = 0f;
                taker.facing = inward;
                taker.position = spot;
            }
            if (setPiece)
            {
                setPieceTaker = taker.id;
                setPieceTeam = t;
                setPieceSpot = spot;
                var goal = new Vector2(halfLen * TeamDir(t), 0f);
                setPieceShot = !kickIn && !cornerKick && spot.Dist(goal) < (mode.id == GameModes.Field ? 32f : halfLen * 0.6f) && mode.keeper;
                _setPieceT = 0f;
                // escanteio: de frente para a marca do pênalti (a área)
                if (cornerKick) inward = (goal - new Vector2(TeamDir(t) * mode.penaltyDist, 0f) - spot).Norm();
                taker.facing = setPieceShot ? (goal - spot).Norm() : inward;
                taker.position = spot - taker.facing * 0.5f;
            }
            if (cut && cornerKick)
            {
                // escanteio: o corte já mostra a área ocupada (atacantes nos seus lugares, marcadores
                // colados, um companheiro perto do cobrador para o toque curto)
                _marked.Clear();
                foreach (var p in players)
                {
                    if (p == taker || p.isKeeper || p.sentOff) continue;
                    p.position = SetPieceTarget(p);
                    p.facing = (ball.pos - p.position).Norm();
                }
                _marked.Clear();
            }
            walkUpTaker = -1;
            walkUpFast = false;
            if (!cut && takerBefore.Dist(taker.position) > 0.8f && takerBefore.Dist(taker.position) < 14f && !taker.isKeeper)
            {
                // reinício rápido (lateral, falta cobrada logo): o cobrador vai até a bola de onde estava,
                // em passo rápido, em vez de aparecer nela
                var to = taker.position;
                walkUpTaker = taker.id;
                walkUpTo = to;
                walkUpFacing = taker.facing == Vector2.zero ? inward : taker.facing;
                walkUpWait = 0f;
                walkUpFast = true;
                taker.position = takerBefore;
                taker.facing = (to - takerBefore).Norm();
                ball.owner = -1;
            }
            else if (cut && (cornerKick || kind == "goalkick" || ((kind == "freekick" || kind == "offside") && setPiece)))
                BeginWalkUp(taker, kind);
            protectTeam = t;
            protectT = 2.5f;
            offsideIds.Clear();
            offsideTeam = -1;
            noOffsideKick = kind == "throwin" || kind == "corner" || kind == "goalkick";
            state = MatchState.Restart;
            stateTimer = cut ? (kind == "freekick" ? 1.6f : 1.1f) : (quickRestarts ? 0.35f : 0.7f);
            if (walkUpTaker >= 0 && walkUpFast) stateTimer += Mathf.Min(2.2f, walkUpTo.Dist(players[walkUpTaker].position) / 4.5f + 0.2f);
            else if (walkUpTaker >= 0) stateTimer += WalkUpTime + walkUpWait;
            Emit("whistle", t, -1, kind);
        }

        const float WalkUpTime = 2.2f;

        /// <summary>
        /// O cobrador começa a alguns metros da bola e vai até ela andando (a bola fica no lugar, sem dono).
        /// Falta: chega por trás da bola, em diagonal; escanteio: vem de dentro do campo, pela linha de fundo;
        /// tiro de meta: o goleiro vem da linha do gol.
        /// </summary>
        void BeginWalkUp(SimPlayer taker, string kind)
        {
            var to = taker.position;
            var f = taker.facing == Vector2.zero ? Vector2.right : taker.facing;
            float side = (rng.Chance(0.5f) ? 1f : -1f);
            Vector2 from;
            if (kind == "corner")
            {
                // de dentro do campo, ao longo da linha de fundo
                from = to + new Vector2(0f, -Mathf.Sign(to.y) * 6f) + new Vector2(-Mathf.Sign(to.x) * 1.5f, 0f);
            }
            else if (kind == "goalkick") from = to - f * 3.5f + new Vector2(0f, side * 1.5f);
            else if (kind == "penalty") from = to - f * 4.5f + new Vector2(0f, side * 1.2f);
            else from = to - f * 4.5f + new Vector2(-f.y, f.x) * side * 2.2f;
            from.x = Mathf.Clamp(from.x, -halfLen + 0.4f, halfLen - 0.4f);
            from.y = Mathf.Clamp(from.y, -halfW + 0.4f, halfW - 0.4f);
            if (from.Dist(to) < 1.5f) return;
            walkUpTaker = taker.id;
            walkUpWait = kind == "goalkick" ? 0.5f : 1.3f;
            walkUpTo = to;
            walkUpFacing = f;
            taker.position = from;
            taker.vel = Vector2.zero;
            taker.facing = (to - from).Norm();
            if (taker.isKeeper) taker.hold = 0f;
            ball.owner = -1;
        }

        /// <summary>Um passo da caminhada do cobrador até a bola; ao chegar, assume a bola parada.</summary>
        void StepWalkUp()
        {
            var p = players[walkUpTaker];
            var d = walkUpTo - p.position;
            float dist = d.magnitude;
            if (walkUpWait > 0f && !walkUpFast && stateTimer > WalkUpTime * 0.8f)
            {
                // parado, de olho na bola, esperando a área/barreira se arrumar
                walkUpWait -= DT;
                p.intent = Vector2.zero;
                p.wantSprint = false;
                p.vel = Vector2.zero;
                p.facing = V2.RotateToward(p.facing, (ball.pos - p.position).Norm(), 6f * DT).Norm();
                return;
            }
            // termina ao chegar, ou quando o tempo da cena acaba (sem ficar preso)
            if (dist < 0.3f || stateTimer <= (walkUpFast ? 0.05f : 0.3f))
            {
                p.position = walkUpTo;
                p.vel = Vector2.zero;
                p.intent = Vector2.zero;
                p.facing = walkUpFacing;
                ball.owner = p.id;
                ball.vel = Vector2.zero;
                if (p.isKeeper && !setPiece && !cornerKick) p.hold = 1.6f;
                walkUpTaker = -1;
                return;
            }
            // anda (trote no fim): rápido o bastante para não alongar a cena
            p.intent = d / dist * (walkUpFast ? Mathf.Clamp(dist * 0.5f, 0.45f, 0.85f) : Mathf.Clamp(dist * 0.35f, 0.3f, 0.55f));
            p.wantSprint = false;
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
                    foreach (var p in players) if (p.id != walkUpTaker) p.intent *= 0.3f;
                    if (walkUpTaker >= 0) StepWalkUp();
                    foreach (var p in players) MovePlayer(p);
                    Dribble();
                    if (stateTimer <= 0f && walkUpTaker < 0)
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
                    ClampKeepers();
                    StepBallInNet();
                    if (stateTimer <= 0f) ResetForKickoff(kickoffTeam);
                    break;
                case MatchState.Foul:
                    StepFoul();
                    break;
                case MatchState.Halftime:
                    stateTimer -= DT;
                    if (stateTimer <= 0f)
                    {
                        half = 2;
                        clock = 0f;
                        teamFouls[0] = teamFouls[1] = 0;   // faltas acumuladas zeram no intervalo
                        foreach (var pl in players) pl.stamina = Mathf.Min(1f, pl.stamina + 0.6f);   // descanso no vestiário
                        kickoffTeam = 1;
                        ResetForKickoff(1);
                    }
                    break;
            }
        }

        void StepPlaying(InputFrame[] frames)
        {
            clock += DT;
            if (ball.owner >= 0) statsPoss[players[ball.owner].team]++;
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
            if (kickoffPass && (ball.owner != kickoffKicker || ball.pos.magnitude > 2f)) kickoffPass = false;
            if (setPiece && (ball.owner != setPieceTaker || ball.pos.Dist(setPieceSpot) > 1.5f)) setPiece = false;
            if (!setPiece) { kickIn = false; cornerKick = false; }
            if (throwIn && ball.owner != throwInTaker) throwIn = false;
            if (throwIn)
            {
                protectTeam = players[throwInTaker].team;
                protectT = Mathf.Max(protectT, 0.3f);
                _throwT += DT;
                if (_throwT > 10f) Pass(players[throwInTaker], players[throwInTaker].facing, 0.4f, false, false);
            }
            if (setPiece)
            {
                // ninguém toma a bola parada; demorou demais, o juiz manda cobrar (passe curto)
                protectTeam = setPieceTeam;
                protectT = Mathf.Max(protectT, 0.3f);
                _setPieceT += DT;
                if (kickIn && mode.id == GameModes.Futsal && _setPieceT > 4f && state == MatchState.Playing)
                {
                    // regra do futsal: 4 segundos para cobrar o lateral; passou, a bola vai para o adversário
                    var spot = setPieceSpot;
                    int other = 1 - setPieceTeam;
                    kickInTimeouts++;
                    SetupRestart(other, spot, "throwin");
                    return;
                }
                if (_setPieceT > (kickIn ? 6f : 12f))
                {
                    if (cornerKick) CrossToBox(players[setPieceTaker], players[setPieceTaker].facing, 0.5f);
                    else Pass(players[setPieceTaker], players[setPieceTaker].facing, 0.4f, false, false);
                }
            }
            for (int t = 0; t < 2; t++)
                ProcessController(t, frames != null && t < frames.Length ? frames[t] : default);
            if (kickoffPass)
            {
                // ninguém demora: depois de alguns segundos a saída é feita automaticamente
                kickoffT += DT;
                protectT = Mathf.Max(protectT, 0.3f);
                if (kickoffT > 6f) KickoffTouch(players[kickoffKicker]);
            }
            AiAll(false);
            foreach (var p in players) MovePlayer(p);
            ResolveCollisions();
            ClampKeepers();
            Slides();
            Dribble();
            Possession();
            if (ball.owner >= 0) possTeam = players[ball.owner].team;
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
            if (rs.magnitude > 0.7f && c.prevRs.magnitude < 0.45f && ball.owner == p.id && penaltyTeam < 0 && !(kickoffPass && p.id == kickoffKicker) && !(setPiece && p.id == setPieceTaker) && !(throwIn && p.id == throwInTaker))
                StartSkill(p, rs.normalized);
            c.prevRs = rs;
            if (penaltyTeam >= 0)
            {
                p.intent = Vector2.zero;
                p.wantSprint = false;
                if (t != penaltyTeam) return;
            }
            if (throwIn && ball.owner == p.id && p.id == throwInTaker)
            {
                // lateral: pés atrás da linha, bola nas mãos; o analógico mira; chute vira arremesso longo
                p.intent = Vector2.zero;
                p.wantSprint = false;
                p.vel = Vector2.zero;
                if (f.move.magnitude > 0.25f) p.facing = V2.RotateToward(p.facing, f.move.normalized, 4f * DT).Norm();
                if ((pressed & InputFrame.Shoot) != 0) pressed = (pressed & ~InputFrame.Shoot) | InputFrame.Lob;
                if ((released & InputFrame.Shoot) != 0) released = (released & ~InputFrame.Shoot) | InputFrame.Lob;
            }
            if (setPiece && ball.owner == p.id && p.id == setPieceTaker)
            {
                // cobrador parado na bola; o analógico mira. Longe do gol, B vira cruzamento.
                p.intent = Vector2.zero;
                p.wantSprint = false;
                p.vel = Vector2.zero;
                if (f.move.magnitude > 0.25f) p.facing = V2.RotateToward(p.facing, f.move.normalized, 4f * DT).Norm();
                if (!setPieceShot && (pressed & InputFrame.Shoot) != 0) pressed = (pressed & ~InputFrame.Shoot) | InputFrame.Lob;
                if (!setPieceShot && (released & InputFrame.Shoot) != 0) { released = (released & ~InputFrame.Shoot) | InputFrame.Lob; }
                if (cornerKick)
                {
                    // escanteio: só cruzamento (alto/chute/enfiado) ou passe curto
                    if ((pressed & InputFrame.Through) != 0) pressed = (pressed & ~InputFrame.Through) | InputFrame.Lob;
                    if ((released & InputFrame.Through) != 0) released = (released & ~InputFrame.Through) | InputFrame.Lob;
                }
            }
            if (kickoffPass && ball.owner == p.id && p.id == kickoffKicker)
            {
                // saída de bola: parado na bola; qualquer botão de passe/chute toca para o companheiro
                p.intent = Vector2.zero;
                p.wantSprint = false;
                p.vel = Vector2.zero;
                const int kicks = InputFrame.Pass | InputFrame.Shoot | InputFrame.Lob | InputFrame.Through;
                if ((pressed & kicks) != 0) KickoffTouch(p);
                c.charging = 0;
                c.pending = 0;
                return;
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
                if (p.team != t || p.isKeeper || p.id == c.player || p.sentOff) continue;
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
            if (n != null && n.id != cur.id && cur.position.Dist(ball.pos) > n.position.Dist(ball.pos) + 4.5f)
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
            if (throwIn && p.id == throwInTaker) { Throw(p, aim, power, kind == InputFrame.Lob || kind == InputFrame.Shoot, kind == InputFrame.Through); return; }
            if (kind == InputFrame.Pass) Pass(p, aim, power, false, false);
            else if (kind == InputFrame.Through) Pass(p, aim, power, true, false);
            else if (kind == InputFrame.Lob)
            {
                if (setPiece && !kickIn && p.id == setPieceTaker) { CrossToBox(p, aim, power); return; }
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
            kickoffPass = false;
            setPiece = false;
            throwIn = false;
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

        /// <summary>
        /// Cruzamento na área (bola parada): procura o companheiro na área com melhor cabeceio
        /// (o lado vem da mira); sem ninguém, joga na marca do pênalti. A força escolhe 1º ou 2º pau.
        /// </summary>
        public void CrossToBox(SimPlayer p, Vector2 aim, float power)
        {
            float dir = TeamDir(p.team);
            var goal = new Vector2(halfLen * dir, 0f);
            float depth = Mathf.Max(mode.boxDepth, 6f);
            SimPlayer best = null;
            float bestS = float.MinValue;
            foreach (var m in players)
            {
                if (m.team != p.team || m == p || m.isKeeper) continue;
                float dx = Mathf.Abs(m.position.x - goal.x);
                if (dx > depth + 2f || Mathf.Abs(m.position.y) > Mathf.Max(mode.boxHalf, goalHalf * 2.5f)) continue;
                float sc = (m.Has(Trait.Cabeceador) ? 1f : 0f) + Mathf.Max(m.defense, m.pace) - dx * 0.03f;
                if (aim.sqrMagnitude > 0.1f) sc += Vector2.Dot((m.position - ball.pos).Norm(), aim.Norm()) * 0.8f;
                foreach (var o in players) if (o.team != p.team && o.position.Dist(m.position) < 1.5f) sc -= 0.4f;
                if (sc > bestS) { bestS = sc; best = m; }
            }
            Vector2 target = best != null ? best.position + best.vel * 0.6f : goal - new Vector2(dir * mode.penaltyDist, Mathf.Lerp(-1f, 1f, 0.5f + aim.y * 0.5f) * goalHalf * 0.5f);
            // força: fraca = primeiro pau, forte = segundo pau
            target.y += Mathf.Lerp(-1f, 1f, power) * goalHalf * 0.4f * -Mathf.Sign(ball.pos.y == 0f ? 1f : ball.pos.y);
            target.x = Mathf.Clamp(target.x, -halfLen + 1f, halfLen - 1f);
            if (best != null) { passReceiver = best.id; passReceiverT = 3f; }
            Pass(p, target - ball.pos, Mathf.Clamp(0.35f + power * 0.4f, 0.3f, 0.8f), false, true);
            Emit("cross", p.team, p.id);
        }

        /// <summary>Toque curto da saída de bola para o companheiro ao lado.</summary>
        void KickoffTouch(SimPlayer p)
        {
            if (ball.owner != p.id) return;
            var mate = kickoffMate >= 0 ? players[kickoffMate] : null;
            if (mate == null) { kickoffPass = false; return; }
            var to = mate.position - ball.pos;
            float dist = Mathf.Max(0.5f, to.magnitude);
            var dirv = (to / dist).Rotated(rng.Gauss() * 0.02f * (1.2f - p.passing));
            float vEnd = 2.6f;
            float v0 = Mathf.Sqrt(vEnd * vEnd + 2f * mode.rollDecel * dist);
            p.kickKind = InputFrame.Pass;
            p.facing = dirv;
            ball.vel = dirv * v0;
            ball.vz = 0f;
            ball.spin = 0f;
            ball.slideT = 0f;
            passReceiver = mate.id;
            passReceiverT = 2f;
            passTick = tick; lastPasser = p.id;
            passFrom = ball.pos;
            lastPasser = p.id;
            noOffsideKick = true;
            ReleaseBall(p);
            // o adversário ainda não entra no círculo até a bola chegar ao companheiro
            protectTeam = p.team;
            protectT = 0.35f;
            Emit("kick", p.team, p.id, "pass", power: 0.2f);
        }

        /// <summary>
        /// Velocidade de saída para a bola rolar <paramref name="dist"/> metros e chegar a <paramref name="vEnd"/>:
        /// atrito de rolamento (a) + arrasto (c·v²) → v0² = ((a + c·vEnd²)·e^(2cs) − a) / c.
        /// </summary>
        float PassSpeed(float dist, float vEnd)
        {
            float a = mode.rollDecel, c = Mathf.Max(1e-4f, mode.groundDrag);   // (rollDecel já inclui o clima)
            float v2 = ((a + c * vEnd * vEnd) * Mathf.Exp(2f * c * dist) - a) / c;
            return Mathf.Sqrt(Mathf.Max(v2, vEnd * vEnd)) * 1.02f;
        }

        void Pass(SimPlayer p, Vector2 aim, float power, bool through, bool lofted)
        {
            if (kickIn && setPiece && p.id == setPieceTaker) { through = false; power = Mathf.Min(power, 0.75f); }   // lateral: passe no pé do companheiro
            if (throwIn && p.id == throwInTaker && ball.owner == p.id) { Throw(p, aim, power, lofted, through); return; }
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
                if (m.team != p.team || m == p || m.sentOff) continue;
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
                    for (int k = 0; k < 3; k++)
                    {
                        float dd = ball.pos.Dist(target);
                        float ve = mode.id != GameModes.Field ? 2.6f + dd * 0.14f : 4.5f + dd * 0.12f;
                        float tt = dd / Mathf.Max(3f, (PassSpeed(dd, ve) + ve) * 0.5f);   // tempo de voo de verdade
                        target = best.position + best.vel * tt * 0.85f;
                    }
                }
                passReceiver = best.id;
                passReceiverT = 3f;
                passTick = tick; lastPasser = p.id;
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
                // força casada com a distância: curto chega macio, longo chega firme (sem morrer no caminho)
                float vEnd = small ? Mathf.Min(7f, 2.6f + dist * 0.14f + power * 2f) : Mathf.Min(11f, 4.5f + dist * 0.12f + power * 3f);
                float v0 = Mathf.Clamp(PassSpeed(dist, vEnd), small ? 4f : 7f, 30f);
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

        /// <summary>
        /// Arremesso lateral com as duas mãos: a bola sai de cima da cabeça (~2 m) em parábola,
        /// curta para o pé do companheiro ou longa (até ~28 m) na área; mais lenta que um passe.
        /// </summary>
        void Throw(SimPlayer p, Vector2 aim, float power, bool longThrow, bool through)
        {
            p.kickKind = InputFrame.Lob;
            if (aim.sqrMagnitude < 0.01f) aim = p.facing;
            aim = aim.Norm();
            // procura o companheiro no cone da mira, até o alcance do arremesso
            float maxD = longThrow ? 28f : 18f;
            SimPlayer best = null;
            float bestS = float.MinValue;
            foreach (var m in players)
            {
                if (m.team != p.team || m == p || m.isKeeper) continue;
                var to = m.position - ball.pos;
                float d = to.magnitude;
                if (d < 2f || d > maxD) continue;
                float cosang = Vector2.Dot(aim, to / d);
                if (cosang < 0.5f) continue;
                float sc = cosang * 2f - d / maxD * (longThrow ? 0f : 0.8f) + (longThrow ? d / maxD : 0f);
                foreach (var o in players) if (o.team != p.team && o.position.Dist(m.position) < 1.6f) sc -= 0.5f;
                if (sc > bestS) { bestS = sc; best = m; }
            }
            Vector2 target = best != null ? best.position + best.vel * 0.7f + (through ? aim * 4f : Vector2.zero) : ball.pos + aim * Mathf.Lerp(8f, maxD, power);
            target.x = Mathf.Clamp(target.x, -halfLen + 0.5f, halfLen - 0.5f);
            target.y = Mathf.Clamp(target.y, -halfW + 0.5f, halfW - 0.5f);
            var toT = target - ball.pos;
            float dist = Mathf.Clamp(toT.magnitude, 1.5f, maxD);
            var dirv = (toT.magnitude > 0.01f ? toT.normalized : aim).Rotated(rng.Gauss() * 0.04f * (1.2f - p.passing));
            // parábola saindo a 2 m: curto chega na altura do peito/pé, longo cai na área
            float h0 = 2.05f, hEnd = longThrow ? 0.3f : 0.15f;
            float flight = (longThrow ? 0.95f : 0.6f) + dist / (longThrow ? 26f : 22f);
            ball.z = h0;
            ball.vel = dirv * (dist * 1.02f / flight);
            ball.vz = (hEnd - h0 + 0.5f * SimBall.G * flight * flight) / flight;
            ball.spin = 0f;
            ball.slideT = 0f;
            if (best != null) { passReceiver = best.id; passReceiverT = 3f; }
            passTick = tick; lastPasser = p.id;
            passFrom = ball.pos;
            noOffsideKick = true;
            p.facing = dirv;
            ReleaseBall(p);
            protectTeam = p.team;
            protectT = 0.25f;
            Emit("kick", p.team, p.id, "throw", power: power);
        }

        void Shoot(SimPlayer p, Vector2 aim, float power, bool finesse = false)
        {
            if (throwIn && p.id == throwInTaker && ball.owner == p.id) { Throw(p, aim, power, true, false); return; }
            // lateral do futsal/society: não vale chutar ao gol, sai um passe para o companheiro
            if (kickIn && setPiece && p.id == setPieceTaker && ball.owner == p.id) { Pass(p, aim, 0.5f, false, false); return; }
            p.kickKind = InputFrame.Shoot;
            float dir = TeamDir(p.team);
            float gx = halfLen * dir;
            float aimY;
            if (Mathf.Abs(aim.y) > 0.2f) aimY = Mathf.Clamp(aim.y * 1.4f, -1f, 1f) * goalHalf * 0.8f;
            else
            {
                // sem mira: canto longe do goleiro (adiantado para um lado) ou, com ele no meio, o segundo pau
                float side = -Mathf.Sign(Mathf.Abs(p.position.y) > 0.5f ? p.position.y : 1f);
                foreach (var k in players)
                    if (k.isKeeper && k.team != p.team && Mathf.Abs(k.position.y) > 0.35f) side = -Mathf.Sign(k.position.y);
                aimY = side * goalHalf * 0.6f;
            }
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
            // altura de chegada proporcional ao gol (futsal/rua têm gols baixos)
            float hk = goalH / 2.44f;
            float h = (Mathf.Lerp(0.15f, 2.5f, Mathf.Pow(power, 1.8f)) + rng.Gauss() * 0.25f * power) * hk;
            if (finesse) h = (Mathf.Lerp(0.6f, 1.9f, power) + rng.Gauss() * 0.15f) * hk;
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
            // por trás é arriscado, mas nem toda chegada vira falta (marcador bom chega na bola)
            if (fromBehind && rng.Chance(Mathf.Lerp(0.22f, 0.06f, p.defense) * (o.sprinting ? 1.3f : 1f) * (p.yellows > 0 ? 0.45f : 1f))) { Foul(p, o); return; }
            float chance = 0.26f + 0.38f * p.defense - 0.34f * o.control + (fromBehind ? 0f : 0.1f);
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
                        else
                        {
                            // chegou junto com a bola: carrinho bom leva a bola antes; senão é falta
                            bool fromBehind = Vector2.Dot(o.facing, (p.position - o.position).Norm()) < -0.45f;
                            float clean = ball.z < 0.5f && p.position.Dist(ball.pos) < 1.3f ? (fromBehind ? 0.2f : 0.45f) + 0.3f * p.defense + (p.Has(Trait.Carrinho) ? 0.15f : 0f) : 0f;
                            if (rng.Chance(clean))
                            {
                                if (ball.owner >= 0) players[ball.owner].stun = 0.35f;
                                ball.owner = -1;
                                ball.vel = p.facing * 6f + p.vel * 0.3f;
                                ball.lastTouchTeam = p.team;
                                ball.lastTouchId = p.id;
                                p.slideHitBall = true;
                                o.stun = 0.7f;
                                Emit("tackle", p.team, p.id, ok: true);
                            }
                            else { Foul(p, o); return; }
                        }
                    }
                }
            }
        }

        /// <summary>Último homem: a vítima ia sozinha para o gol (só o goleiro na frente).</summary>
        bool IsLastDefender(SimPlayer offender, SimPlayer victim)
        {
            if (!mode.keeper || ball.owner != victim.id) return false;
            float gx = -halfLen * TeamDir(offender.team);
            if (Mathf.Abs(victim.position.x - gx) > (mode.id == GameModes.Field ? 30f : halfLen * 0.6f)) return false;
            float vd = Mathf.Abs(victim.position.x - gx);
            foreach (var m in players)
                if (m.team == offender.team && !m.isKeeper && m != offender && !m.sentOff && Mathf.Abs(m.position.x - gx) < vd) return false;
            return true;
        }

        /// <summary>Expulsão: o atleta sai de campo (vai para perto do banco) e o time joga com um a menos.</summary>
        void SendOff(SimPlayer p, string why)
        {
            if (p.isKeeper) { Emit("card", p.team, p.id, "yellow"); p.yellows = Mathf.Max(p.yellows, 1); return; }   // (goleiro: sem reserva no jogo, fica no amarelo)
            int active = 0;
            foreach (var m in players) if (m.team == p.team && !m.sentOff) active++;
            if (active <= Mathf.Max(3, players.Count / 2 - 4)) { Emit("card", p.team, p.id, "yellow"); return; }   // limite de expulsões
            p.sentOff = true;
            p.sentOffT = mode.id == GameModes.Futsal ? 120f : -1f;   // futsal: completa depois de 2 minutos
            Emit("card", p.team, p.id, "red");
            if (ctrl[p.team].player == p.id)
                foreach (var m in players) if (m.team == p.team && !m.sentOff && !m.isKeeper) { ctrl[p.team].player = m.id; break; }
            if (ball.owner == p.id) ball.owner = -1;
            ParkSentOff(p);
        }

        void ParkSentOff(SimPlayer p)
        {
            p.position = new Vector2(-TeamDir(p.team) * (4f + p.index * 0.8f), -(halfW + 3.5f));
            p.vel = Vector2.zero;
            p.intent = Vector2.zero;
            p.stun = 1f;
            p.facing = new Vector2(0f, 1f);
        }

        public void Foul(SimPlayer offender, SimPlayer victim)
        {
            if (state != MatchState.Playing) return;
            offender.stun = Mathf.Max(offender.stun, 0.8f);
            offender.slide = Mathf.Min(offender.slide, 0.15f);
            // a vítima vai ao chão: tropeça, cai, rola e levanta (o tempo no chão varia com a pancada)
            var hit = offender.vel.sqrMagnitude > 0.5f ? offender.vel.normalized : (victim.position - offender.position).Norm();
            if (hit == Vector2.zero) hit = -victim.facing;
            float force = Mathf.Clamp01(offender.vel.magnitude / 8f + (offender.slide > 0f ? 0.35f : 0f));
            victim.fallDir = hit;
            victim.fallKind = Vector2.Dot(hit, victim.facing) > 0.3f ? 1 : Vector2.Dot(hit, victim.facing) < -0.3f ? 0 : 2;   // 0 de frente, 1 por trás, 2 de lado
            victim.down = quickRestarts ? Mathf.Lerp(1.1f, 1.4f, force) : Mathf.Lerp(1.6f, 2.6f, force) + rng.Range(0f, 0.4f);
            victim.downTotal = victim.down;
            victim.stun = victim.down + 0.15f;
            victim.vel = victim.vel * 0.5f + hit * (1.5f + 2.5f * force);
            victim.windup = 0f;
            victim.skill = Skill.None;
            if (ball.owner == victim.id || ball.owner == offender.id)
            {
                ball.owner = -1;
                ball.vel = ball.vel * 0.4f + hit * 1.5f;
            }
            Emit("foul", offender.team, offender.id);
            teamFouls[offender.team]++;
            // cartões: imprudente (carrinho por trás, chegada forte) = amarelo; violento = vermelho
            bool fromBehindHit = Vector2.Dot(hit, victim.facing) > 0.3f;
            float severity = force + (fromBehindHit ? 0.35f : 0f) + (offender.slide > 0f ? 0.2f : 0f);
            bool lastMan = IsLastDefender(offender, victim);
            if (mode.id == GameModes.Street) severity = 0f;   // rua: sem cartões
            if (mode.id == GameModes.Street) lastMan = false;
            if (severity > 1.25f && rng.Chance(0.35f)) SendOff(offender, "red");
            else if (lastMan && rng.Chance(0.5f)) SendOff(offender, "red");   // impediu chance clara de gol
            else if (severity > 0.75f && rng.Chance(0.55f))
            {
                offender.yellows++;
                if (offender.yellows >= 2) SendOff(offender, "second");
                else Emit("card", offender.team, offender.id, "yellow");
            }
            var spot = victim.position;
            spot.x = Mathf.Clamp(spot.x, -halfLen + 1f, halfLen - 1f);
            spot.y = Mathf.Clamp(spot.y, -halfW + 1f, halfW - 1f);
            float gx = -halfLen * TeamDir(offender.team);
            foulTeam = victim.team;
            foulVictim = victim.id;
            foulOffender = offender.id;
            foulSpot = spot;
            _foulPenalty = mode.penaltyDist > 0f && Mathf.Abs(spot.x - gx) < mode.boxDepth && Mathf.Abs(spot.y) < mode.boxHalf;
            passReceiver = -1;
            penaltyTeam = -1;
            kickoffPass = false;
            foreach (var c in ctrl) { c.charging = 0; c.pending = 0; }
            state = MatchState.Foul;
            stateTimer = quickRestarts ? 0.9f : Mathf.Max(1.8f, victim.down - 0.2f);
        }

        /// <summary>Lance parado após a falta: a vítima no chão, os outros param ou vão até o lance.</summary>
        void StepFoul()
        {
            stateTimer -= DT;
            foreach (var p in players)
            {
                if (p.down <= 0f && p.stun <= 0f && !p.isKeeper)
                {
                    // companheiros mais perto vão ver a vítima; o resto desacelera
                    var v = foulVictim >= 0 ? players[foulVictim] : null;
                    float d = v != null ? p.position.Dist(v.position) : 99f;
                    if (v != null && d < 9f && d > 2.2f && p != v)
                    {
                        var to = (v.position - p.position) / d;
                        p.intent = to * (p.team == v.team ? 0.45f : 0.3f);
                        p.facing = V2.RotateToward(p.facing, to, 0.08f);
                    }
                    else p.intent *= 0.85f;
                    p.wantSprint = false;
                }
                MovePlayer(p);
            }
            ResolveCollisions();
            ClampKeepers();
            // a bola rola solta até parar (sem gol nem lateral: o jogo está parado)
            ball.owner = -1;
            ball.Step(DT, mode);
            ball.pos.x = Mathf.Clamp(ball.pos.x, -halfLen - 2f, halfLen + 2f);
            ball.pos.y = Mathf.Clamp(ball.pos.y, -halfW - 2f, halfW + 2f);
            if (stateTimer > 0f) return;
            int t = foulTeam;
            var victim = foulVictim >= 0 ? players[foulVictim] : null;
            foulTeam = foulVictim = foulOffender = -1;
            state = MatchState.Playing;
            if (_foulPenalty)
            {
                // quem sofreu bate se já levantou; senão o melhor finalizador perto
                SimPlayer taker = victim != null && victim.down <= 0f ? victim : null;
                if (taker == null)
                    foreach (var p in players)
                        if (p.team == t && !p.isKeeper && p.down <= 0f && (taker == null || p.shot > taker.shot)) taker = p;
                SetupPenalty(t, taker ?? victim);
                return;
            }
            // futsal: da 6ª falta acumulada do adversário no tempo em diante, tiro livre direto sem
            // barreira (da marca dos 10 m, ou do lugar da falta se for mais perto do gol)
            if (mode.id == GameModes.Futsal && teamFouls[1 - t] >= 6)
            {
                var g = new Vector2(halfLen * TeamDir(t), 0f);
                if (foulSpot.Dist(g) > 10f) foulSpot = g - new Vector2(TeamDir(t) * 10f, 0f);
                noWallKick = true;
            }
            SetupRestart(t, foulSpot, "freekick");
            noWallKick = false;
        }

        /// <summary>
        /// Barreira: perto do gol, os defensores mais próximos se alinham na distância regulamentar,
        /// entre a bola e o gol (cobrindo o lado do primeiro pau); o goleiro arma do outro lado.
        /// </summary>
        void FreeKickWall(int t, Vector2 spot)
        {
            if (!mode.keeper) return;
            int def = 1 - t;
            var goal = new Vector2(halfLen * TeamDir(t), 0f);
            float dist = spot.Dist(goal);
            bool field = mode.id == GameModes.Field;
            if (dist > (field ? 34f : halfLen * 0.75f)) return;
            int n = field ? (dist < 22f ? 4 : 3) : 2;
            var toGoal = (goal - spot) / dist;
            var perp = new Vector2(-toGoal.y, toGoal.x);
            // centro da barreira: na linha bola -> primeiro pau
            var nearPost = new Vector2(goal.x, Mathf.Sign(spot.y == 0f ? 1f : spot.y) * goalHalf * 0.6f);
            var aim = (nearPost - spot).Norm();
            var center = spot + aim * mode.restartDist;
            var cand = new List<SimPlayer>();
            foreach (var p in players) if (p.team == def && !p.isKeeper) cand.Add(p);
            cand.Sort((a, b) => a.position.Dist(center).CompareTo(b.position.Dist(center)));
            float gap = 0.62f;
            for (int i = 0; i < n && i < cand.Count; i++)
            {
                var p = cand[i];
                float off = (i - (n - 1) * 0.5f) * gap;
                p.position = center + perp * off;
                p.vel = Vector2.zero;
                p.facing = -aim;
                wallIds.Add(p.id);
            }
            foreach (var p in players)
                if (p.team == def && p.isKeeper)
                {
                    // goleiro: fica do lado oposto à barreira, um passo à frente da linha
                    float y = -Mathf.Sign(nearPost.y) * goalHalf * 0.3f;
                    p.position = new Vector2(goal.x - TeamDir(t) * 0.6f, y);
                    p.facing = (spot - p.position).Norm();
                }
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
            // o batedor vem da entrada da área até a bola
            walkUpTaker = -1;
            BeginWalkUp(taker, "penalty");
            if (walkUpTaker >= 0) stateTimer += WalkUpTime + walkUpWait;
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
        // marcação: quem já está marcado neste passo (por time defensor)
        readonly System.Collections.Generic.HashSet<int> _marked = new System.Collections.Generic.HashSet<int>();

        void AiAll(bool frozen)
        {
            _marked.Clear();
            foreach (var p in players)
            {
                if (p.sentOff) continue;
                bool human = ctrl[p.team].player == p.id;
                if (human && state == MatchState.Playing) continue;
                if (penaltyTeam >= 0 && !p.isKeeper)
                {
                    p.intent = Vector2.zero;
                    p.wantSprint = false;
                    continue;
                }
                if (p.isKeeper) AiKeeper(p, frozen);
                else { AiOutfield(p, frozen); AntiSpin(p); }
            }
        }

        /// <summary>
        /// Sem jogador rodando: a direção pedida pela IA vira no máximo ~300°/s e, se ela ficar girando
        /// para o mesmo lado (desvios em espiral em volta de companheiros e alvos que andam), o atleta
        /// segue reto por um instante em vez de dar a volta.
        /// </summary>
        void AntiSpin(SimPlayer p)
        {
            var it = p.intent;
            float mag = it.magnitude;
            p.aiTurnAcc *= Mathf.Exp(-DT / 1.2f);
            if (mag < 0.2f || p.aiLastIntent.sqrMagnitude < 0.04f) { p.aiLastIntent = it; return; }
            if (p.aiStraightT > 0f)
            {
                p.aiStraightT -= DT;
                // segue na direção que já tinha (só corrige devagar)
                var d0 = V2.RotateToward(p.aiLastIntent.Norm(), it / mag, 1.2f * DT);
                p.intent = d0 * mag;
                p.aiLastIntent = p.intent;
                return;
            }
            float ang = Vector2.SignedAngle(p.aiLastIntent, it);
            float maxStep = 300f * DT;
            if (Mathf.Abs(ang) > maxStep && Mathf.Abs(ang) < 150f)   // (meia-volta de verdade passa)
            {
                p.intent = p.aiLastIntent.Norm().Rotated(Mathf.Sign(ang) * maxStep * Mathf.Deg2Rad) * mag;
                ang = Mathf.Sign(ang) * maxStep;
            }
            p.aiTurnAcc += ang;
            if (Mathf.Abs(p.aiTurnAcc) > 240f) { p.aiStraightT = 0.9f; p.aiTurnAcc = 0f; }
            p.aiLastIntent = p.intent;
        }

        public Vector2 FormationTarget(SimPlayer p, bool attacking)
        {
            float dir = TeamDir(p.team);
            float bxRel = ball.pos.x * dir;
            float baseX = p.basePos.x * halfLen;
            if (mode.id == GameModes.Field && !p.isKeeper)
            {
                // linhas: zaga e meio alinhados na mesma altura (linha de 4 de verdade); laterais sobem um pouco no ataque
                bool wide = Mathf.Abs(p.basePos.y) > 0.55f;
                // (as formações vão de -1, a linha do gol, até 0: zaga ~-0,75, meio ~-0,45, ataque ~-0,15)
                if (p.basePos.x <= -0.62f) baseX = -0.75f * halfLen + (attacking && wide ? halfLen * 0.12f : 0f);
                else if (p.basePos.x <= -0.3f) baseX = -0.46f * halfLen + (p.basePos.x + 0.46f) * halfLen * 0.3f;
            }
            // o bloco inteiro sobe e desce com a bola (ataque abre, defesa fecha)
            bool field = mode.id == GameModes.Field;
            // atacando, o time se espalha em profundidade (atacantes na linha da zaga rival); defendendo, fecha o bloco
            float spread = attacking ? (field ? 0.66f : 0.8f) : (field ? 0.46f : 0.62f);
            float tx = baseX * spread + bxRel * (field ? 0.58f : 0.42f) + (attacking ? halfLen * 0.24f : -halfLen * 0.1f);
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
            if (kickoffPass && (p.id == kickoffMate || p.id == kickoffKicker))
            {
                // dupla da saída fica no lugar: batedor na bola, companheiro esperando o toque
                if (p.id == kickoffMate) { SteerTo(p, kickoffMateSpot, false); p.facing = (ball.pos - p.position).Norm(); }
                else { p.intent = Vector2.zero; p.wantSprint = false; }
                return;
            }
            if (setPiece && ((!frozen && state == MatchState.Playing) || (cornerKick && state == MatchState.Restart)))
            {
                if (wallIds.Contains(p.id))
                {
                    // barreira: fica plantada, de frente para a bola
                    p.intent = Vector2.zero; p.wantSprint = false; p.vel = Vector2.zero;
                    p.facing = (ball.pos - p.position).Norm();
                    return;
                }
                if (p.id != setPieceTaker)
                {
                    SteerTo(p, SetPieceTarget(p), false);
                    return;
                }
            }
            bool attacking = TeamInPossession(t);
            bool live = !frozen && state == MatchState.Playing;
            var target = FormationTarget(p, attacking);
            bool urgent = false, pressing = false;
            if (live) target = attacking ? SupportRun(p, target, attacking) : Mark(p, target);
            if (live)
            {
                if (passReceiver == p.id && ball.owner < 0)
                {
                    target = InterceptPoint(p);
                    urgent = true;
                }
                else if (ball.owner < 0 && NearestToBall(t) == p && ctrl[t].player != p.id)
                {
                    var human = players[ctrl[t].player];
                    // o mais perto da bola vai nela (antes só ia se o controlado estivesse 3 m mais longe)
                    if (human.position.Dist(ball.pos) > p.position.Dist(ball.pos) + 0.3f)
                    {
                        target = InterceptPoint(p);
                        urgent = true;
                    }
                }
                else if (!attacking && ball.owner >= 0)
                {
                    // só UM companheiro ajuda na pressão (o mais perto além do controlado); o resto guarda a zona
                    var helper = NearestToBall(t, ctrl[t].player);
                    if (helper == p)
                    {
                        var ownGoal = new Vector2(-halfLen * TeamDir(t), 0);
                        float reach = halfLen * (0.3f + 0.15f * teamPress[t]) * (0.75f + 0.5f * p.work);
                        if (ctrl[t].helpT > 0f) { target = ball.pos; urgent = true; }
                        else if (teamPress[t] == 2 && p.position.Dist(ball.pos) < halfLen * 0.45f) { target = ball.pos; urgent = true; }
                        else if (p.position.Dist(ball.pos) < reach)
                        {
                            target = ball.pos + (ownGoal - ball.pos).Norm() * (4.5f - 1.5f * teamPress[t]);
                            pressing = true;
                        }
                    }
                }
            }
            if (!urgent)
            {
                // cada atleta decide no seu ritmo (uns leem o jogo mais rápido) e muda de ideia aos poucos:
                // nada de 20 jogadores mudando de direção no mesmo quadro
                p.thinkT -= DT;
                // a posse mudou: todo mundo reage logo (transição), cada um com seu tempo de reação curto
                if (attacking != p.aiAttacking) { p.aiAttacking = attacking; p.thinkT = Mathf.Min(p.thinkT, p.think * 0.3f); }
                if (frozen || p.thinkT <= 0f || (p.aiGoal - target).sqrMagnitude > 64f)
                {
                    var lane = p.lane * (attacking ? 1f : 0.5f) * (mode.id == GameModes.Field ? 1f : 0.45f);
                    p.aiGoal = target + lane;
                    p.thinkT = p.think;
                }
                // alvo desliza (corridas em curva em vez de zigue-zague)
                float glide = frozen ? 99f : (pressing ? 12f : 8f + 4f * p.work);
                p.aiTarget = frozen || p.aiTarget == Vector2.zero ? p.aiGoal : Vector2.MoveTowards(p.aiTarget, p.aiGoal, glide * DT);
                target = Spacing(p, p.aiTarget, pressing);
            }
            else
            {
                p.aiTarget = p.aiGoal = target;
                p.thinkT = 0f;
            }
            // bola solta perto do gol é do goleiro: o zagueiro não disputa com ele
            if (!urgent || (ball.owner < 0 && KeeperClaims(t))) target = KeeperSpace(p, target);
            SteerTo(p, target, urgent);
        }

        /// <summary>
        /// Posições na bola parada: perto do gol, quem ataca ocupa a área (1º pau, 2º pau, marca
        /// do pênalti, entrada da área) e quem defende marca dentro dela; longe, cada um na sua zona.
        /// </summary>
        /// <summary>Onde o companheiro do escanteio curto se oferece: perto do cobrador, na diagonal para dentro.</summary>
        public Vector2 CornerShortSpot()
        {
            bool field = mode.id == GameModes.Field;
            var s = setPieceSpot;
            return new Vector2(s.x - Mathf.Sign(s.x) * (field ? 7f : 3.5f), s.y - Mathf.Sign(s.y) * (field ? 5f : 2.6f));
        }

        Vector2 SetPieceTarget(SimPlayer p)
        {
            float dir = TeamDir(setPieceTeam);
            var goal = new Vector2(halfLen * dir, 0f);
            bool attackers = p.team == setPieceTeam;
            float dist = setPieceSpot.Dist(goal);
            // lateral (futsal/society): cada um na sua zona, oferecendo-se para o passe
            if (dist > halfLen * 0.95f || kickIn)
            {
                var f = FormationTarget(p, attackers);
                if (!attackers && f.Dist(setPieceSpot) < mode.restartDist) f = setPieceSpot + (f - setPieceSpot).Norm() * mode.restartDist;
                return f;
            }
            int rank = 0;
            foreach (var m in players)
                if (m.team == p.team && !m.isKeeper && m.id != setPieceTaker && !wallIds.Contains(m.id) && m.basePos.x > p.basePos.x) rank++;
            float bd = Mathf.Max(mode.boxDepth, 6f);
            Vector2[] slots =
            {
                new Vector2(goal.x - dir * bd * 0.35f, -goalHalf * 0.8f), new Vector2(goal.x - dir * bd * 0.4f, goalHalf * 0.9f),
                new Vector2(goal.x - dir * bd * 0.62f, 0f), new Vector2(goal.x - dir * bd * 0.55f, goalHalf * 2.2f),
                new Vector2(goal.x - dir * bd * 0.55f, -goalHalf * 2.2f), new Vector2(goal.x - dir * bd * 1.15f, 0f),
            };
            Vector2 t;
            int inBox = mode.id == GameModes.Field ? 5 : 2;
            if (attackers)
            {
                // os mais adiantados vão para a área; zagueiros e o volante ficam na cobertura
                if (rank < inBox) t = slots[rank % slots.Length];
                else if (cornerKick && rank == inBox) t = CornerShortSpot();   // opção do toque curto
                else t = FormationTarget(p, true);
            }
            else
            {
                // defensores: colados no atacante mais próximo dentro da área
                SimPlayer near = null; float nd = 99f;
                foreach (var o in players)
                {
                    if (o.team == p.team || o.isKeeper || o.id == setPieceTaker || _marked.Contains(o.id)) continue;
                    if (Mathf.Abs(o.position.x - goal.x) > bd + 3f) continue;
                    if (cornerKick && o.position.Dist(CornerShortSpot()) < 3f) continue;   // o do toque curto fica livre (distância da bola)
                    float d = o.position.Dist(p.position);
                    if (d < nd) { nd = d; near = o; }
                }
                if (near != null && rank < (mode.id == GameModes.Field ? 7 : 3))
                {
                    _marked.Add(near.id);
                    t = near.position + (goal - near.position).Norm() * 0.9f;
                }
                else t = FormationTarget(p, false);
            }
            // distância regulamentar da bola para quem defende
            if (!attackers && t.Dist(setPieceSpot) < mode.restartDist) t = setPieceSpot + (t - setPieceSpot).Norm() * mode.restartDist;
            t.x = Mathf.Clamp(t.x, -halfLen + 0.5f, halfLen - 0.5f);
            t.y = Mathf.Clamp(t.y, -halfW + 0.5f, halfW - 0.5f);
            return t;
        }

        /// <summary>
        /// Espaço entre companheiros, nos dois momentos do jogo: ninguém ocupa o mesmo lugar
        /// (sem bolo de jogadores em volta da bola). Considera onde os outros estão e para onde vão.
        /// </summary>
        Vector2 Spacing(SimPlayer p, Vector2 target, bool pressing)
        {
            bool attacking = TeamInPossession(p.team);
            float room = mode.id == GameModes.Field ? (attacking ? 8.5f : 6.5f)
                : mode.id == GameModes.Society ? (attacking ? 6f : 4.8f)
                : mode.id == GameModes.Futsal ? (attacking ? 4.6f : 3.6f) : 3.4f;
            if (pressing) room *= 0.5f;
            var push = Vector2.zero;
            foreach (var m in players)
            {
                if (m == p || m.team != p.team || m.isKeeper) continue;
                bool ballGuy = m.id == ball.owner || m.id == ctrl[p.team].player;
                float r = ballGuy ? room * 0.8f : room;
                // posição atual e destino do companheiro
                for (int k = 0; k < 2; k++)
                {
                    var q = k == 0 ? m.position : (m.aiTarget == Vector2.zero ? m.position : m.aiTarget);
                    var dv = target - q;
                    float dd = dv.magnitude;
                    if (dd < r)
                    {
                        var dir = dd > 0.05f ? dv / dd : new Vector2(p.basePos.y >= m.basePos.y ? 0f : 0f, p.basePos.y >= m.basePos.y ? 1f : -1f);
                        push += dir * (r - dd) * (k == 0 ? 0.35f : 0.25f);
                    }
                }
            }
            target += push.Limit(room);
            target.x = Mathf.Clamp(target.x, -halfLen + 0.5f, halfLen - 0.5f);
            target.y = Mathf.Clamp(target.y, -halfW + 0.5f, halfW - 0.5f);
            return target;
        }

        /// <summary>
        /// Movimentação sem bola: atacantes atacam o espaço (até a linha do impedimento); os dois
        /// companheiros mais próximos do portador oferecem linhas de passe DIFERENTES (um em
        /// diagonal à frente, outro de apoio por trás/lado); os demais mantêm a forma do time.
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
                    // ordem de proximidade ao portador (0 = o mais perto)
                    int rank = 0;
                    foreach (var m in players)
                        if (m.team == p.team && m != p && m != carrier && !m.isKeeper && m.position.Dist(carrier.position) < d) rank++;
                    // (as formações vão de -1 a 0: o atacante é quem joga mais adiantado, não x > 0)
                    if (p.basePos.x > -0.25f && rank > 0)
                    {
                        // atacante: corre para frente, mas para na linha do penúltimo defensor
                        // ataca a profundidade: corre até a linha da zaga (o impedimento segura logo abaixo)
                        bool fieldM = mode.id == GameModes.Field;
                        float deep = fieldM ? Mathf.Lerp(6f, 16f, p.work) : Mathf.Lerp(2.5f, 7f, p.work);
                        target.x = Mathf.Max(target.x * dir, carrier.position.x * dir + deep) * dir;
                        if (mode.offside)
                        {
                            float line = OffsideLineX(p.team);
                            if ((target.x - line) * dir > -0.6f) target.x = line - dir * (0.6f + 0.6f * (1f - p.discipline));
                        }
                    }
                    else if (rank < 2 && d < (mode.id == GameModes.Field ? 22f : halfLen * 0.7f) && !p.isKeeper)
                    {
                        bool small = mode.id != GameModes.Field;
                        float dist = (small ? 4.5f : 8f) + (small ? 2f : 4f) * p.passing;
                        Vector2 offer;
                        if (rank == 0)
                        {
                            // linha de passe em diagonal à frente, do lado da posição de origem
                            float side = p.basePos.y >= carrier.basePos.y ? 1f : -1f;
                            offer = carrier.position + new Vector2(dir, 0).Rotated(side * (0.55f + 0.35f * p.flair)) * dist;
                        }
                        else
                        {
                            // apoio: atrás e ao lado (opção de recuar e virar o jogo)
                            float side = p.basePos.y >= carrier.basePos.y ? 1f : -1f;
                            offer = carrier.position + new Vector2(-dir, 0).Rotated(-side * 1.0f) * dist * 0.8f;
                        }
                        target = Vector2.Lerp(target, offer, 0.45f + 0.25f * p.work);
                    }
                    else
                    {
                        // longe da bola: abre o campo (amplitude) em vez de ir atrás dela
                        target.y = Mathf.Lerp(target.y, p.basePos.y * halfW * 0.92f, 0.5f);
                    }
                }
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

        /// <summary>
        /// Consciência defensiva: cada um marca o atacante mais próximo da sua zona (que ainda
        /// não tenha marcador), por dentro, entre ele e o gol. Na quadra e na rua a marcação é
        /// mais individual; no campo, mistura marcação e posição.
        /// </summary>
        Vector2 Mark(SimPlayer p, Vector2 target)
        {
            if (p.isKeeper) return target;
            bool field = mode.id == GameModes.Field;
            float zone = field ? 13f : mode.id == GameModes.Futsal ? 8f : 6f;
            var ownGoal = new Vector2(-halfLen * TeamDir(p.team), 0f);
            SimPlayer best = null;
            float bd = zone;
            foreach (var o in players)
            {
                if (o.team == p.team || o.isKeeper || o.id == ball.owner || _marked.Contains(o.id)) continue;
                float d = o.position.Dist(target);
                if (d < bd) { bd = d; best = o; }
            }
            if (best == null) return target;
            _marked.Add(best.id);
            float gap = field ? 1.8f : 1.1f;
            var mark = best.position + (ownGoal - best.position).Norm() * gap;
            float k = field ? 0.55f : 0.8f;
            var t = Vector2.Lerp(target, mark, k);
            t.x = Mathf.Clamp(t.x, -halfLen + 0.5f, halfLen - 0.5f);
            t.y = Mathf.Clamp(t.y, -halfW + 0.5f, halfW - 0.5f);
            return t;
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
                if (k.isKeeper && k.team == t && (ball.pos.Dist(k.position) < 3.5f || k.dive > 0f || k.hold > 0f)) return true;
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
            else p.intent = (to / d) * Mathf.Clamp(d / (urgent ? 1.2f : mode.id == GameModes.Field ? 4f : 2.4f), 0.15f, 1f);   // quadra: tudo mais perto, chega mais rápido   // indo na bola: chega forte, não freia antes
            // chegando rápido num alvo que ficou de lado/para trás: freia primeiro (antes ele não
            // conseguia fazer a curva e ficava orbitando o ponto, rodando sem parar)
            float v = p.vel.magnitude;
            if (!urgent && v > 1.4f && d > 0.01f && d < v * v / 5f + 0.6f && Vector2.Dot(p.vel / v, to / d) < 0.55f)
                p.intent = Vector2.zero;
            float lazy = 1.35f - 0.7f * p.work;   // quem tem mais disposição pega o pique antes
            p.wantSprint = urgent || (d > (p.sprinting ? 7f : 11f) * lazy && p.stamina > 0.25f);
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
                // com a bola nas mãos: dá alguns passos à frente (sem sair da área) antes de repor
                if (p.hold > 0.6f && Mathf.Abs(p.position.x - gx) < Mathf.Max(2f, mode.boxDepth - 3f)) p.intent = new Vector2(dir * 0.3f, 0f);
                if (!frozen && p.hold > 0f)
                {
                    p.hold -= DT;
                    p.heldT += DT;
                    // ninguém livre: espera mais um pouco (até 4 s no futsal, 5 s nos outros)
                    float maxHold = mode.id == GameModes.Futsal ? 3.6f : 5f;
                    if (p.hold <= 0f && !KeeperDistribute(p, p.heldT >= maxHold)) p.hold = 0.3f;
                    if (ball.owner != p.id) p.heldT = 0f;
                }
                else if (!frozen && state == MatchState.Playing) Pass(p, new Vector2(dir, 0), 0.5f, false, true);
                return;
            }
            Vector2 target = goal;
            bool urgent = false;
            p.keeperSet = false;
            var toBall = ball.pos - goal;
            bool threat = false;
            // reação: o goleiro lê a trajetória ~0,15-0,27 s depois do chute (bons goleiros antes;
            // com jogadores na frente, a bola aparece tarde)
            int react = Mathf.RoundToInt(Mathf.Lerp(21f, 13f, p.defense)) - (p.Has(Trait.Reflexo) ? 2 : 0) + (Screened(p) ? 4 : 0);
            bool reacting = tick - lastShotTick < react;
            if (ball.owner < 0 && ball.vel.x * dir < -5f && !reacting)
            {
                // trajetória até a linha do goleiro (não a do gol): é ali que ele precisa estar
                float planeX = Mathf.Abs(p.position.x - gx) < 3f ? p.position.x : gx;
                float tK = (planeX - ball.pos.x) / ball.vel.x;
                float tCross = (gx - ball.pos.x) / ball.vel.x;
                if (tK > 0f && tK < 2.2f)
                {
                    // leitura imperfeita: chute forte e de longe engana mais (o goleiro ruim mais ainda)
                    float readErr = ((p.id * 7919 + lastShotTick * 31) % 1000 / 1000f - 0.5f) * 2f;
                    float yK = ball.pos.y + ball.vel.y * tK + readErr * Mathf.Clamp01(ball.Speed / 30f) * Mathf.Lerp(0.55f, 0.2f, p.defense);
                    float zK = Mathf.Max(0f, ball.z + ball.vz * tK - 0.5f * SimBall.G * tK * tK);
                    float yCross = ball.pos.y + ball.vel.y * tCross;
                    float zCross = ball.z + ball.vz * tCross - 0.5f * SimBall.G * tCross * tCross;
                    bool over = zCross > goalH + 0.35f && ball.vz < 2f;
                    bool wide = Mathf.Abs(yCross) > goalHalf + 0.6f;   // vai para fora: só acompanha
                    if (Mathf.Abs(yCross) < goalHalf + 1.5f && !over)
                    {
                        threat = true;
                        p.keeperSet = true;
                        target = new Vector2(planeX, Mathf.Clamp(yK, -goalHalf, goalHalf));
                        urgent = true;
                        float lat = yK - p.position.y;
                        float side = Mathf.Abs(lat);
                        float gs = Mathf.Clamp(goalHalf / 3.66f, 0.6f, 1f);
                        float armReach = 0.6f * gs;
                        // dá tempo de chegar de lado (passadas laterais ~4 m/s) e ainda encaixar de frente?
                        bool stepIn = side - armReach < (tK - 0.15f) * Lerp01(1.5f, 2.2f, p.defense);
                        if (p.saveKind == 0 && !wide)
                        {
                            if (side <= armReach || (stepIn && tK > 0.32f))
                            {
                                // encaixe: de frente, baixo (cai sobre a bola) ou saltando no alto
                                if (tK < 0.45f)
                                    StartSave(p, zK < 0.45f ? 2 : zK > 1.75f ? 6 : 1, Mathf.Sign(lat), zK, side, Mathf.Max(0.35f, tK + 0.25f));
                            }
                            else if (tK < Mathf.Lerp(0.5f, 0.62f, p.defense))
                            {
                                // ponte: rasteira, meia-altura ou no ângulo
                                int kind = zK < 0.55f ? 3 : zK > 1.45f ? 5 : 4;
                                StartSave(p, kind, Mathf.Sign(lat), zK, side, 0.95f);
                                p.dive = 0.6f;
                                // impulso lateral: o necessário para as mãos chegarem, até o limite da impulsão
                                float need = Mathf.Max(0f, side - DiveReach(p, kind) * 0.85f) / Mathf.Max(0.18f, tK);
                                float push = Mathf.Clamp(need, 1.6f, Lerp01(2.8f, 3.9f, p.defense) * gs);
                                p.vel = new Vector2((planeX - p.position.x) * 2f, Mathf.Sign(lat) * push);
                                Emit("dive", p.team, p.id);
                            }
                        }
                    }
                }
            }
            if (!threat)
            {
                // na bissetriz do ângulo (linha bola -> centro do gol), mais adiantado quanto mais longe a bola
                float outD = Mathf.Clamp(toBall.magnitude * 0.12f, 0.6f, mode.boxDepth * 0.3f);
                target = toBall.magnitude > 0.1f ? goal + toBall.normalized * outD : goal + new Vector2(dir, 0);
                target.y = Mathf.Clamp(target.y, -goalHalf - 0.5f, goalHalf + 0.5f);
                // mano a mano: atacante com a bola de frente para o gol e ninguém na cobertura -> sai fechando o ângulo
                var own = Owner;
                if (own != null && own.team != p.team && toBall.magnitude < (mode.id == GameModes.Field ? 15f : halfLen * 0.45f) && Mathf.Abs(ball.pos.y) < goalHalf * 3f)
                {
                    bool covered = false;
                    foreach (var m in players)
                        if (m.team == p.team && !m.isKeeper && V2.ClosestOnSegment(m.position, ball.pos, goal).Dist(m.position) < 1.2f && (m.position - goal).magnitude < toBall.magnitude) covered = true;
                    if (!covered)
                    {
                        float rush = Mathf.Min(toBall.magnitude * (0.35f + 0.15f * p.defense), mode.id == GameModes.Field ? 7f : 3.5f);
                        target = goal + toBall.normalized * rush;
                        urgent = toBall.magnitude < (mode.id == GameModes.Field ? 9f : 5f);
                    }
                }
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

        static float DistToSegment(Vector3 q, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            return (a + ab * t - q).magnitude;
        }

        static float Lerp01(float a, float b, float t) => Mathf.Lerp(a, b, Mathf.Clamp01(t));

        void StartSave(SimPlayer p, int kind, float side, float h, float dist, float secs)
        {
            p.saveKind = kind;
            p.saveSide = side == 0f ? 1f : side;
            p.saveH = h;
            p.saveDist = dist;
            p.saveT = p.saveTotal = secs;
            Emit("save_start", p.team, p.id, null, false, h, kind);
        }

        /// <summary>Alcance das mãos no mergulho (do centro do corpo às pontas dos dedos), por tipo de defesa.</summary>
        float DiveReach(SimPlayer p, int kind)
        {
            float gs = Mathf.Clamp(goalHalf / 3.66f, 0.6f, 1f);
            float body = kind == 5 ? 1.35f : kind == 4 ? 1.4f : kind == 3 ? 1.3f : 0.7f;
            return (body + 0.15f * p.defense + (p.Has(Trait.Reflexo) ? 0.1f : 0f)) * gs;
        }

        /// <summary>
        /// Onde estão as mãos do goleiro agora (x, y no campo; z altura). Na ponte as mãos saem do
        /// corpo para o lado do mergulho, na altura escolhida; parado, à frente do peito.
        /// </summary>
        Vector3 KeeperHands(SimPlayer p, out float radius)
        {
            float dir = TeamDir(p.team);
            radius = 0.34f;
            if (p.saveKind >= 3 && p.saveTotal > 0f)
            {
                float u = 1f - p.saveT / p.saveTotal;
                // esticando: chega ao alcance máximo em ~0,25 s e mantém no voo
                float ext = DiveReach(p, p.saveKind) * Mathf.Sin(Mathf.Clamp01(u * 2.6f) * Mathf.PI * 0.5f);
                float hz = p.saveKind == 3 ? 0.22f : p.saveKind == 5 ? Mathf.Clamp(p.saveH, 1.5f, 2.55f) : Mathf.Clamp(p.saveH, 0.6f, 1.5f);
                radius = 0.3f;
                return new Vector3(p.position.x + dir * 0.25f, p.position.y + p.saveSide * ext * 0.9f, Mathf.Lerp(1.0f, hz, Mathf.Clamp01(u * 3f)));
            }
            float hzStand = p.saveKind == 2 ? 0.25f : p.saveKind == 6 ? Mathf.Clamp(p.saveH, 1.8f, 2.7f) : p.saveKind == 1 ? Mathf.Clamp(p.saveH, 0.4f, 1.9f) : 1.0f;
            return new Vector3(p.position.x + dir * 0.35f, p.position.y, hzStand);
        }

        /// <summary>Há jogadores entre a bola e o goleiro (ele vê a bola tarde)?</summary>
        bool Screened(SimPlayer k)
        {
            foreach (var m in players)
            {
                if (m == k || m.id == ball.lastTouchId) continue;
                if (V2.ClosestOnSegment(m.position, ball.pos, k.position).Dist(m.position) < 0.6f && m.position.Dist(k.position) > 1.2f) return true;
            }
            return false;
        }

        /// <summary>
        /// Reposição do goleiro com a bola nas mãos: companheiro livre por perto = arremesso com as
        /// mãos (rolada rasteira no curto, por cima no médio); ninguém livre ou longe = chutão de
        /// bate-pronto para o ataque (no futsal só com as mãos, como na regra).
        /// </summary>
        /// <summary>Menor distância de um adversário à linha do passe (quem pode cortar no caminho).</summary>
        public float LaneFree(int team, Vector2 from, Vector2 to)
        {
            var d = to - from;
            float L = d.magnitude;
            if (L < 0.1f) return 99f;
            var u = d / L;
            float best = 99f;
            foreach (var o in players)
            {
                if (o.team == team || o.sentOff) continue;
                float t = Vector2.Dot(o.position - from, u);
                if (t < 1f || t > L + 1.5f) continue;
                float side = Mathf.Abs(Vector2.Dot(o.position - from, new Vector2(-u.y, u.x)));
                // quem está mais perto da linha e mais perto do passador chega antes da bola
                best = Mathf.Min(best, side - Mathf.Max(0f, (L - t) * 0.04f));
            }
            return best;
        }

        /// <summary>
        /// Reposição do goleiro: só entrega para quem está livre E com a linha do passe livre
        /// (nada de rolar a bola no pé do adversário). Ninguém livre: chutão para o mais
        /// adiantado (campo/society) ou, no futsal, o menos marcado mesmo que seja para o lado.
        /// </summary>
        bool KeeperDistribute(SimPlayer p, bool force = true)
        {
            float dir = TeamDir(p.team);
            bool field = mode.id == GameModes.Field;
            bool canPunt = mode.id != GameModes.Futsal;
            float throwMax = field ? 30f : mode.id == GameModes.Futsal ? halfLen * 1.4f : 20f;
            float needFree = field ? 4.5f : 2.6f, needLane = field ? 2.2f : 1.5f;
            SimPlayer best = null, far = null, safest = null;
            float bestS = float.MinValue, farS = float.MinValue, safeS = float.MinValue;
            foreach (var m in players)
            {
                if (m.team != p.team || m == p || m.sentOff) continue;
                float free = float.MaxValue;
                foreach (var o in players) if (o.team != p.team && !o.sentOff) free = Mathf.Min(free, o.position.Dist(m.position));
                float d = m.position.Dist(p.position);
                float ahead = (m.position.x - p.position.x) * dir;
                float lane = LaneFree(p.team, p.position, m.position);
                float safe = Mathf.Min(free, lane * 1.6f);
                if (ahead > 0.5f && safe > safeS) { safeS = safe; safest = m; }
                if (ahead < 2f) continue;   // nunca para trás nem para o lado colado no gol
                if (d < throwMax && free > needFree && lane > needLane)
                {
                    float sc = Mathf.Min(free, 10f) + Mathf.Min(lane, 6f) * 0.5f - d * 0.06f + ahead * 0.04f + rng.Range(0f, 1.2f);
                    if (sc > bestS) { bestS = sc; best = m; }
                }
                // chutão: o mais adiantado e menos marcado
                float fs = ahead * 0.1f + Mathf.Min(free, 8f) * 0.5f + rng.Range(0f, 1f);
                if (fs > farS) { farS = fs; far = m; }
            }
            if (best == null && !force) return false;   // espera alguém se desmarcar
            bool punt = canPunt && (best == null || rng.Chance(field ? 0.08f : 0.05f));   // com alguém livre, quase sempre sai jogando
            if (punt && far != null) { Punt(p, far); return true; }
            if (best == null) best = safest ?? far;
            if (best != null) KeeperThrow(p, best);
            else Pass(p, new Vector2(dir, rng.Range(-0.3f, 0.3f)), 0.75f, false, true);
            return true;
        }

        /// <summary>Arremesso do goleiro com as mãos (rasteiro no curto, por cima no médio).</summary>
        void KeeperThrow(SimPlayer p, SimPlayer m)
        {
            float d0 = m.position.Dist(p.position);
            var target = m.position + m.vel * (0.25f + d0 / 25f);
            var to = target - p.position;
            float d = Mathf.Max(to.magnitude, 1f);
            var dirv = (to / d).Rotated(rng.Gauss() * 0.025f * (1.3f - p.passing));
            ball.pos = p.position + dirv * 0.45f;
            if (d < (mode.id == GameModes.Field ? 15f : 10f))
            {
                // rolada: a bola sai rente ao chão, rápida o bastante para chegar redonda
                float vEnd = mode.id == GameModes.Field ? 3.5f : 2.5f;
                float v0 = Mathf.Sqrt(vEnd * vEnd + 2f * mode.rollDecel * d);
                ball.vel = dirv * v0;
                ball.z = 0.12f;
                ball.vz = 0f;
            }
            else
            {
                // por cima do ombro: sai da mão alta e cai no pé do companheiro
                float T = 0.55f + d / 26f;
                float h0 = 2.0f;
                ball.z = h0;
                ball.vel = dirv * (d * 0.95f / T);
                ball.vz = (0.4f - h0 + 0.5f * SimBall.G * T * T) / T;
            }
            ball.spin = 0f;
            passReceiver = m.id;
            passReceiverT = 3f;
            passTick = tick; lastPasser = p.id;
            passFrom = ball.pos;
            ReleaseBall(p);
            protectTeam = p.team;
            protectT = 0.8f;
            p.kickKind = InputFrame.Pass;
            Emit("kick", p.team, p.id, "gkthrow", power: 0.4f);
        }

        /// <summary>Chutão de bate-pronto: a bola cai da mão e sai alta para o campo de ataque.</summary>
        void Punt(SimPlayer p, SimPlayer m)
        {
            float dir = TeamDir(p.team);
            bool field = mode.id == GameModes.Field;
            float reach = field ? rng.Range(42f, 60f) * (0.85f + 0.3f * p.passing) : halfLen * rng.Range(0.9f, 1.3f);
            Vector2 target = m != null ? m.position + m.vel * 1.2f : p.position + new Vector2(dir * reach, 0f);
            // não passa do alcance do chute (e não vai para o próprio campo)
            var to = target - p.position;
            if (to.x * dir < 8f) to = new Vector2(dir * reach, to.y);
            if (to.magnitude > reach) to = to.normalized * reach;
            float d = to.magnitude;
            var dirv = (to / d).Rotated(rng.Gauss() * 0.06f);
            float T = field ? 2.3f + d / 60f : 1.3f + d / 40f;
            float h0 = 0.6f;
            ball.pos = p.position + dirv * 0.5f;
            ball.z = h0;
            ball.vel = dirv * (d * 0.9f / T);
            ball.vz = Mathf.Min(24f, (0f - h0 + 0.5f * SimBall.G * T * T) / T);
            ball.spin = rng.Gauss() * 0.6f;
            if (m != null) { passReceiver = m.id; passReceiverT = 4f; passTick = tick; lastPasser = p.id; passFrom = ball.pos; }
            ReleaseBall(p);
            protectTeam = p.team;
            protectT = 0.6f;
            p.kickKind = InputFrame.Lob;
            Emit("kick", p.team, p.id, "punt", power: 1f);
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
            if (p.sentOff)
            {
                // expulso: fora de campo; no futsal volta (outro atleta) depois de 2 minutos ou de um gol sofrido
                if (p.sentOffT > 0f && state == MatchState.Playing) p.sentOffT -= DT;
                if (p.sentOffT > 0f || p.sentOffT < 0f) { ParkSentOff(p); return; }
                p.sentOff = false;
                p.yellows = 0;
                p.stun = 0f;
                p.position = new Vector2(-TeamDir(p.team) * 2f, -(halfW - 0.5f));
            }
            p.kickCd = Mathf.Max(0f, p.kickCd - DT);
            p.tackleAnim = Mathf.Max(0f, p.tackleAnim - DT);
            p.kickAnim = Mathf.Max(0f, p.kickAnim - DT);
            p.dive = Mathf.Max(0f, p.dive - DT);
            if (p.saveT > 0f) { p.saveT -= DT; if (p.saveT <= 0f) { p.saveKind = 0; p.saveT = 0f; } }
            p.skillCd = Mathf.Max(0f, p.skillCd - DT);
            p.burst = Mathf.Max(0f, p.burst - DT);
            p.offBalance = Mathf.Max(0f, p.offBalance - DT);
            p.touchT = Mathf.Max(0f, p.touchT - DT);
            if (p.down > 0f) p.down = Mathf.Max(0f, p.down - DT);
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
            if (p.stun > 0f || p.down > 0f)
            {
                p.stun = Mathf.Max(0f, p.stun - DT);
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
            // espaço entre companheiros: quem não está com a bola nem sob controle do jogador
            // desvia de quem está perto (não se trombam nem se empurram disputando o mesmo lugar)
            if (!hasBall && !p.isKeeper && ctrl[p.team].player != p.id && p.id != passReceiver && !(setPiece && !kickIn))
            {
                float R = mode.id == GameModes.Field ? 3.2f : 2.0f;
                var sep = Vector2.zero;
                foreach (var m in players)
                {
                    if (m == p || m.team != p.team || m.sentOff) continue;
                    // prevê ~0,35 s à frente: desvia antes de cruzar a corrida do companheiro
                    var d = (p.position + p.vel * 0.35f) - (m.position + m.vel * 0.35f);
                    float l = Mathf.Min(d.magnitude, (p.position - m.position).magnitude + 0.5f);
                    // quem está com a bola ou sob controle do jogador não desvia: os outros abrem mais espaço para ele
                    bool lead = m.id == ball.owner || ctrl[m.team].player == m.id;
                    float rr = lead ? R * 1.5f : R;
                    if (l >= rr) continue;
                    if (l < 0.01f) d = new Vector2(p.id % 2 == 0 ? 1f : -1f, 0.3f);
                    float w = (rr - l) / rr;
                    // quem está mais perto da bola tem preferência: o outro abre espaço
                    float pri = lead ? 2.2f : m.position.Dist(ball.pos) < p.position.Dist(ball.pos) ? 1.15f : 0.7f;
                    sep += d.Norm() * w * w * pri;
                }
                if (sep.sqrMagnitude > 1e-4f)
                {
                    float sm = Mathf.Min(sep.magnitude, 1.4f);
                    intent = (intent + sep.Norm() * sm * 1.3f).Limit(1f);
                }
            }
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
            if (p.isKeeper && p.saveKind >= 3 && p.saveT > 0f)
            {
                // no ar: o impulso do mergulho segue (balístico) e freia ao cair no chão; sem "correr" deitado
                float u = 1f - p.saveT / Mathf.Max(0.01f, p.saveTotal);
                // voo quase sem freio; ao tocar o chão, desliza e para aos poucos (antes parava de uma vez)
                float drag = Mathf.Lerp(0.3f, 6.5f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.38f) / 0.3f)));
                p.vel -= p.vel * Mathf.Min(1f, drag * DT);
                // o mergulho nunca leva o goleiro para dentro do próprio gol (antes ele "entrava" na rede,
                // sumia atrás dela e a bola ia junto)
                float gx = -halfLen * TeamDir(p.team);
                float inward = -Mathf.Sign(gx);   // sentido do campo
                if ((p.position.x - gx) * inward < 0.35f && p.vel.x * inward < 0f) p.vel.x = 0f;
                p.position += p.vel * DT;
                if ((p.position.x - gx) * inward < 0.25f) p.position.x = gx + inward * 0.25f;
                return;
            }
            if (p.isKeeper && p.dive > 0f) maxS = Mathf.Lerp(3.6f, 4.8f, p.defense);
            else if (p.isKeeper && p.keeperSet) maxS = Mathf.Min(maxS, Mathf.Lerp(2.0f, 2.9f, p.defense));   // passadas laterais
            if (p.hold > 0f) maxS *= 0.4f;
            var targetV = intent * maxS;
            float spd = p.vel.magnitude;
            float acc = Mathf.Lerp(5f, 8.5f, p.accel) * (p.burst > 0f ? 1.6f : 1f) * (p.offBalance > 0f ? 0.4f : 1f) * (0.82f + 0.18f * p.stamina);
            // movimento do atleta em duas componentes (ao longo da corrida e de lado):
            //   • arrancada forte parado e cada vez menor perto da velocidade máxima;
            //   • frenagem limitada pelo atrito (mais curta devagar, mais longa no pique);
            //   • curva limitada pela aceleração lateral (aberta no pique, fechada devagar)
            var dv = targetV - p.vel;
            float agil = (0.85f + 0.3f * p.accel) * (p.offBalance > 0f ? 0.4f : 1f) * (p.burst > 0f ? 1.3f : 1f);
            if (spd > 0.6f)
            {
                var tdir = p.vel / spd;
                var ndir = new Vector2(-tdir.y, tdir.x);
                float dT = Vector2.Dot(dv, tdir), dN = Vector2.Dot(dv, ndir);
                float top = Mathf.Max(4f, p.TopSpeed());
                float accT = acc * Mathf.Clamp(1.35f - spd / top, 0.3f, 1.35f);
                float brake = Mathf.Lerp(14f, 8.5f, Mathf.Clamp01(spd / 9f)) * agil;
                float lat = Mathf.Lerp(17f, 7.5f, Mathf.Clamp01(spd / 9f)) * agil * (hasBall ? 0.85f : 1f);
                dT = Mathf.Clamp(dT, -brake * DT, accT * DT);
                dN = Mathf.Clamp(dN, -lat * DT, lat * DT);
                p.vel += tdir * dT + ndir * dN;
            }
            else p.vel += dv.Limit(acc * 1.35f * DT);   // saindo do lugar: para qualquer lado
            p.position += p.vel * DT;
            var faceTarget = p.vel.magnitude > 0.4f ? p.vel : intent;
            if (p.faceBall && !hasBall && !p.sprinting && ctrl[p.team].player != p.id) faceTarget = ball.pos - p.position;
            float turnRate = Mathf.Lerp(11f, 4f, Mathf.Clamp01(p.vel.magnitude / 9f)) * (hasBall ? 0.8f : 1f);
            // devagar, sem a bola e sem comando: o corpo fica de frente para o jogo (dá passos de lado/de
            // costas) em vez de seguir a direção de cada empurrãozinho — era isso que fazia o atleta rodar
            if (!hasBall && ctrl[p.team].player != p.id && p.vel.magnitude < 1.8f && !p.isKeeper)
            {
                faceTarget = ball.pos - p.position;
                turnRate = 4.5f;
            }
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
            if (p.windup > 0f)
            {
                // preparando o chute: o corpo vira para a mira (o pé encontra a bola de frente para o alvo)
                if (p.windupKind == InputFrame.Shoot && !(throwIn && p.id == throwInTaker))
                {
                    // chute: o corpo se alinha com o gol (o erro do chute cresce com o corpo torto)
                    float gy = Mathf.Clamp(p.windupAim.y * goalHalf, -goalHalf * 0.8f, goalHalf * 0.8f);
                    faceTarget = new Vector2(halfLen * TeamDir(p.team), gy) - p.position;
                    turnRate = 11f;
                }
                else if (p.windupAim.sqrMagnitude > 0.04f) { faceTarget = p.windupAim; turnRate = 9f; }
                else turnRate *= 0.4f;
            }
            var nf = V2.RotateToward(p.facing, faceTarget.Norm(), turnRate * DT).Norm();
            if (nf != Vector2.zero) p.facing = nf;
            if (p.sprinting) p.stamina = Mathf.Max(0f, p.stamina - 0.055f * DT * (p.Has(Trait.Resistencia) ? 0.65f : 1f));
            else p.stamina = Mathf.Min(1f, p.stamina + 0.03f * DT);
            p.runPhase += p.vel.magnitude * DT * 2.3f;
            ClampPlayer(p);
        }

        /// <summary>
        /// Nenhum goleiro dentro do próprio gol, nem depois das colisões (empurrões de atacantes e
        /// companheiros podiam jogá-lo para trás da linha, onde ele sumia dentro da rede).
        /// </summary>
        void ClampKeepers()
        {
            foreach (var p in players)
            {
                if (!p.isKeeper) continue;
                float gx = -halfLen * TeamDir(p.team), inward = -Mathf.Sign(gx);
                if (Mathf.Abs(p.position.y) < goalHalf + 0.8f && (p.position.x - gx) * inward < 0.25f)
                {
                    p.position.x = gx + inward * 0.25f;
                    if (p.vel.x * inward < 0f) p.vel.x = 0f;
                }
            }
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
            if (setPiece && p.team != setPieceTeam && !wallIds.Contains(p.id))
            {
                // respeitam a distância até a cobrança
                var d = p.position - setPieceSpot;
                if (d.magnitude < mode.restartDist - 0.2f)
                {
                    var outN = d.magnitude > 0.1f ? d.normalized : new Vector2(TeamDir(setPieceTeam), 0f);
                    p.position = setPieceSpot + outN * (mode.restartDist - 0.2f);
                    float vin = Vector2.Dot(p.vel, -outN);
                    if (vin > 0f) p.vel += outN * vin;   // parado na distância, sem correr no lugar
                }
            }
            if (kickoffPass && kickoffKicker >= 0 && p.team != players[kickoffKicker].team)
            {
                // adversários fora do círculo central até a bola rolar
                float rr = mode.centerR + 0.3f;
                if (p.position.magnitude < rr)
                {
                    p.position = p.position.magnitude > 0.1f ? p.position.normalized * rr : new Vector2(-TeamDir(p.team) * rr, 0f);
                    var on = p.position.normalized;
                    float vin = Vector2.Dot(p.vel, -on);
                    if (vin > 0f) p.vel += on * vin;
                }
            }
            float m = mode.runMargin, r = SimPlayer.Radius;
            p.position.x = Mathf.Clamp(p.position.x, -halfLen - m + r, halfLen + m - r);
            p.position.y = Mathf.Clamp(p.position.y, -halfW - m + r, halfW + m - r);
            // goleiro não entra no próprio gol (fica na linha ou à frente dela, entre as traves)
            if (p.isKeeper && Mathf.Abs(p.position.y) < goalHalf + 0.6f)
            {
                float gx = -halfLen * TeamDir(p.team), inward = -Mathf.Sign(gx);
                if ((p.position.x - gx) * inward < 0.2f) { p.position.x = gx + inward * 0.2f; if (p.vel.x * inward < 0f) p.vel.x = 0f; }
            }
        }

        void ResolveCollisions()
        {
            int n = players.Count;
            float minD = SimPlayer.Radius * 2f;
            for (int i = 0; i < n; i++)
            {
                var a = players[i];
                if (a.sentOff) continue;
                for (int j = i + 1; j < n; j++)
                {
                    if (players[j].sentOff) continue;
                    var b = players[j];
                    var d = b.position - a.position;
                    float l = d.magnitude;
                    // goleiro em ação (defesa, mergulho, bola nas mãos): o companheiro não o atropela
                    float need = minD;
                    if (a.team == b.team && (a.isKeeper || b.isKeeper))
                    {
                        var gk = a.isKeeper ? a : b;
                        if (gk.dive > 0f || gk.hold > 0f || ball.pos.Dist(gk.position) < 2.5f) need = 1.5f;
                    }
                    if (l < need && l > 0.0001f && need > minD)
                    {
                        var other = a.isKeeper ? b : a;
                        var gk = a.isKeeper ? a : b;
                        var away = (other.position - gk.position) / l;
                        other.position = gk.position + away * need;
                        other.vel -= away * Mathf.Min(0f, Vector2.Dot(other.vel, away));
                        continue;
                    }
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
                                // choque de corpo: impulso só na direção do contato (inelástico, e = 0,2);
                                // quem é mais forte/rápido "pesa" mais e perde menos velocidade; o resto
                                // da velocidade (de lado) continua — um passa raspando pelo outro
                                var nAB = d / l;
                                float ma = 1f + a.defense * 0.35f + a.vel.magnitude * 0.03f, mb = 1f + b.defense * 0.35f + b.vel.magnitude * 0.03f;
                                float imp = closing * 1.2f / (1f / ma + 1f / mb);
                                a.vel -= nAB * (imp / ma);
                                b.vel += nAB * (imp / mb);
                                a.vel *= 0.92f; b.vel *= 0.92f;
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
                        // ninguém fica "correndo parado" empurrando o outro: some a velocidade contra o contato
                        var nn = d / l;
                        float va = Vector2.Dot(a.vel, nn), vb = Vector2.Dot(b.vel, -nn);
                        if (va > 0f) a.vel -= nn * va * (a.team == b.team ? 1f : 0.6f);
                        if (vb > 0f) b.vel += nn * vb * (a.team == b.team ? 1f : 0.6f);
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
                if (o.saveT > 0f && o.saveKind >= 3)
                {
                    // agarrou no mergulho: a bola fica nas mãos esticadas durante o voo e a queda
                    // (não "teleporta" para o peito)
                    var hd = KeeperHands(o, out _);
                    var tgt = new Vector2(hd.x, hd.y);
                    ball.pos = Vector2.Lerp(ball.pos, tgt, 0.5f);
                    ball.z = Mathf.Max(0f, Mathf.Lerp(ball.z, hd.z - ball.radius, 0.5f));
                    ball.vz = 0f;
                    ball.vel = o.vel;
                    return;
                }
                ball.pos = o.position + o.facing * 0.35f;
                ball.pos.x = Mathf.Clamp(ball.pos.x, -halfLen + ball.radius, halfLen - ball.radius);
                ball.z = 1f;
                ball.vz = 0f;
                ball.vel = o.vel;
                return;
            }
            float spd = o.vel.magnitude;
            float lose = spd > 5f ? 2.4f : 1.7f;
            if (o.windup > 0f) lose = 2.8f;   // preparando o chute: a bola é trazida ao pé, não se perde por distância
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
            if (throwIn && o.id == throwInTaker)
            {
                // bola nas mãos, acima da cabeça, com os pés na linha lateral
                o.position = throwInSpot;
                o.vel = Vector2.zero;
                ball.pos = throwInSpot - o.facing * 0.06f;
                ball.vel = Vector2.zero;
                ball.z = 2.05f; ball.vz = 0f; ball.spin = 0f;
                return;
            }
            if (kickoffPass && o.id == kickoffKicker)
            {
                // saída de bola: a bola fica exatamente na marca do meio-campo até o toque
                ball.pos = Vector2.zero;
                ball.vel = Vector2.zero;
                ball.z = 0f; ball.vz = 0f; ball.spin = 0f;
                o.position = -o.facing * 0.5f;
                o.vel = Vector2.zero;
                return;
            }
            if (setPiece && o.id == setPieceTaker)
            {
                // bola parada no lugar da falta; o cobrador gira em volta dela para mirar
                ball.pos = setPieceSpot;
                ball.vel = Vector2.zero;
                ball.z = 0f; ball.vz = 0f; ball.spin = 0f;
                o.position = setPieceSpot - o.facing * 0.5f;
                return;
            }
            if (o.windup > 0f && ball.z < 0.8f)
            {
                // preparação do chute/passe: a bola chega ao ponto do pé de chute exatamente quando
                // a perna desce (ao fim da preparação) — o pé não chuta o ar nem a bola longe dele
                var f = o.facing == Vector2.zero ? Vector2.right : o.facing;
                var side = new Vector2(f.y, -f.x);   // à direita do corpo (pé direito)
                var spot = o.position + o.vel * o.windup + f * 0.42f + side * 0.1f;
                float frac = Mathf.Clamp01(DT / Mathf.Max(o.windup, DT));
                ball.pos += (spot - ball.pos) * frac;
                ball.vel = o.vel;
                ball.z = Mathf.Max(0f, ball.z - 3f * DT);
                ball.vz = 0f;
                ball.spin = 0f;
                return;
            }
            // condução em toques (do trote ao pique): a bola é tocada à frente e o atleta vai atrás dela
            if (spd > 1.3f && !o.shielding && o.windup <= 0f && ball.z < 0.35f)
            {
                // o toque vai para onde o atleta quer ir (curvas suaves), não só para onde o corpo aponta
                var fwd = o.intent.sqrMagnitude > 0.09f ? (o.facing * 0.6f + o.intent.normalized * 0.4f).Norm() : o.facing;
                if (fwd == Vector2.zero) fwd = o.facing;
                var perp = new Vector2(-fwd.y, fwd.x);
                var rel = ball.pos - o.position;
                float along = Vector2.Dot(rel, fwd), side = Vector2.Dot(rel, perp);
                // a bola corre um pouco para o lado do pé que conduz (não "entre as pernas")
                float foot = (o.id % 4 == 0 ? 1f : -1f) * 0.09f * Mathf.Clamp01(spd / 3f);
                float sideErr = Mathf.Clamp(side - foot, -0.3f, 0.3f);
                // mudança brusca de direção com a bola perto: toque de corte na hora (responde ao comando)
                float turn = ball.vel.sqrMagnitude > 1f ? Vector2.Angle(ball.vel, fwd) : 0f;
                bool cut = turn > 50f && rel.magnitude < 1.1f && o.touchT <= 0f;
                if (along < 0.26f + spd * 0.012f || cut)
                {
                    // toque: a bola sai um pouco à frente (mais no pique e para quem tem menos controle;
                    // andando/trotando fica colada no pé)
                    float slow = Mathf.Clamp01((spd - 1.3f) / 3f);
                    float lead = o.sprinting ? Mathf.Lerp(0.8f, 0.45f, o.control) : Mathf.Lerp(0.16f, 1f, slow) * Mathf.Lerp(0.4f, 0.24f, o.control);
                    // marcador perto: toques mais curtos (bola protegida); campo livre: o habilidoso solta mais
                    var near = NearestOpponent(o);
                    if (near != null && near.position.Dist(o.position) < 2.6f) lead *= Mathf.Lerp(0.75f, 0.5f, o.control);
                    else if (o.sprinting && o.flair > 0.6f) lead *= 1.1f;
                    if (cut) lead *= 0.6f;   // corte: toque curto, a bola fica perto
                    float decel = mode.rollDecel + mode.groundDrag * spd * spd;
                    float extra = Mathf.Sqrt(2f * decel * lead);
                    ball.vel = fwd * (spd * (cut ? 0.85f : 1f) + extra) - perp * sideErr * 4f;
                    // o atleta passou da bola (arrancada): ela é puxada para o pé em vez de atravessar o corpo
                    if (along < 0.05f) ball.pos = Vector2.Lerp(ball.pos, o.position + fwd * 0.2f + perp * foot, 0.5f);
                    ball.z = 0f;
                    ball.vz = 0f;
                    ball.spin = 0f;
                    ball.slideT = 0f;
                    o.touchT = 0.18f;
                }
                else ball.vel -= perp * sideErr * 5f * DT;   // entre os toques a bola rola solta (leve correção de rumo)
                return;
            }
            float ahead = 0.3f + spd * 0.05f;   // parado/andando: bola no pé
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
                float d0 = p.position.Dist(ball.pos);
                float reach = 0.55f + 0.25f * p.control;
                float maxZ = 0.9f;
                if (keeperZone)
                {
                    // goleiro: mãos em 3D (posição real das mãos na defesa escolhida)
                    var hands = KeeperHands(p, out float hr);
                    var b3 = new Vector3(ball.pos.x, ball.pos.y, ball.z + ball.radius);
                    // segmento ombro -> mãos (o braço inteiro bloqueia) + corpo no chão na ponte
                    var sh = new Vector3(p.position.x, p.position.y, p.saveKind >= 3 ? Mathf.Lerp(1.2f, hands.z, 0.5f) : 1.35f);
                    // a bola anda ~0,4 m por passo: testa o caminho dela no passo (sem "atravessar" as mãos)
                    float dHand = 99f, dBody = 99f;
                    var bv3 = new Vector3(ball.vel.x, ball.vel.y, ball.vz) * DT;
                    var feet = new Vector3(p.position.x, p.position.y - p.saveSide * 0.9f, 0.25f);
                    for (int k = 0; k <= 4; k++)
                    {
                        var q = b3 - bv3 * (k / 4f);
                        dHand = Mathf.Min(dHand, DistToSegment(q, sh, hands));
                        if (p.saveKind >= 3) dBody = Mathf.Min(dBody, DistToSegment(q, feet, sh));
                        // de pé, o corpo inteiro fecha o espaço até ~1,9 m
                        else if (q.z < 1.8f) dBody = Mathf.Min(dBody, new Vector2(q.x - p.position.x, q.y - p.position.y).magnitude - 0.1f);
                    }
                    float dk = Mathf.Min(dHand, dBody);
                    if (dk > hr + (p.Has(Trait.Reflexo) ? 0.06f : 0f)) continue;
                    if (ball.owner < 0 && ball.Speed > 14f && dHand < dBody && dHand > hr * 0.65f)
                    {
                        // ponta dos dedos: desvia em vez de segurar
                        float miss = Mathf.Clamp01((dHand / hr - 0.65f) / 0.35f) * Mathf.Clamp01((ball.Speed - 14f) / 14f) * (1.1f - p.defense * 0.5f);
                        if (rng.Chance(miss))
                        {
                            // ponta dos dedos: impulso na direção da normal do contato (da mão para a bola),
                            // puxada para fora do gol e para o lado do mergulho — a bola raspa e desvia de
                            // verdade (às vezes para fora, rente à trave; às vezes ainda entra)
                            var n3 = b3 - hands;
                            if (n3.sqrMagnitude < 1e-4f) n3 = new Vector3(0f, p.saveSide, 0f);
                            n3 = (n3.normalized + new Vector3(TeamDir(p.team) * 0.35f, p.saveSide * 0.45f, hands.z > goalH * 0.75f ? 0.5f : 0f)).normalized;
                            var v3 = new Vector3(ball.vel.x, ball.vel.y, ball.vz);
                            float vn3 = Vector3.Dot(v3, n3);
                            float solid = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01((dHand / hr - 0.65f) / 0.35f)) * (0.8f + 0.3f * p.defense);
                            if (vn3 < 0f) v3 -= n3 * vn3 * 1.2f * solid;
                            v3 *= rng.Range(0.78f, 0.9f);
                            ball.vel = new Vector2(v3.x, v3.y);
                            ball.vz = v3.z;
                            ball.lastTouchTeam = p.team;
                            ball.lastTouchId = p.id;
                            p.kickCd = 0.35f;
                            Emit("touch", p.team, p.id, "fingertip");
                            continue;
                        }
                    }
                    if (ball.owner >= 0)
                    {
                        // tirar a bola do pé do atacante dentro da área (saída nos pés)
                        var o = players[ball.owner];
                        if (o.team == p.team || o.hold > 0f || d0 >= ownerD - 0.05f || tick - takeTick < 20) continue;
                    }
                    if (d0 < bestD) { bestD = d0; best = p; }
                    continue;
                }
                float d = p.position.Dist(ball.pos);
                if (d > reach) continue;
                if (keeperZone && ball.owner < 0 && d > reach * 0.7f && ball.Speed > 14f)
                {
                    // na ponta dos dedos: chute forte no limite do alcance pode passar (ou só desviar)
                    float miss = Mathf.Clamp01((d / reach - 0.7f) / 0.3f) * Mathf.Clamp01((ball.Speed - 14f) / 14f) * (1.1f - p.defense * 0.5f);
                    if (rng.Chance(miss))
                    {
                        ball.vel = ball.vel.Rotated(rng.Range(-0.12f, 0.12f)) * rng.Range(0.75f, 0.95f);
                        ball.lastTouchTeam = p.team;
                        ball.lastTouchId = p.id;
                        p.kickCd = 0.35f;
                        Emit("touch", p.team, p.id, "fingertip");
                        continue;
                    }
                }
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
            // recuo: passe com o pé de um companheiro não pode ser agarrado; o goleiro joga com os pés
            bool backPass = keeperZone && mode.id != GameModes.Street && ball.lastTouchTeam == p.team && ball.lastTouchId != p.id
                && ball.lastTouchId == lastPasser && tick - passTick < 240 && ball.owner < 0;
            if (keeperZone && backPass) Emit("backpass", p.team, p.id);
            if (keeperZone && !backPass)
            {
                float catchP = Mathf.Clamp(1.25f - rel / 24f + p.defense * 0.15f + (p.Has(Trait.GoleiroSeguro) ? 0.12f : 0f) - (p.dive > 0f ? 0.15f : 0f), 0.1f, 0.97f);
                if (rel > 8f && !rng.Chance(catchP))
                {
                    // espalmada: a bola volta para o campo e abre para o lado (escanteio ou rebote longe do gol)
                    // espalmada pela mão: a bola reflete na palma (normal = do corpo para a bola, puxada para
                    // fora do gol) com pouca energia; alta perto do travessão, vai por cima (escanteio)
                    float sideY = Mathf.Abs(ball.pos.y) > 0.3f ? Mathf.Sign(ball.pos.y) : (rng.Chance(0.5f) ? 1f : -1f);
                    var outDir = new Vector2(TeamDir(p.team), 0f);
                    // normal do contato a partir das mãos de verdade (no mergulho, longe do corpo)
                    var hp = KeeperHands(p, out _);
                    var hand = (ball.pos - new Vector2(hp.x, hp.y)).Norm();
                    if (p.saveKind >= 3 && hand == Vector2.zero) hand = new Vector2(0f, p.saveSide);
                    if (hand == Vector2.zero) hand = new Vector2(0f, sideY);
                    var n = (outDir * 0.6f + hand * 0.4f + new Vector2(0f, sideY * 0.25f)).Norm();
                    var vin = ball.vel;
                    float vn = Vector2.Dot(vin, n);
                    var refl = vin - n * (vn * 2f);
                    float e = rng.Range(0.28f, 0.45f) * (1.1f - p.defense * 0.3f);
                    var vout = refl * e;
                    if (Vector2.Dot(vout, outDir) < 3f) vout += outDir * (3f - Vector2.Dot(vout, outDir));
                    if (ball.z > goalH * 0.75f)
                    {
                        // ponta dos dedos por cima do travessão
                        vout = new Vector2(-outDir.x * rng.Range(1f, 2.5f), vin.y * 0.2f + sideY * rng.Range(0.5f, 2f));
                        ball.vz = rng.Range(3.5f, 5f);
                    }
                    else ball.vz = Mathf.Abs(ball.vz) * 0.25f + rng.Range(0.5f, 2.2f);
                    ball.vel = vout;
                    ball.spin = rng.Gauss() * 1.5f;
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
            // domínio: o pé vai na bola e amortece (a vista mostra o gesto; bola forte = toque mais longo)
            if (ball.z < 0.6f && rel > 3f) Emit("receive", p.team, p.id, null, rel > 11f, rel);
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
            if (throwIn && ball.owner == throwInTaker) return;   // bola nas mãos do cobrador do lateral
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
                    // (rua: sem mureta nas linhas de fundo — a bola sai e vira tiro de meta ou escanteio)
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
            // futsal: quem leva o gol jogando com um a menos completa o time
            foreach (var q in players) if (q.team != t && q.sentOff && q.sentOffT > 0f) q.sentOffT = 0.01f;
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

        /// <summary>Bola dentro do gol (comemoração): segue rolando/quicando até morrer na rede. Retorna a velocidade do choque com a rede (0 = sem choque).</summary>
        public float StepBallInNet()
        {
            float side = Mathf.Sign(ball.pos.x);
            bool inside = Mathf.Abs(ball.pos.x) > halfLen;
            ball.Step(DT, mode);
            float imp = KeepBallInNet();
            // depois do gol a bola morre lá dentro: o rebote da rede não a devolve ao campo
            if (inside && Mathf.Abs(ball.pos.x) < halfLen + ball.radius && Mathf.Sign(ball.pos.x) == side)
            {
                ball.pos.x = side * (halfLen + ball.radius);
                if (ball.vel.x * side < 0f) ball.vel.x *= -0.2f;
            }
            if (inside) { ball.vel *= 1f - 1.2f * DT; ball.spin *= 1f - 2f * DT; }   // atrito da rede/grama
            return imp;
        }

        /// <summary>
        /// A rede segura a bola: fundo inclinado (mais fundo embaixo), laterais e teto. O choque
        /// tira quase toda a velocidade (a rede cede e devolve só um pouco).
        /// </summary>
        float KeepBallInNet()
        {
            float impact = 0f;
            float depth = mode.goalDepth;
            if (Mathf.Abs(ball.pos.x) <= halfLen) return 0f;
            float sx = Mathf.Sign(ball.pos.x);
            float back = halfLen + Mathf.Lerp(depth, depth * 0.35f, Mathf.Clamp01(ball.z / Mathf.Max(0.1f, goalH))) - ball.radius;
            if (Mathf.Abs(ball.pos.x) > back)
            {
                float vin = ball.vel.x * sx;
                if (vin > 0f)
                {
                    impact = Mathf.Sqrt(vin * vin + ball.vz * ball.vz * 0.3f);
                    ball.vel.x = -sx * vin * 0.08f;
                    ball.vel.y *= 0.45f;
                    ball.vz *= 0.35f;
                    ball.spin *= 0.3f;
                }
                ball.pos.x = sx * back;
            }
            float side = goalHalf - ball.radius;
            if (Mathf.Abs(ball.pos.y) > side)
            {
                if (ball.vel.y * Mathf.Sign(ball.pos.y) > 0f) { impact = Mathf.Max(impact, Mathf.Abs(ball.vel.y) * 0.6f); ball.vel.y *= -0.15f; ball.vel.x *= 0.6f; }
                ball.pos.y = Mathf.Sign(ball.pos.y) * side;
            }
            if (ball.z > goalH - ball.radius)
            {
                ball.z = goalH - ball.radius;
                if (ball.vz > 0f) { impact = Mathf.Max(impact, ball.vz * 0.5f); ball.vz *= -0.1f; }
            }
            return impact;
        }
    }
}
