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
            float side = hw + (field ? 5.0f : 2.0f);   // (campo: atrás das placas de publicidade, a 4,2 m da linha)
            foreach (float sy in new[] { -1f, 1f })
                foreach (float fx in new[] { -0.55f, 0.55f })
                    spots.Add(new Vector2(fx * hl, sy * side));
            foreach (float sx in new[] { -1f, 1f })
                spots.Add(new Vector2(sx * (hl + (field ? 6.0f : 2.2f)), sim.goalHalf + (field ? 7f : 4f)));   // (atrás das placas do fundo)
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

        /// <summary>Suaviza o movimento dos gandulas: aceleração progressiva, desaceleração antecipada,
        /// transição suave entre tarefas, animações de corrida/parada mais naturais.</summary>
        void UpdateBoyMovement(Boy b, float dt)
        {
            var p = b.av.p;
            Vector2 goal = b.task == 0 ? b.home : b.task == 3 && (b.target - p.position).sqrMagnitude < 0.04f ? b.home : b.target;
            if (b.task == 3 && !sim.runOut && !sim.gkToss && !sim.ballToss) goal = b.home;

            var to = goal - p.position;
            float d = to.magnitude;

            // Velocidade base por tarefa
            float baseSpeed = b.task == 0 ? 1.4f : b.carry ? 4.2f : 6.2f;

            // Curva de aceleração/desaceleração suave
            float accelRate = b.task == 0 ? 6f : 8f;    // mais lento ao sair do repouso
            float decelRate = b.task == 0 ? 4f : 10f;   // freia antes de chegar

            // Aproxima do alvo com curva S
            float arrivalDist = b.task == 0 ? 0.3f : 0.15f;
            float slowdownDist = baseSpeed * baseSpeed / (2f * decelRate);
            float effectiveSpeed = baseSpeed;

            if (d < slowdownDist + arrivalDist)
            {
                // Desacelera progressivamente
                float t = Mathf.Clamp01((d - arrivalDist) / slowdownDist);
                effectiveSpeed = Mathf.Lerp(0.5f, baseSpeed, t * t * (3f - 2f * t));
            }

            var want = d > arrivalDist ? to / d * Mathf.Min(effectiveSpeed, d * 2.2f) : Vector2.zero;
            p.vel = Vector2.MoveTowards(p.vel, want, accelRate * dt);

            // Suaviza rotação do corpo
            if (p.vel.sqrMagnitude > 0.2f)
            {
                float turnSpeed = b.task == 0 ? 6f : 10f;
                p.facing = V2.RotateToward(p.facing, p.vel.normalized, turnSpeed * dt).Norm();
            }
            else
            {
                // Parado: olha para a bola ou para o alvo
                var lookTarget = sim.ball.pos - p.position;
                if (lookTarget.sqrMagnitude > 0.01f)
                    p.facing = V2.RotateToward(p.facing, lookTarget.normalized, 3f * dt).Norm();
            }

            // Ajuste de posição
            p.position += p.vel * dt;
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

        /// <summary>O gandula devolve a bola (evento balltoss): fica onde a bola sai e faz o arremesso.
/// Melhora: posiciona corretamente, prepara o arremesso, usa animação apropriada.</summary>
        void BallBoyToss()
        {
            if (_boys.Count == 0) return;
            var from = sim.ball.pos;
            var b = NearestBoy(from, false);
            if (b == null) return;
            var p = b.av.p;

            // Determina o tipo de reposição
            bool isThrowIn = Mathf.Abs(from.y) > sim.halfW && Mathf.Abs(from.x) < sim.halfLen;
            bool isGoalKick = Mathf.Abs(from.x) > sim.halfLen - 1f;

            // Posiciona o gandula corretamente atrás da bola
            Vector2 off = isGoalKick
                ? new Vector2(Mathf.Sign(from.x) * 1.5f, 0f)  // para tiro de meta: atrás da linha de fundo
                : new Vector2(0f, Mathf.Sign(from.y) * 1.2f); // para lateral: atrás da linha lateral

            // Posiciona se estiver perto
            if (p.position.Dist(from) < 4f) p.position = from + off;

            // Olha para o alvo (cobrador/goleiro)
            var targetPos = isGoalKick ? sim.GkTossFrom : sim.ball.pos; // para lateral, joga para a posição do cobrador
            var to = (targetPos - from).normalized;
            p.facing = to;
            p.vel = Vector2.zero;

            b.task = b.task == 2 ? 2 : 3;
            b.busy = isThrowIn ? 1.2f : 1.0f; // lateral demora mais (prepara o arremesso)

            if (b.av.anim != null)
            {
                // Usa animações de arremesso mais apropriadas
                string clip = isThrowIn ? "GkThrow" : "GkRoll";
                if (AthleteAnimator.Has(clip))
                {
                    float c = AthleteAnimator.KickContact(clip);
                    float delay = isThrowIn ? 0.15f : 0.05f; // lateral tem preparação
                    b.av.anim.Play(clip, Mathf.Max(0f, c - delay), isThrowIn ? 1.0f : 0.8f, 1.1f, 0.1f);
                }
                else
                {
                    // Fallback: animação genérica de arremesso com as mãos
                    PlayGenericThrow(b, isThrowIn);
                }
            }
        }

        /// <summary>Animação genérica de arremesso quando não há clip específico.</summary>
        void PlayGenericThrow(Boy b, bool isThrowIn)
        {
            var p = b.av.p;
            var rig = b.av.rig;
            if (rig == null) return;

            // Animação procedural simples: braços para trás, depois para frente
            b.busy = isThrowIn ? 1.5f : 1.0f;
            b.task = b.task == 2 ? 2 : 3;
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
                // (no campo corre por trás das placas; a bola que saiu ele pega no lançamento)
                if (b != null) b.target = new Vector2(sim.ball.pos.x, Mathf.Sign(sim.ball.pos.y) * (sim.mode.id == GameModes.Field ? sim.halfW + 4.9f : Mathf.Max(Mathf.Abs(sim.ball.pos.y) + 0.4f, sim.halfW + 1.4f)));
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
                    // Usa o novo sistema de movimento suave
                    UpdateBoyMovement(b, dt);

                    // Lógica de transição de tarefas
                    if (b.task == 1)
                    {
                        Vector2 from = b.target;
                        float d = (from - p.position).magnitude;
                        if (d < 0.5f)
                        {
                            // Apanhou a bola que tinha saído e leva ao cone
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
                    }
                    else if (b.task == 2)
                    {
                        Vector2 toCone = b.target - p.position;
                        float d = toCone.magnitude;
                        if (d < 0.4f)
                        {
                            b.busy = 0.7f;
                            if (b.av.anim != null && AthleteAnimator.Has("GkPlace"))
                            {
                                float c = AthleteAnimator.KickContact("GkPlace");
                                b.av.anim.Play("GkPlace", Mathf.Max(0f, c - 0.5f), 0.75f, 1.2f, 0.1f);
                            }
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
