using System;
using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Carrega em tempo de execução o modelo 3D oficial do jogador (Meshy GLB com esqueleto Mixamo)
    /// e gera texturas personalizadas por raça/tom de pele, cabelo, padrão de uniforme e chuteira em cel shading.
    /// </summary>
    public static class MeshyAthleteLoader
    {
        sealed class NodeDef
        {
            public string name;
            public Vector3 pos;
            public Quaternion rot = Quaternion.identity;
            public Vector3 scale = Vector3.one;
            public int mesh = -1;
            public int skin = -1;
            public int[] children = Array.Empty<int>();
        }

        sealed class Template
        {
            public Mesh mesh;
            public Texture2D baseTex;
            public Color32[] basePixels;
            public int texW, texH;
            public byte[] zoneMap;
            public sbyte[] uvPosX; // coordenada 3D X quantizada (-127..127 para -0.5..0.5 m)
            public byte[] uvPosY;  // coordenada 3D Y quantizada (0..255 para 0..1.75 m)
            public sbyte[] uvPosZ; // coordenada 3D Z quantizada (-127..127 para -0.25..0.25 m)
            public NodeDef[] nodes;
            public int[] rootNodes;
            public int[] skinJoints;
            public int hipsNode = -1;
        }

        const byte ZoneNone = 0;
        const byte ZoneHead = 1;
        const byte ZoneShirt = 2;
        const byte ZoneArm = 3;
        const byte ZoneShorts = 4;
        const byte ZoneSocks = 5;
        const byte ZoneBoots = 6;
        const byte ZoneHair = 7;

        static Template _tpl;
        static bool _tried;
        static readonly Dictionary<string, Material> _matCache = new Dictionary<string, Material>();

        public static bool Available => EnsureTemplate() != null;

        public static GameObject InstantiateModel(Transform parent, out SkinnedMeshRenderer smr)
        {
            smr = null;
            var tpl = EnsureTemplate();
            if (tpl == null) return null;

            var root = new GameObject("model");
            root.transform.SetParent(parent, false);

            var nodeGos = new Transform[tpl.nodes.Length];
            for (int i = 0; i < tpl.nodes.Length; i++)
            {
                var nd = tpl.nodes[i];
                var go = new GameObject(string.IsNullOrEmpty(nd.name) ? "node_" + i : nd.name);
                go.transform.localPosition = nd.pos;
                go.transform.localRotation = nd.rot;
                go.transform.localScale = nd.scale;
                nodeGos[i] = go.transform;
            }
            for (int i = 0; i < tpl.nodes.Length; i++)
            {
                foreach (int ch in tpl.nodes[i].children)
                    if (ch >= 0 && ch < nodeGos.Length) nodeGos[ch].SetParent(nodeGos[i], false);
            }
            foreach (int r in tpl.rootNodes)
                if (r >= 0 && r < nodeGos.Length) nodeGos[r].SetParent(root.transform, false);

            for (int i = 0; i < tpl.nodes.Length; i++)
            {
                if (tpl.nodes[i].mesh < 0) continue;
                var r = nodeGos[i].gameObject.AddComponent<SkinnedMeshRenderer>();
                r.sharedMesh = tpl.mesh;
                var bones = new Transform[tpl.skinJoints.Length];
                for (int j = 0; j < tpl.skinJoints.Length; j++)
                    bones[j] = nodeGos[tpl.skinJoints[j]];
                r.bones = bones;
                r.rootBone = tpl.hipsNode >= 0 ? nodeGos[tpl.hipsNode] : (bones.Length > 0 ? bones[0] : nodeGos[i]);
                r.quality = SkinQuality.Bone4;
                r.updateWhenOffscreen = false;
                smr = r;
            }
            return root;
        }

        public static Material MaterialFor(AthleteFactory.Look look, Kit kit, int quality)
        {
            var tpl = EnsureTemplate();
            if (tpl == null) return null;
            int skinQ = Mathf.RoundToInt((look.skinTint.r + look.skinTint.g + look.skinTint.b) * 6f);
            string key = $"{look.baseName}:{skinQ}:{Teams.Hex(look.hairColor)}:{kit.pattern}:{Teams.Hex(kit.c1)}:{Teams.Hex(kit.c2)}:{Teams.Hex(kit.shorts)}:{Teams.Hex(kit.socks)}:{look.boots}:{quality >= 2}";
            if (_matCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var tex = BuildTexture(tpl, look, kit);
            var mat = Gfx.Athlete(Color.white, tex, 0.32f, 0.35f, false);
            _matCache[key] = mat;
            return mat;
        }

        static Texture2D BuildTexture(Template tpl, AthleteFactory.Look look, Kit kit)
        {
            int n = tpl.basePixels.Length;
            var dst = new Color32[n];
            var src = tpl.basePixels;
            var z = tpl.zoneMap;
            var px = tpl.uvPosX;
            var py = tpl.uvPosY;
            var pz = tpl.uvPosZ;

            Color raceSkin = RaceSkinTarget(look.baseName, look.skinTint);
            Color hairTarget = look.hairColor;
            Color c1 = kit.c1;
            Color c2 = kit.c2;
            Color shortsCol = kit.shorts;
            Color socksCol = kit.socks;
            int pat = kit.pattern;
            int bi = ((look.boots % BootPainter.Designs.Length) + BootPainter.Designs.Length) % BootPainter.Designs.Length;
            Color bootCol = BootPainter.Designs[bi].baseC;

            for (int i = 0; i < n; i++)
            {
                Color32 c = src[i];
                byte zone = z[i];
                if (zone == ZoneNone) { dst[i] = c; continue; }

                float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
                float mx = Mathf.Max(r, Mathf.Max(g, b));
                float mn = Mathf.Min(r, Mathf.Min(g, b));
                float sat = mx > 0.01f ? (mx - mn) / mx : 0f;
                float lum = r * 0.30f + g * 0.59f + b * 0.11f;

                float vx = px[i] * (0.5f / 127f);
                float vy = py[i] * (1.75f / 255f);
                float vz = pz[i] * (0.25f / 127f);

                if (zone == ZoneBoots)
                {
                    Color outC = lum > 0.45f ? bootCol * Mathf.Clamp(lum * 1.25f, 0.45f, 1.1f)
                        : Color.Lerp(new Color(r, g, b), bootCol * 0.35f, 0.35f);
                    dst[i] = To32(outC);
                    continue;
                }

                // Zona dedicada de cabelo (topo e parte de trás da cabeça / nuca): nunca vira pele ou loiro falso
                if (zone == ZoneHair)
                {
                    // Se houver pele clara na nuca baixa, aplica tom de pele da raça
                    if (lum > 0.50f && r > g + 0.06f && g >= b + 0.04f && sat > 0.26f)
                    {
                        float shade = Mathf.Clamp(lum / 0.58f, 0.40f, 1.12f);
                        dst[i] = To32(raceSkin * shade);
                        continue;
                    }
                    float hShade = Mathf.Clamp(lum / 0.24f, 0.42f, 0.96f);
                    dst[i] = To32(hairTarget * hShade);
                    continue;
                }

                // Cabelo / sobrancelha / cílios na frente/laterais da cabeça (escuro, antes da detecção de pele)
                if (zone == ZoneHead && lum <= 0.34f && mx < 0.42f)
                {
                    float shade = Mathf.Clamp(lum / 0.18f, 0.42f, 0.96f);
                    dst[i] = To32(hairTarget * shade);
                    continue;
                }

                // Pele: tons quentes claros (lum > 0.34) em cabeça/rosto ou braços/mãos
                bool isWarmSkin = (zone == ZoneHead || zone == ZoneArm || zone == ZoneShirt)
                    && r > g + 0.05f && g >= b + 0.04f && lum > 0.34f && sat > 0.22f;
                if (isWarmSkin)
                {
                    float shade = Mathf.Clamp(lum / 0.58f, 0.38f, 1.15f);
                    dst[i] = To32(raceSkin * shade);
                    continue;
                }

                // Tecido do uniforme (branco / cinza claro na textura base): aplica padrão, gola, punhos e faixas
                if (sat < 0.22f && lum > 0.40f)
                {
                    float shade = Mathf.Clamp(lum / 0.90f, 0.54f, 1.04f);
                    if (zone == ZoneShirt)
                    {
                        Color col = ShirtColor(pat, c1, c2, vx, vy, vz);
                        dst[i] = To32(col * shade);
                        continue;
                    }
                    if (zone == ZoneShorts)
                    {
                        // Friso lateral e barra discreta no calção com a cor secundária
                        bool sideTrim = Mathf.Abs(vz) < 0.025f && Mathf.Abs(vx) > 0.145f;
                        bool hemTrim = vy < 0.58f && vy > 0.55f;
                        Color col = (sideTrim || hemTrim) ? Color.Lerp(shortsCol, c2, 0.75f) : shortsCol;
                        dst[i] = To32(col * shade);
                        continue;
                    }
                    if (zone == ZoneSocks)
                    {
                        // Faixa dupla no topo do meião
                        bool band = (vy > 0.44f && vy < 0.46f) || (vy > 0.475f && vy < 0.49f);
                        Color col = band ? c2 : socksCol;
                        dst[i] = To32(col * shade);
                        continue;
                    }
                }

                dst[i] = c;
            }

            var tex = new Texture2D(tpl.texW, tpl.texH, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(dst);
            tex.Apply(true, true);
            return tex;
        }

        static Color ShirtColor(int pat, Color c1, Color c2, float vx, float vy, float vz)
        {
            // Gola (em torno do pescoço) e punhos das mangas na cor secundária
            if (vy > 1.375f && Mathf.Abs(vx) < 0.115f) return c2;
            if (Mathf.Abs(vx) > 0.34f && vy < 1.24f) return c2;

            switch (pat)
            {
                case 1: // listras verticais
                    return Mathf.FloorToInt((vx + 1f) * 14f) % 2 == 0 ? c1 : c2;
                case 2: // aros horizontais
                    return Mathf.FloorToInt((vy - 0.90f) * 13f) % 2 == 0 ? c1 : c2;
                case 3: // faixa diagonal
                    return Mathf.Abs(vx * 1.15f + (vy - 1.18f) * 0.95f) < 0.065f ? c2 : c1;
                case 4: // metades
                    return vx < 0f ? c1 : c2;
                case 5: // riscas finas
                    return Mathf.Abs(Mathf.Repeat(vx * 22f, 1f) - 0.5f) < 0.14f ? c2 : c1;
                case 6: // faixa horizontal no peito
                    return (vy > 1.16f && vy < 1.27f && Mathf.Abs(vx) < 0.22f) ? c2 : c1;
                case 7: // faixa vertical central
                    return Mathf.Abs(vx) < 0.065f ? c2 : c1;
                case 8: // xadrez
                    return ((Mathf.FloorToInt((vx + 1f) * 10f) + Mathf.FloorToInt(vy * 10f)) & 1) == 0 ? c1 : c2;
                default: // liso com detalhe sutil nos ombros
                    return (vy > 1.33f && Mathf.Abs(vx) > 0.13f && Mathf.Abs(vz) < 0.03f) ? c2 : c1;
            }
        }

        static Color RaceSkinTarget(string race, Color skinTint)
        {
            Color baseTone;
            switch (race)
            {
                case "afro": baseTone = new Color(0.32f, 0.20f, 0.14f); break;
                case "latino": baseTone = new Color(0.75f, 0.53f, 0.38f); break;
                case "asiatico": baseTone = new Color(0.92f, 0.79f, 0.62f); break;
                default: baseTone = new Color(0.94f, 0.78f, 0.68f); break;
            }
            return new Color(
                Mathf.Clamp01(baseTone.r * skinTint.r),
                Mathf.Clamp01(baseTone.g * skinTint.g),
                Mathf.Clamp01(baseTone.b * skinTint.b));
        }

        static Color32 To32(Color c) => new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
            255);

        static Template EnsureTemplate()
        {
            if (_tried) return _tpl;
            _tried = true;
            var asset = Resources.Load<TextAsset>("Athletes/Athlete_Meshy.glb");
            if (asset == null) return null;
            try { _tpl = ParseGlb(asset.bytes); }
            catch (Exception e) { Debug.LogError("Falha ao carregar Athlete_Meshy.glb: " + e); _tpl = null; }
            return _tpl;
        }

        static Template ParseGlb(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 28) return null;
            int jsonLen = BitConverter.ToInt32(bytes, 12);
            string jsonStr = System.Text.Encoding.UTF8.GetString(bytes, 20, jsonLen);
            int binHeader = 20 + jsonLen;
            int binOff = binHeader + 8;

            var root = MiniJson.Parse(jsonStr) as Dictionary<string, object>;
            var bvs = (List<object>)root["bufferViews"];
            var accs = (List<object>)root["accessors"];
            var nodesList = (List<object>)root["nodes"];
            var meshesList = (List<object>)root["meshes"];
            var skinsList = (List<object>)root["skins"];
            var scenesList = (List<object>)root["scenes"];
            var imagesList = (List<object>)root["images"];

            (int off, int len) AccSpan(int accIdx)
            {
                var a = (Dictionary<string, object>)accs[accIdx];
                var bv = (Dictionary<string, object>)bvs[Convert.ToInt32(a["bufferView"])];
                int o = binOff + Convert.ToInt32(bv["byteOffset"]) + (a.TryGetValue("byteOffset", out var bo) ? Convert.ToInt32(bo) : 0);
                int l = Convert.ToInt32(bv["byteLength"]);
                return (o, l);
            }

            var nodes = new NodeDef[nodesList.Count];
            int hipsNode = -1;
            for (int i = 0; i < nodesList.Count; i++)
            {
                var nd = (Dictionary<string, object>)nodesList[i];
                var def = new NodeDef { name = nd.TryGetValue("name", out var nm) ? nm as string : "" };
                if (def.name == "mixamorig:Hips") hipsNode = i;
                if (nd.TryGetValue("translation", out var tr) && tr is List<object> tl && tl.Count == 3)
                    def.pos = new Vector3(-F(tl[0]), F(tl[1]), F(tl[2]));
                if (nd.TryGetValue("rotation", out var rt) && rt is List<object> rl && rl.Count == 4)
                    def.rot = new Quaternion(F(rl[0]), -F(rl[1]), -F(rl[2]), F(rl[3]));
                if (nd.TryGetValue("scale", out var sc) && sc is List<object> sl && sl.Count == 3)
                    def.scale = new Vector3(F(sl[0]), F(sl[1]), F(sl[2]));
                if (nd.TryGetValue("mesh", out var mh) && mh != null) def.mesh = Convert.ToInt32(mh);
                if (nd.TryGetValue("skin", out var sk) && sk != null) def.skin = Convert.ToInt32(sk);
                if (nd.TryGetValue("children", out var ch) && ch is List<object> cl)
                {
                    def.children = new int[cl.Count];
                    for (int k = 0; k < cl.Count; k++) def.children[k] = Convert.ToInt32(cl[k]);
                }
                nodes[i] = def;
            }

            var scene0 = (Dictionary<string, object>)scenesList[0];
            var sceneNodes = (List<object>)scene0["nodes"];
            var rootNodes = new int[sceneNodes.Count];
            for (int i = 0; i < sceneNodes.Count; i++) rootNodes[i] = Convert.ToInt32(sceneNodes[i]);

            var skin0 = (Dictionary<string, object>)skinsList[0];
            var jointsList = (List<object>)skin0["joints"];
            var skinJoints = new int[jointsList.Count];
            for (int i = 0; i < jointsList.Count; i++) skinJoints[i] = Convert.ToInt32(jointsList[i]);
            int ibmAcc = Convert.ToInt32(skin0["inverseBindMatrices"]);
            var (ibmOff, _) = AccSpan(ibmAcc);
            var bindposes = new Matrix4x4[skinJoints.Length];
            for (int i = 0; i < skinJoints.Length; i++)
            {
                int o = ibmOff + i * 64;
                var m = new Matrix4x4();
                for (int c = 0; c < 4; c++)
                    for (int r = 0; r < 4; r++)
                        m[r, c] = BitConverter.ToSingle(bytes, o + (c * 4 + r) * 4);
                m.m01 = -m.m01; m.m02 = -m.m02; m.m03 = -m.m03;
                m.m10 = -m.m10; m.m20 = -m.m20; m.m30 = -m.m30;
                bindposes[i] = m;
            }

            var mesh0 = (Dictionary<string, object>)meshesList[0];
            var prim0 = (Dictionary<string, object>)((List<object>)mesh0["primitives"])[0];
            var attrs = (Dictionary<string, object>)prim0["attributes"];
            int posAcc = Convert.ToInt32(attrs["POSITION"]);
            int nrmAcc = Convert.ToInt32(attrs["NORMAL"]);
            int uvAcc = Convert.ToInt32(attrs["TEXCOORD_0"]);
            int jntAcc = Convert.ToInt32(attrs["JOINTS_0"]);
            int wgtAcc = Convert.ToInt32(attrs["WEIGHTS_0"]);
            int idxAcc = Convert.ToInt32(prim0["indices"]);

            int vCount = Convert.ToInt32(((Dictionary<string, object>)accs[posAcc])["count"]);
            int iCount = Convert.ToInt32(((Dictionary<string, object>)accs[idxAcc])["count"]);
            var (pOff, _) = AccSpan(posAcc);
            var (nOff, _) = AccSpan(nrmAcc);
            var (uOff, _) = AccSpan(uvAcc);
            var (jOff, _) = AccSpan(jntAcc);
            var (wOff, _) = AccSpan(wgtAcc);
            var (xOff, _) = AccSpan(idxAcc);

            var verts = new Vector3[vCount];
            var norms = new Vector3[vCount];
            var uvs = new Vector2[vCount];
            var bws = new BoneWeight[vCount];
            for (int i = 0; i < vCount; i++)
            {
                verts[i] = new Vector3(-BitConverter.ToSingle(bytes, pOff + i * 12), BitConverter.ToSingle(bytes, pOff + i * 12 + 4), BitConverter.ToSingle(bytes, pOff + i * 12 + 8));
                norms[i] = new Vector3(-BitConverter.ToSingle(bytes, nOff + i * 12), BitConverter.ToSingle(bytes, nOff + i * 12 + 4), BitConverter.ToSingle(bytes, nOff + i * 12 + 8)).normalized;
                uvs[i] = new Vector2(BitConverter.ToSingle(bytes, uOff + i * 8), 1f - BitConverter.ToSingle(bytes, uOff + i * 8 + 4));

                int jo = jOff + i * 4;
                int wo = wOff + i * 16;
                float w0 = BitConverter.ToSingle(bytes, wo);
                float w1 = BitConverter.ToSingle(bytes, wo + 4);
                float w2 = BitConverter.ToSingle(bytes, wo + 8);
                float w3 = BitConverter.ToSingle(bytes, wo + 12);
                float sum = w0 + w1 + w2 + w3;
                float inv = sum > 1e-5f ? 1f / sum : 1f;
                bws[i] = new BoneWeight
                {
                    boneIndex0 = bytes[jo], boneIndex1 = bytes[jo + 1], boneIndex2 = bytes[jo + 2], boneIndex3 = bytes[jo + 3],
                    weight0 = w0 * inv, weight1 = w1 * inv, weight2 = w2 * inv, weight3 = w3 * inv,
                };
            }

            var tris = new int[iCount];
            for (int t = 0; t + 2 < iCount; t += 3)
            {
                tris[t] = BitConverter.ToUInt16(bytes, xOff + t * 2);
                tris[t + 1] = BitConverter.ToUInt16(bytes, xOff + (t + 2) * 2);
                tris[t + 2] = BitConverter.ToUInt16(bytes, xOff + (t + 1) * 2);
            }

            var mesh = new Mesh { name = "MeshyAthlete" };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.boneWeights = bws;
            mesh.bindposes = bindposes;
            mesh.RecalculateBounds();

            var im0 = (Dictionary<string, object>)imagesList[0];
            var imBv = (Dictionary<string, object>)bvs[Convert.ToInt32(im0["bufferView"])];
            int imOff = binOff + Convert.ToInt32(imBv["byteOffset"]);
            int imLen = Convert.ToInt32(imBv["byteLength"]);
            var png = new byte[imLen];
            Buffer.BlockCopy(bytes, imOff, png, 0, imLen);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(png, false);
            int tw = tex.width, th = tex.height;
            var basePixels = tex.GetPixels32();

            var jointZone = new byte[skinJoints.Length];
            for (int j = 0; j < skinJoints.Length; j++)
                jointZone[j] = ClassifyBone(nodes[skinJoints[j]].name);

            BuildZoneMap(verts, uvs, tris, bws, jointZone, tw, th,
                out var zoneMap, out var uvPosX, out var uvPosY, out var uvPosZ);

            return new Template
            {
                mesh = mesh,
                baseTex = tex,
                basePixels = basePixels,
                texW = tw,
                texH = th,
                zoneMap = zoneMap,
                uvPosX = uvPosX,
                uvPosY = uvPosY,
                uvPosZ = uvPosZ,
                nodes = nodes,
                rootNodes = rootNodes,
                skinJoints = skinJoints,
                hipsNode = hipsNode,
            };
        }

        static byte ClassifyBone(string name)
        {
            if (string.IsNullOrEmpty(name)) return ZoneNone;
            if (name.Contains("Head") || name.Contains("Neck") || name == "headfront") return ZoneHead;
            if (name.Contains("ForeArm") || name.Contains("Hand")) return ZoneArm;
            if (name.Contains("Foot") || name.Contains("Toe")) return ZoneBoots;
            if (name.Contains("Leg") && !name.Contains("UpLeg")) return ZoneSocks;
            if (name.Contains("UpLeg") || name.Contains("Hips")) return ZoneShorts;
            if (name.Contains("Spine") || name.Contains("Shoulder") || name.Contains("Arm")) return ZoneShirt;
            return ZoneNone;
        }

        static void BuildZoneMap(Vector3[] verts, Vector2[] uvs, int[] tris, BoneWeight[] bws, byte[] jointZone, int w, int h,
            out byte[] map, out sbyte[] posX, out byte[] posY, out sbyte[] posZ)
        {
            map = new byte[w * h];
            posX = new sbyte[w * h];
            posY = new byte[w * h];
            posZ = new sbyte[w * h];

            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                Vector3 v0 = verts[i0], v1 = verts[i1], v2 = verts[i2];
                Vector3 vc = (v0 + v1 + v2) * (1f / 3f);

                byte z = ZoneForVertex(vc, bws[i0], jointZone);
                if (z == ZoneNone) z = ZoneForVertex(v1, bws[i1], jointZone);

                sbyte qx = (sbyte)Mathf.Clamp(Mathf.RoundToInt(vc.x / 0.5f * 127f), -127, 127);
                byte qy = (byte)Mathf.Clamp(Mathf.RoundToInt(vc.y / 1.75f * 255f), 0, 255);
                sbyte qz = (sbyte)Mathf.Clamp(Mathf.RoundToInt(vc.z / 0.25f * 127f), -127, 127);

                Vector2 a = uvs[i0], b = uvs[i1], c = uvs[i2];
                int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) * w) - 1);
                int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) * w) + 1);
                int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) * h) - 1);
                int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) * h) + 1);

                float area = Edge(a, b, c);
                if (Mathf.Abs(area) < 1e-8f) continue;
                float invA = 1f / area;

                for (int y = minY; y <= maxY; y++)
                {
                    float vy = (y + 0.5f) / h;
                    for (int x = minX; x <= maxX; x++)
                    {
                        var p = new Vector2((x + 0.5f) / w, vy);
                        float w0 = Edge(b, c, p) * invA;
                        float w1 = Edge(c, a, p) * invA;
                        float w2 = 1f - w0 - w1;
                        if (w0 >= -0.05f && w1 >= -0.05f && w2 >= -0.05f)
                        {
                            int idx = y * w + x;
                            map[idx] = z;
                            posX[idx] = qx;
                            posY[idx] = qy;
                            posZ[idx] = qz;
                        }
                    }
                }
            }
        }

        static byte ZoneForVertex(Vector3 v, BoneWeight bw, byte[] jointZone)
        {
            byte z = bw.boneIndex0 < jointZone.Length ? jointZone[bw.boneIndex0] : ZoneNone;
            if (v.y < 0.14f) return ZoneBoots;
            if (v.y < 0.52f && (z == ZoneSocks || z == ZoneBoots)) return ZoneSocks;
            if (v.y >= 0.52f && v.y < 0.96f && z == ZoneShorts) return ZoneShorts;
            // Topo da cabeça e parte traseira da cabeça/nuca ficam estritamente na zona de cabelo
            if (v.y >= 1.56f || (v.y >= 1.43f && v.z < -0.015f)) return ZoneHair;
            if (v.y >= 1.43f) return ZoneHead;
            return z != ZoneNone ? z : (v.y > 0.96f ? ZoneShirt : ZoneShorts);
        }

        static float Edge(Vector2 a, Vector2 b, Vector2 c) => (c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x);
        static float F(object o) => Convert.ToSingle(o);
    }
}
