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
        public string weather = "aleatorio";   // sol, nublado, chuva, tempestade, neve, aleatorio (padrão: aleatório)

        /// <summary>Local coberto (ginásios de futsal e os society cobertos): sem chuva nem neve.</summary>
        public static bool Indoor(string mode, int venue) => mode == GameModes.Futsal || (mode == GameModes.Society && (venue == 0 || venue == 1 || venue == 6));

        /// <summary>Clima efetivo da partida: sorteia o aleatório e tira chuva/neve dos locais cobertos.</summary>
        public string WeatherKind()
        {
            string w = weather == "aleatorio" ? RandomWeather(seed) : weather;
            if (Indoor(mode, venue) && (w == "chuva" || w == "tempestade" || w == "neve" || w == "nevasca")) w = "nublado";
            return w;
        }
        /// <summary>
        /// Sorteio do "Aleatório" com pesos de um ano de futebol (antes os 6 climas tinham a mesma chance:
        /// neve ou nevasca em 1 de cada 3 partidas). Agora: sol 40%, nublado 28%, chuva 18%, tempestade 7%,
        /// neve 5%, nevasca 2%.
        /// </summary>
        public static string RandomWeather(uint seed)
        {
            uint r = Mix(seed ^ 0x9E3779B9u) % 100u;
            return r < 40 ? "sol" : r < 68 ? "nublado" : r < 86 ? "chuva" : r < 93 ? "tempestade" : r < 98 ? "neve" : "nevasca";
        }

        /// <summary>Embaralha a semente (a semente da partida é sempre ímpar: o resto direto por 6 só dava
        /// nublado, tempestade ou nevasca).</summary>
        public static uint Mix(uint x)
        {
            x ^= x >> 16; x *= 0x7FEB352Du;
            x ^= x >> 15; x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }
        public string timeOfDay = "noite";  // dia, tarde, noite
        public bool dynamicWeather = true;
        public int streetPlayers = 3;    // rua: 3x3, 4x4 ou 5x5
        public int stadium;              // campo: 0 = estádio do mandante; 1.. = modelos
        public bool training;
        public bool tutorial;            // treino guiado do primeiro acesso (com dois companheiros)
        public int trainingRoster = -1;
        public int trainingKeeper = -1;  // treino: goleiro escolhido (índice no elenco do time B)  // treino: atleta escolhido (índice no elenco; -1 = o melhor finalizador)            // arena de treino: atacante x goleiro, sem relógio
        public bool toss = true;         // sorteio do juiz: lados e saída aleatórios
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

        /// <summary>Nomes e números sobre os atletas: 0 controlados e quem tem a bola, 1 todos, 2 desligado.</summary>
        public static int PlayerTags
        {
            get => PlayerPrefs.GetInt("player_tags", 0);
            set { PlayerPrefs.SetInt("player_tags", value); PlayerPrefs.Save(); }
        }
        public static readonly string[] PlayerTagNames = { "Controlados e bola", "Todos", "Desligado" };

        /// <summary>Quadro do jogador com a bola (nome, número, fôlego), do lado do time dele.</summary>
        public static bool PossessionHud
        {
            get => PlayerPrefs.GetInt("possession_hud", 1) == 1;
            set { PlayerPrefs.SetInt("possession_hud", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>"1"/"2" sobre a cabeça de quem cada controle comanda.</summary>
        public static bool PlayerMarkers
        {
            get => PlayerPrefs.GetInt("player_markers", 1) == 1;
            set { PlayerPrefs.SetInt("player_markers", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Layout do controle: para cada ação (passe, chute, alto, enfiada) qual botão da face
        /// (0 = baixo A/✕, 1 = direita B/○, 2 = esquerda X/□, 3 = cima Y/△).</summary>
        public static int[] PadMap
        {
            get
            {
                var s = PlayerPrefs.GetString("pad_map", "0123");
                var m = new int[4];
                for (int i = 0; i < 4; i++) m[i] = i < s.Length ? Mathf.Clamp(s[i] - '0', 0, 3) : i;
                return m;
            }
            set { PlayerPrefs.SetString("pad_map", string.Concat(value[0], value[1], value[2], value[3])); PlayerPrefs.Save(); }
        }
        public static readonly string[][] PadPresets = { new[] { "0123" }, new[] { "0213" }, new[] { "0312" } };
        public static readonly string[] PadPresetNames = { "Padrão (B / ○ chuta)", "X / □ chuta", "Y / △ chuta", "Personalizado" };

        /// <summary>Correr no gatilho (RT/R2, padrão) ou no botão de ombro (RB/R1); o colocado fica no outro.</summary>
        public static bool SprintOnShoulder
        {
            get => PlayerPrefs.GetInt("sprint_rb", 0) == 1;
            set { PlayerPrefs.SetInt("sprint_rb", value ? 1 : 0); PlayerPrefs.Save(); }
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
                CloseCameraOnce();
                return PlayerPrefs.GetInt("camera_v2", CloseCamera);
            }
            set { PlayerPrefs.SetInt("camera_v2", value); PlayerPrefs.Save(); }
        }

        /// <summary>Padrão: "TV Bem Perto" (a câmera de transmissão mais próxima dos atletas).</summary>
        static int CloseCamera => (int)BroadcastCamera.Kind.Close;

        /// <summary>
        /// 2.19: as partidas começam com a câmera o mais perto possível dos jogadores, em todas as
        /// modalidades ("TV Bem Perto" com zoom "Perto"). Vale uma vez para quem já jogava; depois,
        /// o que a pessoa escolher em Configurações fica.
        /// </summary>
        static void CloseCameraOnce()
        {
            if (PlayerPrefs.GetInt("camera_close_v1", 0) == 0)
            {
                PlayerPrefs.SetInt("camera_close_v1", 1);
                PlayerPrefs.SetInt("camera_v2", CloseCamera);
                PlayerPrefs.SetInt("camera_zoom", DefaultZoom);
                PlayerPrefs.SetInt("camera_close_v2", 1);
                PlayerPrefs.Save();
            }
            // 2.20: "Bem perto" ficou perto demais; o padrão passa a "Perto" (só para quem estava no padrão)
            if (PlayerPrefs.GetInt("camera_close_v2", 0) == 0)
            {
                PlayerPrefs.SetInt("camera_close_v2", 1);
                if (PlayerPrefs.GetInt("camera_zoom", DefaultZoom) == 2) PlayerPrefs.SetInt("camera_zoom", DefaultZoom);
                PlayerPrefs.Save();
            }
        }

        const int DefaultZoom = 1;

        /// <summary>-2 (aberto) .. +2 (perto).</summary>
        public static int CameraZoom
        {
            get { CloseCameraOnce(); return PlayerPrefs.GetInt("camera_zoom", DefaultZoom); }
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
        static readonly string[] StringKeys = { "gfx_res_wh" };

        /// <summary>Foto das preferências ao abrir as opções (para o "Cancelar").</summary>
        public static Dictionary<string, object> Snapshot()
        {
            var d = new Dictionary<string, object>();
            foreach (var k in IntKeys) if (PlayerPrefs.HasKey(k)) d[k] = PlayerPrefs.GetInt(k);
            foreach (var k in FloatKeys) if (PlayerPrefs.HasKey(k)) d[k] = PlayerPrefs.GetFloat(k);
            foreach (var k in StringKeys) if (PlayerPrefs.HasKey(k)) d[k] = PlayerPrefs.GetString(k);
            return d;
        }

        /// <summary>Volta tudo como estava na foto (chaves que não existiam voltam ao padrão).</summary>
        public static void Restore(Dictionary<string, object> d)
        {
            foreach (var k in IntKeys) { if (d.TryGetValue(k, out var v)) PlayerPrefs.SetInt(k, (int)v); else PlayerPrefs.DeleteKey(k); }
            foreach (var k in FloatKeys) { if (d.TryGetValue(k, out var v)) PlayerPrefs.SetFloat(k, (float)v); else PlayerPrefs.DeleteKey(k); }
            foreach (var k in StringKeys) { if (d.TryGetValue(k, out var v)) PlayerPrefs.SetString(k, (string)v); else PlayerPrefs.DeleteKey(k); }
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
        /// <summary>Resolução escolhida como "LxA" (vazio = nativa).</summary>
        public static string ResolutionWH { get => PlayerPrefs.GetString("gfx_res_wh", ""); set { PlayerPrefs.SetString("gfx_res_wh", value); } }

        /// <summary>Qualidade usada para os detalhes do cenário (texturas): a predefinição ou o ajuste avançado.</summary>
        public static int DetailLevel => Textures > 0 ? Textures - 1 + (Textures == 3 && !Application.isMobilePlatform ? 1 : 0) : Quality;
    }
}
