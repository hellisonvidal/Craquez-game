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
        volatile int _total;

        void Update()
        {
            _frames++;
            _total++;
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
            // tela virtual (xvfb) reporta 0 Hz: sem vsync o primeiro quadro não trava
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
            // vigia em outra thread: mostra se o thread principal travou
            var t = new System.Threading.Thread(() =>
            {
                int last = -1;
                while (true)
                {
                    System.Threading.Thread.Sleep(15000);
                    int f = _total;
                    Debug.Log($"[shots] vigia: quadro {f}" + (f == last ? " (thread principal parado!)" : ""));
                    last = f;
                }
            }) { IsBackground = true };
            t.Start();
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
            _app.Tour_Options();
            yield return Shot("opcoes", 1.2f);
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
                if (mc != null && tag == "campo_noite")
                {
                    // defesas: chute de média distância no canto (mergulho) e outro na altura do peito (encaixe)
                    var sim = mc.sim;
                    var bcK = mc.view.bcam;
                    foreach (var (yT, zT, nm) in new[] { (2.6f, 0.4f, "goleiro_mergulho"), (0.2f, 1.4f, "goleiro_encaixe"), (0.6f, 2.0f, "goleiro_alto") })
                    {
                        float gxK = sim.halfLen;
                        SimPlayer gkK = null;
                        foreach (var q in sim.players) if (q.isKeeper && Mathf.Sign(-sim.halfLen * sim.TeamDir(q.team)) == 1f) gkK = q;
                        if (gkK == null) break;
                        gkK.position = new Vector2(gxK - 1.5f, 0f); gkK.vel = Vector2.zero; gkK.saveKind = 0;
                        var from = new Vector2(gxK - 20f, 3f);
                        var tgt = new Vector2(gxK - 1.5f, yT);
                        var dd = tgt - from; float tt = dd.magnitude / 24f;
                        sim.ball.owner = -1; sim.ball.pos = from; sim.ball.vel = dd / tt; sim.ball.z = 0.11f;
                        sim.ball.vz = (zT + 0.5f * SimBall.G * tt * tt - 0.11f) / tt;
                        sim.ball.lastTouchTeam = 1 - gkK.team; sim.lastShotTick = sim.tick;
                        bcK.cinematic = true;
                        bcK.cam.transform.position = bcK.cinePos = new Vector3(gxK - 9f, 2.4f, -7f);
                        bcK.cineLook = new Vector3(gxK - 1.5f, 1.0f, 0f);
                        bcK.cam.transform.LookAt(bcK.cineLook);
                        bcK.cam.fieldOfView = bcK.cineFov = 40f;
                        yield return Shot(nm, 0.62f);
                        yield return Shot(nm + "2", 0.35f);
                        bcK.cinematic = false;
                        yield return new WaitForSecondsRealtime(1.5f);
                    }
                    // gol forçado: bola entrando no canto (rede balançando) e a comemoração sem corte
                    sim.ball.owner = -1;
                    sim.ball.pos = new Vector2(sim.halfLen - 1.2f, 1.5f);
                    sim.ball.vel = new Vector2(24f, 0.5f);
                    sim.ball.z = 1.1f;
                    sim.ball.vz = 0.5f;
                    yield return Shot("gol_rede", 0.5f);
                    yield return Shot("gol_rede2", 0.6f);
                    yield return Shot("gol_comemoracao", 2.5f);
                    yield return Shot("gol_comemoracao2", 3f);
                    // intervalo: relógio no fim do 1º tempo
                    yield return new WaitForSecondsRealtime(12f);
                    sim.clock = sim.halfSeconds;
                    yield return Shot("intervalo_apito", 1.2f);
                    yield return Shot("intervalo", 2.5f);
                }
                // câmeras próximas do jogador (só nas duas primeiras partidas, para não alongar o roteiro)
                if (mc != null && mc.view != null && (tag == "campo_noite" || tag == "futsal"))
                {
                    foreach (var k in new[] { BroadcastCamera.Kind.PlayerSide, BroadcastCamera.Kind.WideClose, BroadcastCamera.Kind.Behind })
                    {
                        mc.view.bcam.mode = (int)k;
                        yield return Shot(tag + "_cam_" + k, 2.5f);
                    }
                    mc.view.bcam.mode = 0;
                }
                _app.Tour_EndMatch();
                yield return new WaitForSecondsRealtime(0.5f);
            }
            // estádios: vista aberta de cada modelo (arquibancada, cobertura, torcida, entorno)
            foreach (int m in new[] { 1, 2, 5, 6, 7, 9, 11, 13, 14 })
            {
                var mc = _app.Tour_Match(GameModes.Field, false, m % 2 == 0 ? "noite" : "dia", "sol", m);
                yield return new WaitForSecondsRealtime(1.5f);
                if (mc != null && mc.view != null)
                {
                    var bc = mc.view.bcam;
                    float hl = mc.sim.halfLen, hw = mc.sim.halfW;
                    bc.cinematic = true;
                    bc.cam.transform.position = bc.cinePos = new Vector3(-hl * 0.55f, 16f, -hw * 0.9f);
                    bc.cineLook = new Vector3(hl * 0.2f, 8f, hw + 10f);
                    bc.cam.transform.LookAt(bc.cineLook);
                    bc.cam.fieldOfView = bc.cineFov = 62f;
                    yield return Shot("estadio_" + m, 1.2f);
                    bc.cam.transform.position = bc.cinePos = new Vector3(0f, 3f, -hw - 2f);
                    bc.cineLook = new Vector3(hl * 0.9f, 7f, 0f);
                    bc.cam.transform.LookAt(bc.cineLook);
                    yield return Shot("estadio_" + m + "_fundo", 1f);
                    // túnel dos vestiários, gol de perto (traves e rede) e a torcida da frente
                    var tz = mc.view.venue.tunnel.z;
                    // (como na cerimônia: mira na frente da porta; mirando atrás dela, a tribuna some)
                    bc.cam.transform.position = bc.cinePos = new Vector3(3f, 3.5f, tz + 18f);
                    bc.cineLook = new Vector3(0f, 2.2f, tz + 6f);
                    bc.cam.transform.LookAt(bc.cineLook);
                    bc.cam.fieldOfView = bc.cineFov = 48f;
                    if (m == 1 || m == 6) yield return Shot("estadio_" + m + "_tunel", 1f);
                    bc.cam.transform.position = bc.cinePos = new Vector3(hl - 7f, 2.0f, -6f);
                    bc.cineLook = new Vector3(hl + 1f, 1.2f, 0f);
                    bc.cam.transform.LookAt(bc.cineLook);
                    if (m == 1) yield return Shot("estadio_gol_perto", 0.8f);
                    bc.cam.transform.position = bc.cinePos = new Vector3(0f, 6f, hw + 2f);
                    bc.cineLook = new Vector3(6f, 9f, hw + 16f);
                    bc.cam.transform.LookAt(bc.cineLook);
                    if (m == 1 || m == 9) yield return Shot("estadio_" + m + "_torcida", 0.8f);
                    bc.cinematic = false;
                }
                _app.Tour_EndMatch();
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Debug.Log("[shots] fim");
            Application.Quit(0);
        }
    }
}
