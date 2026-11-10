using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Vida em volta do local de jogo: carros circulando na avenida/rua (mão dupla, com faróis à noite),
    /// bicicletas na beira da rua e pedestres andando na calçada. Tudo anda num retângulo de cantos
    /// arredondados em volta do campo (o anel da avenida do estádio, o estacionamento do ginásio, a rua do
    /// quarteirão). Malhas simples, uma por veículo/pessoa, mexidas só quando estão na tela.
    /// </summary>
    public class CityTraffic : MonoBehaviour
    {
        class Mover
        {
            public Transform t;
            public float s, speed, offset, phase;
            public int kind;        // 0 carro, 1 bicicleta, 2 pedestre
            public bool loop;       // anel externo (true) ou interno
            public Transform legL, legR, crank;
            public Renderer r;
        }

        readonly List<Mover> _m = new List<Mover>();
        Vector2 _road, _walk;
        float _cornerR = 6f;

        static readonly Color[] CarColors =
        {
            new Color(0.85f, 0.85f, 0.86f), new Color(0.12f, 0.12f, 0.13f), new Color(0.55f, 0.57f, 0.6f), new Color(0.7f, 0.1f, 0.1f),
            new Color(0.12f, 0.22f, 0.45f), new Color(0.95f, 0.95f, 0.95f), new Color(0.3f, 0.32f, 0.35f), new Color(0.9f, 0.75f, 0.1f),
            new Color(0.2f, 0.35f, 0.25f), new Color(0.62f, 0.5f, 0.38f),
        };
        static readonly Color[] Clothes =
        {
            new Color(0.15f, 0.2f, 0.35f), new Color(0.8f, 0.8f, 0.78f), new Color(0.6f, 0.15f, 0.15f), new Color(0.2f, 0.2f, 0.22f),
            new Color(0.35f, 0.5f, 0.3f), new Color(0.85f, 0.6f, 0.2f), new Color(0.4f, 0.55f, 0.75f), new Color(0.55f, 0.45f, 0.35f),
        };
        static readonly Color[] Skins = { new Color(0.95f, 0.78f, 0.65f), new Color(0.78f, 0.58f, 0.42f), new Color(0.55f, 0.38f, 0.26f), new Color(0.36f, 0.24f, 0.16f) };

        /// <param name="road">meia-largura (x) e meia-profundidade (z) do eixo da rua (centro entre as duas mãos)</param>
        /// <param name="walk">idem para a calçada onde andam os pedestres</param>
        public static CityTraffic Create(Transform parent, Vector2 road, Vector2 walk, int cars, int bikes, int people, bool night, int seed, bool skipCameraSide = false)
        {
            var go = new GameObject("Trânsito e pedestres");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<CityTraffic>();
            c._road = road; c._walk = walk;
            c._cornerR = Mathf.Min(8f, Mathf.Min(road.x, road.y) * 0.3f);
            c.Build(cars, bikes, people, night, seed);
            return c;
        }

        void Build(int cars, int bikes, int people, bool night, int seed)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            float per = Perimeter(_road);
            for (int i = 0; i < cars; i++)
            {
                bool outer = i % 2 == 0;
                var m = new Mover { kind = 0, loop = outer, s = per * (i + R() * 0.6f) / cars, speed = (outer ? 1f : -1f) * (7f + R() * 4f), offset = outer ? 1.8f : -1.8f };
                m.t = Car(CarColors[rng.Next(CarColors.Length)], night, R() < 0.25f).transform;
                Add(m);
            }
            for (int i = 0; i < bikes; i++)
            {
                var m = new Mover { kind = 1, s = per * (i + R()) / Mathf.Max(1, bikes), speed = (i % 2 == 0 ? 1f : -1f) * (3.5f + R() * 1.5f), offset = i % 2 == 0 ? 3.4f : -3.4f, phase = R() * 6f };
                m.t = Bike(Clothes[rng.Next(Clothes.Length)], Skins[rng.Next(Skins.Length)], out m.crank, out m.legL, out m.legR).transform;
                Add(m);
            }
            float wper = Perimeter(_walk);
            for (int i = 0; i < people; i++)
            {
                var m = new Mover { kind = 2, s = wper * (i + R()) / Mathf.Max(1, people), speed = (R() < 0.5f ? 1f : -1f) * (1.1f + R() * 0.5f), offset = (R() - 0.5f) * 1.6f, phase = R() * 6f };
                m.t = Person(Clothes[rng.Next(Clothes.Length)], Clothes[rng.Next(Clothes.Length)], Skins[rng.Next(Skins.Length)], 1.6f + R() * 0.25f, out m.legL, out m.legR).transform;
                Add(m);
            }
            Step(0f, true);
        }

        void Add(Mover m)
        {
            m.t.SetParent(transform, false);
            m.r = m.t.GetComponentInChildren<Renderer>();
            _m.Add(m);
        }

        void Update() => Step(Time.deltaTime, false);

        void Step(float dt, bool force)
        {
            float t = Time.time;
            foreach (var m in _m)
            {
                var loop = m.kind == 2 ? _walk : _road;
                float per = Perimeter(loop);
                m.s = Mathf.Repeat(m.s + m.speed * dt, per);
                // fora da tela: só anda (sem refazer pose)
                if (!force && m.r != null && !m.r.isVisible) { Place(m, loop, false); continue; }
                Place(m, loop, true);
                if (m.kind == 1)
                {
                    float a = (t * Mathf.Abs(m.speed) * 1.6f + m.phase) * Mathf.Rad2Deg;
                    if (m.crank != null) m.crank.localRotation = Quaternion.Euler(a, 0, 0);
                    if (m.legL != null) m.legL.localRotation = Quaternion.Euler(Mathf.Sin(a * Mathf.Deg2Rad) * 35f - 30f, 0, 0);
                    if (m.legR != null) m.legR.localRotation = Quaternion.Euler(-Mathf.Sin(a * Mathf.Deg2Rad) * 35f - 30f, 0, 0);
                }
                else if (m.kind == 2)
                {
                    float a = Mathf.Sin(t * Mathf.Abs(m.speed) * 4.2f + m.phase) * 24f;
                    if (m.legL != null) m.legL.localRotation = Quaternion.Euler(a, 0, 0);
                    if (m.legR != null) m.legR.localRotation = Quaternion.Euler(-a, 0, 0);
                }
            }
        }

        void Place(Mover m, Vector2 loop, bool rotate)
        {
            PointAt(loop, m.s, out var p, out var dir);
            if (m.speed < 0f) dir = -dir;
            var side = new Vector3(dir.z, 0f, -dir.x);   // direita de quem anda
            m.t.localPosition = p + side * Mathf.Abs(m.offset) * (m.kind == 2 ? Mathf.Sign(m.offset) : 1f);
            if (rotate && dir.sqrMagnitude > 1e-4f) m.t.localRotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        // ---------------------------------------------------------------- caminho (retângulo arredondado)
        float Perimeter(Vector2 h)
        {
            float r = Mathf.Min(_cornerR, Mathf.Min(h.x, h.y) * 0.5f);
            return 4f * (h.x - r) + 4f * (h.y - r) + 2f * Mathf.PI * r;
        }

        void PointAt(Vector2 h, float s, out Vector3 p, out Vector3 dir)
        {
            float r = Mathf.Min(_cornerR, Mathf.Min(h.x, h.y) * 0.5f);
            float ax = h.x - r, az = h.y - r;
            float arc = Mathf.PI * r * 0.5f;
            // lados: de baixo (z = -h.y, indo +x), direita, de cima (indo -x), esquerda, com um quarto de círculo entre eles
            float[] segs = { 2f * ax, arc, 2f * az, arc, 2f * ax, arc, 2f * az, arc };
            int i = 0;
            while (i < 7 && s > segs[i]) { s -= segs[i]; i++; }
            switch (i)
            {
                case 0: p = new Vector3(-ax + s, 0f, -h.y); dir = Vector3.right; return;
                case 2: p = new Vector3(h.x, 0f, -az + s); dir = Vector3.forward; return;
                case 4: p = new Vector3(ax - s, 0f, h.y); dir = Vector3.left; return;
                case 6: p = new Vector3(-h.x, 0f, az - s); dir = Vector3.back; return;
            }
            // cantos
            Vector3 c; float a0;
            if (i == 1) { c = new Vector3(ax, 0f, -az); a0 = -90f; }
            else if (i == 3) { c = new Vector3(ax, 0f, az); a0 = 0f; }
            else if (i == 5) { c = new Vector3(-ax, 0f, az); a0 = 90f; }
            else { c = new Vector3(-ax, 0f, -az); a0 = 180f; }
            float ang = (a0 + s / Mathf.Max(0.01f, r) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            p = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
            dir = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang));
        }

        // ---------------------------------------------------------------- modelos
        static Material _glass, _tire, _metal, _light, _tail;
        static readonly Dictionary<Color, Material> _paint = new Dictionary<Color, Material>();

        static Material Paint(Color c)
        {
            if (!_paint.TryGetValue(c, out var m) || m == null) _paint[c] = m = Gfx.Character(c, null, 0.6f);
            return m;
        }

        static GameObject Car(Color body, bool night, bool van)
        {
            _glass ??= Gfx.Lit(new Color(0.12f, 0.15f, 0.2f), null, 0.9f);
            _tire ??= Gfx.Lit(new Color(0.05f, 0.05f, 0.06f), null, 0.2f);
            _light ??= Gfx.Emissive(new Color(1f, 0.96f, 0.85f), new Color(2.5f, 2.3f, 2f));
            _tail ??= Gfx.Emissive(new Color(0.7f, 0.05f, 0.05f), new Color(1.6f, 0.1f, 0.08f));
            var go = new GameObject(van ? "Van" : "Carro");
            float L = van ? 4.8f : 4.2f, W = 1.8f, H = van ? 1.9f : 1.4f;
            var k = new MeshKit();
            k.Box(new Vector3(0, 0.62f, 0), new Vector3(W, 0.7f, L));
            if (van) k.Box(new Vector3(0, 1.35f, -0.25f), new Vector3(W * 0.98f, 0.95f, L * 0.78f));
            k.Object("lataria", Paint(body), go.transform, true);
            var g = new MeshKit();
            if (!van) g.Box(new Vector3(0, 1.2f, -0.15f), new Vector3(W * 0.86f, 0.5f, L * 0.48f));
            else g.Box(new Vector3(0, 1.45f, L * 0.36f), new Vector3(W * 0.9f, 0.55f, 0.1f));
            g.Object("vidros", _glass, go.transform, false);
            var tires = new MeshKit();
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var c = new Vector3(sx * (W * 0.5f - 0.05f), 0.33f, sz * L * 0.32f);
                    tires.Cylinder(c - Vector3.right * 0.12f, c + Vector3.right * 0.12f, 0.33f, 10);
                }
            tires.Object("pneus", _tire, go.transform, false);
            var lights = new MeshKit();
            foreach (float sx in new[] { -1f, 1f }) lights.Box(new Vector3(sx * 0.62f, 0.72f, L * 0.5f), new Vector3(0.36f, 0.14f, 0.05f));
            lights.Object("faróis", night ? _light : Gfx.Lit(new Color(0.9f, 0.9f, 0.85f), null, 0.8f), go.transform, false);
            var tail = new MeshKit();
            foreach (float sx in new[] { -1f, 1f }) tail.Box(new Vector3(sx * 0.66f, 0.75f, -L * 0.5f), new Vector3(0.3f, 0.14f, 0.05f));
            tail.Object("lanternas", _tail, go.transform, false);
            return go;
        }

        static GameObject Bike(Color shirt, Color skin, out Transform crank, out Transform legL, out Transform legR)
        {
            _metal ??= Gfx.Lit(new Color(0.2f, 0.22f, 0.25f), null, 0.6f, 0.6f);
            _tire ??= Gfx.Lit(new Color(0.05f, 0.05f, 0.06f), null, 0.2f);
            var go = new GameObject("Bicicleta");
            var f = new MeshKit();
            var rear = new Vector3(0, 0.34f, -0.52f); var front = new Vector3(0, 0.34f, 0.52f);
            var seat = new Vector3(0, 0.95f, -0.18f); var bar = new Vector3(0, 1.05f, 0.42f); var bb = new Vector3(0, 0.32f, 0f);
            f.Cylinder(rear, seat, 0.02f, 4); f.Cylinder(seat, bb, 0.02f, 4); f.Cylinder(bb, front + Vector3.up * 0.55f, 0.02f, 4);
            f.Cylinder(seat + Vector3.up * -0.05f, bar + Vector3.down * 0.05f, 0.02f, 4); f.Cylinder(front, bar, 0.02f, 4); f.Cylinder(rear, bb, 0.02f, 4);
            f.Box(bar, new Vector3(0.5f, 0.03f, 0.03f));
            f.Object("quadro", _metal, go.transform, false);
            Wheel(go.transform, rear); Wheel(go.transform, front);
            // ciclista: tronco inclinado, cabeça, braços no guidão
            var rider = new MeshKit();
            rider.Box(seat + new Vector3(0, 0.38f, 0.14f), new Vector3(0.36f, 0.55f, 0.22f));
            rider.Object("ciclista", Paint(shirt), go.transform, true);
            var head = new MeshKit();
            head.Box(seat + new Vector3(0, 0.82f, 0.3f), new Vector3(0.2f, 0.24f, 0.22f));
            head.Object("cabeça", Paint(skin), go.transform, false);
            crank = new GameObject("pedal").transform; crank.SetParent(go.transform, false); crank.localPosition = bb;
            legL = Leg(go.transform, seat + new Vector3(-0.1f, 0f, 0f), Paint(new Color(0.15f, 0.15f, 0.18f)), 0.8f);
            legR = Leg(go.transform, seat + new Vector3(0.1f, 0f, 0f), Paint(new Color(0.15f, 0.15f, 0.18f)), 0.8f);
            return go;
        }

        static void Wheel(Transform parent, Vector3 c)
        {
            var k = new MeshKit();
            k.Cylinder(c - Vector3.right * 0.03f, c + Vector3.right * 0.03f, 0.34f, 16);
            k.Object("roda", _tire, parent, false);
        }

        static Transform Leg(Transform parent, Vector3 hip, Material m, float len)
        {
            var pivot = new GameObject("perna").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = hip;
            var k = new MeshKit();
            k.Box(new Vector3(0, -len * 0.5f, 0), new Vector3(0.13f, len, 0.14f));
            k.Object("perna", m, pivot, false);
            return pivot;
        }

        static GameObject Person(Color top, Color bottom, Color skin, float h, out Transform legL, out Transform legR)
        {
            var go = new GameObject("Pedestre");
            float legLen = h * 0.47f;
            var body = new MeshKit();
            body.Box(new Vector3(0, legLen + h * 0.16f, 0), new Vector3(h * 0.24f, h * 0.32f, h * 0.13f));
            // braços junto ao corpo
            foreach (float sx in new[] { -1f, 1f }) body.Box(new Vector3(sx * h * 0.15f, legLen + h * 0.13f, 0), new Vector3(h * 0.06f, h * 0.3f, h * 0.07f));
            body.Object("roupa", Paint(top), go.transform, true);
            var head = new MeshKit();
            head.Box(new Vector3(0, legLen + h * 0.39f, 0), new Vector3(h * 0.11f, h * 0.13f, h * 0.12f));
            head.Object("cabeça", Paint(skin), go.transform, false);
            var lm = Paint(bottom);
            legL = Leg(go.transform, new Vector3(-h * 0.06f, legLen, 0), lm, legLen);
            legR = Leg(go.transform, new Vector3(h * 0.06f, legLen, 0), lm, legLen);
            return go;
        }
    }
}
