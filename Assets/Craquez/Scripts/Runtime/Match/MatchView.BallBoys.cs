using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Gandulas de verdade (atletas animados, só no desenho): ficam espalhados em volta do campo, correm até
    /// a bola que sai, devolvem com as mãos para quem vai cobrar (por baixo no lateral, por cima no fundo) e
    /// repõem a bola do cone quando o goleiro pega uma de lá — buscam a que saiu e a deixam no cone.
    /// </summary>
    public partial class MatchView
    {
        class Boy
        {
            public AthleteView av;
            public Vector2 home;
            public int task;        // 0 parado, 1 indo até a bola que saiu, 2 levando a bola ao cone, 3 voltando
            public Vector2 target;
            public int station = -1;
            public float busy;      // animação em curso (arremesso / pondo no cone)
            public bool carry;
        }

        readonly List<Boy> _boys = new List<Boy>();
        Transform _boyBall;
        bool _prevGkToss;

        /// <summary>Seis gandulas: dois em cada lateral e um atrás de cada gol, de colete.</summary>
        void BuildBallBoys(Transform parent, int q)
        {
            if (sim.mode.id != GameModes.Field && sim.mode.id != GameModes.Society) return;
            bool field = sim.mode.id == GameModes.Field;
            float hl = sim.halfLen, hw = sim.halfW;
            var vest = new Kit { c1 = new Color(0.98f, 0.82f, 0.1f), c2 = new Color(0.1f, 0.1f, 0.12f), shorts = new Color(0.12f, 0.12f, 0.15f),
                socks = new Color(0.1f, 0.1f, 0.12f), pattern = 0 };
            var spots = new List<Vector2>();
            float side = hw + (field ? 3.0f : 2.0f);
            foreach (float sy in new[] { -1f, 1f })
                foreach (float fx in new[] { -0.55f, 0.55f })
                    spots.Add(new Vector2(fx * hl, sy * side));
            foreach (float sx in new[] { -1f, 1f })
                spots.Add(new Vector2(sx * (hl + (field ? 3.4f : 2.2f)), sim.goalHalf + (field ? 7f : 4f)));
            int n = q >= 1 ? spots.Count : 4;
            for (int i = 0; i < n; i++)
            {
                var bp = new SimPlayer { id = -1, name = "Gandula", number = 0, face = new float[] { i % 4, (i + 1) % 3, i % 4, 0, i % 2, 1 } };
                var rig = AthleteFactory.Create(bp, vest, vest, parent, q);
                var av = rig.gameObject.AddComponent<AthleteView>();
                bp.position = spots[i];
                bp.facing = (-spots[i]).Norm();
                av.Setup(bp, rig, this);
                rig.transform.localScale = Vector3.one * 0.9f;   // garotos: um pouco menores que os atletas
                _boys.Add(new Boy { av = av, home = spots[i] });
            }
            if (_ball != null)
            {
                _boyBall = Instantiate(_ball.gameObject, transform).transform;
                _boyBall.name = "Bola do gandula";
                _boyBall.gameObject.SetActive(false);
            }
        }

        Boy NearestBoy(Vector2 at, bool freeOnly)
        {
            Boy best = null; float bd = float.MaxValue;
            foreach (var b in _boys)
            {
                if (freeOnly && (b.task == 2 || b.carry)) continue;
                float d = b.av.p.position.Dist(at);
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        /// <summary>A bola do cone foi usada: o gandula mais perto busca a que saiu e repõe o cone.</summary>
        void BallBoyRefill(int station)
        {
            if (_boys.Count == 0 || _decoy == null) return;
            var from = new Vector2(_decoy.position.x, _decoy.position.z);
            var b = NearestBoy(from, true);
            if (b == null) return;
            b.task = 1;
            b.target = from;
            b.station = station;
            if (station >= 0 && station < _stationHidden.Length) _stationHidden[station] = 60f;   // (segurança: repõe sozinho se algo der errado)
        }

        /// <summary>O gandula devolve a bola (evento balltoss): fica onde a bola sai e faz o arremesso.</summary>
        void BallBoyToss()
        {
            if (_boys.Count == 0) return;
            var from = sim.ball.pos;
            var b = NearestBoy(from, false);
            if (b == null) return;
            var p = b.av.p;
            // chega junto da bola (se ainda estava a caminho, termina o passo), de frente para quem vai receber
            var off = new Vector2(Mathf.Sign(from.x) * (Mathf.Abs(from.x) > sim.halfLen ? 0.35f : 0f), Mathf.Abs(from.y) > sim.halfW ? Mathf.Sign(from.y) * 0.35f : 0f);
            if (p.position.Dist(from) < 4f) p.position = from + off;
            var to = (sim.ball.vel.sqrMagnitude > 0.01f ? sim.ball.vel.normalized : (-from).Norm());
            p.facing = to;
            p.vel = Vector2.zero;
            b.task = b.task == 2 ? 2 : 3;
            b.busy = 0.9f;
            if (b.av.anim != null)
            {
                string clip = sim.ball.z > 1.3f ? "GkThrow" : "GkRoll";   // fundo: por cima; lateral: por baixo
                if (AthleteAnimator.Has(clip))
                {
                    float c = AthleteAnimator.KickContact(clip);
                    b.av.anim.Play(clip, Mathf.Max(0f, c - 0.05f), 0.8f, 1.1f, 0.05f);
                }
            }
        }

        void BallBoysStep()
        {
            if (_boys.Count == 0) return;
            float dt = MatchSim.DT;
            // bola saindo pela lateral (ou o goleiro pedindo a bola no fundo): o gandula mais perto corre até ela
            bool outTouch = sim.runOut && Mathf.Abs(sim.ball.pos.y) > sim.halfW + 0.3f && Mathf.Abs(sim.ball.pos.x) < sim.halfLen;
            if (outTouch)
            {
                var b = NearestBoy(sim.ball.pos, true);
                if (b != null && b.task == 0) { b.task = 3; b.busy = 0f; }
                if (b != null) b.target = new Vector2(sim.ball.pos.x, Mathf.Sign(sim.ball.pos.y) * Mathf.Max(Mathf.Abs(sim.ball.pos.y) + 0.4f, sim.halfW + 1.4f));
            }
            if (sim.gkToss && !_prevGkToss)
            {
                var from = sim.GkTossFrom;
                var b = NearestBoy(from, true);
                if (b != null) { b.task = 3; b.target = from + new Vector2(Mathf.Sign(from.x) * 0.35f, 0f); }
            }
            _prevGkToss = sim.gkToss;
            foreach (var b in _boys)
            {
                var p = b.av.p;
                if (b.busy > 0f)
                {
                    b.busy -= dt;
                    p.vel = Vector2.MoveTowards(p.vel, Vector2.zero, 12f * dt);
                    if (b.busy <= 0f && b.task == 2 && b.carry)
                    {
                        // pôs a bola no cone
                        b.carry = false;
                        if (b.station >= 0 && b.station < _stationBalls.Count)
                        {
                            _stationBalls[b.station].gameObject.SetActive(true);
                            _stationHidden[b.station] = 0f;
                        }
                        b.station = -1;
                        b.task = 3;
                    }
                }
                else
                {
                    Vector2 goal = b.task == 0 ? b.home : b.task == 3 && (b.target - p.position).sqrMagnitude < 0.04f ? b.home : b.target;
                    if (b.task == 3 && !sim.runOut && !sim.gkToss && !sim.ballToss) goal = b.home;
                    var to = goal - p.position;
                    float d = to.magnitude;
                    float speed = b.task == 0 ? 1.4f : b.carry ? 4.2f : 6.2f;
                    var want = d > 0.2f ? to / d * Mathf.Min(speed, d * 2.2f) : Vector2.zero;
                    p.vel = Vector2.MoveTowards(p.vel, want, 10f * dt);
                    p.position += p.vel * dt;
                    if (p.vel.sqrMagnitude > 0.2f) p.facing = V2.RotateToward(p.facing, p.vel.normalized, 8f * dt).Norm();
                    else p.facing = V2.RotateToward(p.facing, (sim.ball.pos - p.position).Norm(), 3f * dt).Norm();
                    if (b.task == 1 && d < 0.5f)
                    {
                        // apanhou a bola que tinha saído e leva ao cone
                        b.carry = true;
                        if (_decoy != null) _decoy.gameObject.SetActive(false);
                        if (b.station >= 0 && b.station < _stationBalls.Count)
                        {
                            var st = _stationBalls[b.station].position;
                            b.target = new Vector2(st.x, st.z) - new Vector2(Mathf.Sign(st.x) * 0.5f, 0f);
                            b.task = 2;
                        }
                        else { b.carry = false; b.task = 3; }
                    }
                    else if (b.task == 2 && d < 0.4f)
                    {
                        b.busy = 0.7f;
                        if (b.av.anim != null && AthleteAnimator.Has("GkPlace"))
                        {
                            float c = AthleteAnimator.KickContact("GkPlace");
                            b.av.anim.Play("GkPlace", Mathf.Max(0f, c - 0.5f), 0.75f, 1.2f, 0.1f);
                        }
                    }
                    else if (b.task == 3 && (b.home - p.position).sqrMagnitude < 0.09f && !sim.runOut && !sim.gkToss) b.task = 0;
                }
                b.av.OnTick();
            }
            // a bola que o gandula carrega (na frente do corpo, na altura da cintura)
            if (_boyBall != null)
            {
                Boy carrier = null;
                foreach (var b in _boys) if (b.carry) { carrier = b; break; }
                _boyBall.gameObject.SetActive(carrier != null);
                if (carrier != null)
                {
                    var p = carrier.av.p;
                    var at = p.position + p.facing * 0.32f;
                    _boyBall.position = new Vector3(at.x, 0.95f, at.y);
                }
            }
        }
    }
}
