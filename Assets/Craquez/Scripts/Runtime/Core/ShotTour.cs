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
            // desenhos 2D dos locais (vistos de fora), salvos direto em arquivo
            try
            {
                int k = 0;
                foreach (var (mode, v) in new[] { (GameModes.Field, 1), (GameModes.Field, 3), (GameModes.Field, 9), (GameModes.Field, 15), (GameModes.Field, 21), (GameModes.Field, 29), (GameModes.Futsal, 2), (GameModes.Society, 0) })
                {
                    var tex = Craquez.UI.VenueArt.Exterior(mode, v, new Color(0.1f, 0.35f, 0.8f), 960, 540);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(Dir, "local_" + (k++).ToString("00") + "_" + mode + v + ".png"), tex.EncodeToPNG());
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[shots] desenhos dos locais: " + ex.Message); }
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
                (GameModes.Field, false, "tarde", "tempestade", "campo_chuva"),
                (GameModes.Futsal, false, "noite", "sol", "futsal"),
                (GameModes.Street, false, "tarde", "sol", "rua"),
            })
            {
                var mc = _app.Tour_Match(mode, national, tod, weather);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot(tag + "_inicio", 2.5f);
                yield return Shot(tag + "_jogo", 6f);
                if ((weather == "chuva" || weather == "tempestade") && mc != null && mc.sim.puddles.Count > 0)
                {
                    // poça de perto: anéis da chuva e o reflexo
                    var bc = mc.view.bcam;
                    var pd = mc.sim.puddles[0];
                    bc.cinematic = true;
                    bc.cam.transform.position = bc.cinePos = new Vector3(pd.x + 5f, 2.6f, pd.y - 5f);
                    bc.cineLook = new Vector3(pd.x, 0f, pd.y);
                    bc.cam.transform.LookAt(bc.cineLook);
                    bc.cam.fieldOfView = bc.cineFov = 45f;
                    yield return Shot(tag + "_poca", 1.2f);
                    bc.cinematic = false;
                }
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
                if (mc != null && mc.view != null && (tag == "campo_noite" || tag == "futsal"))
                {
                    // sequência da corrida: tempo fixo por quadro (1/30 s) e câmera de lado acompanhando o
                    // atleta mais rápido no momento (dá para montar um GIF e ver a passada)
                    var bc = mc.view.bcam;
                    // espera alguém em corrida de verdade (rápido e sem marcar ninguém)
                    AthleteView best = null; float bv = 0f;
                    for (int tries = 0; tries < 90 && bv < 5.5f; tries++)
                    {
                        foreach (var av in mc.view.athletes)
                            if (!av.p.isKeeper && !av.p.shielding && av.p.vel.magnitude > bv) { bv = av.p.vel.magnitude; best = av; }
                        if (bv < 5.5f) { bv = 0f; yield return null; }
                    }
                    if (best != null)
                    {
                        Time.captureFramerate = 30;
                        bc.cinematic = true;
                        for (int f = 0; f < 36; f++)
                        {
                            var pp = best.transform.position;
                            var side = Quaternion.Euler(0, 90, 0) * new Vector3(best.p.vel.x, 0f, best.p.vel.y).normalized;
                            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                            bc.cam.transform.position = bc.cinePos = pp + side * 7f + Vector3.up * 1.0f;
                            bc.cineLook = pp + Vector3.up * 0.9f;
                            bc.cam.transform.LookAt(bc.cineLook);
                            bc.cam.fieldOfView = bc.cineFov = 30f;
                            yield return new WaitForEndOfFrame();
                            _n++;
                            var tex = ScreenCapture.CaptureScreenshotAsTexture();
                            File.WriteAllBytes(Path.Combine(Dir, $"{tag}_corrida_{f:00}.png"), tex.EncodeToPNG());
                            Destroy(tex);
                            yield return null;
                        }
                        Time.captureFramerate = 0;
                        bc.cinematic = false;
                    }
                }
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
                    // chute de verdade (do pé do atacante) e a reação do goleiro, quadro a quadro (1/30 s)
                    {
                        float gxS = sim.halfLen;
                        SimPlayer gkS = null, att = null;
                        foreach (var q in sim.players) if (q.isKeeper && Mathf.Sign(-sim.halfLen * sim.TeamDir(q.team)) == 1f) gkS = q;
                        if (gkS != null) foreach (var q in sim.players) if (q.team != gkS.team && !q.isKeeper) { att = q; break; }
                        if (gkS != null && att != null)
                        {
                            foreach (var q in sim.players) if (q != gkS && q != att) q.position = new Vector2(-sim.halfLen * 0.4f, q.position.y * 0.5f);
                            gkS.position = new Vector2(gxS - 1.2f, 0f); gkS.vel = Vector2.zero; gkS.saveKind = 0;
                            att.position = new Vector2(gxS - 17f, -4f); att.vel = new Vector2(3f, 0.5f); att.facing = new Vector2(1f, 0.25f).normalized;
                            sim.ball.owner = att.id; sim.ball.pos = att.position + att.facing * 0.45f; sim.ball.vel = att.vel; sim.ball.z = 0f;
                            sim.TourShoot(att, new Vector2(1f, 0.32f).normalized, 0.75f);
                            Time.captureFramerate = 30;
                            bcK.cinematic = true;
                            bcK.cam.transform.position = bcK.cinePos = new Vector3(gxS - 9f, 3.2f, -15f);
                            bcK.cineLook = new Vector3(gxS - 8f, 0.9f, 0f);
                            bcK.cam.transform.LookAt(bcK.cineLook);
                            bcK.cam.fieldOfView = bcK.cineFov = 52f;
                            for (int f = 0; f < 30; f++)
                            {
                                yield return new WaitForEndOfFrame();
                                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                                File.WriteAllBytes(Path.Combine(Dir, $"chute_defesa_{f:00}.png"), tex.EncodeToPNG());
                                Destroy(tex);
                                yield return null;
                            }
                            Time.captureFramerate = 0;
                            bcK.cinematic = false;
                            yield return new WaitForSecondsRealtime(1.5f);
                        }
                    }
                    // bicicleta (a silhueta da capa): cruzamento, de costas para o gol, contra os refletores
                    {
                        float gxB = sim.halfLen;
                        SimPlayer att = null, gkB = null;
                        foreach (var q in sim.players) if (q.isKeeper && Mathf.Sign(-sim.halfLen * sim.TeamDir(q.team)) == 1f) gkB = q;
                        if (gkB != null) foreach (var q in sim.players) if (q.team != gkB.team && !q.isKeeper) { att = q; break; }
                        if (att != null)
                        {
                            foreach (var q in sim.players) if (q != gkB && q != att) q.position = new Vector2(-sim.halfLen * 0.4f, q.position.y * 0.5f);
                            gkB.position = new Vector2(gxB - 1.2f, 0f); gkB.vel = Vector2.zero; gkB.saveKind = 0;
                            att.position = new Vector2(gxB - 9f, 0.5f); att.vel = Vector2.zero; att.facing = new Vector2(-1f, 0f);
                            att.down = 0f; att.stun = 0f; att.kickCd = 1.6f;   // (não cabeceia nem domina antes: a bicicleta é dele)
                            // cruzamento da esquerda: chega ao atleta a ~1,6 m de altura
                            var from = new Vector2(gxB - 14f, -9f);
                            var tgtB = att.position + new Vector2(0.2f, 0f);
                            float tB = 0.95f;
                            sim.ball.owner = -1; sim.ball.pos = from; sim.ball.vel = (tgtB - from) / tB; sim.ball.z = 0.4f;
                            sim.ball.vz = (1.5f - 0.4f + 0.5f * SimBall.G * tB * tB) / tB;
                            sim.ball.spin = 0f;
                            Time.captureFramerate = 30;
                            bcK.cinematic = true;
                            // câmera baixa, de lado, olhando para cima: o atleta recortado contra os refletores
                            bcK.cam.transform.position = bcK.cinePos = new Vector3(gxB - 9.5f, 0.9f, 0.5f - 8.5f);
                            bcK.cineLook = new Vector3(gxB - 9f, 2.2f, 0.5f);
                            bcK.cam.transform.LookAt(bcK.cineLook);
                            bcK.cam.fieldOfView = bcK.cineFov = 44f;
                            bool kicked = false;
                            for (int f = 0; f < 54; f++)
                            {
                                if (!kicked && f >= Mathf.RoundToInt(tB * 30f) - 1)
                                {
                                    kicked = true;
                                    att.facing = new Vector2(-1f, 0f);
                                    // contato no pé lá no alto (o corpo já está deitado no ar)
                                    sim.ball.pos = att.position + new Vector2(-0.25f, 0f);
                                    sim.ball.z = 1.75f;
                                    sim.Bicycle(att);
                                    // (fora do passo da simulação o evento se perde: aciona o desenho direto)
                                    if (att.id < mc.view.athletes.Count) mc.view.athletes[att.id].Bicycle(1.2f);
                                }
                                if (!kicked) { att.position = new Vector2(gxB - 9f, 0.5f); att.vel = Vector2.zero; att.facing = new Vector2(-1f, 0f); }
                                yield return new WaitForEndOfFrame();
                                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                                File.WriteAllBytes(Path.Combine(Dir, $"bicicleta_{f:00}.png"), tex.EncodeToPNG());
                                Destroy(tex);
                                yield return null;
                            }
                            Time.captureFramerate = 0;
                            bcK.cinematic = false;
                            yield return new WaitForSecondsRealtime(1.5f);
                        }
                    }
                    // queda: falta por trás no meio-campo (tropeço, chão, ajuda e levantar), quadro a quadro
                    for (int qk = 0; qk < 2; qk++)
                    {
                        SimPlayer vic = null, off = null;
                        foreach (var q in sim.players) if (!q.isKeeper && q.team == 0 && vic == null) vic = q;
                        foreach (var q in sim.players) if (!q.isKeeper && q.team == 1 && off == null) off = q;
                        if (vic == null || off == null) break;
                        sim.state = MatchState.Playing;
                        foreach (var q in sim.players) if (q != vic && q != off && !q.isKeeper) q.position = new Vector2(q.position.x * 0.3f + (q.team == 0 ? -12f : 12f), q.position.y * 0.6f);
                        var vp = new Vector2(qk == 0 ? -6f : 6f, 4f);
                        vic.position = vp; vic.facing = new Vector2(1f, 0f); vic.vel = new Vector2(qk == 0 ? 5f : 1.5f, 0f); vic.down = 0f; vic.stun = 0f;
                        // qk 0: por trás, em velocidade; qk 1: de frente (cai de costas)
                        off.position = qk == 0 ? vp - new Vector2(0.55f, 0.1f) : vp + new Vector2(0.6f, 0.05f);
                        off.vel = qk == 0 ? new Vector2(6.5f, 0f) : new Vector2(-5f, 0f); off.facing = off.vel.Norm();
                        sim.ball.owner = vic.id; sim.ball.pos = vp + new Vector2(0.4f, 0f); sim.ball.vel = vic.vel;
                        sim.Foul(off, vic);
                        Time.captureFramerate = 30;
                        bcK.cinematic = true;
                        var w = new Vector3(vp.x, 0f, vp.y);
                        bcK.cam.transform.position = bcK.cinePos = w + new Vector3(1.2f, 1.6f, -6.5f);
                        bcK.cineLook = w + new Vector3(1.2f, 0.5f, 0f);
                        bcK.cam.transform.LookAt(bcK.cineLook);
                        bcK.cam.fieldOfView = bcK.cineFov = 42f;
                        for (int f = 0; f < 150; f++)
                        {
                            bcK.cineLook = new Vector3(vic.position.x, 0.5f, vic.position.y);
                            bcK.cinePos = bcK.cineLook + new Vector3(0f, 1.1f, -6.5f);
                            yield return new WaitForEndOfFrame();
                            if (f % 5 == 0)
                            {
                                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                                File.WriteAllBytes(Path.Combine(Dir, $"queda{qk}_{f / 5:00}.png"), tex.EncodeToPNG());
                                Destroy(tex);
                            }
                            yield return null;
                        }
                        Time.captureFramerate = 0;
                        bcK.cinematic = false;
                        yield return new WaitForSecondsRealtime(1f);
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
                    foreach (var k in new[] { BroadcastCamera.Kind.Close, BroadcastCamera.Kind.Low, BroadcastCamera.Kind.PlayerSide, BroadcastCamera.Kind.Behind })
                    {
                        mc.view.bcam.mode = (int)k;
                        yield return Shot(tag + "_cam_" + k, 2.5f);
                    }
                    mc.view.bcam.mode = 0;
                }
                _app.Tour_EndMatch();
                yield return new WaitForSecondsRealtime(0.5f);
            }
            // arena de treino: só o atacante e o goleiro no CT, câmera atrás do jogador, sem placar
            {
                var mc = _app.Tour_Training();
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("treino_inicio", 2f);
                if (mc != null && mc.view != null)
                {
                    var bc = mc.view.bcam;
                    bc.cinematic = true;
                    bc.cam.transform.position = bc.cinePos = new Vector3(-mc.sim.halfLen * 0.2f, 34f, -mc.sim.halfW * 2.4f);
                    bc.cineLook = new Vector3(0f, 0f, 6f);
                    bc.cam.transform.LookAt(bc.cineLook);
                    bc.cam.fieldOfView = bc.cineFov = 50f;
                    yield return Shot("treino_ct", 1f);
                    bc.cinematic = false;
                }
                _app.Tour_EndMatch();
                yield return new WaitForSecondsRealtime(0.5f);
            }
            // tutorial do primeiro acesso (lista de metas e instrução no alto)
            {
                _app.StartTutorial();
                yield return new WaitForSecondsRealtime(2f);
                yield return Shot("tutorial", 1f);
                _app.Tour_EndMatch();
                yield return new WaitForSecondsRealtime(0.5f);
            }
            // estádios: vista aberta de cada modelo (arquibancada, cobertura, torcida, entorno)
            foreach (int m in new[] { 1, 6, 13, 15, 17, 18, 21, 26, 29 })
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
                    // de fora: vista aérea do entorno e a esplanada na altura de quem chega
                    if (m == 1 || m == 15 || m == 18 || m == 21 || m == 29)
                    {
                        bc.showAll = true;
                        bc.cam.transform.position = bc.cinePos = new Vector3(-hl * 1.4f, 70f, -hw * 3.6f);
                        bc.cineLook = new Vector3(0f, 0f, 0f);
                        bc.cam.transform.LookAt(bc.cineLook);
                        bc.cam.fieldOfView = bc.cineFov = 55f;
                        yield return Shot("estadio_" + m + "_aereo", 1.2f);
                        float pzv = mc.view.venue.plazaZ;
                        bc.cam.transform.position = bc.cinePos = new Vector3(hl * 0.35f, 1.7f, -(pzv + 9f));
                        bc.cineLook = new Vector3(0f, 7f, -(pzv - 11f));
                        bc.cam.transform.LookAt(bc.cineLook);
                        bc.cam.fieldOfView = bc.cineFov = 60f;
                        yield return Shot("estadio_" + m + "_esplanada", 1f);
                        bc.showAll = false;
                        bc.cam.fieldOfView = bc.cineFov = 62f;
                    }
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
