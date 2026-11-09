using System.Collections.Generic;
using UnityEngine;

namespace Craquez.Sim
{
    public enum CompFormat { League, GroupsKnockout, Knockout }

    /// <summary>Uma competição: quem joga, em que modalidade, o formato e o troféu.</summary>
    public class CompDef
    {
        public string id, name, desc, mode;
        public bool national;
        public CompFormat format;
        public int size;                 // número de participantes
        public string[] leagues;         // clubes: ligas de onde saem os times (vazio = todas)
        public string[] continents;      // seleções: continentes (vazio = todos)
        public int trophy;               // desenho do troféu (TrophyArt)
        public string category = "Torneios";   // grupo na lista (torneios, ligas, copas nacionais)
        public Color c1, c2;             // metal e detalhe do troféu
    }

    /// <summary>Um jogo da competição (gols -1 = ainda não jogado; pênaltis no mata-mata empatado).</summary>
    public class CompMatch
    {
        public int a, b, ga = -1, gb = -1, pa, pb, round, group = -1;
        public string stage = "";
        public bool Played => ga >= 0;
        public int Winner => !Played ? -1 : ga > gb ? a : gb > ga ? b : pa > pb ? a : b;
    }

    public class TableRow
    {
        public int team, p, w, d, l, gf, ga;
        public int Pts => w * 3 + d;
        public int Gd => gf - ga;
    }

    /// <summary>
    /// As 15 competições do modo Competições e a campanha em andamento: tabela de pontos
    /// corridos, fase de grupos + mata-mata ou mata-mata direto. Os jogos sem o usuário são
    /// simulados pela força dos times (gols por Poisson); empate no mata-mata vai aos pênaltis.
    /// </summary>
    public static class Competitions
    {
        static readonly string[] Europe = { "ita", "eng", "esp", "fra", "ned", "por", "rus", "ukr" };
        static readonly string[] SouthAm = { "bra", "arg", "uru", "per", "chi", "ecu" };

        static CompDef D(string id, string name, string desc, string mode, bool nat, CompFormat f, int size, string[] lg, string[] ct, int trophy, Color c1, Color c2)
            => new CompDef { id = id, name = name, desc = desc, mode = mode, national = nat, format = f, size = size, leagues = lg ?? new string[0], continents = ct ?? new string[0], trophy = trophy, c1 = c1, c2 = c2 };

        static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f), Silver = new Color(0.86f, 0.88f, 0.92f), Bronze = new Color(0.82f, 0.52f, 0.3f);

