using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Clima da partida: sol, nublado, chuva, tempestade (raios) e neve. Com
    /// clima dinâmico o tempo muda ao longo do jogo. Atua na luz, neblina, céu,
    /// partículas presas à câmera e no brilho do gramado molhado.
    /// </summary>
    public class WeatherFx : MonoBehaviour
    {
        public static readonly string[] Kinds = { "sol", "nublado", "chuva", "tempestade", "neve" };
        public static readonly string[] Labels = { "Ensolarado", "Nublado", "Chuva", "Tempestade", "Neve" };
        static readonly string[] Messages = { "O tempo abriu", "O céu fechou", "Começou a chover", "Tempestade no estádio!", "Começou a nevar" };

        public string kind = "sol";
        public bool dynamic;
        public System.Action<string> onChange;
        public Light sun;
        public Material pitch, pitch2;
        public float baseSun = 1.2f;
        public Color baseFog, baseAmbient;
        ParticleSystem _rain, _snow, _splash;
        readonly Color[] _pitchBase = new Color[2];
        Camera _cam;

        static Texture2D _streak;
        static Texture2D Streak()
        {
            if (_streak != null) return _streak;
            const int w = 8, h = 64;
            _streak = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = Mathf.Clamp01(1f - Mathf.Abs(x - 3.5f) / 3.5f) * Mathf.Sin(y / (float)(h - 1) * Mathf.PI);
                    px[y * w + x] = new Color(1, 1, 1, a * a);
                }
            _streak.SetPixels32(px);
            _streak.Apply();
            return _streak;
        }

        /// <summary>Respingos no chão: anéis que abrem e somem perto da câmera.</summary>
        ParticleSystem Splashes()
        {
            var go = new GameObject("Respingos");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 0.28f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.85f, 0.9f, 1f, 0.5f);
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(44f, 0.01f, 30f);
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.3f, 1, 1.4f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.7f, 0), new GradientAlphaKey(0f, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.sharedMaterial = Gfx.Particles(Color.white, Gfx.Radial(32, Color.white, new Color(1, 1, 1, 0), 0.8f));
            var em = ps.emission;
            em.rateOverTime = 0f;
            return ps;
        }
        float _cloud, _rainK, _snowK, _storm, _next, _flash, _thunder;
        System.Random _rng;
        bool _snowMode;

        public void Setup(string k, bool dyn, uint seed, Camera cam, Light sunLight, Material pitchMat)
        {
            kind = System.Array.IndexOf(Kinds, k) >= 0 ? k : "sol";
            _snowMode = kind == "neve";
            dynamic = dyn;
            sun = sunLight;
            pitch = pitchMat;
            baseSun = sun != null ? sun.intensity : 1f;
            baseAmbient = RenderSettings.ambientLight;
            baseFog = RenderSettings.fogColor;
            _rng = new System.Random((int)seed * 31 + 7);
            _next = 25f + (float)_rng.NextDouble() * 20f;   // primeira virada logo no começo
            _rain = Particles(cam, true);
            _snow = Particles(cam, false);
            _cam = cam;
            _splash = Splashes();
            Target(out _cloud, out _rainK, out _snowK, out _storm);
            // jogo que já começa na chuva/neve: o gramado já tem água/neve acumulada
            Water = kind == "tempestade" ? 0.85f : kind == "chuva" ? 0.55f : 0f;
            SnowCover = kind == "neve" ? 0.55f : 0f;
            Apply(0f);
        }

        /// <summary>Água acumulada no gramado (0..1: as poças crescem com ela) e neve acumulada (0..1).</summary>
        public float Water, SnowCover;

        ParticleSystem Particles(Camera cam, bool rain)
        {
            // o emissor NÃO é filho da câmera: fica em cima do jogo, sempre na horizontal
            // (filho da câmera, girava junto e as gotas saíam de lado, "rodando" em volta do campo)
            var go = new GameObject(rain ? "Chuva" : "Neve");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = rain ? 1.1f : 7f;
            main.startSpeed = rain ? 28f : 1.2f;
            main.startSize = rain ? 0.018f : 0.09f;
            main.maxParticles = rain ? 2500 : 1800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = rain ? 0.6f : 0.04f;
            main.startColor = rain ? new Color(0.82f, 0.87f, 0.95f, 0.6f) : new Color(1f, 1f, 1f, 0.9f);
            var em = ps.emission;
            em.rateOverTime = rain ? 1800 : 260;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            // caixa local 60 x 50 (x, y) e fina em z; com o emissor girado 90° em x, y vira a
            // profundidade do campo e z (direção de saída) aponta para baixo
            sh.scale = new Vector3(60f, 50f, 1f);
            sh.rotation = Vector3.zero;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = rain ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.lengthScale = 1f;
            r.velocityScale = 0.035f;
            // chuva: risco fino (gota esticada pela velocidade); neve: floco redondo
            r.sharedMaterial = Gfx.Particles(Color.white, rain ? Streak() : Gfx.Radial(32, Color.white, new Color(1, 1, 1, 0), 1.5f));
            if (!rain)
            {
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.6f;
                noise.frequency = 0.3f;
            }
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // caixa deitada, gotas para baixo
            return ps;
        }

        void Target(out float cloud, out float rain, out float snow, out float storm)
        {
            cloud = rain = snow = storm = 0f;
            switch (kind)
            {
                case "nublado": cloud = 0.6f; break;
                case "chuva": cloud = 0.85f; rain = 0.65f; break;
                case "tempestade": cloud = 1f; rain = 1f; storm = 1f; break;
                case "neve": cloud = 0.75f; snow = 0.8f; break;
            }
        }

        void Update() => Apply(Time.deltaTime);

        void Apply(float dt)
        {
            if (dynamic && dt > 0f)
            {
                _next -= dt;
                if (_next <= 0f)
                {
                    _next = 35f + (float)_rng.NextDouble() * 35f;
                    string[] opts = _snowMode ? (kind == "neve" ? new[] { "nublado" } : new[] { "neve" })
                        : kind == "sol" ? new[] { "nublado" } : kind == "nublado" ? new[] { "sol", "chuva", "chuva" }
                        : kind == "chuva" ? new[] { "nublado", "tempestade", "nublado" } : new[] { "chuva" };
                    var nk = opts[_rng.Next(opts.Length)];
                    if (nk != kind)
                    {
                        kind = nk;
                        onChange?.Invoke(Messages[System.Array.IndexOf(Kinds, kind)]);
                    }
                }
            }
            Target(out var tc, out var tr, out var ts, out var tst);
            float k = dt <= 0f ? 1f : Mathf.Min(1f, dt * 0.35f);   // transição de ~5 s
            if (dt > 0f)
            {
                // a chuva enche o gramado aos poucos (temporal mais rápido) e ele seca devagar; a neve
                // acumula enquanto neva e derrete devagar
                Water = Mathf.Clamp01(Water + dt * (_rainK * 0.004f + _storm * 0.005f) - dt * 0.0015f * (1f - Mathf.Clamp01(_rainK * 3f)));
                SnowCover = Mathf.Clamp01(SnowCover + dt * _snowK * 0.0035f - dt * 0.001f * (1f - Mathf.Clamp01(_snowK * 3f)));
            }
            _cloud = Mathf.Lerp(_cloud, tc, k);
            _rainK = Mathf.Lerp(_rainK, tr, k);
            _snowK = Mathf.Lerp(_snowK, ts, k);
            _storm = Mathf.Lerp(_storm, tst, k);
            SetEmit(_rain, _rainK > 0.08f, 1800 * _rainK);
            SetEmit(_snow, _snowK > 0.08f, 300 * _snowK);
            SetEmit(_splash, _rainK > 0.1f, 900 * _rainK);
            if (_cam != null)
            {
                // nuvem de chuva/neve acima da região que a câmera vê
                var fw = _cam.transform.forward; fw.y = 0f;
                fw = fw.sqrMagnitude > 0.01f ? fw.normalized : Vector3.forward;
                var over = _cam.transform.position + fw * 16f;
                foreach (var ps in new[] { _rain, _snow })
                    if (ps != null) ps.transform.position = new Vector3(over.x, Mathf.Max(12f, _cam.transform.position.y + 8f), over.z);
            }
            if (_splash != null && _cam != null)
            {
                // respingos no chão à frente da câmera (onde o jogo está)
                var f = _cam.transform.forward; f.y = 0f;
                var gp = _cam.transform.position + (f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward) * 26f;
                _splash.transform.position = new Vector3(gp.x, 0.03f, gp.z);
                _splash.transform.rotation = Quaternion.LookRotation(f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward);
            }
            if (sun != null) sun.intensity = baseSun * (1f - _cloud * 0.55f) + _flash * 3f;
            RenderSettings.ambientLight = Color.Lerp(baseAmbient, baseAmbient * 0.8f + new Color(0.06f, 0.07f, 0.09f), _cloud) + Color.white * _flash * 0.6f;
            RenderSettings.fog = _rainK + _snowK > 0.05f || RenderSettings.fog;
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, 0.0015f + 0.005f * _rainK + 0.004f * _storm + 0.01f * _snowK, k);
            RenderSettings.fogColor = Color.Lerp(baseFog, new Color(0.45f, 0.48f, 0.53f), Mathf.Max(_rainK, _snowK) * 0.7f);
            // gramado molhado: mais escuro e saturado (sem brilho especular — no gramado plano ele
            // virava uma mancha de luz dura que andava em volta do campo com a câmera)
            for (int i = 0; i < 2; i++)
            {
                var pm = i == 0 ? pitch : pitch2;
                if (pm == null) continue;
                if (_pitchBase[i].a <= 0f) _pitchBase[i] = pm.HasProperty("_BaseColor") ? pm.GetColor("_BaseColor") : Color.white;
                var bc = _pitchBase[i];
                var wet = new Color(bc.r * 0.8f, bc.g * 0.86f, bc.b * 0.8f, bc.a);
                var c = Color.Lerp(bc, wet, _rainK);
                if (_snowK > 0.01f) c = Color.Lerp(c, new Color(1.6f, 1.6f, 1.7f), _snowK * 0.7f);
                pm.SetColor("_BaseColor", c);
                pm.SetFloat("_Smoothness", 0.15f);
            }
            if (_storm > 0.5f && dt > 0f)
            {
                _thunder -= dt;
                if (_thunder <= 0f)
                {
                    _thunder = 6f + (float)_rng.NextDouble() * 10f;
                    _flash = 1f;
                }
            }
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 3f);
        }

        static void SetEmit(ParticleSystem ps, bool on, float rate)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.rateOverTime = rate;
            if (on && !ps.isPlaying) ps.Play();
            else if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public string Label => Labels[Mathf.Max(0, System.Array.IndexOf(Kinds, kind))];
        public float Rain => _rainK;
        public float Storm => _storm;
    }
}
