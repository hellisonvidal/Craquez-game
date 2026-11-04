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
        int _holdBtn, _holdTicks, _cooldown, _skillCd;
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
            if (sim.ball.owner == p.id)
            {
                var goal = new Vector2(sim.halfLen * dir, 0f);
                var toGoal = goal - p.position;
                var mv = toGoal.Norm();
                var opp = sim.NearestOpponent(p);
                float oppD = opp != null ? opp.position.Dist(p.position) : 99f;
                if (opp != null && oppD < 4.5f) mv = (mv + (p.position - opp.position).Norm() * 0.9f).Norm();
                if (Mathf.Abs(p.position.y) > sim.halfW * 0.85f) mv.y -= Mathf.Sign(p.position.y) * 0.8f;
                f.move = mv.Norm();
                if (oppD > 3.5f) f.buttons |= InputFrame.Sprint;
                if (opp != null && oppD < 2.6f && _skillCd == 0 && _holdBtn == 0 && p.skill == Skill.None)
                {
                    float ahead = Vector2.Dot((opp.position - p.position).Norm(), p.facing);
                    if (ahead > 0.3f && Noise(sim, 9) < 0.04f + skill * 0.06f)
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
                    float shootRange = sim.halfLen * Mathf.Lerp(0.32f, 0.45f, skill);
                    if (toGoal.magnitude < shootRange && Mathf.Abs(p.position.y) < sim.halfW * 0.6f)
                    {
                        f.move = new Vector2(dir, Noise(sim, 1) > 0.5f ? 0.6f : -0.6f);
                        Press(ref f, InputFrame.Shoot, (int)Mathf.Lerp(18f, 40f, toGoal.magnitude / shootRange));
                        _finesse = Noise(sim, 13) < 0.25f + skill * 0.2f;
                        if (_finesse) f.buttons |= InputFrame.Finesse;
                    }
                    else if (oppD < 2.2f && Noise(sim, 2) < 0.06f + skill * 0.08f)
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
                    if (bd < 1.3f && Noise(sim, 6) < 0.05f + skill * 0.06f) Press(ref f, InputFrame.Pass, 1);
                    else if (bd < 2.6f && bd > 1.4f && Noise(sim, 7) < 0.004f + skill * 0.006f) Press(ref f, InputFrame.Shoot, 1);
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

        void Press(ref InputFrame f, int btn, int ticks)
        {
            f.buttons |= btn;
            _holdBtn = btn;
            _holdTicks = Mathf.Max(1, ticks);
        }
    }
}
