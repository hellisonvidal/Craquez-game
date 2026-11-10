using System.Collections;
using Craquez.Sim;
using UnityEngine;
using UnityEngine.Networking;

namespace Craquez
{
    /// <summary>
    /// Procura uma versão nova do Craquez na página de versões do projeto (GitHub). Roda sozinho ao abrir
    /// o jogo (uma vez) e pelo botão em Configurações; o resultado fica em <see cref="Latest"/>.
    /// </summary>
    public static class UpdateCheck
    {
        const string Api = "https://api.github.com/repos/hellisonvidal/Craquez-game/releases/latest";
        public const string Page = "https://github.com/hellisonvidal/Craquez-game/releases/latest";

        public enum State { Idle, Checking, UpToDate, NewVersion, Failed }
        public static State Status = State.Idle;
        public static string Latest = "", Url = Page;
        /// <summary>APK da versão nova anexado à página de versões (vazio se não houver).</summary>
        public static string ApkUrl = "";
        public static long ApkSize;
        /// <summary>Baixando a versão nova (0..1) ou -1 parado.</summary>
        public static float Progress = -1f;
        public static string InstallMsg = "";
        static bool _auto;

        public static bool HasUpdate => Status == State.NewVersion;

        /// <summary>Checagem automática: só uma vez por sessão.</summary>
        public static void Auto(MonoBehaviour host, System.Action done = null)
        {
            if (_auto) return;
            _auto = true;
            host.StartCoroutine(Run(done));
        }

        public static void Check(MonoBehaviour host, System.Action done) => host.StartCoroutine(Run(done));

        static IEnumerator Run(System.Action done)
        {
            Status = State.Checking;
            using (var rq = UnityWebRequest.Get(Api))
            {
                rq.SetRequestHeader("Accept", "application/vnd.github+json");
                rq.timeout = 12;
                yield return rq.SendWebRequest();
                if (rq.result != UnityWebRequest.Result.Success)
                {
                    Status = State.Failed;
                    done?.Invoke();
                    yield break;
                }
                object doc = null;
                try { doc = MiniJson.Parse(rq.downloadHandler.text); } catch { doc = null; }
                string tag = doc != null ? MiniJson.S(doc, "tag_name", "") : "";
                string url = doc != null ? MiniJson.S(doc, "html_url", Page) : Page;
                if (string.IsNullOrEmpty(tag)) { Status = State.Failed; done?.Invoke(); yield break; }
                Latest = tag.TrimStart('v', 'V');
                ApkUrl = ""; ApkSize = 0;
                var assets = doc != null ? MiniJson.L(doc, "assets") : null;
                if (assets != null)
                    foreach (var a in assets)
                    {
                        string n = MiniJson.S(a, "name", "");
                        if (!n.EndsWith(".apk", System.StringComparison.OrdinalIgnoreCase) || n.ToLowerInvariant().Contains("casa")) continue;
                        ApkUrl = MiniJson.S(a, "browser_download_url", "");
                        ApkSize = (long)MiniJson.F(a, "size", 0f);
                        break;
                    }
                Url = string.IsNullOrEmpty(url) ? Page : url;
                Status = Newer(Latest, Application.version) ? State.NewVersion : State.UpToDate;
                done?.Invoke();
            }
        }

        /// <summary>Atualiza sem sair do jogo: só no Android, com o APK anexado à versão nova
        /// (a versão "casa" é outro aplicativo e não tem APK público: abre a página).</summary>
        public static bool CanInstallInGame =>
#if UNITY_ANDROID && !UNITY_EDITOR
            !string.IsNullOrEmpty(ApkUrl) && !Application.identifier.EndsWith(".casa");
#else
            false;
#endif

        /// <summary>Baixa o APK novo (com progresso em <see cref="Progress"/>) e abre o instalador do Android.
        /// Fora do Android (ou sem APK) abre a página da versão.</summary>
        public static void DownloadAndInstall(MonoBehaviour host, System.Action changed)
        {
            if (!CanInstallInGame) { Application.OpenURL(Url); return; }
            if (Progress >= 0f) return;   // já baixando
            host.StartCoroutine(Download(changed));
        }

