using UnityEngine;

namespace Craquez
{
    /// <summary>Plataforma em que o jogo roda (TV Android, celular, PC).</summary>
    public static class Platform
    {
        static int _tv = -1;
        static string _flavor;

        /// <summary>
        /// Versão do pacote: "" (celular), "tv" (versão para TV: Craquez TV, 32 e 64 bits, padrões de TV).
        /// Vem do arquivo Resources/flavor.txt, gravado pelo CI na build da TV.
        /// </summary>
        public static string Flavor
        {
            get
            {
                if (_flavor == null)
                {
                    var t = Resources.Load<TextAsset>("flavor");
                    _flavor = t != null ? t.text.Trim().ToLowerInvariant() : "";
                }
                return _flavor;
            }
        }

        public static bool TvBuild => Flavor == "tv";

        /// <summary>Android TV / Google TV (inclusive caixas 4K): sem tela de toque, jogo com controle.</summary>
        public static bool IsTv
        {
            get
            {
                if (_tv < 0)
                {
                    _tv = 0;
#if UNITY_ANDROID && !UNITY_EDITOR
                    try
                    {
                        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        using (var act = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        using (var ui = act.Call<AndroidJavaObject>("getSystemService", "uimode"))
                            if (ui != null && ui.Call<int>("getCurrentModeType") == 4) _tv = 1;   // UI_MODE_TYPE_TELEVISION
                    }
                    catch (System.Exception e) { Debug.LogWarning("[plataforma] " + e.Message); }
#endif
                }
                return _tv == 1;
            }
        }

        /// <summary>Altura máxima em que o 3D é desenhado (o HUD e os menus ficam sempre na resolução nativa).
        /// Numa TV 4K (2160 linhas) desenhar o campo em 4K nativo derruba o quadro nas caixas de TV:
        /// desenha mais baixo e amplia com FSR (nítido). PC no "Ultra" desenha nativo.</summary>
        public static int MaxRenderHeight(int quality)
        {
            bool mobile = Application.isMobilePlatform;
            if (!mobile) return quality >= 3 ? 4320 : quality == 2 ? 2160 : 1440;
            // TV (Google TV 4K, ex.: TCL C6K): desenha em 1080p e o FSR amplia para o 4K do painel; o
            // processador da TV é de celular intermediário — mais que isso derruba o quadro
            if (IsTv || TvBuild) return quality >= 2 ? 1440 : quality == 1 ? 1080 : 720;
            return quality >= 3 ? 1440 : quality == 2 ? 1080 : quality == 1 ? 900 : 720;   // (celular: menos pixels = menos calor; o FSR amplia)
        }
    }
}
