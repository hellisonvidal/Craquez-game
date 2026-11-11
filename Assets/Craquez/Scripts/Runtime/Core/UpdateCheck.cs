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
                        string ln = n.ToLowerInvariant();
                        if (!n.EndsWith(".apk", System.StringComparison.OrdinalIgnoreCase) || ln.Contains("casa")) continue;
                        // a versão de TV se atualiza com o APK de TV (32 e 64 bits); o celular, com o do celular
                        if (Platform.TvBuild != ln.Contains("-tv")) continue;
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
            host.StartCoroutine(Download(host, changed));
        }

        static IEnumerator Download(MonoBehaviour host, System.Action changed)
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
            // download cortado no meio (rede caiu) vira "app não instalado" no Android: confere o tamanho
            long got = 0;
            try { got = new System.IO.FileInfo(path).Length; } catch { got = 0; }
            if (ApkSize > 0 && got != ApkSize)
            {
                try { System.IO.File.Delete(path); } catch { }
                Fail("O download veio incompleto (" + got / 1048576 + " de " + ApkSize / 1048576 + " MB). Toque em ATUALIZAR de novo.", changed);
                yield break;
            }
            Progress = 1f;
            _ready = true;
            Install(host, changed);
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

        /// <summary>Instala o APK baixado pelo instalador de sessões do Android (CraquezInstaller.java): confere
        /// antes pacote, versão e assinatura e acompanha o resultado, com o motivo exato se falhar. A primeira vez
        /// o Android pede para permitir "instalar apps desconhecidos" do Craquez.</summary>
        public static void Install(MonoBehaviour host, System.Action changed)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var uriC = new AndroidJavaClass("android.net.Uri"))
                using (var inst = new AndroidJavaClass("com.hellisonvidal.craquez.CraquezInstaller"))
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
                            changed?.Invoke();
                            return;
                        }
                    }
                    string path = CacheApkPath();
                    string bad = inst.CallStatic<string>("check", act, path);
                    if (!string.IsNullOrEmpty(bad))
                    {
                        Progress = -1f;
                        if (bad.StartsWith("arquivo:")) { _ready = false; InstallMsg = "O arquivo baixado está com defeito (" + bad.Substring(8) + "). Toque em ATUALIZAR para baixar de novo."; }
                        else if (bad.StartsWith("assinatura:")) InstallMsg = "A versão instalada foi assinada com outra chave e o Android não deixa trocar por cima. Desinstale o Craquez e instale a " + Latest + " uma vez pela página de versões; depois as atualizações seguem pelo jogo.";
                        else InstallMsg = "Não dá para instalar este arquivo: " + bad.Substring(bad.IndexOf(':') + 1) + ".";
                        changed?.Invoke();
                        return;
                    }
                    string err = inst.CallStatic<string>("install", act, path);
                    Progress = -1f;
                    if (!string.IsNullOrEmpty(err)) { InstallMsg = "Não foi possível abrir o instalador (" + err + ")."; changed?.Invoke(); return; }
                }
                InstallMsg = "Instalando a versão " + Latest + "… confirme na tela do Android.";
                if (host != null) host.StartCoroutine(Watch(changed));
            }
            catch (System.Exception e) { Progress = -1f; InstallMsg = "Não foi possível abrir o instalador (" + e.Message + ")."; }
#endif
            changed?.Invoke();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Acompanha o resultado da sessão de instalação e traduz o motivo das falhas.</summary>
        static IEnumerator Watch(System.Action changed)
        {
            float t = 0f;
            while (t < 600f)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                t += 0.5f;
                string st;
                using (var inst = new AndroidJavaClass("com.hellisonvidal.craquez.CraquezInstaller"))
                    st = inst.CallStatic<string>("status");
                if (st == "ok") { InstallMsg = "Versão " + Latest + " instalada. Abra o Craquez de novo."; changed?.Invoke(); yield break; }
                if (!st.StartsWith("erro:")) continue;
                var p = st.Split(new[] { ':' }, 3);
                int code = 0;
                int.TryParse(p.Length > 1 ? p[1] : "0", out code);
                string det = p.Length > 2 ? p[2] : "";
                InstallMsg = FailText(code, det);
                changed?.Invoke();
                yield break;
            }
        }
#endif

        /// <summary>Códigos de PackageInstaller.STATUS_FAILURE_* em português.</summary>
        public static string FailText(int code, string detail)
        {
            switch (code)
            {
                case 3: return "Instalação cancelada. Toque em ATUALIZAR para tentar de novo (o download já está feito).";   // ABORTED
                case 4: _ready = false; return "O Android achou o arquivo inválido. Toque em ATUALIZAR para baixar de novo.";   // INVALID
                case 5: return "A versão instalada foi assinada com outra chave. Desinstale o Craquez e instale a " + Latest + " uma vez pela página de versões; depois as atualizações seguem pelo jogo.";   // CONFLICT
                case 6: return "Sem espaço no aparelho para instalar (precisa de uns 300 MB livres). Libere espaço e toque em ATUALIZAR.";   // STORAGE
                case 7: return "Esta versão não é compatível com o aparelho.";   // INCOMPATIBLE
                case 2: return "O Android bloqueou a instalação" + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")") + ". Se o Play Protect avisar, toque em \"instalar mesmo assim\".";   // BLOCKED
                default: return "O Android não instalou" + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")") + ". Toque em ATUALIZAR para tentar de novo.";
            }
        }

        static bool _ready;

        /// <summary>Toque em ATUALIZAR: instala o que já foi baixado ou baixa.</summary>
        public static void Update(MonoBehaviour host, System.Action changed)
        {
            if (_ready && Progress < 0f) { Install(host, changed); return; }
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
