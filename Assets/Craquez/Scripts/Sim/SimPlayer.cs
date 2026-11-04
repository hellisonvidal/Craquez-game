using System.Collections.Generic;
using UnityEngine;

namespace Craquez.Sim
{
    public enum Skill { None, Knock, Drag, Roll, Stepover, Roulette }

    [System.Flags]
    public enum Trait
    {
        None = 0, Velocista = 1, Driblador = 2, Finalizador = 4, Colocado = 8, Cabeceador = 16, Maestro = 32,
        Muralha = 64, Carrinho = 128, GoleiroSeguro = 256, Reflexo = 512, LongaDistancia = 1024, Resistencia = 2048, Lider = 4096,
    }

    /// <summary>Estado de um atleta na simulação (puro dado + lógica).</summary>
    public class SimPlayer
    {
        public const float Radius = 0.38f;
        public static readonly float[] SkillTime = { 0f, 0.25f, 0.5f, 0.42f, 0.55f, 0.62f };

        public static readonly Dictionary<string, Trait> TraitNames = new Dictionary<string, Trait>
        {
            ["Velocista"] = Trait.Velocista, ["Driblador"] = Trait.Driblador, ["Finalizador"] = Trait.Finalizador,
            ["Chute Colocado"] = Trait.Colocado, ["Cabeceador"] = Trait.Cabeceador, ["Maestro"] = Trait.Maestro,
            ["Muralha"] = Trait.Muralha, ["Carrinho"] = Trait.Carrinho, ["Goleiro Seguro"] = Trait.GoleiroSeguro,
            ["Reflexo"] = Trait.Reflexo, ["Longa Distância"] = Trait.LongaDistancia, ["Resistência"] = Trait.Resistencia,
            ["Líder"] = Trait.Lider,
        };

        public int id, team, index;
        public bool isKeeper;
        public int number = 1;
        public string name = "";
        public string pos = "";
        public string nat = "";
        public Trait traits;
        public float[] face = new float[0];   // [pele, cabelo-cor, estilo, barba, olhos, porte]
        public Vector2 basePos;

        public float pace = 0.7f, accel = 0.7f, shot = 0.7f, passing = 0.7f, control = 0.7f, defense = 0.7f;

        public Vector2 position, vel, facing = Vector2.right, intent;
        public float stamina = 1f;
        public bool wantSprint, sprinting;

        public float kickCd, stun, slide, tackleAnim, kickAnim, hold, dive, runPhase;
        public bool slideHitBall;

        public float windup, windupTotal, windupPower;
        public int windupKind;
        public Vector2 windupAim;
        public bool windupFinesse;
        public int kickKind;
        public Skill skill;
        public float skillT, skillSide = 1f, skillCd, burst, offBalance, touchT;
        public Vector2 skillDir = Vector2.right;
        public bool faceBall, shielding;

        public bool Has(Trait t) => (traits & t) != 0;
        public float TopSpeed() => Mathf.Lerp(6.4f, 8.8f, pace);
        public float JogSpeed() => TopSpeed() * 0.64f;
    }
}
