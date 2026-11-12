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
        int _throwWait;
        int _holdBtn, _holdTicks, _cooldown, _skillCd, _carryT, _gkFeetT;
        int _fakeOn;   // finta de chute: em quantos passos do fim do chute armado aperta o passe
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
                // finta de chute: com o chute armado, aperta o passe (o chute é cancelado e ele corta para o lado)
                if (_fakeOn > 0 && _holdTicks == _fakeOn) { f.buttons |= InputFrame.Pass; _holdTicks = 1; _fakeOn = 0; }
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
            if (!(sim.throwIn && sim.ball.owner == p.id && p.id == sim.throwInTaker)) _throwWait = 0;
            if (sim.throwIn && sim.ball.owner == p.id && p.id == sim.throwInTaker)
            {
                // lateral com as mãos: espera os companheiros se apresentarem (~2-3 s, antes saía em meio
                // segundo) e arremessa no mais livre; longo na área só se houver alguém livre lá
                if (_holdBtn != 0 || _cooldown > 0) return f;
                _throwWait++;
                if (_throwWait < 120 || (_throwWait < 200 && Noise(sim, 50) > 0.02f)) return f;
                var g2 = new Vector2(sim.halfLen * dir, 0f);
                SimPlayer mate = null; float best = float.MinValue; bool longOne = false;
                foreach (var m in sim.players)
                {
                    if (m.team != p.team || m == p || m.isKeeper || m.sentOff) continue;
                    float d = m.position.Dist(p.position);
                    if (d < 3f || d > 26f) continue;
                    float free = 99f;
                    foreach (var o in sim.players) if (o.team != p.team && !o.sentOff) free = Mathf.Min(free, o.position.Dist(m.position));
                    // adversário no caminho do arremesso
                    foreach (var o in sim.players)
                    {
                        if (o.team == p.team || o.sentOff) continue;
                        var ab = m.position - p.position; float tt = Mathf.Clamp01(Vector2.Dot(o.position - p.position, ab) / Mathf.Max(0.01f, ab.sqrMagnitude));
                        if (tt > 0.15f && tt < 0.9f && o.position.Dist(p.position + ab * tt) < 1.5f) free -= 2f;
                    }
                    float sc = Mathf.Min(free, 6f) * 2f - Mathf.Max(0f, d - 14f) * 0.4f + (m.position.x - p.position.x) * dir * 0.1f;
                    if (sc > best) { best = sc; mate = m; longOne = d > 16f; }
                }
                if (mate == null && _throwWait < 300) return f;   // ninguém livre: espera mais um pouco
                f.move = mate != null ? (mate.position - p.position).Norm() : new Vector2(dir * 0.7f, -Mathf.Sign(p.position.y) * 0.7f).Norm();
                _throwWait = 0;
                if (longOne) Press(ref f, InputFrame.Lob, 18);
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
                if (p.isKeeper)
                {
                    // tiro de meta / reposição com os pés: companheiro livre por perto (passe) ou lançamento
                    // para quem está sozinho lá na frente — nunca um chutão sem destino
                    var opt = BestOption(sim, p, out float os);
                    SimPlayer far = null; float farS = float.MinValue;
                    foreach (var m in sim.players)
                    {
                        if (m.team != p.team || m == p || m.isKeeper || m.sentOff) continue;
                        float ahead = (m.position.x - p.position.x) * dir;
                        if (ahead < sim.halfLen * 0.35f) continue;
                        float free = 99f;
                        foreach (var o in sim.players) if (o.team != p.team && !o.sentOff) free = Mathf.Min(free, o.position.Dist(m.position));
                        float sc = Mathf.Min(free, 10f) + ahead * 0.03f;
                        if (sc > farS) { farS = sc; far = m; }
                    }
                    bool shortOk = opt != null && os > 1.6f && opt.position.Dist(p.position) < sim.halfLen * 0.6f;
                    if (shortOk && (far == null || Noise(sim, 48) < 0.65f))
                    {
                        f.move = (opt.position - p.position).Norm();
                        Press(ref f, InputFrame.Pass, 10);
                    }
                    else if (far != null)
                    {
                        f.move = (far.position - p.position).Norm();
                        Press(ref f, InputFrame.Lob, 22 + (int)(Noise(sim, 44) * 10f));
                    }
                    else
                    {
                        f.move = new Vector2(dir, 0f);
                        Press(ref f, InputFrame.Lob, 26);
                    }
                    return f;
                }
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
            if (sim.ball.owner != p.id || !p.isKeeper) _gkFeetT = 0;
            if (sim.ball.owner == p.id && p.isKeeper && !p.InHands)
            {
                // goleiro com a bola nos pés: passe para quem está livre e com a linha aberta, ou
                // lançamento para o mais sozinho lá na frente — nunca sai driblando nem chuta a esmo
                if (_holdBtn != 0 || _cooldown > 0) return f;
                _gkFeetT++;
                var m = sim.KeeperFootChoice(p, _gkFeetT > (sim.mode.id == GameModes.Futsal ? 8 : 70), Noise(sim, 61), out int how);
                if (how < 0) return f;   // espera um companheiro se desmarcar (até ~1 s)
                _gkFeetT = 0;
                if (how != 0 && sim.mode.id == GameModes.Futsal)
                {
                    // quadra: sem ninguém livre de verdade, o melhor passe possível (linha aberta) rente ao chão
                    var o2 = BestOption(sim, p, out float s2);
                    if (o2 != null && s2 > 0f) { m = o2; how = 0; }
                }
                f.move = m != null ? (m.position - p.position).Norm() : new Vector2(dir, 0f);
                if (how == 0) Press(ref f, InputFrame.Pass, 8);
                else Press(ref f, InputFrame.Lob, how == 1 ? 26 : 14);
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
                        if (p.signature != Skill.None && Noise(sim, 12) < 0.55f)
                        {
                            // o drible característico dele (colocado + analógico direito)
                            f.rstick = new Vector2(-p.facing.y, p.facing.x) * sd;
                            f.buttons |= InputFrame.Finesse;
                        }
                        else
                        {
                            f.rstick = SkillStick(kind, p.facing, sd, out bool sh);
                            if (sh) f.buttons |= InputFrame.Shield;
                        }
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
                    // cara a cara (ninguém de linha entre ele e o gol nem chegando): conduz até perto e só
                    // então finaliza, ou quando o goleiro sai em cima (antes chutava de ~20 m sozinho)
                    bool alone = true;
                    foreach (var o in sim.players)
                    {
                        if (o.team == p.team || o.isKeeper || o.sentOff) continue;
                        var ro = o.position - p.position;
                        if (ro.magnitude < 4.5f || ((o.position.x - p.position.x) * dir > -1f && ro.magnitude < toGoal.magnitude)) { alone = false; break; }
                    }
                    SimPlayer gkO = null;
                    foreach (var k in sim.players) if (k.isKeeper && k.team != team) gkO = k;
                    float gkD = gkO != null ? gkO.position.Dist(p.position) : 99f;
                    bool fieldM = sim.mode.id == GameModes.Field;
                    float finish = fieldM ? 11.5f : sim.halfLen * 0.38f;
                    bool holdOn = alone && sim.mode.keeper && toGoal.magnitude > finish && gkD > (fieldM ? 10.5f : sim.mode.id == GameModes.Futsal ? 6.5f : 6.8f);
                    // tabela: acabou de receber de quem agora corre livre à frente — devolve de primeira, na corrida
                    var giver = sim.LastPasser >= 0 && sim.LastPasser < sim.players.Count ? sim.players[sim.LastPasser] : null;
                    bool giveGo = giver != null && giver.team == team && giver != p && !giver.sentOff && _cooldown == 0
                        && sim.tick - sim.TakeTick < 40 && sim.tick - sim.PassTick < 120
                        && (giver.position.x - p.position.x) * dir > 2f && Vector2.Dot(giver.vel, new Vector2(dir, 0f)) > 2.5f
                        && sim.LaneFree(team, p.position, giver.position + giver.vel * 0.6f) > (fieldM ? 1.4f : 1f)
                        && toGoal.magnitude > shootRange * 0.8f && Noise(sim, 31) < 0.35f;
                    // goleiro adiantado e bola perto da área: cavadinha (passe alto por cima dele)
                    bool chip = gkO != null && sim.mode.keeper && toGoal.magnitude < (fieldM ? 24f : sim.halfLen * 0.6f)
                        && Mathf.Abs(gkO.position.x - sim.halfLen * dir) > (fieldM ? 3.5f : 2.2f) && gkD > 2.5f && gkD < toGoal.magnitude * 0.9f;
                    // zagueiro bem na frente, fechando o chute: finta de chute (quem dribla bem)
                    bool blocker = false;
                    foreach (var o in sim.players)
                    {
                        if (o.team == team || o.isKeeper || o.sentOff) continue;
                        var ro = o.position - p.position;
                        if (ro.magnitude < 2.6f && Vector2.Dot(ro.normalized, toGoal.normalized) > 0.75f) { blocker = true; break; }
                    }
                    if (giveGo)
                    {
                        f.move = (giver.position + giver.vel * 0.7f - p.position).Norm();
                        Press(ref f, InputFrame.Through, 6 + (int)(Noise(sim, 33) * 5f));
                    }
                    else if (chip && toGoal.magnitude < shootRange && Noise(sim, 32) < 0.04f + skill * 0.04f)
                    {
                        f.move = toGoal.Norm();
                        Press(ref f, InputFrame.Lob, 10 + (int)(Noise(sim, 34) * 6f));
                    }
                    else if (blocker && toGoal.magnitude < shootRange * 1.1f && sty.dribble > 0.45f && Noise(sim, 35) < 0.03f + sty.dribble * 0.03f)
                    {
                        f.move = toGoal.Norm();
                        Press(ref f, InputFrame.Shoot, 18);
                        _fakeOn = 9;
                    }
                    else if (holdOn)
                    {
                        f.move = toGoal.Norm();
                        f.buttons |= InputFrame.Sprint;
                    }
                    else if (toGoal.magnitude < shootRange && Mathf.Abs(p.position.y) < sim.halfW * 0.7f)
                    {
                        // mira o canto longe do goleiro
                        float side = Noise(sim, 1) > 0.5f ? 1f : -1f;
                        foreach (var k in sim.players)
                            if (k.isKeeper && k.team != team) side = k.position.y > 0.2f ? -1f : k.position.y < -0.2f ? 1f : side;
                        f.move = new Vector2(dir, side * 0.6f);
                        // cara a cara: finalização rápida e colocada (antes carregava o chute na cara do goleiro)
                        Press(ref f, InputFrame.Shoot, alone ? (fieldM ? 15 : 10) + (int)(Noise(sim, 12) * (fieldM ? 9f : 6f)) : (int)Mathf.Lerp(18f, 40f, toGoal.magnitude / shootRange));
                        _finesse = Noise(sim, 13) < (alone ? 0.4f : 0.25f + skill * 0.2f);
                        if (_finesse) f.buttons |= InputFrame.Finesse;
                    }
                    else if (option != null && optScore > 2.2f - sty.passer * 0.6f && oppD > 2.2f && Noise(sim, 14) < (0.03f + skill * 0.03f) * (0.4f + sty.passer * 1.2f) * (1.2f - sty.dribble * 0.5f))
                    {
                        // jogada para frente: passe (ou enfiada, se ele está correndo para o espaço) para quem está livre adiante
                        f.move = (option.position - p.position).Norm();
                        bool run = Vector2.Dot(option.vel, new Vector2(dir, 0f)) > 3f;
                        Press(ref f, run && Noise(sim, 16) > 0.4f ? InputFrame.Through : InputFrame.Pass, 8 + (int)(Noise(sim, 17) * 8f));
                    }
                    else if (oppD < 2.2f && option != null && (optScore < -0.5f || sim.LaneFree(p.team, p.position, option.position) < (sim.mode.id == GameModes.Field ? 1.1f : 0.7f)))
                    {
                        // pressionado e sem linha de passe: protege a bola e sai para o lado oposto ao marcador
                        // (antes tocava assim mesmo, por dentro do marcador, e perdia a bola)
                        var away = (p.position - opp.position).Norm();
                        var side = new Vector2(-away.y, away.x);
                        if (Vector2.Dot(side, toGoal) < 0f) side = -side;
                        f.move = (away * 0.7f + side * 0.5f).Norm();
                        f.buttons |= InputFrame.Shield;
                        f.buttons &= ~InputFrame.Sprint;
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
                // goleiro adversário com a bola nas mãos: ninguém vai em cima dele (não pode disputar) — recua
                // para fora da área, de frente para a jogada (antes o atacante corria em volta do goleiro)
                var gkH = sim.Owner;
                bool gkHold = gkH != null && gkH.team != team && gkH.isKeeper && gkH.hold > 0f;
                if (gkHold)
                {
                    var outward = new Vector2(sim.TeamDir(gkH.team), 0f);   // (do gol do goleiro para o meio do campo)
                    target = new Vector2(gkH.position.x + outward.x * (sim.mode.boxDepth + 3f), Mathf.Clamp(p.position.y, -sim.halfW * 0.6f, sim.halfW * 0.6f));
                }
                var to = target - p.position;
                float d = to.magnitude;
                f.move = d > 0.05f ? (to / d) * Mathf.Clamp(d / 1.5f, 0.3f, gkHold ? 0.45f : 1f) : Vector2.zero;
                if (d > 3f && !gkHold) f.buttons |= InputFrame.Sprint;
                if (gkHold) return f;
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
                // sem passe bobo: o adversário corre enquanto a bola viaja (num passe longo ele chega bem mais
                // longe) e o companheiro colado no marcador perde a bola no domínio
                float risk = InterceptMargin(sim, p, m.position, field ? 16f : 11f);
                if (risk < 0f) sc -= 4f + Mathf.Min(3f, -risk);
                else if (risk < 0.8f) sc -= 1.2f;
                if (free < (field ? 1.4f : 1.0f)) sc -= 2.5f;
                if (sc > bestS) { bestS = sc; best = m; }
            }
            return best;
        }

        /// <summary>
        /// Folga do passe (m): para cada adversário perto da linha, a distância até ela menos o quanto ele anda
        /// até a bola passar por ali. Negativo = ele chega antes (passe interceptado).
        /// </summary>
        static float InterceptMargin(MatchSim sim, SimPlayer p, Vector2 to, float passSpeed)
        {
            var dv = to - p.position;
            float L = dv.magnitude;
            if (L < 0.1f) return 99f;
            var u = dv / L;
            var nrm = new Vector2(-u.y, u.x);
            float best = 99f;
            foreach (var o in sim.players)
            {
                if (o.team == p.team || o.sentOff) continue;
                var r = o.position - p.position;
                float t = Vector2.Dot(r, u);
                if (t < 0.3f || t > L + 1f) continue;
                float side = Mathf.Abs(Vector2.Dot(r, nrm));
                float reach = (o.isKeeper ? 1.2f : 0.7f) + 5.5f * (t / passSpeed) + Mathf.Max(0f, Vector2.Dot(o.vel, -Mathf.Sign(Vector2.Dot(r, nrm)) * nrm)) * (t / passSpeed);
                best = Mathf.Min(best, side - reach);
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
