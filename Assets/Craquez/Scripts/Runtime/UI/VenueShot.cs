using System.Collections;
using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez.UI
{
    /// <summary>
    /// Foto de verdade do local da partida, vista de fora (para o seletor de locais): monta o
    /// estádio/ginásio/campo fora da vista, fotografa de cima com uma câmera própria (camada só dela)
    /// e desmonta. Fica guardada para não montar de novo.
    /// </summary>
    public static class VenueShot
    {
        const int Layer = 31;
        static readonly Dictionary<string, RenderTexture> _cache = new Dictionary<string, RenderTexture>();
        static bool _busy;

        public static string Key(string mode, int idx, int home) => mode + "/" + idx + "/" + (mode == GameModes.Field && idx == 0 ? home : -1);

        public static Texture Cached(string mode, int idx, int home) => _cache.TryGetValue(Key(mode, idx, home), out var rt) && rt != null ? rt : null;

        public static IEnumerator Render(string mode, int idx, int teamA, int teamB, int streetPlayers, System.Action<Texture> done)
        {
            string key = Key(mode, idx, teamA);
            if (_cache.TryGetValue(key, out var have) && have != null) { done?.Invoke(have); yield break; }
            while (_busy) yield return null;
            if (_cache.TryGetValue(key, out have) && have != null) { done?.Invoke(have); yield break; }
            _busy = true;
            yield return null;   // a tela já mostra o desenho; monta no quadro seguinte
            GameObject holder = null, camGo = null, lightGo = null;
            RenderTexture rt = null;
            try
            {
                var home = Teams.Get(teamA);
                var away = Teams.Get(teamB);
                var sim = new MatchSim();
                if (mode == GameModes.Street) sim.streetPlayers = streetPlayers;
                sim.Setup(mode, new TeamSetup { data = home }, new TeamSetup { data = away }, 1, 60f);
                var kits = Teams.PickKits(home, away, 0, -1);
                holder = new GameObject("Foto do local");
                holder.transform.position = new Vector3(0f, -3000f, 0f);
                if (mode == GameModes.Futsal) VenueBuilder.Futsal(sim, home, away, kits[0], kits[1], idx, false, 0, holder.transform);
                else if (mode == GameModes.Street) VenueBuilder.Street(sim, home, away, kits[0], kits[1], idx, false, 0, holder.transform);
                else if (mode == GameModes.Society) VenueBuilder.Society(sim, home, away, kits[0], kits[1], idx, false, 0, holder.transform);
                else VenueBuilder.Stadium(sim, home, away, kits[0], kits[1], false, 0, holder.transform, idx);
                foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                foreach (var l in holder.GetComponentsInChildren<Light>(true)) l.enabled = false;   // (luzes do teto do ginásio)
                rt = new RenderTexture(768, 432, 24) { name = "Local " + key, antiAliasing = 2 };
                camGo = new GameObject("Câmera da foto do local");
                var cam = camGo.AddComponent<Camera>();
                cam.cullingMask = 1 << Layer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.72f, 0.9f);
                cam.fieldOfView = 48f;
                cam.nearClipPlane = 0.5f;
                cam.farClipPlane = 2000f;
                cam.targetTexture = rt;
                float hl = sim.halfLen, hw = sim.halfW;
                // de fora e de cima, em diagonal: dá para ver a fachada/entorno e um pedaço do campo
                Vector3 pos = mode == GameModes.Field ? new Vector3(-hl * 1.5f, 72f, -hw * 3.7f)
                    : mode == GameModes.Futsal ? new Vector3(-hl * 2.6f, 34f, -hw * 4.4f)
                    : mode == GameModes.Society ? new Vector3(-hl * 1.7f, 30f, -hw * 3.2f)
                    : new Vector3(-hl * 2.2f, 20f, -hw * 3.4f);
                var origin = holder.transform.position;
                cam.transform.position = origin + pos;
                cam.transform.LookAt(origin + new Vector3(0f, mode == GameModes.Futsal ? 6f : 0f, 0f));
                lightGo = new GameObject("Sol da foto do local");
                var sun = lightGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.25f;
                sun.color = new Color(1f, 0.97f, 0.92f);
                sun.cullingMask = 1 << Layer;
                sun.shadows = LightShadows.None;
                lightGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[local] foto falhou: " + ex.Message);
                if (holder != null) Object.Destroy(holder);
                if (camGo != null) Object.Destroy(camGo);
                if (lightGo != null) Object.Destroy(lightGo);
                if (rt != null) rt.Release();
                _busy = false;
                yield break;
            }
            // a câmera desenha no fim deste quadro
            yield return new WaitForEndOfFrame();
            yield return null;
            Object.Destroy(camGo);
            Object.Destroy(lightGo);
            Object.Destroy(holder);
            _cache[key] = rt;
            _busy = false;
            yield return null;
            Resources.UnloadUnusedAssets();   // texturas e malhas do local montado só para a foto
            done?.Invoke(rt);
        }
    }
}
