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
                    if (e.kind == "gkthrow")
                    {
                        // goleiro: um braço só — por cima do ombro, ou rolando rente ao chão
                        _throwT = 0.32f; _oneArm = true; _throwLow = view.sim.ball.z < 0.5f;
                        if (_throwLow) _pitch = 22f;
                        break;
                    }
                    if (e.kind == "punt")
                    {
                        // chutão de bate-pronto: chute forte começando um pouco antes do contato
                        float pc = AthleteAnimator.KickContact("KickPower");
                        anim.Play("KickPower", pc - 0.12f, 0.7f, 1.3f, 0.04f);
                        break;
                    }
                    if (anim.CurrentAction == null || anim.ActionWeight < 0.3f)
                    {
                        string c = e.kind == "shot" ? "Shot" : "Pass";
                        float ct = AthleteAnimator.KickContact(c);
                        anim.Play(c, ct - 0.06f, 0.6f, 1.35f, 0.04f);
                    }
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
        const float FallStumble = 0.16f, FallDrop = 0.42f, FallRise = 0.75f;
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
                    // queda acelerada (gravidade) com pequeno quique no impacto
                    float k = u * u;
                    pitch = Mathf.Lerp(24f * fwd, lying, k);
                    roll += sideRoll * (0.5f + 0.5f * k);
                    lift = Mathf.Lerp(0f, 0.16f, k) + (u > 0.85f ? Mathf.Sin((u - 0.85f) / 0.15f * Mathf.PI) * 0.05f : 0f);
                    break;
                }
                case 3:
                {
                    // no chão: vira de lado segurando a perna e se contorce
                    float turn = Smooth(u * 3f);
                    float writhe = Mathf.Sin(Time.time * 5.5f + p.id) * 6f * (1f - u * 0.6f);
                    pitch = lying;
                    roll += _fallSide * Mathf.Lerp(sideRoll * _fallSide, 62f, turn) + writhe;
                    lift = 0.16f + turn * 0.06f;
                    break;
                }
                case 4:
                {
                    // levanta: volta de bruços, apoia um joelho e fica de pé
                    float k = Smooth(u);
                    pitch = Mathf.Lerp(lying, 0f, k);
                    roll += _fallSide * Mathf.Lerp(62f, 0f, Smooth(u * 2f));
                    lift = Mathf.Lerp(0.22f, 0f, k);
                    break;
                }
            }
            return new Vector3(pitch, roll, lift);
        }

        /// <summary>Pernas e braços na queda (por cima da animação).</summary>
        void FallLimbs()
        {
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
                else if (ph == 3 && knee != null && sfx == hurt) dir = (knee.position - up.position).normalized;   // segura a perna
                else if (ph == 3) dir = (transform.up * 0.3f + right * s * 0.8f - transform.forward * 0.4f * fwd).normalized;   // braço apoiado no chão
                else if (ph == 4)
                {
                    // levantando: empurra o chão com as mãos e depois os braços voltam para baixo
                    var push = (-Vector3.up * 0.8f + right * s * 0.4f + transform.forward * 0.2f).normalized;
                    dir = Vector3.Slerp(push, down, Smooth(u * 1.6f));
                }
                else if (fwd > 0f) dir = (transform.forward * 0.9f + transform.up * 0.35f + right * s * 0.35f).normalized;  // mãos à frente
                else dir = (-transform.forward * 0.7f - transform.up * 0.4f + right * s * 0.6f).normalized;             // mãos atrás
                var cur = lo.position - up.position;
                _pose.Rotate(up, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, dir), _fallW * 0.85f));
                var cur2 = hd.position - lo.position;
                _pose.Rotate(lo, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur2, dir), _fallW * 0.6f));
            }
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

        float _protest;

        /// <summary>Quem fez a falta reclama: braços abertos, palmas para cima.</summary>
        void Protest()
        {
            var sim = view.sim;
            bool want = sim.state == MatchState.Foul && sim.foulOffender == p.id && p.down <= 0f && p.slide <= 0f;
            _protest = Mathf.MoveTowards(_protest, want ? 1f : 0f, Time.deltaTime * 3f);
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
            _lean = Mathf.Lerp(_lean, Mathf.Clamp(-local.x * 0.3f + centri * 0.6f, -7f, 7f), 1f - Mathf.Exp(-dt * 10f));   // corpo firme: inclina pouco e sem atraso
            _pitch = Mathf.Lerp(_pitch, 0f, 1f - Mathf.Exp(-dt * 5f));
            // aceleração inclina o tronco (na coluna, em LateUpdate), não o corpo inteiro: pés firmes
            // tronco: inclina na arrancada e, em velocidade, fica um pouco à frente (como corredor de verdade)
            _accLean = Mathf.Lerp(_accLean, Mathf.Clamp(local.z * 0.5f, -3f, 4f) + Mathf.Clamp01((spd - 3f) / 5f) * 4f, 1f - Mathf.Exp(-dt * 9f));
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
            else if (p.isKeeper && (p.saveKind > 0 || _saveRec > 0f))
            {
                var sp = SavePose(dt);
                pitch += sp.x; roll += sp.y; lift += sp.z;
            }
            else if (p.down > 0f)
            {
                var fb = FallBody(pitch, roll, lift);
                pitch = fb.x; roll = fb.y; lift = fb.z;
            }
            else if (p.offBalance > 0f)
            {
                roll += Mathf.Sin(Time.time * 12f) * 2.5f;
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
            if (p.isKeeper && p.hold <= 0f && p.dive <= 0f && view.sim.ball.owner >= 0)
            {
                var ow = view.sim.players[view.sim.ball.owner];
                if (ow.team != p.team && ow.position.Dist(p.position) < (view.sim.mode.id == GameModes.Field ? 24f : 12f)) guard = true;
            }
            // velocidade da passada suavizada, com arrancada/freada de gente (sem trocar de
            // passada a cada passo da simulação nem "pular" de andar para correr)
            float rawSpd = p.vel.magnitude;
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
            anim?.Tick(dt, animSpeed, animFwd, rawSpd > 0.01f ? lv.x * (_animSpd / rawSpd) : 0f, guard);
            if (!_diag && rig.head != null && p.id == 3)
            {
                _diag = true;
                Debug.Log($"[diag] atleta {p.name}: cabeça a {rig.head.position.y - transform.position.y:F2} m do chão (raiz), pés {rig.footL?.position.y - transform.position.y:F2} m; rotação do modelo {rig.model.localEulerAngles}");
            }
            if (_celebrate > 0f) _celebrate -= dt;
        }

        // ------------------------------------------------------------ defesas do goleiro
        int _saveKindV;
        float _saveRec, _saveSideV, _saveU;

        /// <summary>
        /// Corpo na defesa (pitch, roll, lift): ponte = impulso de lado, voo com o corpo esticado e
        /// queda deitado de lado; encaixe = corpo atrás da bola; baixa = ajoelha sobre a bola;
        /// salto = sobe com os braços. Depois levanta (_saveRec).
        /// </summary>
        Vector3 SavePose(float dt)
        {
            if (p.saveKind > 0)
            {
                _saveKindV = p.saveKind;
                // lado do mergulho no corpo: + = para a direita do goleiro
                var sideW = new Vector3(0, 0, p.saveSide);
                _saveSideV = Vector3.Dot(sideW, transform.right) >= 0f ? 1f : -1f;
                _saveU = 1f - p.saveT / Mathf.Max(0.01f, p.saveTotal);
                _saveRec = 0.45f;
            }
            else
            {
                _saveRec = Mathf.Max(0f, _saveRec - dt);
            }
            float u = _saveU;
            float rec = p.saveKind > 0 ? 1f : _saveRec / 0.45f;   // 1 = em defesa .. 0 = de pé de novo
            float pitch = 0f, roll = 0f, lift = 0f;
            switch (_saveKindV)
            {
                case 3: case 4: case 5:
                {
                    // impulso (0-15%), voo (15-55%), queda e corpo no chão (55-100%)
                    float peak = _saveKindV == 3 ? 0.22f : _saveKindV == 4 ? 0.55f : 0.95f;
                    float lay = _saveKindV == 3 ? 88f : _saveKindV == 4 ? 80f : 70f;
                    float k = Smooth(u / 0.3f);
                    roll = -_saveSideV * lay * k;
                    float fly = u < 0.6f ? Mathf.Sin(Mathf.Clamp01(u / 0.6f) * Mathf.PI) * peak : 0f;
                    lift = fly + 0.28f * k;   // deitado de lado, o quadril fica a ~0,3 m do chão
                    pitch = -8f * k;          // peito um pouco virado para a bola
                    break;
                }
                case 2:   // baixa: ajoelha e cai sobre a bola
                    pitch = 34f * Smooth(u / 0.35f);
                    lift = -0.1f * Smooth(u / 0.35f);
                    break;
                case 6:   // salto no alto
                    lift = Mathf.Sin(Mathf.Clamp01(u / 0.7f) * Mathf.PI) * 0.45f;
                    pitch = -6f;
                    break;
                default:  // encaixe de frente: corpo atrás da bola, leve flexão
                    pitch = 10f * Smooth(u / 0.3f);
                    break;
            }
            return new Vector3(pitch, roll, lift) * rec;
        }

        /// <summary>Braços na defesa: as duas mãos vão juntas na bola (ou no ponto do mergulho).</summary>
        void SaveArms()
        {
            float w = p.saveKind > 0 ? Smooth(_saveU / 0.18f) : _saveRec / 0.45f;
            if (!p.isKeeper || _saveKindV == 0 || w <= 0.001f || rig.spine == null) return;
            var sim = view.sim;
            var ballW = W(sim.ball.pos, sim.ball.z + 0.11f);
            Vector3 target;
            var chest = rig.spine.position;
            if (_saveKindV >= 3)
            {
                // mergulho: braços esticados "acima da cabeça" no plano do voo, puxando para a bola
                var along = Quaternion.AngleAxis(-_saveSideV * (_saveKindV == 5 ? 18f : 6f), transform.forward) * (transform.up * 0.2f + transform.right * _saveSideV * 0.0f);
                var reach = (rig.head != null ? rig.head.position - chest : transform.up).normalized;
                target = chest + reach * 1.0f + transform.forward * 0.15f + along;
                if ((ballW - chest).magnitude < 2.2f) target = Vector3.Lerp(target, ballW, 0.6f);
            }
            else if (_saveKindV == 2) target = chest + transform.forward * 0.45f - Vector3.up * 0.75f;
            else target = (ballW - chest).magnitude < 3f ? ballW : chest + transform.forward * 0.5f + Vector3.up * (_saveKindV == 6 ? 0.8f : 0.1f);
            for (int side = -1; side <= 1; side += 2)
            {
                string sfx = side < 0 ? "_l" : "_r";
                rig.bones.TryGetValue("upperarm" + sfx, out var up);
                rig.bones.TryGetValue("lowerarm" + sfx, out var lo);
                rig.bones.TryGetValue("hand" + sfx, out var hd);
                if (up == null || lo == null || hd == null) continue;
                float s = Mathf.Sign(Vector3.Dot(up.position - chest, transform.right));
                // mãos lado a lado (formando o "W" atrás da bola)
                var handT = target + transform.right * s * 0.09f;
                var dirUp = handT - up.position;
                if (dirUp.sqrMagnitude < 1e-4f) continue;
                _pose.Rotate(up, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(lo.position - up.position, dirUp), w));
                _pose.Rotate(lo, Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(hd.position - lo.position, handT - lo.position), w));
            }
            // pernas na ponte: juntas e esticadas, a de cima um pouco dobrada
            if (_saveKindV >= 3)
            {
                rig.bones.TryGetValue(_saveSideV > 0 ? "calf_l" : "calf_r", out var topCalf);
                _pose.Rotate(topCalf, Quaternion.AngleAxis(25f * w, transform.right));
            }
            else if (_saveKindV == 2)
            {
                foreach (var b in new[] { "calf_l", "calf_r" })
                    if (rig.bones.TryGetValue(b, out var cf)) _pose.Rotate(cf, Quaternion.AngleAxis(-60f * w, transform.right));
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
            // braços: o esquerdo abre (chute de direita), o direito recua
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

        /// <summary>Correções por cima da animação: tronco na aceleração e perna do toque.</summary>
        void BodyLayer()
        {
            float dt = Time.deltaTime;
            if (rig.bones.TryGetValue("spine_01", out var sp1) && Mathf.Abs(_accLean) > 0.05f)
            {
                var ax = transform.right;
                // o tronco inclina como um bloco (quadril), não dobrando a coluna em arco
                _pose.Rotate(sp1, Quaternion.AngleAxis(_accLean * 0.8f, ax));
                _pose.Rotate(rig.spine, Quaternion.AngleAxis(_accLean * 0.2f, ax));
            }
            ArmsDown();
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

        void FeetFirm()
        {
            if (p.slide > 0f || (p.isKeeper && p.dive > 0f) || p.down > 0f) return;
            PoseFix.Feet(rig, transform, _pose, anim != null ? anim.ActionWeight : 0f);
        }

        void ArmsDown()
        {
            if (_celebrate > 0f || (p.isKeeper && (p.dive > 0f || p.hold > 0f)) || p.down > 0f) return;
            PoseFix.Arms(rig, transform, _pose, 1f - (anim != null ? anim.ActionWeight * 0.5f : 0f), p.vel.magnitude);
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
            // (sem arrays temporários: roda para cada atleta a cada quadro)
            if (bl != null) lowest = Mathf.Min(lowest, bl.position.y - 0.035f);
            if (br != null) lowest = Mathf.Min(lowest, br.position.y - 0.035f);
            if (rig.footL != null) lowest = Mathf.Min(lowest, rig.footL.position.y - 0.085f);
            if (rig.footR != null) lowest = Mathf.Min(lowest, rig.footR.position.y - 0.085f);
            if (lowest == float.MaxValue) return;
            float lift = transform.position.y;   // pulos/mergulhos intencionais
            float err = lowest - lift;
            // suaviza para não tremer, mas corrige rápido
            _ground = Mathf.Lerp(_ground, err, 1f - Mathf.Exp(-Time.deltaTime * 18f));
            if (p.slide <= 0f && !(p.isKeeper && p.dive > 0f) && p.stun < 0.5f && p.down <= 0f)
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
            if (p != null && rig != null)
            {
                // ações (cortes, fintas, giro) mantêm um pouco do balanço do quadril; locomoção, nada
                rig.PinHips(anim != null ? anim.ActionWeight * 0.25f : 0f);
                BodyLayer();
                FallLimbs();
                Protest();
                PointArm();
            }
            GroundFeet();
            Cloth();
            if (p == null || rig == null || rig.head == null) return;
            HeadAndNeck();
            Arms();
            SaveArms();
            KeeperHold();
            ThrowArms();
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
            IdDrag = Shader.PropertyToID("_ClothDrag"), IdFlutter = Shader.PropertyToID("_ClothFlutter");
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
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
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
            _clothDragV += ((target - _clothDrag) * 260f - _clothDragV * 32f) * dt;
            _clothDrag += _clothDragV * dt;
            float spd = new Vector2(_clothVel.x, _clothVel.z).magnitude;
            float flutAmp = 0.0004f + Mathf.Min(spd, 9f) * 0.00025f;   // só a barra tremula de leve
            float freq = 9f + spd * 1.3f;
            if (rig.shirt != null)
            {
                float top = (rig.spine != null ? rig.spine.position.y : pos.y + 0.4f) + 0.02f;
                rig.shirt.GetPropertyBlock(_mpb);
                _mpb.SetFloat(IdInflate, 0.007f);
                _mpb.SetVector(IdBand, new Vector4(top, pos.y - 0.14f, 0, 0));
                _mpb.SetVector(IdDrag, _clothDrag);
                _mpb.SetVector(IdFlutter, new Vector4(flutAmp, freq, _clothPhase, 0));
                rig.shirt.SetPropertyBlock(_mpb);
            }
            if (rig.shorts != null)
            {
                rig.shorts.GetPropertyBlock(_mpb);
                _mpb.SetFloat(IdInflate, 0.008f);
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
            bool holding = p.isKeeper && p.hold > 0f && sim.ball.owner == p.id;
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
