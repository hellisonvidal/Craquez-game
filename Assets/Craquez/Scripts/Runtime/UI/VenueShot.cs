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
        // foto copiada para uma textura comum: a RenderTexture perde o conteúdo no Android (app em segundo
        // plano, depois de uma partida) e o quadro ficava preto
        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        static bool _busy;

        public static string Key(string mode, int idx, int home) => mode + "/" + idx + "/" + (mode == GameModes.Field && idx == 0 ? home : -1);

        /// <summary>Foto já pronta: da memória ou, se não, do disco (fotos salvas em execuções anteriores).</summary>
        public static Texture Cached(string mode, int idx, int home)
        {
            string key = Key(mode, idx, home);
            if (_cache.TryGetValue(key, out var t) && t != null) return t;
            var d = LoadDisk(key);
            if (d != null) { _cache[key] = d; return d; }
            return null;
        }

        // fotos salvas no aparelho: a segunda vez que o seletor abre (e nas próximas execuções) é instantâneo.
        // O carimbo muda com a versão do jogo e o número de estádios (refaz as fotos depois de atualizar)
        static string Dir => System.IO.Path.Combine(Application.persistentDataPath, "locais");
        static string FileFor(string key) => System.IO.Path.Combine(Dir, key.Replace('/', '_') + "_" + Application.version + "_" + VenueBuilder.StadiumModels.Length + ".jpg");

        static Texture2D LoadDisk(string key)
        {
            try
            {
                string f = FileFor(key);
                if (!System.IO.File.Exists(f)) return null;
                var t = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "Local " + key, wrapMode = TextureWrapMode.Clamp };
                if (!t.LoadImage(System.IO.File.ReadAllBytes(f), true)) { Object.Destroy(t); return null; }
                return t;
            }
            catch (System.Exception ex) { Debug.LogWarning("[local] foto do disco falhou: " + ex.Message); return null; }
        }

        static void SaveDisk(string key, Texture2D t)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Dir);
                System.IO.File.WriteAllBytes(FileFor(key), t.EncodeToJPG(86));
            }
            catch (System.Exception ex) { Debug.LogWarning("[local] salvar a foto falhou: " + ex.Message); }
        }

        /// <summary>
        /// Monta antes as fotos de todos os locais da modalidade (uma por vez, com folga entre elas), para o
        /// seletor já abrir com as miniaturas prontas. <paramref name="each"/> é chamado a cada foto pronta.
        /// </summary>
        public static IEnumerator Prewarm(string mode, int count, int teamA, int teamB, int streetPlayers, System.Action<int, Texture> each = null, System.Func<bool> hold = null)
        {
            for (int i = 0; i < count; i++)
            {
                while (hold != null && hold()) yield return new WaitForSecondsRealtime(1f);   // (nunca durante a partida)
                if (i > 2 && Cached(mode, i, teamA) == null) yield return new WaitForSecondsRealtime(0.6f);   // folga para o menu
                var have = Cached(mode, i, teamA);
                if (have != null) { each?.Invoke(i, have); continue; }
                Texture got = null;
                yield return Render(mode, i, teamA, teamB, streetPlayers, t => got = t);
                if (got != null) each?.Invoke(i, got);
                yield return null;
                yield return null;
            }
        }

        public static IEnumerator Render(string mode, int idx, int teamA, int teamB, int streetPlayers, System.Action<Texture> done)
        {
            string key = Key(mode, idx, teamA);
            if (_cache.TryGetValue(key, out var have) && have != null) { done?.Invoke(have); yield break; }
            var disk = LoadDisk(key);
            if (disk != null) { _cache[key] = disk; done?.Invoke(disk); yield break; }
            while (_busy) yield return null;
            if (_cache.TryGetValue(key, out have) && have != null) { done?.Invoke(have); yield break; }
            _busy = true;
            yield return null;   // a tela já mostra o desenho; monta no quadro seguinte
            GameObject holder = null, camGo = null, lightGo = null;
            RenderTexture rt = null;
            try
            {
                FanCrowd.Empty = false; FanCrowd.Rain = false; Gfx.CrowdEmpty = false; Gfx.CrowdRain = false;   // foto: torcida normal
                var home = Teams.Get(teamA);
                var away = Teams.Get(teamB);
                var sim = new MatchSim();
                if (mode == GameModes.Street) { sim.streetPlayers = streetPlayers; sim.smallGoals = System.Array.IndexOf(GameModes.SmallGoalStreets, idx) >= 0; sim.streetKeeper = System.Array.IndexOf(GameModes.KeeperStreets, idx) >= 0; }
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
                rt = new RenderTexture(768, 432, 24, RenderTextureFormat.ARGB32) { name = "Local " + key, antiAliasing = 1 };
                camGo = new GameObject("Câmera da foto do local");
                var cam = camGo.AddComponent<Camera>();
                cam.cullingMask = 1 << Layer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.72f, 0.9f);
                cam.fieldOfView = 48f;
                cam.nearClipPlane = 0.5f;
                cam.farClipPlane = 2000f;
                cam.targetTexture = rt;
                cam.enabled = false;   // desenha só uma vez, na hora da cópia (com a luz da foto)
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
            Texture2D photo = null;
            try
            {
                // desenha de novo na hora (garante o conteúdo) e copia para uma textura comum; com a luz do
                // dia própria da foto (no URP só o "sol" da cena ilumina de verdade: à noite, ou com o sol do
                // menu, a foto saía preta)
                var cam0 = camGo.GetComponent<Camera>();
                var oldSun = RenderSettings.sun;
                var oldAmb = RenderSettings.ambientMode;
                var oldSky = RenderSettings.ambientSkyColor; var oldEq = RenderSettings.ambientEquatorColor; var oldGr = RenderSettings.ambientGroundColor;
                var oldAmbL = RenderSettings.ambientLight;
                bool oldFog = RenderSettings.fog;
                RenderSettings.sun = lightGo.GetComponent<Light>();
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.82f);
                RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.5f);
                RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.28f);
                RenderSettings.fog = false;
                try { cam0.Render(); }
                finally
                {
                    RenderSettings.sun = oldSun; RenderSettings.ambientMode = oldAmb;
                    RenderSettings.ambientSkyColor = oldSky; RenderSettings.ambientEquatorColor = oldEq; RenderSettings.ambientGroundColor = oldGr;
                    RenderSettings.ambientLight = oldAmbL; RenderSettings.fog = oldFog;
                }
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                photo = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false) { name = "Local " + key, wrapMode = TextureWrapMode.Clamp };
                photo.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                photo.Apply(false, false);
                SaveDisk(key, photo);
                photo.Apply(false, true);   // sem cópia na memória do processador
                RenderTexture.active = prev;
            }
            catch (System.Exception ex) { Debug.LogWarning("[local] cópia da foto falhou: " + ex.Message); photo = null; }
            Object.Destroy(camGo);
            Object.Destroy(lightGo);
            Object.Destroy(holder);
            rt.Release();
            Object.Destroy(rt);
            if (photo != null) _cache[key] = photo;
            _busy = false;
            yield return null;
            Resources.UnloadUnusedAssets();   // texturas e malhas do local montado só para a foto
            done?.Invoke(photo);
        }
    }
}
