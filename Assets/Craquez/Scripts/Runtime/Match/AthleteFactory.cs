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
        public static readonly string[] Hairs = { "short02", "short01", "short03", "short04", "afro01", "long01", "ponytail01", "braid01", "bob02" };
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
            public int hairStyle; // cabelo procedural (HairCap): 0 raspado .. 7 mais longo
            public bool untucked; // camisa por fora do calção
            public int faceSeed;  // traços do rosto (pelo nome: dois jogadores com o mesmo "DNA" não ficam iguais)
        }

        /// <summary>Estilo de cabelo (curto 30%, médio 20%, topete 15%, undercut 20%, cacheado 5%, mais longo 10%).</summary>
        /// <summary>
        /// Cabelo do modelo (sempre um de verdade, menos nos raspados — antes undercut e cacheado ficavam só com
        /// a touca procedural e, no corpo novo, pareciam carecas): afro com black power, tranças ou curto; os
        /// outros, os curtos na maioria e às vezes longo, rabo de cavalo, chanel ou tranças.
        /// </summary>
        static int PickHair(string b, int shortIdx, float r, float r2)
        {
            // (2.21: poucos de cabelo comprido, como no futebol de verdade — antes ~31% tinham longo/rabo/chanel/tranças)
            if (b == "afro") return r < 0.4f ? 4 : r < 0.5f ? 7 : 0;
            if (r2 < 0.03f) return 5;      // longo
            if (r2 < 0.055f) return 6;     // rabo de cavalo
            if (r2 < 0.07f) return 8;      // chanel
            if (r2 < 0.085f) return 7;     // tranças
            return Mathf.Clamp(shortIdx, 0, 3);
        }

        static int PickStyle(float r) => r < 0.3f ? 1 : r < 0.5f ? 2 : r < 0.65f ? 3 : r < 0.85f ? 4 : r < 0.9f ? 5 : 7;

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
                baseName = b, hair = shaved ? -1 : PickHair(b, hair, R(7), R(17)), hairColor = HairColors[Mathf.Clamp(hc, 0, HairColors.Length - 1)],
                tape = R(15) < 0.3f ? (R(16) < 0.5f ? 1 : 2) : 0,
                // touca de volume só por baixo dos cabelos curtos (nos longos ela atravessava); raspado = só a sombra
                hairStyle = shaved ? 0 : PickHair(b, hair, R(7), R(17)) <= 3 ? 1 : -1,
                // camisa por dentro do calção em 90% dos atletas, por fora em 10%
                untucked = ((h >> 21) & 1023u) % 10u == 0u,
                faceSeed = (int)h2,
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
                if (rig.bones.TryGetValue(hn, out var hb)) hb.localScale = hb.localScale * 0.75f;   // (0,85 ainda parecia mão grande; 0,68 sumia com a mão)
            Dress(rig, look, p.isKeeper ? keeperKit : kit, p.id < 0 && p.number <= 0 ? -1 : p.number, quality);   // (juiz e técnico: sem número)
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
            // travas da chuteira de campo (no futsal e na quadra, tênis de sola lisa): 2 no calcanhar e 4 na
            // frente, presas ao osso do pé e ao da ponta (acompanham a passada)
            if (!IndoorShoes && quality >= 1)
            {
                int di = ((look.boots % BootPainter.Designs.Length) + BootPainter.Designs.Length) % BootPainter.Designs.Length;
                var studMat = Gfx.Lit(BootPainter.Designs[di].studs, null, 0.5f, 0.3f);
                foreach (var sfx in new[] { "_l", "_r" })
                {
                    var ft = B("foot" + sfx); var ball = B("ball" + sfx);
                    if (ft == null || ball == null) continue;
                    var along = ball.position - ft.position; along.y = 0f;
                    if (along.sqrMagnitude < 1e-4f) continue;
                    var fwd = along.normalized;
                    var lat = Vector3.Cross(Vector3.up, fwd);
                    // altura da sola: o ponto mais baixo da malha da chuteira (pose de ligação)
                    float sole = ft.position.y - 0.08f;
                    foreach (var br in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if (br.name == "Boots" && br.sharedMesh != null)
                        {
                            var bb = br.sharedMesh.bounds;
                            float mnY = float.MaxValue;
                            for (int cx = -1; cx <= 1; cx += 2) for (int cy = -1; cy <= 1; cy += 2) for (int cz = -1; cz <= 1; cz += 2)
                                mnY = Mathf.Min(mnY, br.transform.TransformPoint(bb.center + Vector3.Scale(bb.extents, new Vector3(cx, cy, cz))).y);
                            sole = mnY + 0.004f;
                        }
                    var heelK = new MeshKit();
                    var frontK = new MeshKit();
                    var heelC = ft.position - fwd * 0.045f; heelC.y = sole;
                    foreach (float sd in new[] { -1f, 1f })
                    {
                        var hp = heelC + lat * sd * 0.022f;
                        heelK.Cylinder(hp, hp - Vector3.up * 0.009f, 0.0065f, 6);
                    }
                    var bc = ball.position; bc.y = sole;
                    foreach (float off in new[] { -0.03f, 0.022f })
                        foreach (float sd in new[] { -1f, 1f })
                        {
                            var fp = bc + fwd * off + lat * sd * (off < 0f ? 0.03f : 0.022f);
                            frontK.Cylinder(fp, fp - Vector3.up * 0.009f, 0.006f, 6);
                        }
                    var hg = heelK.Object("Travas (calcanhar)" + sfx, studMat, null, false);
                    hg.transform.SetParent(ft, true);
                    var fg = frontK.Object("Travas (frente)" + sfx, studMat, null, false);
                    fg.transform.SetParent(ball, true);
                }
            }
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
                    // (material sem contorno: com o contorno do cel shading, a fita fina virava um borrão escuro sobre a mão)
                    var tg = tape.Object("Fita no punho" + sfx, Gfx.Lit(look.tape == 1 ? Color.white : Color.Lerp(kit.c1, Color.white, 0.55f), null, 0.2f), null, false);
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
                        ApplyFace(smr, look.face, look.faceSeed);
                        rig.body = smr;
                        break;
                    }
                    case "Shirt":
                    case "Shorts":
                    case "Socks":
                    {
                        var part = n == "Shirt" ? KitPart.Shirt : n == "Shorts" ? KitPart.Shorts : KitPart.Socks;
                        smr.sharedMesh = SmoothNormals(smr.sharedMesh);
                        // a camisa leva o número pintado (uma textura por atleta; no Ultra em 1024)
                        int size = part == KitPart.Socks ? tex / 2 : part == KitPart.Shirt ? (quality >= 2 ? 1024 : 512) : tex;   // (listras, número e escudo nítidos de perto)
                        var kt = KitPainter.Paint(smr.sharedMesh, part, kit, axes, size, part == KitPart.Shirt ? number : -1);
                        // tecido dos dois lados (vê-se o avesso pela gola e pelas mangas)
                        if (part == KitPart.Shirt) rig.shirt = smr;
                        else if (part == KitPart.Shorts) rig.shorts = smr;
                        smr.sharedMaterial = Shared("kit:" + kt.GetInstanceID(), () => Gfx.Athlete(Color.white, kt, part == KitPart.Shirt ? 0.36f : 0.28f, 0f, true));
                        break;
                    }
                    case "Boots":
                    {
                        smr.sharedMesh = SmoothBoot(smr.sharedMesh, axes);
                        int d = look.boots;
                        bool indoor = IndoorShoes;
                        smr.sharedMaterial = Shared("boots:" + d + (indoor ? "f" : ""), () => Gfx.Athlete(Color.white, BootPainter.Paint(d, indoor), indoor ? 0.4f : 0.6f, 0f));
                        break;
                    }
                    default:
                        if (n.StartsWith("Hair_"))
                        {
                            string hn = n.Substring(5);
                            // a malha de cabelo do modelo fica por cima do cabelo procedural nos estilos com fios
                            // (raspado, undercut e cacheado ficam só com o procedural)
                            bool on = look.hair >= 0 && hn == Hairs[look.hair];
                            smr.gameObject.SetActive(on);
                            if (on)
                                smr.sharedMaterial = Shared("hair:" + hn + Teams.Hex(look.hairColor), () =>
                                    Gfx.Cutout(look.hairColor * 2.2f, T(hn == "afro01" ? "afro_diffuse" : hn + "_diffuse"), 0.3f, 0.02f));   // (fosco: sem o reflexo claro do refletor no alto da cabeça)
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
            // cabelo com volume e estilo (antes muitos pareciam carecas)
            if (rig.body != null && eyesR != null && bodyMesh != null && look.hairStyle >= 0) HairCap.Build(rig.body, eyesR, axes, look.hairStyle, look.hairColor * (look.hairStyle == 0 ? 0.8f : 1.15f), quality);
            rig.untucked = look.untucked;
            // (o número agora vem pintado na camisa)
        }

        static readonly Dictionary<Mesh, Mesh> _smoothBoots = new Dictionary<Mesh, Mesh>();
        static readonly Dictionary<Mesh, Mesh> _smoothN = new Dictionary<Mesh, Mesh>();

        /// <summary>
        /// Peça de roupa com normais contínuas: os vértices repetidos nas costuras de UV ganham a mesma
        /// normal (média). Antes cada costura virava uma "quina" no sombreado em faixas do cel shading — o
        /// uniforme parecia feito de blocos encaixados (peito estufado, barriga dobrada).
        /// </summary>
        static Mesh SmoothNormals(Mesh src)
        {
            if (src == null) return null;
            if (_smoothN.TryGetValue(src, out var have) && have != null) return have;
            var m = Object.Instantiate(src);
            m.name = src.name;
            var v = m.vertices;
            var nr = m.normals;
            if (nr == null || nr.Length != v.Length) { m.RecalculateNormals(); nr = m.normals; }
            var sum = new Dictionary<Vector3Int, Vector3>();
            for (int i = 0; i < v.Length; i++)
            {
                var k = Vector3Int.RoundToInt(v[i] * 5000f);
                sum[k] = (sum.TryGetValue(k, out var a) ? a : Vector3.zero) + nr[i];
            }
            for (int i = 0; i < v.Length; i++) nr[i] = sum[Vector3Int.RoundToInt(v[i] * 5000f)].normalized;
            m.normals = nr;
            _smoothN[src] = m;
            return m;
        }

        /// <summary>
        /// Chuteira arredondada: suavização de Taubin (alisa as quinas sem encolher a peça) nos vértices
        /// soldados por posição, mantendo a sola plana. O modelo vinha com bico e calcanhar em "caixote".
        /// </summary>
        static Mesh SmoothBoot(Mesh src, KitPainter.Axes ax)
        {
            if (src == null) return null;
            if (_smoothBoots.TryGetValue(src, out var have) && have != null) return have;
            var m = Object.Instantiate(src);
            m.name = src.name + " (lisa)";
            var v = m.vertices;
            int n = v.Length;
            var id = new int[n];
            var weld = new Dictionary<Vector3Int, int>();
            var pos = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                var k = Vector3Int.RoundToInt(v[i] * 20000f);
                if (!weld.TryGetValue(k, out int w)) { w = pos.Count; weld[k] = w; pos.Add(v[i]); }
                id[i] = w;
            }
            var nb = new HashSet<int>[pos.Count];
            for (int i = 0; i < nb.Length; i++) nb[i] = new HashSet<int>();
            var tris = m.triangles;
            for (int t = 0; t + 2 < tris.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = id[tris[t + k]], b = id[tris[t + (k + 1) % 3]];
                    if (a != b) { nb[a].Add(b); nb[b].Add(a); }
                }
            // sola: os vértices mais baixos não se mexem na vertical (o pé continua apoiado)
            var bounds = m.bounds;
            int up = ax.up;   // eixo "para cima" do modelo (do corpo)
            float sole = ax.upSign > 0 ? bounds.min[up] + bounds.size[up] * 0.12f : bounds.max[up] - bounds.size[up] * 0.12f;
            var p = pos.ToArray();
            var tmp = new Vector3[p.Length];
            void Pass(float lambda)
            {
                for (int i = 0; i < p.Length; i++)
                {
                    if (nb[i].Count == 0) { tmp[i] = p[i]; continue; }
                    var avg = Vector3.zero;
                    foreach (int j in nb[i]) avg += p[j];
                    avg /= nb[i].Count;
                    var d = (avg - p[i]) * lambda;
                    if (ax.upSign > 0 ? p[i][up] < sole : p[i][up] > sole) d[up] = 0f;
                    tmp[i] = p[i] + d;
                }
                System.Array.Copy(tmp, p, p.Length);
            }
            for (int it = 0; it < 4; it++) { Pass(0.5f); Pass(-0.53f); }
            for (int i = 0; i < n; i++) v[i] = p[id[i]];
            m.vertices = v;
            m.RecalculateNormals();
            m.RecalculateBounds();
            _smoothBoots[src] = m;
            return m;
        }

        static readonly (string a, string b)[] FacePairs =
        {
            ("Face_NoseWide", "Face_NoseNarrow"), ("Face_NoseLong", "Face_NoseShort"), ("Face_JawWide", "Face_JawNarrow"),
            ("Face_ChinLong", "Face_ChinShort"), ("Face_EyesBig", "Face_EyesSmall"), ("Face_LipsFull", "Face_LipsThin"),
        };
        static readonly string[] FaceSingles = { "Face_CheekBones", "Face_BrowHeavy", "Face_MouthWide", "Face_EarsBig", "Face_EyesSlant", "Face_ForeheadTall" };
        static readonly string[] FaceHeads = { "Face_Lean", "Face_Round", "Face_Oval", "Face_Square", "Face_Triangle" };

        /// <summary>Formato do rosto: pesos das shape keys a partir do DNA do jogador.</summary>
        static void ApplyFace(SkinnedMeshRenderer smr, float[] face, int salt)
        {
            var m = smr.sharedMesh;
            if (m == null || m.blendShapeCount == 0) return;
            uint seed = 2166136261u;
            if (face != null) foreach (var v in face) seed = (seed ^ (uint)(v * 1000)) * 16777619u;
            if (salt != 0) seed = (seed ^ (uint)salt) * 16777619u;
            float Next() { seed = seed * 1664525u + 1013904223u; return (seed >> 8) / 16777216f; }
            void W(string key, float w) { int idx = m.GetBlendShapeIndex(key); if (idx >= 0) smr.SetBlendShapeWeight(idx, w); }
            // traços em pares (um lado ou o outro, nunca os dois): nariz largo/fino, comprido/curto, mandíbula
            // larga/estreita, queixo longo/curto, olhos grandes/pequenos, lábios cheios/finos
            foreach (var pr in FacePairs)
            {
                float r = Next() * 2f - 1f;   // -1..1
                float w = Mathf.Max(0f, Mathf.Abs(r) - 0.2f) / 0.8f * 90f;
                W(pr.a, r > 0f ? w : 0f);
                W(pr.b, r < 0f ? w : 0f);
            }
            // traços soltos
            foreach (var k in FaceSingles) { float r = Next(); W(k, r < 0.45f ? 0f : (r - 0.45f) / 0.55f * 80f); }
            // um formato de cabeça (ou nenhum)
            float hs = Next();
            int pick = (int)(Next() * FaceHeads.Length) % FaceHeads.Length;
            for (int i = 0; i < FaceHeads.Length; i++) W(FaceHeads[i], i == pick && hs > 0.25f ? (hs - 0.25f) / 0.75f * 85f : 0f);
        }
    }
}
