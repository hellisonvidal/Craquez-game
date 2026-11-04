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
        public void PlayersPerSide(string mode, int n)
        {
            var sim = NewMatch(mode);
            Assert.AreEqual(n * 2, sim.players.Count);
        }

        [TestCase(GameModes.Field)]
        [TestCase(GameModes.Futsal)]
        [TestCase(GameModes.Street)]
        public void FullMatchRunsToTheEnd(string mode)
        {
            var sim = NewMatch(mode);
            Play(sim, 60 * 400);
            Assert.AreEqual(MatchState.Fulltime, sim.state);
            Assert.IsFalse(float.IsNaN(sim.ball.pos.x));
            Assert.Greater(sim.statsShots[0] + sim.statsShots[1], 0);
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
            var perp = new Vector2(-p.facing.y, p.facing.x);
            sim.Step(new[] { new InputFrame { rstick = perp }, default });
            Assert.AreNotEqual(Skill.None, p.skill);
        }

        [Test] public void JsonRoundTrip()
        {
            var o = MiniJson.Parse("{\"a\": [1, 2.5, \"x\\n\"], \"b\": {\"c\": true, \"d\": null}}");
            var s = MiniJson.Write(o, false);
            Assert.AreEqual("{\"a\":[1,2.5,\"x\\n\"],\"b\":{\"c\":true,\"d\":null}}", s);
        }
    }
}
