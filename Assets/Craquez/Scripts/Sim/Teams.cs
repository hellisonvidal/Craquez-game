using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Craquez.Sim
{
    public class Kit
    {
        public Color c1, c2, shorts, socks;
        public int pattern;
        /// <summary>Mangas e acabamento (gola/punhos) com cor própria; alfa 0 = automático.</summary>
        public Color sleeves = Color.clear, trim = Color.clear;   // 0 liso, 1 listras, 2 aros, 3 faixa, 4 metades, 5 riscas, 6 faixa no peito
    }

    public class RosterEntry
    {
        public string n = "", pos = "", nat = "";
        public int num, fid, ovr;
        public float[] f = new float[0];
        public float[] a = new float[0];
        public List<string> t = new List<string>();
    }

    public class Stadium
    {
        public string name = "";
        public int tier = 1, cap;
    }

    public class TeamData
    {
        public int index;
        public string name = "", shortName = "", league = "", leagueId = "", country = "";
        public Color c1 = Color.white, c2 = Color.black;
        public float rating = 0.7f;
        public List<Kit> kits = new List<Kit>();
        public Stadium stadium = new Stadium();
        public List<RosterEntry> roster = new List<RosterEntry>();
        /// <summary>Seleções: posição no ranking e bandeira [layout, cores...].</summary>
        public int rank;
        public List<string> flag = new List<string>();
        /// <summary>Seleções: código do país (bandeira em Resources/Flags).</summary>
        public string iso = "";
        public bool IsNational => rank > 0;
        public string Key => leagueId + "/" + shortName;
    }

    public class League
    {
        public string id = "", name = "", sport = "football", country = "";
        public int first, count;
        public bool IsFutsal => sport.Contains("futsal");
        public bool IsStreet => sport.Contains("street");
        public bool IsNational => sport.Contains("national");
    }

    /// <summary>Táticas escolhidas para a partida.</summary>
    public class Tactics
    {
        public string formation = "";
        public int mentality = 1;   // 0 defensiva, 1 equilibrada, 2 ofensiva
        public int press = 1;       // 0 baixa, 1 média, 2 alta
        public int[] lineup = new int[0];
        public int boots = -1;      // modelo de chuteira do time (-1 = cada atleta com a sua)
    }

    /// <summary>
    /// Times e ligas (Resources/Data/leagues.json — nomes alterados, sem licenças)
    /// + "option file" do usuário com nomes, uniformes, estádios e escudos reais.
    /// </summary>
    public static class Teams
    {
        public static readonly string[] PatternNames = { "liso", "listras", "aros", "faixa", "metades", "riscas", "faixa_peito", "faixa_vertical", "xadrez" };
        public static readonly string[] KitNames = { "Principal", "Reserva", "Terceiro" };

        static bool _loaded;
        static readonly List<League> _leagues = new List<League>();
        static readonly List<TeamData> _flat = new List<TeamData>();
        static readonly Dictionary<int, Texture2D> _crests = new Dictionary<int, Texture2D>();
        public static int OverrideCount { get; private set; }
        public static string OptionDir { get; private set; } = "";
        /// <summary>Texto do banco (para testes fora do Unity).</summary>
        public static string SourceOverride;
        /// <summary>O option_file.json lido (para as seções de bolas, chuteiras e locais, aplicadas pelo jogo).</summary>
        public static object OptionDoc { get; private set; }

        public static IReadOnlyList<League> Leagues { get { Ensure(); return _leagues; } }
        public static int Count { get { Ensure(); return _flat.Count; } }

        public static TeamData Get(int i)
        {
            Ensure();
            int n = Mathf.Max(1, _flat.Count);
            return _flat[((i % n) + n) % n];
        }

        public static int LeagueOf(int i)
        {
            Ensure();
            for (int li = 0; li < _leagues.Count; li++)
                if (i >= _leagues[li].first && i < _leagues[li].first + _leagues[li].count) return li;
            return 0;
        }

        public static Texture2D Crest(int i)
        {
            Ensure();
            return _crests.TryGetValue(i, out var t) ? t : null;
        }

        public static void Reload()
        {
            _loaded = false;
            _leagues.Clear();
            _flat.Clear();
            Ensure();
        }

        static void Ensure()
        {
            if (_loaded) return;
            _loaded = true;
            string text = SourceOverride;
            if (text == null)
            {
                var ta = Resources.Load<TextAsset>("Data/leagues");
                text = ta != null ? ta.text : null;
            }
            if (text != null)
            {
                var root = MiniJson.Parse(text);
                // ligas e times em ordem alfabética (ignora acentos e maiúsculas)
                var cmp = System.Globalization.CultureInfo.InvariantCulture.CompareInfo;
                int Cmp(string x, string y) => cmp.Compare(x ?? "", y ?? "", System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace);
                var los = new List<object>(MiniJson.L(root, "leagues") ?? new List<object>());
                los.Sort((x, y) => Cmp(MiniJson.S(x, "name"), MiniJson.S(y, "name")));
                foreach (var lo in los)
                {
                    var lg = new League
                    {
                        id = MiniJson.S(lo, "id"), name = MiniJson.S(lo, "name"),
                        sport = MiniJson.S(lo, "sport", "football"), country = MiniJson.S(lo, "country"),
                        first = _flat.Count,
                    };
                    var ts = new List<TeamData>();
                    foreach (var to in MiniJson.L(lo, "teams") ?? new List<object>()) ts.Add(ParseTeam(to));
                    ts.Sort((x, y) => Cmp(x.name, y.name));
                    foreach (var t in ts)
                    {
                        t.index = _flat.Count;
                        t.league = lg.name;
                        t.leagueId = lg.id;
                        t.country = lg.country;
                        _flat.Add(t);
                    }
                    lg.count = _flat.Count - lg.first;
                    _leagues.Add(lg);
                }
            }
            if (_flat.Count == 0) Fallback();
            StreetCrews();
            ApplyOptionFile();
        }

        // times de rua: nomes de galera, cores fortes, elenco com atletas sorteados das ligas
        static readonly (string name, string sh, string c1, string c2)[] Crews =
        {
            ("Bonde do Morro", "BDM", "#111827", "#facc15"), ("Cria da Praça", "CDP", "#16a34a", "#f8fafc"), ("Esquadrão da Laje", "EDL", "#dc2626", "#111827"),
            ("Os Brabos da Vila", "OBV", "#7c3aed", "#facc15"), ("Rei do Asfalto", "RDA", "#0f172a", "#f97316"), ("Pivetes do Viaduto", "PDV", "#64748b", "#22d3ee"),
            ("Resenha FC", "RES", "#f472b6", "#111827"), ("Tropa da Quebrada", "TDQ", "#1d4ed8", "#f8fafc"), ("Areia Fina", "ARF", "#fde68a", "#0369a1"),
            ("Gaiola das Feras", "GDF", "#f59e0b", "#111827"), ("Varzeanos", "VAR", "#15803d", "#facc15"), ("Rolê Futebol Clube", "ROL", "#ef4444", "#f8fafc"),
            ("Ginga de Rua", "GIN", "#0ea5e9", "#facc15"), ("Muleke Doido", "MUL", "#a3e635", "#111827"), ("Os Malandros", "MAL", "#111827", "#f8fafc"),
            ("Terraço Esporte", "TER", "#6366f1", "#f97316"),
        };

        static void StreetCrews()
        {
            var pool = new List<RosterEntry>();
            foreach (var t in _flat) foreach (var r in t.roster) pool.Add(r);
            var lg = new League { id = "rua", name = "Times de Rua", sport = "street", country = "", first = _flat.Count };
            var cmp = System.Globalization.CultureInfo.InvariantCulture.CompareInfo;
            var crews = new List<(string name, string sh, string c1, string c2)>(Crews);
            crews.Sort((a, b) => cmp.Compare(a.name, b.name, System.Globalization.CompareOptions.IgnoreNonSpace | System.Globalization.CompareOptions.IgnoreCase));
            uint seed = 2166136261u;
            foreach (var c in crews)
            {
                var t = new TeamData
                {
                    index = _flat.Count, name = c.name, shortName = c.sh, c1 = Col(c.c1), c2 = Col(c.c2),
                    league = lg.name, leagueId = lg.id, rating = 0.62f + (seed % 23) / 100f,
                };
                t.kits = DefaultKits(t.c1, t.c2);
                t.stadium = new Stadium { name = "Rua", tier = 1 };
                for (int i = 0; i < 9 && pool.Count > 0; i++)
                {
                    seed = seed * 1664525u + 1013904223u;
                    var src = pool[(int)(seed % (uint)pool.Count)];
                    t.roster.Add(new RosterEntry { n = src.n, pos = i == 0 ? "DF" : i < 4 ? "MF" : "FW", nat = src.nat, num = i + 1 + (i > 6 ? 3 : 0), ovr = src.ovr, f = src.f, a = src.a, t = src.t });
                }
                _flat.Add(t);
            }
            lg.count = _flat.Count - lg.first;
            _leagues.Add(lg);
        }

        static void Fallback()
        {
            var lg = new League { id = "craq", name = "Liga Craquez", first = 0 };
            string[] names = { "Trovão FC", "Atlético Maré", "Canarinhos EC", "Verdão da Vila" };
            string[] cols = { "#d62828", "#1d4ed8", "#facc15", "#16a34a" };
            for (int i = 0; i < names.Length; i++)
            {
                var t = new TeamData { index = i, name = names[i], shortName = names[i].Substring(0, 3).ToUpperInvariant(), c1 = Col(cols[i]), c2 = Color.white, league = lg.name, leagueId = lg.id };
                t.kits = DefaultKits(t.c1, t.c2);
                _flat.Add(t);
            }
            lg.count = _flat.Count;
            _leagues.Add(lg);
        }

        /// <summary>"#rrggbb" -> Color (sem depender do código nativo do Unity).</summary>
        public static Color Col(string s, Color? def = null)
        {
            if (string.IsNullOrEmpty(s)) return def ?? Color.white;
            s = s.TrimStart('#');
            if (s.Length < 6 || !int.TryParse(s.Substring(0, 6), System.Globalization.NumberStyles.HexNumber, null, out int v))
                return def ?? Color.white;
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
        }

        public static string Hex(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255), g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255), b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            return "#" + r.ToString("x2") + g.ToString("x2") + b.ToString("x2");
        }

        static List<Kit> DefaultKits(Color c1, Color c2)
        {
            return new List<Kit>
            {
                new Kit { c1 = c1, c2 = c2, shorts = c2, socks = c1 },
                new Kit { c1 = c2, c2 = c1, shorts = c2, socks = c2 },
                new Kit { c1 = Col("#111827"), c2 = c1, shorts = Col("#111827"), socks = Col("#111827") },
            };
        }

        static List<Kit> ParseKits(List<object> list, Color c1, Color c2)
        {
            var kits = new List<Kit>();
            if (list != null)
                foreach (var k in list)
                {
                    var kc1 = Col(MiniJson.S(k, "c1", "#ffffff"));
                    var kc2 = Col(MiniJson.S(k, "c2", "#000000"));
                    kits.Add(new Kit
                    {
                        c1 = kc1, c2 = kc2,
                        shorts = Col(MiniJson.S(k, "shorts", ""), kc2),
                        socks = Col(MiniJson.S(k, "socks", ""), kc1),
                        pattern = Mathf.Max(0, System.Array.IndexOf(PatternNames, MiniJson.S(k, "pattern", "liso"))),
                        sleeves = Col(MiniJson.S(k, "sleeves", ""), Color.clear),
                        trim = Col(MiniJson.S(k, "trim", ""), Color.clear),
                    });
                }
            if (kits.Count == 0) kits = DefaultKits(c1, c2);
            while (kits.Count < 3) kits.Add(DefaultKits(c1, c2)[kits.Count]);
            return kits;
        }

        static TeamData ParseTeam(object to)
        {
            var t = new TeamData
            {
                name = MiniJson.S(to, "name"), shortName = MiniJson.S(to, "short"),
                c1 = Col(MiniJson.S(to, "c1", "#ffffff")), c2 = Col(MiniJson.S(to, "c2", "#000000")),
                rating = MiniJson.F(to, "rating", 0.7f), rank = MiniJson.I(to, "rank"),
            };
            foreach (var fo in MiniJson.L(to, "flag") ?? new List<object>()) t.flag.Add(fo as string);
            t.iso = MiniJson.S(to, "iso");
            t.kits = ParseKits(MiniJson.L(to, "kits"), t.c1, t.c2);
            var st = MiniJson.D(to, "stadium");
            if (st != null) t.stadium = new Stadium { name = MiniJson.S(st, "name"), tier = MiniJson.I(st, "tier", 1), cap = MiniJson.I(st, "cap") };
            foreach (var r in MiniJson.L(to, "roster") ?? new List<object>())
                t.roster.Add(ParseRoster(r));
            return t;
        }

        static float[] Floats(List<object> l)
        {
            if (l == null) return new float[0];
            var a = new float[l.Count];
            for (int i = 0; i < l.Count; i++) a[i] = l[i] is double d ? (float)d : 0f;
            return a;
        }

        static RosterEntry ParseRoster(object r)
        {
            var e = new RosterEntry
            {
                n = MiniJson.S(r, "n"), pos = MiniJson.S(r, "pos"), nat = MiniJson.S(r, "nat"),
                num = MiniJson.I(r, "num"), fid = MiniJson.I(r, "fid"), ovr = MiniJson.I(r, "ovr"),
                f = Floats(MiniJson.L(r, "f")), a = Floats(MiniJson.L(r, "a")),
            };
            foreach (var x in MiniJson.L(r, "t") ?? new List<object>()) e.t.Add(x as string);
            return e;
        }

        /// <summary>Uniformes da partida: casa usa o pedido; visitante usa o pedido ou o primeiro que não se confunda.</summary>
        public static Kit[] PickKits(TeamData a, TeamData b, int ka = 0, int kb = -1)
        {
            var A = a.kits[Mathf.Clamp(ka, 0, a.kits.Count - 1)];
            if (kb >= 0) return new[] { A, b.kits[Mathf.Clamp(kb, 0, b.kits.Count - 1)] };
            foreach (int i in new[] { 1, 0, 2 })
                if (KitDistance(A, b.kits[i]) > 0.28f) return new[] { A, b.kits[i] };
            return new[] { A, b.kits[2] };
        }

        public static float KitDistance(Kit a, Kit b)
        {
            var x = a.c1;
            var y = b.c1;
            float lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            return new Vector3(x.r - y.r, x.g - y.g, x.b - y.b).magnitude + Mathf.Abs(lum(x) - lum(y)) * 0.5f;
        }

        // ------------------------------------------------------------- option file
        static IEnumerable<string> OptionDirs()
        {
#if !UNITY_EDITOR && UNITY_STANDALONE
            yield return Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "option_file");
