using System.Collections;
using System.Collections.Generic;
using Craquez.Sim;
using Craquez.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Craquez
{
    /// <summary>
    /// Replay instantâneo: grava os últimos 30 s da partida (estado completo de cada atleta e da bola a
    /// cada passo da simulação) e, aberto pela pausa, reproduz o trecho com linha do tempo, câmera lenta,
    /// pausa e câmeras à escolha — inclusive livre, girando em volta da bola ou de um atleta, com zoom.
    /// Ao sair, a partida volta exatamente ao ponto onde foi pausada.
    /// </summary>
    public class InstantReplay : MonoBehaviour
    {
        struct PFull
        {
            public Vector2 pos, vel, facing, fallDir;
            public float slide, dive, stun, down, downTotal, hold, saveT, saveTotal, saveSide, saveH, saveDist, saveContact, windup, windupTotal, kickAnim, tackleAnim, skillT, offBalance;
            public int saveKind, fallKind;
            public bool goalKick, fallRoll, fallLight, fallTrip, sprinting, shielding;
        }

        class Frame
        {
            public PFull[] p;
            public Vector2 ballPos, ballVel;
            public float ballZ, ballVz, ballSpin;
            public int owner;
            public readonly List<SimEvent> ev = new List<SimEvent>();
        }

        const int Keep = 60 * 30;
        readonly Frame[] _ring = new Frame[Keep];
        int _head, _count;

        public bool HasFootage => _count > 60;

        public void Record(MatchSim sim)
        {
            var f = _ring[_head] ??= new Frame();
            Capture(sim, f, true);
            _head = (_head + 1) % Keep;
            _count = Mathf.Min(_count + 1, Keep);
        }

        static void Capture(MatchSim sim, Frame f, bool events)
        {
            int n = sim.players.Count;
            if (f.p == null || f.p.Length != n) f.p = new PFull[n];
            for (int i = 0; i < n; i++)
            {
                var q = sim.players[i];
                f.p[i] = new PFull
                {
                    pos = q.position, vel = q.vel, facing = q.facing, fallDir = q.fallDir,
                    slide = q.slide, dive = q.dive, stun = q.stun, down = q.down, downTotal = q.downTotal, hold = q.hold,
                    saveT = q.saveT, saveTotal = q.saveTotal, saveSide = q.saveSide, saveH = q.saveH, saveDist = q.saveDist, saveContact = q.saveContact,
                    windup = q.windup, windupTotal = q.windupTotal, kickAnim = q.kickAnim, tackleAnim = q.tackleAnim, skillT = q.skillT, offBalance = q.offBalance,
                    saveKind = q.saveKind, fallKind = q.fallKind, goalKick = q.goalKick, fallRoll = q.fallRoll, fallLight = q.fallLight, fallTrip = q.fallTrip,
                    sprinting = q.sprinting, shielding = q.shielding,
                };
            }
            f.ballPos = sim.ball.pos; f.ballVel = sim.ball.vel; f.ballZ = sim.ball.z; f.ballVz = sim.ball.vz; f.ballSpin = sim.ball.spin; f.owner = sim.ball.owner;
            f.ev.Clear();
            if (events)
                foreach (var e in sim.events)
                    if (e.type != "goal" && e.type != "whistle" && e.type != "card") f.ev.Add(e);
        }

        static void Apply(MatchSim sim, Frame f, bool events)
        {
            for (int i = 0; i < sim.players.Count && i < f.p.Length; i++)
            {
                var q = sim.players[i];
                var s = f.p[i];
                q.position = s.pos; q.vel = s.vel; q.facing = s.facing; q.fallDir = s.fallDir;
                q.slide = s.slide; q.dive = s.dive; q.stun = s.stun; q.down = s.down; q.downTotal = s.downTotal; q.hold = s.hold;
                q.saveT = s.saveT; q.saveTotal = s.saveTotal; q.saveSide = s.saveSide; q.saveH = s.saveH; q.saveDist = s.saveDist; q.saveContact = s.saveContact;
                q.windup = s.windup; q.windupTotal = s.windupTotal; q.kickAnim = s.kickAnim; q.tackleAnim = s.tackleAnim; q.skillT = s.skillT; q.offBalance = s.offBalance;
                q.saveKind = s.saveKind; q.fallKind = s.fallKind; q.goalKick = s.goalKick; q.fallRoll = s.fallRoll; q.fallLight = s.fallLight; q.fallTrip = s.fallTrip;
                q.sprinting = s.sprinting; q.shielding = s.shielding;
            }
            sim.ball.pos = f.ballPos; sim.ball.vel = f.ballVel; sim.ball.z = f.ballZ; sim.ball.vz = f.ballVz; sim.ball.spin = f.ballSpin; sim.ball.owner = f.owner;
            sim.events.Clear();
            if (events) sim.events.AddRange(f.ev);
        }

        Frame At(int i) => _ring[(_head - _count + i + Keep * 2) % Keep];

        // ---------------------------------------------------------------- reprodução
        MatchSim _sim;
        MatchView _view;
        Hud _hud;
        System.Action _onClose;
        readonly Frame _live = new Frame();
        bool _open, _playing = true;
        float _pos, _speed = 0.5f;
        int _applied = -1;
        GameObject _ui;
        RectTransform _bar, _fill, _knob;
        Text _playLabel, _camLabel, _focusLabel, _speedLabel, _timeLabel, _msg;
        int _camMode;   // 0 TV, 1 livre, 2 atrás do gol, 3 aérea
        int _focus = -1; // -1 bola, senão id do atleta
        float _yaw = -30f, _pitch = 18f, _dist = 14f, _fov = 40f;
        Vector3 _pivot;
        bool _dragBar;
        static readonly string[] CamNames = { "TV", "Livre", "Atrás do gol", "Aérea" };
        static readonly float[] Speeds = { 0.25f, 0.5f, 1f };

        public bool Open => _open;

        public void Begin(MatchSim sim, MatchView view, Hud hud, System.Action onClose)
        {
            if (_count < 2) { onClose?.Invoke(); return; }
            _sim = sim; _view = view; _hud = hud; _onClose = onClose;
            Capture(sim, _live, false);
            _open = true;
            _playing = true;
            _pos = 0f;
            _applied = -1;
            _view.Freeze(false);
            Time.timeScale = _speed;
            SeekTo(0f);
            _camMode = 0;
            _view.bcam.cinematic = false;
            _view.bcam.enabled = true;
            _view.bcam.SnapToBall();
            BuildUi();
        }

        void Close()
        {
            if (!_open) return;
            _open = false;
            Apply(_sim, _live, false);
            _sim.events.Clear();
            _view.SnapAll();
            _view.bcam.enabled = true;
            _view.bcam.showAll = false;
            _view.bcam.cinematic = false;
            _view.bcam.SnapToBall();
            if (_ui != null) Destroy(_ui);
            _onClose?.Invoke();
        }

        void SeekTo(float pos)
        {
            _pos = Mathf.Clamp(pos, 0f, _count - 1);
            int i = Mathf.FloorToInt(_pos);
            Apply(_sim, At(i), false);
            _applied = i;
            _view.OnTick();
            _view.SnapAll();
            _view.Alpha = 0f;
        }

        void Update()
        {
            if (!_open) return;
            float rdt = Time.unscaledDeltaTime;
            HandleKeys();
            if (_playing && !_dragBar)
            {
                Time.timeScale = _speed;
                _pos = Mathf.Min(_count - 1, _pos + Time.deltaTime * 60f);
                int to = Mathf.FloorToInt(_pos);
                for (int i = _applied + 1; i <= to; i++)
                {
                    Apply(_sim, At(i), true);
                    _view.OnTick();
                }
                _applied = Mathf.Max(_applied, to);
                _view.Alpha = _pos - Mathf.Floor(_pos);
                if (_pos >= _count - 1) { _playing = false; RefreshLabels(); }
            }
            else Time.timeScale = 0f;
            if (!_recording) UpdateBar();
            UpdateCamera(rdt);
        }

        // ---------------------------------------------------------------- câmera
        void UpdateCamera(float rdt)
        {
            var bc = _view.bcam;
            if (_camMode == 0) { bc.enabled = true; bc.cinematic = false; bc.showAll = false; return; }
            bc.enabled = false;
            bc.showAll = true;
            var cam = bc.cam;
            Vector3 target = FocusPoint();
            _pivot = Vector3.Lerp(_pivot == Vector3.zero ? target : _pivot, target, 1f - Mathf.Exp(-rdt * 6f));
            ReadOrbitInput(rdt);
            Vector3 pos;
            if (_camMode == 1)
            {
                var rot = Quaternion.Euler(_pitch, _yaw, 0f);
                pos = _pivot - rot * Vector3.forward * _dist;
                pos.y = Mathf.Max(pos.y, 0.4f);
            }
            else if (_camMode == 2)
            {
                float side = _sim.ball.pos.x >= 0f ? 1f : -1f;
                pos = new Vector3(side * (_sim.halfLen + 6f + _dist * 0.3f), 3f + _pitch * 0.12f, _pivot.z * 0.4f);
            }
            else pos = _pivot + new Vector3(0f, 18f + _dist * 1.4f, -6f - _dist * 0.4f);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(_pivot - pos);
            cam.fieldOfView = _fov;
            cam.nearClipPlane = Mathf.Clamp(Vector3.Distance(pos, _pivot) * 0.06f, 0.05f, 0.8f);
        }

        Vector3 FocusPoint()
        {
            if (_focus >= 0 && _focus < _view.athletes.Count && _view.athletes[_focus] != null)
            {
                var a = _view.athletes[_focus];
                return a.transform.position + Vector3.up * 1.1f;
            }
            return new Vector3(_sim.ball.pos.x, _sim.ball.z + 0.3f, _sim.ball.pos.y);
        }

        Vector2 _lastPointer;
        bool _orbiting;
        float _pinch0;

        void ReadOrbitInput(float rdt)
        {
            // toque: um dedo gira, dois dedos aproximam/afastam; mouse: arrastar gira, roda aproxima; controle: alavancas
            var ts = Touchscreen.current;
            int active = 0;
            Vector2 p0 = default, p1 = default;
            if (ts != null)
                foreach (var t in ts.touches)
                    if (t.press.isPressed) { if (active == 0) p0 = t.position.ReadValue(); else if (active == 1) p1 = t.position.ReadValue(); active++; }
            if (active >= 2)
            {
                float d = (p0 - p1).magnitude;
                if (_pinch0 > 0f) Zoom((_pinch0 - d) * 0.05f);
                _pinch0 = d;
                _orbiting = false;
            }
            else
            {
                _pinch0 = 0f;
                bool down = active == 1 || (Mouse.current != null && Mouse.current.leftButton.isPressed);
                var pt = active == 1 ? p0 : Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                if (down)
                {
                    if (!_orbiting) { _orbiting = !OverUi(pt); _lastPointer = pt; }
                    else
                    {
                        var dlt = pt - _lastPointer;
                        _yaw += dlt.x * 0.25f;
                        _pitch = Mathf.Clamp(_pitch - dlt.y * 0.2f, -5f, 80f);
                        _lastPointer = pt;
                    }
                }
                else _orbiting = false;
            }
            if (Mouse.current != null)
            {
                float sc = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(sc) > 0.01f) Zoom(-Mathf.Sign(sc) * 1.2f);
            }
            var g = Gamepad.current;
            if (g != null)
            {
                var r = g.rightStick.ReadValue();
                _yaw += r.x * 120f * rdt;
                _pitch = Mathf.Clamp(_pitch - r.y * 80f * rdt, -5f, 80f);
                Zoom((g.leftTrigger.ReadValue() - g.rightTrigger.ReadValue()) * 20f * rdt);
            }
        }

        void Zoom(float d)
        {
            // aproxima primeiro chegando perto; depois fecha a lente (detalhe)
            if (d < 0f && _dist <= 3f) _fov = Mathf.Clamp(_fov + d * 1.5f, 12f, 60f);
            else if (d > 0f && _fov < 40f) _fov = Mathf.Clamp(_fov + d * 1.5f, 12f, 40f);
            else _dist = Mathf.Clamp(_dist + d, 2.5f, 70f);
        }

        static bool OverUi(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screen };
            var hits = new List<RaycastResult>();
            es.RaycastAll(data, hits);
            return hits.Count > 0;
        }

        void HandleKeys()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            if (_recording)
            {
                bool tap = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
                if (tap || (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame)) _cancelRec = true;
                return;
            }
            if ((k != null && (k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame)) || (g != null && g.buttonEast.wasPressedThisFrame)) { Close(); return; }
            if ((k != null && k.spaceKey.wasPressedThisFrame) || (g != null && g.buttonSouth.wasPressedThisFrame)) TogglePlay();
            if ((k != null && k.cKey.wasPressedThisFrame) || (g != null && g.buttonNorth.wasPressedThisFrame)) NextCam();
            if ((k != null && k.fKey.wasPressedThisFrame) || (g != null && g.buttonWest.wasPressedThisFrame)) NextFocus();
            float step = 0f;
            if (k != null) { if (k.leftArrowKey.isPressed) step -= 1f; if (k.rightArrowKey.isPressed) step += 1f; }
            if (g != null) step += g.leftStick.ReadValue().x;
            if (Mathf.Abs(step) > 0.2f) { _playing = false; SeekTo(_pos + step * 60f * Time.unscaledDeltaTime); RefreshLabels(); }
        }

        void TogglePlay()
        {
            if (!_playing && _pos >= _count - 1) SeekTo(0f);
            _playing = !_playing;
            RefreshLabels();
        }

        void NextCam()
        {
            _camMode = (_camMode + 1) % CamNames.Length;
            _pivot = Vector3.zero;
            if (_camMode == 1) { var c = _view.bcam.cam.transform.forward; _yaw = Mathf.Atan2(c.x, c.z) * Mathf.Rad2Deg; }
            RefreshLabels();
        }

        void NextFocus()
        {
            // bola → quem está com ela → o mais perto da bola → bola
            int owner = _sim.ball.owner;
            if (_focus < 0) _focus = owner >= 0 ? owner : Nearest();
            else _focus = -1;
            _pivot = Vector3.zero;
            if (_camMode == 0) _camMode = 1;
            RefreshLabels();
        }

        int Nearest()
        {
            int best = -1; float bd = float.MaxValue;
            foreach (var q in _sim.players) { float d = q.position.Dist(_sim.ball.pos); if (d < bd) { bd = d; best = q.id; } }
            return best;
        }

        // ---------------------------------------------------------------- salvar em vídeo
        bool _recording, _cancelRec;
        const int VidW = 1024, VidH = 576, VidFps = 30;

        /// <summary>Gravar o replay em MP4 na galeria: celular e TV (Android).</summary>
        static bool CanSaveVideo =>
