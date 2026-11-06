using System.Collections.Generic;
using UnityEngine;

namespace Craquez.Sim
{
    /// <summary>Regras e dimensões de cada modo (metros / segundos).</summary>
    public class ModeDef
    {
        public string id;
        public float length, width, goalWidth, goalHeight, goalDepth;
        public bool keeper, walls, offside;
        public float wallBounce, boxDepth, boxHalf, penaltyDist, centerR, restartDist;
        public float rollDecel, groundDrag, airDrag, bounce, runMargin;
        public string surface;
        public Vector2[] formation;

        public ModeDef Clone() => (ModeDef)MemberwiseClone();
    }

    public static class GameModes
    {
        public const string Field = "field";
        public const string Futsal = "futsal";
        public const string Street = "street";
        public const string Society = "society";
        public static readonly string[] All = { Field, Futsal, Society, Street };

        static Vector2 V(float x, float y) => new Vector2(x, y);

        public static string DisplayName(string id) => id == Futsal ? "Futsal" : id == Street ? "Rua" : id == Society ? "Society" : "Campo";

        /// <summary>Jogadores por time nas ruas (3x3, 4x4, 5x5).</summary>
        public static readonly int[] StreetSizes = { 3, 4, 5 };

        /// <summary>Formação padrão da rua para o número de jogadores.</summary>
        public static string StreetDefault(int players) => players >= 5 ? "2-1-2" : players == 4 ? "2-2" : "1-2";

        /// <summary>Formações: x de -1 (próprio gol) a 0 (meio); o primeiro é o goleiro (campo/futsal).</summary>
        public static Dictionary<string, Vector2[]> Formations(string id)
        {
            if (id == Futsal)
                return new Dictionary<string, Vector2[]>
                {
                    ["2-1-1"] = new[] { V(-0.95f, 0), V(-0.62f, -0.5f), V(-0.62f, 0.5f), V(-0.35f, 0), V(-0.12f, 0) },
                    ["1-2-1"] = new[] { V(-0.95f, 0), V(-0.65f, 0), V(-0.4f, -0.55f), V(-0.4f, 0.55f), V(-0.12f, 0) },
                    ["2-2"] = new[] { V(-0.95f, 0), V(-0.62f, -0.5f), V(-0.62f, 0.5f), V(-0.2f, -0.4f), V(-0.2f, 0.4f) },
                    ["3-1"] = new[] { V(-0.95f, 0), V(-0.6f, -0.6f), V(-0.65f, 0), V(-0.6f, 0.6f), V(-0.15f, 0) },
                };
            if (id == Street)
                return new Dictionary<string, Vector2[]>
                {
                    // 3x3
                    ["1-2"] = new[] { V(-0.7f, 0), V(-0.3f, -0.5f), V(-0.3f, 0.5f) },
                    ["2-1"] = new[] { V(-0.6f, -0.45f), V(-0.6f, 0.45f), V(-0.25f, 0) },
                    ["1-1-1"] = new[] { V(-0.7f, 0), V(-0.45f, 0), V(-0.2f, 0) },
                    // 4x4
                    ["2-2"] = new[] { V(-0.68f, -0.45f), V(-0.68f, 0.45f), V(-0.25f, -0.45f), V(-0.25f, 0.45f) },
                    ["1-2-1"] = new[] { V(-0.72f, 0), V(-0.45f, -0.55f), V(-0.45f, 0.55f), V(-0.18f, 0) },
                    ["3-1"] = new[] { V(-0.68f, -0.55f), V(-0.72f, 0), V(-0.68f, 0.55f), V(-0.2f, 0) },
                    // 5x5
                    ["2-1-2"] = new[] { V(-0.7f, -0.45f), V(-0.7f, 0.45f), V(-0.45f, 0), V(-0.2f, -0.45f), V(-0.2f, 0.45f) },
                    ["2-2-1"] = new[] { V(-0.7f, -0.45f), V(-0.7f, 0.45f), V(-0.42f, -0.45f), V(-0.42f, 0.45f), V(-0.15f, 0) },
                    ["1-3-1"] = new[] { V(-0.74f, 0), V(-0.45f, -0.6f), V(-0.48f, 0), V(-0.45f, 0.6f), V(-0.15f, 0) },
                };
            if (id == Society)
                return new Dictionary<string, Vector2[]>
                {
                    ["3-2-1"] = new[] { V(-0.95f, 0), V(-0.68f, -0.55f), V(-0.72f, 0), V(-0.68f, 0.55f), V(-0.4f, -0.35f), V(-0.4f, 0.35f), V(-0.12f, 0) },
                    ["2-3-1"] = new[] { V(-0.95f, 0), V(-0.7f, -0.35f), V(-0.7f, 0.35f), V(-0.42f, -0.62f), V(-0.45f, 0), V(-0.42f, 0.62f), V(-0.12f, 0) },
                    ["3-1-2"] = new[] { V(-0.95f, 0), V(-0.68f, -0.55f), V(-0.72f, 0), V(-0.68f, 0.55f), V(-0.45f, 0), V(-0.15f, -0.35f), V(-0.15f, 0.35f) },
                    ["2-2-2"] = new[] { V(-0.95f, 0), V(-0.7f, -0.35f), V(-0.7f, 0.35f), V(-0.42f, -0.45f), V(-0.42f, 0.45f), V(-0.15f, -0.3f), V(-0.15f, 0.3f) },
                };
            var gk = V(-0.96f, 0);
            Vector2[] back4 = { V(-0.72f, -0.62f), V(-0.76f, -0.22f), V(-0.76f, 0.22f), V(-0.72f, 0.62f) };
            Vector2[] Cat(params Vector2[][] parts)
            {
                var l = new List<Vector2>();
                foreach (var p in parts) l.AddRange(p);
                return l.ToArray();
            }
            return new Dictionary<string, Vector2[]>
            {
                ["4-4-2"] = Cat(new[] { gk }, back4, new[] { V(-0.42f, -0.66f), V(-0.46f, -0.2f), V(-0.46f, 0.2f), V(-0.42f, 0.66f), V(-0.12f, -0.2f), V(-0.14f, 0.22f) }),
                ["4-3-3"] = Cat(new[] { gk }, back4, new[] { V(-0.48f, -0.35f), V(-0.52f, 0), V(-0.48f, 0.35f), V(-0.16f, -0.6f), V(-0.1f, 0), V(-0.16f, 0.6f) }),
                ["4-2-3-1"] = Cat(new[] { gk }, back4, new[] { V(-0.55f, -0.2f), V(-0.55f, 0.2f), V(-0.32f, -0.6f), V(-0.3f, 0), V(-0.32f, 0.6f), V(-0.1f, 0) }),
                ["3-5-2"] = new[] { gk, V(-0.74f, -0.4f), V(-0.78f, 0), V(-0.74f, 0.4f), V(-0.42f, -0.78f), V(-0.5f, -0.3f), V(-0.54f, 0), V(-0.5f, 0.3f), V(-0.42f, 0.78f), V(-0.12f, -0.2f), V(-0.14f, 0.22f) },
                ["5-3-2"] = new[] { gk, V(-0.66f, -0.8f), V(-0.76f, -0.38f), V(-0.8f, 0), V(-0.76f, 0.38f), V(-0.66f, 0.8f), V(-0.46f, -0.4f), V(-0.5f, 0), V(-0.46f, 0.4f), V(-0.14f, -0.2f), V(-0.14f, 0.22f) },
            };
        }

