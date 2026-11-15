using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>Referências do atleta instanciado: ossos, malhas e número da camisa.</summary>
    public class AthleteRig : MonoBehaviour
    {
        public Transform model;
        public SkinnedMeshRenderer body, shirt, shorts;
        /// <summary>Camisa por fora do calção (10% dos atletas); os outros com a camisa por dentro.</summary>
        public bool untucked;
        public Animator animator;
        public readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        public Transform head, neck, spine, pelvis, footL, footR, toeL, toeR, handL, handR, forearmL, forearmR;
        /// <summary>Pontos virtuais (sockets) para condução de bola e encaixe do goleiro.</summary>
        public Transform ballControlPoint, gkCatchSocket;
        /// <summary>Colisor opcional de corpo-a-corpo no Root (ignora camada Ball).</summary>
        public CapsuleCollider rootCollider;
        /// <summary>Rotação que alinha a frente do modelo com +Z do objeto raiz.</summary>
        public Quaternion modelFix = Quaternion.identity;
        /// <summary>Rotações locais de repouso do pescoço e da cabeça.</summary>
        public Quaternion headRest = Quaternion.identity, neckRest = Quaternion.identity;
        /// <summary>Posição de repouso do quadril no referencial do atleta (para "correr no lugar").</summary>
        public Vector3 pelvisRest;
        /// <summary>Inclinação do pé (tornozelo -> ponta) em repouso, em graus (negativo = ponta para baixo).</summary>
        public float footPitchRest;

        public static float FootPitch(Transform foot, Transform ball)
        {
            if (foot == null || ball == null) return 0f;
            var d = ball.position - foot.position;
            return Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        }

        Transform Bone(string primary, string mixamoFallback = null)
        {
            if (bones.TryGetValue(primary, out var t)) return t;
            if (!string.IsNullOrEmpty(mixamoFallback) && bones.TryGetValue(mixamoFallback, out var m)) return m;
            return null;
        }

        static readonly (string alias, string mixamo)[] MixamoAliases =
        {
            ("pelvis", "mixamorig:Hips"),
            ("spine_01", "mixamorig:Spine"),
            ("spine_02", "mixamorig:Spine1"),
            ("spine_03", "mixamorig:Spine2"),
            ("neck_01", "mixamorig:Neck"),
            ("head", "mixamorig:Head"),
            ("clavicle_l", "mixamorig:LeftShoulder"),
            ("clavicle_r", "mixamorig:RightShoulder"),
            ("upperarm_l", "mixamorig:LeftArm"),
            ("upperarm_r", "mixamorig:RightArm"),
            ("lowerarm_l", "mixamorig:LeftForeArm"),
            ("lowerarm_r", "mixamorig:RightForeArm"),
            ("hand_l", "mixamorig:LeftHand"),
            ("hand_r", "mixamorig:RightHand"),
            ("thigh_l", "mixamorig:LeftUpLeg"),
            ("thigh_r", "mixamorig:RightUpLeg"),
            ("calf_l", "mixamorig:LeftLeg"),
            ("calf_r", "mixamorig:RightLeg"),
            ("foot_l", "mixamorig:LeftFoot"),
            ("foot_r", "mixamorig:RightFoot"),
            ("ball_l", "mixamorig:LeftToeBase"),
            ("ball_r", "mixamorig:RightToeBase"),
        };

        void AliasMixamoBones()
        {
            foreach (var (alias, mix) in MixamoAliases)
                if (!bones.ContainsKey(alias) && bones.TryGetValue(mix, out var t)) bones[alias] = t;
        }

        public void Init(Transform m)
        {
            model = m;
            foreach (var t in m.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
            AliasMixamoBones();
            head = Bone("head", "mixamorig:Head");
            neck = Bone("neck_01", "mixamorig:Neck");
            if (head != null) headRest = head.localRotation;
            if (neck != null) neckRest = neck.localRotation;
            spine = Bone("spine_03", "mixamorig:Spine2");
            pelvis = Bone("pelvis", "mixamorig:Hips");
            footL = Bone("foot_l", "mixamorig:LeftFoot");
            footR = Bone("foot_r", "mixamorig:RightFoot");
            toeL = Bone("ball_l", "mixamorig:LeftToeBase");
            toeR = Bone("ball_r", "mixamorig:RightToeBase");
            handL = Bone("hand_l", "mixamorig:LeftHand");
            handR = Bone("hand_r", "mixamorig:RightHand");
            forearmL = Bone("lowerarm_l", "mixamorig:LeftForeArm");
            forearmR = Bone("lowerarm_r", "mixamorig:RightForeArm");
            // os clipes vêm de um FBX só com o esqueleto: a Unity remove o nó raiz e os caminhos
            // começam em "Root/..."; no modelo eles ficam sob "Armature". O Animator vai no
            // "Armature" para os caminhos baterem (antes nenhuma curva encontrava os ossos).
            bool isMeshy = bones.ContainsKey("mixamorig:Hips");
            var arm = Bone("Armature") ?? Bone("SmartRigArmature");
            if (!isMeshy)
            {
                var animHost = arm != null && arm != m ? arm.gameObject : m.gameObject;
                var old = m.GetComponent<Animator>();
                if (old != null && animHost != m.gameObject) old.enabled = false;
                animator = animHost.GetComponent<Animator>();
                if (animator == null) animator = animHost.AddComponent<Animator>();   // (?? não vê o "nulo" da Unity)
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // ossos sempre atualizados (evita cabeça "solta")
            }
            if (head != null)
                Debug.Log($"[diag] modelo {m.name}: cabeça em repouso a {head.position.y - transform.position.y:F2} m; filhos {m.childCount}; Armature rot {(arm != null ? arm.localEulerAngles.ToString() : "-")}");
            // descobre para onde o modelo olha e corrige para +Z
            Vector3 fwdDir = Vector3.zero;
            if (isMeshy && bones.TryGetValue("upperarm_l", out var ual) && bones.TryGetValue("upperarm_r", out var uar))
            {
                var armR = uar.position - ual.position; armR.y = 0f;
                if (armR.sqrMagnitude > 1e-6f) fwdDir = Vector3.Cross(Vector3.up, armR.normalized);
            }
            else
            {
                if (footL != null && toeL != null)
                {
                    var f = toeL.position - footL.position; f.y = 0f;
                    if (f.sqrMagnitude > 1e-6f) fwdDir += f.normalized;
                }
                if (footR != null && toeR != null)
                {
                    var f = toeR.position - footR.position; f.y = 0f;
                    if (f.sqrMagnitude > 1e-6f) fwdDir += f.normalized;
                }
            }
            if (fwdDir.sqrMagnitude > 1e-6f)
            {
                modelFix = Quaternion.FromToRotation(fwdDir.normalized, Vector3.forward);
                m.localRotation = modelFix * m.localRotation;
            }
            if (pelvis != null) pelvisRest = transform.InverseTransformPoint(pelvis.position);
            footPitchRest = FootPitch(footL, toeL);
            // pés no chão
            if (footL != null)
            {
                float minY = float.MaxValue;
                foreach (var t in new[] { footL, footR, toeL, toeR })
                    if (t != null) minY = Mathf.Min(minY, t.position.y);
                if (minY < 0.3f && minY > -0.3f) m.localPosition += Vector3.up * (0.035f - (minY - transform.position.y));
            }
            if (isMeshy) SetupMeshyDriver(m);
            ConfigureHierarchyAndSockets();
        }

        struct DriverNodeSpec
        {
            public string name;
            public int parent;
            public Vector3 pos;
            public Quaternion rot;
            public Vector3 scale;
        }

        struct DriverLink
        {
            public Transform src;
            public Transform dst;
            public Quaternion restOffset;
        }

        static DriverNodeSpec[] _driverSpec;
        static bool _driverSpecTried;

        Transform _driverRoot, _driverHips, _meshyHips;
        DriverLink[] _driverLinks;
        Vector3 _driverPelvisRest, _meshyPelvisRest;
        float _hipHeightScale = 1f;
        bool _driverInitScaled;

        static DriverNodeSpec[] EnsureDriverSpec()
        {
            if (_driverSpecTried) return _driverSpec;
            _driverSpecTried = true;
            GameObject proto = null;
            foreach (var r in new[] { "Athletes/Athlete_latino", "Athletes/Athlete_europeu", "Athletes/Athlete_afro", "Athletes/Athlete_asiatico" })
            {
                proto = Resources.Load<GameObject>(r);
                if (proto != null) break;
            }
            if (proto == null) return null;
            Transform arm = null;
            foreach (var t in proto.GetComponentsInChildren<Transform>(true))
                if (t.name == "Armature") { arm = t; break; }
            if (arm == null) return null;

            var list = new List<DriverNodeSpec>(64);
            void Collect(Transform t, int parentIdx)
            {
                if (t.GetComponent<Renderer>() != null) return;
                int myIdx = list.Count;
                list.Add(new DriverNodeSpec
                {
                    name = t.name,
                    parent = parentIdx,
                    pos = t.localPosition,
                    rot = t.localRotation,
                    scale = t.localScale,
                });
                for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i), myIdx);
            }
            Collect(arm, -1);
            _driverSpec = list.ToArray();
            return _driverSpec;
        }

        void SetupMeshyDriver(Transform m)
        {
            foreach (var a in m.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            var spec = EnsureDriverSpec();
            if (spec == null || spec.Length == 0) return;

            var drvGo = new GameObject("AnimDriver");
            _driverRoot = drvGo.transform;
            _driverRoot.SetParent(transform, false);

            var nodes = new Transform[spec.Length];
            var byName = new Dictionary<string, Transform>(spec.Length);
            for (int i = 0; i < spec.Length; i++)
            {
                var sp = spec[i];
                var go = new GameObject(sp.name);
                var tr = go.transform;
                tr.SetParent(sp.parent < 0 ? _driverRoot : nodes[sp.parent], false);
                tr.localPosition = sp.pos;
                tr.localRotation = sp.rot;
                tr.localScale = sp.scale;
                nodes[i] = tr;
                if (!byName.ContainsKey(sp.name)) byName[sp.name] = tr;
            }

            // Alinha o driver para +Z na pose de repouso (mesma referência de frente do modelo Meshy)
            byName.TryGetValue("foot_l", out var dFl);
            byName.TryGetValue("ball_l", out var dTl);
            byName.TryGetValue("foot_r", out var dFr);
            byName.TryGetValue("ball_r", out var dTr);
            Vector3 dFwd = Vector3.zero;
            if (dFl != null && dTl != null)
            {
                var f = dTl.position - dFl.position; f.y = 0f;
                if (f.sqrMagnitude > 1e-6f) dFwd += f.normalized;
            }
            if (dFr != null && dTr != null)
            {
                var f = dTr.position - dFr.position; f.y = 0f;
                if (f.sqrMagnitude > 1e-6f) dFwd += f.normalized;
            }
            if (dFwd.sqrMagnitude > 1e-6f)
                _driverRoot.localRotation = Quaternion.FromToRotation(dFwd.normalized, Vector3.forward);

            animator = nodes[0].gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            byName.TryGetValue("pelvis", out _driverHips);
            _meshyHips = pelvis;
            if (_driverHips != null && _meshyHips != null)
            {
                _driverPelvisRest = _driverRoot.InverseTransformPoint(_driverHips.position);
                _meshyPelvisRest = m.InverseTransformPoint(_meshyHips.position);
                float dh = _driverHips.position.y - transform.position.y;
                float mh = _meshyHips.position.y - transform.position.y;
                _hipHeightScale = Mathf.Clamp(mh / Mathf.Max(0.2f, dh), 0.75f, 1.35f);
            }

            var childMap = new Dictionary<string, string>
            {
                { "upperarm_l", "lowerarm_l" }, { "lowerarm_l", "hand_l" },
                { "upperarm_r", "lowerarm_r" }, { "lowerarm_r", "hand_r" },
                { "thigh_l", "calf_l" }, { "calf_l", "foot_l" },
                { "thigh_r", "calf_r" }, { "calf_r", "foot_r" },
            };
            var armAlign = new Dictionary<string, Quaternion>();
            Quaternion LimbAlign(string alias, Transform src, Transform dst)
            {
                if (alias == "hand_l" && armAlign.TryGetValue("lowerarm_l", out var alL)) return alL;
                if (alias == "hand_r" && armAlign.TryGetValue("lowerarm_r", out var alR)) return alR;
                if (!childMap.TryGetValue(alias, out var chName)) return Quaternion.identity;
                if (!byName.TryGetValue(chName, out var srcCh) || !bones.TryGetValue(chName, out var dstCh))
                    return Quaternion.identity;
                Vector3 sd = srcCh.position - src.position;
                Vector3 dd = dstCh.position - dst.position;
                if (sd.sqrMagnitude < 1e-6f || dd.sqrMagnitude < 1e-6f) return Quaternion.identity;
                var q = Quaternion.FromToRotation(dd.normalized, sd.normalized);
                if (alias.StartsWith("lowerarm")) armAlign[alias] = q;
                return q;
            }

            var links = new List<DriverLink>(MixamoAliases.Length);
            foreach (var (alias, mix) in MixamoAliases)
            {
                if (!byName.TryGetValue(alias, out var src) || !bones.TryGetValue(mix, out var dst)) continue;
                Quaternion align = LimbAlign(alias, src, dst);
                links.Add(new DriverLink
                {
                    src = src,
                    dst = dst,
                    restOffset = Quaternion.Inverse(src.rotation) * (align * dst.rotation),
                });
            }
            _driverLinks = links.ToArray();
        }

        /// <summary>Copia a pose avaliada no esqueleto MakeHuman oculto para os ossos Mixamo do Meshy.</summary>
        public void SyncDriver()
        {
            if (_driverLinks == null || model == null) return;
            if (!_driverInitScaled)
            {
                _driverInitScaled = true;
                if (_meshyHips != null)
                    pelvisRest = transform.InverseTransformPoint(model.TransformPoint(_meshyPelvisRest));
            }
            if (_driverHips != null && _meshyHips != null && _driverRoot != null)
            {
                Vector3 dPelvis = _driverRoot.InverseTransformPoint(_driverHips.position) - _driverPelvisRest;
                _meshyHips.position = model.TransformPoint(_meshyPelvisRest + dPelvis * _hipHeightScale);
            }
            for (int i = 0; i < _driverLinks.Length; i++)
            {
                var ln = _driverLinks[i];
                if (ln.src != null && ln.dst != null)
                    ln.dst.rotation = ln.src.rotation * ln.restOffset;
            }
        }

        /// <summary>
        /// Camada 1 (Root/Collision) + Camada 3 (Sockets virtuais):
        /// isola qualquer colisor de tronco da camada Ball e cria BallControlPoint / GK_CatchSocket.
        /// </summary>
        void ConfigureHierarchyAndSockets()
        {
            int bodyLayer = LayerMask.NameToLayer("PlayerBody");
            int ballLayer = LayerMask.NameToLayer("Ball");
            if (bodyLayer >= 0)
            {
                gameObject.layer = bodyLayer;
                if (ballLayer >= 0) Physics.IgnoreLayerCollision(bodyLayer, ballLayer, true);
            }
            rootCollider = GetComponent<CapsuleCollider>();
            if (rootCollider != null)
            {
                // Mantém a cápsula restrita ao eixo do tronco para corpo-a-corpo sem rebater a bola.
                rootCollider.radius = Mathf.Min(rootCollider.radius, 0.28f);
                rootCollider.height = Mathf.Max(rootCollider.height, 1.72f);
                rootCollider.center = new Vector3(0f, rootCollider.height * 0.5f, 0f);
            }
            if (ballControlPoint == null)
            {
                var existing = transform.Find("BallControlPoint");
                if (existing != null) ballControlPoint = existing;
                else
                {
                    var go = new GameObject("BallControlPoint");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(0f, 0.11f, 0.40f);
                    ballControlPoint = go.transform;
                }
            }
            if (gkCatchSocket == null)
            {
                var parent = spine != null ? spine : transform;
                var existing = parent.Find("GK_CatchSocket");
                if (existing != null) gkCatchSocket = existing;
                else
                {
                    var go = new GameObject("GK_CatchSocket");
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = new Vector3(0f, -0.05f, 0.30f);
                    gkCatchSocket = go.transform;
                }
            }
            UpdateSockets();
        }

        /// <summary>Atualiza a posição do GK_CatchSocket entre handL e handR à frente do tronco.</summary>
        public void UpdateSockets()
        {
            if (gkCatchSocket == null) return;
            if (handL != null && handR != null)
            {
                var mid = (handL.position + handR.position) * 0.5f;
                gkCatchSocket.position = mid + transform.forward * 0.06f;
            }
            else if (spine != null)
            {
                gkCatchSocket.position = spine.position + transform.forward * 0.30f - Vector3.up * 0.08f;
            }
        }

        /// <summary>
        /// Os clipes de captura trazem parte do deslocamento no quadril (a corrida anda ~1 m
        /// e volta a cada ciclo: pés "patinando"). Prende o quadril na horizontal; a
        /// simulação é quem desloca o atleta. keep = fração do movimento do clipe mantida.
        /// </summary>
        public void PinHips(float keep)
        {
            if (pelvis == null) return;
            var lp = transform.InverseTransformPoint(pelvis.position);
            lp.x = Mathf.Lerp(pelvisRest.x, lp.x, keep);
            lp.z = Mathf.Lerp(pelvisRest.z, lp.z, keep);
            pelvis.position = transform.TransformPoint(lp);
        }

        // espessura (largura/profundidade) de cada osso para o biotipo: (osso, magro, forte).
        // O modelo base já é encorpado: o "médio" afina um pouco; o forte fica perto do original.
        // Inclui pelvis/hips para largura do quadril, mas NÃO escala ossos que dobram (coxa, canela, braço, antebraço)
        // para evitar entortar joelho/cotovelo. A forma da perna/braço fica do modelo original.
        static readonly (string bone, float slim, float strong)[] Shape =
        {
            // tronco e pescoço
            ("spine_01", 0.80f, 1.08f), ("spine_02", 0.88f, 1.12f), ("spine_03", 0.94f, 1.16f),
            ("clavicle_l", 0.94f, 1.08f), ("clavicle_r", 0.94f, 1.08f), ("neck_01", 0.96f, 1.10f),
            // quadril/ombros para largura corporal (escala apenas espessura, não comprimento)
            ("pelvis", 0.88f, 1.12f), ("mixamorig:Hips", 0.88f, 1.12f),
        };

        /// <summary>
        /// Biotipo (0 = magro .. 1 = forte): escala os ossos só na espessura e compensa nos filhos.
        /// Chamar uma vez, logo depois de Init.
        /// </summary>
        public void Physique(float build, float frame = 0.5f)
        {
            build = Mathf.Clamp01(build);
            // ombros/caixa torácica: estreitos (0) .. largos (1), independente do porte
            float fr = Mathf.Lerp(0.98f, 1.12f, Mathf.Clamp01(frame));
            // cabeça: proporção real ~1:7.5 da altura; modelo base tem cabeça grande
            // magros têm cabeça proporcionalmente maior; fortes têm cabeça menor
            if (head != null)
            {
                float headScale = Mathf.Lerp(0.96f, 0.88f, build); // magro 0.96, forte 0.88
                head.localScale *= headScale;
            }
            foreach (var (name, slim, strong) in Shape)
            {
                float k = Mathf.Lerp(slim, strong, build);
                // ombros e quadril recebem influência do frame (largura)
                if (name == "spine_03" || name.StartsWith("clavicle") || name == "pelvis" || name == "mixamorig:Hips")
                    k *= fr;
                if (!bones.TryGetValue(name, out var b) || b.childCount == 0) continue;
                // eixo do comprimento: o que aponta para o primeiro osso filho
                Transform child = null;
                foreach (Transform c in b) if (c.GetComponent<Renderer>() == null) { child = c; break; }
                if (child == null) continue;
                var dir = b.InverseTransformDirection(child.position - b.position).normalized;
                int len = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? (Mathf.Abs(dir.x) > Mathf.Abs(dir.z) ? 0 : 2) : (Mathf.Abs(dir.y) > Mathf.Abs(dir.z) ? 1 : 2);
                var sc = Vector3.one * k;
                // comprimento: clavícula mais longa = ombros mais largos; pescoço um pouco mais curto
                // pelvis/hips NÃO alonga (mantém comprimento), só alarga
                if (name.StartsWith("clavicle"))
                    sc[len] = Mathf.Lerp(1.0f, 1.05f, Mathf.Clamp01(frame * 0.6f + build * 0.4f));
                else if (name == "neck_01")
                    sc[len] = 0.94f;
                else if (name == "pelvis" || name == "mixamorig:Hips")
                    sc[len] = 1f; // não alonga o quadril
                b.localScale = Vector3.Scale(b.localScale, sc);
                // filhos: desfaz a escala do pai no referencial de cada filho
                foreach (Transform c in b)
                {
                    var inv = Vector3.one;
                    for (int i = 0; i < 3; i++)
                    {
                        var axisInParent = c.localRotation * (i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward);
                        float f = Mathf.Abs(axisInParent.x) * sc.x + Mathf.Abs(axisInParent.y) * sc.y + Mathf.Abs(axisInParent.z) * sc.z;
                        float w = Mathf.Abs(axisInParent.x) + Mathf.Abs(axisInParent.y) + Mathf.Abs(axisInParent.z);
                        inv[i] = w / Mathf.Max(0.01f, f);
                    }
                    c.localScale = Vector3.Scale(c.localScale, inv);
                }
            }
            Proportions();
        }

        /// <summary>
        /// Proporções de atleta (medidas num jogador de referência): tronco um pouco mais curto (a altura
        /// final é a mesma, então as pernas ficam proporcionalmente mais longas, sem afundar os pés), braço
        /// mais comprido e quadril um pouco mais largo. Só move as articulações (sem escala nos ossos que
        /// dobram: com escala, joelho e cotovelo entortavam).
        /// Funciona para ambos modelos MakeHuman e Meshy (que usam nomes "mixamorig:*").
        /// </summary>
        void Proportions()
        {
            bool isMeshy = bones.ContainsKey("mixamorig:Hips");
            void Move(string primary, string mixamoFallback, float k)
            {
                string key = isMeshy ? mixamoFallback : primary;
                if (bones.TryGetValue(key, out var t)) t.localPosition *= k;
            }
            // tronco mais curto -> pernas proporcionalmente mais longas
            Move("spine_02", "mixamorig:Spine1", 0.95f);
            Move("spine_03", "mixamorig:Spine2", 0.95f);
            Move("neck_01", "mixamorig:Neck", 0.97f);
            // braços mais longos
            Move("lowerarm_l", "mixamorig:LeftForeArm", 1.08f);
            Move("lowerarm_r", "mixamorig:RightForeArm", 1.08f);
            // quadril um pouco mais largo
            if (isMeshy)
            {
                if (bones.TryGetValue("mixamorig:LeftUpLeg", out var tl) && bones.TryGetValue("mixamorig:RightUpLeg", out var tr) && tl.parent == tr.parent)
                {
                    var half = (tl.localPosition - tr.localPosition) * 0.5f;
                    tl.localPosition += half * 0.06f;
                    tr.localPosition -= half * 0.06f;
                }
            }
            else
            {
                if (bones.TryGetValue("thigh_l", out var tl) && bones.TryGetValue("thigh_r", out var tr) && tl.parent == tr.parent)
                {
                    var half = (tl.localPosition - tr.localPosition) * 0.5f;
                    tl.localPosition += half * 0.06f;
                    tr.localPosition -= half * 0.06f;
                }
            }
        }
    }
}