#if UNITY_ANDROID && !UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>Toca o replay do começo ao fim (na velocidade e câmera escolhidas) com passo de tempo fixo
        /// e grava cada quadro da câmera (sem os botões) num vídeo MP4 salvo em Filmes/Craquez.</summary>
        IEnumerator SaveVideo()
        {
            if (_recording) yield break;
#if UNITY_ANDROID && !UNITY_EDITOR
            var cam = _view.bcam.cam;
            var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var act = player.GetStatic<AndroidJavaObject>("currentActivity");
            var vid = new AndroidJavaClass("com.hellisonvidal.craquez.CraquezVideo");
            string err = vid.CallStatic<string>("start", act, VidW, VidH, VidFps);
            if (!string.IsNullOrEmpty(err)) { Say("Não deu para gravar neste aparelho (" + err + ")."); yield break; }
            _recording = true;
            _cancelRec = false;
            var rtex = new RenderTexture(VidW, VidH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            var tex = new Texture2D(VidW, VidH, TextureFormat.RGBA32, false);
            var buf = new byte[VidW * VidH * 4];
            // na tela: o que está sendo gravado (sem os controles) e o andamento
            var dockUi = _ui;
            var shade = UiKit.Image(_hud.Overlay, Color.black, null, "gravando");
            shade.rectTransform.Place(0, 0, 1, 1);
            var view = new GameObject("vídeo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            view.transform.SetParent(shade.transform, false);
            view.GetComponent<RawImage>().texture = rtex;
            var fit = view.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)VidW / VidH;
            var rec = UiKit.Label(shade.transform, "", 24, Theme.BodyBold, Color.white, TextAnchor.MiddleCenter);
            rec.rectTransform.Place(0, 0, 1, 0, 0, 30, 0, -80);
            dockUi.SetActive(false);
            cam.targetTexture = rtex;
            SeekTo(0f);
            _playing = true;
            Time.captureDeltaTime = 1f / VidFps;
            long pts = 0;
            int n = 0;
            string fail = null;
            while (_open && !_cancelRec)
            {
                yield return new WaitForEndOfFrame();
                var prev = RenderTexture.active;
                RenderTexture.active = rtex;
                tex.ReadPixels(new Rect(0, 0, VidW, VidH), 0, 0, false);
                RenderTexture.active = prev;
                tex.GetRawTextureData<byte>().CopyTo(buf);
                pts = (long)(n * 1000000L / VidFps);
                string e = vid.CallStatic<string>("frame", buf, pts);
                if (!string.IsNullOrEmpty(e)) { fail = e; break; }
                n++;
                float k = _count > 1 ? _pos / (_count - 1) : 1f;
                rec.text = "● Gravando o replay…  " + Mathf.RoundToInt(k * 100f) + "%   (toque para cancelar)";
                if (!_playing || _pos >= _count - 1) break;
            }
            Time.captureDeltaTime = 0f;
            cam.targetTexture = null;
            _recording = false;
            Destroy(shade.gameObject);
            if (dockUi != null) dockUi.SetActive(true);
            string res;
            if (fail != null || _cancelRec || n < 2) { vid.CallStatic("cancel"); res = fail != null ? "erro:" + fail : "cancelado"; }
            else res = vid.CallStatic<string>("finish", act, "Craquez-replay-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss"), pts);
            rtex.Release(); Destroy(rtex); Destroy(tex);
            act.Dispose(); player.Dispose(); vid.Dispose();
            _playing = false;
            RefreshLabels();
            if (res.StartsWith("ok:")) Say("Vídeo salvo na galeria: " + res.Substring(3));
            else if (res == "cancelado") Say("Gravação cancelada.");
            else Say("Não deu para salvar o vídeo (" + res.Substring(res.IndexOf(':') + 1) + ").");
#else
            yield break;
#endif
        }

        void Say(string m) { if (_msg != null) _msg.text = m; }

        // ---------------------------------------------------------------- interface
        void BuildUi()
        {
            if (_ui != null) Destroy(_ui);
            var root = _hud.Overlay;
            var go = new GameObject("Replay", typeof(RectTransform));
            go.transform.SetParent(root, false);
            _ui = go;
            var rt = (RectTransform)go.transform;
            rt.Place(0, 0, 1, 1);
            // selo "REPLAY" no alto
            var tag = UiKit.Image(rt, new Color(0.85f, 0.1f, 0.12f, 0.92f), Theme.RoundSmall, "selo").rectTransform.At(0.5f, 1f, 220, 54, 0, -128);   // (abaixo do placar)
            UiKit.Label(tag, "REPLAY", 30, Theme.HeadItalic, Color.white, TextAnchor.MiddleCenter).rectTransform.Place(0, 0, 1, 1);
            // sair também no alto (fácil de achar no celular)
            var x = UiKit.Button(rt, "✕  Sair do replay", Close, 22, new Color(0.7f, 0.1f, 0.14f, 0.95f));
            ((RectTransform)x.transform).At(1f, 1f, 230, 56, -170, -44);
            // barra inferior compacta: linha do tempo + duas fileiras de botões pequenos e centralizados, longe
            // das bordas (no celular os botões do Android — voltar, início — ficam por cima dos cantos de baixo)
            var dock = UiKit.Image(rt, new Color(0.02f, 0.04f, 0.1f, 0.78f), Theme.RoundSmall, "controles").rectTransform.Place(0, 0, 1, 0, 200, 70, 200, -230);   // (t negativo = topo acima da borda de baixo)
            _bar = UiKit.Image(dock, new Color(1f, 1f, 1f, 0.2f), Theme.RoundSmall, "linha do tempo").rectTransform.Place(0, 1, 1, 1, 130, -34, 40, 20);
            _fill = UiKit.Image(_bar, new Color(0.95f, 0.2f, 0.25f, 0.95f), Theme.RoundSmall, "andamento").rectTransform.Place(0, 0, 0, 1);
            _knob = UiKit.Image(_bar, Color.white, Theme.Circle, "cursor").rectTransform.At(0, 0.5f, 28, 28);
            _timeLabel = UiKit.Label(dock, "", 20, Theme.Body, Theme.TextDim, TextAnchor.MiddleLeft);
            _timeLabel.rectTransform.Place(0, 1, 0, 1, 18, -40, -120, 12);
            var row1 = UiKit.Rect(dock, "reprodução").Place(0, 0, 1, 0, 12, 68, 12, -118);
            var row2 = UiKit.Rect(dock, "câmera").Place(0, 0, 1, 0, 12, 10, 12, -60);
            foreach (var row in new[] { row1, row2 })
            {
                var h = UiKit.HList(row, 10);
                h.childForceExpandWidth = false;
            }
            Button B(RectTransform row, string t, System.Action a, out Text label, Color? c = null, float wdt = 150f)
            {
                var b = UiKit.Button(row, t, a, 19, c ?? new Color(0.1f, 0.14f, 0.24f, 0.95f));
                UiKit.Size(b, 50, 50, 0f, wdt);
                label = b.GetComponentInChildren<Text>();
                return b;
            }
            B(row1, "|◀ Início", () => { SeekTo(0f); RefreshLabels(); }, out _, null, 130);
            B(row1, "◀◀ 5 s", () => { _playing = false; SeekTo(_pos - 300f); RefreshLabels(); }, out _, null, 120);
            B(row1, "", TogglePlay, out _playLabel, new Color(0.12f, 0.45f, 0.25f, 0.95f), 170);
            B(row1, "5 s ▶▶", () => { _playing = false; SeekTo(_pos + 300f); RefreshLabels(); }, out _, null, 120);
            B(row1, "", () => { int i = System.Array.IndexOf(Speeds, _speed); _speed = Speeds[(i + 1) % Speeds.Length]; RefreshLabels(); }, out _speedLabel, null, 170);
            if (CanSaveVideo) B(row1, "⬇ Salvar vídeo", () => StartCoroutine(SaveVideo()), out _, new Color(0.15f, 0.35f, 0.62f, 0.95f), 190);
            B(row2, "", NextCam, out _camLabel, null, 230);
            B(row2, "", NextFocus, out _focusLabel, null, 200);
            B(row2, "Zoom −", () => { if (_camMode == 0) { _camMode = 1; _pivot = Vector3.zero; } Zoom(4f); RefreshLabels(); }, out _, null, 110);
            B(row2, "Zoom +", () => { if (_camMode == 0) { _camMode = 1; _pivot = Vector3.zero; } Zoom(-4f); RefreshLabels(); }, out _, null, 110);
            _msg = UiKit.Label(rt, "", 22, Theme.BodyBold, Color.white, TextAnchor.MiddleCenter);
            _msg.rectTransform.Place(0, 0, 1, 0, 200, 274, 200, -308);
            _msg.raycastTarget = false;
            var hint = UiKit.Label(rt, "arraste para girar a câmera  •  pinça / roda do mouse: zoom  •  espaço: pausar  •  ← →: avançar/voltar", 18, Theme.Body, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
            hint.rectTransform.Place(0, 0, 1, 0, 200, 238, 200, -268);
            hint.raycastTarget = false;
            _hud.ShowPauseButton(false);
            RefreshLabels();
        }

        void RefreshLabels()
        {
            if (_playLabel == null) return;
            _playLabel.text = _playing ? "❚❚  Pausar" : "▶  Reproduzir";
            _speedLabel.text = "Velocidade " + (_speed < 0.3f ? "¼" : _speed < 0.6f ? "½" : "1") + "×";
            _camLabel.text = "Câmera: " + CamNames[_camMode];
            _focusLabel.text = _focus < 0 ? "Foco: bola" : "Foco: " + Short(_sim.players[_focus].name);
        }

        static string Short(string n) { int i = n.LastIndexOf(' '); return i > 0 ? n.Substring(i + 1) : n; }

        void UpdateBar()
        {
            if (_bar == null) return;
            // arrastar na linha do tempo
            Vector2 pt = default;
            bool down = false;
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed) { down = true; pt = ts.primaryTouch.position.ReadValue(); }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed) { down = true; pt = Mouse.current.position.ReadValue(); }
            var canvasCam = _hud.canvas != null && _hud.canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _hud.canvas.worldCamera : null;
            if (down && (_dragBar || RectTransformUtility.RectangleContainsScreenPoint(_bar, pt, canvasCam)))
            {
                if (!_dragBar) { _dragBar = true; _playing = false; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_bar, pt, canvasCam, out var local);
                float u = Mathf.Clamp01((local.x - _bar.rect.xMin) / Mathf.Max(1f, _bar.rect.width));
                SeekTo(u * (_count - 1));
            }
            else if (!down && _dragBar) { _dragBar = false; RefreshLabels(); }
            float k = _count > 1 ? _pos / (_count - 1) : 0f;
            _fill.anchorMax = new Vector2(k, 1f);
            _knob.anchorMin = _knob.anchorMax = new Vector2(k, 0.5f);
            _knob.anchoredPosition = Vector2.zero;
            float back = (_count - 1 - _pos) / 60f;
            _timeLabel.text = "-" + back.ToString("0.0") + " s";
        }
    }
}
