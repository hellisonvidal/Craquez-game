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
                Url = string.IsNullOrEmpty(url) ? Page : url;
                Status = Newer(Latest, Application.version) ? State.NewVersion : State.UpToDate;
                done?.Invoke();
            }
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
