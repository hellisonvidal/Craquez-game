using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Marcas no gramado: o rastro do carrinho (grama arrancada, terra aparecendo) e as "patadas" das
    /// freadas e mudanças bruscas de direção. Cada marca é um quad no chão que vai sumindo devagar (o
    /// gramado se ajeita); tudo numa malha só, refeita só quando algo muda.
    /// </summary>
    public class TurfMarks : MonoBehaviour
    {
        const int Max = 160;
        readonly Vector3[] _a = new Vector3[Max], _b = new Vector3[Max];
        readonly float[] _w = new float[Max], _born = new float[Max], _life = new float[Max];
        int _next, _count;
        Mesh _mesh;
        Vector3[] _v;
        Color[] _c;
        Vector2[] _uv;
        int[] _t;
        bool _dirty;
        float _refresh;
        public float strength = 1f;   // grama natural 1; sintética: marca mais clara e curta

        public static TurfMarks Create(Transform parent, bool synthetic)
        {
            var go = new GameObject("Marcas no gramado");
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TurfMarks>();
            tm.strength = synthetic ? 0.45f : 1f;
            tm._mesh = new Mesh { name = "marcas" };
            tm._mesh.MarkDynamic();
            tm._v = new Vector3[Max * 4]; tm._c = new Color[Max * 4]; tm._uv = new Vector2[Max * 4]; tm._t = new int[Max * 6];
            for (int i = 0; i < Max; i++)
            {
                int v = i * 4, t = i * 6;
                tm._t[t] = v; tm._t[t + 1] = v + 1; tm._t[t + 2] = v + 2; tm._t[t + 3] = v; tm._t[t + 4] = v + 2; tm._t[t + 5] = v + 3;
                tm._uv[v] = new Vector2(0, 0); tm._uv[v + 1] = new Vector2(0, 1); tm._uv[v + 2] = new Vector2(1, 1); tm._uv[v + 3] = new Vector2(1, 0);
            }
            tm._mesh.vertices = tm._v; tm._mesh.colors = tm._c; tm._mesh.uv = tm._uv; tm._mesh.triangles = tm._t;
            tm._mesh.bounds = new Bounds(Vector3.zero, new Vector3(400f, 2f, 300f));
            go.AddComponent<MeshFilter>().sharedMesh = tm._mesh;
            var mr = go.AddComponent<MeshRenderer>();
            // terra com bordas suaves (textura radial alongada), escura e translúcida sobre a grama
            // (material de partículas: usa a cor dos vértices, que guarda o desbotar de cada marca)
            mr.sharedMaterial = Gfx.Particles(new Color(0.24f, 0.18f, 0.11f, 1f), Gfx.Radial(64, Color.white, new Color(1, 1, 1, 0), 1.3f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tm;
        }

        /// <summary>Marca do ponto a ao ponto b (mundo), com a largura dada, durando ~life segundos.</summary>
        public void Mark(Vector3 a, Vector3 b, float width, float life = 40f)
        {
            int i = _next;
            _next = (_next + 1) % Max;
            _count = Mathf.Min(_count + 1, Max);
            a.y = b.y = 0.016f;
            _a[i] = a; _b[i] = b; _w[i] = width; _born[i] = Time.time; _life[i] = life * (0.6f + 0.4f * strength);
            _dirty = true;
        }

        void Update()
        {
            _refresh -= Time.deltaTime;
            if (!_dirty && _refresh > 0f) return;
            _refresh = 0.5f;
            _dirty = false;
            float now = Time.time;
            for (int i = 0; i < Max; i++)
            {
                int v = i * 4;
                float age = now - _born[i];
                float a = i < _count || _life[i] > 0f ? Mathf.Clamp01(1f - age / Mathf.Max(0.1f, _life[i])) : 0f;
                if (_life[i] <= 0f) a = 0f;
                a *= 0.75f * strength;
                var d = _b[i] - _a[i];
                var side = Vector3.Cross(Vector3.up, d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward) * (_w[i] * 0.5f);
                // um pouco além das pontas: a borda suave da textura não corta a marca
                var ext = d.sqrMagnitude > 1e-6f ? d.normalized * _w[i] * 0.4f : Vector3.zero;
                _v[v] = _a[i] - ext - side; _v[v + 1] = _a[i] - ext + side; _v[v + 2] = _b[i] + ext + side; _v[v + 3] = _b[i] + ext - side;
                var c = new Color(1f, 1f, 1f, a);
                _c[v] = _c[v + 1] = _c[v + 2] = _c[v + 3] = c;
            }
            _mesh.vertices = _v;
            _mesh.colors = _c;
        }
    }
}
