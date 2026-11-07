using System.Collections;
using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez.UI
{
    /// <summary>
    /// Foto de verdade de cada local (estádio, ginásio, society, rua): monta o cenário 3D longe
    /// da cena, fotografa com uma câmera própria (camada separada) e guarda a imagem. Enquanto
    /// a foto não fica pronta, a tela mostra o desenho do MenuArt.
    /// </summary>
    public static class VenuePreview
    {
        const int Layer = 31;
        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        static bool _busy;

        public static string Key(string mode, int variant, int home, bool night) => mode + ":" + variant + ":" + home + ":" + (night ? "n" : "d");

        public static Texture2D Cached(string mode, int variant, int home, bool night)
            => _cache.TryGetValue(Key(mode, variant, home, night), out var t) ? t : null;

        /// <summary>Fotografa o local (no próximo quadro) e chama done com a imagem.</summary>
        public static void Request(MonoBehaviour host, string mode, int variant, int home, int away, bool night, int streetPlayers, System.Action<Texture2D> done)
        {
            var c = Cached(mode, variant, home, night);
            if (c != null) { done(c); return; }
            if (host == null) return;
            if (_busy) { _pending = () => Request(host, mode, variant, home, away, night, streetPlayers, done); return; }   // a última pedida espera a atual
            host.StartCoroutine(Shoot(mode, variant, home, away, night, streetPlayers, done));
        }

        static System.Action _pending;

        static void Next()
        {
            var p = _pending;
            _pending = null;
            p?.Invoke();
        }

        static IEnumerator Shoot(string mode, int variant, int homeI, int awayI, bool night, int streetPlayers, System.Action<Texture2D> done)
        {
            _busy = true;
            var home = Teams.Get(homeI);
            var away = Teams.Get(awayI);
            GameObject root = null, camGo = null, sunGo = null;
            RenderTexture rt = null;
            var fog = RenderSettings.fog;
            var amb = RenderSettings.ambientLight;
            var ambMode = RenderSettings.ambientMode;
            Texture2D tex = null;
            try
            {
                var sim = new MatchSim { streetPlayers = streetPlayers };
                sim.Setup(mode, new TeamSetup { data = home }, new TeamSetup { data = away }, 1u, 60f);
                var kits = Teams.PickKits(home, away, 0, -1);
                root = new GameObject("Prévia do local");
                int q = Mathf.Min(Settings.Quality, 1);
                Venue v;
                if (mode == GameModes.Futsal) v = VenueBuilder.Futsal(sim, home, away, kits[0], kits[1], variant, night, q, root.transform);
                else if (mode == GameModes.Street) v = VenueBuilder.Street(sim, home, away, kits[0], kits[1], variant, night, q, root.transform);
                else if (mode == GameModes.Society) v = VenueBuilder.Society(sim, home, away, kits[0], kits[1], variant, night, q, root.transform);
                else v = VenueBuilder.Stadium(sim, home, away, kits[0], kits[1], night, q, root.transform, variant);
                var off = new Vector3(0f, -3000f, 0f);
                root.transform.position = off;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                foreach (var l in root.GetComponentsInChildren<Light>(true)) l.cullingMask = 1 << Layer;
                // luz do "sol" só para a foto
                sunGo = new GameObject("Sol da prévia");
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.cullingMask = 1 << Layer;
                sun.intensity = night ? 0.6f : 1.15f;
                sun.color = night ? new Color(0.85f, 0.9f, 1f) : new Color(1f, 0.96f, 0.9f);
                sun.shadows = LightShadows.Soft;
                sunGo.transform.rotation = Quaternion.Euler(night ? 65f : 48f, 30f, 0f);
                RenderSettings.fog = false;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = night ? new Color(0.3f, 0.33f, 0.4f) : new Color(0.55f, 0.57f, 0.6f);
                // enquadramento de transmissão: alto, na lateral, pegando o campo e a arquibancada do outro lado
                float hl = sim.halfLen, hw = sim.halfW;
                bool field = mode == GameModes.Field;
                var pos = off + new Vector3(-hl * 0.35f, field ? 24f : 10f, -(hw + (field ? 48f : 16f)));
                var look = off + new Vector3(hl * 0.05f, field ? 2f : 1f, hw * 0.25f);
                camGo = new GameObject("Câmera da prévia");
                var cam = camGo.AddComponent<Camera>();
                cam.cullingMask = 1 << Layer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = night ? new Color(0.02f, 0.03f, 0.07f) : new Color(0.45f, 0.62f, 0.86f);
                cam.fieldOfView = field ? 44f : 52f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 600f;
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.LookRotation(look - pos);
                rt = new RenderTexture(768, 432, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;
                UrpSetup.Camera(cam, Settings.Quality);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[prévia] " + e.Message);
                Cleanup(root, camGo, sunGo, rt, fog, amb, ambMode);
                _busy = false;
                Next();
                yield break;
            }
            // a câmera renderiza sozinha (URP) no fim do quadro; dois quadros para sombras/luzes
            yield return null;
            yield return new WaitForEndOfFrame();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply(true);
            RenderTexture.active = prev;
            Cleanup(root, camGo, sunGo, rt, fog, amb, ambMode);
            _cache[Key(mode, variant, homeI, night)] = tex;
            _busy = false;
            done?.Invoke(tex);
            Next();
        }

        static void Cleanup(GameObject root, GameObject cam, GameObject sun, RenderTexture rt, bool fog, Color amb, UnityEngine.Rendering.AmbientMode mode)
        {
            if (cam != null) { cam.GetComponent<Camera>().targetTexture = null; Object.Destroy(cam); }
            if (root != null) Object.Destroy(root);
            if (sun != null) Object.Destroy(sun);
            if (rt != null) { rt.Release(); Object.Destroy(rt); }
            RenderSettings.fog = fog;
            RenderSettings.ambientLight = amb;
            RenderSettings.ambientMode = mode;
            Resources.UnloadUnusedAssets();   // texturas e materiais do cenário montado só para a foto
        }
    }
}
