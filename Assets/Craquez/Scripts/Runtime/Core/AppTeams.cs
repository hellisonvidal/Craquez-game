using System.Collections.Generic;
using Craquez.Sim;
using Craquez.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Craquez
{
    /// <summary>
    /// Escolha de time em níveis: Clubes ou Seleções › país (clubes) ou continente (seleções) › liga (quando
    /// o país tem mais de uma) › time. Antes era uma lista só com todas as ligas e grupos de seleções.
    /// </summary>
    public partial class App
    {
        static readonly Dictionary<string, string> CountryNames = new Dictionary<string, string>
        {
            { "BR", "Brasil" }, { "AR", "Argentina" }, { "UY", "Uruguai" }, { "PE", "Peru" }, { "CL", "Chile" }, { "EC", "Equador" },
            { "CO", "Colômbia" }, { "PY", "Paraguai" }, { "BO", "Bolívia" }, { "VE", "Venezuela" }, { "MX", "México" }, { "US", "Estados Unidos" },
            { "IT", "Itália" }, { "EN", "Inglaterra" }, { "GB", "Inglaterra" }, { "ES", "Espanha" }, { "FR", "França" }, { "DE", "Alemanha" },
            { "NL", "Holanda" }, { "PT", "Portugal" }, { "RU", "Rússia" }, { "UA", "Ucrânia" }, { "BE", "Bélgica" }, { "TR", "Turquia" },
            { "SA", "Arábia Saudita" }, { "JP", "Japão" }, { "KR", "Coreia do Sul" }, { "CN", "China" },
        };

        /// <summary>Times da modalidade organizados: clubes por país e liga; seleções pelo ranking e por continente.</summary>
        public class TeamTree
        {
            public readonly List<(string name, List<(string league, List<int> ids)> leagues)> countries = new List<(string, List<(string, List<int>)>)>();
            public readonly List<(string name, List<int> ids)> nations = new List<(string, List<int>)>();
        }

        public static TeamTree BuildTeamTree(string mode)
        {
            var tree = new TeamTree();
            var byCountry = new Dictionary<string, List<(string, List<int>)>>();
            var order = new List<string>();
            foreach (var l in LeaguesFor(mode, false))
            {
                if (l.IsNational && mode != GameModes.Street) continue;
                var ids = new List<int>();
                for (int i = 0; i < l.count; i++) ids.Add(l.first + i);
                Teams.SortByName(ids);
                string cn = !string.IsNullOrEmpty(l.country) && CountryNames.TryGetValue(l.country.ToUpperInvariant(), out var n) ? n
                    : !string.IsNullOrEmpty(l.country) ? l.country : l.name;
                if (!byCountry.TryGetValue(cn, out var list)) { list = new List<(string, List<int>)>(); byCountry[cn] = list; order.Add(cn); }
                list.Add((l.name, ids));
            }
            // Brasil primeiro; depois em ordem alfabética
            order.Sort((a, b) => a == "Brasil" ? -1 : b == "Brasil" ? 1 : string.Compare(a, b, System.StringComparison.CurrentCulture));
            foreach (var c in order) tree.countries.Add((c, byCountry[c]));
            if (mode != GameModes.Street)
            {
                var all = new List<int>();
                foreach (var l in LeaguesFor(mode, true)) if (l.IsNational) for (int i = 0; i < l.count; i++) all.Add(l.first + i);
                all.Sort((x, y) => Teams.Get(x).rank.CompareTo(Teams.Get(y).rank));
                if (all.Count > 0)
                {
                    tree.nations.Add(("Todas (ranking)", all));
                    foreach (var c in Teams.Continents)
                    {
                        var ids = all.FindAll(i => Teams.Continent(Teams.Get(i).iso) == c);
                        if (ids.Count > 0) tree.nations.Add((c, ids));
                    }
                }
            }
            return tree;
        }

        /// <summary>
        /// Monta os seletores (região: país dos clubes ou "Seleções • continente" › liga, se o país tiver mais
        /// de uma › time) dentro de <paramref name="list"/> e chama <paramref name="onPick"/> a cada troca de
        /// time. Devolve o primeiro seletor (para o foco do controle).
        /// </summary>
        public static Selectable TeamPicker(RectTransform list, string mode, int current, System.Action<int> onPick, float rowH = 70f, int preferSecond = 0)
        {
            var tree = BuildTeamTree(mode);
            int nC = tree.countries.Count;
            var regions = new List<string>();
            foreach (var c in tree.countries) regions.Add(c.name);
            foreach (var n in tree.nations) regions.Add(n.name == "Todas (ranking)" ? "Seleções  •  Ranking" : "Seleções  •  " + n.name);
            int g = 0, lg = 0;
            bool found = false;
            for (int c = 0; c < nC && !found; c++)
                for (int k = 0; k < tree.countries[c].leagues.Count && !found; k++)
                    if (tree.countries[c].leagues[k].ids.Contains(current)) { g = c; lg = k; found = true; }
            if (!found)
                for (int c = tree.nations.Count - 1; c >= 0 && !found; c--)   // o continente antes do ranking geral
                    if (tree.nations[c].ids.Contains(current)) { g = nC + c; found = true; }

            Cycler regionC = null, leagueC = null, teamC = null;
            List<int> Ids() => g < nC ? tree.countries[g].leagues[lg].ids : tree.nations[g - nC].ids;
            List<string> Names(List<int> ids) { var n = new List<string>(); foreach (var id in ids) n.Add(Teams.Get(id).name); return n; }
            List<string> Leagues() { var n = new List<string>(); if (g < nC) foreach (var l in tree.countries[g].leagues) n.Add(l.league); return n; }
            void Pick(int team) { current = team; onPick?.Invoke(team); }
            void SyncTeams()
            {
                var ids = Ids();
                if (!ids.Contains(current)) Pick(ids[Mathf.Min(ids.Count - 1, preferSecond)]);
                teamC.SetOptions(Names(ids), Mathf.Max(0, ids.IndexOf(current)));
            }
            void SyncLeagues()
            {
                var ln = Leagues();
                leagueC.gameObject.SetActive(ln.Count > 1);
                if (ln.Count > 0) leagueC.SetOptions(ln, Mathf.Clamp(lg, 0, ln.Count - 1));
            }

            regionC = UiKit.Cycler(list, "País / seleções", regions, g, i => { g = i; lg = 0; SyncLeagues(); SyncTeams(); });
            UiKit.Size(regionC, rowH, rowH);
            regionC.gameObject.SetActive(regions.Count > 1);
            var l0 = Leagues();
            leagueC = UiKit.Cycler(list, "Liga", l0.Count > 0 ? l0 : new List<string> { "" }, lg, i => { lg = i; SyncTeams(); });
            UiKit.Size(leagueC, rowH, rowH);
            leagueC.gameObject.SetActive(l0.Count > 1);
            teamC = UiKit.Cycler(list, "Time", Names(Ids()), Mathf.Max(0, Ids().IndexOf(current)), i => Pick(Ids()[i]));
            UiKit.Size(teamC, rowH, rowH);
            if (!Ids().Contains(current)) Pick(Ids()[0]);
            return regions.Count > 1 ? (Selectable)regionC : teamC;
        }
    }
}