        static IEnumerator Download(System.Action changed)
        {
            Progress = 0f;
            InstallMsg = "Baixando a versão " + Latest + "…";
            changed?.Invoke();
            string path = CacheApkPath();
            if (string.IsNullOrEmpty(path)) { Fail("Não foi possível preparar o download.", changed); yield break; }
            try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)); if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
            catch (System.Exception e) { Fail("Sem espaço para baixar (" + e.Message + ").", changed); yield break; }
            using (var rq = new UnityWebRequest(ApkUrl, UnityWebRequest.kHttpVerbGET))
            {
                rq.downloadHandler = new DownloadHandlerFile(path) { removeFileOnAbort = true };
                rq.timeout = 0;
                var op = rq.SendWebRequest();
                float last = -1f;
                while (!op.isDone)
                {
                    Progress = Mathf.Clamp01(rq.downloadProgress);
                    if (Progress - last >= 0.01f) { last = Progress; InstallMsg = "Baixando a versão " + Latest + "…  " + Mathf.RoundToInt(Progress * 100f) + "%"; changed?.Invoke(); }
                    yield return null;
                }
                if (rq.result != UnityWebRequest.Result.Success) { Fail("O download falhou (" + rq.error + "). Tente de novo.", changed); yield break; }
            }
            Progress = 1f;
            Install(changed);
        }

        static void Fail(string msg, System.Action changed)
        {
            Progress = -1f;
            InstallMsg = msg;
            changed?.Invoke();
        }

        static string CacheApkPath()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var dir = act.Call<AndroidJavaObject>("getCacheDir"))
                    return dir.Call<string>("getAbsolutePath") + "/update/craquez.apk";   // o mesmo lugar que o ApkProvider entrega
            }
            catch (System.Exception e) { Debug.LogWarning("[atualização] " + e.Message); }
#endif
            return null;
        }

        /// <summary>Abre o instalador do Android com o APK baixado (a primeira vez o Android pede para
        /// permitir "instalar apps desconhecidos" do Craquez).</summary>
        public static void Install(System.Action changed)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var uriC = new AndroidJavaClass("android.net.Uri"))
                {
                    string pkg = act.Call<string>("getPackageName");
                    using (var pm = act.Call<AndroidJavaObject>("getPackageManager"))
                    {
                        if (!pm.Call<bool>("canRequestPackageInstalls"))
                        {
                            using (var uri = uriC.CallStatic<AndroidJavaObject>("parse", "package:" + pkg))
                            using (var it = new AndroidJavaObject("android.content.Intent", "android.settings.MANAGE_UNKNOWN_APP_SOURCES", uri))
                                act.Call("startActivity", it);
                            Progress = -1f;
                            InstallMsg = "Permita \"instalar apps desconhecidos\" para o Craquez, volte e toque em ATUALIZAR de novo (o download já está feito).";
                            _ready = true;
                            changed?.Invoke();
                            return;
                        }
                    }
                    using (var uri = uriC.CallStatic<AndroidJavaObject>("parse", "content://" + pkg + ".update/craquez.apk"))
                    using (var it = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW"))
                    {
                        it.Call<AndroidJavaObject>("setDataAndType", uri, "application/vnd.android.package-archive");
                        it.Call<AndroidJavaObject>("addFlags", 0x00000001 | 0x10000000);   // ler a URI + nova tarefa
                        act.Call("startActivity", it);
                    }
                }
                Progress = -1f;
                _ready = true;
                InstallMsg = "Confirme a instalação na tela do Android. Se ele disser que o app não pôde ser instalado, a versão instalada foi assinada com outra chave: desinstale e instale a nova uma vez (depois as atualizações seguem pelo jogo).";
            }
            catch (System.Exception e) { Progress = -1f; InstallMsg = "Não foi possível abrir o instalador (" + e.Message + ")."; }
#endif
            changed?.Invoke();
        }

        static bool _ready;

        /// <summary>Toque em ATUALIZAR: instala o que já foi baixado ou baixa.</summary>
        public static void Update(MonoBehaviour host, System.Action changed)
        {
            if (_ready && Progress < 0f) { Install(changed); return; }
            DownloadAndInstall(host, changed);
        }

        /// <summary>a &gt; b, comparando números separados por ponto (2.10.0 &gt; 2.9.1).</summary>
        public static bool Newer(string a, string b)
        {
            var pa = a.Split('.');
            var pb = b.Split('.');
            for (int i = 0; i < Mathf.Max(pa.Length, pb.Length); i++)
            {
                int x = i < pa.Length && int.TryParse(Digits(pa[i]), out int xv) ? xv : 0;
                int y = i < pb.Length && int.TryParse(Digits(pb[i]), out int yv) ? yv : 0;
                if (x != y) return x > y;
            }
            return false;
        }

        static string Digits(string s)
        {
            int n = 0;
            while (n < s.Length && char.IsDigit(s[n])) n++;
            return n == 0 ? "0" : s.Substring(0, n);
        }

        public static string Message()
        {
            switch (Status)
            {
                case State.Checking: return "Procurando atualizações…";
                case State.UpToDate: return "Você está na versão mais nova (" + Application.version + ").";
                case State.NewVersion: return "Nova versão disponível: " + Latest + " (você tem a " + Application.version + ").";
                case State.Failed: return "Não foi possível procurar agora (sem internet?). Tente de novo mais tarde.";
                default: return "Versão instalada: " + Application.version + ".";
            }
        }
    }
}
