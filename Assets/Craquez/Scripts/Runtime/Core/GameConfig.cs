using System.Collections.Generic;
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
        public bool quickRestarts = Settings.QuickRestarts;   // faltas: cobrança rápida ou cena antes da cobrança

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

        /// <summary>Câmera da partida (lista da 2.1; a escolha da 2.0 é convertida na primeira leitura).</summary>
        public static int Camera
        {
            get
            {
                if (!PlayerPrefs.HasKey("camera_v2") && PlayerPrefs.HasKey("camera"))
                    PlayerPrefs.SetInt("camera_v2", BroadcastCamera.FromOld(PlayerPrefs.GetInt("camera", 0)));
                return PlayerPrefs.GetInt("camera_v2", 0);
            }
            set { PlayerPrefs.SetInt("camera_v2", value); PlayerPrefs.Save(); }
        }

        /// <summary>-2 (aberto) .. +2 (perto).</summary>
        public static int CameraZoom
        {
            get => PlayerPrefs.GetInt("camera_zoom", 0);
            set { PlayerPrefs.SetInt("camera_zoom", value); PlayerPrefs.Save(); }
        }

        /// <summary>Faltas/impedimentos: cobrança rápida (true) ou cena curta com barreira (false).</summary>
        public static bool QuickRestarts
        {
            get => PlayerPrefs.GetInt("quick_restarts", 0) == 1;
            set { PlayerPrefs.SetInt("quick_restarts", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat("music_volume", 0.55f);
            set { PlayerPrefs.SetFloat("music_volume", value); PlayerPrefs.Save(); }
        }

        /// <summary>Trilha sonora dos menus.</summary>
        public static bool Music
        {
            get => PlayerPrefs.GetInt("music", 1) == 1;
            set { PlayerPrefs.SetInt("music", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool SeenIntro
        {
            get => PlayerPrefs.GetInt("seen_intro", 0) == 1;
            set { PlayerPrefs.SetInt("seen_intro", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>0 = leve, 1 = equilibrado, 2 = alto, 3 = ultra (só PC).</summary>
        public static int DefaultQuality()
        {
            if (Application.isMobilePlatform)
                return SystemInfo.systemMemorySize >= 3800 ? 1 : 0;
            if (SystemInfo.graphicsMemorySize >= 6000 && SystemInfo.processorCount >= 6) return 3;
            return SystemInfo.graphicsMemorySize >= 3000 ? 2 : 1;
        }

        static readonly string[] _qualityPc = { "Leve", "Equilibrado", "Alto", "Ultra" };
        static readonly string[] _qualityMobile = { "Leve", "Equilibrado", "Alto" };
        public static string[] QualityNames => Application.isMobilePlatform ? _qualityMobile : _qualityPc;

        // ---- salvar / cancelar (tela de opções) ----
        static readonly string[] IntKeys = { "quality", "vibration", "camera_v2", "camera_zoom", "quick_restarts", "gfx_scale", "gfx_fps", "gfx_vsync", "gfx_aa", "gfx_shadows", "gfx_tex", "gfx_display", "gfx_res", "music" };
        static readonly string[] FloatKeys = { "volume", "music_volume" };

        /// <summary>Foto das preferências ao abrir as opções (para o "Cancelar").</summary>
        public static Dictionary<string, object> Snapshot()
        {
            var d = new Dictionary<string, object>();
            foreach (var k in IntKeys) if (PlayerPrefs.HasKey(k)) d[k] = PlayerPrefs.GetInt(k);
            foreach (var k in FloatKeys) if (PlayerPrefs.HasKey(k)) d[k] = PlayerPrefs.GetFloat(k);
            return d;
        }

        /// <summary>Volta tudo como estava na foto (chaves que não existiam voltam ao padrão).</summary>
        public static void Restore(Dictionary<string, object> d)
        {
            foreach (var k in IntKeys) { if (d.TryGetValue(k, out var v)) PlayerPrefs.SetInt(k, (int)v); else PlayerPrefs.DeleteKey(k); }
            foreach (var k in FloatKeys) { if (d.TryGetValue(k, out var v)) PlayerPrefs.SetFloat(k, (float)v); else PlayerPrefs.DeleteKey(k); }
            PlayerPrefs.Save();
            AudioListener.volume = Volume;
        }

        public static bool Differs(Dictionary<string, object> d)
        {
            var now = Snapshot();
            if (now.Count != d.Count) return true;
            foreach (var kv in now) if (!d.TryGetValue(kv.Key, out var v) || !v.Equals(kv.Value)) return true;
            return false;
        }

        static int I(string k, int d) => PlayerPrefs.GetInt(k, d);
        static void I(string k, int v, bool _) { PlayerPrefs.SetInt(k, v); PlayerPrefs.Save(); }

        // ---- gráficos avançados (0 = automático: segue a predefinição) ----
        public static readonly string[] RenderScaleNames = { "Automática", "50%", "67%", "75%", "85%", "100%", "125%", "150%" };
        public static readonly float[] RenderScaleValues = { 0f, 0.5f, 0.67f, 0.75f, 0.85f, 1f, 1.25f, 1.5f };
        public static int RenderScale { get => I("gfx_scale", 0); set => I("gfx_scale", value, true); }

        public static readonly string[] FpsNames = { "Automático", "30", "60", "90", "120", "144", "165", "240", "Sem limite" };
        public static readonly int[] FpsValues = { 0, 30, 60, 90, 120, 144, 165, 240, -1 };
        public static int FpsCap { get => I("gfx_fps", 0); set => I("gfx_fps", value, true); }

        public static readonly string[] VSyncNames = { "Desligada", "Ligada" };
        public static int VSync { get => I("gfx_vsync", 0); set => I("gfx_vsync", value, true); }

        public static readonly string[] AaNames = { "Automático", "Desligado", "FXAA", "SMAA", "SMAA alto", "MSAA 2x", "MSAA 4x", "MSAA 8x" };
        public static int Antialias { get => I("gfx_aa", 0); set => I("gfx_aa", value, true); }

        public static readonly string[] ShadowNames = { "Automáticas", "Desligadas", "Baixas", "Médias", "Altas", "Muito altas" };
        public static int Shadows { get => I("gfx_shadows", 0); set => I("gfx_shadows", value, true); }

        public static readonly string[] TextureNames = { "Automáticas", "Baixas", "Médias", "Altas" };
        public static int Textures { get => I("gfx_tex", 0); set => I("gfx_tex", value, true); }

        public static readonly string[] DisplayNames = { "Tela cheia (sem bordas)", "Tela cheia exclusiva", "Janela" };
        public static int Display { get => I("gfx_display", 0); set => I("gfx_display", value, true); }

        /// <summary>Índice em Screen.resolutions (-1 = nativa do monitor).</summary>
        public static int Resolution { get => I("gfx_res", -1); set => I("gfx_res", value, true); }

        /// <summary>Qualidade usada para os detalhes do cenário (texturas): a predefinição ou o ajuste avançado.</summary>
        public static int DetailLevel => Textures > 0 ? Textures - 1 + (Textures == 3 && !Application.isMobilePlatform ? 1 : 0) : Quality;
    }
}
