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
        Text _playLabel, _camLabel, _focusLabel, _speedLabel, _timeLabel;
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
            UpdateBar();
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
            var tag = UiKit.Image(rt, new Color(0.85f, 0.1f, 0.12f, 0.92f), Theme.RoundSmall, "selo").rectTransform.At(0.5f, 1f, 220, 54, 0, -40);
            UiKit.Label(tag, "REPLAY", 30, Theme.HeadItalic, Color.white, TextAnchor.MiddleCenter).rectTransform.Place(0, 0, 1, 1);
            // barra inferior: linha do tempo + botões
            var dock = UiKit.Image(rt, new Color(0.02f, 0.04f, 0.1f, 0.78f), null, "controles").rectTransform.Place(0, 0, 1, 0, 0, 0, 0, 170);
            _bar = UiKit.Image(dock, new Color(1f, 1f, 1f, 0.2f), Theme.RoundSmall, "linha do tempo").rectTransform.Place(0, 1, 1, 1, 150, -40, 150, -18);
            _fill = UiKit.Image(_bar, new Color(0.95f, 0.2f, 0.25f, 0.95f), Theme.RoundSmall, "andamento").rectTransform.Place(0, 0, 0, 1);
            _knob = UiKit.Image(_bar, Color.white, Theme.Circle, "cursor").rectTransform.At(0, 0.5f, 34, 34);
            _timeLabel = UiKit.Label(dock, "", 22, Theme.Body, Theme.TextDim, TextAnchor.MiddleLeft);
            _timeLabel.rectTransform.Place(0, 1, 0, 1, 20, -48, -140, -10);
            var row = UiKit.Rect(dock, "botões").Place(0, 0, 1, 1, 20, 14, 20, 64);
            UiKit.HList(row, 14);
            Button B(string t, System.Action a, out Text label)
            {
                var b = UiKit.Button(row, t, a, 26, new Color(0.1f, 0.14f, 0.24f, 0.95f));
                UiKit.Size(b, 72, 72, 1f);
                label = b.GetComponentInChildren<Text>();
                return b;
            }
            B("", TogglePlay, out _playLabel);
            B("", () => { int i = System.Array.IndexOf(Speeds, _speed); _speed = Speeds[(i + 1) % Speeds.Length]; RefreshLabels(); }, out _speedLabel);
            B("", NextCam, out _camLabel);
            B("", NextFocus, out _focusLabel);
            B("−  Zoom", () => Zoom(4f), out _);
            B("+  Zoom", () => Zoom(-4f), out _);
            B("Sair", Close, out _);
            var hint = UiKit.Label(rt, "arraste para girar a câmera  •  pinça / roda do mouse: zoom", 22, Theme.Body, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);
            hint.rectTransform.Place(0, 0, 1, 0, 0, 176, 0, 210);
            hint.raycastTarget = false;
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
