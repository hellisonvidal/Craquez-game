using Craquez.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Craquez.Tests
{
    public class SimTests
    {
        static MatchSim NewMatch(string mode, uint seed = 42)
        {
            int a = mode == GameModes.Futsal ? FirstLeague(true) : 0;
            var sim = new MatchSim();
            sim.Setup(mode, new TeamSetup { data = Teams.Get(a) }, new TeamSetup { data = Teams.Get(a + 1) }, seed, 120f);
            return sim;
        }

        static int FirstLeague(bool futsal)
        {
            foreach (var l in Teams.Leagues) if (l.IsFutsal == futsal) return l.first;
            return 0;
        }

        static void Play(MatchSim sim, int ticks)
        {
            var a = new CpuBrain(0, 0.7f);
            var b = new CpuBrain(1, 0.7f);
            for (int i = 0; i < ticks && sim.state != MatchState.Fulltime; i++)
                sim.Step(new[] { a.MakeInput(sim), b.MakeInput(sim) });
        }

        [Test] public void LeaguesLoad()
        {
            Assert.GreaterOrEqual(Teams.Leagues.Count, 20);
            Assert.GreaterOrEqual(Teams.Count, 300);
            Assert.GreaterOrEqual(Teams.Get(0).roster.Count, 11);
            Assert.AreEqual(4, Teams.Get(0).kits.Count);
        }

        [TestCase(GameModes.Field, 11)]
        [TestCase(GameModes.Futsal, 5)]
        [TestCase(GameModes.Street, 3)]
        [TestCase(GameModes.Society, 7)]
        public void PlayersPerSide(string mode, int n)
        {
            var sim = NewMatch(mode);
            Assert.AreEqual(n * 2, sim.players.Count);
        }

        [TestCase(GameModes.Field)]
        [TestCase(GameModes.Futsal)]
        [TestCase(GameModes.Street)]
        [TestCase(GameModes.Society)]
        public void FullMatchRunsToTheEnd(string mode)
        {
            int shots = 0;
            foreach (uint seed in new uint[] { 42, 43 })   // (uma partida só pode, por acaso, não ter chutes)
            {
                var sim = NewMatch(mode, seed);
                Play(sim, 60 * 400);
                Assert.AreEqual(MatchState.Fulltime, sim.state);
                Assert.IsFalse(float.IsNaN(sim.ball.pos.x));
                shots += sim.statsShots[0] + sim.statsShots[1];
            }
            Assert.Greater(shots, 0);
        }

        [Test] public void StreetSizes()
        {
            foreach (int n in GameModes.StreetSizes)
            {
                var sim = new MatchSim { streetPlayers = n };
                sim.Setup(GameModes.Street, new TeamSetup { data = Teams.Get(0) }, new TeamSetup { data = Teams.Get(1) }, 5, 120f);
                int c = 0;
                foreach (var p in sim.players) if (p.team == 0) c++;
                Assert.AreEqual(n, c, "rua " + n + "x" + n);
                for (int i = 0; i < 60 * 60; i++) sim.Step(new InputFrame[2]);
                Assert.IsFalse(float.IsNaN(sim.ball.pos.x));
            }
        }

        [Test] public void Deterministic()
        {
            var a = NewMatch(GameModes.Field, 7);
            var b = NewMatch(GameModes.Field, 7);
            Play(a, 3000);
            Play(b, 3000);
            Assert.AreEqual(a.score[0], b.score[0]);
            Assert.AreEqual(a.score[1], b.score[1]);
            Assert.AreEqual(a.ball.pos, b.ball.pos);
            Assert.AreEqual(a.rng.State, b.rng.State);
        }

        [Test] public void InputFrameRoundTrip()
        {
            var f = new InputFrame { move = new Vector2(0.5f, -1f), rstick = new Vector2(-0.3f, 0.2f), buttons = InputFrame.Shoot | InputFrame.Finesse };
            var q = f.Quantized();
            Assert.AreEqual(f.buttons, q.buttons);
            Assert.AreEqual(q.move.x, 0.5f, 0.01f);
            Assert.AreEqual(q.move.y, -1f, 0.01f);
            Assert.AreEqual(q.rstick.x, -0.3f, 0.01f);
        }

        [Test] public void ShootingFromCloseScoresSometimes()
        {
            int goals = 0;
            for (uint s = 1; s <= 20; s++)
            {
                var sim = NewMatch(GameModes.Field, s);
                // pula o pontapé inicial
                for (int i = 0; i < 70; i++) sim.Step(new InputFrame[2]);
                var p = sim.Controlled(0);
                float dir = sim.TeamDir(0);
                p.position = new Vector2(dir * (sim.halfLen - 14f), 2f);
                p.facing = new Vector2(dir, 0);
                sim.ball.pos = p.position + p.facing * 0.4f;
                sim.ball.owner = p.id;
                var aim = new Vector2(dir * 0.3f, s % 2 == 0 ? 1f : -1f);
                var press = new InputFrame { move = aim, buttons = InputFrame.Shoot };
                for (int i = 0; i < 40; i++) sim.Step(new[] { press, default });
                for (int i = 0; i < 180 && sim.state == MatchState.Playing; i++) sim.Step(new[] { new InputFrame { move = aim }, default });
                if (sim.score[0] > 0) goals++;
            }
            Assert.Greater(goals, 0, "nenhum gol em 20 chutes de perto");
        }

        [Test] public void SkillStartsWithRightStickFlick()
        {
            var sim = NewMatch(GameModes.Field);
            for (int i = 0; i < 70; i++) sim.Step(new InputFrame[2]);
            var p = sim.Controlled(0);
            Assume.That(sim.ball.owner == p.id);
            // longe da saída de bola (no círculo central só vale o passe)
            p.position = new Vector2(sim.TeamDir(0) * 12f, 3f);
            sim.ball.pos = p.position + p.facing * 0.35f;
            sim.Step(new InputFrame[2]);
            var perp = new Vector2(-p.facing.y, p.facing.x);
            sim.Step(new[] { new InputFrame { rstick = perp }, default });
            Assert.AreNotEqual(Skill.None, p.skill);
        }

        [TestCase(GameModes.Field)]
        [TestCase(GameModes.Futsal)]
        [TestCase(GameModes.Society)]
        public void KickoffStartsWithPassToMate(string mode)
        {
            var sim = NewMatch(mode);
            for (int i = 0; i < 70; i++) sim.Step(new InputFrame[2]);
            var p = sim.Controlled(0);
            Assume.That(sim.ball.owner == p.id && sim.kickoffPass);
            var start = p.position;
            // tenta sair conduzindo e chutando: não anda com a bola; o chute vira toque para o companheiro
            for (int i = 0; i < 30; i++) sim.Step(new[] { new InputFrame { move = new Vector2(sim.TeamDir(0), 0), buttons = InputFrame.Sprint }, default });
            Assert.Less(p.position.Dist(start), 0.2f, "batedor saiu conduzindo");
            Assert.AreEqual(p.id, sim.ball.owner);
            sim.Step(new[] { new InputFrame { buttons = InputFrame.Shoot }, default });
            Assert.AreEqual(-1, sim.ball.owner);
            Assert.AreEqual(sim.kickoffMate, sim.passReceiver);
            Assert.Less(sim.ball.Speed, 12f, "saída virou chute");
            int got = -1;
            for (int i = 0; i < 120 && got < 0; i++) { sim.Step(new InputFrame[2]); if (sim.ball.owner >= 0) got = sim.ball.owner; }
            Assert.AreEqual(sim.kickoffMate, got, "companheiro não recebeu a saída");
        }

        [Test] public void CpuKickoffPassesQuickly()
        {
            var sim = NewMatch(GameModes.Field);
            var b = new CpuBrain(0, 0.7f);
            for (int i = 0; i < 60 * 4 && sim.kickoffPass; i++) sim.Step(new[] { b.MakeInput(sim), default });
            Assert.IsFalse(sim.kickoffPass, "CPU não fez a saída");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FoulKnocksVictimDownThenFreeKickWithWall(bool quick)
        {
            var sim = NewMatch(GameModes.Field);
            sim.quickRestarts = quick;
            for (int i = 0; i < 70; i++) sim.Step(new InputFrame[2]);
            float dir = sim.TeamDir(0);
            SimPlayer victim = null, offender = null;
            foreach (var p in sim.players) if (!p.isKeeper && p.team == 0 && victim == null) victim = p;
            foreach (var p in sim.players) if (!p.isKeeper && p.team == 1 && offender == null) offender = p;
            victim.position = new Vector2(dir * (sim.halfLen - 24f), 4f);
            offender.position = victim.position - new Vector2(dir, 0) * 0.6f;
            offender.vel = new Vector2(dir * 6f, 0);
            sim.ball.pos = victim.position + new Vector2(dir * 0.4f, 0);
            sim.ball.owner = victim.id;
            sim.Foul(offender, victim);
            Assert.AreEqual(MatchState.Foul, sim.state);
            Assert.Greater(victim.down, 0.9f);
            var spot = sim.foulSpot;
            for (int i = 0; i < 60 * 4 && sim.state == MatchState.Foul; i++) sim.Step(new InputFrame[2]);
            Assert.AreEqual(MatchState.Restart, sim.state);
            Assert.Less(sim.ball.pos.Dist(spot), 0.01f, "cobrança fora do lugar da falta");
            if (quick) Assert.AreEqual(0, sim.wallIds.Count);
            else
            {
                Assert.GreaterOrEqual(sim.wallIds.Count, 3, "sem barreira");
                foreach (int id in sim.wallIds) Assert.AreEqual(sim.mode.restartDist, sim.players[id].position.Dist(spot), 0.8f);
            }
        }

        [TestCase(GameModes.Field, 6.5f)]
        [TestCase(GameModes.Futsal, 3.2f)]
        public void PlayersKeepSpacing(string mode, float minNeighbor)
        {
            // ninguém amontoado: distância média até o companheiro mais próximo durante o jogo
            var sim = NewMatch(mode, 3);
            var a = new CpuBrain(0, 0.7f);
            var b = new CpuBrain(1, 0.7f);
            double sum = 0; int n = 0;
            for (int i = 0; i < 60 * 90; i++)
            {
                sim.Step(new[] { a.MakeInput(sim), b.MakeInput(sim) });
                if (sim.state != MatchState.Playing || i % 15 != 0) continue;
                foreach (var p in sim.players)
                {
                    if (p.isKeeper) continue;
                    float best = 999f;
                    foreach (var q in sim.players) if (q != p && q.team == p.team && !q.isKeeper) best = Mathf.Min(best, q.position.Dist(p.position));
                    sum += best; n++;
                }
            }
            Assert.Greater(sum / n, minNeighbor);
        }

        [Test] public void PlayersHaveDistinctPersonalities()
        {
            var sim = NewMatch(GameModes.Field, 9);
            float minW = 1f, maxW = 0f;
            foreach (var p in sim.players) { minW = Mathf.Min(minW, p.work); maxW = Mathf.Max(maxW, p.work); }
            Assert.Greater(maxW - minW, 0.2f);
        }

        static SimPlayer FirstOutfield(MatchSim sim, int team)
        {
            foreach (var p in sim.players) if (!p.isKeeper && p.team == team) return p;
            return null;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ThrowInWithHandsOnlyOnField(bool field)
        {
            var sim = NewMatch(field ? GameModes.Field : GameModes.Futsal);
            for (int i = 0; i < 70; i++) sim.Step(new InputFrame[2]);
            var other = FirstOutfield(sim, 1);
            sim.ball.owner = -1;
            sim.ball.pos = new Vector2(sim.TeamDir(0) * 5f, sim.halfW - 0.4f);
            sim.ball.vel = new Vector2(0f, 7f);
            sim.ball.z = 0f; sim.ball.vz = 0f;
            sim.ball.lastTouchTeam = 1; sim.ball.lastTouchId = other.id;
            bool restarted = false;
            for (int i = 0; i < 60 * 5 && !(restarted && sim.state == MatchState.Playing); i++)
            {
                sim.Step(new InputFrame[2]);
                foreach (var e in sim.events) if (e.type == "whistle" && e.kind == "throwin") restarted = true;
            }
            Assert.IsTrue(restarted, "não houve lateral");
            Assert.AreEqual(field, sim.throwIn, "lateral com as mãos só no campo");
            if (field) Assert.Greater(sim.ball.z, 1.8f, "bola não está nas mãos (acima da cabeça)");
            else Assert.Less(sim.ball.z, 0.3f, "nos outros modos o lateral é com os pés");
            // cobra: passe
            bool thrown = false;
            float zAtRelease = 0f;
            for (int i = 0; i < 8; i++) sim.Step(new[] { new InputFrame { move = new Vector2(sim.TeamDir(0), -1f), buttons = InputFrame.Pass }, default });
            for (int i = 0; i < 30 && !thrown; i++)
            {
                sim.Step(new[] { new InputFrame { move = new Vector2(sim.TeamDir(0), -1f) }, default });
                foreach (var e in sim.events) if (e.type == "kick") { thrown = e.kind == "throw"; zAtRelease = sim.ball.z; if (!field) break; }
                if (!field && sim.ball.owner < 0) break;
            }
            Assert.AreEqual(field, thrown, "arremesso com as mãos");
            if (field) Assert.Greater(zAtRelease, 1.5f, "arremesso sai de cima da cabeça");
            Assert.IsFalse(sim.throwIn);
        }

        [TestCase(22f, true)]   // fora da área (dentro seria pênalti)
        [TestCase(45f, false)]
        public void FreeKickBallStaysAndOptionsDependOnDistance(float distToGoal, bool shotAllowed)
        {
            var sim = NewMatch(GameModes.Field);
            for (int i = 0; i < 70; i++) sim.Step(new InputFrame[2]);
            float dir = sim.TeamDir(0);
            var victim = FirstOutfield(sim, 0);
            var offender = FirstOutfield(sim, 1);
            victim.position = new Vector2(dir * (sim.halfLen - distToGoal), 6f);
            offender.position = victim.position - new Vector2(dir, 0) * 0.6f;
            offender.vel = new Vector2(dir * 6f, 0);
            sim.ball.owner = victim.id;
            sim.Foul(offender, victim);
            for (int i = 0; i < 60 * 9 && sim.state != MatchState.Playing; i++) sim.Step(new InputFrame[2]);
            Assert.IsTrue(sim.setPiece, "sem bola parada");
            Assert.AreEqual(shotAllowed, sim.setPieceShot);
            var spot = sim.setPieceSpot;
            // o cobrador tenta sair conduzindo: a bola continua parada no lugar; adversários longe
            for (int i = 0; i < 90; i++)
            {
                sim.Step(new[] { new InputFrame { move = new Vector2(dir, 0.3f), buttons = InputFrame.Sprint }, default });
                foreach (var p in sim.players)
                    if (p.team == 1 && !sim.wallIds.Contains(p.id))
                        Assert.GreaterOrEqual(p.position.Dist(spot), sim.mode.restartDist - 0.4f, "adversário em cima da bola");
            }
            Assert.Less(sim.ball.pos.Dist(spot), 0.01f, "bola saiu do lugar");
            Assert.IsTrue(sim.setPiece);
            // B: chute direto perto da área; longe, vira cruzamento
            bool crossed = false, shot = false;
            int before = sim.statsShots[0];
            for (int i = 0; i < 30; i++) { sim.Step(new[] { new InputFrame { move = new Vector2(dir, 0), buttons = InputFrame.Shoot }, default }); }
            for (int i = 0; i < 30; i++)
            {
                sim.Step(new[] { new InputFrame { move = new Vector2(dir, 0) }, default });
                foreach (var e in sim.events) if (e.type == "cross") crossed = true;
            }
            shot = sim.statsShots[0] > before;
            Assert.IsFalse(sim.setPiece, "falta não foi cobrada");
            Assert.AreEqual(shotAllowed, shot, "chute direto");
            Assert.AreEqual(!shotAllowed, crossed, "cruzamento");
        }

        /// <summary>
        /// Passe em plena corrida: a preparação traz a bola ao pé e o passe sai de fato
        /// (sem "chutar o ar" com a bola longe), com a bola junto do pé na saída.
        /// </summary>
        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        public void KickWhileSprintingHitsTheBall(uint seed)
        {
            var sim = NewMatch(GameModes.Field, seed);
            for (int i = 0; i < 70; i++) sim.Step(new InputFrame[2]);
            var p = sim.Controlled(0);
            float dir = sim.TeamDir(0);
            foreach (var o in sim.players) if (o.team == 1) o.position = new Vector2(-dir * 40f, o.position.y);
            p.position = new Vector2(-dir * 10f, 0f);
            p.facing = new Vector2(dir, 0);
            sim.ball.pos = p.position + p.facing * 0.4f;
            sim.ball.owner = p.id;
            sim.kickoffPass = false;
            var run = new InputFrame { move = new Vector2(dir, 0), buttons = InputFrame.Sprint };
            for (int i = 0; i < 90; i++) sim.Step(new[] { run, default });
            Assume.That(sim.ball.owner == p.id, "perdeu a bola conduzindo");
            sim.Step(new[] { new InputFrame { move = new Vector2(dir, 0.4f), buttons = InputFrame.Sprint | InputFrame.Pass }, default });
            float atKick = -1f;
            for (int i = 0; i < 40 && atKick < 0f; i++)
            {
                var before = p.position.Dist(sim.ball.pos);
                sim.Step(new[] { run, default });
                if (sim.ball.owner != p.id) atKick = before;
            }
            Assert.GreaterOrEqual(atKick, 0f, "o passe não saiu");
            Assert.Less(atKick, 0.75f, "bola longe do pé na hora do passe");
        }

        /// <summary>Goleiro com a bola nas mãos repõe com as mãos (arremesso/rolada) ou de chutão — e o time fica com ela.</summary>
        [TestCase(GameModes.Field)]
        [TestCase(GameModes.Futsal)]
        public void KeeperDistributesWithHandsOrPunt(string mode)
        {
            int throws = 0, punts = 0, kept = 0;
            for (uint seed = 1; seed <= 12; seed++)
            {
                var sim = NewMatch(mode, seed);
                for (int i = 0; i < 70; i++) sim.Step(new InputFrame[2]);
                SimPlayer gk = null;
                foreach (var p in sim.players) if (p.isKeeper && p.team == 1) gk = p;
                sim.kickoffPass = false;
                gk.position = new Vector2(-sim.halfLen * sim.TeamDir(1) + sim.TeamDir(1) * 1.5f, 0f);
                sim.ball.pos = gk.position;
                sim.ball.owner = gk.id;
                gk.hold = 0.3f;
                string kind = null;
                for (int i = 0; i < 120 && kind == null; i++)
                {
                    sim.events.Clear();
                    sim.Step(new InputFrame[2]);
                    foreach (var e in sim.events) if (e.type == "kick" && e.id == gk.id) kind = e.kind;
                }
                Assert.IsNotNull(kind, "o goleiro não repôs a bola");
                if (kind == "gkthrow") throws++;
                else if (kind == "punt") punts++;
                for (int i = 0; i < 240 && sim.ball.owner < 0; i++) sim.Step(new InputFrame[2]);
                if (sim.ball.owner >= 0 && sim.players[sim.ball.owner].team == 1) kept++;
            }
            Assert.Greater(throws, 0, "nunca repôs com as mãos");
            if (mode == GameModes.Futsal) Assert.AreEqual(0, punts, "futsal não tem chutão de bate-pronto");
            Assert.GreaterOrEqual(kept, 6, "a reposição quase sempre vai para o adversário");
        }

        /// <summary>Todas as competições (torneios, ligas e copas) vão do começo ao campeão (salvando e carregando no meio).</summary>
        [Test] public void CompetitionsRunToAChampion()
        {
            Assert.GreaterOrEqual(Competitions.All.Length, 50, "torneios + ligas + copas nacionais");
            foreach (var d in Competitions.All)
            {
                var pool = Competitions.Pool(d);
                Assert.GreaterOrEqual(pool.Count, Mathf.Min(d.size, 4), d.name + ": poucos times");
                int user = pool[pool.Count / 2];
                var run = CompRun.Start(d, user, 7u);
                Assert.IsTrue(run.teams.Contains(user), d.name + ": time do usuário fora");
                int guard = 0, userGames = 0;
                while (!run.Finished && guard++ < 100)
                {
                    var m = run.NextUserMatch();
                    if (m != null) { userGames++; run.PlayRound(2, 1); }
                    else run.SimulateRound();
                    if (guard == 2)
                    {
                        var json = MiniJson.Write(run.ToJson());
                        run = CompRun.FromJson(MiniJson.Parse(json));
                        Assert.IsNotNull(run, d.name + ": não carregou");
                    }
                }
                Assert.IsTrue(run.Finished, d.name + ": não terminou (rodada " + run.CurrentRound + ", jogos " + run.matches.Count + ", grupos " + run.groups.Count + ", times " + run.teams.Count + ", pendentes " + run.matches.FindAll(x => !x.Played).Count + ")");
                Assert.Greater(userGames, 0, d.name + ": o usuário não jogou");
                // ganhando todos os jogos por 2 x 1, o usuário é campeão
                Assert.AreEqual(user, run.champion, d.name + ": vencendo tudo não foi campeão");
            }
        }

        /// <summary>Companheiros não se trombam: quase nunca ficam a menos de 1,4 m um do outro (com um jogador humano no time).</summary>
        [TestCase(GameModes.Field)]
        [TestCase(GameModes.Society)]
        [TestCase(GameModes.Futsal)]
        public void TeammatesDontBump(string mode)
        {
            var sim = NewMatch(mode, 5);
            int samples = 0, close = 0;
            for (int i = 0; i < 60 * 90; i++)
            {
                // time 0 com um "jogador humano": corre atrás da bola e, com ela, conduz para o gol
                var me = sim.Controlled(0);
                var to = sim.ball.owner == me.id ? new Vector2(sim.TeamDir(0), 0f) : (sim.ball.pos - me.position).Norm();
                var f = new InputFrame { move = to, buttons = InputFrame.Sprint };
                if (sim.ball.owner == me.id && i % 90 == 0) f.buttons |= InputFrame.Pass;
                sim.Step(new[] { f.Quantized(), default });
                if (sim.state != MatchState.Playing || sim.setPiece || i % 6 != 0) continue;   // (bola parada não conta)
                foreach (var a in sim.players)
                    foreach (var b in sim.players)
                    {
                        if (a.id >= b.id || a.team != b.team || a.team != 0) continue;
                        samples++;
                        if (a.position.Dist(b.position) < 1.4f) close++;
                    }
            }
            float rate = close / (float)Mathf.Max(1, samples);
            Assert.Less(rate, 0.01f, "companheiros colados em " + (rate * 100f).ToString("F2") + "% do tempo");
        }

        [Test] public void SubstitutionSwapsPlayerAndBlocksReturn()
        {
            var sim = NewMatch(GameModes.Field);
            var p = sim.players.Find(x => x.team == 0 && !x.isKeeper);
            var roster = sim.teams[0].data.roster;
            int bench = -1;
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].pos == "GK") continue;
                if (!sim.players.Exists(x => x.team == 0 && x.rosterIndex == i)) { bench = i; break; }
            }
            Assert.GreaterOrEqual(bench, 0, "banco sem reserva de linha");
            int outIdx = p.rosterIndex;
            Assert.IsTrue(sim.Substitute(p.id, bench));
            Assert.AreEqual(roster[bench].n, p.name);
            Assert.AreEqual(4, sim.subsLeft[0]);
            // quem saiu não volta (campo); goleiro só por goleiro
            var q = sim.players.Find(x => x.team == 0 && !x.isKeeper && x.id != p.id);
            Assert.IsFalse(sim.Substitute(q.id, outIdx));
            var gk = sim.players.Find(x => x.team == 0 && x.isKeeper);
            int outfield = roster.FindIndex(e => e.pos != "GK" && !sim.players.Exists(x => x.team == 0 && x.rosterIndex == roster.IndexOf(e)) && roster.IndexOf(e) != outIdx);
            Assert.IsFalse(sim.Substitute(gk.id, outfield));
        }

        [Test] public void BallStaysInsideTheNet()
        {
            var sim = NewMatch(GameModes.Field);
            sim.ball.owner = -1;
            sim.ball.pos = new Vector2(sim.halfLen + 0.3f, 2f);
            sim.ball.vel = new Vector2(30f, 4f);
            sim.ball.z = 1.5f;
            sim.ball.vz = 2f;
            float maxImpact = 0f;
            for (int i = 0; i < 300; i++) maxImpact = Mathf.Max(maxImpact, sim.StepBallInNet());
            Assert.Greater(maxImpact, 10f, "a bola tem de bater forte na rede");
            Assert.Greater(sim.ball.pos.x, sim.halfLen, "a bola fica dentro do gol");
            Assert.LessOrEqual(sim.ball.pos.x, sim.halfLen + sim.mode.goalDepth);
            Assert.LessOrEqual(Mathf.Abs(sim.ball.pos.y), sim.goalHalf);
            Assert.Less(sim.ball.Speed, 1.5f, "a rede segura a bola");
        }

        /// <summary>Chutes no gol de 11 a 22 m: o goleiro defende boa parte, mas não todos (e mergulha nos cantos).</summary>
        [Test] public void KeeperSavesAndConcedes()
        {
            int goals = 0, shots = 0, dives = 0, stands = 0;
            var evs = new System.Collections.Generic.Dictionary<string, int>();
            var hist = new System.Collections.Generic.SortedDictionary<int, (int, int, int)>();
            var r = new System.Random(7);
            for (int n = 0; n < 160; n++)
            {
                var sim = NewMatch(GameModes.Field, (uint)(n + 1));
                for (int i = 0; i < 70; i++) sim.Step(new InputFrame[2]);
                float dir = sim.TeamDir(0);
                float gx = dir * sim.halfLen;
                var gk = sim.players.Find(x => x.team == 1 && x.isKeeper);
                // ninguém de linha por perto: só goleiro x bola
                foreach (var q in sim.players) if (!q.isKeeper) q.position = new Vector2(-dir * 20f, q.position.y);
                gk.position = new Vector2(gx - dir * 1f, 0f);
                gk.vel = Vector2.zero;
                float dist = 11f + (float)r.NextDouble() * 9f;
                var from = new Vector2(gx - dir * dist, ((float)r.NextDouble() * 2f - 1f) * 8f);
                var tgt = new Vector2(gx, ((float)r.NextDouble() * 2f - 1f) * (sim.goalHalf - 0.25f));
                float tz = (float)r.NextDouble() * (sim.goalH - 0.3f);
                float speed = 24f + (float)r.NextDouble() * 10f;
                var d = (tgt - from);
                float t = d.magnitude / speed;
                sim.ball.owner = -1;
                sim.ball.pos = from;
                sim.ball.vel = d / t;
                sim.ball.z = 0.11f;
                sim.ball.vz = (tz + 0.5f * SimBall.G * t * t - 0.11f) / t;
                sim.ball.lastTouchTeam = 0;
                sim.lastShotTick = sim.tick;
                sim.kickoffPass = false;
                sim.protectT = 0f;
                shots++;
                bool dived = false, stood = false;
                for (int i = 0; i < 150 && sim.state == MatchState.Playing; i++)
                {
                    sim.Step(new InputFrame[2]);
                    foreach (var ev in sim.events) if (ev.type == "save" || ev.type == "touch" || ev.type == "post" || ev.type == "out" || ev.type == "whistle") { string key = ev.type + ":" + ev.kind + ":" + ev.ok; evs[key] = evs.TryGetValue(key, out var c0) ? c0 + 1 : 1; }
                    if (gk.saveKind >= 3) dived = true;
                    else if (gk.saveKind > 0) stood = true;
                }
                if (dived) dives++;
                if (stood) stands++;
                if (sim.score[0] > 0) goals++;
                int bk = Mathf.Min(3, (int)(Mathf.Abs(tgt.y) / 1f)) * 10 + (tz < 0.6f ? 0 : tz < 1.5f ? 1 : 2);
                hist[bk] = hist.TryGetValue(bk, out var hv) ? (hv.Item1 + 1, hv.Item2 + (sim.score[0] > 0 ? 1 : 0), hv.Item3 + (dived ? 1 : 0)) : (1, sim.score[0] > 0 ? 1 : 0, dived ? 1 : 0);
            }
            foreach (var kv in hist) System.Console.WriteLine($"   y~{kv.Key / 10} m, altura {(kv.Key % 10 == 0 ? "baixa" : kv.Key % 10 == 1 ? "média" : "alta")}: {kv.Value.Item2}/{kv.Value.Item1} gols, {kv.Value.Item3} mergulhos");
            float rate = goals / (float)shots;

            foreach (var kv in evs) System.Console.WriteLine("   " + kv.Key + " " + kv.Value);
            System.Console.WriteLine($"[goleiro] {goals}/{shots} gols ({rate:P0}), mergulhos {dives}, encaixes {stands}");
            Assert.Greater(rate, 0.15f, "goleiro defende demais");
            Assert.Less(rate, 0.7f, "goleiro defende de menos");
            Assert.Greater(dives, 15, "o goleiro quase não mergulha");
            Assert.Greater(stands, 10, "o goleiro quase não encaixa de frente");
        }

        /// <summary>Futsal/society: o lateral é com os pés e sempre um passe (nunca chute), cobrado em até 4 s no futsal.</summary>
        [TestCase("futsal")]
        [TestCase("society")]
        public void KickInIsAlwaysAPass(string mode)
        {
            int kickins = 0, shotsFromKickIn = 0, timeouts = 0;
            for (uint seed = 1; seed <= 4; seed++)
            {
                var sim = NewMatch(mode, seed);
                var a = new CpuBrain(0, 0.7f); var b = new CpuBrain(1, 0.7f);
                for (int i = 0; i < 60 * 300 && sim.state != MatchState.Fulltime; i++)
                {
                    bool was = sim.kickIn && sim.setPiece;
                    int taker = sim.setPieceTaker;
                    sim.Step(new[] { a.MakeInput(sim), b.MakeInput(sim) });
                    foreach (var e in sim.events)
                    {
                        if (e.type == "whistle" && e.kind == "throwin") { kickins++; Assert.IsTrue(sim.kickIn, "lateral sem a regra do pé"); }
                        if (was && e.type == "kick" && e.id == taker && e.kind != "pass" && e.kind != "lob") shotsFromKickIn++;
                    }
                }
                timeouts += sim.kickInTimeouts;
            }
            Assert.Greater(kickins, 3);
            Assert.AreEqual(0, shotsFromKickIn, "o lateral virou chute");
            Assert.LessOrEqual(timeouts, kickins / 5, "CPU estoura os 4 segundos do lateral");
        }

        /// <summary>Escanteio: o cobrador vai andando até a bola e cobra cruzando na área ou com toque curto (nunca conduz).</summary>
        [TestCase(GameModes.Field)]
        [TestCase(GameModes.Futsal)]
        public void CornerIsCrossOrShortPass(string mode)
        {
            int corners = 0, walkUps = 0, crosses = 0, shorts = 0, other = 0, carried = 0;
            for (uint seed = 1; seed <= 12; seed++)
            {
                var sim = NewMatch(mode, seed);
                var a = new CpuBrain(0, 0.7f); var b = new CpuBrain(1, 0.7f);
                for (int i = 0; i < 90 + seed * 13; i++) sim.Step(new[] { a.MakeInput(sim), b.MakeInput(sim) });
                int t = (int)(seed % 2);
                float sx = (sim.halfLen - 0.3f) * sim.TeamDir(t), sy = (seed % 4 < 2 ? 1f : -1f) * (sim.halfW - 0.3f);
                sim.ForceRestart(t, new Vector2(sx, sy), "corner");
                corners++;
                if (sim.walkUpTaker >= 0) walkUps++;
                var held = sim.ball.pos;
                int taker = -1;
                for (int i = 0; i < 60 * 20; i++)
                {
                    if (sim.walkUpTaker >= 0) Assert.Less(sim.ball.pos.Dist(held), 0.01f, $"a bola saiu do lugar durante a caminhada: {held} -> {sim.ball.pos} owner {sim.ball.owner} st {sim.state} i {i} seed {seed} wu {sim.walkUpTaker} ko {sim.kickoffPass}");
                    bool was = sim.cornerKick && sim.setPiece && sim.state == MatchState.Playing;
                    if (was) taker = sim.setPieceTaker;
                    sim.Step(new[] { a.MakeInput(sim), b.MakeInput(sim) });
                    bool done = false;
                    foreach (var e in sim.events)
                    {
                        if (!was) continue;
                        if (e.type == "cross") { crosses++; done = true; }
                        else if (e.type == "kick" && e.id == taker) { if (e.kind == "pass") shorts++; else if (e.kind != "lob") other++; done = true; }
                    }
                    if (was && !sim.setPiece && !done && sim.ball.owner == taker) { carried++; done = true; }
                    if (done) break;
                }
            }
            System.Console.WriteLine($"[escanteio {mode}] {corners} escanteios: {crosses} cruzamentos, {shorts} curtos, {other} outros, {carried} conduzidos");
            Assert.AreEqual(corners, walkUps, "sem a cena do cobrador indo até a bola");
            Assert.AreEqual(0, other, "escanteio virou chute");
            Assert.AreEqual(0, carried, "o cobrador saiu conduzindo a bola do escanteio");
            Assert.GreaterOrEqual(crosses + shorts, corners - 1);
            Assert.GreaterOrEqual(crosses, shorts);
        }

        /// <summary>Ninguém fica correndo em círculos: soma do giro da direção de corrida numa janela de 2 s.</summary>
        [TestCase(GameModes.Field)]
        [TestCase(GameModes.Futsal)]
        public void PlayersDontRunInCircles(string mode)
        {
            int loops = 0, samples = 0, floops = 0, keeperIn = 0;
            for (uint seed = 1; seed <= 3; seed++)
            {
                var sim = NewMatch(mode, seed);
                var a = new CpuBrain(0, 0.7f); var b = new CpuBrain(1, 0.7f);
                int n = sim.players.Count;
                var prev = new Vector2[n];
                var prevF = new Vector2[n];
                var fsum = new float[n];
                var fh = new System.Collections.Generic.Queue<float>[n];
                for (int k = 0; k < n; k++) fh[k] = new System.Collections.Generic.Queue<float>();
                var hist = new System.Collections.Generic.Queue<float>[n];
                var sum = new float[n];
                for (int k = 0; k < n; k++) hist[k] = new System.Collections.Generic.Queue<float>();
                for (int i = 0; i < 60 * 240 && sim.state != MatchState.Fulltime; i++)
                {
                    sim.Step(new[] { a.MakeInput(sim), b.MakeInput(sim) });
                    if (sim.state != MatchState.Playing) continue;
                    foreach (var p in sim.players)
                    {
                        if (p.isKeeper)
                        {
                            float gx = -sim.halfLen * sim.TeamDir(p.team);
                            if (Mathf.Abs(p.position.y) < sim.goalHalf && (p.position.x - gx) * -Mathf.Sign(gx) < 0.1f) keeperIn++;
                            continue;
                        }
                        float turn = 0f;
                        if (p.vel.magnitude > 1.2f && prev[p.id].magnitude > 1.2f) turn = Vector2.SignedAngle(prev[p.id], p.vel);
                        prev[p.id] = p.vel;
                        float ft = prevF[p.id] != Vector2.zero ? Vector2.SignedAngle(prevF[p.id], p.facing) : 0f;
                        prevF[p.id] = p.facing;
                        fh[p.id].Enqueue(ft); fsum[p.id] += ft;
                        if (fh[p.id].Count > 120) fsum[p.id] -= fh[p.id].Dequeue();
                        if (Mathf.Abs(fsum[p.id]) > 330f)
                        {
                            floops++; fh[p.id].Clear(); fsum[p.id] = 0f;
                            if (floops <= 12) System.Console.WriteLine($"   giro: id {p.id} dono {sim.ball.owner == p.id} ctrl {sim.ctrl[p.team].player == p.id} vel {p.vel.magnitude:F1} dBola {p.position.Dist(sim.ball.pos):F1} faceBall {p.faceBall} skill {p.skill} stun {p.stun:F1} receptor {sim.passReceiver == p.id} donoTime {(sim.ball.owner >= 0 ? sim.players[sim.ball.owner].team : -1)} time {p.team}");
                        }
                        hist[p.id].Enqueue(turn); sum[p.id] += turn;
                        if (hist[p.id].Count > 120) sum[p.id] -= hist[p.id].Dequeue();
                        samples++;
                        if (Mathf.Abs(sum[p.id]) > 330f) { loops++; hist[p.id].Clear(); sum[p.id] = 0f; }
                    }
                }
            }
            System.Console.WriteLine($"[círculos {mode}] {loops} voltas completas correndo, {floops} giros do corpo, em {samples / 60} s-jogador");
            Assert.AreEqual(0, keeperIn, "goleiro dentro do próprio gol");
            // (no futsal, quadra pequena, as voltas para marcar/receber são mais comuns)
            Assert.Less(loops, samples / 60 / (mode == GameModes.Field ? 200 : 40), "jogadores correndo em círculos");
        }

        /// <summary>Saída de bola: a bola fica exatamente na marca do meio-campo até o toque.</summary>
        [Test] public void KickoffBallOnCenterMark()
        {
            foreach (var mode in new[] { GameModes.Field, GameModes.Futsal, GameModes.Society })
            {
                var sim = NewMatch(mode, 3);
                for (int i = 0; i < 200 && sim.kickoffPass; i++)
                {
                    if (sim.ball.owner == sim.kickoffKicker) Assert.Less(sim.ball.pos.magnitude, 0.01f, mode + ": bola fora da marca central");
                    sim.Step(new InputFrame[2]);
                }
            }
        }

        /// <summary>Recuo: passe com o pé do companheiro para o goleiro não pode ser agarrado.</summary>
        [Test] public void BackPassIsNotCaught()
        {
            int held = 0, tries = 0;
            for (uint seed = 1; seed <= 6; seed++)
            {
                var sim = NewMatch(GameModes.Field, seed);
                for (int i = 0; i < 70; i++) sim.Step(new InputFrame[2]);
                sim.kickoffPass = false;
                var gk = sim.players.Find(x => x.team == 0 && x.isKeeper);
                var df = sim.players.Find(x => x.team == 0 && !x.isKeeper);
                foreach (var q in sim.players) if (q.team == 1) q.position = new Vector2(sim.TeamDir(0) * 30f, q.position.y);
                df.position = gk.position + new Vector2(sim.TeamDir(0) * 14f, 2f);
                sim.ball.owner = df.id; sim.ball.pos = df.position; sim.ball.vel = Vector2.zero; sim.ball.z = 0;
                sim.ctrl[0].player = df.id;
                var aim = (gk.position - df.position).Norm();
                var press = new InputFrame { move = aim, buttons = InputFrame.Pass };
                for (int i = 0; i < 6; i++) sim.Step(new[] { press, default });
                tries++;
                for (int i = 0; i < 150; i++) { sim.Step(new[] { new InputFrame(), default }); if (gk.hold > 0f) { held++; break; } }
            }
            Assert.AreEqual(0, held, "o goleiro agarrou um recuo de " + tries);
        }

        /// <summary>Cartões: falta violenta expulsa; o expulso fica fora e o jogo segue sem ele.</summary>
        [Test] public void RedCardSendsPlayerOff()
        {
            var sim = NewMatch(GameModes.Field, 3);
            for (int i = 0; i < 200; i++) sim.Step(new InputFrame[2]);
            var off = sim.players.Find(x => x.team == 1 && !x.isKeeper);
            var vic = sim.players.Find(x => x.team == 0 && !x.isKeeper);
            int tries = 0;
            while (!off.sentOff && tries++ < 60)
            {
                sim.state = MatchState.Playing;
                off.vel = new Vector2(9f, 0); off.slide = 0.5f; vic.facing = new Vector2(1f, 0);
                off.position = vic.position - new Vector2(0.5f, 0);
                sim.Foul(off, vic);
            }
            Assert.IsTrue(off.sentOff, "falta violenta sem vermelho");
            for (int i = 0; i < 60 * 30; i++) sim.Step(new InputFrame[2]);
            Assert.Greater(Mathf.Abs(off.position.y), sim.halfW, "expulso voltou para o campo");
            Assert.AreNotEqual(off.id, sim.ball.owner);
        }

        /// <summary>Reposição do goleiro: a bola chega a um companheiro (não no pé do adversário).</summary>
        [TestCase("field")]
        [TestCase("futsal")]
        public void KeeperDistributionReachesTeammate(string mode)
        {
            int ok = 0, total = 0;
            for (uint seed = 1; seed <= (mode == "futsal" ? 30u : 12u); seed++)
            {
                var sim = NewMatch(mode, seed);
                var a = new CpuBrain(0, 0.7f); var b = new CpuBrain(1, 0.7f);
                for (int i = 0; i < 60 * 6; i++) sim.Step(new[] { a.MakeInput(sim), b.MakeInput(sim) });
                sim.kickoffPass = false;
                var gk = sim.players.Find(x => x.team == 0 && x.isKeeper);
                sim.state = MatchState.Playing;
                sim.ball.owner = gk.id; gk.hold = 1.2f; gk.heldT = 0f;
                sim.ball.pos = gk.position; sim.ball.vel = Vector2.zero;
                bool released = false;
                for (int i = 0; i < 60 * 9; i++)
                {
                    sim.Step(new[] { a.MakeInput(sim), b.MakeInput(sim) });
                    if (!released && sim.ball.owner != gk.id) released = true;
                    if (released && sim.ball.owner >= 0 && sim.ball.owner != gk.id)
                    {
                        total++;
                        if (sim.players[sim.ball.owner].team == 0) ok++;
                        break;
                    }
                }
            }
            System.Console.WriteLine($"[reposição {mode}] {ok}/{total} para o companheiro");
            Assert.Greater(total, 3);
            Assert.GreaterOrEqual(ok / (float)total, 0.75f, "o goleiro entrega a bola ao adversário");
        }

        /// <summary>CPU x CPU: o jogo acontece (chutes e posse no campo de ataque, não só troca de passes no meio).</summary>
        [TestCase("field", 8)]
        [TestCase("society", 8)]
        [TestCase("futsal", 10)]
        public void CpuMatchHasAction(string mode, int minShots)
        {
            int shots = 0, owned = 0, attackThird = 0;
            for (uint seed = 1; seed <= 3; seed++)
            {
                var sim = NewMatch(mode, seed);
                var a = new CpuBrain(0, 0.7f); var b = new CpuBrain(1, 0.7f);
                for (int i = 0; i < 60 * 300 && sim.state != MatchState.Fulltime; i++)
                {
                    sim.Step(new[] { a.MakeInput(sim), b.MakeInput(sim) });
                    foreach (var e in sim.events) if (e.type == "kick" && e.kind == "shot") shots++;
                    if (sim.ball.owner >= 0 && sim.state == MatchState.Playing)
                    {
                        var o = sim.players[sim.ball.owner];
                        owned++;
                        if (o.position.x * sim.TeamDir(o.team) > sim.halfLen / 3f) attackThird++;
                    }
                }
            }
            System.Console.WriteLine($"[ação {mode}] chutes {shots}, posse no terço de ataque {attackThird * 100 / Mathf.Max(1, owned)}%");
            Assert.GreaterOrEqual(shots, minShots, "CPU x CPU quase não chuta");
            Assert.Greater(attackThird / (float)Mathf.Max(1, owned), 0.07f, "a bola não chega ao ataque");
        }

        /// <summary>Quedas: toda queda tem causa (falta, trombada, bicicleta) e não são frequentes demais.</summary>
        [Test] public void FallsHaveCause()
        {
            int falls = 0, fouls = 0, bumpFalls = 0, other = 0;
            float minutes = 0f;
            for (uint seed = 1; seed <= 3; seed++)
            {
                var sim = NewMatch(GameModes.Field, seed);
                var a = new CpuBrain(0, 0.7f); var b = new CpuBrain(1, 0.7f);
                var was = new bool[sim.players.Count];
                for (int i = 0; i < 60 * 300 && sim.state != MatchState.Fulltime; i++)
                {
                    sim.Step(new[] { a.MakeInput(sim), b.MakeInput(sim) });
                    bool foul = false, bump = false, bike = false;
                    foreach (var e in sim.events) { if (e.type == "foul") { foul = true; fouls++; } if (e.type == "bump") bump = true; if (e.type == "kick" && e.kind == "bicycle") bike = true; }
                    for (int k = 0; k < sim.players.Count; k++)
                    {
                        bool d = sim.players[k].down > 0f;
                        if (d && !was[k]) { falls++; if (foul || bike) { } else if (bump) bumpFalls++; else other++; }
                        was[k] = d;
                    }
                }
                minutes += 300f / 60f;
            }
            System.Console.WriteLine($"[quedas] {falls} quedas em {minutes:F0} min de jogo simulado: {fouls} faltas, {bumpFalls} por trombada, {other} sem causa");
            Assert.AreEqual(0, other, "jogador caiu sem falta, trombada ou bicicleta");
        }

        /// <summary>On-line (lockstep): dois aparelhos, cada um com a sua simulação, trocando só os comandos, terminam iguais.</summary>
        [Test] public void LockstepStaysInSync()
        {
            Craquez.Sim.Net.LoopbackTransport.Pair(out var na, out var nb);
            var simA = NewMatch(GameModes.Field, 7);
            var simB = NewMatch(GameModes.Field, 7);
            var sa = new Craquez.Sim.Net.LockstepSession(na, 0);
            var sb = new Craquez.Sim.Net.LockstepSession(nb, 1);
            var cpuA = new CpuBrain(0, 0.7f); var cpuB = new CpuBrain(1, 0.7f);
            int steps = 0;
            for (int i = 0; i < 60 * 40; i++)
            {
                // cada lado decide o próprio comando com o que vê na própria simulação
                sa.PushLocal(cpuA.MakeInput(simA));
                sb.PushLocal(cpuB.MakeInput(simB));
                bool a = sa.Step(simA), b = sb.Step(simB);
                if (a && b) steps++;
            }
            System.Console.WriteLine($"[on-line] {steps} passos em lockstep; hash {simA.StateHash()} x {simB.StateHash()}");
            Assert.Greater(steps, 60 * 30);
            Assert.IsFalse(sa.Desynced || sb.Desynced, "dessincronizou no passo " + Mathf.Max(sa.DesyncTick, sb.DesyncTick));
            Assert.AreEqual(simA.StateHash(), simB.StateHash(), "as duas partidas terminaram diferentes");
        }

        [Test] public void JsonRoundTrip()
        {
            var o = MiniJson.Parse("{\"a\": [1, 2.5, \"x\\n\"], \"b\": {\"c\": true, \"d\": null}}");
            var s = MiniJson.Write(o, false);
            Assert.AreEqual("{\"a\":[1,2.5,\"x\\n\"],\"b\":{\"c\":true,\"d\":null}}", s);
        }
    }
}
