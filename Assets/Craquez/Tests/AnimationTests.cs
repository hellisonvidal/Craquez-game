#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace Craquez.Tests
{
    /// <summary>
    /// Atleta animado de verdade (modelo + clipes de captura): as correções por cima da
    /// animação não podem se acumular, o tronco fica de pé e os pés apontam para a frente.
    /// </summary>
    public class AnimationTests
    {
        static (GameObject root, AthleteRig rig) Make(string name)
        {
            var model = Resources.Load<GameObject>("Athletes/Athlete_latino");
            Assert.IsNotNull(model, "modelo Athlete_latino ausente");
            var root = new GameObject(name);
            var inst = Object.Instantiate(model, root.transform, false);
            inst.name = "model";
            var rig = root.AddComponent<AthleteRig>();
            rig.Init(inst.transform);
            return (root, rig);
        }

        static Transform B(AthleteRig r, string n) => r.bones.TryGetValue(n, out var t) ? t : null;

        static string PathOf(Transform t, Transform root)
        {
            var s = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        /// <summary>A animação precisa mexer de fato nos ossos (caminhos do clipe = hierarquia do modelo).</summary>
        [Test]
        public void ClipesMexemNosOssos()
        {
            var (g, r) = Make("D");
            try
            {
                Assert.IsTrue(AthleteAnimator.Clips.TryGetValue("Run", out var ci), "clipe Run ausente");
                var model = r.model;
                var paths = new System.Collections.Generic.HashSet<string>();
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t != model) paths.Add(PathOf(t, model));
                var binds = UnityEditor.AnimationUtility.GetCurveBindings(ci.clip);
                int hit = 0;
                var sample = new System.Text.StringBuilder();
                foreach (var b in binds)
                {
                    if (paths.Contains(b.path)) hit++;
                    else if (sample.Length < 600) sample.Append(b.path).Append(" | ");
                }
                var thigh = B(r, "thigh_l");
                Debug.Log($"[anim-diag] clipe Run: {binds.Length} curvas, {hit} com caminho existente no modelo; modelo: '{PathOf(thigh, model)}'; sem par: {sample}");
                // amostragem direta do clipe
                ci.clip.SampleAnimation(model.gameObject, 0f);
                var q0 = thigh.localRotation;
                ci.clip.SampleAnimation(model.gameObject, ci.clip.length * 0.5f);
                float sampled = Quaternion.Angle(q0, thigh.localRotation);
                // pelo grafo (como no jogo)
                var anim = new AthleteAnimator(r.animator, "D", 3);
                float minA = 999f, maxA = -999f;
                var fwd = g.transform.forward;
                for (int f = 0; f < 90; f++)
                {
                    anim.Tick(1f / 60f, 5f, 5f, 0f, false);
                    var d = B(r, "calf_l").position - thigh.position;
                    float a = Vector3.SignedAngle(Vector3.down, d, g.transform.right);
                    minA = Mathf.Min(minA, a);
                    maxA = Mathf.Max(maxA, a);
                }
                anim.Destroy();
                Debug.Log($"[anim-diag] coxa: SampleAnimation varia {sampled:F1}°; pelo grafo a coxa oscila {maxA - minA:F1}° ({minA:F0}..{maxA:F0})");
                Assert.Greater(hit, binds.Length / 2, "curvas do clipe não encontram os ossos do modelo");
                Assert.Greater(maxA - minA, 20f, "a corrida não move as pernas");
            }
            finally { Object.DestroyImmediate(g); }
        }

        [Test]
        public void CorrecoesNaoAcumulamEPoseNatural()
        {
            var (ga, a) = Make("A");
            var (gb, b) = Make("B");
            var (gc, c) = Make("C");
            var animA = new AthleteAnimator(a.animator, "A", 7);
            var animB = new AthleteAnimator(b.animator, "B", 7);
            var animC = new AthleteAnimator(c.animator, "C", 7);
            var pose = new PoseEdits();
            const float dt = 1f / 60f;
            float maxDiff = 0f, uprightSum = 0f, footSum = 0f;
            int notRewritten = 0, n = 0, frames = 420;
            try
            {
                for (int f = 0; f < frames; f++)
                {
                    // parado -> andando -> correndo -> pique
                    float speed = f < 60 ? 0f : f < 150 ? 1.4f : f < 300 ? 4.5f : 7.5f;
                    pose.Restore();
                    animA.Tick(dt, speed, speed, 0f, false);
                    animB.Tick(dt, speed, speed, 0f, false);
                    var before = B(c, "spine_01").localRotation;
                    animC.Tick(dt, speed, speed, 0f, false);
                    if (f > 0 && Quaternion.Angle(B(c, "spine_01").localRotation, before) < 0.001f) notRewritten++;
                    foreach (var bn in new[] { "spine_01", "spine_03", "thigh_l", "calf_l", "upperarm_l" })
                        maxDiff = Mathf.Max(maxDiff, Quaternion.Angle(B(a, bn).localRotation, B(b, bn).localRotation));
                    // B: postura natural
                    var up = B(b, "head").position - B(b, "pelvis").position;
                    uprightSum += Vector3.Angle(up, Vector3.up);
                    var fwd = gb.transform.forward;
                    foreach (var s in new[] { "_l", "_r" })
                    {
                        var d = B(b, "ball" + s).position - B(b, "foot" + s).position;
                        d.y = 0f;
                        footSum += Vector3.Dot(d.normalized, fwd);
                    }
                    n++;
                    // A: correções (tronco, perna, braço) como no jogo; C: sem devolver a pose (antigo)
                    var ax = ga.transform.right;
                    pose.Rotate(B(a, "spine_01"), Quaternion.AngleAxis(6f, ax));
                    pose.Rotate(B(a, "spine_03"), Quaternion.AngleAxis(5f, ax));
                    pose.Rotate(B(a, "thigh_l"), Quaternion.AngleAxis(-20f, ax));
                    pose.Rotate(B(a, "calf_l"), Quaternion.AngleAxis(-12f, ax));
                    pose.Rotate(B(a, "upperarm_l"), Quaternion.AngleAxis(15f, Vector3.up));
                    var sc = B(c, "spine_01");
                    sc.rotation = Quaternion.AngleAxis(6f, gc.transform.right) * sc.rotation;
                }
                float upright = uprightSum / n, foot = footSum / (2 * n);
                Debug.Log($"[anim-test] diferença máx. com correções: {maxDiff:F2}°; quadros em que a animação não reescreveu a coluna: {notRewritten}/{frames}; tronco inclinado em média {upright:F1}°; pés para a frente {foot:F2} (1 = alinhados)");
                Assert.Less(maxDiff, 0.5f, "correções acumulando sobre a animação");
                Assert.Less(upright, 30f, "tronco tombado");
                Assert.Greater(foot, 0.5f, "pés virados para trás/lado");
            }
            finally
            {
                animA.Destroy();
                animB.Destroy();
                animC.Destroy();
                Object.DestroyImmediate(ga);
                Object.DestroyImmediate(gb);
                Object.DestroyImmediate(gc);
            }
        }
    }
}
#endif
