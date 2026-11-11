#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace Craquez.EditorTools
{
    /// <summary>
    /// Convite do on-line: o link craquez://sala/CODIGO abre o jogo direto na sala (a página do convite tem
    /// o botão que chama esse link). Acrescenta o filtro de intenção na atividade principal do jogo.
    /// </summary>
    public class AndroidDeepLink : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 60;

        const string Filter =
            "<intent-filter>\n" +
            "        <action android:name=\"android.intent.action.VIEW\" />\n" +
            "        <category android:name=\"android.intent.category.DEFAULT\" />\n" +
            "        <category android:name=\"android.intent.category.BROWSABLE\" />\n" +
            "        <data android:scheme=\"craquez\" android:host=\"sala\" />\n" +
            "      </intent-filter>";

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest)) { Debug.LogWarning("Craquez: manifesto não encontrado para o link do convite"); return; }
            string xml = File.ReadAllText(manifest);
            if (xml.Contains("android:scheme=\"craquez\"")) return;
            int launcher = xml.IndexOf("android.intent.category.LAUNCHER", System.StringComparison.Ordinal);
            if (launcher < 0) { Debug.LogWarning("Craquez: atividade principal não encontrada (link do convite)"); return; }
            int end = xml.IndexOf("</intent-filter>", launcher, System.StringComparison.Ordinal);
            if (end < 0) return;
            end += "</intent-filter>".Length;
            xml = xml.Insert(end, "\n      " + Filter);
            File.WriteAllText(manifest, xml);
            Debug.Log("Craquez: link craquez://sala adicionado ao manifesto");
        }
    }
}
#endif
