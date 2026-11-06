using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Craquez
{
    /// <summary>
    /// Correções feitas por cima da animação (tronco, perna do toque, braços).
    /// Guarda a rotação animada de cada osso antes de mexer e a devolve no quadro
    /// seguinte, antes da animação ser avaliada: as correções nunca se acumulam,
    /// mesmo quando a animação não reescreve um osso (curva constante).
    /// </summary>
    public class PoseEdits
    {
        readonly List<Transform> _t = new List<Transform>();
        readonly List<Quaternion> _q = new List<Quaternion>();

        /// <summary>Gira o osso no espaço do mundo (delta * rotação atual).</summary>
        public void Rotate(Transform t, Quaternion worldDelta)
        {
            if (t == null) return;
            if (!_t.Contains(t)) { _t.Add(t); _q.Add(t.localRotation); }
            t.rotation = worldDelta * t.rotation;
        }

        public void Restore()
        {
            for (int i = 0; i < _t.Count; i++) if (_t[i] != null) _t[i].localRotation = _q[i];
            _t.Clear();
            _q.Clear();
        }
    }

    /// <summary>Correções de pose por cima da captura (testáveis sem a partida).</summary>
    public static class PoseFix
    {
        /// <summary>
        /// Pés firmes: a captura deixa o pé "pendurado" (ponta caída). No chão, o pé fica
        /// plano; no ar, a ponta não cai mais que ~30° do natural (chutes podem esticar mais).
        /// </summary>
        public static void Feet(AthleteRig rig, Transform root, PoseEdits pose, float act)
        {
            float rest = rig.footPitchRest;
            float ground = root.position.y;
            for (int side = 0; side < 2; side++)
            {
                var foot = side == 0 ? rig.footL : rig.footR;
                rig.bones.TryGetValue(side == 0 ? "ball_l" : "ball_r", out var toe);
                if (foot == null || toe == null) continue;
                float pitch = AthleteRig.FootPitch(foot, toe);
                float h = Mathf.Min(foot.position.y, toe.position.y + 0.05f) - ground;
                float stance = 1f - Mathf.Clamp01((h - 0.1f) / 0.12f);   // 1 = apoiado
                float lo = rest - Mathf.Lerp(30f, 55f, act), hi = rest + 25f;
                float target = Mathf.Lerp(Mathf.Clamp(pitch, lo, hi), rest + 2f, stance * (1f - act * 0.7f));
                float delta = target - pitch;
                if (Mathf.Abs(delta) < 0.5f) continue;
                var fwd = toe.position - foot.position;
                var axis = Vector3.Cross(fwd, Vector3.up);
                if (axis.sqrMagnitude < 1e-6f) continue;
                pose.Rotate(foot, Quaternion.AngleAxis(delta, axis.normalized));   // + levanta a ponta
            }
        }

        /// <summary>Abertura lateral do braço (graus) no referencial do atleta.</summary>
        public static float Abduction(AthleteRig rig, Transform root, int side, out float elev)
        {
            elev = 0f;
            rig.bones.TryGetValue(side < 0 ? "upperarm_l" : "upperarm_r", out var up);
            rig.bones.TryGetValue(side < 0 ? "lowerarm_l" : "lowerarm_r", out var lo);
            if (up == null || lo == null) return 0f;
            var arm = (lo.position - up.position).normalized;
            float s = Mathf.Sign(Vector3.Dot(up.position - (rig.spine != null ? rig.spine.position : root.position), root.right));
            elev = Vector3.Angle(arm, -root.up);
            return Mathf.Atan2(Vector3.Dot(arm, root.right) * s, Vector3.Dot(arm, -root.up)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Braços naturais: a captura (retargeted) abre e ergue os braços demais. Limita a
        /// abertura lateral (~16°) e a elevação (~50°), preservando o balanço da corrida.
        /// </summary>
        public static void Arms(AthleteRig rig, Transform root, PoseEdits pose, float w, float speed = 0f)
        {
            var right = root.right;
            var down = -root.up;
            for (int side = -1; side <= 1; side += 2)
            {
                rig.bones.TryGetValue(side < 0 ? "upperarm_l" : "upperarm_r", out var up);
                rig.bones.TryGetValue(side < 0 ? "lowerarm_l" : "lowerarm_r", out var lo);
                if (up == null || lo == null) continue;
                float s = Mathf.Sign(Vector3.Dot(up.position - (rig.spine != null ? rig.spine.position : root.position), right));
                float abd = Abduction(rig, root, side, out _);
                if (abd > 16f) pose.Rotate(up, Quaternion.AngleAxis(-(abd - 16f) * w * s, root.forward));   // (+ em torno da frente ergueria o braço direito)
                var arm = (lo.position - up.position).normalized;
                float elev = Vector3.Angle(arm, down);
                // correndo forte o braço sobe mais (balanço natural da corrida); parado/andando, menos
                float maxElev = Mathf.Lerp(50f, 78f, Mathf.Clamp01((speed - 3f) / 4f));
                if (elev > maxElev)
                {
                    var axis = Vector3.Cross(arm, down);
                    if (axis.sqrMagnitude > 1e-6f) pose.Rotate(up, Quaternion.AngleAxis((elev - maxElev) * w, axis.normalized));
                }
            }
        }
    }

    /// <summary>
    /// Mistura as animações de captura de movimento (CMU, transferidas para o
    /// esqueleto dos atletas) pelo Playables API:
    ///   • locomoção: parado / andar / trotar / correr / de costas / de lado,
    ///     com fase sincronizada e velocidade de reprodução casada com a do atleta;
    ///   • ações por cima: passe, chute, voleio, fintas, giro, cortes...
    /// </summary>
    public class AthleteAnimator
    {
        public static readonly string[] Loco = { "Idle", "Walk", "Jog", "Run", "Backpedal", "Jockey", "Sprint" };
        const int IIdle = 0, IWalk = 1, IJog = 2, IRun = 3, IBack = 4, IJockey = 5, ISprint = 6;

        public class ClipInfo
        {
            public AnimationClip clip;
            public float speed;   // m/s médio do clipe (anims.json)
            public bool loop;
        }

        static Dictionary<string, ClipInfo> _clips;

        public static Dictionary<string, ClipInfo> Clips
        {
            get
            {
                if (_clips != null) return _clips;
                _clips = new Dictionary<string, ClipInfo>();
                var meta = Resources.Load<TextAsset>("Athletes/anims");
                var md = meta != null ? Sim.MiniJson.Parse(meta.text) as Dictionary<string, object> : null;
                foreach (var c in Resources.LoadAll<AnimationClip>("Athletes/Athlete_Anims"))
                {
                    string n = c.name;
                    int bar = n.LastIndexOf('|');
                    if (bar >= 0) n = n.Substring(bar + 1);
                    if (n.StartsWith("__")) continue;
                    object m = null;
                    md?.TryGetValue(n, out m);
                    _clips[n] = new ClipInfo { clip = c, speed = Sim.MiniJson.F(m, "speed", 0f), loop = m is Dictionary<string, object> d && d.TryGetValue("loop", out var l) && l is bool b && b };
                }
                if (_clips.Count == 0) Debug.LogError("Nenhuma animação encontrada em Resources/Athletes/Athlete_Anims");
                return _clips;
            }
        }

        PlayableGraph _graph;
        AnimationMixerPlayable _top, _loco;
        readonly AnimationClipPlayable[] _locoP = new AnimationClipPlayable[Loco.Length];
        readonly float[] _locoW = new float[Loco.Length];
        readonly float[] _locoSpeed = new float[Loco.Length];
        AnimationClipPlayable _action;
        float _actionW, _actionT, _actionEnd, _actionRate, _fadeOut;
        float _phase;
        bool _valid;
        readonly float[] _locoTarget = new float[Loco.Length];
        static float[] _contact;   // fase (0..1) do apoio do pé esquerdo em cada clipe de locomoção
        static float[] _groundSpeed; // velocidade real dos pés no chão em cada clipe (m/s), medida na pose
        float _idleRate = 1f;
        readonly float[] _sp4 = new float[4], _sp5 = new float[5];
        static readonly int[] Ix4 = { IIdle, IWalk, IJog, IRun }, Ix5 = { IIdle, IWalk, IJog, IRun, ISprint };

        /// <summary>
        /// Acha, uma vez, em que ponto de cada ciclo o pé esquerdo toca o chão, para que
        /// andar/trotar/correr se misturem com as pernas na mesma fase (sem "tesoura").
        /// </summary>
        static void FindContacts(Animator animator)
        {
            _contact = new float[Loco.Length];
            _groundSpeed = new float[Loco.Length];
            var go = animator.gameObject;
            Transform fl = null, fr = null, pel = null;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "foot_l") fl = t;
                else if (t.name == "foot_r") fr = t;
                else if (t.name == "pelvis") pel = t;
            }
            if (fl == null || fr == null) return;
            for (int i = 0; i < Loco.Length; i++)
            {
                if (!Clips.TryGetValue(Loco[i], out var ci) || !ci.loop || i == IIdle) continue;
                float len = ci.clip.length, best = float.MaxValue, bt = 0f;
                const int N = 40;
                var rigRoot = go.GetComponentInParent<AthleteRig>();
                var fwd = rigRoot != null ? rigRoot.transform.forward : go.transform.forward;
                fwd.y = 0f;
                fwd.Normalize();
                var lp = new Vector3[N];
                var rp = new Vector3[N];
                for (int k = 0; k < N; k++)
                {
                    float t = len * k / N;
                    ci.clip.SampleAnimation(go, t);
                    var o = rigRoot != null ? rigRoot.transform.position : go.transform.position;
                    // quadril preso na horizontal, como no jogo (PinHips): mede a passada "no lugar"
                    if (pel != null) { var ph = pel.position; o.x = ph.x; o.z = ph.z; }
                    lp[k] = fl.position - o;
                    rp[k] = fr.position - o;
                    // apoio do pé esquerdo: pé esquerdo mais baixo e mais à frente que o direito
                    float d = (fl.position.y - fr.position.y) * 3f - Vector3.Dot(fl.position - fr.position, fwd);
                    if (d < best) { best = d; bt = (float)k / N; }
                }
                _contact[i] = bt;
                // velocidade do chão: quanto o pé de apoio corre para trás, por segundo
                float sum = 0f;
                int cnt = 0;
                foreach (var f in new[] { lp, rp })
                {
                    float minY = float.MaxValue;
                    foreach (var q in f) minY = Mathf.Min(minY, q.y);
                    for (int k = 0; k < N; k++)
                    {
                        var a = f[k];
                        var b = f[(k + 1) % N];
                        if (a.y > minY + 0.03f || b.y > minY + 0.03f) continue;
                        float back = -Vector3.Dot(b - a, fwd);
                        if (back <= 0f) continue;
                        sum += back / (len / N);
                        cnt++;
                    }
                }
                float gs = cnt >= 3 ? sum / cnt : 0f;
                if (i == IBack) gs = 0f;   // de costas a medida é ruidosa: usa o valor do json
                _groundSpeed[i] = gs;
                Debug.Log($"[anim] {Loco[i]}: apoio do pé esq. em {bt:F2} do ciclo, chão a {gs:F2} m/s (json {ci.speed:F2})");
            }
        }
        public string CurrentAction { get; private set; }
        public float ActionWeight => _actionW;

        public AthleteAnimator(Animator animator, string name, int seed = 0)
        {
            // cada atleta com a sua fase e o seu ritmo de respiração (sem todos em sincronia)
            uint h = (uint)(seed * 2654435761u + 12345u);
            _phase = (h % 1000u) / 1000f;
            _idleRate = 0.9f + ((h >> 10) % 200u) / 1000f;
            var clips = Clips;
            if (clips.Count == 0 || animator == null) return;
            if (_contact == null)
            {
                try { FindContacts(animator); }
                catch (System.Exception e) { Debug.LogWarning("contatos: " + e.Message); _contact = new float[Loco.Length]; }
            }
            _graph = PlayableGraph.Create("anim:" + name);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(_graph, "out", animator);
            _top = AnimationMixerPlayable.Create(_graph, 2);
            _loco = AnimationMixerPlayable.Create(_graph, Loco.Length);
            for (int i = 0; i < Loco.Length; i++)
            {
                if (!clips.TryGetValue(Loco[i], out var ci)) ci = clips.TryGetValue("Idle", out var idle) ? idle : null;
                if (ci == null) continue;
                _locoP[i] = AnimationClipPlayable.Create(_graph, ci.clip);
                _locoP[i].SetApplyFootIK(false);
                float ms = _groundSpeed != null ? Mathf.Abs(_groundSpeed[i]) : 0f;
                // usa a velocidade medida nos pés quando plausível (passada casada com o deslocamento)
                _locoSpeed[i] = Mathf.Max(0.01f, ms > 0.6f && ms < 11f ? ms : ci.speed);
                _graph.Connect(_locoP[i], 0, _loco, i);
            }
            // o pique tem de ser mais rápido que a corrida (a medida do clipe de pique é curta)
            _locoSpeed[ISprint] = Mathf.Max(_locoSpeed[ISprint], _locoSpeed[IRun] * 1.4f);
            _graph.Connect(_loco, 0, _top, 0);
            _top.SetInputWeight(0, 1f);
            output.SetSourcePlayable(_top);
            _valid = true;
        }

        public void Destroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
            _valid = false;
        }

        /// <summary>Dispara uma ação (clipe inteiro ou trecho). start/len em segundos do clipe.</summary>
        public void Play(string clip, float start = 0f, float len = -1f, float rate = 1f, float fadeIn = 0.08f)
        {
            if (!_valid || !Clips.TryGetValue(clip, out var ci)) return;
            if (_action.IsValid())
            {
                _graph.Disconnect(_top, 1);
                _action.Destroy();
            }
            _action = AnimationClipPlayable.Create(_graph, ci.clip);
            _action.SetApplyFootIK(false);
            _graph.Connect(_action, 0, _top, 1);
            _actionT = Mathf.Clamp(start, 0f, ci.clip.length);
            _actionEnd = len > 0f ? Mathf.Min(ci.clip.length, _actionT + len) : ci.clip.length;
            _actionRate = rate;
            _fadeOut = 0.18f;
            CurrentAction = clip;
            if (_actionW < 0.01f) _actionW = 0.01f;
            _fadeIn = Mathf.Max(0.01f, fadeIn);
        }

        float _fadeIn = 0.08f;

        public void Stop(float fade = 0.15f)
        {
            if (CurrentAction == null) return;
            _actionEnd = Mathf.Min(_actionEnd, _actionT + fade * _actionRate);
            _fadeOut = fade;
        }

        /// <summary>
        /// Atualiza a locomoção. speed em m/s; fwd/side = velocidade no referencial do corpo (m/s);
        /// guard = postura de marcação (contenção).
        /// </summary>
        public void Tick(float dt, float speed, float fwd, float side, bool guard)
        {
            if (!_valid) return;
            var _w = _locoTarget;
            for (int i = 0; i < _w.Length; i++) _w[i] = 0f;
            float back = Mathf.Clamp01(-fwd / Mathf.Max(0.4f, speed));
            // passo lateral só quando anda quase de lado (e devagar); correndo, gira o corpo
            float lateral = speed > 3.5f ? 0f : Mathf.Clamp01(Mathf.Abs(side) / Mathf.Max(0.4f, speed) - 0.35f) * 1.6f;
            if (speed < 0.25f)
            {
                if (guard) _w[IJockey] = 1f;
                else _w[IIdle] = 1f;
            }
            else if (guard || lateral > 0.85f)
            {
                float k = Mathf.Clamp01(speed / 1.2f);
                _w[IJockey] = k;
                _w[IIdle] = 1f - k;
            }
            else if (back > 0.55f && speed < 4.5f)
            {
                float k = Mathf.Clamp01(speed / 0.8f);
                _w[IBack] = k;
                _w[IIdle] = 1f - k;
            }
            else
            {
                // parado -> andar -> trotar -> correr -> pique (por velocidade)
                bool sprint = _locoP[ISprint].IsValid();
                // limiares = velocidade natural de cada passada (medida nos pés), ligeiramente acima
                var sp = sprint ? _sp5 : _sp4;
                sp[0] = 0f; sp[1] = _locoSpeed[IWalk]; sp[2] = _locoSpeed[IJog]; sp[3] = _locoSpeed[IRun];
                if (sprint) sp[4] = _locoSpeed[ISprint];
                for (int k = 1; k < sp.Length; k++) sp[k] = Mathf.Max(sp[k] * 1.12f, sp[k - 1] + 0.6f);
                var ix = sprint ? Ix5 : Ix4;
                float s = Mathf.Min(speed, 10.5f);
                int seg = 0;
                while (seg < sp.Length - 2 && s >= sp[seg + 1]) seg++;
                float t = Mathf.Clamp01((s - sp[seg]) / (sp[seg + 1] - sp[seg]));
                _w[ix[seg]] += 1f - t;
                _w[ix[seg + 1]] += t;
            }
            // transições suaves entre posturas (sem "pulos" de pose)
            float wk = 1f - Mathf.Exp(-dt * 9f);
            for (int i = 0; i < _locoW.Length; i++) _locoW[i] = Mathf.Lerp(_locoW[i], _w[i], wk);
            // fase única: avança pela distância percorrida / passada do clipe dominante
            float cyclesPerSec = 0f, wsum = 0f;
            for (int i = 0; i < Loco.Length; i++)
            {
                if (!_locoP[i].IsValid() || _locoW[i] <= 0f) continue;
                float len = (float)_locoP[i].GetAnimationClip().length;
                // ritmo casado com a velocidade (pés sem patinar), dentro de limites naturais
                float rate = (i == IIdle || speed < 0.2f) ? _idleRate : Mathf.Clamp(speed / _locoSpeed[i], 0.7f, 1.65f);
                cyclesPerSec += _locoW[i] * rate / Mathf.Max(0.1f, len);
                wsum += _locoW[i];
            }
            if (wsum > 0f) _phase = Mathf.Repeat(_phase + dt * cyclesPerSec / wsum, 1f);
            for (int i = 0; i < Loco.Length; i++)
            {
                if (!_locoP[i].IsValid()) continue;
                float len = (float)_locoP[i].GetAnimationClip().length;
                float off = (_contact != null && i != IIdle) ? _contact[i] : 0f;
                _locoP[i].SetTime(Mathf.Repeat(_phase + off, 1f) * len);
                _loco.SetInputWeight(i, _locoW[i]);
            }
            // ação por cima
            float target = 0f;
            if (CurrentAction != null)
            {
                _actionT += dt * _actionRate;
                if (_actionT >= _actionEnd)
                {
                    CurrentAction = null;
                }
                else
                {
                    target = 1f;
                    _action.SetTime(_actionT);
                    if (_actionEnd - _actionT < _fadeOut * _actionRate) target = (_actionEnd - _actionT) / (_fadeOut * _actionRate);
                }
            }
            float rateW = target > _actionW ? dt / _fadeIn : dt / 0.18f;
            _actionW = Mathf.MoveTowards(_actionW, target, rateW);
            if (_action.IsValid()) _top.SetInputWeight(1, _actionW);
            _top.SetInputWeight(0, 1f - _actionW);
            if (_actionW <= 0f && _action.IsValid() && CurrentAction == null)
            {
                _graph.Disconnect(_top, 1);
                _action.Destroy();
            }
            _graph.Evaluate(0f);
        }
    }
}