#endif
            yield return Path.Combine(Application.persistentDataPath, "option_file");
        }

        static void ApplyOptionFile()
        {
            OverrideCount = 0;
            OptionDoc = null;
            _crests.Clear();
            if (SourceOverride != null) return;
            foreach (var dir in OptionDirs())
            {
                string path = Path.Combine(dir, "option_file.json");
                if (!File.Exists(path)) continue;
                OptionDir = dir;
                object doc;
                try { doc = MiniJson.Parse(File.ReadAllText(path)); }
                catch (System.Exception e) { Debug.LogWarning("option_file.json inválido: " + e.Message); return; }
                OptionDoc = doc;
                var teams = MiniJson.D(doc, "teams");
                if (teams == null) return;
                for (int i = 0; i < _flat.Count; i++)
                {
                    var t = _flat[i];
                    if (!teams.TryGetValue(t.Key, out var o)) continue;
                    if (MiniJson.S(o, "name", null) is string nm) t.name = nm;
                    if (MiniJson.S(o, "short", null) is string sh) t.shortName = sh;
                    var kl = MiniJson.L(o, "kits");
                    if (kl != null)
                    {
                        t.kits = ParseKits(kl, t.c1, t.c2);
                        t.c1 = t.kits[0].c1;
                        t.c2 = t.kits[0].c2;
                    }
                    var st = MiniJson.D(o, "stadium");
                    if (st != null)
                    {
                        if (st.ContainsKey("name")) t.stadium.name = MiniJson.S(st, "name");
                        if (st.ContainsKey("cap")) t.stadium.cap = MiniJson.I(st, "cap");
                        if (st.ContainsKey("tier")) t.stadium.tier = MiniJson.I(st, "tier", 1);
                    }
                    foreach (var pl in MiniJson.L(o, "players") ?? new List<object>())
                    {
                        int idx = MiniJson.I(pl, "index", -1);
                        if (idx < 0 || idx >= t.roster.Count) continue;
                        var e = t.roster[idx];
                        var pd = (Dictionary<string, object>)pl;
                        if (pd.ContainsKey("n")) e.n = MiniJson.S(pl, "n");
                        if (pd.ContainsKey("num")) e.num = MiniJson.I(pl, "num");
                        if (pd.ContainsKey("pos")) e.pos = MiniJson.S(pl, "pos");
                        if (pd.ContainsKey("nat")) e.nat = MiniJson.S(pl, "nat");
                        if (pd.ContainsKey("ovr")) e.ovr = Mathf.Clamp(MiniJson.I(pl, "ovr"), 30, 99);
                    }
                    string crest = MiniJson.S(o, "crest", "");
                    if (crest != "" && File.Exists(Path.Combine(dir, crest)))
                    {
                        var tex = new Texture2D(2, 2);
                        if (tex.LoadImage(File.ReadAllBytes(Path.Combine(dir, crest)))) _crests[i] = tex;
                    }
                    OverrideCount++;
                }
                return;
            }
        }

        /// <summary>
        /// Documento completo do option file: todos os times (nome, sigla, uniformes, estádio,
        /// elenco) + as seções extras do jogo (bolas, chuteiras, locais).
        /// </summary>
        public static Dictionary<string, object> OptionDocument(Dictionary<string, object> extras = null)
        {
            Ensure();
            var leagues = new List<object>();
            foreach (var lg in _leagues)
            {
                var ids = new List<object>();
                for (int i = 0; i < lg.count; i++) ids.Add(_flat[lg.first + i].Key);
                leagues.Add(new Dictionary<string, object> { ["id"] = lg.id, ["name"] = lg.name, ["teams"] = ids });
            }
            var teams = new Dictionary<string, object>();
            foreach (var t in _flat)
            {
                var kits = new List<object>();
                foreach (var k in t.kits)
                    kits.Add(new Dictionary<string, object>
                    {
                        ["c1"] = Hex(k.c1), ["c2"] = Hex(k.c2),
                        ["shorts"] = Hex(k.shorts), ["socks"] = Hex(k.socks),
                        ["pattern"] = PatternNames[Mathf.Clamp(k.pattern, 0, PatternNames.Length - 1)],
                        ["sleeves"] = k.sleeves.a > 0f ? Hex(k.sleeves) : "",
                        ["trim"] = k.trim.a > 0f ? Hex(k.trim) : "",
                    });
                var players = new List<object>();
                for (int i = 0; i < t.roster.Count; i++)
                {
                    var r = t.roster[i];
                    players.Add(new Dictionary<string, object>
                    {
                        ["index"] = i, ["n"] = r.n, ["num"] = r.num, ["pos"] = r.pos, ["nat"] = r.nat, ["ovr"] = r.ovr,
                        ["traits"] = new List<object>(r.t),
                    });
                }
                teams[t.Key] = new Dictionary<string, object>
                {
                    ["name"] = t.name, ["short"] = t.shortName, ["kits"] = kits,
                    ["stadium"] = new Dictionary<string, object> { ["name"] = t.stadium.name, ["cap"] = t.stadium.cap, ["tier"] = t.stadium.tier },
                    ["crest"] = "crests/" + t.Key.Replace("/", "_") + ".png", ["players"] = players,
                };
            }
            var doc = new Dictionary<string, object>
            {
                ["formato"] = "craquez-option-file-2",
                ["ajuda"] = "Option file do Craquez com tudo o que há no jogo. Edite e salve como option_file.json na pasta option_file " +
                            "(ao lado do executável no PC, ou na pasta de dados do app). Times: nome, sigla, uniformes (cores #RRGGBB e padrão), " +
                            "estádio e elenco (nome, número, posição, nacionalidade, geral 30-99). Escudos: PNG quadrado em crests/ com o nome indicado. " +
                            "bolas/chuteiras: nome e cores de cada modelo (o jogo vem com modelos originais; aqui você pode renomear e recolorir). " +
                            "locais: nomes dos estádios, ginásios, campos de society e ruas. Apague o que não quiser alterar.",
                ["ligas"] = leagues,
                ["teams"] = teams,
            };
            if (extras != null) foreach (var kv in extras) doc[kv.Key] = kv.Value;
            return doc;
        }

        /// <summary>Grava um modelo com tudo para o usuário editar. Retorna o caminho.</summary>
        public static string ExportOptionTemplate(Dictionary<string, object> extras = null)
        {
            string dir = Path.Combine(Application.persistentDataPath, "option_file");
            Directory.CreateDirectory(Path.Combine(dir, "crests"));
            string path = Path.Combine(dir, "option_file_modelo.json");
            File.WriteAllText(path, MiniJson.Write(OptionDocument(extras)));
            return path;
        }
    }
}
