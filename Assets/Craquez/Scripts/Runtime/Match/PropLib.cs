using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Modelos de cenário (Quaternius, CC0): árvores, pinheiros, arbustos, flores, capim, pedras e
    /// casas/sobrados. Vêm de Resources/Props/props.bytes (gerado por tools/blender/props.py) e
    /// são juntados por material em poucas malhas (PropBatch), como o resto do estádio.
    /// </summary>
    public static class PropLib
    {
        public class Part
        {
            public string key;
            public Vector3[] v, n;
            public Vector2[] uv;
            public int[] i;
        }

        public class Model
        {
            public string name;
            public Vector3 min, max;
            public Part[] parts;
            public Vector3 Size => max - min;
            public int Tris { get { int t = 0; foreach (var p in parts) t += p.i.Length / 3; return t; } }
        }

        public static string[] Palette = new string[0];
        static Dictionary<string, Model> _models;
        static bool _tried;

        public static bool Ready => Load() != null;

        static Dictionary<string, Model> Load()
        {
            if (_tried) return _models;
            _tried = true;
            var ta = Resources.Load<TextAsset>("Props/props");
            if (ta == null) return null;
            try { _models = Parse(ta.bytes); }
            catch (System.Exception e) { Debug.LogWarning("Props: " + e.Message); _models = null; }
            return _models;
        }

        /// <summary>Lê o formato CQP1 (ver props.py).</summary>
        public static Dictionary<string, Model> Parse(byte[] bytes)
        {
            var r = new BinaryReader(new MemoryStream(bytes));
            if (new string(r.ReadChars(4)) != "CQP1") throw new IOException("formato desconhecido");
            string S() { int n = r.ReadByte(); return System.Text.Encoding.UTF8.GetString(r.ReadBytes(n)); }
            int np = r.ReadInt32();
            Palette = new string[np];
            for (int k = 0; k < np; k++) Palette[k] = S();
            int nm = r.ReadInt32();
            var d = new Dictionary<string, Model>(nm);
            for (int m = 0; m < nm; m++)
            {
                var mo = new Model { name = S() };
                mo.min = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                mo.max = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                mo.parts = new Part[r.ReadInt32()];
                for (int p = 0; p < mo.parts.Length; p++)
                {
                    var pa = new Part { key = S() };
                    int nv = r.ReadInt32(), ni = r.ReadInt32();
                    pa.v = new Vector3[nv]; pa.n = new Vector3[nv]; pa.uv = new Vector2[nv]; pa.i = new int[ni];
                    for (int k = 0; k < nv; k++)
                    {
                        pa.v[k] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        pa.n[k] = new Vector3(r.ReadSByte(), r.ReadSByte(), r.ReadSByte()) / 127f;
                        pa.uv[k] = new Vector2(Mathf.HalfToFloat(r.ReadUInt16()), Mathf.HalfToFloat(r.ReadUInt16()));
                    }
                    for (int k = 0; k < ni; k++) pa.i[k] = r.ReadUInt16();
                    mo.parts[p] = pa;
                }
                d[mo.name] = mo;
            }
            return d;
        }

        public static Model Get(string name) { var d = Load(); return d != null && d.TryGetValue(name, out var m) ? m : null; }

        /// <summary>Nomes que começam com o prefixo (ex.: "Tree", "Pine", "House_2Story").</summary>
        public static List<string> Names(string prefix)
        {
            var l = new List<string>();
            var d = Load();
            if (d != null) foreach (var k in d.Keys) if (k.StartsWith(prefix)) l.Add(k);
            l.Sort(System.StringComparer.Ordinal);
            return l;
        }

        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        public static Texture2D Tex(string key)
        {
            if (_tex.TryGetValue(key, out var t)) return t;
            t = Resources.Load<Texture2D>("Props/Tex/" + key);
            _tex[key] = t;
            return t;
        }

        /// <summary>
        /// Paleta 8x8 das casas: cada célula é uma cor nomeada (Main = parede, Bricks = barrado,
        /// Light = frisos, Glass = vidro...). Sem filtro, para não misturar as cores vizinhas.
        /// </summary>
        public static Texture2D PaletteTex(Color main, Color trim, bool night)
        {
            var t = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Paleta das casas" };
            var px = new Color[64];
            for (int k = 0; k < 64; k++) px[k] = Color.magenta;
            for (int k = 0; k < Palette.Length && k < 64; k++)
            {
                Color c;
                switch (Palette[k])
                {
                    case "Main": c = main; break;
                    case "Bricks": c = trim; break;
                    case "Light": c = Color.Lerp(main, Color.white, 0.55f); break;
                    case "White": c = new Color(0.9f, 0.89f, 0.86f); break;
                    case "Glass": c = night ? new Color(0.95f, 0.78f, 0.45f) : new Color(0.26f, 0.34f, 0.42f); break;
                    case "Windows": c = new Color(0.86f, 0.85f, 0.82f); break;
                    case "Dark": c = new Color(0.22f, 0.23f, 0.25f); break;
                    case "Black": c = new Color(0.09f, 0.09f, 0.1f); break;
                    case "Wood": c = new Color(0.5f, 0.32f, 0.18f); break;
                    case "DarkWood": c = new Color(0.33f, 0.2f, 0.11f); break;
                    default: c = new Color(0.27f, 0.17f, 0.11f); break;
                }
                px[k] = c;
            }
            t.SetPixels(px);
            t.Apply(false, true);
            return t;
        }
    }

    /// <summary>Junta os modelos colocados em uma malha por material (uma chamada de desenho cada).</summary>
    public class PropBatch
    {
        readonly Dictionary<string, MeshKit> _kits = new Dictionary<string, MeshKit>();
        // casas: um lote por cor de parede (cada um com a sua paleta)
        readonly Dictionary<int, MeshKit> _houses = new Dictionary<int, MeshKit>();
        public int Tris { get; private set; }
        public bool Empty => _kits.Count == 0 && _houses.Count == 0;

        /// <summary>Põe o modelo no ponto (base no chão), girado em y e na escala dada. Devolve o modelo.</summary>
        public PropLib.Model Add(string name, Vector3 pos, float yaw, float scale, int paint = 0)
        {
            var m = PropLib.Get(name);
            if (m == null) return null;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            foreach (var p in m.parts)
            {
                MeshKit k;
                if (p.key == "pal") { if (!_houses.TryGetValue(paint, out k)) _houses[paint] = k = new MeshKit(); }
                else if (!_kits.TryGetValue(p.key, out k)) _kits[p.key] = k = new MeshKit();
                k.Append(p.v, p.n, p.uv, p.i, pos, rot, scale);
                Tris += p.i.Length / 3;
            }
            return m;
        }

        /// <summary>Árvore de copa redonda com a altura pedida (variação sorteada).</summary>
        public void Tree(Vector3 p, float h, float r01, float yaw) => Fit(Pick("Tree", r01), p, h, yaw);
        public void Pine(Vector3 p, float h, float r01, float yaw) => Fit(Pick("Pine", r01), p, h, yaw);

        public void Fit(string name, Vector3 p, float h, float yaw)
        {
            var m = PropLib.Get(name);
            if (m == null) return;
            Add(name, p, yaw, h / Mathf.Max(0.5f, m.max.y));
        }

        public static string Pick(string prefix, float r01)
        {
            var l = PropLib.Names(prefix);
            return l.Count == 0 ? null : l[Mathf.Clamp((int)(r01 * l.Count), 0, l.Count - 1)];
        }

        /// <summary>
        /// Cria os objetos. leaf = tom das folhas (multiplica a textura: outono, neve, noite);
        /// houseMain/houseTrim = cores das paredes de cada lote de casas (índice = paint).
        /// </summary>
        public void Emit(Transform root, bool shadows, Color leaf, bool night, Color[] houseMain = null)
        {
            float dim = night ? 0.55f : 1f;
            foreach (var kv in _kits)
            {
                var tex = PropLib.Tex(kv.Key);
                bool leafy = kv.Key.StartsWith("Leaves") || kv.Key == "Leaves" || kv.Key == "Grass" || kv.Key == "Flowers";
                Material mat = leafy
                    ? Gfx.Cutout(kv.Key == "Flowers" ? Color.white * dim : leaf * dim, tex, 0.45f, 0.08f)
                    : Gfx.Lit(Color.white * (night ? 0.6f : 1f), tex, kv.Key == "Rocks" ? 0.15f : 0.08f);
                kv.Value.Object("Cenário " + kv.Key, mat, root, shadows);
            }
            foreach (var kv in _houses)
            {
                var main = houseMain != null && houseMain.Length > 0 ? houseMain[kv.Key % houseMain.Length] : new Color(0.85f, 0.8f, 0.7f);
                var trim = Color.Lerp(main, new Color(0.3f, 0.28f, 0.26f), 0.45f);
                kv.Value.Object("Casas " + kv.Key, Gfx.Lit(Color.white, PropLib.PaletteTex(main, trim, night), 0.15f), root, shadows);
            }
        }
    }
}
