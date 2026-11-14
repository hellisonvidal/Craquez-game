using Craquez.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;

namespace Craquez
{
    public enum DeviceKind { Touch, Keyboard1, Keyboard2, Gamepad1, Gamepad2 }

    /// <summary>Estado dos controles de toque (preenchido por TouchControls).</summary>
    public static class TouchState
    {
        public static Vector2 move;
        public static Vector2 rstick;
        public static int buttons;
        public static bool active;
    }

    /// <summary>
    /// Lê teclado, controles (Xbox/PlayStation/genéricos) e toque e entrega um
    /// InputFrame por jogador. Detecta a plataforma e o tipo de controle para
    /// mostrar os botões certos na ajuda.
    /// </summary>
    public static class InputRouter
    {
        public static DeviceKind DefaultP1()
        {
            if (Gamepad.all.Count > 0) return DeviceKind.Gamepad1;
            if (Platform.IsTv) return DeviceKind.Keyboard1;   // TV sem controle: teclado/controle remoto (sem toque)
            if (Application.isMobilePlatform || (Touchscreen.current != null && Keyboard.current == null)) return DeviceKind.Touch;
            return DeviceKind.Keyboard1;
        }

        public static DeviceKind DefaultP2(DeviceKind p1)
        {
            if (p1 == DeviceKind.Gamepad1 && Gamepad.all.Count > 1) return DeviceKind.Gamepad2;
            if (p1 != DeviceKind.Gamepad1 && Gamepad.all.Count > 0) return DeviceKind.Gamepad1;
            return p1 == DeviceKind.Keyboard1 ? DeviceKind.Keyboard2 : DeviceKind.Keyboard1;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// No Android o layout padrão da Unity lê o gatilho só do eixo "GAS": muitos controles (e caixas de
        /// TV) mandam o gatilho no eixo RTRIGGER ou como botão R2, e aí o CORRER (R2/RT) nunca chegava.
        /// Acrescenta esses dois caminhos aos controles do Android.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AndroidTriggers()
        {
            try
            {
                // estado: 7 palavras de botões (220 bits) e depois os eixos em float
                InputSystem.RegisterLayoutOverride(@"{
                    ""name"": ""CraquezAndroidTriggers"",
                    ""extendMultiple"": [ ""AndroidGamepad"", ""AndroidGamepadWithDpadAxes"", ""AndroidGamepadWithDpadButtons"", ""DualShock4GamepadAndroid"", ""XboxOneGamepadAndroid"" ],
                    ""controls"": [
                        { ""name"": ""craquezLT"", ""layout"": ""Axis"", ""offset"": 96, ""format"": ""FLT"", ""parameters"": ""clamp=1,clampMin=0,clampMax=1"" },
                        { ""name"": ""craquezRT"", ""layout"": ""Axis"", ""offset"": 100, ""format"": ""FLT"", ""parameters"": ""clamp=1,clampMin=0,clampMax=1"" },
                        { ""name"": ""craquezL2"", ""layout"": ""Button"", ""offset"": 12, ""bit"": 8, ""format"": ""BIT"" },
                        { ""name"": ""craquezR2"", ""layout"": ""Button"", ""offset"": 12, ""bit"": 9, ""format"": ""BIT"" }
                    ]
                }");
            }
            catch (System.Exception e) { Debug.LogWarning("[controle] gatilhos extras: " + e.Message); }
        }
#endif

        /// <summary>Gatilho apertado: eixo padrão, eixo alternativo do Android ou botão digital L2/R2.</summary>
        static bool Trigger(Gamepad g, bool right)
        {
            var t = right ? g.rightTrigger : g.leftTrigger;
            if (t.ReadValue() > 0.35f) return true;
            if (g.TryGetChildControl(right ? "craquezRT" : "craquezLT") is AxisControl a && a.ReadValue() > 0.35f) return true;
            if (g.TryGetChildControl(right ? "craquezR2" : "craquezL2") is ButtonControl b && b.isPressed) return true;
            return false;
        }

        static readonly int[] ActionBits = { InputFrame.Pass, InputFrame.Shoot, InputFrame.Lob, InputFrame.Through };
        public static readonly string[] ActionNames = { "Passe", "Chute", "Alto / cruzamento", "Enfiada" };
        static int[] _map; static float _mapT = -1f;
        /// <summary>Lê o layout salvo (no máximo uma vez por segundo: muda pelo menu de opções).</summary>
        static int[] PadMapCached()
        {
            if (_map == null || Time.unscaledTime - _mapT > 1f) { _map = Settings.PadMap; _mapT = Time.unscaledTime; }
            return _map;
        }
        public static void InvalidateMap() => _map = null;

        static UnityEngine.InputSystem.Controls.ButtonControl Face(Gamepad g, int i) =>
            i == 0 ? g.buttonSouth : i == 1 ? g.buttonEast : i == 2 ? g.buttonWest : g.buttonNorth;

        /// <summary>Nome do botão da face (Xbox / PlayStation).</summary>
        public static string FaceName(int i, bool ps) =>
            ps ? (i == 0 ? "Cruz" : i == 1 ? "Bola" : i == 2 ? "Quadrado" : "Triângulo") : (i == 0 ? "A" : i == 1 ? "B" : i == 2 ? "X" : "Y");

        static Gamepad Pad(int i) => Gamepad.all.Count > i ? Gamepad.all[i] : null;

        public static bool IsPlayStation(Gamepad g) => g is DualShockGamepad || (g != null && g.description.manufacturer != null && g.description.manufacturer.Contains("Sony"));

        public static InputFrame Read(DeviceKind dev)
        {
            var f = new InputFrame();
            switch (dev)
            {
                case DeviceKind.Touch:
                    f.move = TouchState.move;
                    f.rstick = TouchState.rstick;
                    f.buttons = TouchState.buttons;
                    break;
                case DeviceKind.Gamepad1:
                case DeviceKind.Gamepad2:
                {
                    var g = Pad(dev == DeviceKind.Gamepad1 ? 0 : 1);
                    if (g == null) break;
                    f.move = Dead(g.leftStick.ReadValue()) + Dpad(g);
                    f.rstick = Dead(g.rightStick.ReadValue());
                    // layout escolhido em Configurações → Controles
                    var map = PadMapCached();
                    for (int a = 0; a < 4; a++)
                        if (Face(g, map[a]).isPressed) f.buttons |= ActionBits[a];
                    bool rt = Trigger(g, true), rb = g.rightShoulder.isPressed;
                    bool swap = Settings.SprintOnShoulder;
                    if (swap ? rb : rt) f.buttons |= InputFrame.Sprint;
                    if (swap ? rt : rb) f.buttons |= InputFrame.Finesse;
                    if (g.leftShoulder.isPressed) f.buttons |= InputFrame.Switch;
                    if (Trigger(g, false)) f.buttons |= InputFrame.Shield;
                    if (g.startButton.isPressed) f.buttons |= InputFrame.Pause;
                    break;
                }
                case DeviceKind.Keyboard1:
                {
                    var k = Keyboard.current;
                    if (k == null) break;
                    f.move = Axis(k.dKey.isPressed, k.aKey.isPressed, k.wKey.isPressed, k.sKey.isPressed);
                    f.rstick = Axis(k.rightArrowKey.isPressed, k.leftArrowKey.isPressed, k.upArrowKey.isPressed, k.downArrowKey.isPressed);
                    if (k.jKey.isPressed) f.buttons |= InputFrame.Pass;
                    if (k.kKey.isPressed) f.buttons |= InputFrame.Shoot;
                    if (k.lKey.isPressed) f.buttons |= InputFrame.Lob;
                    if (k.iKey.isPressed) f.buttons |= InputFrame.Through;
                    if (k.leftShiftKey.isPressed) f.buttons |= InputFrame.Sprint;
                    if (k.qKey.isPressed) f.buttons |= InputFrame.Switch;
                    if (k.eKey.isPressed) f.buttons |= InputFrame.Shield;
                    if (k.uKey.isPressed) f.buttons |= InputFrame.Finesse;
                    if (k.escapeKey.isPressed || k.pKey.isPressed) f.buttons |= InputFrame.Pause;
                    break;
                }
                case DeviceKind.Keyboard2:
                {
                    var k = Keyboard.current;
                    if (k == null) break;
                    f.move = Axis(k.rightArrowKey.isPressed, k.leftArrowKey.isPressed, k.upArrowKey.isPressed, k.downArrowKey.isPressed);
                    f.rstick = Axis(k.pageDownKey.isPressed, k.deleteKey.isPressed, k.homeKey.isPressed, k.endKey.isPressed);
                    if (k.numpad1Key.isPressed) f.buttons |= InputFrame.Pass;
                    if (k.numpad2Key.isPressed) f.buttons |= InputFrame.Shoot;
                    if (k.numpad3Key.isPressed) f.buttons |= InputFrame.Lob;
                    if (k.numpad5Key.isPressed) f.buttons |= InputFrame.Through;
                    if (k.numpad0Key.isPressed || k.rightShiftKey.isPressed) f.buttons |= InputFrame.Sprint;
                    if (k.numpad4Key.isPressed) f.buttons |= InputFrame.Switch;
                    if (k.numpad6Key.isPressed) f.buttons |= InputFrame.Shield;
                    if (k.numpad9Key.isPressed) f.buttons |= InputFrame.Finesse;
                    if (k.numpadEnterKey.isPressed) f.buttons |= InputFrame.Pause;
                    break;
                }
            }
            f.move = f.move.Limit(1f);
            return f;
        }

        static Vector2 Dead(Vector2 v)
        {
            float m = v.magnitude;
            if (m < 0.18f) return Vector2.zero;
            return v / m * Mathf.Clamp01((m - 0.18f) / 0.78f);
        }

        static Vector2 Dpad(Gamepad g) => g.dpad.ReadValue();

        static Vector2 Axis(bool r, bool l, bool u, bool d)
        {
            var v = new Vector2((r ? 1 : 0) - (l ? 1 : 0), (u ? 1 : 0) - (d ? 1 : 0));
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        /// <summary>Algum botão de "confirmar/pular" em qualquer dispositivo (telas de abertura).</summary>
        public static bool AnyPressedThisFrame()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            foreach (var g in Gamepad.all)
                if (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
            return false;
        }

        public static bool BackPressedThisFrame()
        {
            if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame)) return true;
            foreach (var g in Gamepad.all)
                if (g.buttonEast.wasPressedThisFrame) return true;
            return false;
        }