        static readonly CompDef[] Fixed =
        {
            D("copa_mundo", "Copa do Mundo Craquez", "As 16 melhores seleções: grupos e mata-mata", GameModes.Field, true, CompFormat.GroupsKnockout, 16, null, null, 0, Gold, new Color(0.1f, 0.55f, 0.3f)),
            D("copa_america", "Copa das Américas", "Seleções das Américas: 2 grupos e semifinais", GameModes.Field, true, CompFormat.GroupsKnockout, 8, null, new[] { "América do Sul", "América do Norte e Central" }, 1, Silver, new Color(0.15f, 0.35f, 0.75f)),
            D("eurocopa", "Eurocopa Craquez", "16 seleções europeias: grupos e mata-mata", GameModes.Field, true, CompFormat.GroupsKnockout, 16, null, new[] { "Europa" }, 2, Silver, new Color(0.1f, 0.2f, 0.55f)),
            D("afro_asiatica", "Copa Afro-Asiática", "Seleções da África, Ásia e Oceania em mata-mata", GameModes.Field, true, CompFormat.Knockout, 8, null, new[] { "África", "Ásia e Oceania" }, 3, Gold, new Color(0.75f, 0.2f, 0.12f)),
            D("liga_campeoes", "Liga dos Campeões Craquez", "Os 16 maiores clubes da Europa", GameModes.Field, false, CompFormat.GroupsKnockout, 16, Europe, null, 4, Silver, new Color(0.08f, 0.1f, 0.35f)),
            D("libertadores", "Copa Libertadores Craquez", "Os 16 maiores clubes da América do Sul", GameModes.Field, false, CompFormat.GroupsKnockout, 16, SouthAm, null, 5, Silver, new Color(0.85f, 0.7f, 0.2f)),
            D("brasileirao", "Brasileirão Craquez", "Série A em pontos corridos (turno único)", GameModes.Field, false, CompFormat.League, 20, new[] { "bra" }, null, 6, Gold, new Color(0.05f, 0.55f, 0.25f)),
            D("copa_brasil", "Copa do Brasil Craquez", "16 clubes das Séries A e B no mata-mata", GameModes.Field, false, CompFormat.Knockout, 16, new[] { "bra", "brb" }, null, 7, Gold, new Color(0.1f, 0.3f, 0.7f)),
            D("sul_americana", "Copa Sul-Americana Craquez", "16 clubes de Argentina, Uruguai, Peru, Chile e Equador", GameModes.Field, false, CompFormat.Knockout, 16, new[] { "arg", "uru", "per", "chi", "ecu" }, null, 8, Silver, new Color(0.1f, 0.45f, 0.35f)),
            D("mundial_clubes", "Mundial de Clubes Craquez", "Os 8 melhores clubes do mundo", GameModes.Field, false, CompFormat.Knockout, 8, null, null, 9, Gold, new Color(0.85f, 0.12f, 0.15f)),
            D("supercopa", "Supercopa dos Gigantes", "Os 4 clubes mais fortes: semifinal e final", GameModes.Field, false, CompFormat.Knockout, 4, null, null, 10, Bronze, new Color(0.12f, 0.12f, 0.14f)),
            D("mundial_futsal", "Copa do Mundo de Futsal", "8 seleções de futsal: grupos e mata-mata", GameModes.Futsal, true, CompFormat.GroupsKnockout, 8, null, null, 11, Gold, new Color(0.95f, 0.45f, 0.1f)),
            D("liga_futsal", "Liga Nacional de Futsal Craquez", "Pontos corridos com os 16 clubes", GameModes.Futsal, false, CompFormat.League, 16, new[] { "fbr" }, null, 12, Silver, new Color(0.9f, 0.2f, 0.25f)),
            D("copa_society", "Copa Society Craquez", "8 clubes no society, mata-mata", GameModes.Society, false, CompFormat.Knockout, 8, new[] { "bra" }, null, 13, Bronze, new Color(0.2f, 0.65f, 0.3f)),
            D("torneio_rua", "Torneio de Rua Craquez", "8 times na rua, mata-mata", GameModes.Street, false, CompFormat.Knockout, 8, null, null, 14, Silver, new Color(0.95f, 0.75f, 0.1f)),
        };

        static CompDef[] _all;

        /// <summary>
        /// Todas as competições: os 15 torneios e, para cada liga do jogo, o campeonato (pontos
        /// corridos) e a copa nacional do país (mata-mata).
        /// </summary>
        public static CompDef[] All
        {
            get
            {
                if (_all != null) return _all;
                var l = new List<CompDef>(Fixed);
                var cups = new List<CompDef>();
                var cupCountries = new HashSet<string>();
                int k = 0;
                foreach (var lg in Teams.Leagues)
                {
                    if (lg.IsNational || lg.IsStreet || lg.count < 4) continue;
                    bool futsal = lg.IsFutsal;
                    string mode = futsal ? GameModes.Futsal : GameModes.Field;
                    var c2 = Palette[k % Palette.Length];
                    // campeonato da liga (Série A e Liga Nacional de Futsal já existem nos torneios)
                    if (lg.id != "bra" && lg.id != "fbr")
                    {
                        var d = D("liga_" + lg.id, lg.name, "Campeonato em pontos corridos com os " + lg.count + " times", mode, false, CompFormat.League, lg.count, new[] { lg.id }, null, (k * 7 + 3) % 15, k % 2 == 0 ? Gold : Silver, c2);
                        d.category = futsal ? "Ligas de futsal" : "Ligas";
                        l.Add(d);
                    }
                    // copa nacional do país (todas as divisões do país juntas)
                    string key = (futsal ? "f" : "") + lg.country;
                    if (!cupCountries.Add(key) || lg.country == "BR" && !futsal) { k++; continue; }
                    var same = new List<string>();
                    int total = 0;
                    foreach (var o in Teams.Leagues)
                        if (!o.IsNational && !o.IsStreet && o.IsFutsal == futsal && o.country == lg.country) { same.Add(o.id); total += o.count; }
                    int size = total >= 16 ? 16 : total >= 8 ? 8 : 4;
                    var cup = D("copa_" + key.ToLowerInvariant(), CupName(lg.country, futsal), "Mata-mata com " + size + " times do país", mode, false, CompFormat.Knockout, size, same.ToArray(), null, (k * 11 + 6) % 15, k % 2 == 0 ? Silver : Gold, c2);
                    cup.category = futsal ? "Copas de futsal" : "Copas nacionais";
                    cups.Add(cup);
                    k++;
                }
                l.AddRange(cups);
                return _all = l.ToArray();
            }
        }

