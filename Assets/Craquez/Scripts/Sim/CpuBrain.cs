using UnityEngine;

namespace Craquez.Sim
{
    /// <summary>
    /// Adversário controlado pelo computador. Produz um InputFrame, exatamente
    /// como um controle físico ou um jogador remoto. Determinístico.
    /// </summary>
    public class CpuBrain
    {
        public int team;
        public float skill;
        int _holdBtn, _holdTicks, _cooldown, _skillCd, _carryT;
        Vector2 _carry;
        bool _finesse;

        public CpuBrain(int t = 1, float difficulty = 0.6f)
        {
            team = t;
            skill = difficulty;
        }

        float Noise(MatchSim sim, int salt)
        {
            unchecked
            {
                int h = (sim.tick * 73856093) ^ (salt * 19349663) ^ (team * 83492791);
                h = (h ^ (h >> 13)) * 1274126177;
                return (h & 0xFFFF) / 65535f;
            }
        }

        public InputFrame MakeInput(MatchSim sim)
        {
            var f = new InputFrame();
            if (sim.state != MatchState.Playing) return f;
            var p = sim.Controlled(team);
            float dir = sim.TeamDir(team);
            _cooldown = Mathf.Max(0, _cooldown - 1);
            _skillCd = Mathf.Max(0, _skillCd - 1);
            if (_holdBtn != 0)
            {
                f.buttons |= _holdBtn;
                if (_finesse) f.buttons |= InputFrame.Finesse;
                _holdTicks--;
                if (_holdTicks <= 0)
                {
                    f.buttons &= ~_holdBtn;
                    _holdBtn = 0;
                    _finesse = false;
                    _cooldown = 20;
                }
            }
            if (sim.kickoffPass && sim.ball.owner == p.id && p.id == sim.kickoffKicker)
            {
                // saída de bola: toque para o companheiro depois de um instante
                if (sim.kickoffMate >= 0 && sim.players[sim.kickoffMate].position.Dist(sim.kickoffMateSpot) < 0.8f && Noise(sim, 21) < 0.06f)
                    f.buttons |= InputFrame.Pass;
                return f;
            }
            if (sim.setPiece && sim.ball.owner == p.id && p.id == sim.setPieceTaker)
            {
                // falta: espera os companheiros se posicionarem e escolhe a cobrança
                if (_holdBtn != 0 || _cooldown > 0) return f;
                var g = new Vector2(sim.halfLen * dir, 0f);
                float dg = p.position.Dist(g);
                if (Noise(sim, 40) > 0.012f) return f;
                float r = Noise(sim, 41);
                if (sim.setPieceShot && dg < 28f && r < 0.45f + p.shot * 0.3f)
                {
                    float side = Noise(sim, 42) > 0.5f ? 1f : -1f;
                    f.move = new Vector2(dir, side * 0.7f);
                    _finesse = Noise(sim, 43) < 0.7f;
                    if (_finesse) f.buttons |= InputFrame.Finesse;
                    Press(ref f, InputFrame.Shoot, (int)Mathf.Lerp(22f, 38f, dg / 28f));
                }
                else if (dg < sim.halfLen * 0.85f && r < 0.8f)
                {
                    f.move = (g - p.position).Norm();
                    Press(ref f, InputFrame.Lob, 14 + (int)(Noise(sim, 44) * 16f));
                }
                else
                {
                    f.move = new Vector2(dir, (Noise(sim, 45) - 0.5f) * 1.4f).Norm();
                    Press(ref f, InputFrame.Pass, 10);
                }
                return f;
            }
            if (sim.ball.owner == p.id)
            {
                var goal = new Vector2(sim.halfLen * dir, 0f);
                var toGoal = goal - p.position;
                var opp = sim.NearestOpponent(p);
                float oppD = opp != null ? opp.position.Dist(p.position) : 99f;
                // conduz para o espaço: entre algumas direções (rumo ao gol e abrindo para os lados),
                // escolhe a menos congestionada; o driblador encara mais, o cauteloso foge do marcador
                // (decide a cada ~0,2 s e mantém: sem tremer entre duas direções)
                if (--_carryT <= 0 || _carry == Vector2.zero) { _carry = BestCarry(sim, p, toGoal.Norm()); _carryT = 12; }
                var mv = Vector2.Lerp(_carry, toGoal.Norm(), 0.15f).Norm();
                if (Mathf.Abs(p.position.y) > sim.halfW * 0.85f) mv.y -= Mathf.Sign(p.position.y) * 0.8f;
                f.move = mv.Norm();
                if (oppD > 3.5f - p.flair) f.buttons |= InputFrame.Sprint;
                if (opp != null && oppD < 2.6f && _skillCd == 0 && _holdBtn == 0 && p.skill == Skill.None)
                {
                    float ahead = Vector2.Dot((opp.position - p.position).Norm(), p.facing);
                    if (ahead > 0.3f && Noise(sim, 9) < (0.03f + skill * 0.05f) * (0.4f + p.flair * 1.4f))
                    {
                        var perp = new Vector2(-p.facing.y, p.facing.x);
                        float r = Noise(sim, 10);
                        if (r < 0.4f) f.rstick = perp * (Noise(sim, 11) > 0.5f ? 1f : -1f);
                        else if (r < 0.7f)
                        {
                            f.rstick = perp * (Noise(sim, 12) > 0.5f ? 1f : -1f);
                            f.buttons |= InputFrame.Shield;
                        }
                        else if (r < 0.85f) f.rstick = -p.facing;
                        else
                        {
                            f.rstick = -p.facing;
                            f.buttons |= InputFrame.Shield;
                        }
                        _skillCd = 50;
                    }
                }
                if (_holdBtn == 0 && _cooldown == 0)
                {
                    float shootRange = sim.halfLen * Mathf.Lerp(0.32f, 0.45f, skill) * (0.8f + 0.35f * p.shot) * (p.Has(Trait.LongaDistancia) ? 1.2f : 1f);
                    if (toGoal.magnitude < shootRange && Mathf.Abs(p.position.y) < sim.halfW * 0.7f)
                    {
                        // mira o canto longe do goleiro
                        float side = Noise(sim, 1) > 0.5f ? 1f : -1f;
                        foreach (var k in sim.players)
                            if (k.isKeeper && k.team != team) side = k.position.y > 0.2f ? -1f : k.position.y < -0.2f ? 1f : side;
                        f.move = new Vector2(dir, side * 0.6f);
                        Press(ref f, InputFrame.Shoot, (int)Mathf.Lerp(18f, 40f, toGoal.magnitude / shootRange));
                        _finesse = Noise(sim, 13) < 0.25f + skill * 0.2f;
                        if (_finesse) f.buttons |= InputFrame.Finesse;
                    }
                    else if (oppD > 2.2f && Noise(sim, 14) < (0.012f + skill * 0.012f) * (0.6f + p.passing * 0.8f) && sim.HasForwardOption(p))
                    {
                        // jogada para frente: passe ou enfiada para quem está livre mais adiante
                        f.move = new Vector2(dir, (Noise(sim, 15) - 0.5f) * 0.8f).Norm();
                        Press(ref f, Noise(sim, 16) > 0.45f ? InputFrame.Pass : InputFrame.Through, 10 + (int)(Noise(sim, 17) * 12f));
                    }
                    else if (oppD < 2.2f && Noise(sim, 2) < (0.06f + skill * 0.08f) * (1.4f - p.flair * 0.7f))
                    {
                        f.move = new Vector2(dir, (Noise(sim, 3) - 0.5f) * 1.6f).Norm();
                        Press(ref f, Noise(sim, 4) > 0.25f ? InputFrame.Pass : InputFrame.Lob, 8 + (int)(Noise(sim, 5) * 14f));
                    }
                }
            }
            else
            {
                var target = sim.ball.pos + sim.ball.vel * 0.3f;
                if (sim.ball.owner >= 0 && sim.players[sim.ball.owner].team != team)
                {
                    var ownGoal = new Vector2(-sim.halfLen * dir, 0f);
                    target = sim.ball.pos + (ownGoal - sim.ball.pos).Norm() * 0.7f;
                }
                var to = target - p.position;
                float d = to.magnitude;
                f.move = d > 0.05f ? (to / d) * Mathf.Clamp(d / 1.5f, 0.3f, 1f) : Vector2.zero;
                if (d > 3f) f.buttons |= InputFrame.Sprint;
                var owner = sim.Owner;
                if (owner != null && owner.team != team && p.position.Dist(owner.position) < 3.5f)
                {
                    f.buttons |= InputFrame.Shield;
                    f.buttons &= ~InputFrame.Sprint;
                }
                if (owner != null && owner.team != team && _cooldown == 0)
                {
                    float bd = p.position.Dist(sim.ball.pos);
                    // bote: escolhe o momento (de frente para o portador); por trás quase não arrisca
                    bool behind = Vector2.Dot(owner.facing, (p.position - owner.position).Norm()) < -0.45f;
                    float tackleRate = (0.05f + skill * 0.06f) * (behind ? 0.2f : 1f) * (0.6f + p.discipline * 0.6f);
                    if (bd < 1.3f && Noise(sim, 6) < tackleRate) Press(ref f, InputFrame.Pass, 1);
                    else if (bd < 2.6f && bd > 1.4f && !behind && Noise(sim, 7) < (0.002f + skill * 0.004f) * (0.5f + p.discipline)) Press(ref f, InputFrame.Shoot, 1);
                }
                else if (owner == null && _cooldown == 0)
                {
                    float meD = p.position.Dist(sim.ball.pos);
                    var n = sim.NearestToBall(team);
                    if (n != null && n.id != p.id && n.position.Dist(sim.ball.pos) + 4f < meD)
                    {
                        f.buttons |= InputFrame.Switch;
                        _cooldown = 15;
                    }
                }
            }
            return f;
        }