        /// <summary>Aparelhos de entrada conectados (diagnóstico na aba Controles).</summary>
        public static string DeviceReport()
        {
            var sb = new System.Text.StringBuilder("Aparelhos detectados:");
            int n = 0;
            foreach (var d in InputSystem.devices)
            {
                if (d is Mouse || d is Touchscreen || d is Sensor) continue;
                string kind = d is Gamepad ? "controle" : d is Joystick ? "joystick (não reconhecido como controle)" : d is Keyboard ? "teclado/controle remoto" : d.layout;
                sb.Append("\n•  ").Append(string.IsNullOrEmpty(d.displayName) ? d.name : d.displayName).Append("  —  ").Append(kind);
                n++;
            }
            if (n == 0) sb.Append(" nenhum");
            return sb.ToString();
        }

        public static string DeviceName(DeviceKind d)
        {
            switch (d)
            {
                case DeviceKind.Touch: return "Toque";
                case DeviceKind.Keyboard1: return "Teclado (WASD)";
                case DeviceKind.Keyboard2: return "Teclado (setas)";
                case DeviceKind.Gamepad1: return Pad(0) == null ? "Controle 1 (desconectado)" : (IsPlayStation(Pad(0)) ? "PlayStation 1" : "Xbox/Controle 1");
                default: return Pad(1) == null ? "Controle 2 (desconectado)" : (IsPlayStation(Pad(1)) ? "PlayStation 2" : "Xbox/Controle 2");
            }
        }