        /// <summary>Recria a lista (o option file pode ter mudado nomes das ligas).</summary>
        public static void Refresh() => _all = null;

        static readonly Color[] Palette =
        {
            new Color(0.75f, 0.12f, 0.15f), new Color(0.1f, 0.3f, 0.7f), new Color(0.1f, 0.55f, 0.3f), new Color(0.55f, 0.2f, 0.65f),
            new Color(0.9f, 0.5f, 0.1f), new Color(0.1f, 0.55f, 0.65f), new Color(0.2f, 0.2f, 0.24f), new Color(0.85f, 0.7f, 0.15f),
        };

        static string CupName(string country, bool futsal)
        {
            string c;
            switch (country)
            {
                case "IT": c = "Copa da Itália"; break;
                case "EN": c = "Copa da Inglaterra"; break;
                case "ES": c = "Copa da Espanha"; break;
                case "FR": c = "Copa da França"; break;
                case "US": c = "Copa dos Estados Unidos"; break;
                case "AR": c = "Copa da Argentina"; break;
                case "UY": c = "Copa do Uruguai"; break;
                case "PE": c = "Copa do Peru"; break;
                case "CL": c = "Copa do Chile"; break;
                case "EC": c = "Copa do Equador"; break;
                case "SA": c = "Copa da Arábia Saudita"; break;
                case "NL": c = "Copa da Holanda"; break;
                case "PT": c = "Taça de Portugal"; break;
                case "RU": c = "Copa da Rússia"; break;
                case "UA": c = "Copa da Ucrânia"; break;
                case "BR": c = "Copa do Brasil"; break;
                default: c = "Copa " + country; break;
            }
            return futsal ? c + " de Futsal" : c;
        }

        public static CompDef Find(string id)
        {
            foreach (var d in All) if (d.id == id) return d;
            return null;
        }

        /// <summary>Times que podem disputar (os melhores primeiro).</summary>
        public static List<int> Pool(CompDef d)
        {
            var ids = new List<int>();
            foreach (var lg in Teams.Leagues)
            {
                if (lg.IsStreet) continue;
                bool futsal = d.mode == GameModes.Futsal;
                if (lg.IsFutsal != futsal || lg.IsNational != d.national) continue;
                if (!d.national && d.leagues.Length > 0 && System.Array.IndexOf(d.leagues, lg.id) < 0) continue;
                for (int i = 0; i < lg.count; i++)
                {
                    int t = lg.first + i;
                    if (d.national && d.continents.Length > 0 && System.Array.IndexOf(d.continents, Teams.Continent(Teams.Get(t).iso)) < 0) continue;
                    ids.Add(t);
                }
            }
            ids.Sort((x, y) => Strength(y).CompareTo(Strength(x)));
            return ids;
        }

