using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Expressões faciais sem ossos de rosto: cria, uma vez por malha, formas (blend shapes) de boca
    /// aberta, sorriso, sobrancelhas franzidas/erguidas e piscada, deslocando os vértices do rosto em
    /// torno de marcos medidos no modelo (a partir do centro dos olhos: boca 7,3 cm abaixo, cantos a
    /// ±2,4 cm, queixo ~12 cm abaixo, sobrancelhas ~3 cm acima). Valem para a pele, as sobrancelhas e os
    /// cílios (cada malha no seu espaço), então tudo se mexe junto.
    /// </summary>
    public static class FaceRig
    {
        public const string JawOpen = "Expr_JawOpen", Smile = "Expr_Smile", BrowDown = "Expr_BrowDown", BrowUp = "Expr_BrowUp", Blink = "Expr_Blink";
        static readonly HashSet<Mesh> _done = new HashSet<Mesh>();

        static float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float G(float d, float r) => Mathf.Exp(-(d * d) / (2f * r * r));

        /// <summary>Cria as formas nas malhas do rosto do atleta (pele, sobrancelhas, cílios).</summary>
        public static void Build(SkinnedMeshRenderer body, SkinnedMeshRenderer eyes, KitPainter.Axes ax, params SkinnedMeshRenderer[] extra)
        {
            if (body == null || eyes == null || body.sharedMesh == null || eyes.sharedMesh == null) return;
            // marcos no mundo (pose de ligação: os nós das malhas não se mexem com a animação)
            var eb = eyes.sharedMesh.bounds;
            var eyeC = eyes.transform.TransformPoint(eb.center);
            var U = Vector3.zero; U[ax.up] = ax.upSign;
            var F = Vector3.zero; F[ax.dep] = ax.depSign;
            var Uw = body.transform.TransformDirection(U).normalized;
            var Fw = body.transform.TransformDirection(F).normalized;
            var Xw = Vector3.Cross(Uw, Fw).normalized;
            // meia largura dos dois olhos na direção lateral (sem supor qual eixo da malha é o lateral)
            var et = eyes.transform;
            float eyeHalf = Mathf.Abs(Vector3.Dot(et.TransformVector(new Vector3(eb.extents.x, 0, 0)), Xw))
                          + Mathf.Abs(Vector3.Dot(et.TransformVector(new Vector3(0, eb.extents.y, 0)), Xw))
                          + Mathf.Abs(Vector3.Dot(et.TransformVector(new Vector3(0, 0, eb.extents.z)), Xw));
            // centro de cada olho: meia largura menos o raio do globo (~1,25 cm)
            float eyeX = Mathf.Clamp(eyeHalf - 0.0125f, 0.025f, 0.04f);
            Add(body, eyeC, Xw, Uw, Fw, eyeX);
            foreach (var m in extra) if (m != null) Add(m, eyeC, Xw, Uw, Fw, eyeX);
        }

        static void Add(SkinnedMeshRenderer smr, Vector3 e, Vector3 X, Vector3 U, Vector3 F, float eyeX)
        {
            var mesh = smr.sharedMesh;
            if (mesh == null || _done.Contains(mesh) || mesh.GetBlendShapeIndex(JawOpen) >= 0) { _done.Add(mesh); return; }
            _done.Add(mesh);
            var v = mesh.vertices;
            int n = v.Length;
            var jaw = new Vector3[n]; var smile = new Vector3[n]; var down = new Vector3[n]; var up = new Vector3[n]; var blink = new Vector3[n];
            var t = smr.transform;
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                var w = t.TransformPoint(v[i]) - e;
                float dx = Vector3.Dot(w, X), dy = Vector3.Dot(w, U), dz = Vector3.Dot(w, F);
                if (dy > 0.12f || dy < -0.17f || Mathf.Abs(dx) > 0.09f || dz < -0.07f) continue;
                float front = S((dz + 0.06f) / 0.04f);   // rosto (não a nuca)
                float adx = Mathf.Abs(dx), sx = Mathf.Sign(dx);
                // boca aberta: tudo abaixo da fenda da boca desce e recua (o maxilar), sumindo no pescoço
                float below = S((-0.073f - dy) / 0.006f) * (1f - S((adx - 0.035f) / 0.025f)) * (1f - S((-0.14f - dy) / 0.03f)) * front;
                Vector3 dJaw = (-U * 0.011f - F * 0.004f) * below;
                // sorriso: cantos da boca sobem, abrem e recuam; maçãs do rosto sobem um pouco
                float corner = G(adx - 0.024f, 0.011f) * G(dy + 0.072f, 0.012f) * front;
                float cheek = G(adx - 0.035f, 0.016f) * G(dy + 0.03f, 0.016f) * front;
                Vector3 dSmile = (U * 0.0042f + X * sx * 0.003f - F * 0.002f) * corner + (U * 0.0025f + F * 0.0008f) * cheek;
                // sobrancelhas: faixa ~3 cm acima dos olhos; franzir puxa para baixo e para o meio (mais no meio)
                float brow = G(dy - 0.03f, 0.011f) * (1f - S((adx - 0.06f) / 0.015f)) * front;
                float inner = 0.55f + 0.45f * (1f - Mathf.Clamp01(adx / 0.06f));
                Vector3 dDown = (-U * 0.0055f * inner - X * sx * 0.0015f) * brow;
                // pálpebra de baixo sobe um pouco ao franzir (olhar de esforço)
                float lower = G(adx - eyeX, 0.013f) * G(dy + 0.011f, 0.005f) * S(dz / 0.01f);
                dDown += U * 0.0022f * lower;
                Vector3 dUp = U * 0.006f * brow;
                // piscada: a pálpebra de cima desce sobre o olho
                float lid = G(adx - eyeX, 0.014f) * S((dy - 0.001f) / 0.004f) * (1f - S((dy - 0.017f) / 0.006f)) * S((dz + 0.004f) / 0.008f);
                Vector3 dBlink = (-U * 0.0105f + F * 0.0015f) * lid;
                jaw[i] = t.InverseTransformVector(dJaw);
                smile[i] = t.InverseTransformVector(dSmile);
                down[i] = t.InverseTransformVector(dDown);
                up[i] = t.InverseTransformVector(dUp);
                blink[i] = t.InverseTransformVector(dBlink);
                any |= below > 0f || corner > 0.01f || brow > 0.01f || lid > 0.01f;
            }
            if (!any) return;
            mesh.AddBlendShapeFrame(JawOpen, 100f, jaw, null, null);
            mesh.AddBlendShapeFrame(Smile, 100f, smile, null, null);
            mesh.AddBlendShapeFrame(BrowDown, 100f, down, null, null);
            mesh.AddBlendShapeFrame(BrowUp, 100f, up, null, null);
            mesh.AddBlendShapeFrame(Blink, 100f, blink, null, null);
        }
    }

    /// <summary>Pesos das expressões de um atleta (pele + sobrancelhas + cílios), suavizados.</summary>
    public class FaceDriver
    {
        readonly SkinnedMeshRenderer[] _r;
        readonly int[,] _idx;   // [renderer, forma]
        readonly float[] _cur = new float[5], _set = new float[5];
        float _blinkT, _blinkNext;
        readonly System.Random _rng;

        public FaceDriver(int seed, params SkinnedMeshRenderer[] rs)
        {
            var list = new List<SkinnedMeshRenderer>();
            foreach (var r in rs) if (r != null && r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex(FaceRig.JawOpen) >= 0) list.Add(r);
            _r = list.ToArray();
            _idx = new int[_r.Length, 5];
            string[] names = { FaceRig.JawOpen, FaceRig.Smile, FaceRig.BrowDown, FaceRig.BrowUp, FaceRig.Blink };
            for (int i = 0; i < _r.Length; i++)
                for (int k = 0; k < 5; k++) _idx[i, k] = _r[i].sharedMesh.GetBlendShapeIndex(names[k]);
            _rng = new System.Random(seed);
            _blinkNext = 1f + (float)_rng.NextDouble() * 4f;
            for (int k = 0; k < 5; k++) _set[k] = -1f;
        }

        public bool Valid => _r.Length > 0;

        /// <summary>jaw, smile, browDown, browUp em 0..1; piscadas automáticas.</summary>
        public void Update(float dt, float jaw, float smile, float browDown, float browUp, bool eyesShut = false)
        {
            if (_r.Length == 0) return;
            _blinkNext -= dt;
            if (_blinkNext <= 0f) { _blinkT = 0.16f; _blinkNext = 2f + (float)_rng.NextDouble() * 4.5f; }
            float blink = 0f;
            if (_blinkT > 0f) { _blinkT -= dt; blink = Mathf.Sin(Mathf.Clamp01(1f - _blinkT / 0.16f) * Mathf.PI); }
            if (eyesShut) blink = 0.85f;
            float k = 1f - Mathf.Exp(-dt * 10f);
            _cur[0] = Mathf.Lerp(_cur[0], jaw, k);
            _cur[1] = Mathf.Lerp(_cur[1], smile, k);
            _cur[2] = Mathf.Lerp(_cur[2], browDown, k);
            _cur[3] = Mathf.Lerp(_cur[3], browUp, k);
            _cur[4] = blink;
            for (int s = 0; s < 5; s++)
            {
                float w = Mathf.Clamp01(_cur[s]) * 100f;
                if (Mathf.Abs(w - _set[s]) < 0.5f) continue;
                _set[s] = w;
                for (int i = 0; i < _r.Length; i++) if (_idx[i, s] >= 0) _r[i].SetBlendShapeWeight(_idx[i, s], w);
            }
        }

        /// <summary>Rosto neutro (atleta longe da câmera: sem custo de formas ativas).</summary>
        public void Neutral()
        {
            for (int s = 0; s < 5; s++)
            {
                if (_set[s] == 0f) continue;
                _set[s] = 0f; _cur[s] = 0f;
                for (int i = 0; i < _r.Length; i++) if (_idx[i, s] >= 0) _r[i].SetBlendShapeWeight(_idx[i, s], 0f);
            }
        }
    }
}
