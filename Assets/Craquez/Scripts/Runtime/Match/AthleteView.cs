using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>Liga um SimPlayer ao atleta 3D: posição/rumo interpolados, animações e poses extras.</summary>
    public class AthleteView : MonoBehaviour
    {
        public SimPlayer p;
        public AthleteRig rig;
        public AthleteAnimator anim;
        public MatchView view;
        Vector3 _prev, _cur;
        float _yawRate;
        float _yaw, _lean, _pitch, _hop, _hopT, _celebrate, _diveSide, _slideT;
        Vector2 _lastVel;
        bool _diag;
        Transform _marker;

        public static Vector3 W(Vector2 v, float y = 0f) => new Vector3(v.x, y, v.y);

        public void Setup(SimPlayer sp, AthleteRig r, MatchView v)
        {
            p = sp;
            rig = r;
            view = v;
            anim = new AthleteAnimator(r.animator, sp.name, sp.id);
            _prev = _cur = W(sp.position);
            _yaw = Yaw(sp.facing);
            transform.position = _cur;
            MeasureSoles();
            InitFingers();
            SkinnedMeshRenderer brow = null, lash = null;
            foreach (var sr in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { if (sr.name == "Eyebrows") brow = sr; else if (sr.name == "Eyelashes") lash = sr; }
            _face = new FaceDriver(sp.id * 31 + 7, rig.body, brow, lash);
        }

        FaceDriver _face;
        float _footSole = 0.085f, _ballSole = 0.035f;

        /// <summary>
        /// Distância real (já na escala do atleta) dos ossos do pé e da ponta do pé até a sola da chuteira,
        /// medida na pose de ligação — e as travas, no campo. Antes eram valores fixos (8,5 e 3,5 cm) que não
        /// batiam com cada biotipo nem com a altura: o calcanhar afundava até ~1,5 cm no gramado.
        /// </summary>
        void MeasureSoles()
        {
            if (rig == null || rig.footL == null) return;
            rig.bones.TryGetValue("ball_l", out var ball);
            foreach (var r in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name != "Boots" || r.sharedMesh == null) continue;
                var bb = r.sharedMesh.bounds;
                float mn = float.MaxValue;
                for (int cx = -1; cx <= 1; cx += 2) for (int cy = -1; cy <= 1; cy += 2) for (int cz = -1; cz <= 1; cz += 2)
                    mn = Mathf.Min(mn, r.transform.TransformPoint(bb.center + Vector3.Scale(bb.extents, new Vector3(cx, cy, cz))).y);
                float studs = AthleteFactory.IndoorShoes ? 0f : 0.008f * (rig.model != null ? rig.model.lossyScale.y : 1f);
                _footSole = Mathf.Clamp(rig.footL.position.y - mn + studs, 0.05f, 0.14f);
                if (ball != null) _ballSole = Mathf.Clamp(ball.position.y - mn + studs, 0.012f, 0.06f);
                break;
            }
        }
        float _effortT;

        /// <summary>
        /// Expressões: esforço no pique (sobrancelha franzida, boca entreaberta), cansaço (boca aberta,
        /// ofegante), grito e sorriso no gol, careta de dor no chão, protesto, concentração na cobrança.
        /// Só perto da câmera (de longe, rosto neutro e sem custo).
        /// </summary>
        void Expressions()
        {
            if (_face == null || !_face.Valid || rig.head == null) return;
            var cam = view != null && view.bcam != null ? view.bcam.cam : null;
            if (cam == null || (cam.transform.position - rig.head.position).sqrMagnitude > 34f * 34f) { _face.Neutral(); return; }
            float dt = Time.deltaTime, t = Time.time + _phaseOff;
            float spd = p.vel.magnitude;
            float sprint = Mathf.Clamp01((spd - 5f) / 2.5f), tired = Mathf.Clamp01((0.55f - p.stamina) / 0.4f);
            float act = anim != null ? anim.ActionWeight : 0f;
            if (_kickBodyT > 0f || p.windup > 0f) _effortT = 0.6f; else _effortT = Mathf.Max(0f, _effortT - dt);
            float jaw = 0.08f * Mathf.Max(0f, Mathf.Sin(t * 0.7f)), smile = 0.05f + 0.1f * Mathf.Max(0f, Mathf.Sin(t * 0.23f)), down = 0f, up = 0f;
            bool shut = false;
            // corrida: esforço cresce com a velocidade; cansado, boca aberta no ritmo da respiração
            down = Mathf.Max(down, sprint * 0.65f + Mathf.Clamp01((spd - 3f) / 3f) * 0.2f);
            jaw = Mathf.Max(jaw, sprint * 0.3f + tired * (0.35f + 0.25f * Mathf.Sin(t * 6.5f)));
            if (sprint > 0.2f || tired > 0.2f) smile = 0f;
            // chute/passe forte: dentes cerrados, olhar fixo
            if (_effortT > 0f) { down = Mathf.Max(down, 0.8f); jaw = Mathf.Min(jaw, 0.12f); smile = 0f; }
            // comemoração: grito com a boca bem aberta e sorriso
            if (_celebrate > 0f) { smile = 1f; jaw = 0.55f + 0.35f * Mathf.Abs(Mathf.Sin(t * 3.2f)); down = 0f; up = 0.35f; }
            // no chão (falta): dor
            if (p.down > 0f && !p.isKeeper) { down = 1f; jaw = 0.45f + 0.2f * Mathf.Sin(t * 4f); smile = 0f; shut = p.down > 0.6f && Mathf.Sin(t * 1.3f) > 0.2f; }
            // reclamando com o juiz / pedindo a bola
            if (_protest > 0.2f) { down = Mathf.Max(down, 0.75f); jaw = Mathf.Max(jaw, 0.4f + 0.25f * Mathf.Abs(Mathf.Sin(t * 5f))); smile = 0f; }
            // goleiro na defesa: olhos arregalados e boca firme
            if (p.isKeeper && (p.saveKind > 0 || _gkReady > 0.4f)) { down = Mathf.Max(down, 0.5f); up = 0f; jaw = Mathf.Max(jaw, p.saveKind > 0 ? 0.35f : 0.05f); smile = 0f; }
            _face.Update(dt, jaw, smile, down, up, shut);
        }

        // ------------------------------------------------------------ mãos relaxadas
        // A captura de movimento não tem dedos: as mãos ficavam espalmadas, duras como tábuas. Na pose de
        // ligação curvamos cada falange na direção da palma (como a mão solta de quem corre) e repomos
        // essa curva a cada quadro.
        Transform[] _fingers;
        Transform _midL, _midR;
        Quaternion[] _fingerRot;
        static readonly string[] FingerNames = { "index", "middle", "ring", "pinky", "thumb" };

        void InitFingers()
        {
            if (rig == null) return;
            var list = new System.Collections.Generic.List<Transform>();
            var rots = new System.Collections.Generic.List<Quaternion>();
            foreach (var sfx in new[] { "_l", "_r" })
            {
                if (!rig.bones.TryGetValue("hand" + sfx, out var hd)) continue;
                Transform F(string n) => FindDeep(hd, n);
                var i1 = F("index_01" + sfx); var p1 = F("pinky_01" + sfx); var m1 = F("middle_01" + sfx); var m2 = F("middle_02" + sfx);
                if (sfx == "_l") _midL = m1; else _midR = m1;
                if (i1 == null || p1 == null || m1 == null || m2 == null) continue;
                var fdir = (m2.position - m1.position).normalized;
                // normal da palma: o mesmo produto vetorial dá a palma numa mão e as costas na outra (espelho)
                var palm = Vector3.Cross(fdir, (p1.position - i1.position).normalized).normalized * (sfx == "_r" ? -1f : 1f);
                for (int f = 0; f < FingerNames.Length; f++)
                {
                    bool thumb = f == 4;
                    // índice um pouco mais solto, mínimo mais fechado (mão relaxada)
                    float extra = f == 0 ? -6f : f == 2 ? 4f : f == 3 ? 9f : 0f;
                    for (int k = 1; k <= 3; k++)
                    {
                        var b = F(FingerNames[f] + "_0" + k + sfx);
                        if (b == null || b.childCount == 0) continue;
                        var c = b.GetChild(0);
                        var dir = c.position - b.position;
                        if (dir.sqrMagnitude < 1e-8f) continue;
                        float ang = thumb ? (k == 1 ? 6f : 12f) : (k == 1 ? 18f : k == 2 ? 30f : 18f) + extra;
                        var target = Vector3.RotateTowards(dir.normalized, thumb ? (palm + fdir * 0.3f).normalized : palm, ang * Mathf.Deg2Rad, 0f);
                        b.rotation = Quaternion.FromToRotation(dir.normalized, target) * b.rotation;
                        list.Add(b);
                        rots.Add(b.localRotation);
                    }
                }
            }
            _fingers = list.ToArray();
            _fingerRot = rots.ToArray();
        }

        static Transform FindDeep(Transform t, string n)
        {
            if (t.name == n) return t;
            for (int i = 0; i < t.childCount; i++) { var r = FindDeep(t.GetChild(i), n); if (r != null) return r; }
            return null;
        }

        void ApplyFingers()
        {
            if (_fingers == null) return;
            for (int i = 0; i < _fingers.Length; i++) _fingers[i].localRotation = _fingerRot[i];
        }

        void OnDestroy()
        {
            anim?.Destroy();
            if (_shadow != null) Destroy(_shadow.gameObject);
        }

        static float Yaw(Vector2 f) => Mathf.Atan2(f.x, f.y) * Mathf.Rad2Deg;

        /// <summary>Chamado a cada passo da simulação.</summary>
        public void OnTick()
        {
            _prev = _cur;
            _cur = W(p.position);
            // velocidade de verdade (deslocamento por passo), suavizada: sem "correr parado"
            float real = (_cur - _prev).magnitude / Craquez.Sim.MatchSim.DT;
            if (real > 15f) real = 0f;   // teleporte (reposição, replay)
            _realSpd = Mathf.Lerp(_realSpd, real, 0.35f);
            // carrinho no gramado: rastro de grama arrancada a cada ~25 cm
            if (p.slide > 0f && view != null && view.Turf != null && p.id >= 0)
            {
                if (_slideMark == Vector3.zero) _slideMark = _cur;
                else if ((_cur - _slideMark).sqrMagnitude > 0.0625f) { view.Turf.Mark(_slideMark, _cur, 0.34f); _slideMark = _cur; }
            }
            else _slideMark = Vector3.zero;
        }

        Vector3 _slideMark;

        float _realSpd;

        public void Snap()
        {
            _prev = _cur = W(p.position);
            _yaw = Yaw(p.facing);
        }

        public void OnEvent(SimEvent e)
        {
            switch (e.type)
            {
                case "windup":
                {
                    if (view.sim.throwIn && view.sim.throwInTaker == p.id) break;   // lateral: sem gesto de chute
                    float rate = 1.25f;
                    string clip = e.kind == "shot" ? (p.windupPower > 0.6f ? "KickPower" : "Shot") : e.kind == "lob" ? "Kick" : "Pass";
                    // o pico do pé (contato) cai no instante em que a simulação solta a bola
                    float contact = AthleteAnimator.KickContact(clip);
                    float start = Mathf.Max(0f, contact - p.windupTotal * rate);
                    anim.Play(clip, start, (contact - start) + 0.5f, rate, 0.05f);
                    _kickBodyT = _kickBodyTotal = (contact - start) / rate + 0.45f;
                    _kickContactU = ((contact - start) / rate) / _kickBodyTotal;
                    _kickLoft = e.kind == "lob" || e.kind == "cross" ? 1f : 0f;
                    break;
                }
                case "kick":
                    if (e.kind == "throw") { _throwT = 0.32f; _oneArm = false; _throwLow = false; break; }   // lateral com as mãos
                    if (e.kind == "drop") break;   // goleiro soltou a bola no pé: sem gesto de chute
                    if (e.kind == "bicycle") { Bicycle(1.2f); break; }
                    if (e.kind == "gkthrow")
                    {
                        // goleiro: um braço só — por cima do ombro, ou rolando rente ao chão
                        _throwLow = view.sim.ball.z < 0.5f;
                        if (!_throwLow && AthleteAnimator.Has("GkThrow"))
                        {
                            // arremesso por cima do ombro (captura): a mão solta a bola no instante da saída
                            float tc = AthleteAnimator.KickContact("GkThrow");
                            anim.Play("GkThrow", Mathf.Max(0f, tc - 0.3f), 0.8f, 1.2f, 0.05f);
                            break;
                        }
                        if (_throwLow && AthleteAnimator.Has("GkRoll"))
                        {
                            // rolando a bola: abaixa e solta rente ao chão (captura)
                            float rc = AthleteAnimator.KickContact("GkRoll");
                            anim.Play("GkRoll", Mathf.Max(0f, rc - 0.45f), 0.9f, 1.15f, 0.05f);
                            break;
                        }
                        _throwT = 0.32f; _oneArm = true;
                        if (_throwLow) _pitch = 22f;
                        break;
                    }
                    if (e.kind == "punt")
                    {
                        // chutão de bate-pronto: chute forte começando um pouco antes do contato
                        string pk = AthleteAnimator.Has("GkPunt") ? "GkPunt" : "KickPower";
                        float pc = AthleteAnimator.KickContact(pk);
                        anim.Play(pk, Mathf.Max(0f, pc - 0.25f), 0.8f, 1.25f, 0.04f);
                        break;
                    }
                    if (anim.CurrentAction == null || anim.ActionWeight < 0.3f)
                    {
                        string c = e.kind == "shot" ? "Shot" : "Pass";
                        float ct = AthleteAnimator.KickContact(c);
                        anim.Play(c, ct - 0.06f, 0.6f, 1.35f, 0.04f);
                    }
                    break;
                case "bote":
                {
                    // bote: passo longo esticando a perna na bola
                    float cb = AthleteAnimator.KickContact("Pass");
                    anim.Play("Pass", cb - 0.1f, 0.55f, 1.25f, 0.04f);
                    break;
                }
                case "skill":
                    switch ((Skill)e.skill)
                    {
                        case Skill.Drag: anim.Play("Spin", 0.35f, 0.75f, 1.35f); break;
                        case Skill.Roulette: anim.Play("Spin", 0.2f, 0.95f, 1.2f); break;
                        case Skill.Roll: anim.Play(p.skillSide > 0 ? "CutL" : "CutR", 0.2f, 0.7f, 1.3f); break;
                        case Skill.Stepover: anim.Play(p.skillSide > 0 ? "FeintR" : "FeintL", 0.3f, 0.9f, 1.4f); break;
                        case Skill.Elastico: anim.Play(p.skillSide > 0 ? "CutR" : "CutL", 0.15f, 0.65f, 1.75f); break;
                        case Skill.Pedalada: anim.Play(p.skillSide > 0 ? "FeintR" : "FeintL", 0.2f, 0.9f, 1.9f); break;
                        case Skill.Corte: anim.Play(p.skillSide > 0 ? "CutL" : "CutR", 0.15f, 0.7f, 1.5f); break;
                        case Skill.Chapeu:
                        case Skill.Caneta:
                        {
                            float ctc = AthleteAnimator.KickContact("Pass");
                            anim.Play("Pass", ctc - 0.06f, 0.55f, 1.5f, 0.04f);
                            break;
                        }
                    }
                    break;
                case "bump":
                {
                    // e.id = quem perdeu o choque (este, se for ele), e.skill = quem ganhou
                    var sim = view.sim;
                    var other = e.skill >= 0 && e.skill < sim.players.Count ? sim.players[e.skill] : null;
                    if (other == null) break;
                    var away = W(p.position - other.position);
                    away.y = 0f;
                    if (away.sqrMagnitude < 1e-4f) break;
                    float k = Mathf.Clamp01(e.power / 7f);
                    _bumpVel += away.normalized * (e.id == p.id ? 35f + 40f * k : 14f);   // (antes ~17° de tranco: parecia que ia cair de costas)
                    break;
                }
                case "fooled":
                    anim.Play("QuickStop", 0.6f, 0.7f, 1.2f);
                    break;
                case "gkplace":
                    if (AthleteAnimator.Has("GkPlace"))
                    {
                        // tiro de meta: abaixa e coloca a bola no chão
                        float pc2 = AthleteAnimator.KickContact("GkPlace");
                        anim.Play("GkPlace", Mathf.Max(0f, pc2 - 0.55f), 1.15f, 1.1f, 0.1f);
                    }
                    break;
                case "header":
                    _hopT = 0.55f;
                    if (AthleteAnimator.Has("Header"))
                    {
                        // a cabeça vai na bola: o golpe de pescoço da captura cai no instante do toque
                        float hc = AthleteAnimator.KickContact("Header");
                        anim.Play("Header", Mathf.Max(0f, hc - 0.12f), 0.62f, 1.15f, 0.04f);
                    }
                    break;
                case "receive":
                {
                    // domínio: o pé mais perto da bola vai ao encontro dela e recua junto (amortece)
                    var bw = W(view.sim.ball.pos);
                    _recvLeg = rig.footL != null && rig.footR != null && (bw - rig.footL.position).sqrMagnitude < (bw - rig.footR.position).sqrMagnitude ? 0 : 1;
                    _recvT = e.ok ? 0.34f : 0.26f;
                    _recvTotal = _recvT;
                    break;
                }
                case "tackle_try":
                    _pitch = 18f;
                    break;
                case "slide":
                    _slideT = 0f;
                    // carrinho da captura (UAL): entra deslizando, segue no chão e levanta no fim
                    if (AthleteAnimator.Mocap && AthleteAnimator.Has("SlideIn") && anim != null)
                    {
                        anim.Play("SlideIn", 0.12f, -1f, 1.8f, 0.05f);
                        _slideClip = true;
                    }
                    break;
                case "dive":
                    _diveSide = Vector3.Dot(W(view.sim.ball.vel), transform.right) >= 0 ? -1f : 1f;
                    break;
                case "goal":
                    _celebrate = 4f;
                    break;
            }
        }

        /// <summary>(compatibilidade) a queda agora vem da simulação: SimPlayer.down.</summary>
        public void OnVictimFoul() { }

        // ------------------------------------------------------------ queda (falta)
        const float FallStumble = 0.16f, FallDrop = 0.42f, FallRise = 1.1f;   // (levantar: senta, apoia um joelho e fica de pé)
        float _fallW, _fallSide = 1f;
        int _fallPhase;   // 0 em pé, 1 tropeço, 2 caindo, 3 no chão, 4 levantando

        /// <summary>Fase e progresso da queda a partir do tempo no chão da simulação.</summary>
        float FallState(out int phase)
        {
            phase = 0;
            if (p.down <= 0f || p.downTotal <= 0f) return 0f;
            float t = p.downTotal - p.down;
            float rise = Mathf.Min(FallRise, p.downTotal * 0.35f);
            if (t < FallStumble) { phase = 1; return t / FallStumble; }
            if (t < FallStumble + FallDrop) { phase = 2; return (t - FallStumble) / FallDrop; }
            if (p.down > rise) { phase = 3; return (t - FallStumble - FallDrop) / Mathf.Max(0.01f, p.downTotal - rise - FallStumble - FallDrop); }
            phase = 4;
            return 1f - p.down / rise;
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>
        /// Corpo inteiro na queda: tropeça (tronco à frente/atrás), desaba girando sobre os pés,
        /// rola de lado no chão e levanta apoiando um joelho. Retorna (pitch, roll, lift).
        /// </summary>
        Vector3 FallBody(float pitch, float roll, float lift)
        {
            float u = FallState(out _fallPhase);
            if (_fallPhase == 0) return new Vector3(pitch, roll, lift);
            float fwd = p.fallKind == 0 ? -1f : 1f;   // pancada de frente cai de costas; por trás ou de lado, de frente
            if (_fallPhase == 1 && u < 0.05f) _fallSide = (p.id % 2 == 0) ? 1f : -1f;
            float lying = 86f * fwd;
            float sideRoll = p.fallKind == 2 ? 25f * _fallSide : 0f;
            switch (_fallPhase)
            {
                case 1:
                    pitch = Mathf.Lerp(pitch, 24f * fwd, Smooth(u));
                    roll += sideRoll * u * 0.5f;
                    break;
                case 2:
                {
                    // queda acelerada (gravidade) com pequeno quique no impacto; no carrinho, voa por cima
                    // da perna (o corpo sobe um pouco antes de cair de frente)
                    float k = u * u;
                    pitch = Mathf.Lerp(24f * fwd, lying, k);
                    roll += sideRoll * (0.5f + 0.5f * k);
                    // (0,16 deixava o tronco meio enterrado no piso: costas e ombros têm ~0,12 m de espessura)
                    lift = Mathf.Lerp(0f, 0.21f, k) + (u > 0.85f ? Mathf.Sin((u - 0.85f) / 0.15f * Mathf.PI) * 0.05f : 0f);
                    if (p.fallTrip) lift += Mathf.Sin(Mathf.Clamp01(u * 1.3f) * Mathf.PI) * 0.35f;
                    break;
                }
                case 3:
                {
                    // no chão: vira de lado segurando a perna e se contorce
                    float turn = p.fallLight ? 0f : Smooth(u * 3f);   // queda leve: sem virar de lado se contorcendo
                    float writhe = p.fallLight ? 0f : Mathf.Sin(Time.time * 5.5f + p.id) * 6f * (1f - u * 0.6f);
                    pitch = lying;
                    roll += _fallSide * Mathf.Lerp(sideRoll * _fallSide, 62f, turn) + writhe;
                    // às vezes rola no chão (uma volta inteira) antes de ficar sentindo
                    if (p.fallRoll) roll += _fallSide * 360f * Smooth(Mathf.Clamp01(u / 0.3f));
                    lift = 0.21f + turn * 0.07f;
                    break;
                }
                case 4:
                {
                    // levanta: volta de bruços, apoia um joelho e fica de pé
                    float k = Smooth(u);
                    pitch = Mathf.Lerp(lying, 0f, k);
                    roll += _fallSide * Mathf.Lerp(62f, 0f, Smooth(u * 2f));
                    lift = Mathf.Lerp(0.27f, 0f, k);
                    break;
                }
            }
            return new Vector3(pitch, roll, lift);
        }

        /// <summary>Pernas e braços na queda (por cima da animação).</summary>
        void FallLimbs()
        {
            if (_bikeT > 0f) return;   // (na bicicleta as pernas são dela)
            if (_getUp) { _fallW = 0f; return; }   // (levantando com a captura)
            if (_fallPhase == 0) { _fallW = Mathf.MoveTowards(_fallW, 0f, Time.deltaTime * 4f); if (_fallW <= 0f) return; }
            else _fallW = Mathf.MoveTowards(_fallW, 1f, Time.deltaTime * 8f);
            float u = FallState(out int ph);
            var right = transform.right;
            float fwd = p.fallKind == 0 ? -1f : 1f;
            // pernas: joelhos dobram no tropeço, encolhem no chão (perna "machucada") e apoiam ao levantar
            float hurtThigh = 0f, hurtCalf = 0f, otherThigh = 0f, otherCalf = 0f;
            switch (ph)
            {
                case 1: otherThigh = hurtThigh = -18f * u; otherCalf = hurtCalf = 30f * u; break;
                case 2: hurtThigh = -25f; hurtCalf = 45f; otherThigh = -10f; otherCalf = 25f; break;
                case 3:
                {
                    float k = Smooth(u * 3f);
                    float pump = Mathf.Sin(Time.time * 4f + p.id) * 10f;
                    hurtThigh = Mathf.Lerp(-25f, -75f, k) + pump; hurtCalf = Mathf.Lerp(45f, 110f, k);
                    otherThigh = Mathf.Lerp(-10f, -30f, k); otherCalf = Mathf.Lerp(25f, 50f, k);
                    break;
                }
                case 4:
                {
                    float kneel = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
                    hurtThigh = -85f * kneel; hurtCalf = 115f * kneel;
                    otherThigh = -40f * kneel; otherCalf = 30f * kneel;
                    break;
                }
            }
            string hurt = _fallSide > 0 ? "_r" : "_l", other = _fallSide > 0 ? "_l" : "_r";
            void Leg(string sfx, float thigh, float calf)
            {
                rig.bones.TryGetValue("thigh" + sfx, out var th);
                rig.bones.TryGetValue("calf" + sfx, out var cf);
                _pose.Rotate(th, Quaternion.AngleAxis(thigh * _fallW, right));
                _pose.Rotate(cf, Quaternion.AngleAxis(calf * _fallW, right));
            }
            Leg(hurt, hurtThigh, hurtCalf);
            Leg(other, otherThigh, otherCalf);
            // tronco curva um pouco no chão (encolhido)
            if (ph == 3 && rig.spine != null) _pose.Rotate(rig.spine, Quaternion.AngleAxis(18f * _fallW, right));
            // alguém veio dar a mão (companheiro, adversário ou o juiz): no fim do tempo no chão e ao levantar,
            // a mão do lado dele sobe até a mão de quem ajuda e o corpo é puxado junto
            var helper = Helper();
            Transform helperHand = null;
            if (helper != null && (ph == 4 || (ph == 3 && u > 0.55f)) && helper.rig != null)
                helper.rig.bones.TryGetValue("hand_r", out helperHand);
            // braços: amortecem a queda (à frente ou atrás) e, no chão, uma mão vai à perna
            Transform knee = null;
            rig.bones.TryGetValue("calf" + hurt, out knee);
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "_l" : "_r";
                rig.bones.TryGetValue("upperarm" + sfx, out var up);
                rig.bones.TryGetValue("lowerarm" + sfx, out var lo);
                rig.bones.TryGetValue("hand" + sfx, out var hd);
                if (up == null || lo == null || hd == null) continue;
                float s = Mathf.Sign(Vector3.Dot(up.position - (rig.spine != null ? rig.spine.position : transform.position), right));
                Vector3 dir;
                var down = (-transform.up + right * s * 0.12f).normalized;   // braço solto ao lado do corpo
                if (ph == 0) dir = down;   // já de pé: os braços descem (antes ficavam erguidos enquanto a pose se desfazia)
                else if (ph == 3 && knee != null && sfx == hurt && !p.fallLight) dir = (knee.position - up.position).normalized;   // segura a perna
                else if (ph == 3) dir = (transform.up * 0.3f + right * s * 0.8f - transform.forward * 0.4f * fwd).normalized;   // braço apoiado no chão
                else if (ph == 4)
                {
                    // levantando: empurra o chão com as mãos e depois os braços voltam para baixo
                    var push = (-Vector3.up * 0.8f + right * s * 0.4f + transform.forward * 0.2f).normalized;
                    dir = Vector3.Slerp(push, down, Smooth(u * 1.6f));
                }
                else if (fwd > 0f) dir = (transform.forward * 0.9f + transform.up * 0.35f + right * s * 0.35f).normalized;  // mãos à frente
                else dir = (-transform.forward * 0.7f - transform.up * 0.4f + right * s * 0.6f).normalized;             // mãos atrás
                if (helperHand != null)
                {
                    // o braço do lado de quem ajuda vai na mão dele (o outro apoia no chão)
                    float hs = Mathf.Sign(Vector3.Dot(helperHand.position - (rig.spine != null ? rig.spine.position : transform.position), right));
                    if (hs == s) dir = (helperHand.position - up.position).normalized;
                }
                var cur = lo.position - up.position;
                _pose.Rotate(up, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, dir), _fallW * 0.85f));
                var cur2 = hd.position - lo.position;
                _pose.Rotate(lo, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur2, dir), _fallW * 0.6f));
            }
        }

        /// <summary>Quem está dando a mão para este atleta levantar (perto), se houver.</summary>
        AthleteView Helper()
        {
            var r = view.Referee;
            if (r != null && r.p != null && r.p.helping == p.id && r.p.position.Dist(p.position) < 1.6f) return r;
            foreach (var a in view.athletes)
                if (a != null && a != this && a.p != null && a.p.helping == p.id && a.p.position.Dist(p.position) < 1.6f) return a;
            return null;
        }

        /// <summary>Árbitro: direção para onde aponta (zero = braços normais).</summary>
        public Vector3 point;
        float _pointW;

        void PointArm()
        {
            _pointW = Mathf.MoveTowards(_pointW, point != Vector3.zero ? 1f : 0f, Time.deltaTime * 4f);
            if (_pointW <= 0.001f) return;
            rig.bones.TryGetValue("upperarm_r", out var up);
            rig.bones.TryGetValue("lowerarm_r", out var lo);
            rig.bones.TryGetValue("hand_r", out var hd);
            if (up == null || lo == null || hd == null) return;
            var dir = (point.normalized * 0.9f + Vector3.up * 0.35f).normalized;
            _pose.Rotate(up, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(lo.position - up.position, dir), _pointW));
            _pose.Rotate(lo, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(hd.position - lo.position, dir), _pointW));
        }

        float _protest, _help;
        bool _slideClip, _getUp;
        Vector3 _bump, _bumpVel;   // inclinação do choque (graus, no plano do chão: x/z do mundo)

        /// <summary>Ombro a ombro: correndo lado a lado com um adversário, inclina contra ele (graus, mundo).</summary>
        Vector3 ShoulderDuel()
        {
            var sim = view.sim;
            float v = p.vel.magnitude;
            if (v < 3f || p.down > 0f || p.slide > 0f || p.isKeeper) return Vector3.zero;
            SimPlayer best = null; float bd = 0.95f;
            foreach (var o in sim.players)
            {
                if (o.team == p.team || o.sentOff || o.isKeeper) continue;
                float d = o.position.Dist(p.position);
                if (d >= bd || o.vel.magnitude < 3f || Vector2.Dot(o.vel / o.vel.magnitude, p.vel / v) < 0.7f) continue;
                bd = d; best = o;
            }
            if (best == null) return Vector3.zero;
            var to = W(best.position - p.position); to.y = 0f;
            float k = Mathf.Clamp01((0.95f - bd) / 0.35f);
            return to.normalized * 7f * k;
        }

        /// <summary>Dando a mão para quem caiu: inclina o tronco e estende o braço até a mão dele.</summary>
        void HelpUp()
        {
            var sim = view.sim;
            SimPlayer v = p.helping >= 0 && p.helping < sim.players.Count ? sim.players[p.helping] : null;
            bool want = v != null && v.down > 0f && p.position.Dist(v.position) < 1.3f;
            _help = Mathf.MoveTowards(_help, want ? 1f : 0f, Time.deltaTime * 2.5f);
            if (_help <= 0.001f || v == null) return;
            rig.bones.TryGetValue("upperarm_r", out var up);
            rig.bones.TryGetValue("lowerarm_r", out var lo);
            rig.bones.TryGetValue("hand_r", out var hd);
            rig.bones.TryGetValue("spine_01", out var sp1);
            if (up == null || lo == null || hd == null) return;
            // a mão vai até a altura da mão de quem está no chão e sobe junto quando ele levanta
            float rise = v.downTotal > 0f ? Mathf.Clamp01(1f - v.down / Mathf.Min(0.9f, v.downTotal * 0.35f)) : 0f;
            // (mira a mão de quem está no chão, que sobe na direção dela)
            var target = W(v.position) + Vector3.up * Mathf.Lerp(0.5f, 1.05f, rise) + (transform.position - W(v.position)).normalized * 0.25f;
            if (sp1 != null) _pose.Rotate(sp1, Quaternion.AngleAxis(Mathf.Lerp(28f, 8f, rise) * _help, transform.right));
            var dir = (target - up.position).normalized;
            _pose.Rotate(up, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(lo.position - up.position, dir), _help));
            _pose.Rotate(lo, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(hd.position - lo.position, dir), _help));
        }

        /// <summary>Quem fez a falta reclama: braços abertos, palmas para cima.</summary>
        void Protest()
        {
            var sim = view.sim;
            bool want = sim.state == MatchState.Foul && sim.foulOffender == p.id && p.down <= 0f && p.slide <= 0f;
            _protest = Mathf.MoveTowards(_protest, want ? 1f : 0f, Time.deltaTime * 3f);
            if (AthleteAnimator.Mocap && AthleteAnimator.Has("Protest") && anim != null)
            {
                // balança a cabeça e abre os braços ("não fiz nada!"), da captura
                if (want && anim.CurrentAction == null && p.vel.magnitude < 2.5f) anim.Play("Protest", (p.id * 0.37f) % 1.2f, 2.2f, 1f, 0.25f);
                else if (!want && anim.CurrentAction == "Protest") anim.Stop(0.3f);
                return;
            }
            if (_protest <= 0.001f) return;
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "_l" : "_r";
                rig.bones.TryGetValue("upperarm" + sfx, out var up);
                rig.bones.TryGetValue("lowerarm" + sfx, out var lo);
                if (up == null || lo == null) continue;
                float s = Mathf.Sign(Vector3.Dot(up.position - (rig.spine != null ? rig.spine.position : transform.position), transform.right));
                var dir = (transform.right * s * 0.9f + transform.forward * 0.5f + Vector3.up * (0.1f + 0.15f * Mathf.Sin(Time.time * 6f))).normalized;
                _pose.Rotate(up, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(lo.position - up.position, dir), _protest * 0.8f));
            }
        }

        /// <summary>Comemoração: pulinhos e braços para cima por alguns segundos.</summary>
        float _bikeT, _bikeTotal;
        /// <summary>Bicicleta: o corpo gira para trás no ar em torno do quadril, a perna do chute passa por
        /// cima da cabeça, a outra encolhe, os braços abrem; cai de costas.</summary>
        /// <summary>Goleiro organizando a defesa (falta/escanteio contra): aponta e grita com a barreira.</summary>
        public void Directing()
        {
            if (anim == null || !p.isKeeper || !AthleteAnimator.Has("GkDirect") || anim.CurrentAction != null) return;
            anim.Play("GkDirect", (p.id * 1.3f) % 3f, 2.6f, 1f, 0.25f);
        }

        /// <summary>Técnico/reserva parado: braços cruzados (captura) enquanto on; solta quando off.</summary>
        public void ArmsFolded(bool on)
        {
            if (anim == null || !AthleteAnimator.Has("ArmsFolded")) return;
            if (on && anim.CurrentAction == null) anim.Play("ArmsFolded", (p.number * 0.9f) % 2f, -1f, 1f, 0.4f);
            else if (!on && anim.CurrentAction == "ArmsFolded") anim.Stop(0.35f);
        }

        public void Bicycle(float seconds) { _bikeT = _bikeTotal = seconds; }
        public bool InBicycle => _bikeT > 0f;
        /// <summary>Fase da bicicleta 0..1 (o contato com a bola é em ~0,35).</summary>
        float BikeU => _bikeTotal > 0f ? 1f - _bikeT / _bikeTotal : 1f;

        /// <summary>Ângulo do corpo (para trás) e altura do quadril na bicicleta.</summary>
        Vector2 BikeBody(float u)
        {
            // começa já no ar (o contato acontece logo): sobe girando, passa da horizontal, cai de costas
            float pitch = u < 0.35f ? Mathf.Lerp(-35f, -150f, Mathf.SmoothStep(0f, 1f, u / 0.35f))
                : u < 0.6f ? Mathf.Lerp(-150f, -120f, (u - 0.35f) / 0.25f)
                : Mathf.Lerp(-120f, -88f, Mathf.SmoothStep(0f, 1f, (u - 0.6f) / 0.4f));
            float hip = u < 0.35f ? Mathf.Lerp(1.1f, 1.5f, Mathf.Sin(u / 0.35f * Mathf.PI * 0.5f))
                : u < 0.75f ? Mathf.Lerp(1.5f, 0.35f, Mathf.SmoothStep(0f, 1f, (u - 0.35f) / 0.4f)) : 0.3f;
            return new Vector2(pitch, hip);
        }

        void BicycleLimbs()
        {
            if (_bikeT <= 0f || rig == null) return;
            float u = BikeU;
            float w = Mathf.Clamp01(Mathf.Min(u / 0.08f, (1f - u) / 0.2f));
            var right = transform.right;
            var fwd = transform.forward;
            // perna do chute (direita) sobe por cima da cabeça e estica; a de apoio dobra para baixo
            float kick = u < 0.35f ? Mathf.Lerp(-40f, -125f, u / 0.35f) : Mathf.Lerp(-125f, -60f, Mathf.Clamp01((u - 0.35f) / 0.4f));
            rig.bones.TryGetValue("thigh_r", out var thR); rig.bones.TryGetValue("calf_r", out var cfR);
            rig.bones.TryGetValue("thigh_l", out var thL); rig.bones.TryGetValue("calf_l", out var cfL);
            _pose.Rotate(thR, Quaternion.AngleAxis(kick * w, right));
            _pose.Rotate(cfR, Quaternion.AngleAxis(Mathf.Lerp(40f, 5f, Mathf.Clamp01(u / 0.33f)) * w, right));
            _pose.Rotate(thL, Quaternion.AngleAxis(-35f * w, right));
            _pose.Rotate(cfL, Quaternion.AngleAxis(80f * w, right));
            // pés esticados na linha da canela (peito do pé na bola), sem torcer para os lados
            foreach (var pair in new[] { (cfR, rig.footR), (cfL, rig.footL) })
            {
                var cf = pair.Item1; var ft = pair.Item2;
                if (cf == null || ft == null || ft.childCount == 0) continue;
                var toe = ft.GetChild(0);
                var shin = (ft.position - cf.position).normalized;
                var footDir = toe.position - ft.position;
                var want = Vector3.Slerp(footDir.normalized, shin, 0.75f);
                _pose.Rotate(ft, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(footDir, want), w));
            }
            // braços abertos para os lados (equilíbrio e amortecer a queda)
            foreach (var sfx in new[] { "_l", "_r" })
            {
                rig.bones.TryGetValue("upperarm" + sfx, out var ua);
                if (ua == null) continue;
                float sd = Mathf.Sign(Vector3.Dot(ua.position - (rig.spine != null ? rig.spine.position : transform.position), right));
                _pose.Rotate(ua, Quaternion.AngleAxis(-sd * 65f * w, fwd));
            }
        }

        bool _pullSet;
        /// <summary>Puxão de camisa: o braço vai até as costas de quem está sendo puxado.</summary>
        void PullArm()
        {
            var sim = view.sim;
            if (p.pulling >= 0 && p.pulling < sim.players.Count)
            {
                var o = sim.players[p.pulling];
                var dir = W(o.position - p.position);
                point = (dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward) * 0.8f + Vector3.up * 0.15f;
                _pullSet = true;
            }
            else if (_pullSet) { point = Vector3.zero; _pullSet = false; }
        }

        public void Celebrate(float seconds) => _celebrate = Mathf.Max(_celebrate, seconds);
        public bool Celebrating => _celebrate > 0f;

        void Update()
        {
            if (p == null) return;
            float a = view != null ? view.Alpha : 1f;
            float dt = Time.deltaTime;
            var pos = Vector3.Lerp(_prev, _cur, a);
            // rumo suave (o corpo gira um pouco atrás da direção da simulação)
            float target = Yaw(p.facing);
            // giro com velocidade angular limitada (mais lento em alta velocidade, como um atleta real)
            float spd = p.vel.magnitude;
            float diff = Mathf.DeltaAngle(_yaw, target);
            // giro do corpo limitado como num atleta de verdade (antes até 900°/s: o corpo "rodava" na hora)
            float maxRate = Mathf.Lerp(480f, 300f, Mathf.Clamp01(spd / 8f));
            float step = Mathf.Clamp(diff * (1f - Mathf.Exp(-dt * 14f)), -maxRate * dt, maxRate * dt);
            _yaw += step;
            _yawRate = Mathf.Lerp(_yawRate, step / Mathf.Max(1e-4f, dt), 1f - Mathf.Exp(-dt * 8f));
            // inclinação nas curvas e na aceleração
            var acc = (p.vel - _lastVel) / Mathf.Max(1e-4f, dt);
            acc2D = acc;
            _lastVel = p.vel;
            var local = Quaternion.Euler(0, -_yaw, 0) * W(acc);
            // inclinação para dentro da curva (força centrípeta) + aceleração lateral
            float centri = -_yawRate * Mathf.Deg2Rad * spd * 1.6f;
            _lean = Mathf.Lerp(_lean, Mathf.Clamp(-local.x * 0.3f + centri * 0.6f, -7f, 7f), 1f - Mathf.Exp(-dt * 10f));   // corpo firme: inclina pouco e sem atraso
            _pitch = Mathf.Lerp(_pitch, 0f, 1f - Mathf.Exp(-dt * 5f));
            // aceleração inclina o tronco (na coluna, em LateUpdate), não o corpo inteiro: pés firmes
            // tronco: inclina na arrancada e, em velocidade, fica um pouco à frente (como corredor de verdade)
            _accLean = Mathf.Lerp(_accLean, Mathf.Clamp(local.z * 0.5f, -3f, 4f) + (AthleteAnimator.Mocap ? 0f : Mathf.Clamp01((spd - 3f) / 5f) * 4f), 1f - Mathf.Exp(-dt * 9f));
            float lift = 0f, roll = _lean, pitch = _pitch;
            if (_celebrate > 0f && p.vel.magnitude < 3f)
            {
                // pulos em séries (pula, pula, respira...)
                float burst = Mathf.Sin(_celebrate * 1.6f);
                if (burst > 0f) lift += Mathf.Abs(Mathf.Sin(_celebrate * 7f)) * 0.2f * Mathf.Clamp01(_celebrate) * Mathf.Clamp01(burst * 3f);
            }
            if (_hopT > 0f)
            {
                _hopT -= dt;
                float u = 1f - _hopT / 0.55f;
                lift += Mathf.Sin(u * Mathf.PI) * 0.45f;
            }
            if (p.slide > 0f)
            {
                _slideT += dt;
                float k = Mathf.Clamp01(_slideT / 0.12f);
                if (!_slideClip)
                {
                    pitch = Mathf.Lerp(pitch, -62f, k);
                    lift = Mathf.Lerp(lift, 0.12f, k);
                }
            }
            else if (p.isKeeper && (p.saveKind > 0 || _saveRec > 0f))
            {
                var sp = SavePose(dt);
                pitch += sp.x; roll += sp.y; lift += sp.z;
                if (_saveKindV >= 2 && _saveKindV <= 5 && !_saveClip)
                {
                    // o corpo gira em torno do quadril, não dos pés: sem isto o desenho "voava" ~0,7 m além
                    // de onde estão as mãos na simulação (o goleiro parecia passar por cima da bola)
                    var rightYaw = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
                    pos -= rightYaw * (_saveSideV * 0.95f * Mathf.Sin(Mathf.Abs(sp.y) * Mathf.Deg2Rad) * 0.5f);
                }
            }
            else if (p.down > 0f)
            {
                var fb = FallBody(pitch, roll, lift);
                pitch = fb.x; roll = fb.y; lift = fb.z;
                if (_fallPhase == 4 && p.fallKind == 0 && !p.fallRoll && AthleteAnimator.Mocap && AthleteAnimator.Has("GetUp") && anim != null)
                {
                    // de costas no chão: senta, apoia a mão e levanta (captura); o corpo deixa de girar
                    if (!_getUp)
                    {
                        _getUp = true;
                        float rise = Mathf.Min(FallRise, p.downTotal * 0.35f);
                        float len = AthleteAnimator.ClipLength("GetUp");
                        anim.Play("GetUp", 0.15f, -1f, Mathf.Clamp((len - 0.15f) / Mathf.Max(0.3f, p.down), 0.8f, 2.2f), 0.12f);
                    }
                    float w = anim.CurrentAction == "GetUp" ? anim.ActionWeight : 0f;
                    pitch = Mathf.Lerp(pitch, 0f, w); roll = Mathf.Lerp(roll, _lean, w); lift = Mathf.Lerp(lift, 0f, w);
                }
            }
            if (p.down <= 0f) _getUp = false;
            if (_bikeT > 0f)
            {
                _bikeT -= Time.deltaTime;
                var bb = BikeBody(BikeU);
                pitch = bb.x; roll = 0f;
                // gira em torno do quadril (0,95 m), não dos pés: o quadril fica na altura da bicicleta
                var hipL = Vector3.up * 0.95f;
                var rot = Quaternion.Euler(pitch, _yaw, 0f);
                var hipW = rot * hipL;
                pos += new Vector3(-hipW.x, 0f, -hipW.z);
                lift = bb.y - hipW.y;
            }
            // trombada: o corpo é jogado para longe do contato e volta (mola amortecida); no ombro a
            // ombro em velocidade, os dois inclinam um contra o outro
            {
                float dtB = Mathf.Min(Time.deltaTime, 0.05f);
                _bumpVel += (-_bump * 90f - _bumpVel * 12f) * dtB;
                _bump += _bumpVel * dtB;
                var shoulder = ShoulderDuel();
                var lr = Quaternion.Euler(0f, -_yaw, 0f) * (_bump + shoulder);   // para o referencial do corpo
                roll += -lr.x;
                pitch += lr.z;
            }
            transform.position = pos + Vector3.up * lift;
            transform.rotation = Quaternion.Euler(pitch, _yaw, roll);
            // animação
            var v3 = W(p.vel);
            var lv = Quaternion.Euler(0, -_yaw, 0) * v3;
            // postura de marcação (agachado) só colado no adversário com a bola e de frente para ele
            bool guard = false;
            if (p.shielding && !p.isKeeper && view.sim.ball.owner >= 0)
            {
                var ow = view.sim.players[view.sim.ball.owner];
                var toOw = ow.position - p.position;
                guard = ow.team != p.team && toOw.magnitude < 2.6f && Vector2.Dot(toOw.normalized, p.facing) > 0.5f;
            }
            // goleiro em posição de espera (joelhos flexionados, peso na ponta dos pés) quando o
            // adversário vem com a bola, e deslocando de lado acompanhando o lance
            if (p.isKeeper && !p.InHands && p.dive <= 0f && view.sim.ball.owner >= 0)
            {
                var ow = view.sim.players[view.sim.ball.owner];
                if (ow.team != p.team && ow.position.Dist(p.position) < (view.sim.mode.id == GameModes.Field ? 24f : 12f)) guard = true;
            }
            // velocidade da passada suavizada, com arrancada/freada de gente (sem trocar de
            // passada a cada passo da simulação nem "pular" de andar para correr)
            float rawSpd = Mathf.Min(p.vel.magnitude, _realSpd + 0.35f);
            float accLim = rawSpd > _animSpd ? 11f : 16f;
            _animSpd = Mathf.MoveTowards(_animSpd, rawSpd, accLim * dt);
            _animSpd = Mathf.Lerp(_animSpd, rawSpd, 1f - Mathf.Exp(-dt * 3f));
            float animSpeed = _animSpd, animFwd = rawSpd > 0.01f ? lv.z * (_animSpd / rawSpd) : 0f;
            // girando parado: passinhos no lugar em vez de "patinar" sobre os pés
            float turnStep = Mathf.Clamp(Mathf.Abs(_yawRate) * 0.0065f, 0f, 1.3f);
            if (animSpeed < 1.2f && turnStep > 0.35f && !guard)
            {
                animSpeed = Mathf.Max(animSpeed, turnStep);
                animFwd = Mathf.Max(animFwd, animSpeed * 0.8f);
            }
            if (p.down > 0f) { animSpeed = 0f; animFwd = 0f; guard = false; }   // caído: sem passada
            if (p.isKeeper && (p.saveKind >= 3 || (_saveRec > 0f && _saveKindV >= 3))) { animSpeed = 0f; animFwd = 0f; guard = false; }   // no voo: sem passada
            if (p.isKeeper && p.saveKind > 0 && p.saveKind < 3) guard = true;
            Cuts(dt, spd);
            Touches();
            _pose.Restore();   // tira as correções do quadro anterior antes de avaliar a animação
            if (anim != null)
            {
                InitStyle();
                bool hasBall = view.sim.ball.owner == p.id;
                bool braking = Vector2.Dot(acc2D, p.vel) < -6f * Mathf.Max(1f, rawSpd);
                float cad = _cadence * (hasBall ? 1.12f : 1f) * (braking ? 1.1f : 1f);
                anim.cadence = Mathf.Lerp(anim.cadence, cad, 1f - Mathf.Exp(-dt * 4f));
                // parado esperando (bola parada, reinício): mãos na cintura / nos joelhos de vez em quando
                bool idle = rawSpd < 0.3f && !hasBall && p.down <= 0f && !p.isKeeper && _celebrate <= 0f;
                _idleT = idle ? _idleT + dt : 0f;
            }
            anim?.Tick(dt, animSpeed, animFwd, rawSpd > 0.01f ? lv.x * (_animSpd / rawSpd) : 0f, guard, p.isKeeper);
            if (_slideClip && anim != null)
            {
                if (p.down > 0f) _slideClip = false;   // (caiu: a queda assume)
                else if (p.slide > 0f) { if (anim.CurrentAction != "SlideIn" && anim.CurrentAction != "SlideLoop") anim.Play("SlideLoop", 0f, -1f, 1f, 0.1f); }
                else { anim.Play("SlideOut", 0f, -1f, 1.25f, 0.1f); _slideClip = false; }
            }
            if (_saveClip && anim != null)
            {
                // fim do clipe da defesa, ou o goleiro já de pé e correndo: volta para a passada
                if (anim.CurrentAction != _saveClipName) _saveClip = false;
                else if (p.saveKind == 0 && _saveRec <= 0f && p.vel.magnitude > 1.6f) { anim.Stop(0.25f); _saveClip = false; }
            }
            if (!_diag && rig.head != null && p.id == 3)
            {
                _diag = true;
                Debug.Log($"[diag] atleta {p.name}: cabeça a {rig.head.position.y - transform.position.y:F2} m do chão (raiz), pés {rig.footL?.position.y - transform.position.y:F2} m; rotação do modelo {rig.model.localEulerAngles}");
            }
            if (_celebrate > 0f) _celebrate -= dt;
            // a comemoração acaba quando o jogo volta (saída de bola/jogo rolando): ninguém fica de braços erguidos
            var stt = view.sim.state;
            if (p.id >= 0 && (stt == Craquez.Sim.MatchState.Kickoff || stt == Craquez.Sim.MatchState.Playing || stt == Craquez.Sim.MatchState.Restart || stt == Craquez.Sim.MatchState.Foul))   // (banco e aquecimento: livres)
            {
                _celebrate = 0f;
                _armsUp = Mathf.MoveTowards(_armsUp, 0f, dt * 4f);
            }
        }

        // ------------------------------------------------------------ defesas do goleiro
        int _saveKindV;
        float _saveRec, _saveSideV, _saveU, _saveRecT = 0.45f;
        bool _crossHand;
        bool _saveHeld;

        bool _saveClip;
        string _saveClipName;
        int _saveClipTick = -999;

        /// <summary>
        /// Defesa com a captura do Mixamo: mergulho para o lado certo, bloqueio com o corpo, pegada baixa,
        /// no peito ou com salto. O clipe é tocado de modo que o instante em que as mãos chegam (medido na
        /// conversão) caia junto com o contato previsto pela simulação; o deslocamento vem da simulação.
        /// </summary>
        void StartSaveClip()
        {
            if (!AthleteAnimator.Mocap || anim == null) return;
            string clip = null;
            float rate = 1.15f;
            switch (p.saveKind)
            {
                case 3: case 4: case 5: clip = _saveSideV > 0f ? "GkDiveR" : "GkDiveL"; rate = 1.25f; break;
                case 2: clip = _saveSideV > 0f ? "GkBlockR" : "GkBlockL"; rate = 1.3f; break;
                case 1: clip = p.saveH < 0.5f ? "GkCatchLow" : "GkCatch"; break;
                case 6: clip = "GkCatchHigh"; rate = 1.2f; break;
            }
            if (clip == null || !AthleteAnimator.Has(clip)) { _saveClip = false; return; }
            // a mesma defesa recalculada logo em seguida: não recomeça o clipe
            if (_saveClip && _saveClipName == clip && view.sim.tick - _saveClipTick < 30) return;
            float simCT = Mathf.Clamp(p.saveContact, 0.12f, 0.75f) * p.saveTotal;   // s até o contato
            float cc = AthleteAnimator.KickContact(clip);
            float start = Mathf.Max(0f, cc - simCT * rate);
            anim.Play(clip, start, -1f, rate, 0.05f);
            _saveClip = true;
            _saveClipName = clip;
            _saveClipTick = view.sim.tick;
        }

        /// <summary>
        /// Corpo na defesa (pitch, roll, lift): ponte = impulso de lado, voo com o corpo esticado e
        /// queda deitado de lado; encaixe = corpo atrás da bola; baixa = ajoelha sobre a bola;
        /// salto = sobe com os braços. Depois levanta (_saveRec).
        /// </summary>
        Vector3 SavePose(float dt)
        {
            if (p.saveKind > 0)
            {
                float simU = 1f - p.saveT / Mathf.Max(0.01f, p.saveTotal);
                bool fresh = _saveRec <= 0f || p.saveKind != _saveKindV || simU + 0.05f < _saveU;
                _saveKindV = p.saveKind;
                if (fresh)
                {
                    // lado do mergulho no corpo (+ = direita do goleiro): decidido no começo da defesa e
                    // mantido até o fim (antes era recalculado a cada quadro e o corpo "trocava" de lado)
                    var sideW = new Vector3(0, 0, p.saveSide);
                    _saveSideV = Vector3.Dot(sideW, transform.right) >= 0f ? 1f : -1f;
                    _saveU = simU;
                    // espalmada de mão trocada: no mergulho alto, às vezes só a mão de cima cruza e tira a bola
                    _crossHand = p.saveKind == 5 && ((p.id * 7 + view.sim.tick) % 10) < 4;
                    StartSaveClip();
                }
                // progresso contínuo entre os passos da simulação (sem "quadros")
                else _saveU = Mathf.Clamp(Mathf.Max(_saveU + dt / Mathf.Max(0.05f, p.saveTotal), simU - 0.02f), 0f, Mathf.Min(1f, simU + 0.04f));
                // levantar do chão leva mais tempo que voltar de uma defesa de pé (antes 0,45 s para tudo:
                // o goleiro "saltava" de deitado para de pé)
                _saveRecT = _saveKindV >= 2 && _saveKindV <= 5 ? 0.85f : 0.4f;
                _saveRec = _saveRecT;
                _saveHeld = p.InHands;
            }
            else
            {
                _saveRec = Mathf.Max(0f, _saveRec - dt);
            }
            float u = _saveU;
            float recLin = p.saveKind > 0 ? 1f : _saveRec / _saveRecT;
            float rec = Smooth(recLin);   // 1 = em defesa .. 0 = de pé de novo (com aceleração e freada)
            float pitch = 0f, roll = 0f, lift = 0f, extraPitch = 0f;
            float cu = Mathf.Clamp(p.saveContact, 0.12f, 0.75f);
            switch (_saveKindV)
            {
                case 3: case 4: case 5:
                {
                    // 1) impulso: o goleiro abaixa e transfere o peso para a perna do lado do mergulho;
                    // 2) voo com o corpo esticado JUNTO com a chegada da bola (cu = instante do contato);
                    // 3) queda de lado, com um pequeno quique, e o peito virando para o chão (protege a bola)
                    float peak = _saveKindV == 3 ? 0.22f : _saveKindV == 4 ? 0.55f : 0.95f;
                    float lay = _saveKindV == 3 ? 88f : _saveKindV == 4 ? 80f : 70f;
                    float load = Mathf.Sin(Mathf.Clamp01(u / (cu * 0.35f)) * Mathf.PI) * (u < cu * 0.35f ? 1f : 0f);
                    float k = Smooth((u - cu * 0.12f) / (cu * 0.8f));
                    roll = -_saveSideV * (lay * k + 6f * load);
                    float land = cu * 2f;
                    float fly = u < land ? Mathf.Sin(Mathf.Clamp01(u / land) * Mathf.PI) * peak : 0f;
                    // quique na queda (~0,15 s) — o corpo bate e assenta
                    float du = (u - land) * p.saveTotal;
                    float bounce = du > 0f && du < 0.22f ? Mathf.Sin(du / 0.22f * Mathf.PI) * 0.05f * (peak > 0.3f ? 1f : 0.5f) : 0f;
                    lift = fly + bounce + 0.28f * k - 0.07f * load;
                    // no ar: peito para a bola; no chão: vira um pouco para baixo, sobre os braços
                    float ground = Smooth((u - land) / 0.25f);
                    pitch = Mathf.Lerp(-8f, _saveHeld ? 20f : 10f, ground) * k;
                    break;
                }
                case 2:
                {
                    // baixa / abafa: desce de lado sobre a trajetória da bola (corpo atravessado, como o
                    // goleiro de verdade fecha o chute rasteiro e o cara a cara). Antes ajoelhava com as
                    // canelas dobradas para a frente
                    float k = Smooth(u / 0.32f);
                    roll = -_saveSideV * 74f * k;
                    pitch = 6f * k;
                    lift = 0.06f * k;
                    break;
                }
                case 7:   // bloqueio com o pé: o corpo inclina para o lado da perna esticada
                {
                    float k = Smooth(u / 0.22f);
                    roll = -_saveSideV * 12f * k;
                    pitch = 5f * k;
                    lift = -0.03f * k;
                    break;
                }
                case 6:   // salto no alto: agacha, sobe e está no ponto mais alto quando a bola chega; desce e amortece
                {
                    float top = Mathf.Max(0.1f, cu);
                    float crouch = u < top * 0.3f ? Mathf.Sin(u / (top * 0.3f) * Mathf.PI) : 0f;
                    float air = Mathf.Sin(Mathf.Clamp01((u - top * 0.2f) / (top * 1.6f)) * Mathf.PI);
                    float landK = u > top * 1.6f ? Mathf.Sin(Mathf.Clamp01((u - top * 1.6f) / 0.3f) * Mathf.PI) : 0f;
                    lift = air * 0.5f - crouch * 0.12f - landK * 0.1f;
                    pitch = -8f * air + 10f * crouch + 8f * landK;
                    roll = -_saveSideV * 6f * air;
                    break;
                }
                default:  // encaixe de frente: corpo atrás da bola, joelhos flexionados e inclinado para o lado dela
                {
                    float k = Smooth(u / 0.3f);
                    pitch = 10f * k;
                    roll = -_saveSideV * Mathf.Clamp(p.saveDist * 16f, 0f, 14f) * k;
                    lift = -0.05f * k;
                    break;
                }
            }
            if (_saveClip)
            {
                // a captura faz o corpo; aqui só a altura do voo: rasteiro mais baixo, no ângulo mais alto
                float hk = _saveKindV == 3 ? -0.32f : _saveKindV == 5 ? 0.3f : 0f;
                float bell = Mathf.Sin(Mathf.Clamp01(u / (cu * 2f)) * Mathf.PI);
                return new Vector3(0f, 0f, hk * bell * rec);
            }
            // levantando do chão: apoia num joelho (tronco à frente) antes de ficar de pé
            if (p.saveKind == 0 && _saveKindV >= 2 && _saveKindV <= 5) extraPitch = 22f * Mathf.Sin(recLin * Mathf.PI);
            return new Vector3(pitch * rec + extraPitch, roll * rec, lift * rec);
        }

        /// <summary>Braços na defesa: as duas mãos vão juntas na bola (ou no ponto do mergulho).</summary>
        void SaveArms()
        {
            // braços chegam na bola no instante do contato (antes já estavam esticados logo no início)
            float w = p.saveKind > 0 ? Smooth(_saveU / Mathf.Max(0.08f, Mathf.Clamp(p.saveContact, 0.12f, 0.75f) * 0.85f)) : Smooth(_saveRec / _saveRecT);
            if (!p.isKeeper || _saveKindV == 0 || w <= 0.001f || rig.spine == null) return;
            if (_saveKindV == 7) { FootBlock(w); return; }
            if (_saveClip) return;   // braços da captura
            var sim = view.sim;
            var ballW = W(sim.ball.pos, sim.ball.z + 0.11f);
            Vector3 target;
            var chest = rig.spine.position;
            // as mãos vão para onde a simulação diz que elas estão (é isso que decide a defesa) e, com a
            // bola chegando, para a própria bola: o encaixe/espalmada acontece na bola, não ao lado dela
            var hs = sim.KeeperHands(p, out _);
            var handsSim = W(new Vector2(hs.x, hs.y), hs.z);
            float near = Mathf.Clamp01(1f - ((ballW - handsSim).magnitude - 0.4f) / 1.8f);
            if (_saveKindV >= 3 && _saveKindV <= 5)
            {
                // mergulho: braços esticados no plano do voo, na direção das mãos da simulação
                var reach = (rig.head != null ? rig.head.position - chest : transform.up).normalized;
                var flight = chest + reach * 0.95f + transform.forward * 0.15f;
                target = Vector3.Lerp(flight, handsSim, 0.6f);
            }
            else if (_saveKindV == 2) target = Vector3.Lerp(chest + transform.forward * 0.5f, handsSim, 0.6f);   // deitado de lado: braços à frente, na bola
            else target = handsSim;
            target = Vector3.Lerp(target, ballW, near);
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "_l" : "_r";
                rig.bones.TryGetValue("upperarm" + sfx, out var up);
                rig.bones.TryGetValue("lowerarm" + sfx, out var lo);
                rig.bones.TryGetValue("hand" + sfx, out var hd);
                if (up == null || lo == null || hd == null) continue;
                float s = Mathf.Sign(Vector3.Dot(up.position - chest, transform.right));
                // mão trocada: o braço do lado do mergulho fica junto do corpo e o de cima cruza até a bola
                if (_crossHand && _saveKindV == 5 && s == _saveSideV) continue;
                // mãos lado a lado (formando o "W" atrás da bola)
                var handT = _crossHand && _saveKindV == 5 ? target : target + transform.right * s * 0.09f;
                var dirUp = handT - up.position;
                if (dirUp.sqrMagnitude < 1e-4f) continue;
                _pose.Rotate(up, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(lo.position - up.position, dirUp), w));
                _pose.Rotate(lo, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(hd.position - lo.position, handT - lo.position), w));
            }
            // pernas. (+ em torno de "right" leva o osso para trás: coxa à frente = ângulo negativo,
            // joelho dobrado = canela para trás = ângulo positivo)
            float cuL = Mathf.Clamp(p.saveContact, 0.12f, 0.75f);
            var rgt = transform.right;
            if (_saveKindV >= 2 && _saveKindV <= 5)
            {
                // ponte: a perna de baixo (a do impulso) estica; a de cima sobe com o joelho no impulso
                // (como quem empurra o corpo para o lado) e depois acompanha o voo, um pouco dobrada
                float drive = p.saveKind > 0 && _saveKindV != 2 ? Mathf.Sin(Mathf.Clamp01(_saveU / (cuL * 1.3f)) * Mathf.PI) : 0f;
                string top = LegOnSide(-_saveSideV);
                rig.bones.TryGetValue("thigh" + top, out var topThigh);
                rig.bones.TryGetValue("calf" + top, out var topCalf);
                _pose.Rotate(topThigh, Quaternion.AngleAxis(-(18f * w + 32f * drive), rgt));
                _pose.Rotate(topCalf, Quaternion.AngleAxis(25f * w + 40f * drive, rgt));
                if (_saveKindV == 2)
                {
                    // abafando: as pernas dobram juntas, fechando o espaço por baixo
                    string bot = LegOnSide(_saveSideV);
                    rig.bones.TryGetValue("thigh" + bot, out var bt);
                    rig.bones.TryGetValue("calf" + bot, out var bc);
                    _pose.Rotate(bt, Quaternion.AngleAxis(-25f * w, rgt));
                    _pose.Rotate(bc, Quaternion.AngleAxis(45f * w, rgt));
                }
            }
            else if (_saveKindV == 6)
            {
                // salto: agacha (impulso), sobe com um joelho à frente (protege e dá impulso) e a outra perna
                // estica; na volta ao chão os dois joelhos dobram (amortece)
                float u6 = _saveU, top6 = Mathf.Max(0.1f, cuL);
                float crouch = u6 < top6 * 0.3f ? Mathf.Sin(u6 / (top6 * 0.3f) * Mathf.PI) : 0f;
                float air = p.saveKind > 0 ? Mathf.Sin(Mathf.Clamp01((u6 - top6 * 0.2f) / (top6 * 1.6f)) * Mathf.PI) : 0f;
                float landK = p.saveKind > 0 && u6 > top6 * 1.6f ? Mathf.Sin(Mathf.Clamp01((u6 - top6 * 1.6f) / 0.3f) * Mathf.PI) : 0f;
                string kn = LegOnSide(-_saveSideV);
                foreach (var sfx in LR)
                {
                    rig.bones.TryGetValue("thigh" + sfx, out var th);
                    rig.bones.TryGetValue("calf" + sfx, out var cf);
                    bool knee = sfx == kn;
                    float thA = -(28f * crouch + 30f * landK + (knee ? 60f * air : 6f * air));
                    float cfA = 50f * crouch + 50f * landK + (knee ? 85f * air : 12f * air);
                    _pose.Rotate(th, Quaternion.AngleAxis(thA * w, rgt));
                    _pose.Rotate(cf, Quaternion.AngleAxis(cfA * w, rgt));
                }
            }
            else if (_saveKindV == 1)
            {
                // encaixe: joelhos flexionados, peso à frente
                foreach (var sfx in new[] { "_l", "_r" })
                {
                    rig.bones.TryGetValue("thigh" + sfx, out var th);
                    rig.bones.TryGetValue("calf" + sfx, out var cf);
                    _pose.Rotate(th, Quaternion.AngleAxis(-14f * w, rgt));
                    _pose.Rotate(cf, Quaternion.AngleAxis(22f * w, rgt));
                }
            }
        }

        /// <summary>Sufixo ("_l"/"_r") da perna que está fisicamente do lado dado (+1 direita do corpo).</summary>
        string LegOnSide(float side)
        {
            if (rig.pelvis != null && rig.bones.TryGetValue("thigh_r", out var tr))
            {
                float s = Mathf.Sign(Vector3.Dot(tr.position - rig.pelvis.position, transform.right));
                return (s == Mathf.Sign(side)) ? "_r" : "_l";
            }
            return side > 0 ? "_r" : "_l";
        }

        /// <summary>
        /// Bloqueio com o pé: a perna do lado da bola estica para o lado (rente ao chão), a outra dobra para
        /// apoiar, e os braços abrem para equilibrar (e ocupar espaço).
        /// </summary>
        void FootBlock(float w)
        {
            var fwd = transform.forward; var right = transform.right;
            // (pernas pelo lado físico, não pelo nome do osso: antes a perna "do lado" podia ser a de lá e
            // cruzava na frente da outra)
            string near = LegOnSide(_saveSideV), far = near == "_l" ? "_r" : "_l";
            // a perna do lado da bola estica para o lado e um pouco à frente (o pé na trajetória), joelho
            // quase esticado e o corpo baixa sobre a perna de apoio
            rig.bones.TryGetValue("thigh" + near, out var th);
            rig.bones.TryGetValue("calf" + near, out var cf);
            rig.bones.TryGetValue("thigh" + far, out var th2);
            rig.bones.TryGetValue("calf" + far, out var cf2);
            var ft = near == "_l" ? rig.footL : rig.footR;
            if (th != null && cf != null)
            {
                // direção da perna: para o lado do mergulho, rente ao chão, um pouco à frente
                var legDir = (right * _saveSideV * 0.82f - Vector3.up * 0.5f + fwd * 0.18f).normalized;
                _pose.Rotate(th, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cf.position - th.position, legDir), w));
                if (ft != null) _pose.Rotate(cf, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(ft.position - cf.position, legDir), w * 0.9f));
            }
            _pose.Rotate(th2, Quaternion.AngleAxis(-30f * w, right));                                   // a outra dobra (apoio)
            _pose.Rotate(cf2, Quaternion.AngleAxis(55f * w, right));
            if (rig.spine != null) _pose.Rotate(rig.spine, Quaternion.AngleAxis(-_saveSideV * 10f * w, fwd));   // tronco equilibra para o outro lado
            foreach (var sfx in LR)
            {
                rig.bones.TryGetValue("upperarm" + sfx, out var ua);
                if (ua == null) continue;
                float sd = Mathf.Sign(Vector3.Dot(ua.position - rig.spine.position, right));
                _pose.Rotate(ua, Quaternion.AngleAxis(sd * 50f * w, fwd));                              // braços abertos
            }
        }

        float _kickBodyT, _kickBodyTotal, _kickContactU, _kickLoft;

        /// <summary>
        /// Corpo no chute: braço do lado contrário abre para equilibrar, o outro vai para trás; no
        /// contato o tronco fica sobre a bola (rasteiro) ou um pouco para trás (por cima).
        /// </summary>
        void KickBody()
        {
            if (_kickBodyT <= 0f || rig.spine == null) return;
            _kickBodyT -= Time.deltaTime;
            float u = 1f - Mathf.Clamp01(_kickBodyT / Mathf.Max(0.01f, _kickBodyTotal));
            float w = Smooth(u / Mathf.Max(0.05f, _kickContactU * 0.6f)) * (1f - Smooth((u - _kickContactU) / (1f - _kickContactU + 1e-3f)) * 0.8f);
            var ax = transform.right;
            // tronco: sobre a bola no rasteiro, inclinado para trás na bola alta
            _pose.Rotate(rig.spine, Quaternion.AngleAxis(Mathf.Lerp(9f, -10f, _kickLoft) * w, ax));
            // braços: o esquerdo abre (chute de direita), o direito recua (a captura do Mixamo já tem)
            if (!AthleteAnimator.Mocap)
            foreach (var (sfx, abd, back) in new[] { ("_l", 1f, 0f), ("_r", 0.3f, 1f) })
            {
                if (!rig.bones.TryGetValue("upperarm" + sfx, out var up)) continue;
                float s = sfx == "_l" ? -1f : 1f;
                _pose.Rotate(up, Quaternion.AngleAxis(-s * 55f * abd * w, transform.forward) * Quaternion.AngleAxis(25f * back * w, ax));
            }
        }

        float _recvT, _recvTotal;
        int _recvLeg;

        /// <summary>Gesto do domínio: coxa sobe, pé vira por dentro e recua acompanhando a bola.</summary>
        void ReceiveLeg()
        {
            if (_recvT <= 0f) return;
            _recvT -= Time.deltaTime;
            float u = 1f - Mathf.Clamp01(_recvT / Mathf.Max(0.01f, _recvTotal));
            float k = u < 0.35f ? Smooth(u / 0.35f) : 1f - Smooth((u - 0.35f) / 0.65f);
            string sfx = _recvLeg == 0 ? "_l" : "_r";
            rig.bones.TryGetValue("thigh" + sfx, out var th);
            rig.bones.TryGetValue("calf" + sfx, out var cf);
            rig.bones.TryGetValue("foot" + sfx, out var ft);
            var ax = transform.right;
            float out_ = _recvLeg == 0 ? 1f : -1f;
            _pose.Rotate(th, Quaternion.AngleAxis(-30f * k, ax) * Quaternion.AngleAxis(out_ * 18f * k, transform.forward));
            _pose.Rotate(cf, Quaternion.AngleAxis(22f * k, ax));
            _pose.Rotate(ft, Quaternion.AngleAxis(out_ * 35f * k, transform.up));   // parte de dentro do pé de frente para a bola
            if (rig.spine != null) _pose.Rotate(rig.spine, Quaternion.AngleAxis(6f * k, ax));   // corpo sobre a bola
        }

        float _animSpd;
        readonly PoseEdits _pose = new PoseEdits();
        float _accLean, _cutCd, _touchAnim, _lastTouchT;
        Vector2 _refDir;
        int _touchLeg;

        /// <summary>Corte brusco em velocidade: animação de corte (pé de apoio cravado).</summary>
        void Cuts(float dt, float spd)
        {
            _cutCd -= dt;
            var dir = spd > 0.5f ? p.vel / spd : _refDir;
            if (_refDir == Vector2.zero) _refDir = dir;
            float ang = Vector2.SignedAngle(_refDir, dir);   // + = anti-horário visto de cima (esquerda)
            if (spd > 3.6f && p.down <= 0f && Mathf.Abs(ang) > 70f && _cutCd <= 0f && (anim.CurrentAction == null || anim.ActionWeight < 0.2f) && p.slide <= 0f && !p.isKeeper)
            {
                anim.Play(ang > 0f ? "CutL" : "CutR", 0.28f, 0.5f, 1.4f, 0.1f);
                _cutCd = 0.9f;
                // patada no gramado na mudança brusca de direção
                if (view.Turf != null && rig.footL != null)
                {
                    var foot = (ang > 0f ? rig.footR : rig.footL) ?? rig.footL;
                    var d3 = new Vector3(dir.x, 0f, dir.y);
                    view.Turf.Mark(foot.position - d3 * 0.12f, foot.position + d3 * 0.2f, 0.16f, 25f);
                }
            }
            _refDir = Vector2.Lerp(_refDir, dir, 1f - Mathf.Exp(-dt * 5f)).normalized;
        }

        /// <summary>Toque de condução: o pé que está no ar vai na bola quando a simulação a empurra.</summary>
        void Touches()
        {
            var sim = view.sim;
            if (p.touchT > _lastTouchT + 0.08f && sim.ball.owner == p.id && sim.ball.z < 0.3f && (anim.CurrentAction == null || anim.ActionWeight < 0.3f))
            {
                _touchAnim = 1f;
                // toca a perna que está atrás (vindo em balanço para a frente), não a de apoio
                _touchLeg = rig.footL != null && rig.footR != null && Vector3.Dot(rig.footL.position - rig.footR.position, transform.forward) < 0f ? 0 : 1;
            }
            _lastTouchT = p.touchT;
        }

        // ------------------------------------------------------------ jeito de correr (estilo + intensidade)
        Vector2 acc2D;
        bool _styleInit;
        float _cadence = 1f, _armAmp = 1f, _twist = 1f, _knee = 1f, _idleT, _restPose;
        // jeito de carregar os braços: abertos/colados ao corpo, cotovelo mais ou menos dobrado,
        // braços mais à frente, assimetria (um braço balança mais que o outro)
        float _armCarry, _elbowStyle, _armFwd, _armAsym;
        float _swingL, _swingR, _armSideLag;
        int _restKind;

        void InitStyle()
        {
            if (_styleInit) return;
            _styleInit = true;
            uint h = (uint)((p.name ?? "").GetHashCode() ^ (p.id * 7919));
            float R(int k) => ((h >> (k * 5)) & 31) / 31f;
            _cadence = 0.93f + R(0) * 0.14f;      // passada mais longa ou mais curta
            _armAmp = 0.85f + R(1) * 0.45f;       // braços mais soltos ou mais contidos
            _twist = 0.8f + R(2) * 0.6f;          // ombros girando mais ou menos
            _knee = 0.9f + R(3) * 0.35f;          // quem levanta mais o joelho
            _restKind = (int)(R(4) * 3f);         // 0 mãos na cintura, 1 braços soltos, 2 mãos nos joelhos (cansado)
            _armCarry = 3f + R(5) * 11f;          // graus de abertura dos braços
            uint h2 = h * 2654435761u;
            float R2(int k) => ((h2 >> (k * 5)) & 31) / 31f;
            _elbowStyle = -10f + R2(0) * 26f;
            _armFwd = -4f + R2(1) * 10f;
            _armAsym = 0.85f + R2(2) * 0.3f;
            if (p.isKeeper) _armAmp *= 0.85f;
        }

        /// <summary>
        /// Por cima da captura: braços com a amplitude do estilo e da intensidade (pique = braço
        /// bombeando com cotovelo a ~90°), joelho mais alto no pique, rotação dos ombros contra o
        /// quadril, braços abertos conduzindo a bola; parado, de vez em quando mãos na cintura.
        /// </summary>
        void Gait()
        {
            if (rig.spine == null || p.down > 0f || p.slide > 0f || (p.isKeeper && (p.saveKind > 0 || p.InHands)) || _celebrate > 0f) return;
            if (AthleteAnimator.Mocap) { RestPose(); return; }   // braços e tronco da captura, como estão
            float act = anim != null ? anim.ActionWeight : 0f;
            float w = (1f - act) * (1f - _procW);   // a passada desenhada (ProcGait) já cuida de braços e pernas
            if (w <= 0.05f) return;
            float spd = _animSpd;
            float run = Mathf.Clamp01((spd - 2f) / 3f), sprint = Mathf.Clamp01((spd - 5.5f) / 2.5f);
            bool hasBall = view.sim.ball.owner == p.id;
            var right = transform.right; var up = transform.up; var fwd = transform.forward;
            float amp = _armAmp * (1f + 0.4f * sprint) * (hasBall ? 0.85f : 1f) - 1f;
            _thighL = ThighAngle("_l", up, right);
            _thighR = ThighAngle("_r", up, right);
            _localVel = transform.InverseTransformDirection(new Vector3(p.vel.x, 0f, p.vel.y));
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "_l" : "_r";
                rig.bones.TryGetValue("upperarm" + sfx, out var ua);
                rig.bones.TryGetValue("lowerarm" + sfx, out var la);
                rig.bones.TryGetValue("hand" + sfx, out var hd);
                if (ua == null || la == null || hd == null) continue;
                float s = Mathf.Sign(Vector3.Dot(ua.position - rig.spine.position, right));
                // balanço acoplado às pernas: o braço acompanha a coxa do lado oposto (como no corpo
                // humano), em qualquer ritmo — andando, trotando, correndo. A amplitude e o jeito de
                // carregar os braços mudam de atleta para atleta.
                var armDir = la.position - ua.position;
                float swing = Vector3.SignedAngle(-up, Vector3.ProjectOnPlane(armDir, right), right);
                float legA = side < 0 ? _thighR : _thighL;
                float gain = Mathf.Lerp(0.75f, 1.45f, Mathf.Max(run, sprint)) * _armAmp * (side < 0 ? 1f : _armAsym) * (hasBall ? 0.8f : 1f);
                float moving = Mathf.Clamp01((spd - 0.3f) / 1.2f);
                float want = legA * gain - (_armFwd + 8f * run + 6f * sprint);
                ref float sw = ref (side < 0 ? ref _swingL : ref _swingR);
                sw = Mathf.Lerp(sw, want, 1f - Mathf.Exp(-Time.deltaTime * 18f));
                float corr = Mathf.Clamp(sw - swing, -40f, 40f) * 0.8f * moving;
                // amplia o que a captura já tem no pique (braço bombeando)
                corr += Mathf.Clamp(swing * amp * run, -12f, 12f) * (1f - moving * 0.5f);
                _pose.Rotate(ua, Quaternion.AngleAxis(corr * w, right));
                // abertura: estilo + equilíbrio (curva: o braço de fora abre; de lado/de costas: os dois abrem)
                float turnOpen = Mathf.Clamp(-_yawRate * s * 0.04f, 0f, 14f) * Mathf.Clamp01(spd / 4f);
                float lateral = Mathf.Clamp01(Mathf.Abs(_localVel.x) / 2.5f) + Mathf.Clamp01(-_localVel.z / 2f);
                float open = (_armCarry * (0.6f + 0.4f * run) + turnOpen + lateral * 10f) * moving;
                _pose.Rotate(ua, Quaternion.AngleAxis(s * open * w, fwd));
                // cotovelo: dobra mais correndo (~90° no pique), quase solto andando; dobra mais
                // quando o braço vai à frente e abre quando vai para trás (como no movimento real)
                float bend = Vector3.Angle(la.position - ua.position, hd.position - la.position);
                float phaseBend = Mathf.Clamp(-sw * 0.35f, -12f, 22f) * moving;
                float wantBend = Mathf.Clamp(Mathf.Lerp(15f, 88f, Mathf.Max(run * 0.7f, sprint)) + _elbowStyle * moving + phaseBend, 8f, 110f);
                if (bend < wantBend) _pose.Rotate(la, Quaternion.AngleAxis(-(wantBend - bend) * 0.8f * w, right));
                // com a bola: braços um pouco abertos (equilíbrio)
                if (hasBall) _pose.Rotate(ua, Quaternion.AngleAxis(s * 12f * w * Mathf.Max(0.4f, run), fwd));
            }
            // joelho alto no pique (só a coxa que está à frente)
            if (sprint > 0.01f)
                foreach (var sfx in new[] { "_l", "_r" })
                {
                    rig.bones.TryGetValue("thigh" + sfx, out var th);
                    rig.bones.TryGetValue("calf" + sfx, out var cf);
                    if (th == null || cf == null) continue;
                    float a = Vector3.SignedAngle(-up, Vector3.ProjectOnPlane(cf.position - th.position, right), right);
                    if (a < -5f) _pose.Rotate(th, Quaternion.AngleAxis(Mathf.Clamp(a * (_knee * 1.25f - 1f) * sprint, -14f, 0f) * w, right));
                }
            // ombros contra o quadril (amplia o giro natural da corrida)
            if (rig.bones.TryGetValue("clavicle_l", out var cl) && rig.bones.TryGetValue("clavicle_r", out var cr) && rig.bones.TryGetValue("thigh_l", out var tl) && rig.bones.TryGetValue("thigh_r", out var tr))
            {
                float tw = Vector3.SignedAngle(Vector3.ProjectOnPlane(tr.position - tl.position, up), Vector3.ProjectOnPlane(cr.position - cl.position, up), up);
                _pose.Rotate(rig.spine, Quaternion.AngleAxis(Mathf.Clamp(tw * (_twist - 1f), -8f, 8f) * run * w, up));
            }
            // tronco antecipa a curva (o olhar e os ombros entram antes do quadril)
            float into = Mathf.Clamp(_yawRate * 0.035f, -12f, 12f) * Mathf.Clamp01(spd / 3f);
            _pose.Rotate(rig.spine, Quaternion.AngleAxis(into * w, up));
            // parado: respira e troca o peso de uma perna para a outra (ninguém fica estátua)
            float still = 1f - Mathf.Clamp01(spd / 0.8f);
            if (still > 0.01f)
            {
                float t = Time.time + _phaseOff;
                float breathe = Mathf.Sin(t * (p.stamina < 0.5f ? 2.6f : 1.5f)) * (p.stamina < 0.5f ? 2.2f : 1.1f);
                _pose.Rotate(rig.spine, Quaternion.AngleAxis(-breathe * still * w, right));
                rig.bones.TryGetValue("spine_01", out var sp1);
                _pose.Rotate(sp1, Quaternion.AngleAxis(Mathf.Sin(t * 0.45f) * 2.2f * still * w, fwd));
                if (rig.pelvis != null) _pose.Rotate(rig.pelvis, Quaternion.AngleAxis(Mathf.Sin(t * 0.45f + 0.6f) * 1.6f * still * w, fwd));
            }
            RestPose();
        }

        float _phaseOff => p != null ? p.id * 1.618f : 0f;

        float _gkReady;

        /// <summary>
        /// Goleiro em prontidão quando o adversário ataca: base larga, joelhos dobrados, peso na ponta
        /// dos pés, tronco à frente e mãos abertas na altura da cintura, acompanhando a bola com o corpo.
        /// </summary>
        void KeeperReady()
        {
            float dt = Time.deltaTime;
            var sim = view.sim;
            // (com a captura do Mixamo a espera e os passos de lado do goleiro são clipes: GkReady/GkSide)
            bool can = !(AthleteAnimator.Mocap && AthleteAnimator.Has("GkReady")) && p.isKeeper && rig.pelvis != null && rig.spine != null && p.saveKind == 0 && _saveRec <= 0f && !p.InHands && p.down <= 0f;
            float threat = 0f;
            if (can)
            {
                var goal = new Vector2(-sim.halfLen * sim.TeamDir(p.team), 0f);
                float d = sim.ball.pos.Dist(goal);
                bool theirs = sim.ball.owner >= 0 ? sim.players[sim.ball.owner].team != p.team : sim.ball.lastTouchTeam != p.team;
                threat = theirs ? Mathf.Clamp01(1f - (d - 8f) / (sim.halfLen * 0.55f)) : 0f;
                threat *= 1f - Mathf.Clamp01((_animSpd - 2.5f) / 2f);   // correndo, não
                threat *= 1f - (anim != null ? anim.ActionWeight : 0f);
            }
            _gkReady = Mathf.MoveTowards(_gkReady, threat, dt * 3f);
            float w = _gkReady;
            // goleiro se ajeitando devagar (de lado, de costas): passadas curtas desenhadas, joelhos
            // sempre dobrados para a frente (os clipes de andar de costas/de lado entortavam as pernas)
            float mv = p.vel.magnitude;
            bool shuffle = can && mv > 0.25f && mv < 3.4f && (anim == null || anim.ActionWeight < 0.3f)
                && Vector2.Dot(p.vel / mv, p.facing) < 0.75f;
            _gkShuffle = Mathf.MoveTowards(_gkShuffle, shuffle ? 1f : 0f, dt * 6f);
            if (_gkShuffle > 0f) _gkStep += dt * Mathf.Max(mv, 0.8f) / 0.9f;   // ~0,45 m por passo
            float legW = Mathf.Max(w, _gkShuffle);
            if (legW <= 0.01f) return;
            var right = transform.right; var up = transform.up; var fwd = transform.forward;
            float t = Time.time * 2.2f + _phaseOff;
            float bounce = Mathf.Sin(t * 3f) * 3f * w;   // pequenos ajustes na ponta dos pés
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "_l" : "_r";
                rig.bones.TryGetValue("thigh" + sfx, out var th);
                rig.bones.TryGetValue("calf" + sfx, out var cf);
                var ft = side < 0 ? rig.footL : rig.footR;
                if (th != null && cf != null && ft != null)
                {
                    float ps = Mathf.Sign(Vector3.Dot(th.position - rig.pelvis.position, right));
                    var lat = right * ps * 0.22f;
                    // passo: uma perna de cada vez sobe (quadril e joelho dobram mais) e vai na direção do movimento
                    float lift = Mathf.Max(0f, Mathf.Sin((_gkStep + (side < 0 ? 0f : 0.5f)) * Mathf.PI * 2f)) * _gkShuffle;
                    var mvW = new Vector3(p.vel.x, 0f, p.vel.y);
                    var reach = mvW.sqrMagnitude > 0.01f ? mvW.normalized * 0.18f * lift : Vector3.zero;
                    float hip = 30f + bounce * w + 18f * lift, knee = 58f + 34f * lift;
                    var thighDir = (Quaternion.AngleAxis(-hip, right) * -up + lat + reach).normalized;
                    _pose.Rotate(th, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cf.position - th.position, thighDir), legW));
                    var calfDir = (Quaternion.AngleAxis(-hip + knee, right) * -up + lat * 0.6f + reach * 0.5f).normalized;
                    _pose.Rotate(cf, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(ft.position - cf.position, calfDir), legW));
                }
                if (w <= 0.01f) continue;
                rig.bones.TryGetValue("upperarm" + sfx, out var ua);
                rig.bones.TryGetValue("lowerarm" + sfx, out var la);
                rig.bones.TryGetValue("hand" + sfx, out var hd);
                if (ua != null && la != null && hd != null)
                {
                    float ps = Mathf.Sign(Vector3.Dot(ua.position - rig.spine.position, right));
                    var upDir = (Quaternion.AngleAxis(ps * 38f, fwd) * (Quaternion.AngleAxis(-28f, right) * -up)).normalized;
                    _pose.Rotate(ua, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(la.position - ua.position, upDir), w));
                    var loDir = (Quaternion.AngleAxis(-75f, right) * -up + right * ps * 0.25f).normalized;   // palmas para a frente, mãos abertas
                    _pose.Rotate(la, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(hd.position - la.position, loDir), w));
                }
            }
            rig.bones.TryGetValue("spine_01", out var sp1);
            float tw = Mathf.Max(w, _gkShuffle * 0.5f);
            _pose.Rotate(sp1, Quaternion.AngleAxis(16f * tw, right));
            _pose.Rotate(rig.spine, Quaternion.AngleAxis(6f * tw, right));
        }

        float _gkShuffle, _gkStep;

        // ------------------------------------------------------------ passada desenhada
        float _gPhase, _procW;
        Vector2 _gDir;

        float _bounce;

        // curvas (fase do ciclo, graus) — quadril: + flexão (coxa à frente); joelho: + dobrado
        static readonly float[] HipWalk = { 0f, 24f, 0.15f, 15f, 0.5f, -10f, 0.62f, -6f, 0.75f, 18f, 0.88f, 27f, 1f, 24f };
        static readonly float[] KneeWalk = { 0f, 5f, 0.15f, 17f, 0.4f, 5f, 0.6f, 38f, 0.72f, 60f, 0.88f, 12f, 1f, 5f };
        static readonly float[] HipJog = { 0f, 28f, 0.18f, 6f, 0.38f, -14f, 0.55f, 6f, 0.75f, 34f, 0.88f, 39f, 1f, 28f };
        static readonly float[] KneeJog = { 0f, 10f, 0.12f, 24f, 0.38f, 11f, 0.55f, 74f, 0.68f, 94f, 0.85f, 36f, 0.95f, 11f, 1f, 10f };   // (apoio mais esticado: corria "sentado")
        static readonly float[] HipSprint = { 0f, 36f, 0.12f, 10f, 0.25f, -18f, 0.45f, 12f, 0.65f, 60f, 0.78f, 68f, 0.9f, 50f, 1f, 36f };
        static readonly float[] KneeSprint = { 0f, 14f, 0.08f, 25f, 0.25f, 15f, 0.45f, 110f, 0.56f, 122f, 0.75f, 58f, 0.9f, 18f, 1f, 14f };

        /// <summary>Curva por pontos (fase, valor), interpolação suave (Catmull-Rom), periódica.</summary>
        static float Curve(float[] k, float u)
        {
            int n = k.Length / 2;
            u = Mathf.Repeat(u, 1f);
            int i = 0;
            while (i < n - 2 && u > k[(i + 1) * 2]) i++;
            float u0 = k[i * 2], u1 = k[(i + 1) * 2];
            float t = (u - u0) / Mathf.Max(1e-4f, u1 - u0);
            float p1 = k[i * 2 + 1], p2 = k[(i + 1) * 2 + 1];
            float p0 = i > 0 ? k[(i - 1) * 2 + 1] : k[(n - 2) * 2 + 1];
            float p3 = i + 2 < n ? k[(i + 2) * 2 + 1] : k[1 * 2 + 1];
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>Ciclos de passada por segundo pela velocidade (m/s).</summary>
        static float Freq(float v)
        {
            if (v < 1.4f) return Mathf.Lerp(0.75f, 0.95f, v / 1.4f);
            if (v < 3.5f) return Mathf.Lerp(0.95f, 1.4f, (v - 1.4f) / 2.1f);
            if (v < 6f) return Mathf.Lerp(1.4f, 1.72f, (v - 3.5f) / 2.5f);
            return Mathf.Lerp(1.72f, 2.15f, Mathf.Clamp01((v - 6f) / 3f));
        }

        static float G(float u, float c, float w)
        {
            float d = Mathf.Repeat(u - c + 0.5f, 1f) - 0.5f;   // distância circular na fase
            return Mathf.Exp(-d * d / (2f * w * w));
        }

        /// <summary>
        /// Ciclo de caminhada/corrida desenhado à mão (não a captura): para cada perna, a flexão do quadril
        /// e do joelho ao longo da passada (apoio, impulso, balanço), mais o braço do lado oposto, o giro
        /// da pelve e o contragiro dos ombros. Andar → trotar → correr → pique é uma mistura contínua,
        /// e a fase anda com a velocidade real (o pé não escorrega). Só para a frente; de costas e de
        /// lado fica a captura.
        /// </summary>
        void ProcGait()
        {
            float dt = Time.deltaTime;
            // (com a captura do Mixamo a passada é a da captura: nada desenhado por cima)
            bool skip = AthleteAnimator.Mocap || rig.spine == null || rig.pelvis == null || p.down > 0f || p.slide > 0f || _bikeT > 0f || (p.isKeeper && (p.saveKind > 0 || p.InHands || _saveRec > 0f)) || _celebrate > 0f;
            float spd = _animSpd;
            var right = transform.right; var up = transform.up; var fwd = transform.forward;
            var lv = transform.InverseTransformDirection(new Vector3(p.vel.x, 0f, p.vel.y));
            // direção do passo no corpo: para a frente, de lado ou de costas (a passada segue o
            // movimento, o joelho sempre dobra para trás e os braços balançam com as pernas)
            var mvL = new Vector2(lv.x, lv.z);
            float mvMag = mvL.magnitude;
            if (_gDir == Vector2.zero) _gDir = new Vector2(0f, 1f);
            // só muda a direção da passada com movimento de verdade e devagar (o tremido da velocidade
            // em baixa fazia a perna "varrer" em círculo)
            if (mvMag > 0.9f) _gDir = Vector2.Lerp(_gDir, mvL / mvMag, 1f - Mathf.Exp(-dt * 3f)).normalized;
            float back = Mathf.Clamp01(-_gDir.y), lateral = Mathf.Abs(_gDir.x);
            float act = anim != null ? anim.ActionWeight : 0f;
            float moving = Mathf.Clamp01((spd - 0.35f) / 0.7f);
            // só as ações grandes (chute, carrinho, drible) tiram a passada; toques curtos não
            float want = skip ? 0f : (1f - Mathf.Clamp01((act - 0.3f) / 0.4f)) * moving;
            _procW = Mathf.MoveTowards(_procW, want, dt * 5f);
            float W = _procW;
            float run = Mathf.Clamp01((spd - 1.9f) / 2.6f), sprint = Mathf.Clamp01((spd - 5.8f) / 2f) * (1f - back) * (1f - lateral);
            // de costas e de lado: passos mais curtos e baixos
            float dirAmp = Mathf.Lerp(1f, 0.55f, Mathf.Max(back, lateral * 0.8f));
            // comprimento do ciclo (duas passadas): curto andando, longo no pique; o estilo muda a passada
            // ritmo: passadas por segundo de verdade (andar ~0,95, trote ~1,4, corrida ~1,75, pique ~2,15
            // ciclos/s); a passada (comprimento) sai da velocidade. Antes o comprimento era fixo por faixa.
            float freq = Freq(spd) * Mathf.Clamp(_cadence, 0.85f, 1.2f) / Mathf.Sqrt(Mathf.Max(0.85f, transform.localScale.y));
            _gPhase = Mathf.Repeat(_gPhase + freq * dt, 1f);
            if (W <= 0.01f) { _bounce = 0f; return; }
            bool hasBall = view.sim.ball.owner == p.id;
            float legScale = hasBall ? 0.85f : 1f;
            // quadris e joelhos das duas pernas (direita = fase, esquerda = fase + meio ciclo)
            // ângulos do quadril e do joelho ao longo do ciclo (0 = pé tocando o chão), tirados de
            // curvas medidas de caminhada, trote e pique (biomecânica da corrida): no pique o apoio é
            // curto (~25% do ciclo), o quadril estende no impulso e o joelho recolhe alto no balanço
            float wJ = Mathf.Clamp01((spd - 1.4f) / 2.1f), wS = Mathf.Clamp01((spd - 3.5f) / 4.5f) * (1f - back) * (1f - lateral * 0.7f);
            float HipAt(float u)
            {
                float a = Mathf.Lerp(Curve(HipWalk, u), Curve(HipJog, u), wJ);
                return Mathf.Lerp(a, Curve(HipSprint, u), wS) * legScale * dirAmp;
            }
            float KneeAt(float u)
            {
                float a = Mathf.Lerp(Curve(KneeWalk, u), Curve(KneeJog, u), wJ);
                a = Mathf.Lerp(a, Curve(KneeSprint, u), wS);
                return a * Mathf.Lerp(1f, 0.92f, hasBall ? 1f : 0f) * Mathf.Lerp(1f, 0.7f, Mathf.Max(back, lateral)) * Mathf.Lerp(1f, _knee, wJ);
            }
            // voo: o corpo sobe entre um apoio e outro (2 voos por ciclo) e afunda no meio do apoio;
            // andando é o contrário (sobe no meio do apoio, de leve)
            float stanceEnd = Mathf.Lerp(Mathf.Lerp(0.6f, 0.38f, wJ), 0.25f, wS);
            float cyc = Mathf.Repeat(_gPhase * 2f, 1f);   // meio ciclo = um passo
            float flightU = Mathf.Clamp01((cyc - stanceEnd) / Mathf.Max(0.05f, 1f - stanceEnd));
            float runB = Mathf.Sin(flightU * Mathf.PI) * Mathf.Lerp(0.055f, 0.045f, wS) - (cyc < stanceEnd ? Mathf.Sin(cyc / stanceEnd * Mathf.PI) * 0.02f : 0f);
            float walkB = Mathf.Sin(Mathf.Clamp01(cyc / 0.6f) * Mathf.PI) * 0.018f;
            _bounce = Mathf.Lerp(walkB, runB, wJ) * W * (1f - Mathf.Max(back, lateral) * 0.6f) * (hasBall ? 0.7f : 1f) * transform.localScale.y;
            float uR = _gPhase, uL = Mathf.Repeat(_gPhase + 0.5f, 1f);
            // pelve: gira para o lado da perna que vai à frente; ombros no sentido contrário
            // (andando o quadril quase não gira nem cai de lado: com mais, o atleta parecia "rebolar")
            float pelvisYaw = (HipAt(uR) - HipAt(uL)) * Mathf.Lerp(0.06f, 0.15f, run) * 0.5f;
            _pose.Rotate(rig.pelvis, Quaternion.AngleAxis(-pelvisYaw * W, up));
            _pose.Rotate(rig.spine, Quaternion.AngleAxis(pelvisYaw * 1.6f * _twist * W, up));
            // queda lateral da pelve (lado da perna em balanço desce um pouco) e inclinação para a frente
            float drop = (G(uR, 0.25f, 0.12f) - G(uL, 0.25f, 0.12f)) * Mathf.Lerp(1.2f, 1.1f, run);
            _pose.Rotate(rig.pelvis, Quaternion.AngleAxis(drop * W, fwd));
            rig.bones.TryGetValue("spine_01", out var sp1);
            // tronco: trote quase ereto (~2°), corrida rápida ~6°, pique intenso ~12° (o corpo "cai" para a frente)
            float fastRun = Mathf.Clamp01((spd - 4.2f) / 1.8f) * (1f - back) * (1f - lateral);
            _pose.Rotate(sp1, Quaternion.AngleAxis((Mathf.Lerp(1.5f, 3f, run) * (1f - back) + 3f * fastRun + 6.5f * sprint) * W, right));
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "_l" : "_r";
                rig.bones.TryGetValue("thigh" + sfx, out var th);
                rig.bones.TryGetValue("calf" + sfx, out var cf);
                var ft = side < 0 ? rig.footL : rig.footR;
                if (th == null || cf == null || ft == null) continue;
                // lado físico pela posição do osso (não pelo nome)
                float ps = Mathf.Sign(Vector3.Dot(th.position - rig.pelvis.position, right));
                float u = ps > 0f ? uR : uL;
                float hip = HipAt(u), knee = KneeAt(u);
                // + no eixo "right" leva o vetor para trás: flexão (para a frente) é ângulo negativo
                var lat = right * ps * Mathf.Lerp(0.035f, 0.015f, run) * transform.localScale.x;   // pés um pouco afastados, na proporção do atleta
                // passada: componente para a frente/para trás no plano do corpo e um pouco para o lado
                // (nada de girar o plano da perna: isso desenhava círculos)
                var thighDir = (Quaternion.AngleAxis(-hip * _gDir.y, right) * (Quaternion.AngleAxis(hip * _gDir.x * 0.6f, fwd) * -up) + lat).normalized;
                _pose.Rotate(th, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cf.position - th.position, thighDir), W));
                // o joelho dobra sempre para trás do corpo
                var calfDir = (Quaternion.AngleAxis(knee, right) * thighDir + lat * 0.5f).normalized;
                _pose.Rotate(cf, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(ft.position - cf.position, calfDir), W));
            }
            // braços: refeitos como numa corrida de verdade — o braço balança a partir do ombro, no ritmo
            // da perna oposta, num plano levemente cruzado para dentro do corpo; correndo, o cotovelo fica
            // perto de 90° (fecha um pouco na ida e abre na volta), a mão vai até a altura do peito na
            // frente e passa do quadril atrás; andando, o braço vai quase esticado e solto. Os ombros
            // avançam e recuam junto com o braço.
            // Padrões medidos (análise de marcha): andando (~1,4 m/s) o ombro vai ~10° à frente e ~20°
            // atrás, com o cotovelo quase solto (~15-30°); trotando, ~35° de amplitude e cotovelo ~70°;
            // correndo/pique, até ~55° à frente e ~45° atrás com o cotovelo entre ~60° (atrás) e ~100°
            // (à frente). O braço balança no plano do corpo, só um pouco para dentro; a mão não cruza a
            // frente do peito. O antebraço e a mão vêm um pouco atrasados em relação ao ombro (o braço é
            // solto, como um pêndulo), e a mão fica relaxada.
            float runArm = Smooth((spd - 1.8f) / 2.6f);   // andar -> trote: o cotovelo dobra aos poucos
            float cross = Mathf.Lerp(3f, 7f, runArm);
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "_l" : "_r";
                rig.bones.TryGetValue("upperarm" + sfx, out var ua);
                rig.bones.TryGetValue("lowerarm" + sfx, out var la);
                rig.bones.TryGetValue("hand" + sfx, out var hd);
                rig.bones.TryGetValue("clavicle" + sfx, out var clv);
                if (ua == null || la == null || hd == null) continue;
                float ps = Mathf.Sign(Vector3.Dot(ua.position - rig.spine.position, right));
                float uOpp = ps > 0f ? uL : uR;   // braço direito acompanha a perna esquerda
                // fase: andando, o braço está mais à frente quando o pé oposto toca o chão; correndo, junto
                // com o joelho oposto no alto (~0,15 ciclo antes). (Antes a fase era interpolada entre u e
                // u - 0,85 e, no meio do trote, o braço balançava junto com a perna do mesmo lado.)
                float ph = uOpp + 0.15f * runArm;
                float c = Mathf.Cos(2f * Mathf.PI * ph);
                float vel = -Mathf.Sin(2f * Mathf.PI * ph);   // > 0: braço indo para a frente
                float amp = (Mathf.Lerp(15f, 28f, runArm) + 9f * fastRun + 20f * sprint) * Mathf.Min(_armAmp, 1.1f) * (ps < 0 ? 1f : _armAsym) * (hasBall ? 0.85f : 1f)
                            * Mathf.Lerp(1f, 0.6f, Mathf.Max(back, lateral));
                // à frente e atrás: andando o braço vai mais para trás; correndo, quase simétrico
                float fwdK = Mathf.Lerp(0.6f, 0.95f, runArm), backK = Mathf.Lerp(1.2f, 1f, runArm);
                float swing = (c > 0f ? c * amp * fwdK : c * amp * backK) + _armFwd * 0.3f;
                // cotovelo: fecha na ida e abre na volta, com um pequeno atraso (antebraço solto)
                float cl = Mathf.Cos(2f * Mathf.PI * (ph - 0.06f));
                float elbowWalk = 16f + 10f * Mathf.Max(0f, cl);
                float elbowRun = Mathf.Lerp(70f, 80f, fastRun) + Mathf.Lerp(12f, 18f, sprint) * cl;   // trote: antebraço mais baixo e solto
                float elbow = Mathf.Lerp(elbowWalk, elbowRun, runArm) + 10f * sprint + _elbowStyle * 0.3f * runArm;
                elbow = Mathf.Clamp(elbow, 8f, 115f - Mathf.Max(0f, swing) * 0.5f);
                if (clv != null) _pose.Rotate(clv, Quaternion.AngleAxis(-ps * swing * 0.1f * Mathf.Max(0.4f, run) * W, up));   // ombro acompanha
                float abd = _armCarry * 0.25f + 11f + (hasBall ? 3.5f : 0f) + lateral * 5f;   // (7° ainda parecia braço colado)
                var swingAxis = Quaternion.AngleAxis(ps * cross, up) * right;
                var upDir = Quaternion.AngleAxis(-swing, swingAxis) * -up;
                upDir = (Quaternion.AngleAxis(ps * abd, fwd) * upDir).normalized;
                _pose.Rotate(ua, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(la.position - ua.position, upDir), W));
                // antebraço no mesmo plano, a mão indo só um pouco para dentro (sem "guarda de boxe")
                var loDir = (Quaternion.AngleAxis(-(swing + elbow), swingAxis) * -up).normalized;
                loDir = (loDir - right * ps * Mathf.Lerp(0.02f, 0.07f, runArm)).normalized;
                _pose.Rotate(la, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(hd.position - la.position, loDir), W));
                // punho solto: a mão fica para trás quando o braço acelera para a frente e vice-versa
                float wrist = Mathf.Lerp(12f, 6f, runArm) * vel * W;
                _pose.Rotate(hd, Quaternion.AngleAxis(wrist, swingAxis));
            }
        }
        float _thighL, _thighR;
        Vector3 _localVel;

        /// <summary>Ângulo da coxa no plano da corrida (negativo = perna à frente).</summary>
        float ThighAngle(string sfx, Vector3 up, Vector3 right)
        {
            rig.bones.TryGetValue("thigh" + sfx, out var th);
            rig.bones.TryGetValue("calf" + sfx, out var cf);
            if (th == null || cf == null) return 0f;
            return Vector3.SignedAngle(-up, Vector3.ProjectOnPlane(cf.position - th.position, right), right);
        }

        /// <summary>Parado há um tempo: mãos na cintura (ou nos joelhos, cansado), como no jogo de verdade.</summary>
        void RestPose()
        {
            bool tired = p.stamina < 0.45f;
            bool want = _idleT > 2.2f && (_restKind != 1 || tired) && seat < 0.5f;
            _restPose = Mathf.MoveTowards(_restPose, want ? 1f : 0f, Time.deltaTime * (want ? 2f : 5f));
            if (_restPose <= 0.001f || rig.pelvis == null) return;
            bool knees = tired && _restKind == 2;
            var right = transform.right; var up = transform.up; var fwd = transform.forward;
            if (knees)
            {
                rig.bones.TryGetValue("spine_01", out var sp1);
                _pose.Rotate(sp1, Quaternion.AngleAxis(28f * _restPose, right));
                _pose.Rotate(rig.spine, Quaternion.AngleAxis(12f * _restPose, right));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "_l" : "_r";
                rig.bones.TryGetValue("upperarm" + sfx, out var ua);
                rig.bones.TryGetValue("lowerarm" + sfx, out var la);
                rig.bones.TryGetValue("hand" + sfx, out var hd);
                rig.bones.TryGetValue("calf" + sfx, out var kneeB);
                if (ua == null || la == null || hd == null) continue;
                float s = Mathf.Sign(Vector3.Dot(ua.position - rig.spine.position, right));
                Vector3 handT, elbowT;
                if (knees && kneeB != null) { handT = kneeB.position + up * 0.05f + fwd * 0.08f; elbowT = Vector3.Lerp(ua.position, handT, 0.5f) + right * s * 0.12f; }
                else { handT = rig.pelvis.position + right * s * 0.19f + up * 0.1f + fwd * 0.02f; elbowT = ua.position + right * s * 0.3f - up * 0.2f - fwd * 0.08f; }
                _pose.Rotate(ua, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(la.position - ua.position, elbowT - ua.position), _restPose));
                _pose.Rotate(la, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(hd.position - la.position, handT - la.position), _restPose));
            }
        }

        /// <summary>
        /// Postura: mede o tronco (quadril -> pescoço) no referencial do corpo e tira o excesso da
        /// captura — corcunda, tronco caído para um lado — deixando o peito aberto e o atleta ereto.
        /// O limite cresce com a velocidade (corrida inclina, de pé fica reto). Vale para todos:
        /// jogadores, goleiros e o juiz.
        /// </summary>
        void Posture()
        {
            if (AthleteAnimator.Mocap || rig.pelvis == null || rig.neck == null || rig.spine == null) return;
            if (p.down > 0f || p.slide > 0f || _bikeT > 0f || (p.isKeeper && (p.saveKind > 0 || _saveRec > 0f)) || _celebrate > 0f) return;
            float act = anim != null ? anim.ActionWeight : 0f;
            float w = 1f - act * 0.7f;   // chutes e fintas mantêm o gesto
            var up = transform.up;
            var fwd = transform.forward;
            var right = transform.right;
            var torso = rig.neck.position - rig.pelvis.position;
            float spd = p.vel.magnitude;
            float lean = Mathf.Atan2(Vector3.Dot(torso, fwd), Vector3.Dot(torso, up)) * Mathf.Rad2Deg;
            float side = Mathf.Atan2(Vector3.Dot(torso, right), Vector3.Dot(torso, up)) * Mathf.Rad2Deg;
            // inclinação permitida: de pé ~5°, trote ~9°, pique ~15° (+ a inclinação da arrancada)
            float maxLean = Mathf.Lerp(5f, 15f, Mathf.Clamp01(spd / 8f)) + Mathf.Max(0f, _accLean);
            bool keeperReady = p.isKeeper && spd < 2f;
            if (keeperReady) maxLean = 16f;   // goleiro em posição de espera: tronco à frente de propósito
            float minLean = -4f;              // nem jogado para trás
            float fix = lean > maxLean ? lean - maxLean : lean < minLean ? lean - minLean : 0f;
            if (Mathf.Abs(fix) > 0.1f)
            {
                rig.bones.TryGetValue("spine_01", out var sp1);
                _pose.Rotate(sp1, Quaternion.AngleAxis(-fix * 0.45f * w, right));
                _pose.Rotate(rig.spine, Quaternion.AngleAxis(-fix * 0.4f * w, right));
            }
            float sideFix = Mathf.Abs(side) > 4f ? side - Mathf.Sign(side) * 4f : 0f;
            if (Mathf.Abs(sideFix) > 0.1f) _pose.Rotate(rig.spine, Quaternion.AngleAxis(sideFix * 0.8f * w, fwd));
            // peito aberto: o topo da coluna levemente para trás (ombros não caem para a frente)
            // (o "peito aberto" de -3° deixava o peito saltado para a frente: agora o tronco fica neutro, com
            // os ombros levemente à frente, como no corpo relaxado)
            _pose.Rotate(rig.spine, Quaternion.AngleAxis(1.5f * w * (1f - Mathf.Clamp01(spd / 7f)), right));
            // peito saltado: a parte de cima da coluna (peito -> pescoço) inclinada para trás em relação à de
            // baixo (quadril -> peito) arqueia as costas e empurra o peito para a frente. Endireita até a
            // linha de cima ficar ~3° à frente da de baixo (coluna reta, ombros relaxados)
            var lower = Vector3.ProjectOnPlane(rig.spine.position - rig.pelvis.position, right);
            var upper = Vector3.ProjectOnPlane(rig.neck.position - rig.spine.position, right);
            if (lower.sqrMagnitude > 1e-4f && upper.sqrMagnitude > 1e-4f)
            {
                float arch = Vector3.SignedAngle(lower, upper, right);   // + = de cima mais à frente
                if (arch < 3f) _pose.Rotate(rig.spine, Quaternion.AngleAxis(Mathf.Min(3f - arch, 14f) * 0.85f * w, right));
            }
        }

        /// <summary>Correções por cima da animação: tronco na aceleração e perna do toque.</summary>
        void BodyLayer()
        {
            float dt = Time.deltaTime;
            Posture();
            if (rig.bones.TryGetValue("spine_01", out var sp1) && Mathf.Abs(_accLean) > 0.05f)
            {
                var ax = transform.right;
                // o tronco inclina como um bloco (quadril), não dobrando a coluna em arco
                _pose.Rotate(sp1, Quaternion.AngleAxis(_accLean * 0.8f, ax));
                _pose.Rotate(rig.spine, Quaternion.AngleAxis(_accLean * 0.2f, ax));
            }
            ArmsDown();
            ProcGait();
            KeeperReady();
            Gait();
            StillArms();
            ReceiveLeg();
            KickBody();
            FeetFirm();
            if (_touchAnim > 0f)
            {
                // toque curto e "na hora": o pé chega na bola logo no início (pico ~0,05 s)
                _touchAnim = Mathf.Max(0f, _touchAnim - dt / 0.2f);
                float u = 1f - _touchAnim;
                float k = u < 0.25f ? u / 0.25f : 1f - (u - 0.25f) / 0.75f;
                string sfx = _touchLeg == 0 ? "_l" : "_r";
                rig.bones.TryGetValue("thigh" + sfx, out var th);
                rig.bones.TryGetValue("calf" + sfx, out var cf);
                var ax = transform.right;
                _pose.Rotate(th, Quaternion.AngleAxis(-26f * k, ax));
                _pose.Rotate(cf, Quaternion.AngleAxis(-16f * k, ax));
            }
        }

        /// <summary>
        /// Parado ou quase parado: braços soltos ao lado do corpo, cotovelo quase esticado e mão para
        /// baixo (a captura deixava os antebraços erguidos, mãos para cima, como quem segura algo).
        /// </summary>
        void StillArms()
        {
            if (AthleteAnimator.Mocap || rig.spine == null || p.down > 0f || p.slide > 0f || _celebrate > 0f || seat > 0.3f || _restPose > 0.05f) return;
            if (p.isKeeper && (p.saveKind > 0 || p.InHands || _gkReady > 0.05f)) return;
            float act = anim != null ? anim.ActionWeight : 0f;
            float w = (1f - Mathf.Clamp01((_animSpd - 0.5f) / 0.9f)) * (1f - act) * (1f - _procW);
            if (w <= 0.01f) return;
            var right = transform.right;
            foreach (var sfx in LR)
            {
                rig.bones.TryGetValue("upperarm" + sfx, out var ua);
                rig.bones.TryGetValue("lowerarm" + sfx, out var la);
                rig.bones.TryGetValue("hand" + sfx, out var hd);
                if (ua == null || la == null || hd == null) continue;
                var upper = (la.position - ua.position).normalized;
                // antebraço ~12° à frente do braço (cotovelo levemente dobrado), mão seguindo o antebraço
                var want = (Quaternion.AngleAxis(-12f, right) * upper).normalized;
                _pose.Rotate(la, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(hd.position - la.position, want), w));
                // punho neutro: a mão continua a linha do antebraço (nada de mão dobrada para cima)
                var mid = sfx == "_l" ? _midL : _midR;
                if (mid != null)
                {
                    var fore = (hd.position - la.position).normalized;
                    _pose.Rotate(hd, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(mid.position - hd.position, fore), w * 0.85f));
                }
            }
        }

        void FeetFirm()
        {
            if (AthleteAnimator.Mocap || p.slide > 0f || _bikeT > 0f || (p.isKeeper && (p.dive > 0f || p.Diving || _saveRec > 0f)) || p.down > 0f) return;   // (na bicicleta os pés "achatados no chão" entortavam no ar)
            PoseFix.Feet(rig, transform, _pose, anim != null ? anim.ActionWeight : 0f);
        }

        void ArmsDown()
        {
            if (AthleteAnimator.Mocap || _celebrate > 0f || (p.isKeeper && (p.dive > 0f || p.InHands)) || p.down > 0f) return;
            PoseFix.Arms(rig, transform, _pose, 1f - (anim != null ? anim.ActionWeight * 0.5f : 0f), p.vel.magnitude);
        }

        float _ground;
        Transform _shadow;
        static Material _shadowMat;

        /// <summary>Pés sempre no chão: depois da animação, alinha o pé mais baixo ao piso.</summary>
        void GroundFeet()
        {
            if (_bikeT > 0f) return;   // no ar, de cabeça para baixo: nada de prender os pés no chão
            if (rig == null || rig.model == null) return;
            Transform bl = null, br = null;
            rig.bones.TryGetValue("ball_l", out bl);
            rig.bones.TryGetValue("ball_r", out br);
            float lowest = float.MaxValue;
            // (sem arrays temporários: roda para cada atleta a cada quadro)
            if (bl != null) lowest = Mathf.Min(lowest, bl.position.y - _ballSole);
            if (br != null) lowest = Mathf.Min(lowest, br.position.y - _ballSole);
            if (rig.footL != null) lowest = Mathf.Min(lowest, rig.footL.position.y - _footSole);
            if (rig.footR != null) lowest = Mathf.Min(lowest, rig.footR.position.y - _footSole);
            if (lowest == float.MaxValue) return;
            float lift = transform.position.y;   // pulos/mergulhos intencionais
            float err = lowest - lift;
            // suaviza para não tremer, mas corrige rápido
            _ground = Mathf.Lerp(_ground, err, 1f - Mathf.Exp(-Time.deltaTime * 18f));
            // (goleiro em qualquer fase da defesa — voo, queda, deitado, levantando — fica fora: antes,
            // deitado com as pernas no alto, o corpo era puxado para baixo até o pé tocar o chão e
            // metade dele sumia dentro do gramado)
            bool keeperSave = p.isKeeper && (p.dive > 0f || p.saveKind > 0 || _saveRec > 0f);
            if (keeperSave) _ground = Mathf.Lerp(_ground, 0f, 1f - Mathf.Exp(-Time.deltaTime * 10f));
            if (p.slide <= 0f && !keeperSave && p.stun < 0.5f && p.down <= 0f)
                rig.model.position -= Vector3.up * _ground;
            // depois de firmar os pés: o sobe-e-desce da corrida (voo entre as passadas)
            if (p.slide <= 0f && !keeperSave && p.down <= 0f) rig.model.position += Vector3.up * _bounce;
            // sombra de contato
            if (_shadow == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(go.GetComponent<Collider>());
                go.name = "sombra";
                _shadowMat ??= Gfx.UnlitTransparent(new Color(0, 0, 0, 0.5f), Gfx.Radial(64, Color.white, new Color(1, 1, 1, 0), 1.6f));
                go.GetComponent<MeshRenderer>().sharedMaterial = _shadowMat;
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _shadow = go.transform;
                _shadow.SetParent(transform.parent, false);
            }
            var c = rig.pelvis != null ? rig.pelvis.position : transform.position;
            _shadow.position = new Vector3(c.x, 0.012f, c.z);
            _shadow.rotation = Quaternion.Euler(90, transform.eulerAngles.y, 0);
            _shadow.localScale = new Vector3(0.9f, 1.15f, 1f);
        }

        Transform _uaL, _uaR;
        Vector3 _uaLBase, _uaRBase;

        /// <summary>Ombros mais estreitos: aproxima a articulação do ombro do pescoço (~12%).</summary>
        void NarrowShoulders()
        {
            if (_uaL == null && rig.bones.TryGetValue("upperarm_l", out _uaL)) _uaLBase = _uaL.localPosition;
            if (_uaR == null && rig.bones.TryGetValue("upperarm_r", out _uaR)) _uaRBase = _uaR.localPosition;
            // (0,88 ainda deixava o ombro para fora da linha do tronco, como uma ombreira)
            // (o corpo já vem com os ombros na largura certa do Blender: aqui só um ajuste leve — apertar
            // demais empurrava a malha do ombro para o peito e o peito parecia estufado)
            // (com a captura do Mixamo o ombro fica onde o corpo foi feito: apertar puxava o braço para
            // dentro da camisa e a manga parecia solta do tronco)
            float sh = AthleteAnimator.Mocap ? 1f : 0.93f;
            if (_uaL != null) _uaL.localPosition = _uaLBase * sh;
            if (_uaR != null) _uaR.localPosition = _uaRBase * sh;
            // quadril um pouco mais estreito: pernas mais juntas (as coxas saíam muito afastadas)
            if (_thL == null && rig.bones.TryGetValue("thigh_l", out _thL)) _thLBase = _thL.localPosition;
            if (_thR == null && rig.bones.TryGetValue("thigh_r", out _thR)) _thRBase = _thR.localPosition;
            if (_thL != null) _thL.localPosition = _thLBase * 0.9f;
            if (_thR != null) _thR.localPosition = _thRBase * 0.9f;
        }
        Transform _thL, _thR;
        Vector3 _thLBase, _thRBase;

        /// <summary>
        /// Pernas sem torcer: (1) o joelho nunca dobra para a frente (perna "torta para trás", hiperestendida)
        /// nem para os lados além de ~8°; (2) a coxa gira no próprio eixo até o pé apontar para a frente
        /// do corpo (antes o pé era desvirado só no tornozelo e a canela ficava torcida).
        /// </summary>
        void KneeGuard()
        {
            if (AthleteAnimator.Mocap || rig.pelvis == null || p.down > 0f || p.slide > 0f || _bikeT > 0f || seat > 0.3f) return;
            if (p.isKeeper && (p.saveKind > 0 || _saveRec > 0f || p.dive > 0f)) return;
            float act = anim != null ? anim.ActionWeight : 0f;
            var right = transform.right; var fwd = transform.forward; var up = Vector3.up;
            foreach (var sfx in LR)
            {
                if (!rig.bones.TryGetValue("thigh" + sfx, out var th) || !rig.bones.TryGetValue("calf" + sfx, out var cf)) continue;
                var ft = sfx == "_l" ? rig.footL : rig.footR;
                rig.bones.TryGetValue(sfx == "_l" ? "ball_l" : "ball_r", out var toe);
                if (ft == null) continue;
                var a = cf.position - th.position;
                if (a.sqrMagnitude < 1e-4f) continue;
                // (2) giro da coxa: ponta do pé para a frente (com a abertura natural de ~8°)
                if (toe != null)
                {
                    var an = a.normalized;
                    var toeDir = Vector3.ProjectOnPlane(toe.position - ft.position, an);
                    float sgn = sfx == "_l" ? -1f : 1f;
                    var want = Vector3.ProjectOnPlane(Quaternion.AngleAxis(sgn * 8f, up) * fwd, an);
                    if (toeDir.sqrMagnitude > 1e-5f && want.sqrMagnitude > 1e-3f)
                    {
                        float tw = Vector3.SignedAngle(toeDir, want, an);
                        if (Mathf.Abs(tw) > 4f) _pose.Rotate(th, Quaternion.AngleAxis(Mathf.Clamp(tw, -70f, 70f) * 0.85f * (1f - act * 0.6f), an));
                    }
                }
                // (1) dobra do joelho no plano da passada: sempre para trás (+); de lado, no máximo ~8°
                var a2 = cf.position - th.position;
                var b = ft.position - cf.position;
                var aS = Vector3.ProjectOnPlane(a2, right); var bS = Vector3.ProjectOnPlane(b, right);
                if (aS.sqrMagnitude > 1e-4f && bS.sqrMagnitude > 1e-4f)
                {
                    float bend = Vector3.SignedAngle(aS, bS, right);
                    if (bend < -1f) _pose.Rotate(cf, Quaternion.AngleAxis(-1f - bend, right));
                }
                var aF = Vector3.ProjectOnPlane(cf.position - th.position, fwd); var bF = Vector3.ProjectOnPlane(ft.position - cf.position, fwd);
                if (aF.sqrMagnitude > 1e-4f && bF.sqrMagnitude > 1e-4f)
                {
                    float side = Vector3.SignedAngle(aF, bF, fwd);
                    if (Mathf.Abs(side) > 8f) _pose.Rotate(cf, Quaternion.AngleAxis(-(side - Mathf.Sign(side) * 8f) * (1f - act * 0.5f), fwd));
                }
            }
        }

        /// <summary>
        /// Ombros baixos, acompanhando o fim do pescoço: as clavículas descem ~24° e o pescoço fica um
        /// pouco mais longo. Vale em todos os estados (antes só na postura de pé: na comemoração, na
        /// queda e nas defesas os ombros voltavam a ficar quase na altura do pescoço).
        /// </summary>
        void LowerShoulders()
        {
            if (rig.spine == null) return;
            if (_neckT == null && rig.bones.TryGetValue("neck_01", out _neckT)) _neckPos0 = _neckT.localPosition;
            if (_neckT != null) _neckT.localPosition = _neckPos0 * 1.03f;
            var fwd = transform.forward;
            var right = transform.right;
            // nos braços para cima (comemoração, salto do goleiro) desce menos, para o braço não "quebrar"
            float k = (_celebrate > 0f && _armsUp > 0.3f) || (p.isKeeper && p.saveKind > 0) ? 0.45f : 1f;
            if (AthleteAnimator.Mocap) return;   // a captura do Mixamo já traz os ombros relaxados (baixar mais soltava a manga do tronco)
            foreach (var cn in Clavicles)
            {
                if (!rig.bones.TryGetValue(cn, out var clv) || clv.childCount == 0) continue;
                float cs = Mathf.Sign(Vector3.Dot(clv.GetChild(0).position - rig.spine.position, right));
                _pose.Rotate(clv, Quaternion.AngleAxis(-cs * 18f * k, fwd));   // (24° deixava o ombro caído, abaixo do começo do tronco)
            }
        }

        Transform _neckT;
        Vector3 _neckPos0;

        /// <summary>
        /// Base de pé: as pernas um pouco afastadas (antes ficavam coladas, joelho com joelho). Só
        /// parado ou andando devagar; correndo, a passada desenhada já cuida da largura.
        /// </summary>
        void Stance()
        {
            if (AthleteAnimator.Mocap || rig.pelvis == null || p.down > 0f || p.slide > 0f || (p.isKeeper && (p.saveKind > 0 || _saveRec > 0f)) || seat > 0.5f || _bikeT > 0f) return;
            float slow = 1f - Mathf.Clamp01((_animSpd - 0.6f) / 1.6f);
            if (slow <= 0.01f) return;
            var fwd = transform.forward;
            foreach (var sfx in LR)
            {
                if (!rig.bones.TryGetValue("thigh" + sfx, out var th) || !rig.bones.TryGetValue("calf" + sfx, out var cf)) continue;
                float ps = Mathf.Sign(Vector3.Dot(th.position - rig.pelvis.position, transform.right));
                _pose.Rotate(th, Quaternion.AngleAxis(ps * 1.8f * slow, fwd));
                _pose.Rotate(cf, Quaternion.AngleAxis(-ps * 1.2f * slow, fwd));   // canela de volta à vertical (pé plano)
            }
        }

        static readonly string[] LR = { "_l", "_r" };

        /// <summary>Sentado no banco: coxas na horizontal, canelas na vertical, mãos sobre as coxas.</summary>
        void SeatPose()
        {
            if (seat <= 0.001f || rig.pelvis == null) return;
            var right = transform.right;
            foreach (var sfx in LR)
            {
                rig.bones.TryGetValue("thigh" + sfx, out var th);
                rig.bones.TryGetValue("calf" + sfx, out var cf);
                rig.bones.TryGetValue("upperarm" + sfx, out var ua);
                rig.bones.TryGetValue("lowerarm" + sfx, out var la);
                if (th != null && cf != null)
                {
                    var thighDir = Vector3.Slerp(-transform.up, (transform.forward - transform.up * 0.08f).normalized, seat);
                    _pose.Rotate(th, Quaternion.FromToRotation(cf.position - th.position, thighDir));
                    var ft = sfx == "_l" ? rig.footL : rig.footR;
                    if (ft != null) _pose.Rotate(cf, Quaternion.FromToRotation(ft.position - cf.position, Vector3.Slerp(ft.position - cf.position, -transform.up, seat)));
                }
                if (ua != null && la != null)
                {
                    _pose.Rotate(ua, Quaternion.AngleAxis(-22f * seat, right));
                    _pose.Rotate(la, Quaternion.AngleAxis(-48f * seat, right));
                }
            }
            rig.bones.TryGetValue("spine_01", out var sp1);
            _pose.Rotate(sp1, Quaternion.AngleAxis(8f * seat, right));   // tronco um pouco à frente, apoiado
        }
        static readonly string[] Clavicles = { "clavicle_l", "clavicle_r" };

        Renderer _bodyR;
        /// <summary>Sentado (reservas no banco): 0 de pé .. 1 sentado.</summary>
        public float seat;

        void LateUpdate()
        {
            // fora da tela (e sem sombra na tela): pula as correções de pose — economiza processador e
            // bateria (no campo, metade dos atletas, os do aquecimento e o banco ficam fora do quadro)
            if (rig != null && _bodyR == null) _bodyR = rig.body != null ? (Renderer)rig.body : rig.GetComponentInChildren<SkinnedMeshRenderer>();   // (o corpo, não uma peça qualquer)
            if (_bodyR != null && !_bodyR.isVisible && Time.frameCount > 3) return;
            if (rig != null) NarrowShoulders();
            if (rig != null && p != null) LowerShoulders();
            if (p != null && rig != null)
            {
                // ações (cortes, fintas, giro) mantêm um pouco do balanço do quadril; locomoção, nada
                rig.PinHips(anim != null ? anim.ActionWeight * 0.25f : 0f);
                BodyLayer();
                FallLimbs();
                Protest();
                PullArm();
                PointArm();
                HelpUp();
                BicycleLimbs();
                SeatPose();
                Stance();
                KneeGuard();
                FeetFirm();   // (de novo, depois de girar as pernas: sola plana)
            }
            GroundFeet();
            Cloth();
            if (p == null || rig == null || rig.head == null) return;
            HeadAndNeck();
            Expressions();
            Arms();
            SaveArms();
            KeeperHold();
            ThrowArms();
            ApplyFingers();
        }

        float _throwT, _throwHoldW;

        /// <summary>
        /// Lateral com as mãos: segurando, os dois braços sobem com a bola acima e atrás da cabeça;
        /// no arremesso, giram para a frente (o tronco acompanha).
        /// </summary>
        bool _oneArm, _throwLow;

        void ThrowArms()
        {
            var sim = view.sim;
            bool holding = sim.throwIn && sim.throwInTaker == p.id && sim.ball.owner == p.id;
            _throwHoldW = Mathf.MoveTowards(_throwHoldW, holding ? 1f : 0f, Time.deltaTime / 0.2f);
            float swing = 0f;
            if (_throwT > 0f) { _throwT -= Time.deltaTime; swing = 1f - Mathf.Clamp01(_throwT / 0.32f); }
            float w = Mathf.Max(_throwHoldW, _throwT > 0f ? 1f - swing * swing : 0f);
            if (w <= 0.001f) return;
            var fwd = transform.forward;
            var up = Vector3.up;
            // segurando: braços para cima e um pouco para trás; arremessando: vão à frente
            var target = Vector3.Slerp((up * 0.88f - fwd * 0.4f).normalized, (fwd * 0.85f + up * 0.35f).normalized, swing);
            if (_oneArm && _throwT > 0f)
                target = _throwLow ? Vector3.Slerp((-fwd * 0.7f - up * 0.6f).normalized, (fwd * 0.9f - up * 0.35f).normalized, swing)   // rolada: de trás para a frente, por baixo
                                   : Vector3.Slerp((up * 0.7f - fwd * 0.7f).normalized, (fwd * 0.9f + up * 0.45f).normalized, swing);  // por cima do ombro
            if (rig.spine != null && swing > 0f) _pose.Rotate(rig.spine, Quaternion.AngleAxis(Mathf.Sin(swing * Mathf.PI) * 18f * w, transform.right));
            for (int side = -1; side <= 1; side += 2)
            {
                if (_oneArm && _throwT > 0f && side < 0) continue;   // goleiro: só o braço direito
                string sfx = side < 0 ? "_l" : "_r";
                rig.bones.TryGetValue("upperarm" + sfx, out var ua);
                rig.bones.TryGetValue("lowerarm" + sfx, out var la);
                rig.bones.TryGetValue("hand" + sfx, out var hd);
                if (ua == null || la == null) continue;
                var cur = (la.position - ua.position).normalized;
                var want = (target + transform.right * side * 0.18f).normalized;
                _pose.Rotate(ua, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, want), w));
                if (hd != null)
                {
                    // antebraço: dobra o cotovelo para as mãos se encontrarem na bola
                    var curF = (hd.position - la.position).normalized;
                    var wantF = (want - transform.right * side * 0.45f - fwd * 0.15f * (1f - swing)).normalized;
                    _pose.Rotate(la, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(curF, wantF), w));
                }
            }
        }

        // ---------------------------------------------------------------- tecido solto
        static readonly int IdInflate = Shader.PropertyToID("_ClothInflate"), IdBand = Shader.PropertyToID("_ClothBand"),
            IdDrag = Shader.PropertyToID("_ClothDrag"), IdFlutter = Shader.PropertyToID("_ClothFlutter"), IdTuck = Shader.PropertyToID("_ClothTuck"), IdOutline = Shader.PropertyToID("_OutlineMul");
        MaterialPropertyBlock _mpb;
        Vector3 _clothPos, _clothVel, _clothDrag, _clothDragV;
        float _clothPhase = -1f;

        /// <summary>
        /// Camisa e calção com folga e movimento: a barra fica para trás na arrancada, balança
        /// nas mudanças de direção e tremula com a velocidade (mola amortecida, sem física cara).
        /// </summary>
        void Cloth()
        {
            if (rig == null || rig.pelvis == null || (rig.shirt == null && rig.shorts == null)) return;
            // passo limitado (quadro lento, engasgo, pausa): a mola do tecido era integrada com o tempo do
            // quadro inteiro e, num quadro longo, ficava instável e explodia — o deslocamento crescia sem
            // parar, o uniforme era esticado para longe ou virava NaN e sumia ("jogador sem roupa")
            float dt = Mathf.Clamp(Time.deltaTime, 1e-4f, 1f / 30f);
            if (_mpb == null) { _mpb = new MaterialPropertyBlock(); _clothPos = rig.pelvis.position; _clothPhase = (p != null ? p.id : 0) * 1.37f; }
            var pos = rig.pelvis.position;
            var vel = (pos - _clothPos) / dt;
            _clothPos = pos;
            if (vel.sqrMagnitude > 400f) vel = Vector3.zero;   // teleporte (replay, reposição)
            var acc = (vel - _clothVel) / dt;
            _clothVel = Vector3.Lerp(_clothVel, vel, 1f - Mathf.Exp(-dt * 12f));
            // alvo: o tecido "fica para trás" do movimento e da aceleração
            var target = -_clothVel * 0.0025f - Vector3.ClampMagnitude(acc, 40f) * 0.0004f;
            target.y = Mathf.Clamp(target.y, -0.02f, 0.02f);
            target = Vector3.ClampMagnitude(target, 0.025f);
            // mola com amortecimento crítico: o tecido acompanha sem ficar balançando (o corpo não parece gelatina)
            // subpassos de no máximo 1/120 s (estável para qualquer taxa de quadros)
            int nSub = Mathf.CeilToInt(dt * 120f);
            float h = dt / nSub;
            for (int i = 0; i < nSub; i++)
            {
                _clothDragV += ((target - _clothDrag) * 260f - _clothDragV * 32f) * h;
                _clothDrag += _clothDragV * h;
            }
            if (float.IsNaN(_clothDrag.x) || float.IsNaN(_clothDrag.y) || float.IsNaN(_clothDrag.z) || float.IsNaN(_clothDragV.x)
                || float.IsNaN(_clothVel.x) || float.IsInfinity(_clothDrag.x))
            { _clothDrag = Vector3.zero; _clothDragV = Vector3.zero; _clothVel = Vector3.zero; }
            _clothDrag = Vector3.ClampMagnitude(_clothDrag, 0.04f);
            _clothDragV = Vector3.ClampMagnitude(_clothDragV, 2f);
            float spd = new Vector2(_clothVel.x, _clothVel.z).magnitude;
            float flutAmp = 0.0004f + Mathf.Min(spd, 9f) * 0.00025f;   // só a barra tremula de leve
            float freq = 9f + spd * 1.3f;
            if (rig.shirt != null)
            {
                float top = (rig.spine != null ? rig.spine.position.y : pos.y + 0.4f) + 0.02f;
                rig.shirt.GetPropertyBlock(_mpb);
                _mpb.SetFloat(IdInflate, 0.0025f);   // (0,007 + a folga do modelo estufava o peito)
                _mpb.SetVector(IdBand, new Vector4(top, pos.y - 0.14f, 0, 0));
                _mpb.SetVector(IdDrag, _clothDrag);
                _mpb.SetVector(IdFlutter, new Vector4(flutAmp, freq, _clothPhase, 0));
                // camisa por dentro (90%): entra no calção logo acima do quadril; por fora: solta
                _mpb.SetFloat(IdOutline, 0.45f);
                _mpb.SetVector(IdTuck, rig.untucked ? Vector4.zero : new Vector4(pos.y + 0.075f * transform.localScale.y, 0.03f, 0, 0));
                rig.shirt.SetPropertyBlock(_mpb);
            }
            if (rig.shorts != null)
            {
                rig.shorts.GetPropertyBlock(_mpb);
                _mpb.SetFloat(IdInflate, -0.004f);   // (o calção vinha mais largo que a camisa na cintura: degrau)
                _mpb.SetFloat(IdOutline, 0.45f);
                _mpb.SetVector(IdBand, new Vector4(pos.y - 0.02f, pos.y - 0.34f, 0, 0));
                _mpb.SetVector(IdDrag, _clothDrag * 0.55f);
                _mpb.SetVector(IdFlutter, new Vector4(flutAmp * 0.7f, freq * 1.1f, _clothPhase + 1.7f, 0));
                rig.shorts.SetPropertyBlock(_mpb);
            }
        }

        float _holdW;

        /// <summary>Goleiro com a bola: braços fecham à frente do peito e a bola fica encaixada nas mãos.</summary>
        void KeeperHold()
        {
            var sim = view.sim;
            bool holding = p.isKeeper && p.InHands && sim.ball.owner == p.id;
            _holdW = Mathf.MoveTowards(_holdW, holding ? 1f : 0f, Time.deltaTime / 0.12f);
            if (_holdW <= 0.001f || rig.spine == null) return;
            var fwd = transform.forward;
            var target = rig.spine.position + fwd * 0.3f - Vector3.up * 0.08f;
            Vector3 handL = target, handR = target;
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "_l" : "_r";
                rig.bones.TryGetValue("upperarm" + sfx, out var up);
                rig.bones.TryGetValue("lowerarm" + sfx, out var lo);
                rig.bones.TryGetValue("hand" + sfx, out var hd);
                if (up == null || lo == null || hd == null) continue;
                float s = Mathf.Sign(Vector3.Dot(up.position - rig.spine.position, transform.right));
                // mão de cada lado da bola; cotovelo para baixo e para fora
                var handT = target + transform.right * s * 0.11f;
                var elbowT = Vector3.Lerp(up.position, handT, 0.5f) - Vector3.up * 0.12f + transform.right * s * 0.1f;
                _pose.Rotate(up, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(lo.position - up.position, elbowT - up.position), _holdW));
                _pose.Rotate(lo, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(hd.position - lo.position, handT - lo.position), _holdW));
                if (s < 0) handL = hd.position; else handR = hd.position;
            }
            if (holding && view.BallVisual != null)
                view.BallVisual.position = Vector3.Lerp(view.BallVisual.position, (handL + handR) * 0.5f + fwd * 0.06f, _holdW);
        }

        float _armsUp;

        /// <summary>Braços erguidos na comemoração (sobre a animação).</summary>
        void Arms()
        {
            float want = _celebrate > 0f && p.vel.magnitude < 4.5f ? 1f : 0f;
            _armsUp = Mathf.MoveTowards(_armsUp, want, Time.deltaTime * 3f);
            if (_armsUp <= 0.001f) return;
            float pump = 0.8f + 0.2f * Mathf.Sin(_celebrate * 9f);
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "_l" : "_r";
                rig.bones.TryGetValue("upperarm" + sfx, out var up);
                rig.bones.TryGetValue("lowerarm" + sfx, out var lo);
                rig.bones.TryGetValue("hand" + sfx, out var hd);
                if (up == null || lo == null || hd == null) continue;
                // lado do corpo: decide pelo ombro em relação ao peito
                float s = Mathf.Sign(Vector3.Dot(up.position - (rig.spine != null ? rig.spine.position : transform.position), transform.right));
                var target = (Vector3.up * 2.2f + transform.right * s * 0.9f + transform.forward * 0.15f).normalized;
                var cur = lo.position - up.position;
                _pose.Rotate(up, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, target), _armsUp * pump));
                var cur2 = hd.position - lo.position;
                _pose.Rotate(lo, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur2, (target + Vector3.up * 0.3f).normalized), _armsUp * 0.8f));
            }
        }

        Quaternion _neckSet, _headSet, _neckBase, _headBase, _look = Quaternion.identity;
        bool _headInit;

        /// <summary>
        /// Cabeça firme: parte da pose animada deste quadro (nunca acumula a correção do
        /// quadro anterior), amortece o balanço da captura e vira suavemente para a bola.
        /// </summary>
        void HeadAndNeck()
        {
            var neck = rig.neck;
            var head = rig.head;
            var nCur = neck != null ? neck.localRotation : Quaternion.identity;
            var hCur = head.localRotation;
            // se a animação não reescreveu os ossos neste quadro, usa a base anterior
            if (_headInit && Quaternion.Angle(hCur, _headSet) < 0.01f && (neck == null || Quaternion.Angle(nCur, _neckSet) < 0.01f))
            {
                nCur = _neckBase;
                hCur = _headBase;
            }
            _neckBase = nCur;
            _headBase = hCur;
            _headInit = true;
            // amortece o pescoço/cabeça da captura (metade do movimento, sem cabeça caída)
            if (neck != null) neck.localRotation = Quaternion.Slerp(rig.neckRest, nCur, 0.45f);
            head.localRotation = Quaternion.Slerp(rig.headRest, hCur, 0.4f);
            // olhar para a bola, suavizado
            var ballW = W(view.sim.ball.pos, view.sim.ball.z + 0.11f);
            var toBall = ballW - head.position;
            var fwd = transform.forward;
            float yawTo = Vector3.SignedAngle(Vector3.ProjectOnPlane(fwd, Vector3.up), Vector3.ProjectOnPlane(toBall, Vector3.up), Vector3.up);
            float busy = anim != null ? 1f - anim.ActionWeight * 0.8f : 1f;
            yawTo = Mathf.Clamp(yawTo, -55f, 55f) * busy;
            float pitchTo = Mathf.Clamp(-Mathf.Atan2(toBall.y, new Vector2(toBall.x, toBall.z).magnitude) * Mathf.Rad2Deg, -20f, 25f) * busy;
            var want = Quaternion.AngleAxis(yawTo * 0.7f, Vector3.up) * Quaternion.AngleAxis(pitchTo * 0.5f, transform.right);
            _look = Quaternion.Slerp(_look, want, 1f - Mathf.Exp(-Time.deltaTime * 5f));
            if (neck != null) neck.rotation = Quaternion.Slerp(Quaternion.identity, _look, 0.4f) * neck.rotation;
            head.rotation = Quaternion.Slerp(Quaternion.identity, _look, 0.6f) * head.rotation;
            _neckSet = neck != null ? neck.localRotation : Quaternion.identity;
            _headSet = head.localRotation;
        }

        /// <summary>Marcador do jogador controlado (anel sob os pés).</summary>
        public void SetMarker(Transform m) => _marker = m;
    }
}
