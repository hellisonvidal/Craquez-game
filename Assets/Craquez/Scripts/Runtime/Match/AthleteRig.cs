using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>Referências do atleta instanciado: ossos, malhas e número da camisa.</summary>
    public class AthleteRig : MonoBehaviour
    {
        public Transform model;
        public SkinnedMeshRenderer body;
        public Animator animator;
        public readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        public Transform head, neck, spine, pelvis, footL, footR, handL, handR;
        /// <summary>Rotação que alinha a frente do modelo com +Z do objeto raiz.</summary>
        public Quaternion modelFix = Quaternion.identity;
        /// <summary>Rotações locais de repouso do pescoço e da cabeça.</summary>
        public Quaternion headRest = Quaternion.identity, neckRest = Quaternion.identity;
        Transform _number, _numberFront;

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
            // pés no chão
            if (footL != null)
            {
                float minY = float.MaxValue;
                foreach (var t in new[] { footL, footR, ballL, B("ball_r") })
                    if (t != null) minY = Mathf.Min(minY, t.position.y);
                if (minY < 0.3f && minY > -0.3f) m.localPosition += Vector3.up * (0.035f - (minY - transform.position.y));
            }
        }

        /// <summary>Número nas costas (e pequeno no peito), acompanhando o tronco.</summary>
        public void SetNumber(int n, Kit kit)
        {
            if (spine == null) return;
            var col = kit.c1.grayscale > 0.55f ? new Color(0.06f, 0.07f, 0.1f) : Color.white;
            if (Mathf.Abs(kit.c1.grayscale - kit.c2.grayscale) > 0.3f) col = kit.c2;
            var mat = NumberMat(n, col);
            _number = MakeQuad(mat, 0.24f, -1f, 0.04f, 0f);
            _numberFront = MakeQuad(mat, 0.08f, 1f, 0.1f, -0.09f);
        }

        static readonly Dictionary<string, Material> _numMats = new Dictionary<string, Material>();

        static Material NumberMat(int n, Color col)
        {
            string key = n + Teams.Hex(col);
            if (_numMats.TryGetValue(key, out var m) && m != null) return m;
            m = Gfx.Cutout(col, Glyphs.Number(n, 128), 0.5f, 0.2f);
            _numMats[key] = m;
            return m;
        }

        Transform MakeQuad(Material mat, float size, float side, float up, float lateral)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "numero";
            Destroy(go.GetComponent<Collider>());
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var fwd = transform.forward;
            float depth = side < 0 ? 0.16f : 0.15f;   // por cima da camisa (peça com folga)
            go.transform.localScale = new Vector3(size, size, 1f);
            go.transform.position = spine.position + fwd * (side * depth) + Vector3.up * up + transform.right * lateral;
            // o quad do Unity mostra a face para -Z local: aponta -Z para fora do corpo
            go.transform.rotation = Quaternion.LookRotation(side < 0 ? fwd : -fwd, Vector3.up);
            go.transform.SetParent(spine, true);
            return go.transform;
        }
    }
}