        /// <summary>Ajuda dos comandos para o dispositivo.</summary>
        public static string Help(DeviceKind d)
        {
            if (d == DeviceKind.Touch)
                return "Analógico: mover   •   PASSE / CHUTE (segure p/ força) / ALTO / ENFIADA\nCORRER  •  TROCAR  •  PROTEGER  •  COLOCADO  •  deslize no lado direito: dribles";
            if (d == DeviceKind.Keyboard1)
                return "WASD mover  •  J passe  •  K chute  •  L alto  •  I enfiada  •  Shift correr\nQ trocar  •  E proteger/conter  •  U colocado  •  Setas: dribles  •  Esc pausa";
            if (d == DeviceKind.Keyboard2)
                return "Setas mover  •  Num1 passe  •  Num2 chute  •  Num3 alto  •  Num5 enfiada  •  Num0 correr\nNum4 trocar  •  Num6 proteger  •  Num9 colocado  •  Home/End/Del/PgDn: dribles";
            var g = Pad(d == DeviceKind.Gamepad1 ? 0 : 1);
            bool ps = IsPlayStation(g);
            var m = Settings.PadMap;
            bool sh = Settings.SprintOnShoulder;
            string run = ps ? (sh ? "R1" : "R2") : (sh ? "RB" : "RT"), fin = ps ? (sh ? "R2" : "R1") : (sh ? "RT" : "RB");
            return FaceName(m[0], ps) + " passe  •  " + FaceName(m[1], ps) + " chute  •  " + FaceName(m[2], ps) + " alto/cruzamento  •  " + FaceName(m[3], ps) + " enfiada  •  " + run + " correr\n"
                + (ps ? "L1" : "LB") + " trocar  •  " + (ps ? "L2" : "LT") + " proteger/conter  •  " + fin + " colocado  •  Analógico direito: dribles";
        }

