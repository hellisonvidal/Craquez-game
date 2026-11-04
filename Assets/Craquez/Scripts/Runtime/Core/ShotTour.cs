using System.Collections;
using System.IO;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Roteiro automático de screenshots (usado no CI): passa por todas as telas,
    /// joga trechos de partidas em cada modo e tira closes dos atletas.
    /// Ativado com o argumento de linha de comando --shots=PASTA.
    /// </summary>
    public class ShotTour : MonoBehaviour
    {
        public static string Dir;
        App _app;
        int _n;

        public static bool Requested()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (a.StartsWith("--shots=")) { Dir = a.Substring(8); return true; }
            return false;
        }

        float _beat;
        int _frames;

        void Update()
        {
            _frames++;
            if (Time.realtimeSinceStartup - _beat > 10f)
            {
                Debug.Log($"[shots] batimento: {Time.realtimeSinceStartup:F0}s, {_frames} quadros desde o último ({_frames / Mathf.Max(0.01f, Time.realtimeSinceStartup - _beat):F1} fps), tela {Screen.width}x{Screen.height}");
                _beat = Time.realtimeSinceStartup;
                _frames = 0;
            }
        }

        public void Run(App app)
        {
            Debug.Log("[shots] início do roteiro em " + Dir);
            _app = app;
            Directory.CreateDirectory(Dir);
            StartCoroutine(Tour());
        }

        IEnumerator Shot(string name, float wait = 0.6f)
        {
            yield return new WaitForSecondsRealtime(wait);
            yield return new WaitForEndOfFrame();
            _n++;
            string path = Path.Combine(Dir, _n.ToString("00") + "_" + name + ".png");
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log("[shots] " + path);
        }

        IEnumerator Tour()
        {
            yield return Shot("intro_claude", 1.6f);
            yield return new WaitForSecondsRealtime(3.4f);
            yield return Shot("intro_gratuito", 1.2f);
            yield return new WaitForSecondsRealtime(3.5f);
            yield return Shot("titulo", 2.5f);
            _app.Tour_Modes();
            yield return Shot("modos", 1.2f);
            _app.Tour_Setup(GameModes.Field, false);
            yield return Shot("config_campo", 1f);
            _app.Tour_Teams();
            yield return Shot("times_campo", 2f);
            _app.Tour_Tactics();
            yield return Shot("taticas", 1f);
            _app.Tour_Setup(GameModes.Field, true);
            _app.Tour_Teams();
            yield return Shot("times_selecoes", 2f);

            // partidas
            foreach (var (mode, national, tod, weather, tag) in new[]
            {
                (GameModes.Field, false, "noite", "sol", "campo_noite"),
                (GameModes.Field, true, "dia", "sol", "selecoes_dia"),
                (GameModes.Field, false, "tarde", "chuva", "campo_chuva"),
                (GameModes.Futsal, false, "noite", "sol", "futsal"),
                (GameModes.Street, false, "tarde", "sol", "rua"),
            })
            {
                var mc = _app.Tour_Match(mode, national, tod, weather);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot(tag + "_inicio", 2.5f);
                yield return Shot(tag + "_jogo", 6f);
                // close em um atleta (verifica corpo, uniforme e animação)
                if (mc != null && mc.view != null && mc.view.athletes.Count > 3)
                {
                    var bc = mc.view.bcam;
                    var a = mc.view.athletes[3];
                    bc.cinematic = true;
                    for (int k = 0; k < 2; k++)
                    {
                        var p = a.transform.position;
                        var side = k == 0 ? a.transform.forward : a.transform.right;
                        bc.cam.transform.position = p + side * 2.6f + Vector3.up * 1.4f;
                        bc.cinePos = bc.cam.transform.position;
                        bc.cineLook = p + Vector3.up * 1.0f;
                        bc.cam.transform.LookAt(bc.cineLook);
                        bc.cineFov = 38f;
                        bc.cam.fieldOfView = 38f;
                        yield return Shot(tag + "_close" + k, 0.4f);
                    }
                    bc.cinematic = false;
                }
                yield return Shot(tag + "_jogo2", 3f);
                _app.Tour_EndMatch();
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Debug.Log("[shots] fim");
            Application.Quit(0);
        }
    }
}
