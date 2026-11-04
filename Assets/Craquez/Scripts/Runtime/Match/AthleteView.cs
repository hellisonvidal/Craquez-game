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
        float _yaw, _lean, _pitch, _hop, _hopT, _celebrate, _fall, _diveSide, _slideT;
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
        }

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
                    float rate = 1.25f;
                    string clip = e.kind == "shot" ? (p.windupPower > 0.6f ? "KickPower" : "Shot") : e.kind == "lob" ? "Kick" : "Pass";
                    float start = 1.1f - p.windupTotal * rate - 0.08f;
                    anim.Play(clip, start, 0.95f, rate, 0.06f);
                    break;
                }
                case "kick":
                    if (anim.CurrentAction == null || anim.ActionWeight < 0.3f)
                        anim.Play(e.kind == "shot" ? "Shot" : "Pass", 1.0f, 0.75f, 1.35f, 0.05f);
                    break;
                case "skill":
                    switch ((Skill)e.skill)
                    {
                        case Skill.Drag: anim.Play("Spin", 0.35f, 0.75f, 1.35f); break;
                        case Skill.Roulette: anim.Play("Spin", 0.2f, 0.95f, 1.2f); break;
                        case Skill.Roll: anim.Play(p.skillSide > 0 ? "CutL" : "CutR", 0.2f, 0.7f, 1.3f); break;
                        case Skill.Stepover: anim.Play(p.skillSide > 0 ? "FeintR" : "FeintL", 0.3f, 0.9f, 1.4f); break;
                    }
                    break;
                case "fooled":
                    anim.Play("QuickStop", 0.6f, 0.7f, 1.2f);
                    break;
                case "header":
                    _hopT = 0.55f;
                    break;
                case "tackle_try":
                    _pitch = 18f;
                    break;
                case "slide":
                    _slideT = 0f;
                    break;
                case "dive":
                    _diveSide = Vector3.Dot(W(view.sim.ball.vel), transform.right) >= 0 ? -1f : 1f;
                    break;
                case "goal":
                    _celebrate = 4f;
                    break;
            }
        }

        public void OnVictimFoul() => _fall = 1.1f;

        /// <summary>Comemoração: pulinhos e braços para cima por alguns segundos.</summary>
        public void Celebrate(float seconds) => _celebrate = Mathf.Max(_celebrate, seconds);

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
            float maxRate = Mathf.Lerp(900f, 340f, Mathf.Clamp01(spd / 8f));
            float step = Mathf.Clamp(diff * (1f - Mathf.Exp(-dt * 14f)), -maxRate * dt, maxRate * dt);
            _yaw += step;
            _yawRate = Mathf.Lerp(_yawRate, step / Mathf.Max(1e-4f, dt), 1f - Mathf.Exp(-dt * 8f));
            // inclinação nas curvas e na aceleração
            var acc = (p.vel - _lastVel) / Mathf.Max(1e-4f, dt);
            _lastVel = p.vel;
            var local = Quaternion.Euler(0, -_yaw, 0) * W(acc);
            // inclinação para dentro da curva (força centrípeta) + aceleração lateral
            float centri = -_yawRate * Mathf.Deg2Rad * spd * 1.6f;
            _lean = Mathf.Lerp(_lean, Mathf.Clamp(-local.x * 0.6f + centri, -16f, 16f), 1f - Mathf.Exp(-dt * 6f));
            _pitch = Mathf.Lerp(_pitch, 0f, 1f - Mathf.Exp(-dt * 5f));
            // aceleração inclina o tronco (na coluna, em LateUpdate), não o corpo inteiro: pés firmes
            _accLean = Mathf.Lerp(_accLean, Mathf.Clamp(local.z * 1.1f, -7f, 9f) + Mathf.Clamp01((spd - 6f) / 3f) * 4f, 1f - Mathf.Exp(-dt * 5f));
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
                pitch = Mathf.Lerp(pitch, -62f, k);
                lift = Mathf.Lerp(lift, 0.12f, k);
            }
            else if (p.isKeeper && p.dive > 0f)
            {
                float u = 1f - p.dive / 0.6f;
                roll = _diveSide * Mathf.Lerp(0f, 78f, Mathf.Clamp01(u * 3f));
                lift = Mathf.Sin(Mathf.Clamp01(u * 1.6f) * Mathf.PI) * 0.6f + 0.25f * Mathf.Clamp01(u * 3f);
            }
            else if (p.stun > 0.5f && _fall > 0f)
            {
                _fall -= dt;
                pitch = Mathf.Lerp(pitch, 75f, Mathf.Clamp01((1.1f - _fall) * 4f));
                lift = 0.15f * Mathf.Clamp01((1.1f - _fall) * 4f);
            }
            else if (p.offBalance > 0f)
            {
                roll += Mathf.Sin(Time.time * 18f) * 6f;
            }
            transform.position = pos + Vector3.up * lift;
            transform.rotation = Quaternion.Euler(pitch, _yaw, roll);
            // animação
            var v3 = W(p.vel);
            var lv = Quaternion.Euler(0, -_yaw, 0) * v3;
            bool guard = p.shielding && !p.isKeeper && view.sim.ball.owner >= 0 && view.sim.players[view.sim.ball.owner].team != p.team;
            float animSpeed = p.vel.magnitude, animFwd = lv.z;
            // girando parado: passinhos no lugar em vez de "patinar" sobre os pés
            float turnStep = Mathf.Clamp(Mathf.Abs(_yawRate) * 0.0065f, 0f, 1.3f);
            if (animSpeed < 1.2f && turnStep > 0.35f && !guard)
            {
                animSpeed = Mathf.Max(animSpeed, turnStep);
                animFwd = Mathf.Max(animFwd, animSpeed * 0.8f);
            }
            Cuts(dt, spd);
            Touches();
            _pose.Restore();   // tira as correções do quadro anterior antes de avaliar a animação
            anim?.Tick(dt, animSpeed, animFwd, lv.x, guard);
            if (!_diag && rig.head != null && p.id == 3)
            {
                _diag = true;
                Debug.Log($"[diag] atleta {p.name}: cabeça a {rig.head.position.y - transform.position.y:F2} m do chão (raiz), pés {rig.footL?.position.y - transform.position.y:F2} m; rotação do modelo {rig.model.localEulerAngles}");
            }
            if (_celebrate > 0f) _celebrate -= dt;
        }

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
            if (spd > 3.6f && Mathf.Abs(ang) > 70f && _cutCd <= 0f && (anim.CurrentAction == null || anim.ActionWeight < 0.2f) && p.slide <= 0f && !p.isKeeper)
            {
                anim.Play(ang > 0f ? "CutL" : "CutR", 0.28f, 0.5f, 1.4f, 0.1f);
                _cutCd = 0.9f;
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
                // a perna em balanço (pé mais alto) é a que toca
                _touchLeg = rig.footL != null && rig.footR != null && rig.footL.position.y > rig.footR.position.y ? 0 : 1;
            }
            _lastTouchT = p.touchT;
        }

        /// <summary>Correções por cima da animação: tronco na aceleração e perna do toque.</summary>
        void BodyLayer()
        {
            float dt = Time.deltaTime;
            if (rig.bones.TryGetValue("spine_01", out var sp1) && Mathf.Abs(_accLean) > 0.05f)
            {
                var ax = transform.right;
                _pose.Rotate(sp1, Quaternion.AngleAxis(_accLean * 0.55f, ax));
                _pose.Rotate(rig.spine, Quaternion.AngleAxis(_accLean * 0.45f, ax));
            }
            if (_touchAnim > 0f)
            {
                _touchAnim = Mathf.Max(0f, _touchAnim - dt / 0.24f);
                float k = Mathf.Sin((1f - _touchAnim) * Mathf.PI);
                string sfx = _touchLeg == 0 ? "_l" : "_r";
                rig.bones.TryGetValue("thigh" + sfx, out var th);
                rig.bones.TryGetValue("calf" + sfx, out var cf);
                var ax = transform.right;
                _pose.Rotate(th, Quaternion.AngleAxis(-26f * k, ax));
                _pose.Rotate(cf, Quaternion.AngleAxis(-16f * k, ax));
            }
        }

        float _ground;
        Transform _shadow;
        static Material _shadowMat;

        /// <summary>Pés sempre no chão: depois da animação, alinha o pé mais baixo ao piso.</summary>
        void GroundFeet()
        {
            if (rig == null || rig.model == null) return;
            Transform bl = null, br = null;
            rig.bones.TryGetValue("ball_l", out bl);
            rig.bones.TryGetValue("ball_r", out br);
            float lowest = float.MaxValue;
            foreach (var t in new[] { bl, br }) if (t != null) lowest = Mathf.Min(lowest, t.position.y - 0.035f);
            foreach (var t in new[] { rig.footL, rig.footR }) if (t != null) lowest = Mathf.Min(lowest, t.position.y - 0.085f);
            if (lowest == float.MaxValue) return;
            float lift = transform.position.y;   // pulos/mergulhos intencionais
            float err = lowest - lift;
            // suaviza para não tremer, mas corrige rápido
            _ground = Mathf.Lerp(_ground, err, 1f - Mathf.Exp(-Time.deltaTime * 18f));
            if (p.slide <= 0f && !(p.isKeeper && p.dive > 0f) && p.stun < 0.5f)
                rig.model.position -= Vector3.up * _ground;
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

        void LateUpdate()
        {
            if (p != null && rig != null) BodyLayer();
            GroundFeet();
            if (p == null || rig == null || rig.head == null) return;
            HeadAndNeck();
            Arms();
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
