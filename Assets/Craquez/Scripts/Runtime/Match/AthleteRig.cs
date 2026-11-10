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
        public Animator animator;
        public readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        public Transform head, neck, spine, pelvis, footL, footR, handL, handR;
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

        public void Init(Transform m)
        {
            model = m;
            foreach (var t in m.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
            Transform B(string n) => bones.TryGetValue(n, out var t) ? t : null;
            head = B("head"); neck = B("neck_01");
            if (head != null) headRest = head.localRotation;
            if (neck != null) neckRest = neck.localRotation; spine = B("spine_03"); pelvis = B("pelvis");
            footL = B("foot_l"); footR = B("foot_r"); handL = B("hand_l"); handR = B("hand_r");
            // os clipes vêm de um FBX só com o esqueleto: a Unity remove o nó raiz e os caminhos
            // começam em "Root/..."; no modelo eles ficam sob "Armature". O Animator vai no
            // "Armature" para os caminhos baterem (antes nenhuma curva encontrava os ossos).
            var arm = B("Armature");
            var animHost = arm != null && arm != m ? arm.gameObject : m.gameObject;
            var old = m.GetComponent<Animator>();
            if (old != null && animHost != m.gameObject) old.enabled = false;
            animator = animHost.GetComponent<Animator>();
            if (animator == null) animator = animHost.AddComponent<Animator>();   // (?? não vê o "nulo" da Unity)
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // ossos sempre atualizados (evita cabeça "solta")
            if (head != null)
                Debug.Log($"[diag] modelo {m.name}: cabeça em repouso a {head.position.y - transform.position.y:F2} m; filhos {m.childCount}; Armature rot {(B("Armature") != null ? B("Armature").localEulerAngles.ToString() : "-")}");
            // descobre para onde o modelo olha (pé -> ponta do pé) e corrige para +Z
            var ballL = B("ball_l");
            if (footL != null && ballL != null)
            {
                var f = ballL.position - footL.position;
                f.y = 0;
                if (f.sqrMagnitude > 1e-6f)
                {
                    modelFix = Quaternion.FromToRotation(f.normalized, Vector3.forward);
                    m.localRotation = modelFix * m.localRotation;
                }
            }
            if (pelvis != null) pelvisRest = transform.InverseTransformPoint(pelvis.position);
            footPitchRest = FootPitch(footL, B("ball_l"));
            // pés no chão
            if (footL != null)
            {
                float minY = float.MaxValue;
                foreach (var t in new[] { footL, footR, ballL, B("ball_r") })
                    if (t != null) minY = Mathf.Min(minY, t.position.y);
                if (minY < 0.3f && minY > -0.3f) m.localPosition += Vector3.up * (0.035f - (minY - transform.position.y));
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
            // cintura fina e peito mais cheio (o "V" do atleta)
            ("spine_01", 0.84f, 0.95f), ("spine_02", 0.92f, 1.02f), ("spine_03", 1.0f, 1.09f),
            ("clavicle_l", 0.98f, 1.03f), ("clavicle_r", 0.98f, 1.03f), ("neck_01", 1.0f, 1.08f),
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
                sc[len] = name.StartsWith("clavicle") ? Mathf.Lerp(1.06f, 1.16f, Mathf.Clamp01(frame * 0.6f + build * 0.4f))
                    : name == "neck_01" ? 0.88f : 1f;
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
    }
}
