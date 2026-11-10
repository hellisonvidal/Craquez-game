using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// O cenário é montado em malhas grandes (todos os prédios de uma cor numa malha só, os quatro
    /// postes juntos...). A câmera esconde o que tapa o jogo pela caixa de cada malha — e uma caixa que
    /// dá a volta no campo "tapava" sempre: sumia tudo (prédios da rua inteiros) ou nada (pilares e
    /// postes do lado da câmera na frente do jogo). Aqui cada malha assim é cortada em pedaços: o lado
    /// da câmera em fatias ao longo do campo e o resto inteiro. Assim só some o pedaço que tapa.
    /// </summary>
    public static class VenueSplit
    {
        static readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
        static readonly List<Vector4> T = new List<Vector4>();
        static readonly List<Color> C = new List<Color>();
        static readonly List<Vector4>[] U = { new List<Vector4>(), new List<Vector4>(), new List<Vector4>(), new List<Vector4>() };
        static readonly List<int> Tri = new List<int>();

        public static void Split(IEnumerable<Transform> roots, float halfLen, float halfW)
        {
            var todo = new List<MeshFilter>();
            foreach (var root in roots)
                if (root != null)
                    foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                        if (Candidate(mf, halfW)) todo.Add(mf);
            foreach (var mf in todo)
            {
                try { Cut(mf, halfLen, halfW); }
                catch (System.Exception ex) { Debug.LogWarning("[cenário] corte de " + mf.name + ": " + ex.Message); }
            }
        }

        static bool Candidate(MeshFilter mf, float hw)
        {
            var m = mf.sharedMesh;
            var mr = mf.GetComponent<MeshRenderer>();
            if (m == null || mr == null || !m.isReadable || m.subMeshCount != 1 || m.vertexCount < 8) return false;
            // só malhas simples (sem scripts que mexam nelas: torcida animada, bandeiras, rede...)
            if (mf.GetComponents<Component>().Length > 3) return false;
            var b = mr.bounds;
            if (b.size.y < 0.6f && b.max.y < 0.8f) return false;   // chão e pinturas
            // atravessa a linha do lado da câmera (um pedaço antes dela e outro depois) e é grande
            float zl = -(hw + 0.5f);
            return b.min.z < zl - 0.5f && b.max.z > zl + 0.5f && Mathf.Max(b.size.x, b.size.z) > 20f;
        }

        static void Cut(MeshFilter mf, float hl, float hw)
        {
            var m = mf.sharedMesh;
            var tr = mf.transform;
            m.GetVertices(V);
            m.GetNormals(N);
            m.GetTangents(T);
            m.GetColors(C);
            for (int i = 0; i < 4; i++) m.GetUVs(i, U[i]);
            var tris = m.triangles;
            float zl = -(hw + 0.5f);
            // célula de cada triângulo pelo centro (em coordenadas do mundo): 0 = resto, 1..4 = fatias do lado da câmera
            const int Slices = 4;
            float span = hl * 2f + 40f;
            var cells = new List<int>[Slices + 1];
            for (int i = 0; i <= Slices; i++) cells[i] = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                var c = tr.TransformPoint((V[tris[t]] + V[tris[t + 1]] + V[tris[t + 2]]) / 3f);
                int cell = 0;
                if (c.z < zl) cell = 1 + Mathf.Clamp(Mathf.FloorToInt((c.x + span / 2f) / span * Slices), 0, Slices - 1);
                cells[cell].Add(t);
            }
            int used = 0;
            foreach (var l in cells) if (l.Count > 0) used++;
            if (used < 2) return;
            var mr = mf.GetComponent<MeshRenderer>();
            bool first = true;
            for (int ci = 0; ci <= Slices; ci++)
            {
                if (cells[ci].Count == 0) continue;
                var piece = Build(m, tris, cells[ci], ci);
                if (first) { mf.sharedMesh = piece; first = false; continue; }
                var go = new GameObject(mf.name + " (lado da câmera " + ci + ")");
                go.layer = mf.gameObject.layer;
                go.transform.SetParent(tr, false);
                go.AddComponent<MeshFilter>().sharedMesh = piece;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = mr.sharedMaterials;
                r.shadowCastingMode = mr.shadowCastingMode;
                r.receiveShadows = mr.receiveShadows;
                r.lightProbeUsage = mr.lightProbeUsage;
                r.reflectionProbeUsage = mr.reflectionProbeUsage;
            }
            Object.Destroy(m);
        }

        static Mesh Build(Mesh src, int[] tris, List<int> sel, int cell)
        {
            var map = new Dictionary<int, int>();
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<Vector4>(); var c = new List<Color>();
            var u = new List<Vector4>[4];
            for (int i = 0; i < 4; i++) u[i] = new List<Vector4>();
            Tri.Clear();
            foreach (int s in sel)
                for (int k = 0; k < 3; k++)
                {
                    int o = tris[s + k];
                    if (!map.TryGetValue(o, out int ni))
                    {
                        ni = v.Count;
                        map[o] = ni;
                        v.Add(V[o]);
                        if (N.Count == V.Count) n.Add(N[o]);
                        if (T.Count == V.Count) t.Add(T[o]);
                        if (C.Count == V.Count) c.Add(C[o]);
                        for (int i = 0; i < 4; i++) if (U[i].Count == V.Count) u[i].Add(U[i][o]);
                    }
                    Tri.Add(ni);
                }
            var m = new Mesh { name = src.name + " " + cell };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            if (n.Count == v.Count) m.SetNormals(n);
            if (t.Count == v.Count) m.SetTangents(t);
            if (c.Count == v.Count) m.SetColors(c);
            for (int i = 0; i < 4; i++) if (u[i].Count == v.Count) m.SetUVs(i, u[i]);
            m.SetTriangles(Tri, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
