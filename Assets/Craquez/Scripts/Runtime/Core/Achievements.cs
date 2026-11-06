using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Conquistas internas (bronze, prata, ouro, platina): estatísticas do jogador
    /// guardadas no aparelho (PlayerPrefs) e conferidas a cada lance e no fim da partida.
    /// </summary>
    public static class Achievements
    {
        public enum Tier { Bronze, Prata, Ouro, Platina }

        public class Def
        {
            public string id, name, desc, stat;
            public int target;
            public Tier tier;
        }

        static Def D(string id, Tier t, string name, string desc, string stat, int target) => new Def { id = id, tier = t, name = name, desc = desc, stat = stat, target = target };

        public static readonly Def[] All =
        {
            // ---- bronze: primeiros passos
            D("m1", Tier.Bronze, "Apito inicial", "Jogue sua primeira partida", "matches", 1),
            D("w1", Tier.Bronze, "Primeira vitória", "Vença uma partida", "wins", 1),
            D("g1", Tier.Bronze, "Primeiro gol", "Marque um gol", "goals", 1),
            D("fm1", Tier.Bronze, "Estreia na quadra", "Jogue uma partida de futsal", "matches_futsal", 1),
            D("sm1", Tier.Bronze, "Pelada de rua", "Jogue uma partida na rua", "matches_street", 1),
            D("cm1", Tier.Bronze, "Gramado sagrado", "Jogue uma partida de futebol de campo", "matches_field", 1),
            D("so1", Tier.Bronze, "Grama sintética", "Jogue uma partida de society", "matches_society", 1),
            D("n1", Tier.Bronze, "Vestindo a camisa", "Vença com uma seleção", "wins_national", 1),
            D("sk1", Tier.Bronze, "Primeiro drible", "Faça um drible (finta)", "skills", 1),
            D("tk1", Tier.Bronze, "Carrinho limpo", "Ganhe uma dividida", "tackles", 1),
            D("d1", Tier.Bronze, "Ninguém perde", "Empate uma partida", "draws", 1),
            D("wt1", Tier.Bronze, "Comentarista", "Assista a uma partida CPU x CPU", "watch", 1),
            D("p2", Tier.Bronze, "Clássico no sofá", "Jogue contra um amigo no mesmo aparelho", "local2p", 1),
            D("rn1", Tier.Bronze, "Pé na poça", "Jogue uma partida com chuva ou tempestade", "rain_match", 1),
            D("nt1", Tier.Bronze, "Sob os refletores", "Jogue uma partida à noite", "night_match", 1),
            D("sn1", Tier.Bronze, "Bola na neve", "Jogue uma partida com neve", "snow_match", 1),
            D("sv5", Tier.Bronze, "Paredão", "Seu goleiro faz 5 defesas", "saves", 5),
            D("sh10", Tier.Bronze, "Pé calibrado", "Finalize 10 vezes", "shots", 10),
            D("ps50", Tier.Bronze, "Toque de bola", "Acerte 50 passes", "passes", 50),
            // ---- prata
            D("m10", Tier.Prata, "Rodagem", "Jogue 10 partidas", "matches", 10),
            D("w10", Tier.Prata, "Embalado", "Vença 10 partidas", "wins", 10),
            D("g25", Tier.Prata, "Artilheiro", "Marque 25 gols", "goals", 25),
            D("ht1", Tier.Prata, "Hat-trick", "Um mesmo jogador marca 3 gols numa partida", "hattricks", 1),
            D("mg4", Tier.Prata, "Goleada", "Vença por 4 gols de diferença", "best_margin", 4),
            D("cs5", Tier.Prata, "Muralha", "Termine 5 partidas sem sofrer gol", "cleansheets", 5),
            D("fw5", Tier.Prata, "Rei da quadra", "Vença 5 partidas de futsal", "wins_futsal", 5),
            D("sw5", Tier.Prata, "Dono da rua", "Vença 5 partidas na rua", "wins_street", 5),
            D("cw5", Tier.Prata, "Dono do campo", "Vença 5 partidas de futebol de campo", "wins_field", 5),
            D("sow5", Tier.Prata, "Rei do society", "Vença 5 partidas de society", "wins_society", 5),
            D("n5", Tier.Prata, "Convocado", "Vença 5 partidas com seleções", "wins_national", 5),
            D("sk25", Tier.Prata, "Malabarista", "Faça 25 dribles", "skills", 25),
            D("tk50", Tier.Prata, "Cão de guarda", "Ganhe 50 divididas", "tackles", 50),
            D("cb1", Tier.Prata, "Virada!", "Vença uma partida em que esteve perdendo", "comebacks", 1),
            D("hd1", Tier.Prata, "Craque de verdade", "Vença no nível Craque ou Lenda", "wins_hard", 1),
            D("st3", Tier.Prata, "Sequência", "Vença 3 partidas seguidas", "best_streak", 3),
            D("ps500", Tier.Prata, "Tiki-taka", "Acerte 500 passes", "passes", 500),
            D("sh100", Tier.Prata, "Chutador", "Finalize 100 vezes", "shots", 100),
            D("gf20", Tier.Prata, "Pivô goleador", "Marque 20 gols no futsal", "goals_futsal", 20),
            D("gs20", Tier.Prata, "Gol de canela", "Marque 20 gols na rua", "goals_street", 20),
            D("vf5", Tier.Prata, "Turnê dos ginásios", "Jogue em 5 quadras de futsal diferentes", "venues_futsal", 5),
            D("vs5", Tier.Prata, "Turnê das ruas", "Jogue em 5 ruas diferentes", "venues_street", 5),
            D("bl5", Tier.Prata, "Colecionador", "Jogue com 5 bolas diferentes", "balls", 5),
            D("sv50", Tier.Prata, "São goleiro", "Seu goleiro faz 50 defesas", "saves", 50),
            // ---- ouro
            D("m50", Tier.Ouro, "Veterano", "Jogue 50 partidas", "matches", 50),
            D("w50", Tier.Ouro, "Campeão", "Vença 50 partidas", "wins", 50),
            D("g100", Tier.Ouro, "Centenário", "Marque 100 gols", "goals", 100),
            D("ht5", Tier.Ouro, "Especialista em trincas", "Faça 5 hat-tricks", "hattricks", 5),
            D("mg6", Tier.Ouro, "Atropelo", "Vença por 6 gols de diferença", "best_margin", 6),
            D("cs20", Tier.Ouro, "Cofre", "Termine 20 partidas sem sofrer gol", "cleansheets", 20),
            D("lg1", Tier.Ouro, "Lendário", "Vença no nível Lenda", "wins_legend", 1),
            D("st5", Tier.Ouro, "Invicto", "Vença 5 partidas seguidas", "best_streak", 5),
            D("sk100", Tier.Ouro, "Fenômeno", "Faça 100 dribles", "skills", 100),
            D("cb5", Tier.Ouro, "Rei da virada", "Vença 5 partidas de virada", "comebacks", 5),
            D("fw25", Tier.Ouro, "Lenda do salão", "Vença 25 partidas de futsal", "wins_futsal", 25),
            D("sw25", Tier.Ouro, "Lenda da várzea", "Vença 25 partidas na rua", "wins_street", 25),
            D("n20", Tier.Ouro, "Seleção do mundo", "Vença 20 partidas com seleções", "wins_national", 20),
            D("bg6", Tier.Ouro, "Chuva de gols", "Marque 6 gols numa partida", "best_goals_match", 6),
            D("hd10", Tier.Ouro, "Nível elite", "Vença 10 partidas no Craque ou Lenda", "wins_hard", 10),
            D("tk250", Tier.Ouro, "Xerife", "Ganhe 250 divididas", "tackles", 250),
            D("bl15", Tier.Ouro, "Museu da bola", "Jogue com 15 bolas diferentes", "balls", 15),
            D("vf9", Tier.Ouro, "Todos os ginásios", "Jogue em todas as quadras de futsal", "venues_futsal", 9),
            // ---- platina
            D("m100", Tier.Platina, "Vida de boleiro", "Jogue 100 partidas", "matches", 100),
            D("w100", Tier.Platina, "Imortal", "Vença 100 partidas", "wins", 100),
            D("g300", Tier.Platina, "Máquina de gols", "Marque 300 gols", "goals", 300),
            D("st10", Tier.Platina, "Dez seguidas", "Vença 10 partidas seguidas", "best_streak", 10),
            D("lg10", Tier.Platina, "Lenda das lendas", "Vença 10 partidas no nível Lenda", "wins_legend", 10),
            D("cs50", Tier.Platina, "Fortaleza", "Termine 50 partidas sem sofrer gol", "cleansheets", 50),
            D("all50", Tier.Platina, "Craquez completo", "Desbloqueie 50 conquistas", "unlocked", 50),
        };

        // ---------------------------------------------------------------- dados salvos
        static Dictionary<string, int> _stats;
        static HashSet<string> _done;
        static Dictionary<string, HashSet<string>> _sets;

        static void Load()
        {
            if (_stats != null) return;
            _stats = new Dictionary<string, int>();
            _done = new HashSet<string>();
            _sets = new Dictionary<string, HashSet<string>>();
            foreach (var kv in (PlayerPrefs.GetString("ach_stats", "")).Split(';'))
            {
                int eq = kv.IndexOf('=');
                if (eq > 0 && int.TryParse(kv.Substring(eq + 1), out int v)) _stats[kv.Substring(0, eq)] = v;
            }
            foreach (var id in PlayerPrefs.GetString("ach_done", "").Split(',')) if (id.Length > 0) _done.Add(id);
            foreach (var part in PlayerPrefs.GetString("ach_sets", "").Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                var hs = new HashSet<string>(part.Substring(eq + 1).Split(','));
                hs.Remove("");
                _sets[part.Substring(0, eq)] = hs;
            }
        }

        static void Save()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in _stats) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
            PlayerPrefs.SetString("ach_stats", sb.ToString());
            PlayerPrefs.SetString("ach_done", string.Join(",", _done));
            var ss = new System.Text.StringBuilder();
            foreach (var kv in _sets) ss.Append(kv.Key).Append('=').Append(string.Join(",", kv.Value)).Append(';');
            PlayerPrefs.SetString("ach_sets", ss.ToString());
            PlayerPrefs.Save();
        }

        public static int Stat(string k) { Load(); return _stats.TryGetValue(k, out var v) ? v : 0; }
        public static bool Done(Def d) { Load(); return _done.Contains(d.id); }
        public static int DoneCount { get { Load(); return _done.Count; } }
        public static int Progress(Def d) => Mathf.Min(d.target, d.stat == "unlocked" ? DoneCount : Stat(d.stat));

        public static void Add(string k, int n = 1) { Load(); _stats[k] = Stat(k) + n; }
        public static void Max(string k, int v) { Load(); if (v > Stat(k)) _stats[k] = v; }
        public static void Set(string k, int v) { Load(); _stats[k] = v; }

        /// <summary>Conjunto (quadras, ruas, bolas usadas): a estatística é o tamanho do conjunto.</summary>
        public static void AddToSet(string k, string value)
        {
            Load();
            if (!_sets.TryGetValue(k, out var hs)) _sets[k] = hs = new HashSet<string>();
            hs.Add(value);
            _stats[k] = hs.Count;
        }

        /// <summary>Confere tudo, grava e devolve as conquistas desbloqueadas agora.</summary>
        public static List<Def> Check()
        {
            Load();
            var fresh = new List<Def>();
            for (int pass = 0; pass < 2; pass++)   // "Craquez completo" depende das outras
                foreach (var d in All)
                    if (!_done.Contains(d.id) && Progress(d) >= d.target)
                    {
                        _done.Add(d.id);
                        fresh.Add(d);
                    }
            Save();
            return fresh;
        }

        public static Color TierColor(Tier t) => t == Tier.Bronze ? new Color(0.8f, 0.5f, 0.25f) : t == Tier.Prata ? new Color(0.78f, 0.82f, 0.88f) : t == Tier.Ouro ? new Color(0.98f, 0.8f, 0.15f) : new Color(0.55f, 0.9f, 1f);
    }

    /// <summary>Acompanha uma partida e alimenta as conquistas.</summary>
    public class MatchTracker
    {
        readonly MatchConfig _cfg;
        readonly bool[] _human = new bool[2];
        readonly Dictionary<int, int> _scorers = new Dictionary<int, int>();
        bool _wasBehind;
        int _team = -1;   // time do jogador (o primeiro humano)
        public System.Action<Achievements.Def> onUnlock;

        public MatchTracker(MatchConfig cfg)
        {
            _cfg = cfg;
            _human[0] = cfg.sideA != Side.Cpu;
            _human[1] = cfg.sideB != Side.Cpu;
            _team = _human[0] ? 0 : _human[1] ? 1 : -1;
        }

        bool Mine(int team) => team >= 0 && team < 2 && _human[team];

        public void OnEvent(SimEvent e, MatchSim sim)
        {
            if (_team < 0) return;
            bool changed = true;
            switch (e.type)
            {
                case "goal":
                    if (Mine(e.team) && e.kind != "own")
                    {
                        Achievements.Add("goals");
                        Achievements.Add("goals_" + ModeKey());
                        if (e.id >= 0) _scorers[e.id] = (_scorers.TryGetValue(e.id, out var c) ? c : 0) + 1;
                    }
                    if (sim.score[_team] < sim.score[1 - _team]) _wasBehind = true;
                    break;
                case "skill": if (Mine(e.team)) Achievements.Add("skills"); else changed = false; break;
                case "tackle": if (Mine(e.team) && e.ok) Achievements.Add("tackles"); else changed = false; break;
                case "steal": if (Mine(e.team)) Achievements.Add("tackles"); else changed = false; break;
                case "save": if (Mine(e.team) && e.ok) Achievements.Add("saves"); else changed = false; break;
                case "kick":
                    if (!Mine(e.team)) { changed = false; break; }
                    if (e.kind == "shot" || e.kind == "chip") Achievements.Add("shots");
                    else Achievements.Add("passes");
                    break;
                default: changed = false; break;
            }
            if (changed) Notify();
        }

        string ModeKey() => _cfg.mode == GameModes.Futsal ? "futsal" : _cfg.mode == GameModes.Street ? "street" : _cfg.mode == GameModes.Society ? "society" : "field";

        void Notify()
        {
            foreach (var d in Achievements.Check()) onUnlock?.Invoke(d);
        }

        /// <summary>Fim de jogo: partidas, vitórias, sequências, placares e locais.</summary>
        public List<Achievements.Def> Finish(MatchSim sim, string weatherKind)
        {
            if (_team < 0)
            {
                Achievements.Add("watch");
                return Achievements.Check();
            }
            string mk = ModeKey();
            Achievements.Add("matches");
            Achievements.Add("matches_" + mk);
            if (_human[0] && _human[1]) Achievements.Add("local2p");
            if (weatherKind == "chuva" || weatherKind == "tempestade") Achievements.Add("rain_match");
            if (weatherKind == "neve") Achievements.Add("snow_match");
            if (_cfg.timeOfDay == "noite") Achievements.Add("night_match");
            if (_cfg.mode == GameModes.Futsal) Achievements.AddToSet("venues_futsal", _cfg.venue.ToString());
            if (_cfg.mode == GameModes.Street) Achievements.AddToSet("venues_street", _cfg.venue.ToString());
            Achievements.AddToSet("balls", _cfg.ball.ToString());
            int mine = sim.score[_team], theirs = sim.score[1 - _team];
            Achievements.Max("best_goals_match", mine);
            foreach (var kv in _scorers) if (kv.Value >= 3) Achievements.Add("hattricks");
            if (theirs == 0) Achievements.Add("cleansheets");
            bool versusCpu = !(_human[0] && _human[1]);
            if (mine > theirs && versusCpu)
            {
                Achievements.Add("wins");
                Achievements.Add("wins_" + mk);
                if (_cfg.national) Achievements.Add("wins_national");
                if (_cfg.difficulty >= 0.74f) Achievements.Add("wins_hard");
                if (_cfg.difficulty >= 0.94f) Achievements.Add("wins_legend");
                if (_wasBehind) Achievements.Add("comebacks");
                Achievements.Max("best_margin", mine - theirs);
                Achievements.Add("streak");
                Achievements.Max("best_streak", Achievements.Stat("streak"));
            }
            else if (versusCpu)
            {
                Achievements.Set("streak", 0);
                if (mine == theirs) Achievements.Add("draws");
            }
            else if (mine == theirs) Achievements.Add("draws");
            return Achievements.Check();
        }
    }
}
