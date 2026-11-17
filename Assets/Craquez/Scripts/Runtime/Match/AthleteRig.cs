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
            var arm = Bone("Armature") ?? Bone("SmartRigArmature");
            var animHost = arm != null && arm != m ? arm.gameObject : m.gameObject;
            var old = m.GetComponent<Animator>();
            if (old != null && animHost != m.gameObject) old.enabled = false;
            animator = animHost.GetComponent<Animator>();
            if (animator == null) animator = animHost.AddComponent<Animator>();   // (?? não vê o "nulo" da Unity)
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // ossos sempre atualizados (evita cabeça "solta")
            if (head != null)
                Debug.Log($"[diag] modelo {m.name}: cabeça em repouso a {head.position.y - transform.position.y:F2} m; filhos {m.childCount}; Armature rot {(arm != null ? arm.localEulerAngles.ToString() : "-")}");
            // descobre para onde o modelo olha (pé esquerdo -> ponta do pé) e corrige para +Z
            if (footL != null && toeL != null)
            {
                var f = toeL.position - footL.position;
                f.y = 0;
                if (f.sqrMagnitude > 1e-6f)
                {
                    modelFix = Quaternion.FromToRotation(f.normalized, Vector3.forward);
                    m.localRotation = modelFix * m.localRotation;
                }
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
            ConfigureHierarchyAndSockets();
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
        static readonly (string bone, float slim, float strong)[] Shape =
        {
            // só o tronco e o pescoço: os ossos que dobram (coxa, canela, braço, antebraço) não são
            // escalados — com escala desigual, o joelho e o cotovelo entortavam ao dobrar (pernas
            // "para dentro"); a perna fica com a forma original do modelo
            // variação nítida de peso/porte físico: do leve/magro ao pesado/forte
            ("spine_01", 0.80f, 1.08f), ("spine_02", 0.88f, 1.12f), ("spine_03", 0.94f, 1.16f),
            ("clavicle_l", 0.94f, 1.08f), ("clavicle_r", 0.94f, 1.08f), ("neck_01", 0.96f, 1.10f),
        };

        /// <summary>
        /// Biotipo (0 = magro .. 1 = forte): escala os ossos só na espessura e compensa nos filhos.
        /// Chamar uma vez, logo depois de Init.
        /// </summary>
        public void Physique(float build, float frame = 0.5f)
        {
            build = Mathf.Clamp01(build);
            // ombros/caixa torácica: estreitos (0) .. largos (1), independente do porte
            // (proporções primeiro: os ombros saem na largura de referência e o biotipo varia em volta dela)
            Proportions();
            float fr = Mathf.Lerp(0.95f, 1.06f, Mathf.Clamp01(frame));
            // cabeça um pouco menor (proporção real ~1:7,5 da altura; o modelo base tem cabeça grande)
            if (head != null) head.localScale *= 0.93f;
            foreach (var (name, slim, strong) in Shape)
            {
                float k = Mathf.Lerp(slim, strong, build);
                if (name == "spine_03" || name.StartsWith("clavicle")) k *= fr;
                if (!bones.TryGetValue(name, out var b) || b.childCount == 0) continue;
                // eixo do comprimento: o que aponta para o primeiro osso filho
                Transform child = null;
                foreach (Transform c in b) if (c.GetComponent<Renderer>() == null) { child = c; break; }
                if (child == null) continue;
                var dir = b.InverseTransformDirection(child.position - b.position).normalized;
                int len = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? (Mathf.Abs(dir.x) > Mathf.Abs(dir.z) ? 0 : 2) : (Mathf.Abs(dir.y) > Mathf.Abs(dir.z) ? 1 : 2);
                var sc = Vector3.one * k;
                // comprimento: clavícula mais longa = ombros mais largos (o modelo tinha ombros caídos e
                // estreitos para um atleta); pescoço um pouco mais curto
                sc[len] = name.StartsWith("clavicle") ? Mathf.Lerp(1.0f, 1.05f, Mathf.Clamp01(frame * 0.6f + build * 0.4f))
                    : name == "neck_01" ? 0.94f : 1f;
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
        }

        // proporções de referência do corpo humano adulto (Drillis & Contini), em fração da altura
        const float RefUpperArm = 0.186f, RefForearm = 0.146f, RefHand = 0.108f, RefShoulders = 0.215f;
        // quanto cada modelo se aproxima da referência (o resto mantém a diferença entre os modelos de cada etnia)
        const float RefBlend = 0.75f;
        /// <summary>Altura estimada do modelo (m), pela articulação da cabeça (≈ 85,6% da altura).</summary>
        float ModelHeight() => head != null ? Mathf.Max(1.2f, (head.position.y - transform.position.y) / 0.856f) : 1.78f;

        /// <summary>
        /// Proporções de atleta, medidas osso a osso em cada modelo (os quatro modelos MakeHuman tinham braço
        /// curto e antebraço comprido — cotovelo baixo —, mãos e ombros largos demais e, em alguns, canela maior
        /// que a coxa). Leva cada trecho para perto da referência humana, mantendo um pouco da diferença de cada
        /// etnia, e devolve os pés ao chão. Só move as articulações (sem escala nos ossos que dobram: com escala,
        /// joelho e cotovelo entortavam), menos a mão, que é escalada por igual.
        /// </summary>
        void Proportions()
        {
            float H = ModelHeight();
            Transform B(string n) => bones.TryGetValue(n, out var t) ? t : null;
            // move a articulação "child" ao longo do osso "parent" até o trecho medir ~target
            void Seg(string parent, string child, float target)
            {
                var a = B(parent); var c = B(child);
                if (a == null || c == null) return;
                float cur = Vector3.Distance(a.position, c.position);
                if (cur < 1e-4f) return;
                c.localPosition *= Mathf.Lerp(1f, target / cur, RefBlend);
            }
// tronco um pouco mais curto (pernas proporcionalmente mais longas)
            void Move(string n, float k) { var t = B(n); if (t != null) t.localPosition *= k; }
            Move("spine_02", 0.97f); Move("spine_03", 0.97f);
            // ombros: distância entre as articulações dos ombros
            var ul = B("upperarm_l"); var ur = B("upperarm_r");
            if (ul != null && ur != null)
            {
                float w = Vector3.Distance(ul.position, ur.position);
                if (w > 1e-3f)
                {
                    float k = Mathf.Lerp(1f, RefShoulders * H / w, RefBlend);
                    ul.localPosition *= k; ur.localPosition *= k;
                }
            }
            foreach (var sd in new[] { "_l", "_r" })
            {
                // braço e antebraço (cotovelo na altura certa)
                Seg("upperarm" + sd, "lowerarm" + sd, RefUpperArm * H);
                Seg("lowerarm" + sd, "hand" + sd, RefForearm * H);
                // mão: punho até a ponta do dedo médio
                var hd = B("hand" + sd); var tip = B("middle_03" + sd);
                if (hd != null && tip != null)
                {
                    float cur = Vector3.Distance(hd.position, tip.position) + 0.025f;
                    hd.localScale *= Mathf.Lerp(1f, RefHand * H / cur, RefBlend);
                }
                // coxa e canela do mesmo tamanho (a soma, e a altura do quadril, ficam)
                var th = B("thigh" + sd); var cf = B("calf" + sd); var ft = B("foot" + sd);
                if (th != null && cf != null && ft != null)
                {
                    float t = Vector3.Distance(th.position, cf.position), sh = Vector3.Distance(cf.position, ft.position);
                    float m = (t + sh) * 0.5f;
                    if (t > 1e-3f && sh > 1e-3f)
                    {
                        cf.localPosition *= Mathf.Lerp(1f, m / t, RefBlend);
                        ft.localPosition *= Mathf.Lerp(1f, m / sh, RefBlend);
                    }
                }
            }
            // pés de volta ao chão (o comprimento das pernas pode ter mudado um pouco)
            if (model != null)
            {
                float minY = float.MaxValue;
                foreach (var t in new[] { footL, footR, toeL, toeR })
                    if (t != null) minY = Mathf.Min(minY, t.position.y);
                if (minY < transform.position.y + 0.3f && minY > transform.position.y - 0.3f)
                    model.position += Vector3.up * (transform.position.y + 0.035f - minY);
            }
        }
    }
}