        public static float Strength(int t)
        {
            var tm = Teams.Get(t);
            if (tm == null) return 0.5f;
            if (tm.roster.Count == 0) return tm.rating;
            float s = 0f;
            foreach (var r in tm.roster) s += r.ovr;
            return Mathf.Lerp(tm.rating, s / tm.roster.Count / 100f, 0.5f);
        }
    }

    /// <summary>Campanha em andamento (salva como JSON).</summary>
    public class CompRun
    {
        public string id;
        public int user;
        public uint seed;
        public List<int> teams = new List<int>();
        public List<List<int>> groups = new List<List<int>>();
        public List<CompMatch> matches = new List<CompMatch>();
        public int champion = -1;
        System.Random _rng;

        public CompDef Def => Competitions.Find(id);
        System.Random Rng => _rng ??= new System.Random((int)(seed ^ (uint)matches.Count * 2654435761u));

        /// <summary>Começa a competição com o time do usuário (entra no lugar do mais fraco se precisar).</summary>
        public static CompRun Start(CompDef d, int userTeam, uint seed)
        {
            var r = new CompRun { id = d.id, user = userTeam, seed = seed };
            var pool = Competitions.Pool(d);
            int n = Mathf.Min(d.size, pool.Count);
            if (d.format != CompFormat.League) n = n >= 16 ? 16 : n >= 8 ? 8 : n >= 4 ? 4 : 2;
            for (int i = 0; i < pool.Count && r.teams.Count < n; i++) r.teams.Add(pool[i]);
            if (!r.teams.Contains(userTeam))
            {
                if (r.teams.Count >= n) r.teams.RemoveAt(r.teams.Count - 1);
                r.teams.Add(userTeam);
            }
            r.teams.Sort((x, y) => Competitions.Strength(y).CompareTo(Competitions.Strength(x)));
            switch (d.format)
            {
                case CompFormat.League:
                    r.groups.Add(new List<int>(r.teams));
                    r.RoundRobin(r.groups[0], 0, 0, "Rodada");
                    break;
                case CompFormat.GroupsKnockout:
                {
                    int ng = Mathf.Max(1, r.teams.Count / 4);
                    for (int g = 0; g < ng; g++) r.groups.Add(new List<int>());
                    // potes: cabeças de chave espalhados (serpentina pelos grupos)
                    for (int i = 0; i < r.teams.Count; i++)
                    {
                        int pot = i / ng, k = i % ng;
                        r.groups[pot % 2 == 0 ? k : ng - 1 - k].Add(r.teams[i]);
                    }
                    for (int g = 0; g < ng; g++) r.RoundRobin(r.groups[g], g, 0, "Grupo " + (char)('A' + g));
                    break;
                }
                default:
                    r.KnockoutRound(SeedBracket(r.teams), 0);
                    break;
            }
            return r;
        }

        static List<int> SeedBracket(List<int> seeded)
        {
            // 1 x último, 2 x penúltimo... com os cabeças em lados opostos da chave
            var order = new List<int> { 0 };
            while (order.Count < seeded.Count)
            {
                var next = new List<int>();
                int m = order.Count * 2;
                foreach (var o in order) { next.Add(o); next.Add(m - 1 - o); }
                order = next;
            }
            var res = new List<int>();
            foreach (var o in order) res.Add(seeded[o]);
            return res;
        }

        void RoundRobin(List<int> ts, int group, int firstRound, string stage)
        {
            var list = new List<int>(ts);
            if (list.Count % 2 == 1) list.Add(-1);
            int n = list.Count;
            for (int r = 0; r < n - 1; r++)
            {
                for (int i = 0; i < n / 2; i++)
                {
                    int a = list[i], b = list[n - 1 - i];
                    if (a < 0 || b < 0) continue;
                    if (r % 2 == 1) { int t = a; a = b; b = t; }
                    matches.Add(new CompMatch { a = a, b = b, round = firstRound + r, group = group, stage = stage });
                }
                // rotação do método do círculo (o primeiro fica parado)
                int last = list[n - 1];
                list.RemoveAt(n - 1);
                list.Insert(1, last);
            }
        }

        void KnockoutRound(List<int> ts, int round)
        {
            string stage = ts.Count == 2 ? "Final" : ts.Count == 4 ? "Semifinal" : ts.Count == 8 ? "Quartas de final" : ts.Count == 16 ? "Oitavas de final" : "Mata-mata";
            for (int i = 0; i + 1 < ts.Count; i += 2) matches.Add(new CompMatch { a = ts[i], b = ts[i + 1], round = round, stage = stage });
        }

        public int CurrentRound
        {
            get
            {
                int r = int.MaxValue;
                foreach (var m in matches) if (!m.Played) r = Mathf.Min(r, m.round);
                return r == int.MaxValue ? -1 : r;
            }
        }

        public bool Finished => champion >= 0;

        /// <summary>O próximo jogo do usuário (nulo se ele já foi eliminado ou acabou).</summary>
        public CompMatch NextUserMatch()
        {
            int r = CurrentRound;
            if (r < 0) return null;
            foreach (var m in matches) if (!m.Played && m.round == r && (m.a == user || m.b == user)) return m;
            return null;
        }

        /// <summary>O usuário ainda disputa (não foi eliminado e a competição não acabou).</summary>
        public bool UserAlive => !Finished && StillIn(user);

        bool StillIn(int t)
        {
            // pontos corridos e grupos: segue até o fim da fase; mata-mata: perdeu, saiu
            var d = Def;
            if (d.format == CompFormat.League) return true;
            foreach (var m in matches) if (m.group < 0 && m.Played && (m.a == t || m.b == t) && m.Winner != t) return false;
            if (d.format == CompFormat.GroupsKnockout && GroupsDone && !matches.Exists(m => m.group < 0 && (m.a == t || m.b == t))) return false;
            return true;
        }

        bool GroupsDone => !matches.Exists(m => m.group >= 0 && !m.Played);

        /// <summary>Resultado do jogo do usuário (gols do usuário primeiro) e simulação do resto da rodada.</summary>
        public void PlayRound(int userGoals, int oppGoals)
        {
            var um = NextUserMatch();
            if (um != null)
            {
                if (um.a == user) { um.ga = userGoals; um.gb = oppGoals; }
                else { um.ga = oppGoals; um.gb = userGoals; }
                if (um.group < 0 && um.ga == um.gb) Penalties(um);
            }
            SimulateRound();
        }

        /// <summary>Simula a rodada atual inteira (inclusive o jogo do usuário, se ele pedir).</summary>
        public void SimulateRound()
        {
            int r = CurrentRound;
            if (r < 0) { Advance(); return; }   // (o último jogo da fase foi o do usuário)
            foreach (var m in matches)
                if (!m.Played && m.round == r) Simulate(m);
            Advance();
        }

        void Simulate(CompMatch m)
        {
            var d = Def;
            float avg = d.mode == GameModes.Futsal ? 2.6f : d.mode == GameModes.Field ? 1.3f : 2.2f;
            float sa = Competitions.Strength(m.a), sb = Competitions.Strength(m.b);
            m.ga = Poisson(avg * Mathf.Exp(3.2f * (sa - sb)));
            m.gb = Poisson(avg * Mathf.Exp(3.2f * (sb - sa)));
            if (m.group < 0 && m.ga == m.gb) Penalties(m);
        }

        void Penalties(CompMatch m)
        {
            // cobranças alternadas (5 + alternadas), quem é mais forte converte um pouco mais
            float ka = 0.74f + (Competitions.Strength(m.a) - 0.7f) * 0.4f, kb = 0.74f + (Competitions.Strength(m.b) - 0.7f) * 0.4f;
            int a = 0, b = 0;
            for (int i = 0; i < 5; i++) { if (Rng.NextDouble() < ka) a++; if (Rng.NextDouble() < kb) b++; }
            for (int i = 0; a == b && i < 20; i++) { if (Rng.NextDouble() < ka) a++; if (Rng.NextDouble() < kb) b++; }
            if (a == b) a++;
            m.pa = a; m.pb = b;
        }

        int Poisson(float l)
        {
            float L = Mathf.Exp(-Mathf.Clamp(l, 0.05f, 8f)), p = 1f;
            int k = 0;
            do { k++; p *= (float)Rng.NextDouble(); } while (p > L && k < 15);
            return k - 1;
        }

        void Advance()
        {
            if (CurrentRound >= 0) return;   // ainda há jogos na rodada/fase
            var d = Def;
            int nextRound = 0;
            foreach (var m in matches) nextRound = Mathf.Max(nextRound, m.round + 1);
            if (d.format == CompFormat.League)
            {
                champion = Table(0)[0].team;
                return;
            }
            var ko = matches.FindAll(m => m.group < 0);
            if (d.format == CompFormat.GroupsKnockout && ko.Count == 0)
            {
                // classificados: 1º e 2º de cada grupo, cruzados (1A x 2B, 1B x 2A...)
                var first = new List<int>();
                var second = new List<int>();
                for (int g = 0; g < groups.Count; g++) { var t = Table(g); first.Add(t[0].team); second.Add(t[1].team); }
                var ts = new List<int>();
                for (int g = 0; g < groups.Count; g += 2)
                {
                    int h = Mathf.Min(g + 1, groups.Count - 1);
                    ts.Add(first[g]); ts.Add(second[h]);
                    if (h != g) { ts.Add(first[h]); ts.Add(second[g]); }
                }
                KnockoutRound(ts, nextRound);
                return;
            }
            // mata-mata: vencedores da última rodada
            int last = -1;
            foreach (var m in ko) last = Mathf.Max(last, m.round);
            var winners = new List<int>();
            foreach (var m in ko) if (m.round == last) winners.Add(m.Winner);
            if (winners.Count <= 1) { champion = winners.Count == 1 ? winners[0] : -1; return; }
            KnockoutRound(winners, nextRound);
        }

        /// <summary>Classificação de um grupo (ou da liga, grupo 0): pontos, saldo, gols pró.</summary>
        public List<TableRow> Table(int group)
        {
            var rows = new Dictionary<int, TableRow>();
            foreach (var t in groups[group]) rows[t] = new TableRow { team = t };
            foreach (var m in matches)
            {
                if (!m.Played || m.group != group) continue;
                if (!rows.ContainsKey(m.a) || !rows.ContainsKey(m.b)) continue;
                var A = rows[m.a]; var B = rows[m.b];
                A.p++; B.p++;
                A.gf += m.ga; A.ga += m.gb; B.gf += m.gb; B.ga += m.ga;
                if (m.ga > m.gb) { A.w++; B.l++; } else if (m.gb > m.ga) { B.w++; A.l++; } else { A.d++; B.d++; }
            }
            var list = new List<TableRow>(rows.Values);
            list.Sort((x, y) =>
            {
                int c = y.Pts.CompareTo(x.Pts);
                if (c == 0) c = y.Gd.CompareTo(x.Gd);
                if (c == 0) c = y.gf.CompareTo(x.gf);
                if (c == 0) c = Competitions.Strength(y.team).CompareTo(Competitions.Strength(x.team));
                return c;
            });
            return list;
        }

        // ---------------------------------------------------------------- salvar
        public Dictionary<string, object> ToJson()
        {
            var ms = new List<object>();
            foreach (var m in matches)
                ms.Add(new Dictionary<string, object> { ["a"] = m.a, ["b"] = m.b, ["ga"] = m.ga, ["gb"] = m.gb, ["pa"] = m.pa, ["pb"] = m.pb, ["r"] = m.round, ["g"] = m.group, ["s"] = m.stage });
            var gs = new List<object>();
            foreach (var g in groups) gs.Add(new List<object>(g.ConvertAll(x => (object)x)));
            return new Dictionary<string, object>
            {
                ["id"] = id, ["user"] = user, ["seed"] = (long)seed, ["champion"] = champion,
                ["teams"] = new List<object>(teams.ConvertAll(x => (object)x)), ["groups"] = gs, ["matches"] = ms,
            };
        }

        public static CompRun FromJson(object o)
        {
            if (!(o is Dictionary<string, object>) || Competitions.Find(MiniJson.S(o, "id", "")) == null) return null;
            var r = new CompRun { id = MiniJson.S(o, "id"), user = MiniJson.I(o, "user"), seed = (uint)MiniJson.F(o, "seed", 1f), champion = MiniJson.I(o, "champion", -1) };
            foreach (var t in MiniJson.L(o, "teams") ?? new List<object>()) r.teams.Add(System.Convert.ToInt32(t));
            foreach (var g in MiniJson.L(o, "groups") ?? new List<object>())
            {
                var l = new List<int>();
                foreach (var t in (List<object>)g) l.Add(System.Convert.ToInt32(t));
                r.groups.Add(l);
            }
            foreach (var m in MiniJson.L(o, "matches") ?? new List<object>())
                r.matches.Add(new CompMatch
                {
                    a = MiniJson.I(m, "a"), b = MiniJson.I(m, "b"), ga = MiniJson.I(m, "ga", -1), gb = MiniJson.I(m, "gb", -1),
                    pa = MiniJson.I(m, "pa"), pb = MiniJson.I(m, "pb"), round = MiniJson.I(m, "r"), group = MiniJson.I(m, "g", -1), stage = MiniJson.S(m, "s", ""),
                });
            return r;
        }
    }
}
