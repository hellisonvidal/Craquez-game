using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>Quem controla cada lado.</summary>
    public enum Side { Cpu, Player1, Player2 }

    /// <summary>Configuração da próxima partida (montada pelos menus).</summary>
    public class MatchConfig
    {
        public string mode = GameModes.Field;
        public int teamA, teamB = 1;
        public int kitA, kitB = -1;
        public Tactics tacticsA = new Tactics(), tacticsB = new Tactics();
        public Side sideA = Side.Player1, sideB = Side.Cpu;
        public float difficulty = 0.6f;
        public int minutes = 5;          // duração total (dois tempos)
        public string weather = "sol";   // sol, nublado, chuva, tempestade, neve, aleatorio
        public string timeOfDay = "noite";  // dia, tarde, noite
        public bool dynamicWeather = true;
        public int streetPlayers = 3;    // rua: 3x3, 4x4 ou 5x5
        public int stadium;              // campo: 0 = estádio do mandante; 1.. = modelos
        public bool swapSides;           // trocar os campos: o mandante começa atacando para a esquerda
        public int venue;                // futsal/rua: qual quadra/rua
        public uint seed = 1;
        public bool ceremony = true;
        public bool national;            // seleções em vez de clubes
        public int ball;                 // BallCatalog
        public int boots = -1;           // -1 = cada atleta com a sua; senão modelo único

        public float SecondsPerHalf => minutes * 60f * 0.5f;
    }

    /// <summary>Preferências persistentes.</summary>
    public static class Settings
    {
        public static int Quality
        {
            get => PlayerPrefs.GetInt("quality", DefaultQuality());
            set { PlayerPrefs.SetInt("quality", value); PlayerPrefs.Save(); }
        }

        public static float Volume
        {
            get => PlayerPrefs.GetFloat("volume", 0.9f);
            set { PlayerPrefs.SetFloat("volume", value); PlayerPrefs.Save(); AudioListener.volume = value; }
        }

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt("vibration", 1) == 1;
            set { PlayerPrefs.SetInt("vibration", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int Camera
        {
            get => PlayerPrefs.GetInt("camera", 0);
            set { PlayerPrefs.SetInt("camera", value); PlayerPrefs.Save(); }
        }

        /// <summary>-2 (aberto) .. +2 (perto).</summary>
        public static int CameraZoom
        {
            get => PlayerPrefs.GetInt("camera_zoom", 0);
            set { PlayerPrefs.SetInt("camera_zoom", value); PlayerPrefs.Save(); }
        }

        public static bool SeenIntro
        {
            get => PlayerPrefs.GetInt("seen_intro", 0) == 1;
            set { PlayerPrefs.SetInt("seen_intro", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>0 = leve, 1 = equilibrado, 2 = alto.</summary>
        public static int DefaultQuality()
        {
            if (Application.isMobilePlatform)
                return SystemInfo.systemMemorySize >= 3800 ? 1 : 0;
            return SystemInfo.graphicsMemorySize >= 3000 ? 2 : 1;
        }

        public static readonly string[] QualityNames = { "Leve", "Equilibrado", "Alto" };
    }
}
