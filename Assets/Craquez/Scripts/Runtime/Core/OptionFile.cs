using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Seções do option file que pertencem ao jogo (fora do banco de times): bolas, chuteiras e
    /// nomes dos locais. Exporta junto com os times (Teams.OptionDocument) e aplica o que o
    /// usuário editou em option_file.json.
    /// </summary>
    public static class OptionFile
    {
        static string Hex(Color c) => Teams.Hex(c);

        /// <summary>Seções extras: "bolas", "chuteiras" e "locais".</summary>
        public static Dictionary<string, object> Sections()
        {
            var balls = new List<object>();
            for (int i = 0; i < BallCatalog.All.Length; i++)
            {
                var b = BallCatalog.All[i];
                balls.Add(new Dictionary<string, object>
                {
                    ["indice"] = i, ["nome"] = b.name, ["modalidade"] = b.futsal ? "futsal" : "campo",
                    ["gomos"] = b.panels.ToString(), ["desenho"] = b.art.ToString(),
                    ["cor_fundo"] = Hex(b.a), ["cor_principal"] = Hex(b.b), ["cor_detalhe"] = Hex(b.c),
                });
            }
            var boots = new List<object>();
            for (int i = 0; i < BootPainter.Designs.Length; i++)
            {
                var d = BootPainter.Designs[i];
                boots.Add(new Dictionary<string, object>
                {
                    ["indice"] = i, ["nome"] = d.name, ["desenho"] = d.motif,
                    ["base"] = Hex(d.baseC), ["detalhe"] = Hex(d.accent), ["sola"] = Hex(d.sole), ["travas"] = Hex(d.studs),
                });
            }
            var stadiums = new List<object>();
            foreach (var m in VenueBuilder.StadiumModels) stadiums.Add(m.name);
            return new Dictionary<string, object>
            {
                ["bolas"] = balls,
                ["chuteiras"] = boots,
                ["locais"] = new Dictionary<string, object>
                {
                    ["estadios"] = stadiums,
                    ["futsal"] = new List<object>(VenueBuilder.FutsalNames),
                    ["society"] = new List<object>(VenueBuilder.SocietyNames),
                    ["rua"] = new List<object>(VenueBuilder.StreetNames),
                },
            };
        }

        /// <summary>Option file completo em JSON (times + seções do jogo).</summary>
        public static string Json() => MiniJson.Write(Teams.OptionDocument(Sections()));

        public static string Export() => Teams.ExportOptionTemplate(Sections());

        static readonly Dictionary<int, (string name, Color a, Color b, Color c)> _ballDefaults = new Dictionary<int, (string, Color, Color, Color)>();
        static readonly Dictionary<int, (string name, Color a, Color b, Color c, Color d)> _bootDefaults = new Dictionary<int, (string, Color, Color, Color, Color)>();
        static string[] _stadiumDefaults, _futsalDefaults, _societyDefaults, _streetDefaults;

        /// <summary>Aplica bolas, chuteiras e locais do option_file.json carregado (volta ao original o que saiu do arquivo).</summary>
        public static void Apply()
        {
            Teams.Get(0);   // garante o banco (e o option file) carregados
            // restaura os originais antes (recarregar o option file não acumula mudanças)
            if (_stadiumDefaults == null)
            {
                for (int i = 0; i < BallCatalog.All.Length; i++) { var b = BallCatalog.All[i]; _ballDefaults[i] = (b.name, b.a, b.b, b.c); }
                for (int i = 0; i < BootPainter.Designs.Length; i++) { var d = BootPainter.Designs[i]; _bootDefaults[i] = (d.name, d.baseC, d.accent, d.sole, d.studs); }
                _stadiumDefaults = new string[VenueBuilder.StadiumModels.Length];
                for (int i = 0; i < _stadiumDefaults.Length; i++) _stadiumDefaults[i] = VenueBuilder.StadiumModels[i].name;
                _futsalDefaults = (string[])VenueBuilder.FutsalNames.Clone();
                _societyDefaults = (string[])VenueBuilder.SocietyNames.Clone();
                _streetDefaults = (string[])VenueBuilder.StreetNames.Clone();
            }
            foreach (var kv in _ballDefaults) { var b = BallCatalog.All[kv.Key]; b.name = kv.Value.name; b.a = kv.Value.a; b.b = kv.Value.b; b.c = kv.Value.c; }
            foreach (var kv in _bootDefaults) { var d = BootPainter.Designs[kv.Key]; d.name = kv.Value.name; d.baseC = kv.Value.a; d.accent = kv.Value.b; d.sole = kv.Value.c; d.studs = kv.Value.d; }
            for (int i = 0; i < _stadiumDefaults.Length; i++) { var m = VenueBuilder.StadiumModels[i]; m.name = _stadiumDefaults[i]; VenueBuilder.StadiumModels[i] = m; }
            System.Array.Copy(_futsalDefaults, VenueBuilder.FutsalNames, _futsalDefaults.Length);
            System.Array.Copy(_societyDefaults, VenueBuilder.SocietyNames, _societyDefaults.Length);
            System.Array.Copy(_streetDefaults, VenueBuilder.StreetNames, _streetDefaults.Length);

            var doc = Teams.OptionDoc;
            if (doc == null) return;
            Color C(object o, string k, Color def) => MiniJson.S(o, k, "") is string s && s.Length >= 6 ? Teams.Col(s, def) : def;
            foreach (var o in MiniJson.L(doc, "bolas") ?? new List<object>())
            {
                int i = MiniJson.I(o, "indice", -1);
                if (i < 0 || i >= BallCatalog.All.Length) continue;
                var b = BallCatalog.All[i];
                if (MiniJson.S(o, "nome", "") is string n && n != "") b.name = n;
                b.a = C(o, "cor_fundo", b.a); b.b = C(o, "cor_principal", b.b); b.c = C(o, "cor_detalhe", b.c);
            }
            foreach (var o in MiniJson.L(doc, "chuteiras") ?? new List<object>())
            {
                int i = MiniJson.I(o, "indice", -1);
                if (i < 0 || i >= BootPainter.Designs.Length) continue;
                var d = BootPainter.Designs[i];
                if (MiniJson.S(o, "nome", "") is string n && n != "") d.name = n;
                d.baseC = C(o, "base", d.baseC); d.accent = C(o, "detalhe", d.accent); d.sole = C(o, "sola", d.sole); d.studs = C(o, "travas", d.studs);
            }
            var loc = MiniJson.D(doc, "locais");
            if (loc != null)
            {
                void Names(string key, string[] dst)
                {
                    var l = MiniJson.L(loc, key);
                    if (l == null) return;
                    for (int i = 0; i < l.Count && i < dst.Length; i++) if (l[i] is string s && s != "") dst[i] = s;
                }
                Names("futsal", VenueBuilder.FutsalNames);
                Names("society", VenueBuilder.SocietyNames);
                Names("rua", VenueBuilder.StreetNames);
                var st = MiniJson.L(loc, "estadios");
                if (st != null)
                    for (int i = 0; i < st.Count && i < VenueBuilder.StadiumModels.Length; i++)
                        if (st[i] is string s && s != "") { var m = VenueBuilder.StadiumModels[i]; m.name = s; VenueBuilder.StadiumModels[i] = m; }
            }
        }
    }
}