        /// <summary>Direção de condução com menos adversários à frente (cone de ~6 m).</summary>
        Vector2 BestCarry(MatchSim sim, SimPlayer p, Vector2 toGoal)
        {
            var best = toGoal;
            float bestS = float.MinValue;
            float range = sim.mode.id == GameModes.Field ? 7f : 4.5f;
            for (int i = -2; i <= 2; i++)
            {
                var d = toGoal.Rotated(i * 0.42f);
                float sc = Vector2.Dot(d, toGoal) * 1.2f;
                foreach (var o in sim.players)
                {
                    if (o.team == p.team) continue;
                    var to = o.position - p.position;
                    float dist = to.magnitude;
                    if (dist > range || dist < 0.01f) continue;
                    float cos = Vector2.Dot(to / dist, d);
                    if (cos > 0.5f) sc -= (cos - 0.5f) * 2f * (1f - dist / range) * (1.5f - p.flair);
                }
                // perto da lateral, não corre para fora
                var ahead = p.position + d * 4f;
                if (Mathf.Abs(ahead.y) > sim.halfW - 1f) sc -= 1f;
                sc += (Noise(sim, 30 + i) - 0.5f) * 0.15f;   // um pouco de imprevisibilidade
                if (sc > bestS) { bestS = sc; best = d; }
            }
            return best;
        }

        void Press(ref InputFrame f, int btn, int ticks)
        {
            f.buttons |= btn;
            _holdBtn = btn;
            _holdTicks = Mathf.Max(1, ticks);
        }
    }
}
