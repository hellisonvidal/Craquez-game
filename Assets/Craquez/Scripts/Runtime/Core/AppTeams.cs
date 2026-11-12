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
            /// <summary>Código do país (para o mapa) e continente de cada país de <see cref="countries"/> (null: sem país, ex. rua).</summary>
            public readonly List<(string iso, string continent)> where = new List<(string, string)>();
            public readonly List<(string name, List<int> ids)> nations = new List<(string, List<int>)>();
        }

        public static TeamTree BuildTeamTree(string mode)
        {
            var tree = new TeamTree();
            var byCountry = new Dictionary<string, List<(string, List<int>)>>();
            var isoOf = new Dictionary<string, string>();
            var order = new List<string>();
            foreach (var l in LeaguesFor(mode, false))
            {
                if (l.IsNational && mode != GameModes.Street) continue;
                var ids = new List<int>();
                for (int i = 0; i < l.count; i++) ids.Add(l.first + i);
                Teams.SortByName(ids);
                string cn = !string.IsNullOrEmpty(l.country) && CountryNames.TryGetValue(l.country.ToUpperInvariant(), out var n) ? n
                    : !string.IsNullOrEmpty(l.country) ? l.country : l.name;
                if (!byCountry.TryGetValue(cn, out var list))
                {
                    list = new List<(string, List<int>)>(); byCountry[cn] = list; order.Add(cn);
                    isoOf[cn] = string.IsNullOrEmpty(l.country) ? null : l.country.ToUpperInvariant();
                }
                list.Add((l.name, ids));
            }
            // Brasil primeiro; depois em ordem alfabética
            order.Sort((a, b) => a == "Brasil" ? -1 : b == "Brasil" ? 1 : string.Compare(a, b, System.StringComparison.CurrentCulture));
            foreach (var c in order)
            {
                tree.countries.Add((c, byCountry[c]));
                string iso = isoOf[c];
                tree.where.Add((iso == "EN" ? "GB" : iso, iso == null ? null : Teams.Continent(iso)));
            }
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

        const string AllContinents = "Todos os continentes";

        /// <summary>Imagem do mapa (Resources/UI/Mapas): o país em destaque no continente, o continente ou o mundo.</summary>
        static Texture2D MapFor(string continent, string iso)
        {
            Texture2D t = null;
            if (!string.IsNullOrEmpty(iso)) t = Resources.Load<Texture2D>("UI/Mapas/pais_" + iso);
            if (t == null && !string.IsNullOrEmpty(continent))
            {
                string slug = continent switch
                {
                    "América do Sul" => "america-do-sul", "Europa" => "europa", "África" => "africa",
                    "América do Norte e Central" => "america-do-norte", "Ásia e Oceania" => "asia-oceania", _ => null,
                };
                if (slug != null) t = Resources.Load<Texture2D>("UI/Mapas/" + slug);
            }
            return t != null ? t : Resources.Load<Texture2D>("UI/Mapas/mundo");
        }

        /// <summary>
        /// Monta os seletores (continente › região: país dos clubes ou "Seleções • continente" › liga, se o país
        /// tiver mais de uma › time) dentro de <paramref name="list"/>, com o mapa do continente ao lado (o país
        /// escolhido em destaque), e chama <paramref name="onPick"/> a cada troca de time. Devolve o primeiro
        /// seletor (para o foco do controle).
        /// </summary>
        public static Selectable TeamPicker(RectTransform list, string mode, int current, System.Action<int> onPick, float rowH = 70f, int preferSecond = 0)
        {
            var tree = BuildTeamTree(mode);
            int nC = tree.countries.Count;
            // regiões: (país i >= 0) ou (grupo de seleções -1 - k)
            var allRegions = new List<int>();
            for (int c = 0; c < nC; c++) allRegions.Add(c);
            for (int k = 0; k < tree.nations.Count; k++) allRegions.Add(-1 - k);
            string RegionName(int r) => r >= 0 ? tree.countries[r].name
                : tree.nations[-1 - r].name == "Todas (ranking)" ? "Seleções  •  Ranking" : "Seleções  •  " + tree.nations[-1 - r].name;
            string ContinentOf(int r) => r >= 0 ? tree.where[r].continent : tree.nations[-1 - r].name == "Todas (ranking)" ? null : tree.nations[-1 - r].name;
            // continentes com algum time (na ordem das confederações)
            var conts = new List<string> { AllContinents };
            foreach (var c in Teams.Continents)
                if (allRegions.Exists(r => ContinentOf(r) == c)) conts.Add(c);

            int region = allRegions.Count > 0 ? allRegions[0] : 0, lg = 0;
            bool found = false;
            for (int c = 0; c < nC && !found; c++)
                for (int k = 0; k < tree.countries[c].leagues.Count && !found; k++)
                    if (tree.countries[c].leagues[k].ids.Contains(current)) { region = c; lg = k; found = true; }
            if (!found)
                for (int k = tree.nations.Count - 1; k >= 0 && !found; k--)   // o continente antes do ranking geral
                    if (tree.nations[k].ids.Contains(current)) { region = -1 - k; found = true; }
            int ci = Mathf.Max(0, conts.IndexOf(ContinentOf(region) ?? AllContinents));
            List<int> Regions() => conts[ci] == AllContinents ? allRegions : allRegions.FindAll(r => ContinentOf(r) == conts[ci]);
            List<string> RegionNames() { var n = new List<string>(); foreach (var r in Regions()) n.Add(RegionName(r)); return n; }

            Cycler regionC = null, leagueC = null, teamC = null;
            RawImage map = null;
            List<int> Ids() => region >= 0 ? tree.countries[region].leagues[lg].ids : tree.nations[-1 - region].ids;
            List<string> Names(List<int> ids) { var n = new List<string>(); foreach (var id in ids) n.Add(Teams.Get(id).name); return n; }
            List<string> Leagues() { var n = new List<string>(); if (region >= 0) foreach (var l in tree.countries[region].leagues) n.Add(l.league); return n; }
            void Pick(int team) { current = team; onPick?.Invoke(team); }

            // seletores à esquerda, mapa à direita
            var box = UiKit.Rect(list, "escolha do time");
            var boxLE = UiKit.Size(box, rowH, rowH);
            var h = UiKit.HList(box, 14);
            h.childForceExpandWidth = false;
            var left = UiKit.Rect(box, "seletores");
            UiKit.VList(left, 8, 0, false);
            UiKit.Size(left, -1, -1, 1f);
            float mapW = rowH * 4.3f;
            var frame = UiKit.Image(box, new Color(0.02f, 0.04f, 0.12f, 0.55f), Theme.Round, "mapa");
            frame.raycastTarget = false;
            UiKit.Size(frame, -1, -1, 0f, mapW);
            var mapRt = UiKit.Rect(frame.transform, "imagem").Place(0, 0, 1, 1, 6, 6, 6, 6);
            map = mapRt.gameObject.AddComponent<RawImage>();
            map.raycastTarget = false;
            var fit = mapRt.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1.6f;

            void Layout()
            {
                int rows = 0;
                foreach (Transform c in left) if (c.gameObject.activeSelf) rows++;
                float hh = rows * rowH + Mathf.Max(0, rows - 1) * 8f;
                boxLE.minHeight = boxLE.preferredHeight = hh;
            }
            void SyncMap()
            {
                string iso = region >= 0 ? tree.where[region].iso : null;
                string cont = region >= 0 ? tree.where[region].continent : ContinentOf(region);
                if (region < 0 && cont == null && conts[ci] != AllContinents) cont = conts[ci];
                var tex = MapFor(cont, iso);
                map.texture = tex;
                frame.gameObject.SetActive(tex != null);
            }
            void SyncTeams()
            {
                var ids = Ids();
                if (!ids.Contains(current)) Pick(ids[Mathf.Min(ids.Count - 1, preferSecond)]);
                teamC.SetOptions(Names(ids), Mathf.Max(0, ids.IndexOf(current)));
                SyncMap();
            }
            void SyncLeagues()
            {
                var ln = Leagues();
                leagueC.gameObject.SetActive(ln.Count > 1);
                if (ln.Count > 0) leagueC.SetOptions(ln, Mathf.Clamp(lg, 0, ln.Count - 1));
                Layout();
            }

            var contC = UiKit.Cycler(left, "Continente", conts, ci, i =>
            {
                ci = i;
                var rs = Regions();
                if (!rs.Contains(region)) { region = rs[0]; lg = 0; }
                regionC.SetOptions(RegionNames(), Mathf.Max(0, rs.IndexOf(region)));
                regionC.gameObject.SetActive(rs.Count > 1);
                SyncLeagues(); SyncTeams();
            });
            UiKit.Size(contC, rowH, rowH);
            contC.gameObject.SetActive(conts.Count > 2);
            regionC = UiKit.Cycler(left, "País / seleções", RegionNames(), Mathf.Max(0, Regions().IndexOf(region)), i => { region = Regions()[i]; lg = 0; SyncLeagues(); SyncTeams(); });
            UiKit.Size(regionC, rowH, rowH);
            regionC.gameObject.SetActive(Regions().Count > 1);
            var l0 = Leagues();
            leagueC = UiKit.Cycler(left, "Liga", l0.Count > 0 ? l0 : new List<string> { "" }, lg, i => { lg = i; SyncTeams(); });
            UiKit.Size(leagueC, rowH, rowH);
            leagueC.gameObject.SetActive(l0.Count > 1);
            teamC = UiKit.Cycler(left, "Time", Names(Ids()), Mathf.Max(0, Ids().IndexOf(current)), i => Pick(Ids()[i]));
            UiKit.Size(teamC, rowH, rowH);
            if (!Ids().Contains(current)) Pick(Ids()[0]);
            Layout();
            SyncMap();
            return conts.Count > 2 ? contC : Regions().Count > 1 ? (Selectable)regionC : teamC;
        }
    }
}
