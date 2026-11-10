using System.Collections;
using System.IO;
using Craquez.Sim;
using Craquez.UI;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Fotografa todos os locais (estádios, ginásios, campos de society e ruas) vistos de fora, com a
    /// montagem 3D de verdade, e salva cada um como imagem (usado no CI com --venues=PASTA). As imagens
    /// vão para Resources/Locais e o seletor de locais mostra a imagem pronta: o visual do 3D, sem
    /// montar nada no menu (abre na hora).
    /// </summary>
    public class VenueBake : MonoBehaviour
    {
        public static string Dir;

        public static bool Requested()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (a.StartsWith("--venues=")) { Dir = a.Substring(9); return true; }
            return false;
        }

        /// <summary>Nome do arquivo (sem extensão) da imagem de um local. Estádio do mandante: um por porte.</summary>
        public static string Name(string mode, int idx, int tier = 1) =>
            mode + "_" + idx + (mode == GameModes.Field && idx == 0 ? "_t" + Mathf.Clamp(tier, 1, 3) : "");

        void Start()
        {
            Directory.CreateDirectory(Dir);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
            StartCoroutine(Bake());
        }

        IEnumerator Bake()
        {
            yield return new WaitForSecondsRealtime(2f);
            int n = 0;
            // estádio do mandante (modelo 0): um por porte (1 a 3), com um time daquele porte
            for (int tier = 1; tier <= 3; tier++)
            {
                int team = TeamOfTier(tier);
                yield return One(GameModes.Field, 0, team, Name(GameModes.Field, 0, tier));
                n++;
            }
            for (int i = 1; i < VenueBuilder.StadiumModels.Length; i++) { yield return One(GameModes.Field, i, 0, Name(GameModes.Field, i)); n++; }
            for (int i = 0; i < VenueBuilder.FutsalNames.Length; i++) { yield return One(GameModes.Futsal, i, 0, Name(GameModes.Futsal, i)); n++; }
            for (int i = 0; i < VenueBuilder.SocietyNames.Length; i++) { yield return One(GameModes.Society, i, 0, Name(GameModes.Society, i)); n++; }
            for (int i = 0; i < VenueBuilder.StreetNames.Length; i++) { yield return One(GameModes.Street, i, 0, Name(GameModes.Street, i)); n++; }
            Debug.Log("[locais] " + n + " imagens prontas em " + Dir);
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        static int TeamOfTier(int tier)
        {
            for (int i = 0; i < Teams.Count; i++)
            {
                var t = Teams.Get(i);
                if (t != null && !t.IsNational && t.stadium != null && t.stadium.name != "Rua" && t.stadium.tier == tier) return i;
            }
            return 0;
        }

        IEnumerator One(string mode, int idx, int team, string name)
        {
            Texture got = null;
            int other = team == 0 ? 1 : 0;
            yield return VenueShot.Render(mode, idx, team, other, 4, t => got = t, true);
            var tex = got as Texture2D;
            if (tex == null) { Debug.LogWarning("[locais] sem imagem: " + name); yield break; }
            try
            {
                File.WriteAllBytes(Path.Combine(Dir, name + ".jpg"), tex.EncodeToJPG(88));
                Debug.Log("[locais] " + name);
            }
            catch (System.Exception ex) { Debug.LogWarning("[locais] " + name + ": " + ex.Message); }
            Destroy(tex);
            yield return null;
        }
    }
}
