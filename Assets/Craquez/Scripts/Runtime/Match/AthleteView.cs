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
            anim = new AthleteAnimator(r.animator, sp.name);
            _prev = _cur = W(sp.position);
            _yaw = Yaw(sp.facing);
            transform.position = _cur;
        }

        void OnDestroy() => anim?.Destroy();

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

        void Update()
        {
            if (p == null) return;
            float a = view != null ? view.Alpha : 1f;
            float dt = Time.deltaTime;
            var pos = Vector3.Lerp(_prev, _cur, a);
            // rumo suave (o corpo gira um pouco atrás da direção da simulação)
            float target = Yaw(p.facing);
            _yaw = Mathf.LerpAngle(_yaw, target, 1f - Mathf.Exp(-dt * 16f));
            // inclinação nas curvas e na aceleração
            var acc = (p.vel - _lastVel) / Mathf.Max(1e-4f, dt);
            _lastVel = p.vel;
            var local = Quaternion.Euler(0, -_yaw, 0) * W(acc);
            _lean = Mathf.Lerp(_lean, Mathf.Clamp(-local.x * 0.9f, -12f, 12f), 1f - Mathf.Exp(-dt * 6f));
            _pitch = Mathf.Lerp(_pitch, 0f, 1f - Mathf.Exp(-dt * 5f));
            float lift = 0f, roll = _lean, pitch = _pitch + Mathf.Clamp(local.z * 0.35f, -4f, 6f);
            if (_celebrate > 0f && p.vel.magnitude < 3f)
                lift += Mathf.Abs(Mathf.Sin(_celebrate * 7f)) * 0.22f * Mathf.Clamp01(_celebrate);
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
            anim?.Tick(dt, p.vel.magnitude, lv.z, lv.x, guard);
            if (!_diag && rig.head != null && p.id == 3)
            {
                _diag = true;
                Debug.Log($"[diag] atleta {p.name}: cabeça a {rig.head.position.y - transform.position.y:F2} m do chão (raiz), pés {rig.footL?.position.y - transform.position.y:F2} m; rotação do modelo {rig.model.localEulerAngles}");
            }
            if (_celebrate > 0f) _celebrate -= dt;
        }

        void LateUpdate()
        {
            if (p == null || rig == null || rig.head == null) return;
            // cabeça acompanha a bola (limitado)
            var ballW = W(view.sim.ball.pos, view.sim.ball.z + 0.11f);
            var toBall = ballW - rig.head.position;
            var fwd = transform.forward;
            float yawTo = Vector3.SignedAngle(Vector3.ProjectOnPlane(fwd, Vector3.up), Vector3.ProjectOnPlane(toBall, Vector3.up), Vector3.up);
            yawTo = Mathf.Clamp(yawTo, -60f, 60f) * (anim != null ? 1f - anim.ActionWeight * 0.7f : 1f);
            float pitchTo = Mathf.Clamp(-Mathf.Atan2(toBall.y, new Vector2(toBall.x, toBall.z).magnitude) * Mathf.Rad2Deg, -25f, 35f);
            var add = Quaternion.AngleAxis(yawTo * 0.6f, Vector3.up) * Quaternion.AngleAxis(pitchTo * 0.4f, transform.right);
            if (rig.neck != null) rig.neck.rotation = Quaternion.Slerp(Quaternion.identity, add, 0.4f) * rig.neck.rotation;
            rig.head.rotation = Quaternion.Slerp(Quaternion.identity, add, 0.6f) * rig.head.rotation;
        }

        /// <summary>Marcador do jogador controlado (anel sob os pés).</summary>
        public void SetMarker(Transform m) => _marker = m;
    }
}
