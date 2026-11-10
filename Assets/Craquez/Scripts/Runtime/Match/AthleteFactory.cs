using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Monta atletas realistas a partir dos modelos MakeHuman (Resources/Athletes):
    /// escolhe biotipo, cabelo, tom de pele e formato do rosto pelo "DNA" do
    /// jogador (vindo do banco de ligas) e veste o uniforme do time.
    /// </summary>
    public static class AthleteFactory
    {
        public static readonly string[] Bases = { "afro", "latino", "europeu", "asiatico" };
        /// <summary>Futsal: tênis de quadra no lugar das chuteiras (vale para os atletas criados em seguida).</summary>
        public static bool IndoorShoes;
        static readonly Dictionary<string, GameObject> _models = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

        static readonly Dictionary<string, string> SkinTex = new Dictionary<string, string>
        {
            ["afro"] = "young_darkskinned_male_diffuse", ["latino"] = "young_lightskinned_male_diffuse2",
            ["europeu"] = "young_lightskinned_male_diffuse", ["asiatico"] = "young_lightskinned_male_diffuse3",
        };
        static readonly Dictionary<string, string> BrowTex = new Dictionary<string, string>
        {
            ["afro"] = "eyebrow001", ["latino"] = "eyebrow006", ["europeu"] = "eyebrow003", ["asiatico"] = "eyebrow009",
        };
        public static readonly string[] Hairs = { "short02", "short01", "short03", "short04", "afro01" };
        static readonly Color[] HairColors =
        {
            new Color(0.05f, 0.04f, 0.035f), new Color(0.16f, 0.1f, 0.06f), new Color(0.3f, 0.19f, 0.1f),
            new Color(0.62f, 0.45f, 0.26f), new Color(0.75f, 0.6f, 0.38f), new Color(0.45f, 0.18f, 0.08f),
        };

        static GameObject Model(string b)
        {
            if (_models.TryGetValue(b, out var m) && m != null) return m;
            m = Resources.Load<GameObject>("Athletes/Athlete_" + b);
            _models[b] = m;
            return m;
        }

        static Texture2D T(string name)
        {
            if (_tex.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>("Athletes/Textures/" + name);
            _tex[name] = t;
            return t;
        }

        static Material Shared(string key, System.Func<Material> make)
        {
            if (_mats.TryGetValue(key, out var m) && m != null) return m;
            m = make();
            _mats[key] = m;
            return m;
        }

        /// <summary>Parâmetros visuais derivados do jogador.</summary>
        public struct Look
        {
            public string baseName;
            public int hair;
            public Color hairColor, skinTint;
            public float scale;
            public float build;   // 0 magro .. 1 forte
            public float frame;   // 0 ombros estreitos .. 1 largos
            public float[] face;
            public int boots;
            public int tape;      // fita nos punhos: 0 nenhuma, 1 um, 2 os dois
        }

        /// <summary>face = [pele 0-5, cor do cabelo, estilo, barba, olhos, porte] (leagues.json).</summary>
        public static Look LookFor(SimPlayer p, int salt = 0)
        {
            var f = p.face ?? new float[0];
            float F(int i, float d) => i < f.Length ? f[i] : d;
            uint h = (uint)(p.name.GetHashCode() ^ (salt * 7919));
            uint h2 = (h ^ 0x9E3779B9u) * 2654435761u;
            float R(int k) => (k < 8 ? (h >> (k * 3)) & 0xFF : (h2 >> ((k - 8) * 6)) & 0xFF) / 255f;   // k >= 8: segundo hash (bits suficientes)
            int skin = Mathf.RoundToInt(F(0, R(1) * 5f));
            string b = skin >= 4 ? "afro" : skin == 3 ? (R(2) > 0.5f ? "afro" : "latino") : skin == 2 ? "latino" : (R(3) < 0.18f ? "asiatico" : "europeu");
            if (p.nat == "JP" || p.nat == "KR" || p.nat == "CN") b = "asiatico";
            int style = Mathf.RoundToInt(F(2, R(4) * 5f));
            int hair = b == "afro" ? (style % 2 == 0 ? 4 : 0) : (style % 4);
            int hc = Mathf.RoundToInt(F(1, R(5) * 5f));
            if (b == "afro" || b == "asiatico") hc = Mathf.Min(hc, 1);
            float tone = Mathf.Clamp(skin, 0, 5);
            var tint = b == "afro" ? Color.Lerp(new Color(1.05f, 1.02f, 1f), new Color(0.82f, 0.78f, 0.76f), (tone - 3f) / 2f)
                : Color.Lerp(new Color(1.04f, 1.02f, 1f), new Color(0.9f, 0.82f, 0.74f), tone / 3f);
            // cada pele um pouco diferente (mais quente, mais rosada, mais oliva)
            float warm = (R(11) - 0.5f) * 0.08f, rosy = (R(12) - 0.5f) * 0.05f;
            tint = new Color(tint.r * (1f + warm + rosy), tint.g * (1f + warm * 0.3f), tint.b * (1f - warm * 0.6f + rosy * 0.4f));
            // cabelo: alguns raspados, alguns descoloridos
            if (R(13) < 0.07f) hc = 4;
            bool shaved = R(14) < 0.08f;
            return new Look
            {
                baseName = b, hair = shaved ? -1 : Mathf.Clamp(hair, 0, Hairs.Length - 1), hairColor = HairColors[Mathf.Clamp(hc, 0, HairColors.Length - 1)],
                tape = R(15) < 0.3f ? (R(16) < 0.5f ? 1 : 2) : 0,
                // altura: do baixinho (1,68 m) ao grandalhão (1,93 m), mais gente perto da média
                skinTint = tint, scale = Mathf.Lerp(0.91f, 1.09f, Mathf.Clamp01(F(5, R(6) * 4f) / 4f * 0.5f + (R(8) + R(9)) * 0.25f)), face = f,
                frame = Mathf.Clamp01(R(10) * 0.7f + F(5, 2f) / 4f * 0.3f),
                // maioria magro/médio; só alguns fortes (porte do banco + sorteio por nome)
                build = Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(F(5, R(6) * 4f) / 4f * 0.6f + R(7) * 0.4f), 1.6f)),
                boots = (int)((h >> 5) % (uint)BootPainter.Designs.Length),
            };
        }

        /// <summary>Cria o atleta (com uniforme e número). Retorna o objeto raiz.</summary>
        public static AthleteRig Create(SimPlayer p, Kit kit, Kit keeperKit, Transform parent, int quality, int boots = -1, bool captain = false)
        {
            var look = LookFor(p);
            if (boots >= 0) look.boots = boots;
            var model = Model(look.baseName) ?? Model("latino");
            if (model == null)
            {
                Debug.LogError("Modelos de atleta ausentes em Resources/Athletes");
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                cap.transform.SetParent(parent, false);
                return cap.AddComponent<AthleteRig>();
            }
            var root = new GameObject("Atleta " + p.number + " " + p.name);
            root.transform.SetParent(parent, false);
            var inst = Object.Instantiate(model, root.transform, false);
            inst.name = "model";
            var rig = root.AddComponent<AthleteRig>();
            rig.Init(inst.transform);
            rig.Physique(look.build, look.frame);
            // altura de verdade: cada modelo tem uma altura própria (cabeça em repouso de 1,60 a 1,74 m,
            // ou seja, 1,81 a 1,95 m no topo), e a escala sorteada ia de 0,91 a 1,09 — saíam atletas de
            // até 2,10 m, grandes demais para o campo. Agora a escala leva cada um à altura sorteada,
            // de 1,68 a 1,93 m (média ~1,80 m).
            float u = Mathf.InverseLerp(0.91f, 1.09f, look.scale);
            float tall = Mathf.Lerp(1.68f, 1.93f, u);
            float modelH = rig.head != null ? rig.head.position.y - root.transform.position.y + 0.21f : 1.85f;
            inst.transform.localScale = Vector3.one * Mathf.Clamp(tall / Mathf.Max(1.2f, modelH), 0.82f, 1.08f);
            // mãos do modelo eram grandes demais para o corpo: um pouco menores
            foreach (var hn in new[] { "hand_l", "hand_r" })
                if (rig.bones.TryGetValue(hn, out var hb)) hb.localScale = hb.localScale * 0.85f;   // (0,68 sumia com a mão)
            Dress(rig, look, p.isKeeper ? keeperKit : kit, p.number, quality);
            Accessories(rig, look, p.isKeeper ? keeperKit : kit, p.isKeeper, captain, quality);
            return rig;
        }

        /// <summary>
        /// Detalhes que diferenciam os atletas: luvas do goleiro, braçadeira do capitão, fita nos
        /// punhos. Montados na pose de ligação e presos aos ossos (acompanham a animação).
        /// </summary>
        static void Accessories(AthleteRig rig, Look look, Kit kit, bool keeper, bool captain, int quality)
        {
            Transform B(string n) => rig.bones.TryGetValue(n, out var t) ? t : null;
            foreach (var sfx in new[] { "_l", "_r" })
            {
                var up = B("upperarm" + sfx); var lo = B("lowerarm" + sfx); var hd = B("hand" + sfx);
                if (up == null || lo == null || hd == null) continue;
                var armDir = (hd.position - lo.position).normalized;
                if (keeper)
                {
                    // luva: punho grosso + mão espalmada maior que a mão (cor de destaque, palma clara)
                    Transform finger = null;
                    foreach (Transform c in hd) if (c.GetComponent<Renderer>() == null) { finger = c; break; }
                    var fdir = finger != null ? (finger.position - hd.position).normalized : armDir;
                    var glove = new MeshKit();
                    glove.Cylinder(hd.position - armDir * 0.07f, hd.position + armDir * 0.01f, 0.05f, 10);   // punho
                    var go = glove.Object("Luva (punho)" + sfx, Gfx.Character(Color.Lerp(kit.c2, Color.black, 0.35f), null, 0.3f), null, false);
                    go.transform.SetParent(hd, true);
                    var palm = new MeshKit();
                    palm.Box(Vector3.zero, new Vector3(0.105f, 0.045f, 0.16f));
                    var pg = palm.Object("Luva" + sfx, Gfx.Character(Color.Lerp(kit.c1, Color.white, 0.55f), null, 0.3f), hd, false);
                    pg.transform.position = hd.position + fdir * 0.07f;
                    pg.transform.rotation = Quaternion.LookRotation(fdir, Vector3.Cross(fdir, armDir).sqrMagnitude > 1e-4f ? Vector3.Cross(fdir, rig.transform.forward) : Vector3.up);
                    pg.transform.SetParent(hd, true);
                }
                else if (look.tape > 0 && (look.tape == 2 || sfx == "_r"))
                {
                    var tape = new MeshKit();
                    // fita fina no pulso, acima da mão (antes era larga e cobria a mão: parecia um toco escuro)
                    tape.Cylinder(hd.position - armDir * 0.075f, hd.position - armDir * 0.035f, 0.029f, 12);
                    var tg = tape.Object("Fita no punho" + sfx, Gfx.Character(look.tape == 1 ? Color.white : Color.Lerp(kit.c1, Color.white, 0.55f), null, 0.2f), null, false);
                    tg.transform.SetParent(lo, true);
                }
                if (captain && sfx == "_l")
                {
                    // braçadeira amarela no braço esquerdo
                    var a = Vector3.Lerp(up.position, lo.position, 0.38f);
                    var b2 = Vector3.Lerp(up.position, lo.position, 0.52f);
                    var band = new MeshKit();
                    band.Cylinder(a, b2, 0.058f, 12);
                    var bg = band.Object("Braçadeira de capitão", Gfx.Character(new Color(1f, 0.82f, 0.1f), null, 0.3f), null, false);
                    bg.transform.SetParent(up, true);
                }
            }
        }

        public static void Dress(AthleteRig rig, Look look, Kit kit, int number, int quality)
        {
            var smrs = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Mesh bodyMesh = null, socksMesh = null, bootsMesh = null;
            foreach (var r in smrs)
            {
                if (r.name == "Body") bodyMesh = r.sharedMesh;
                if (r.name == "Socks") socksMesh = r.sharedMesh;
                if (r.name == "Boots") bootsMesh = r.sharedMesh;
            }
            var axes = bodyMesh != null ? KitPainter.AxesFrom(bodyMesh, socksMesh, bootsMesh) : default;
            int tex = quality >= 2 ? 1024 : 512;
            foreach (var smr in smrs)
            {
                string n = smr.name;
                smr.updateWhenOffscreen = false;
                smr.quality = quality >= 2 ? SkinQuality.Bone4 : SkinQuality.Bone2;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                switch (n)
                {
                    case "Body":
                    {
                        var skin = Shared("skin:" + look.baseName + Teams.Hex(look.skinTint), () =>
                            Gfx.Athlete(look.skinTint, T(SkinTex[look.baseName]), 0.3f, 1f));
                        var mats = new Material[Mathf.Max(1, smr.sharedMesh.subMeshCount)];
                        for (int i = 0; i < mats.Length; i++) mats[i] = skin;
                        smr.sharedMaterials = mats;
                        ApplyFace(smr, look.face);
                        rig.body = smr;
                        break;
                    }
                    case "Shirt":
                    case "Shorts":
                    case "Socks":
                    {
                        var part = n == "Shirt" ? KitPart.Shirt : n == "Shorts" ? KitPart.Shorts : KitPart.Socks;
                        // a camisa leva o número pintado (uma textura por atleta; no Ultra em 1024)
                        int size = part == KitPart.Socks ? tex / 2 : part == KitPart.Shirt ? (quality >= 3 ? 1024 : 512) : tex;
                        var kt = KitPainter.Paint(smr.sharedMesh, part, kit, axes, size, part == KitPart.Shirt ? number : -1);
                        // tecido dos dois lados (vê-se o avesso pela gola e pelas mangas)
                        if (part == KitPart.Shirt) rig.shirt = smr;
                        else if (part == KitPart.Shorts) rig.shorts = smr;
                        smr.sharedMaterial = Shared("kit:" + kt.GetInstanceID(), () => Gfx.Athlete(Color.white, kt, part == KitPart.Shirt ? 0.36f : 0.28f, 0f, true));
                        break;
                    }
                    case "Boots":
                    {
                        int d = look.boots;
                        bool indoor = IndoorShoes;
                        smr.sharedMaterial = Shared("boots:" + d + (indoor ? "f" : ""), () => Gfx.Athlete(Color.white, BootPainter.Paint(d, indoor), indoor ? 0.4f : 0.6f, 0f));
                        break;
                    }
                    default:
                        if (n.StartsWith("Hair_"))
                        {
                            string hn = n.Substring(5);
                            bool on = look.hair >= 0 && hn == Hairs[look.hair];
                            smr.gameObject.SetActive(on);
                            if (on)
                                smr.sharedMaterial = Shared("hair:" + hn + Teams.Hex(look.hairColor), () =>
                                    Gfx.Cutout(look.hairColor * 2.2f, T(hn == "afro01" ? "afro_diffuse" : hn + "_diffuse"), 0.35f, 0.25f));
                        }
                        else if (n == "Eyes")
                        {
                            smr.sharedMaterial = Shared("eyes", () => Gfx.Lit(Color.white, T("brown_eye"), 0.85f));
                            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        }
                        else if (n == "Eyebrows")
                        {
                            smr.sharedMaterial = Shared("brow:" + look.baseName, () => Gfx.Cutout(look.hairColor * 1.6f, T(BrowTex[look.baseName]), 0.3f));
                            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        }
                        else if (n == "Eyelashes")
                        {
                            smr.sharedMaterial = Shared("lashes", () => Gfx.Cutout(new Color(0.1f, 0.08f, 0.07f), T("eyelashes02"), 0.3f));
                            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            smr.gameObject.SetActive(quality >= 1);
                        }
                        break;
                }
            }
            // expressões faciais (formas criadas uma vez por malha, a partir dos marcos do rosto)
            SkinnedMeshRenderer eyesR = null, browR = null, lashR = null;
            foreach (var r in smrs) { if (r.name == "Eyes") eyesR = r; else if (r.name == "Eyebrows") browR = r; else if (r.name == "Eyelashes") lashR = r; }
            if (rig.body != null && eyesR != null && bodyMesh != null) FaceRig.Build(rig.body, eyesR, axes, browR, lashR);
            // (o número agora vem pintado na camisa)
        }

        static readonly string[] FaceKeys =
        {
            "Face_NoseWide", "Face_NoseLong", "Face_JawWide", "Face_ChinLong", "Face_CheekBones",
            "Face_Lean", "Face_Round", "Face_EyesBig", "Face_BrowHeavy", "Face_LipsFull",
        };

        /// <summary>Formato do rosto: pesos das shape keys a partir do DNA do jogador.</summary>
        static void ApplyFace(SkinnedMeshRenderer smr, float[] face)
        {
            var m = smr.sharedMesh;
            if (m == null || m.blendShapeCount == 0) return;
            uint seed = 2166136261u;
            if (face != null) foreach (var v in face) seed = (seed ^ (uint)(v * 1000)) * 16777619u;
            for (int k = 0; k < FaceKeys.Length; k++)
            {
                int idx = m.GetBlendShapeIndex(FaceKeys[k]);
                if (idx < 0) continue;
                seed = seed * 1664525u + 1013904223u;
                float r = (seed >> 8) / 16777216f;
                float w = r < 0.35f ? 0f : (r - 0.35f) / 0.65f * 85f;
                smr.SetBlendShapeWeight(idx, w);
            }
        }
    }
}
