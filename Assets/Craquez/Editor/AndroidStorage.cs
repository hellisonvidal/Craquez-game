#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace Craquez.EditorTools
{
    /// <summary>
    /// Android: permissões para ler o option file fora da pasta do app (Downloads, Documentos...).
    /// No Android 11+ a leitura de .json de outras pastas exige "acesso a todos os arquivos"
    /// (MANAGE_EXTERNAL_STORAGE), que o próprio usuário libera nas configurações (o jogo leva até lá).
    /// </summary>
    public class AndroidStorage : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 50;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest)) { Debug.LogWarning("Craquez: manifesto não encontrado em " + manifest); return; }
            string xml = File.ReadAllText(manifest);
            string[] perms =
            {
                "<uses-permission android:name=\"android.permission.READ_EXTERNAL_STORAGE\" android:maxSdkVersion=\"32\" />",
                "<uses-permission android:name=\"android.permission.MANAGE_EXTERNAL_STORAGE\" />",
            };
            int at = xml.IndexOf("<application", System.StringComparison.Ordinal);
            if (at < 0) return;
            foreach (var p in perms)
            {
                string name = p.Substring(p.IndexOf("android:name=", System.StringComparison.Ordinal));
                name = name.Substring(0, name.IndexOf(' '));
                if (xml.Contains(name)) continue;
                xml = xml.Insert(at, "  " + p + "\n  ");
                at = xml.IndexOf("<application", System.StringComparison.Ordinal);
            }
            File.WriteAllText(manifest, xml);
            Debug.Log("Craquez: permissões de leitura de arquivos adicionadas ao manifesto");
        }
    }
}
#endif