        public static string RoleFor(Vector2 b, int slot, bool hasKeeper)
        {
            if (hasKeeper && slot == 0) return "GK";
            if (b.x < -0.6f) return "DF";
            if (b.x < -0.3f) return "MF";
            return "FW";
        }

        public static ModeDef Get(string id) => Get(id, 3);

        /// <summary>players: jogadores por time na rua (o espaço cresce com o número de jogadores).</summary>
        public static ModeDef Get(string id, int players)
        {
            if (id == Society)
                return new ModeDef
                {
                    id = Society, length = 50, width = 30, goalWidth = 5, goalHeight = 2.2f, goalDepth = 1.4f,
                    keeper = true, walls = false, wallBounce = 0, boxDepth = 8, boxHalf = 10, penaltyDist = 9,
                    offside = false, centerR = 5, restartDist = 5,
                    rollDecel = 1.05f, groundDrag = 0.005f, airDrag = 0.014f, bounce = 0.55f,
                    surface = "synthetic", runMargin = 2, formation = Formations(Society)["3-2-1"],
                };
            if (id == Futsal)
                return new ModeDef
                {
                    id = Futsal, length = 40, width = 20, goalWidth = 3, goalHeight = 2, goalDepth = 1,
                    keeper = true, walls = false, wallBounce = 0, boxDepth = 6, boxHalf = 8, penaltyDist = 6,
                    offside = false, centerR = 3, restartDist = 5,
                    rollDecel = 0.75f, groundDrag = 0.004f, airDrag = 0.016f, bounce = 0.45f,
                    surface = "wood", runMargin = 1, formation = Formations(Futsal)["2-1-1"],
                };
            if (id == Street)
                return new ModeDef
                {
                    id = Street, length = players >= 5 ? 38 : players == 4 ? 34 : 30, width = players >= 5 ? 22 : players == 4 ? 20 : 18,
                    goalWidth = players >= 5 ? 3f : 2.6f, goalHeight = players >= 5 ? 1.6f : 1.4f, goalDepth = 0.8f,
                    keeper = false, walls = true, wallBounce = 0.72f, boxDepth = 0, boxHalf = 0, penaltyDist = 0,
                    offside = false, centerR = 2.5f, restartDist = 3,
                    rollDecel = 0.95f, groundDrag = 0.006f, airDrag = 0.014f, bounce = 0.68f,
                    surface = "asphalt", runMargin = 0, formation = Formations(Street)[StreetDefault(players)],
                };
            return new ModeDef
            {
                id = Field, length = 105, width = 68, goalWidth = 7.32f, goalHeight = 2.44f, goalDepth = 2,
                keeper = true, walls = false, wallBounce = 0, boxDepth = 16.5f, boxHalf = 20.15f, penaltyDist = 11,
                offside = true, centerR = 9.15f, restartDist = 9.15f,
                rollDecel = 1.25f, groundDrag = 0.006f, airDrag = 0.013f, bounce = 0.58f,
                surface = "grass", runMargin = 3, formation = Formations(Field)["4-4-2"],
            };
        }
    }
}
