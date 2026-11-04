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
            public float[] face;
            public int boots;
        }

        /// <summary>face = [pele 0-5, cor do cabelo, estilo, barba, olhos, porte] (leagues.json).</summary>
        public static Look LookFor(SimPlayer p, int salt = 0)
        {
            var f = p.face ?? new float[0];
            float F(int i, float d) => i < f.Length ? f[i] : d;
            uint h = (uint)(p.name.GetHashCode() ^ (salt * 7919));
            float R(int k) => ((h >> (k * 3)) & 0xFF) / 255f;
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
            return new Look
            {
                baseName = b, hair = Mathf.Clamp(hair, 0, Hairs.Length - 1), hairColor = HairColors[Mathf.Clamp(hc, 0, HairColors.Length - 1)],
                skinTint = tint, scale = Mathf.Lerp(0.95f, 1.05f, F(5, R(6) * 4f) / 4f), face = f,
                boots = (int)((h >> 5) % (uint)BootPainter.Designs.Length),
            };
        }

        /// <summary>Cria o atleta (com uniforme e número). Retorna o objeto raiz.</summary>
        public static AthleteRig Create(SimPlayer p, Kit kit, Kit keeperKit, Transform parent, int quality, int boots = -1)
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
            inst.transform.localScale = Vector3.one * look.scale;
            Dress(rig, look, p.isKeeper ? keeperKit : kit, p.number, quality);
            return rig;
        }

        public static void Dress(AthleteRig rig, Look look, Kit kit, int number, int quality)
        {
            var smrs = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Mesh bodyMesh = null, socksMesh = null;
            foreach (var r in smrs)
            {
                if (r.name == "Body") bodyMesh = r.sharedMesh;
                if (r.name == "Socks") socksMesh = r.sharedMesh;
            }
            var axes = bodyMesh != null ? KitPainter.AxesFrom(bodyMesh, socksMesh) : default;
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
                            Gfx.Lit(look.skinTint, T(SkinTex[look.baseName]), 0.32f));
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
                        var kt = KitPainter.Paint(smr.sharedMesh, part, kit, axes, part == KitPart.Socks ? tex / 2 : tex);
                        // tecido dos dois lados (vê-se o avesso pela gola e pelas mangas)
                        smr.sharedMaterial = Shared("kit:" + kt.GetInstanceID(), () => Gfx.LitN(Color.white, kt, Gfx.KnitNormal, part == KitPart.Shirt ? 0.34f : 0.26f, 0.35f, true));
                        break;
                    }
                    case "Boots":
                    {
                        int d = look.boots;
                        smr.sharedMaterial = Shared("boots:" + d, () => Gfx.Lit(Color.white, BootPainter.Paint(d), 0.62f));
                        break;
                    }
                    default:
                        if (n.StartsWith("Hair_"))
                        {
                            string hn = n.Substring(5);
                            bool on = hn == Hairs[look.hair];
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
            rig.SetNumber(number, kit);
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
