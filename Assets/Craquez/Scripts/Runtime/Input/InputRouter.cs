using Craquez.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
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
            if (Application.isMobilePlatform || (Touchscreen.current != null && Keyboard.current == null)) return DeviceKind.Touch;
            return DeviceKind.Keyboard1;
        }

        public static DeviceKind DefaultP2(DeviceKind p1)
        {
            if (p1 == DeviceKind.Gamepad1 && Gamepad.all.Count > 1) return DeviceKind.Gamepad2;
            if (p1 != DeviceKind.Gamepad1 && Gamepad.all.Count > 0) return DeviceKind.Gamepad1;
            return p1 == DeviceKind.Keyboard1 ? DeviceKind.Keyboard2 : DeviceKind.Keyboard1;
        }

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
                    if (g.buttonSouth.isPressed) f.buttons |= InputFrame.Pass;
                    if (g.buttonEast.isPressed) f.buttons |= InputFrame.Shoot;
                    if (g.buttonWest.isPressed) f.buttons |= InputFrame.Lob;
                    if (g.buttonNorth.isPressed) f.buttons |= InputFrame.Through;
                    if (g.rightTrigger.ReadValue() > 0.35f) f.buttons |= InputFrame.Sprint;
                    if (g.leftShoulder.isPressed) f.buttons |= InputFrame.Switch;
                    if (g.leftTrigger.ReadValue() > 0.35f) f.buttons |= InputFrame.Shield;
                    if (g.rightShoulder.isPressed) f.buttons |= InputFrame.Finesse;
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
            if (IsPlayStation(g))
                return "Cruz passe  •  Bola chute  •  Quadrado alto  •  Triângulo enfiada  •  R2 correr\nL1 trocar  •  L2 proteger/conter  •  R1 colocado  •  R3 (analógico direito): dribles";
            return "A passe  •  B chute  •  X alto/cruzamento  •  Y enfiada  •  RT correr\nLB trocar  •  LT proteger/conter  •  RB colocado  •  Analógico direito: dribles";
        }

        /// <summary>Opções da cobrança de falta (o chute direto só aparece perto do gol).</summary>
        public static string SetPieceHelp(DeviceKind d, bool shot)
        {
            string pass, cross, kick, curl;
            if (d == DeviceKind.Touch) { pass = "PASSE"; cross = "ALTO"; kick = "CHUTE"; curl = "COLOCADO"; }
            else if (d == DeviceKind.Keyboard1) { pass = "J"; cross = "L"; kick = "K"; curl = "U + K"; }
            else if (d == DeviceKind.Keyboard2) { pass = "Num1"; cross = "Num3"; kick = "Num2"; curl = "Num9 + Num2"; }
            else
            {
                bool ps = IsPlayStation(Pad(d == DeviceKind.Gamepad1 ? 0 : 1));
                pass = ps ? "Cruz" : "A"; cross = ps ? "Quadrado" : "X"; kick = ps ? "Bola" : "B"; curl = ps ? "R1 + Bola" : "RB + B";
            }
            string s = "FALTA   •   mire com o analógico   •   " + pass + " passe   •   " + cross + " cruzar na área";
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
