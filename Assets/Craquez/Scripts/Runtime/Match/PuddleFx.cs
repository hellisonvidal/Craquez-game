using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Poças no gramado (as mesmas da simulação, onde a bola freia e quase não quica): crescem com a
    /// água acumulada, escuras e espelhadas; os pingos da chuva abrem anéis nelas; a bola e os atletas
    /// passando espirram água. A neve acumulada é só cor no gramado (WeatherFx) e física na bola.
    /// </summary>
    public class PuddleFx : MonoBehaviour
    {
        MatchSim _sim;
        WeatherFx _weather;
        MatchView _view;
        Transform[] _quads;
        ParticleSystem _rings, _spray;
        float _ringAcc;
        System.Random _rng = new System.Random(5);
        float[] _playerSplash;

        public static PuddleFx Create(Transform parent, MatchSim sim, WeatherFx weather, MatchView view)
        {
            if (sim.puddles.Count == 0) return null;
            var go = new GameObject("Poças");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<PuddleFx>();
            fx._sim = sim; fx._weather = weather; fx._view = view;
            fx.Build();
            return fx;
        }

        void Build()
        {
            var tex = Blob(128);
            // água: escura, levemente azulada e lisa (reflete o céu/refletores no brilho especular);
            // recorte pelo contorno irregular (o material transparente ignorava o alfa da textura e a
            // poça aparecia como um ladrilho quadrado)
            // azul claro de água refletindo o céu (escura demais parecia piche)
            var mat = Gfx.Cutout(new Color(0.56f, 0.74f, 0.92f), tex, 0.5f, 0.92f);
            _quads = new Transform[_sim.puddles.Count];
            for (int i = 0; i < _quads.Length; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "Poça " + i;
                q.transform.SetParent(transform, false);
                var pd = _sim.puddles[i];
                q.transform.localPosition = new Vector3(pd.x, 0.016f, pd.y);
                // formato irregular: alongada e girada ao acaso (sempre a mesma para a poça)
                q.transform.localRotation = Quaternion.Euler(90f, (i * 67) % 180, 0f);
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                _quads[i] = q.transform;
            }
            _rings = Rings();
            _spray = Spray();
            _playerSplash = new float[_sim.players.Count];
        }

        void Update()
        {
            if (_sim == null) return;
            float dt = Time.deltaTime;
            float wetA = Mathf.Clamp01((_sim.water - 0.2f) / 0.3f);
            for (int i = 0; i < _quads.Length; i++)
            {
                float r = _sim.PuddleRadius(i);
                bool on = r > 0.15f;
                if (_quads[i].gameObject.activeSelf != on) _quads[i].gameObject.SetActive(on);
                if (!on) continue;
                float stretch = 1f + 0.35f * ((i * 37) % 10) / 10f;
                _quads[i].localScale = new Vector3(r * 2f * stretch, r * 2f / stretch, 1f) * Mathf.Lerp(0.6f, 1f, wetA);
            }
            // pingos batendo nas poças: anéis que abrem e somem
            float rain = _weather != null ? _weather.Rain : 0f;
            _ringAcc += dt * rain * 260f;
            while (_ringAcc >= 1f && _rings != null)
            {
                _ringAcc -= 1f;
                int i = _rng.Next(_quads.Length);
                float r = _sim.PuddleRadius(i);
                if (r < 0.3f || !_quads[i].gameObject.activeSelf) continue;
                var pd = _sim.puddles[i];
                float a = (float)_rng.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)_rng.NextDouble()) * r * 0.8f;
                var ep = new ParticleSystem.EmitParams { position = new Vector3(pd.x + Mathf.Cos(a) * d, 0.03f, pd.y + Mathf.Sin(a) * d), applyShapeToPosition = false };
                _rings.Emit(ep, 1);
            }
            // bola e atletas atravessando poças: espirra água
            if (_spray == null) return;
            var b = _sim.ball;
            if (b.z < 0.12f && b.Speed > 2.5f && _sim.PuddleAt(b.pos) > 0.15f)
                SprayAt(new Vector3(b.pos.x, 0.05f, b.pos.y), new Vector3(b.vel.x, 0f, b.vel.y), Mathf.CeilToInt(b.Speed * 0.6f));
            for (int k = 0; k < _sim.players.Count; k++)
            {
                var p = _sim.players[k];
                _playerSplash[k] -= dt;
                if (_playerSplash[k] > 0f || p.vel.magnitude < 3f || p.sentOff) continue;
                if (_sim.PuddleAt(p.position) < 0.2f) continue;
                _playerSplash[k] = 0.28f;   // a cada passada
                SprayAt(new Vector3(p.position.x, 0.04f, p.position.y), new Vector3(p.vel.x, 0f, p.vel.y), 8);
            }
        }

        void SprayAt(Vector3 at, Vector3 vel, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var side = new Vector3(-vel.z, 0f, vel.x).normalized * ((float)_rng.NextDouble() - 0.5f) * 2.4f;
                var v = vel * (0.25f + 0.25f * (float)_rng.NextDouble()) + side + Vector3.up * (1.2f + 1.6f * (float)_rng.NextDouble());
                _spray.Emit(new ParticleSystem.EmitParams { position = at, velocity = v, applyShapeToPosition = false }, 1);
            }
        }

        ParticleSystem Rings()
        {
            var go = new GameObject("Pingos nas poças");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = false; m.playOnAwake = false;
            m.startLifetime = 0.6f; m.startSpeed = 0f; m.startSize = 0.16f;
            m.maxParticles = 900;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startColor = new Color(0.85f, 0.9f, 1f, 0.55f);
            var em = ps.emission; em.enabled = false;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.3f, 1f, 3.2f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.sharedMaterial = Gfx.Particles(Color.white, Ring(64));
            ps.Play();
            return ps;
        }

        ParticleSystem Spray()
        {
            var go = new GameObject("Respingos");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = false; m.playOnAwake = false;
            m.startLifetime = 0.45f; m.startSpeed = 0f; m.startSize = 0.045f;
            m.gravityModifier = 1f;
            m.maxParticles = 600;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startColor = new Color(0.82f, 0.88f, 0.95f, 0.75f);
            var em = ps.emission; em.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Gfx.Particles(Color.white, Gfx.Radial(32, Color.white, new Color(1, 1, 1, 0), 1.4f));
            ps.Play();
            return ps;
        }

        /// <summary>Mancha de água de borda irregular (o mesmo desenho para todas, girado e esticado).</summary>
        static Texture2D Blob(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float ang = Mathf.Atan2(v, u);
                    float edge = 0.78f + 0.1f * Mathf.Sin(ang * 3f + 0.7f) + 0.06f * Mathf.Sin(ang * 7f + 2.1f) + 0.04f * Mathf.Sin(ang * 13f);
                    float d = Mathf.Sqrt(u * u + v * v) / edge;
                    float a = Mathf.Clamp01((1f - d) / 0.18f);
                    // borda um pouco mais escura (terra molhada) e reflexos claros do céu no meio
                    float lum = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01((1f - d) / 0.35f));
                    float sky = Mathf.Clamp01(Mathf.Sin(u * 2.2f + v * 3.1f) * 0.5f + 0.5f) * 0.08f;
                    byte r = (byte)(Mathf.Clamp01(lum * 0.94f + sky) * 255), g = (byte)(Mathf.Clamp01(lum * 0.97f + sky) * 255), b = (byte)(Mathf.Clamp01(lum + sky) * 255);
                    px[y * size + x] = new Color32(r, g, b, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        static Texture2D Ring(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.1f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }
    }
}
