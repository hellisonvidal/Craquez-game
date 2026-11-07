using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Elementos animados extras de cada local: câmera aérea em cabos no estádio, bandeirolas no
    /// teto do ginásio, bandeira no mastro do society, pipas, bandeirinhas de festa e varal na rua.
    /// Tudo leve (poucos vértices, animação por transform ou por vértice com vento).
    /// </summary>
    public class VenueExtras : MonoBehaviour
    {
        MatchSim _sim;
        readonly List<(Mesh mesh, Vector3[] rest, Vector3[] cur, float amp, float freq, float phase, float spanAxis)> _cloth = new List<(Mesh, Vector3[], Vector3[], float, float, float, float)>();
        readonly List<(Transform t, Vector3 center, Vector3 a, float freq, float phase, LineRenderer line, Vector3 anchor)> _kites = new List<(Transform, Vector3, Vector3, float, float, LineRenderer, Vector3)>();
        Transform _cam;
        LineRenderer[] _cables;
        Vector3[] _cableTop;
        Vector3 _camPos;

        public static VenueExtras Create(Transform parent, MatchSim sim, Venue v, Kit home, Kit away, bool night, int quality, int variant)
        {
            var go = new GameObject("Extras do local");
            go.transform.SetParent(parent, false);
            var x = go.AddComponent<VenueExtras>();
            x._sim = sim;
            float hl = sim.halfLen, hw = sim.halfW;
            string m = sim.mode.id;
            if (m == GameModes.Field) { x.SpiderCam(hl, hw, night); x.CornerFlagCloth(hl, hw, home.c1); }
            else if (m == GameModes.Futsal || (m == GameModes.Society && variant == 0)) x.Pennants(hl, hw, home, away, 10.5f);
            else if (m == GameModes.Society) x.MastFlag(new Vector3(-hl - 6f, 0, hw + 7f), home);
            else if (m == GameModes.Street)
            {
                x.Kites(hl, hw, variant);
                if (variant != 3 && variant != 5) x.Bunting(hl, hw, variant);   // terraço/telhado: sem fios
                if (variant == 0 || variant == 1 || variant == 8 || variant == 13) x.Clothesline(hl, hw, variant);
            }
            return x;
        }

        // ------------------------------------------------------------ estádio
        void SpiderCam(float hl, float hw, bool night)
        {
            var body = new MeshKit();
            body.Box(Vector3.zero, new Vector3(0.9f, 0.5f, 0.9f));
            body.Box(new Vector3(0, -0.45f, 0), new Vector3(0.35f, 0.4f, 0.45f));
            body.Cylinder(new Vector3(0, -0.5f, 0.2f), new Vector3(0, -0.5f, 0.45f), 0.12f, 10);   // lente
            var go = body.Object("Câmera aérea", Gfx.Lit(new Color(0.12f, 0.13f, 0.15f), null, 0.5f, 0.5f), transform, false);
            _cam = go.transform;
            _cableTop = new[] { new Vector3(-hl - 14f, 34f, -hw - 16f), new Vector3(hl + 14f, 34f, -hw - 16f), new Vector3(hl + 14f, 34f, hw + 16f), new Vector3(-hl - 14f, 34f, hw + 16f) };
            _cables = new LineRenderer[4];
            var mat = Gfx.Unlit(new Color(0.08f, 0.08f, 0.09f));
            for (int i = 0; i < 4; i++)
            {
                var lgo = new GameObject("Cabo " + i);
                lgo.transform.SetParent(transform, false);
                var lr = lgo.AddComponent<LineRenderer>();
                lr.sharedMaterial = mat;
                lr.widthMultiplier = 0.035f;
                lr.positionCount = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _cables[i] = lr;
            }
            _camPos = new Vector3(0, 19f, 0);
        }

        void CornerFlagCloth(float hl, float hw, Color c)
        {
            // pano extra nas bandeirinhas que ondula com o vento (por cima da bandeira fixa)
            var mat = Gfx.Cutout(c, Gfx.Solid(Color.white), 0.1f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var b = new Vector3(hl * sx, 1.2f, hw * sz);
                    Cloth("Bandeira de escanteio", b, -sx, 0.42f, 0.38f, 4, mat, 0.05f, 6f, 0f);
                }
        }

        // ------------------------------------------------------------ ginásio
        void Pennants(float hl, float hw, Kit home, Kit away, float y)
        {
            var cols = new[] { home.c1, home.c2, away.c1, Color.white, new Color(0.98f, 0.8f, 0.15f) };
            // varais em X pelo teto, fora do placar central
            foreach (var (a, b) in new[] { (new Vector3(-hl - 6f, y, -hw - 4f), new Vector3(hl + 6f, y, -hw * 0.25f)), (new Vector3(-hl - 6f, y, hw + 4f), new Vector3(hl + 6f, y, hw * 0.25f)) })
                Bunting(a, b, cols, 0.55f, 0.6f, 1.2f);
        }

        // ------------------------------------------------------------ society
        void MastFlag(Vector3 basePos, Kit team)
        {
            var mast = new MeshKit();
            mast.Cylinder(basePos, basePos + Vector3.up * 9f, 0.06f, 8);
            mast.Object("Mastro", Gfx.Lit(new Color(0.85f, 0.86f, 0.88f), null, 0.6f, 0.7f), transform, true);
            var tex = Gfx.Tex(64, 32);
            var px = new Color32[64 * 32];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 64; x++) px[y * 64 + x] = (Color32)(y < 11 ? team.c1 : y < 21 ? team.c2 : team.c1);
            tex.SetPixels32(px);
            tex.Apply(true);
            Cloth("Bandeira do clube", basePos + Vector3.up * 8.9f, 1f, 2.4f, 1.5f, 10, Gfx.Cutout(Color.white, tex, 0.1f), 0.18f, 3.2f, 0.4f);
        }

        // ------------------------------------------------------------ rua
        void Kites(float hl, float hw, int variant)
        {
            var rng = new System.Random(variant * 41 + 7);
            int n = 2 + variant % 2;
            var lineMat = Gfx.Unlit(new Color(0.9f, 0.9f, 0.88f));
            for (int i = 0; i < n; i++)
            {
                Color c = Color.HSVToRGB((float)rng.NextDouble(), 0.8f, 0.95f), c2 = Color.HSVToRGB((float)rng.NextDouble(), 0.7f, 0.95f);
                var k = new MeshKit();
                // pipa (losango) com rabiola
                k.Quad(new Vector3(0, 0.55f, 0), new Vector3(0.4f, 0, 0), new Vector3(0, -0.6f, 0), new Vector3(-0.4f, 0, 0));
                k.Quad(new Vector3(-0.4f, 0, 0), new Vector3(0, -0.6f, 0), new Vector3(0.4f, 0, 0), new Vector3(0, 0.55f, 0));
                var go = k.Object("Pipa " + i, Gfx.Lit(c, null, 0.2f), transform, false);
                var tail = new MeshKit();
                for (int j = 0; j < 5; j++)
                {
                    float y0 = -0.7f - j * 0.28f, y1 = y0 - 0.14f;
                    tail.Quad(new Vector3(-0.06f, y0, 0), new Vector3(0.06f, y0, 0), new Vector3(0.06f, y1, 0), new Vector3(-0.06f, y1, 0));
                    tail.Quad(new Vector3(-0.06f, y1, 0), new Vector3(0.06f, y1, 0), new Vector3(0.06f, y0, 0), new Vector3(-0.06f, y0, 0));
                }
                tail.Object("Rabiola", Gfx.Lit(c2, null, 0.2f), go.transform, false);
                var center = new Vector3((float)(rng.NextDouble() * 2 - 1) * hl * 1.3f, 22f + (float)rng.NextDouble() * 10f, hw + 25f + (float)rng.NextDouble() * 25f);
                var lgo = new GameObject("Linha da pipa");
                lgo.transform.SetParent(transform, false);
                var lr = lgo.AddComponent<LineRenderer>();
                lr.sharedMaterial = lineMat;
                lr.widthMultiplier = 0.02f;
                lr.positionCount = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _kites.Add((go.transform, center, new Vector3(3f + (float)rng.NextDouble() * 3f, 1.5f + (float)rng.NextDouble() * 1.5f, 1f), 0.25f + (float)rng.NextDouble() * 0.25f, (float)rng.NextDouble() * 6f, lr, center + new Vector3(-6f, -center.y + 1.5f, 25f)));
            }
        }

        void Bunting(float hl, float hw, int variant)
        {
            Color[] festa = { new Color(0.95f, 0.2f, 0.2f), new Color(0.98f, 0.8f, 0.1f), new Color(0.15f, 0.55f, 0.95f), new Color(0.2f, 0.75f, 0.3f), new Color(0.95f, 0.45f, 0.75f), Color.white };
            float y = 6.5f;
            Bunting(new Vector3(-hl, y, -hw - 3f), new Vector3(-hl * 0.3f, y, hw + 3f), festa, 0.35f, 0.42f, 0.7f);
            Bunting(new Vector3(hl * 0.3f, y, -hw - 3f), new Vector3(hl, y, hw + 3f), festa, 0.35f, 0.42f, 0.7f);
        }

        void Clothesline(float hl, float hw, int variant)
        {
            Color[] roupa = { new Color(0.9f, 0.9f, 0.92f), new Color(0.2f, 0.35f, 0.7f), new Color(0.85f, 0.25f, 0.2f), new Color(0.95f, 0.8f, 0.3f), new Color(0.3f, 0.6f, 0.35f) };
            var a = new Vector3(-hl * 0.8f, 4.2f, hw + 6f);
            var b = new Vector3(hl * 0.6f, 4.4f, hw + 6f);
            var rope = new MeshKit();
            rope.Cylinder(a, b, 0.015f, 4);
            rope.Object("Varal", Gfx.Lit(new Color(0.8f, 0.8f, 0.78f), null, 0.1f), transform, false);
            int n = 7;
            for (int i = 0; i < n; i++)
            {
                var p = Vector3.Lerp(a, b, (i + 0.5f) / n);
                float w = 0.5f + (i % 3) * 0.2f, h = 0.6f + (i % 2) * 0.35f;
                var tex = Gfx.Solid(roupa[(i + variant) % roupa.Length]);
                // pendurada no varal, balança no vento (eixo do pano = vertical)
                ClothHanging("Roupa " + i, p, w, h, Gfx.Cutout(roupa[(i + variant) % roupa.Length], tex, 0.1f), 0.06f, 2.2f, i * 0.7f);
            }
        }

        // ------------------------------------------------------------ peças reutilizáveis
        /// <summary>Fileira de bandeirolas triangulares num fio com barriga (catenária) que balança.</summary>
        void Bunting(Vector3 a, Vector3 b, Color[] cols, float w, float h, float gap)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(2, Mathf.FloorToInt(len / gap));
            var wire = new MeshKit();
            var flags = new MeshKit();
            Vector3 Sag(float t) => Vector3.Lerp(a, b, t) - Vector3.up * Mathf.Sin(t * Mathf.PI) * len * 0.06f;
            for (int i = 0; i < 12; i++) wire.Cylinder(Sag(i / 12f), Sag((i + 1) / 12f), 0.01f, 3);
            var dir = (b - a).normalized;
            var byColor = new MeshKit[cols.Length];
            for (int i = 0; i < n; i++)
            {
                var p = Sag((i + 0.5f) / n);
                var mk = byColor[i % cols.Length] ??= new MeshKit();
                var l = p - dir * w * 0.5f; var r = p + dir * w * 0.5f; var tip = p - Vector3.up * h;
                mk.Quad(l, r, tip, tip);
                mk.Quad(tip, tip, r, l);
            }
            wire.Object("Fio", Gfx.Lit(new Color(0.2f, 0.2f, 0.2f), null, 0.1f), transform, false);
            for (int c = 0; c < cols.Length; c++)
            {
                if (byColor[c] == null) continue;
                var go = byColor[c].Object("Bandeirolas", Gfx.Lit(cols[c], null, 0.15f), transform, false);
                AddWave(go.GetComponent<MeshFilter>().sharedMesh, 0.05f, 1.4f, a.x * 0.1f + c, 1);
            }
        }

        /// <summary>Pano preso por uma borda vertical (bandeira), ondula mais na ponta solta.</summary>
        void Cloth(string name, Vector3 pole, float dirX, float w, float h, int seg, Material mat, float amp, float freq, float phase)
        {
            var k = new MeshKit();
            for (int i = 0; i < seg; i++)
            {
                float x0 = i * w / seg, x1 = (i + 1) * w / seg;
                k.Quad(pole + new Vector3(dirX * x0, 0, 0), pole + new Vector3(dirX * x0, -h, 0), pole + new Vector3(dirX * x1, -h, 0), pole + new Vector3(dirX * x1, 0, 0), new Vector2(x0 / w, 1), new Vector2(x1 / w, 0));
                k.Quad(pole + new Vector3(dirX * x1, 0, 0), pole + new Vector3(dirX * x1, -h, 0), pole + new Vector3(dirX * x0, -h, 0), pole + new Vector3(dirX * x0, 0, 0), new Vector2(x1 / w, 1), new Vector2(x0 / w, 0));
            }
            var go = k.Object(name, mat, transform, false);
            AddWave(go.GetComponent<MeshFilter>().sharedMesh, amp, freq, phase, 0, pole.x, dirX);
        }

        void ClothHanging(string name, Vector3 top, float w, float h, Material mat, float amp, float freq, float phase)
        {
            var k = new MeshKit();
            k.Quad(top + new Vector3(-w / 2, 0, 0), top + new Vector3(w / 2, 0, 0), top + new Vector3(w / 2, -h, 0), top + new Vector3(-w / 2, -h, 0));
            k.Quad(top + new Vector3(-w / 2, -h, 0), top + new Vector3(w / 2, -h, 0), top + new Vector3(w / 2, 0, 0), top + new Vector3(-w / 2, 0, 0));
            var go = k.Object(name, mat, transform, false);
            AddWave(go.GetComponent<MeshFilter>().sharedMesh, amp, freq, phase, 2, top.y);
        }

        /// <summary>mode 0 = bandeira (cresce com a distância do mastro em x), 1 = bandeirolas (cresce para baixo do fio), 2 = pendurado (cresce para baixo do topo).</summary>
        void AddWave(Mesh mesh, float amp, float freq, float phase, int mode, float origin = 0f, float dirX = 1f)
        {
            mesh.MarkDynamic();
            var rest = mesh.vertices;
            var cur = (Vector3[])rest.Clone();
            _cloth.Add((mesh, rest, cur, amp, freq, phase, mode * 1000f + origin + (dirX < 0 ? 0.5f : 0f)));
            _clothOrigin.Add(origin);
            _clothMode.Add(mode);
            _clothDir.Add(dirX);
        }

        readonly List<float> _clothOrigin = new List<float>(), _clothDir = new List<float>();
        readonly List<int> _clothMode = new List<int>();
        float _t;

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            // tecidos ao vento
            for (int i = 0; i < _cloth.Count; i++)
            {
                var c = _cloth[i];
                int mode = _clothMode[i];
                float o = _clothOrigin[i];
                for (int k = 0; k < c.rest.Length; k++)
                {
                    var r = c.rest[k];
                    float wgt = mode == 0 ? Mathf.Abs(r.x - o) : mode == 1 ? 0.6f : Mathf.Clamp01((o - r.y) * 1.5f);
                    float wave = Mathf.Sin(_t * c.freq + c.phase + r.x * 2.2f + r.z * 1.3f) * c.amp * wgt;
                    float flap = Mathf.Sin(_t * c.freq * 1.7f + c.phase + r.x * 4f) * c.amp * 0.4f * wgt;
                    c.cur[k] = r + new Vector3(0, flap * 0.3f, wave + flap);
                }
                c.mesh.vertices = c.cur;
            }
            // pipas: oito no céu, linha até a mão de alguém lá longe
            foreach (var k in _kites)
            {
                float a = _t * k.freq + k.phase;
                var pos = k.center + new Vector3(Mathf.Sin(a) * k.a.x, Mathf.Sin(a * 2f) * k.a.y, Mathf.Cos(a * 0.7f) * k.a.z);
                k.t.position = pos;
                k.t.rotation = Quaternion.LookRotation(Vector3.forward) * Quaternion.Euler(0, 0, Mathf.Sin(a * 2f) * 25f);
                k.line.SetPosition(0, pos);
                k.line.SetPosition(1, k.anchor);
            }
            // câmera aérea: segue a bola de longe, devagar, sempre acima dos jogadores
            if (_cam != null && _sim != null)
            {
                var want = new Vector3(_sim.ball.pos.x * 0.7f, 18f + Mathf.Sin(_t * 0.2f) * 2f, _sim.ball.pos.y * 0.5f - 6f);
                _camPos = Vector3.Lerp(_camPos, want, 1f - Mathf.Exp(-dt * 0.5f));
                _cam.position = _camPos;
                var look = new Vector3(_sim.ball.pos.x, 0, _sim.ball.pos.y) - _camPos;
                _cam.rotation = Quaternion.Slerp(_cam.rotation, Quaternion.LookRotation(new Vector3(look.x, 0, look.z).sqrMagnitude > 0.1f ? new Vector3(look.x, 0, look.z) : Vector3.forward), 1f - Mathf.Exp(-dt * 1.5f));
                for (int i = 0; i < 4; i++)
                {
                    _cables[i].SetPosition(0, _camPos + Vector3.up * 0.25f);
                    _cables[i].SetPosition(1, _cableTop[i]);
                }
            }
        }
    }
}
