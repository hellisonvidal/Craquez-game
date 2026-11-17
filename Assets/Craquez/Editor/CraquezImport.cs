using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Craquez.EditorTools
{
    /// <summary>
    /// Configuração automática de importação:
    ///  • atletas (FBX MakeHuman): Generic, malha legível (pintura do uniforme), blend shapes, sem materiais;
    ///  • Athlete_Anims.fbx: um clipe por ação, nomes limpos, laço conforme anims.json,
    ///    e só rotações (+ posição do quadril) para servir a todos os biotipos;
    ///  • texturas dos atletas: cabelo com alfa, tamanho menor no Android.
    /// </summary>
    public class CraquezImport : AssetPostprocessor
    {
        const string AthleteDir = "Assets/Craquez/Resources/Athletes/";

        /// <summary>Muda quando as regras mudam: força reimportar os FBX.</summary>
        public override uint GetVersion() => 4;   // (4: cobertura do alfa nos mipmaps e ASTC 4x4 nas texturas dos atletas)

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(AthleteDir)) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importBlendShapes = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.optimizeGameObjects = false;
            mi.importConstraints = false;
            if (IsAnims(assetPath))
            {
                mi.importAnimation = true;
                mi.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
                mi.resampleCurves = true;
            }
            else
            {
                mi.importAnimation = false;
                mi.isReadable = true;   // KitPainter lê vértices/UV
                mi.weldVertices = true;
            }
        }

        void OnPreprocessAnimation()
        {
            if (!IsAnims(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            var loops = LoopTable();
            var clips = mi.defaultClipAnimations;
            var outClips = new List<ModelImporterClipAnimation>();
            foreach (var c in clips)
            {
                string n = c.name;
                int bar = n.LastIndexOf('|');
                if (bar >= 0) n = n.Substring(bar + 1);
                c.name = n;
                c.loopTime = loops.TryGetValue(n, out var l) && l;
                c.loopPose = false;
                c.lockRootRotation = false;
                c.keepOriginalPositionY = true;
                outClips.Add(c);
            }
            mi.clipAnimations = outClips.ToArray();
        }

        /// <summary>FBX de animações: o principal e o complementar (Athlete_Anims2: dribles e fintas extras da CMU).</summary>
        static bool IsAnims(string path) => path.EndsWith("Athlete_Anims.fbx") || path.EndsWith("Athlete_Anims2.fbx");

        static Dictionary<string, bool> LoopTable()
        {
            var d = new Dictionary<string, bool>();
            foreach (var file in new[] { "anims.json", "anims2.json" })
            {
                var path = AthleteDir + file;
                if (!File.Exists(path)) continue;
                var root = Craquez.Sim.MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
                if (root == null) continue;
                foreach (var kv in root)
                    if (kv.Value is Dictionary<string, object> m && m.TryGetValue("loop", out var l) && l is bool b) d[kv.Key] = b;
            }
            return d;
        }

        /// <summary>Remove curvas de posição/escala (exceto o quadril): o clipe serve a qualquer biotipo.</summary>
        void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!IsAnims(assetPath)) return;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                bool pos = b.propertyName.StartsWith("m_LocalPosition");
                bool scale = b.propertyName.StartsWith("m_LocalScale");
                bool pelvis = b.path.EndsWith("/pelvis") || b.path == "pelvis";
                // nó do esqueleto ("Armature") e osso raiz: guardam a conversão de eixos do Blender
                // (Z para cima); se a animação mexer neles, o atleta "deita". Só os ossos animam.
                bool armatureNode = b.path == "" || b.path == "Armature" || b.path == "Root" || b.path.EndsWith("/Root");
                if (armatureNode || scale || (pos && !pelvis)) AnimationUtility.SetEditorCurve(clip, b, null);
            }
        }

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith("Assets/Craquez/Resources/Flags/"))
            {
                // bandeiras: legíveis (o jogo desenha a ondulação e a bandeira do clube por cima)
                var fi = (TextureImporter)assetImporter;
                fi.isReadable = true;
                fi.mipmapEnabled = false;
                fi.textureCompression = TextureImporterCompression.Uncompressed;
                fi.npotScale = TextureImporterNPOTScale.None;
                return;
            }
            if (assetPath.StartsWith("Assets/Craquez/Resources/UI/"))
            {
                // capa e logo: resolução cheia (a capa panorâmica tem 2400 px), sem mipmaps
                var ui = (TextureImporter)assetImporter;
                ui.maxTextureSize = 4096;
                ui.mipmapEnabled = false;
                ui.npotScale = TextureImporterNPOTScale.None;
                ui.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }
            if (!assetPath.StartsWith(AthleteDir + "Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            string n = Path.GetFileNameWithoutExtension(assetPath);
            bool skin = n.Contains("skinned");
            bool eye = n.EndsWith("_eye");   // (brown_eye: íris, sem recorte)
            bool cut = !skin && !eye;   // cabelo, sobrancelha, cílios: recorte pelo alfa
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = cut;
            ti.maxTextureSize = skin ? 2048 : 1024;
            ti.anisoLevel = 4;
            if (cut)
            {
                // os mipmaps menores "comiam" o alfa: de longe o cabelo ralo e com falhas (via-se o couro
                // cabeludo) e as sobrancelhas sumiam. Preserva a cobertura no limiar do recorte (0,3)
                ti.mipMapsPreserveCoverage = true;
                ti.alphaTestReferenceValue = 0.3f;
            }
            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true;
            // pele: o rosto ocupa só um canto do atlas — em 1024 com ASTC 6x6 ficava borrado e em blocos;
            // texturas com recorte em ASTC 4x4 (o 6x6 deixava a borda dos fios serrilhada)
            android.maxTextureSize = skin ? 2048 : 1024;
            android.format = skin || cut ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            ti.SetPlatformTextureSettings(android);
        }
    }
}
