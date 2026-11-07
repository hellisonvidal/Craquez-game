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
            Assert.AreEqual(3, Teams.Get(0).kits.Count);
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
            var sim = NewMatch(mode);
            Play(sim, 60 * 400);
            Assert.AreEqual(MatchState.Fulltime, sim.state);
            Assert.IsFalse(float.IsNaN(sim.ball.pos.x));
            Assert.Greater(sim.statsShots[0] + sim.statsShots[1], 0);
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
            for (int i = 0; i < 60 * 5 && sim.state != MatchState.Playing; i++) sim.Step(new InputFrame[2]);
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

        [Test] public void JsonRoundTrip()
        {
            var o = MiniJson.Parse("{\"a\": [1, 2.5, \"x\\n\"], \"b\": {\"c\": true, \"d\": null}}");
            var s = MiniJson.Write(o, false);
            Assert.AreEqual("{\"a\":[1,2.5,\"x\\n\"],\"b\":{\"c\":true,\"d\":null}}", s);
        }
    }
}
