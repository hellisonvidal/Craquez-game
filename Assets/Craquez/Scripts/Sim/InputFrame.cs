using UnityEngine;

namespace Craquez.Sim
{
    /// <summary>
    /// Um "quadro" de entrada de um jogador em um tick da simulação. Só ESTADO
    /// (botões segurados + analógicos); as bordas (apertou/soltou, "flick" do
    /// analógico direito) são calculadas dentro da simulação. 6 bytes na rede.
    /// </summary>
    public struct InputFrame
    {
        public const int Pass = 1;      // A / Cruz       - passe rasteiro | defesa: bote
        public const int Shoot = 2;     // B / Círculo    - chute (segurar = força) | defesa: carrinho
        public const int Lob = 4;       // X / Quadrado   - passe alto / cruzamento
        public const int Through = 8;   // Y / Triângulo  - enfiada | defesa: chamar 2º marcador
        public const int Sprint = 16;   // RT / R2        - correr
        public const int Switch = 32;   // LB / L1        - trocar jogador
        public const int Pause = 64;    // Start / Options
        public const int Shield = 128;  // LT / L2        - proteger | defesa: contenção
        public const int Finesse = 256; // RB / R1        - chute colocado
        public const int Size = 6;

        public Vector2 move;
        public Vector2 rstick;
        public int buttons;

        public bool Has(int b) => (buttons & b) != 0;

        public void Write(byte[] b, int o = 0)
        {
            b[o] = (byte)(sbyte)Mathf.Clamp(Mathf.RoundToInt(move.x * 127f), -127, 127);
            b[o + 1] = (byte)(sbyte)Mathf.Clamp(Mathf.RoundToInt(move.y * 127f), -127, 127);
            b[o + 2] = (byte)(buttons & 0xFF);
            b[o + 3] = (byte)((buttons >> 8) & 0xFF);
            b[o + 4] = (byte)(sbyte)Mathf.Clamp(Mathf.RoundToInt(rstick.x * 127f), -127, 127);
            b[o + 5] = (byte)(sbyte)Mathf.Clamp(Mathf.RoundToInt(rstick.y * 127f), -127, 127);
        }

        public static InputFrame Read(byte[] b, int o = 0)
        {
            return new InputFrame
            {
                move = new Vector2((sbyte)b[o] / 127f, (sbyte)b[o + 1] / 127f),
                buttons = b[o + 2] | (b[o + 3] << 8),
                rstick = new Vector2((sbyte)b[o + 4] / 127f, (sbyte)b[o + 5] / 127f),
            };
        }

        /// <summary>Quantiza como na rede, para que local e remoto simulem o mesmo valor.</summary>
        public InputFrame Quantized()
        {
            var b = new byte[Size];
            Write(b);
            return Read(b);
        }
    }
}
