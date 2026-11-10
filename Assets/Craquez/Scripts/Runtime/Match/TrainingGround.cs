using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    public static partial class VenueBuilder
    {
        /// <summary>
        /// Centro de treinamento (arena de treino): só o gramado, as traves, o alambrado e o verde em volta.
        /// Sem arquibancada, torcida, placar, telões, bancos ou gente na beira do campo.
        /// </summary>
        public static Venue TrainingGround(MatchSim sim, TeamData home, Kit homeKit, bool night, int quality, Transform parent)
        {
            var v = new Venue { root = new GameObject("Centro de treinamento").transform };
            v.root.SetParent(parent, false);
            float hl = sim.halfLen, hw = sim.halfW;
            v.name = "CT " + home.name;
            v.noDog = true;
            v.standDist = 30f;
            uint seed = (uint)home.name.GetHashCode() ^ 0x5eedu;
            Pitch(v, hl, hw, seed, quality, 3);
            Lines(v, sim, hl, hw);
            Goals(v, sim, hl, true);
            CornerFlags(v, hl, hw, homeKit.c1);
            PitchFence(v, hl, hw);
            // gramado em volta e uma faixa de terra batida para correr
            var ground = new MeshKit();
            ground.Quad(new Vector3(-500, -0.05f, -500), new Vector3(-500, -0.05f, 500), new Vector3(500, -0.05f, 500), new Vector3(500, -0.05f, -500), new Vector2(0, 0), new Vector2(60, 60));
            var gc = new Color(0.27f, 0.43f, 0.2f);
            ground.Object("Terreno", Gfx.Lit(night ? gc * 0.4f : gc, Gfx.Grass(256, Color.white, 57), 0.05f), v.root, false);
            var path = new MeshKit();
            float px = hl + 9f, pz = hw + 8f, w = 1.6f;
            path.Quad(new Vector3(-px - w, -0.03f, pz), new Vector3(-px - w, -0.03f, pz + w), new Vector3(px + w, -0.03f, pz + w), new Vector3(px + w, -0.03f, pz));
            path.Quad(new Vector3(px, -0.03f, -pz), new Vector3(px, -0.03f, pz + w), new Vector3(px + w, -0.03f, pz + w), new Vector3(px + w, -0.03f, -pz));
            path.Quad(new Vector3(-px - w, -0.03f, -pz), new Vector3(-px - w, -0.03f, pz + w), new Vector3(-px, -0.03f, pz + w), new Vector3(-px, -0.03f, -pz));
            path.Object("Pista de corrida", Gfx.Lit(new Color(0.55f, 0.42f, 0.3f), null, 0.05f), v.root, false);
            // material de treino encostado no alambrado: manequins de barreira, cones e mini-traves
            var dummy = new MeshKit();
            var cone = new MeshKit();
            var metal = new MeshKit();
            for (int i = 0; i < 5; i++)
            {
                var at = new Vector3(-hl * 0.55f + i * 0.75f, 0f, hw + 3.6f);
                dummy.Cylinder(at, at + Vector3.up * 1.75f, 0.22f, 8);
                metal.Cylinder(at + new Vector3(0, 0, 0.15f), at + new Vector3(0, -0.01f, 0.6f), 0.03f, 5);
            }
            for (int i = 0; i < 10; i++)
            {
                var at = new Vector3(hl * 0.15f + i * 1.3f, 0f, hw + 3.2f + (i % 2) * 0.6f);
                cone.Cylinder(at, at + Vector3.up * 0.06f, 0.14f, 8); cone.Cylinder(at, at + Vector3.up * 0.3f, 0.06f, 8);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                var at = new Vector3(s * (hl * 0.35f), 0f, hw + 4.2f);
                metal.Cylinder(at + new Vector3(-0.9f, 0, 0), at + new Vector3(-0.9f, 1f, 0), 0.04f, 6);
                metal.Cylinder(at + new Vector3(0.9f, 0, 0), at + new Vector3(0.9f, 1f, 0), 0.04f, 6);
                metal.Cylinder(at + new Vector3(-0.9f, 1f, 0), at + new Vector3(0.9f, 1f, 0), 0.04f, 6);
            }
            dummy.Object("Manequins de barreira", Gfx.Lit(Color.Lerp(homeKit.c1, Color.white, 0.15f), null, 0.3f), v.root, true);
            cone.Object("Cones", Gfx.Lit(new Color(1f, 0.45f, 0.08f), null, 0.3f), v.root, false);
            metal.Object("Mini-traves", Gfx.Lit(new Color(0.92f, 0.92f, 0.94f), null, 0.4f, 0.5f), v.root, false);
            // prédio do CT ao fundo, com faixa na cor do clube, e refletores baixos de treino
            var bld = new MeshKit();
            var band = new MeshKit();
            var glass = new MeshKit();
            float bz = hw + 26f;
            bld.Box(new Vector3(0f, 4f, bz + 6f), new Vector3(46f, 8f, 12f), 3f);
            band.Box(new Vector3(0f, 7.3f, bz - 0.02f), new Vector3(46.2f, 1.1f, 0.2f));
            for (int i = 0; i < 10; i++) glass.Box(new Vector3(-20.25f + i * 4.5f, 3.6f, bz - 0.03f), new Vector3(3f, 2.4f, 0.1f));
            bld.Object("Prédio do CT", Gfx.Lit(new Color(0.86f, 0.86f, 0.84f), null, 0.2f), v.root, true);
            band.Object("Faixa do clube", Gfx.Lit(homeKit.c1, null, 0.3f), v.root, false);
            glass.Object("Janelas do CT", night ? Gfx.Emissive(new Color(0.2f, 0.2f, 0.18f), new Color(1f, 0.9f, 0.7f) * 0.8f) : Gfx.Lit(new Color(0.25f, 0.35f, 0.45f), null, 0.9f, 0.2f), v.root, false);
            var poles = new MeshKit();
            foreach (float sx in new[] { -1f, -0.33f, 0.33f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var b = new Vector3(sx * hl * 0.9f, 0f, sz * (hw + 7.5f));
                    poles.Cylinder(b, b + Vector3.up * 14f, 0.16f, 6);
                    poles.Box(b + new Vector3(0, 14.2f, -sz * 0.3f), new Vector3(1.8f, 0.9f, 0.4f));
                    if (night)
                    {
                        var lg = new GameObject("Refletor de treino");
                        lg.transform.SetParent(v.root, false);
                        lg.transform.localPosition = b + new Vector3(0, 13.8f, -sz * 0.5f);
                        lg.transform.LookAt(v.root.TransformPoint(new Vector3(sx * hl * 0.45f, 0f, 0f)));
                        var l = lg.AddComponent<Light>();
                        l.type = LightType.Spot;
                        l.range = 70f;
                        l.spotAngle = 75f;
                        l.intensity = 2.2f;
                        l.shadows = LightShadows.None;
                        v.floodlights.Add(l);
                    }
                }
            poles.Object("Postes de luz", Gfx.Lit(new Color(0.55f, 0.56f, 0.58f), null, 0.4f, 0.6f), v.root, false);
            // árvores em volta, em grupos separados por lado (uma malha só, em anel, "envolvia" o campo e
            // o sistema que esconde o que tapa a câmera sumia com todas)
            var groups = new PropKit[6];
            for (int g = 0; g < groups.Length; g++) groups[g] = new PropKit((int)(seed % 9973) + g * 17);
            var rnd = groups[0];
            for (int i = 0; i < 44; i++)
            {
                float a = i / 44f * Mathf.PI * 2f + rnd.R() * 0.08f;
                if (Mathf.Sin(a) < -0.55f) continue;   // não tapa a câmera
                float rr = hw + 34f + rnd.R() * 18f;
                var p = new Vector3(Mathf.Cos(a) * rr * 1.7f, 0f, Mathf.Sin(a) * rr);
                if (Mathf.Abs(p.x) < 30f && p.z > 0f && p.z < bz + 14f) continue;   // não dentro do prédio
                groups[Mathf.Clamp((int)(Mathf.Repeat(a, Mathf.PI * 2f) / (Mathf.PI * 2f) * groups.Length), 0, groups.Length - 1)].Tree(p, 8f + rnd.R() * 6f);
            }
            for (int g = 0; g < groups.Length; g++)
            {
                var gr = new GameObject("Árvores " + g).transform;
                gr.SetParent(v.root, false);
                groups[g].Emit(gr, quality, new Color(0.2f, 0.42f, 0.18f), new Color(0.45f, 0.32f, 0.2f), Color.white, Color.white);
            }
            v.tunnel = new Vector3(0, 0, -(hw + 6f));
            v.outerR = hw + 30f;
            return v;
        }
    }
}
