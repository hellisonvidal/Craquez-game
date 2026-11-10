using System.Collections.Generic;
using UnityEngine;

namespace Craquez.Sim
{
    public enum Skill { None, Knock, Drag, Roll, Stepover, Roulette, Elastico, Pedalada, Chapeu, Caneta, Corte }

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
        public static readonly float[] SkillTime = { 0f, 0.25f, 0.5f, 0.42f, 0.55f, 0.62f, 0.46f, 0.8f, 0.3f, 0.28f, 0.34f };
        public static readonly string[] SkillNames = { "", "Toque à frente", "Puxada", "Arrastada", "Pedalada simples", "Roleta", "Elástico", "Pedalada", "Chapéu", "Caneta", "Corte" };

        /// <summary>Jogada característica (drible próprio) — só alguns atletas de meio para a frente.</summary>
        public Skill signature = Skill.None;

        public static readonly Dictionary<string, Trait> TraitNames = new Dictionary<string, Trait>
        {
            ["Velocista"] = Trait.Velocista, ["Driblador"] = Trait.Driblador, ["Finalizador"] = Trait.Finalizador,
            ["Chute Colocado"] = Trait.Colocado, ["Cabeceador"] = Trait.Cabeceador, ["Maestro"] = Trait.Maestro,
            ["Muralha"] = Trait.Muralha, ["Carrinho"] = Trait.Carrinho, ["Goleiro Seguro"] = Trait.GoleiroSeguro,
            ["Reflexo"] = Trait.Reflexo, ["Longa Distância"] = Trait.LongaDistancia, ["Resistência"] = Trait.Resistencia,
            ["Líder"] = Trait.Lider,
        };

        public int id, team, index;
        /// <summary>Anti-giro da IA: última direção pedida, giro acumulado e tempo de "seguir reto".</summary>
        public Vector2 aiLastIntent;
        /// <summary>Giro acumulado do corpo (graus, decai em ~1 s): limita voltas seguidas no mesmo sentido.</summary>
        public float bodyTurnAcc;
        /// <summary>Goleiro saindo no cara a cara (fecha o ângulo, já armado: reage mais rápido).</summary>
        public bool oneOnOne;
        public float aiTurnAcc, aiStraightT;
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
        /// <summary>Índice no elenco do time (-1 = gerado); usado nas substituições.</summary>
        public int rosterIndex = -1;
        public bool wantSprint, sprinting;

        public float kickCd, stun, slide, tackleAnim, kickAnim, hold, dive, runPhase;
        public bool slideHitBall;
        /// <summary>
        /// Defesa do goleiro: 0 nenhuma, 1 encaixe de frente, 2 defesa baixa (cai sobre a bola),
        /// 3 ponte rasteira, 4 ponte à meia-altura, 5 ponte alta (no ângulo), 6 salto para agarrar no alto.
        /// saveSide = lado do mergulho no eixo y do campo; saveH = altura das mãos no alvo; saveDist = quanto precisa ir de lado.
        /// </summary>
        public int saveKind;
        /// <summary>Ponte (mergulho lateral: rasteira, meia-altura ou no ângulo). O salto no alto (6) é de pé.</summary>
        public bool Diving => saveKind >= 3 && saveKind <= 5;
        /// <summary>Fração da defesa (0..1) em que a bola deve chegar às mãos: o corpo e os braços chegam ao máximo ali.</summary>
        public float saveContact = 0.4f;
        /// <summary>Goleiro cobrando tiro de meta: a bola fica no chão (hold é só o tempo de espera, não está nas mãos).</summary>
        public bool goalKick;
        /// <summary>Indo ajudar a levantar quem sofreu a falta (id da vítima; -1 = não).</summary>
        public int helping = -1;
        /// <summary>Goleiro com a bola nas mãos (não vale para o tiro de meta, com a bola no chão).</summary>
        public bool InHands => hold > 0f && !goalKick;
        /// <summary>Cartões: amarelos recebidos e expulsão (fica fora de campo; no futsal volta outro em 2 min).</summary>
        public int yellows;
        public bool sentOff;
        public float sentOffT;
        /// <summary>Goleiro lendo um chute: desloca em passadas laterais (não corre).</summary>
        public bool keeperSet;
        /// <summary>Há quanto tempo o goleiro está com a bola nas mãos (espera alguém se desmarcar).</summary>
        public float heldT;
        public float saveT, saveTotal, saveSide, saveH, saveDist;

        public float windup, windupTotal, windupPower;
        public int windupKind;
        public Vector2 windupAim;
        public bool windupFinesse;
        public int kickKind;
        public Skill skill;
        public float skillT, skillSide = 1f, skillCd, burst, offBalance, touchT;
        public Vector2 skillDir = Vector2.right;
        public bool faceBall, shielding;
        /// <summary>No chão após falta: segundos restantes, total, direção da pancada e tipo de queda (0 frente, 1 costas, 2 lado).</summary>
        public float down, downTotal;
        public Vector2 fallDir;
        public int fallKind;
        /// <summary>Personalidade (0..1): disposição, ousadia, disciplina tática; leitura de jogo (s entre decisões).</summary>
        public float work = 0.5f, flair = 0.5f, discipline = 0.6f, think = 0.35f, thinkT;
        /// <summary>IA: decisão atual (aiGoal), alvo suavizado (aiTarget) e deslocamento pessoal na formação.</summary>
        public Vector2 aiGoal, aiTarget, lane;
        public bool aiAttacking;

        public bool Has(Trait t) => (traits & t) != 0;
        public float TopSpeed() => Mathf.Lerp(6.4f, 8.8f, pace);
        public float JogSpeed() => TopSpeed() * 0.64f;
    }
}