        /// <summary>Nomes dos comandos no controle (tutorial): mover, passe, chute, alto, correr, drible.</summary>
        public static (string move, string pass, string shoot, string lob, string sprint, string skill) Names(DeviceKind d)
        {
            if (d == DeviceKind.Touch) return ("o analógico (lado esquerdo da tela)", "PASSE", "CHUTE", "ALTO", "CORRER", "deslize o dedo no lado direito da tela");
            if (d == DeviceKind.Keyboard1) return ("W A S D", "J", "K", "L", "Shift", "as setas");
            if (d == DeviceKind.Keyboard2) return ("as setas", "Num1", "Num2", "Num3", "Num0", "Home/End/Del/PgDn");
            bool ps = IsPlayStation(Pad(d == DeviceKind.Gamepad1 ? 0 : 1));
            var m = Settings.PadMap;
            bool sh = Settings.SprintOnShoulder;
            return ("a alavanca esquerda", FaceName(m[0], ps), FaceName(m[1], ps), FaceName(m[2], ps), ps ? (sh ? "R1" : "R2") : (sh ? "RB" : "RT"), "a alavanca direita");
        }

        /// <summary>Opções da cobrança de falta (o chute direto só aparece perto do gol).</summary>
        public static string SetPieceHelp(DeviceKind d, bool shot, string title = "FALTA")
        {
            string pass, cross, kick, curl;
            if (d == DeviceKind.Touch) { pass = "PASSE"; cross = "ALTO"; kick = "CHUTE"; curl = "COLOCADO"; }
            else if (d == DeviceKind.Keyboard1) { pass = "J"; cross = "L"; kick = "K"; curl = "U + K"; }
            else if (d == DeviceKind.Keyboard2) { pass = "Num1"; cross = "Num3"; kick = "Num2"; curl = "Num9 + Num2"; }
            else
            {
                bool ps = IsPlayStation(Pad(d == DeviceKind.Gamepad1 ? 0 : 1));
                var m = Settings.PadMap;
                bool sh = Settings.SprintOnShoulder;
                pass = FaceName(m[0], ps); cross = FaceName(m[2], ps); kick = FaceName(m[1], ps);
                curl = (ps ? (sh ? "R2" : "R1") : (sh ? "RT" : "RB")) + " + " + kick;
            }
            string s = title + "   •   mire com o analógico   •   " + pass + " passe   •   " + cross + " cruzar na área";
            if (shot) s += "   •   " + kick + " chute direto (segure p/ força)   •   " + curl + " colocado";
            return s;
        }

        public static void Rumble(DeviceKind d, float low, float high, float seconds, MonoBehaviour host)
        {
            if (!Settings.Vibration) return;
            var g = d == DeviceKind.Gamepad1 ? Pad(0) : d == DeviceKind.Gamepad2 ? Pad(1) : null;
            if (g == null)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (d == DeviceKind.Touch && high > 0.5f) Handheld.Vibrate();
#endif
                return;
            }
            g.SetMotorSpeeds(low, high);
            host.StartCoroutine(StopRumble(g, seconds));
        }

        static System.Collections.IEnumerator StopRumble(Gamepad g, float s)
        {
            yield return new WaitForSecondsRealtime(s);
            g.SetMotorSpeeds(0, 0);
        }
    }
}
