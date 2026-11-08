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
            if (sim.throwIn && sim.ball.owner == p.id && p.id == sim.throwInTaker)
            {
                // lateral com as mãos: espera um instante e arremessa para a frente (longo perto da área)
                if (_holdBtn != 0 || _cooldown > 0) return f;
                if (Noise(sim, 50) > 0.03f) return f;
                var g2 = new Vector2(sim.halfLen * dir, 0f);
                bool nearBox = p.position.Dist(g2) < 30f;
                f.move = new Vector2(dir * 0.7f, -Mathf.Sign(p.position.y) * 0.7f).Norm();
                if (nearBox && Noise(sim, 51) < 0.5f) Press(ref f, InputFrame.Lob, 18);
                else Press(ref f, InputFrame.Pass, 8);
                return f;
            }
            if (sim.setPiece && sim.ball.owner == p.id && p.id == sim.setPieceTaker)
            {
                // falta: espera os companheiros se posicionarem e escolhe a cobrança
                if (_holdBtn != 0 || _cooldown > 0) return f;
                var g = new Vector2(sim.halfLen * dir, 0f);
                float dg = p.position.Dist(g);
                if (sim.kickIn)
                {
                    // lateral do futsal/society: passe curto para o companheiro mais livre (antes dos 4 s)
                    if (Noise(sim, 46) > 0.03f) return f;
                    SimPlayer mateBest = null;
                    float bestS = float.MinValue;
                    foreach (var m in sim.players)
                    {
                        if (m.team != p.team || m == p || m.isKeeper) continue;
                        float d = m.position.Dist(p.position);
                        float free = float.MaxValue;
                        foreach (var o in sim.players) if (o.team != p.team) free = Mathf.Min(free, o.position.Dist(m.position));
                        float sc = Mathf.Min(free, 5f) * 2f - Mathf.Abs(d - 7f) * 0.5f + (m.position.x - p.position.x) * dir * 0.2f;
                        if (sc > bestS) { bestS = sc; mateBest = m; }
                    }
                    f.move = mateBest != null ? (mateBest.position - p.position).Norm() : new Vector2(dir, 0f);
                    Press(ref f, InputFrame.Pass, 8);
                    return f;
                }
                if (sim.cornerKick)
                {
                    // escanteio: cruzamento na área (força varia: primeiro ou segundo pau) ou toque curto
                    // para o companheiro que veio buscar, se ele estiver livre
                    if (Noise(sim, 47) > 0.02f) return f;
                    var shortSpot = sim.CornerShortSpot();
                    SimPlayer near = null;
                    foreach (var m in sim.players)
                        if (m.team == p.team && m != p && !m.isKeeper && !m.sentOff && m.position.Dist(shortSpot) < (sim.mode.id == GameModes.Field ? 5f : 3f)) near = m;
                    float freeN = 99f;
                    if (near != null) foreach (var o in sim.players) if (o.team != p.team && !o.sentOff) freeN = Mathf.Min(freeN, o.position.Dist(near.position));
                    if (near != null && freeN > 2.5f && Noise(sim, 977) < 0.28f)
                    {
                        f.move = (near.position - p.position).Norm();
                        Press(ref f, InputFrame.Pass, 6);
                    }
                    else
                    {
                        f.move = (g - p.position).Norm();
                        Press(ref f, InputFrame.Lob, 10 + (int)(Noise(sim, 49) * 20f));
                    }
                    return f;
                }
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
                // condução sem rodar: a direção vira com limite e, se ficar girando para o mesmo lado
                // (em volta do marcador), segue para o gol por um instante
                mv = mv.Norm();
                if (_lastId != p.id) { _lastId = p.id; _lastMove = Vector2.zero; _spinAcc = 0f; _straightT = 0; }
                _spinAcc *= Mathf.Exp(-MatchSim.DT / 1.2f);
                if (_lastMove != Vector2.zero)
                {
                    if (_straightT > 0) { _straightT--; mv = Vector2.Lerp(_lastMove, toGoal.Norm(), 0.2f).Norm(); }
                    else
                    {
                        float ang = Vector2.SignedAngle(_lastMove, mv);
                        float maxStep = 260f * MatchSim.DT;
                        if (Mathf.Abs(ang) > maxStep && Mathf.Abs(ang) < 150f) { mv = _lastMove.Rotated(Mathf.Sign(ang) * maxStep * Mathf.Deg2Rad); ang = Mathf.Sign(ang) * maxStep; }
                        _spinAcc += ang;
                        if (Mathf.Abs(_spinAcc) > 220f) { _straightT = 50; _spinAcc = 0f; _carry = toGoal.Norm(); _carryT = 50; }
                    }
                }
                _lastMove = mv;
                f.move = mv;
                if (oppD > 3.5f - p.flair) f.buttons |= InputFrame.Sprint;
                var sty = StyleOf(p);
                // improviso: o driblador encara o marcador de mais longe, com o drible preferido (às vezes
                // outro); o veloz prefere a arrancada; o cauteloso quase não tenta
                float engage = Mathf.Lerp(2.2f, 3.6f, sty.dribble);
                if (opp != null && oppD < engage && _skillCd == 0 && _holdBtn == 0 && p.skill == Skill.None)
                {
                    float ahead = Vector2.Dot((opp.position - p.position).Norm(), p.facing);
                    if (ahead > 0.3f && Noise(sim, 9) < (0.025f + skill * 0.04f) * (0.25f + sty.dribble * 2.2f))
                    {
                        int kind = Noise(sim, 10) < 0.6f ? sty.favorite : (int)(Noise(sim, 11) * 4.99f);
                        float sd = Vector2.Dot(opp.position - p.position, new Vector2(-p.facing.y, p.facing.x)) > 0f ? -1f : 1f;   // sai pelo lado livre
                        f.rstick = SkillStick(kind, p.facing, sd, out bool sh);
                        if (sh) f.buttons |= InputFrame.Shield;
                        _skillCd = (int)Mathf.Lerp(70f, 35f, sty.dribble);
                    }
                    else if (ahead > 0.5f && sty.dribble > 0.55f && Noise(sim, 18) < 0.04f * sty.dribble)
                    {
                        // ginga: muda a direção da condução de repente (corte para dentro/para fora)
                        float sd = Noise(sim, 19) > 0.5f ? 1f : -1f;
                        _carry = _carry.Rotated(sd * Mathf.Lerp(0.6f, 1.0f, sty.risk));
                        _carryT = 18;
                        _skillCd = 25;
                    }
                }
                if (_holdBtn == 0 && _cooldown == 0)
                {
                    float shootRange = sim.halfLen * Mathf.Lerp(0.4f, 0.55f, skill) * (0.72f + 0.45f * sty.shooter + 0.15f * sty.risk) * (p.Has(Trait.LongaDistancia) ? 1.2f : 1f);
                    var option = BestOption(sim, p, out float optScore);
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
                    else if (option != null && optScore > 2.2f - sty.passer * 0.6f && oppD > 2.2f && Noise(sim, 14) < (0.03f + skill * 0.03f) * (0.4f + sty.passer * 1.2f) * (1.2f - sty.dribble * 0.5f))
                    {
                        // jogada para frente: passe (ou enfiada, se ele está correndo para o espaço) para quem está livre adiante
                        f.move = (option.position - p.position).Norm();
                        bool run = Vector2.Dot(option.vel, new Vector2(dir, 0f)) > 3f;
                        Press(ref f, run && Noise(sim, 16) > 0.4f ? InputFrame.Through : InputFrame.Pass, 8 + (int)(Noise(sim, 17) * 8f));
                    }
                    else if (oppD < 2.2f && option != null && Noise(sim, 2) < (0.05f + skill * 0.06f) * (1.5f - sty.dribble * 0.9f))
                    {
                        // pressionado: toca para o companheiro mais livre (não para uma direção qualquer)
                        f.move = (option.position - p.position).Norm();
                        float dd = option.position.Dist(p.position);
                        Press(ref f, dd > sim.halfLen * 0.6f ? InputFrame.Lob : InputFrame.Pass, 6 + (int)(Mathf.Clamp01(dd / sim.halfLen) * 10f));
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
                    float tackleRate = (0.022f + skill * 0.03f) * (behind ? 0.2f : 1f) * (0.6f + p.discipline * 0.6f);
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

        /// <summary>
        /// Melhor companheiro para o passe: livre (marcador longe), linha do passe aberta e quanto
        /// faz o time avançar; atrás só se estiver bem livre. Retorna a nota (maior = melhor).
        /// </summary>
        SimPlayer BestOption(MatchSim sim, SimPlayer p, out float bestS)
        {
            float dir = sim.TeamDir(p.team);
            SimPlayer best = null;
            bestS = float.MinValue;
            bool field = sim.mode.id == GameModes.Field;
            foreach (var m in sim.players)
            {
                if (m.team != p.team || m == p || m.isKeeper || m.sentOff) continue;
                float d = m.position.Dist(p.position);
                if (d < (field ? 5f : 2.5f) || d > sim.halfLen * 1.2f) continue;
                float free = 99f;
                foreach (var o in sim.players) if (o.team != p.team && !o.sentOff) free = Mathf.Min(free, o.position.Dist(m.position));
                float lane = sim.LaneFree(p.team, p.position, m.position);
                float gain = (m.position.x - p.position.x) * dir;
                float sc = Mathf.Min(free, field ? 8f : 5f) * 0.45f + Mathf.Min(lane, 4f) * 0.6f + gain * (field ? 0.09f : 0.16f) - d * 0.02f;
                if (lane < (field ? 1.2f : 0.8f)) sc -= 3f;
                if (sc > bestS) { bestS = sc; best = m; }
            }
            return best;
        }

        /// <summary>Direção de condução com menos adversários à frente (cone de ~6 m).</summary>
        /// <summary>Jeito de jogar de cada atleta (vem dos atributos, das habilidades e de um sorteio fixo pelo nome).</summary>
        struct Style
        {
            public float dribble, passer, shooter, risk;
            public int favorite;   // drible preferido: 0 arrancada, 1 puxada, 2 rolinho, 3 pedalada, 4 giro
        }

        readonly System.Collections.Generic.Dictionary<int, Style> _styles = new System.Collections.Generic.Dictionary<int, Style>();

        Style StyleOf(SimPlayer p)
        {
            if (_styles.TryGetValue(p.id, out var st)) return st;
            uint h = 2166136261u;   // FNV-1a do nome (determinístico em qualquer plataforma)
            foreach (char ch in p.name ?? "") { h ^= ch; h *= 16777619u; }
            h ^= (uint)p.id * 2654435761u;
            float R(int k) => ((h >> (k * 6)) & 63) / 63f;
            st.dribble = Mathf.Clamp01(p.flair * 0.75f + (p.Has(Trait.Driblador) ? 0.4f : 0f) + (R(0) - 0.5f) * 0.4f);
            st.passer = Mathf.Clamp01(p.passing * 0.8f + (p.Has(Trait.Maestro) ? 0.35f : 0f) + (R(1) - 0.5f) * 0.3f);
            st.shooter = Mathf.Clamp01(p.shot * 0.8f + (p.Has(Trait.Finalizador) ? 0.25f : 0f) + (p.Has(Trait.LongaDistancia) ? 0.25f : 0f) + (R(2) - 0.5f) * 0.3f);
            st.risk = R(3);
            st.favorite = p.Has(Trait.Velocista) && R(4) < 0.6f ? 0 : (int)(R(4) * 4.99f);
            _styles[p.id] = st;
            return st;
        }

        static Vector2 SkillStick(int kind, Vector2 facing, float side, out bool shield)
        {
            var perp = new Vector2(-facing.y, facing.x) * side;
            shield = kind == 3 || kind == 4;
            return kind == 0 ? facing : kind == 1 || kind == 4 ? -facing : perp;
        }

        Vector2 _lastMove;
        int _lastId = -1;
        float _spinAcc;
        int _straightT;

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
