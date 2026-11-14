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
                var model = r.animator.transform;   // os caminhos do clipe são relativos ao Animator
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
                float minA = 999f, maxA = -999f, drift = 0f;
                var pelvis = B(r, "pelvis");
                var p0 = g.transform.TransformPoint(r.pelvisRest);
                for (int f = 0; f < 90; f++)
                {
                    anim.Tick(1f / 60f, 5f, 5f, 0f, false);
                    r.PinHips(0f);
                    var d = B(r, "calf_l").position - thigh.position;
                    float a = Vector3.SignedAngle(Vector3.down, d, g.transform.right);
                    minA = Mathf.Min(minA, a);
                    maxA = Mathf.Max(maxA, a);
                    var dp = pelvis.position - p0;
                    dp.y = 0f;
                    drift = Mathf.Max(drift, dp.magnitude);
                }
                anim.Destroy();
                Debug.Log($"[anim-diag] coxa: SampleAnimation varia {sampled:F1}°; pelo grafo a coxa oscila {maxA - minA:F1}° ({minA:F0}..{maxA:F0}); quadril sai do lugar até {drift:F2} m");
                Assert.Greater(hit, binds.Length / 2, "curvas do clipe não encontram os ossos do modelo");
                Assert.Greater(maxA - minA, 20f, "a corrida não move as pernas");
                Assert.Less(drift, 0.05f, "quadril não ficou preso na horizontal");
            }
            finally { Object.DestroyImmediate(g); }
        }

        /// <summary>Medidas de cada clipe no referencial do atleta (quadril, rumo do quadril e dos pés).</summary>
        [Test]
        public void MedidasDosClipes()
        {
            var (g, r) = Make("M");
            try
            {
                var root = g.transform;
                var host = r.animator.gameObject;
                var pel = B(r, "pelvis");
                var tl = B(r, "thigh_l");
                var tr = B(r, "thigh_r");
                Vector3 L(Vector3 w) => root.InverseTransformPoint(w);
                float Yaw(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                var restP = L(pel.position);
                float restHip = Yaw(Quaternion.Euler(0, -90, 0) * (L(tr.position) - L(tl.position)));
                Debug.Log($"[clip] repouso: quadril em ({restP.x:F2},{restP.y:F2},{restP.z:F2}); rumo do quadril {restHip:F0}°");
                foreach (var kv in AthleteAnimator.Clips)
                {
                    var c = kv.Value.clip;
                    const int N = 24;
                    Vector3 mn = Vector3.one * 99f, mx = -Vector3.one * 99f, sum = Vector3.zero;
                    float hipSum = 0f, footSum = 0f, hipMin = 999f, hipMax = -999f;
                    for (int k = 0; k < N; k++)
                    {
                        c.SampleAnimation(host, c.length * k / N);
                        var pp = L(pel.position);
                        mn = Vector3.Min(mn, pp); mx = Vector3.Max(mx, pp); sum += pp;
                        float hy = Yaw(Quaternion.Euler(0, -90, 0) * (L(tr.position) - L(tl.position)));
                        hipSum += hy; hipMin = Mathf.Min(hipMin, hy); hipMax = Mathf.Max(hipMax, hy);
                        foreach (var sfx in new[] { "_l", "_r" })
                        {
                            var d = L(B(r, "ball" + sfx).position) - L(B(r, "foot" + sfx).position);
                            footSum += Yaw(new Vector3(d.x, 0, d.z));
                        }
                    }
                    var avg = sum / N;
                    Debug.Log($"[clip] {kv.Key,-10} {c.length:F2}s quadril médio ({avg.x:F2},{avg.y:F2},{avg.z:F2}) x[{mn.x:F2},{mx.x:F2}] z[{mn.z:F2},{mx.z:F2}] y[{mn.y:F2},{mx.y:F2}]; rumo quadril {hipSum / N:F0}° [{hipMin:F0},{hipMax:F0}]; pés {footSum / (2 * N):F0}°");
                }
            }
            finally { Object.DestroyImmediate(g); }
        }

        /// <summary>Corpo atlético não deforma: cabeça na altura certa, mãos e pés simétricos.</summary>
        [Test]
        public void CorpoProporcional()
        {
            var (g, r) = Make("P");
            r.Physique(0.4f);
            try
            {
                var root = g.transform;
                float head = B(r, "head").position.y - root.position.y;
                float handL = Vector3.Distance(B(r, "upperarm_l").position, B(r, "hand_l").position);
                float handR = Vector3.Distance(B(r, "upperarm_r").position, B(r, "hand_r").position);
                float legL = Vector3.Distance(B(r, "thigh_l").position, B(r, "foot_l").position);
                float legR = Vector3.Distance(B(r, "thigh_r").position, B(r, "foot_r").position);
                var s1 = B(r, "spine_01").lossyScale;
                Debug.Log($"[corpo] cabeça {head:F2} m; braços {handL:F2}/{handR:F2}; pernas {legL:F2}/{legR:F2}; escala cintura {s1}");
                Assert.That(head, Is.InRange(1.45f, 1.95f));
                Assert.Less(Mathf.Abs(handL - handR), 0.02f);
                Assert.Less(Mathf.Abs(legL - legR), 0.02f);
                Assert.That(handL, Is.InRange(0.45f, 0.7f));
            }
            finally { Object.DestroyImmediate(g); }
        }

        /// <summary>Braços não abrem/erguem demais e o pé não fica pendurado depois das correções.</summary>
        [Test]
        public void BracosEPesNaturais()
        {
            var (g, r) = Make("Q");
            var anim = new AthleteAnimator(r.animator, "Q", 5);
            var pose = new PoseEdits();
            float maxAbdBefore = 0f, maxAbdAfter = 0f, minPitchBefore = 999f, minPitchAfter = 999f;
            try
            {
                foreach (var (spd, guard) in new[] { (5f, false), (1.2f, true), (0f, false), (7.5f, false) })
                    for (int f = 0; f < 120; f++)
                    {
                        pose.Restore();
                        anim.Tick(1f / 60f, spd, guard ? 0f : spd, guard ? spd : 0f, guard);
                        r.PinHips(0f);
                        for (int sd = -1; sd <= 1; sd += 2) maxAbdBefore = Mathf.Max(maxAbdBefore, PoseFix.Abduction(r, g.transform, sd, out _));
                        foreach (var (ft, tb) in new[] { (r.footL, B(r, "ball_l")), (r.footR, B(r, "ball_r")) }) minPitchBefore = Mathf.Min(minPitchBefore, AthleteRig.FootPitch(ft, tb) - r.footPitchRest);
                        PoseFix.Arms(r, g.transform, pose, 1f);
                        PoseFix.Feet(r, g.transform, pose, 0f);
                        for (int sd = -1; sd <= 1; sd += 2) maxAbdAfter = Mathf.Max(maxAbdAfter, PoseFix.Abduction(r, g.transform, sd, out _));
                        foreach (var (ft, tb) in new[] { (r.footL, B(r, "ball_l")), (r.footR, B(r, "ball_r")) }) minPitchAfter = Mathf.Min(minPitchAfter, AthleteRig.FootPitch(ft, tb) - r.footPitchRest);
                    }
                Debug.Log($"[pose] abertura máx. do braço: {maxAbdBefore:F0}° -> {maxAbdAfter:F0}°; ponta do pé (rel. repouso) mín.: {minPitchBefore:F0}° -> {minPitchAfter:F0}°");
                Assert.Less(maxAbdAfter, 26f, "braços ainda abertos");
                Assert.Greater(minPitchAfter, -38f, "pé ainda pendurado");
            }
            finally { anim.Destroy(); Object.DestroyImmediate(g); }
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
