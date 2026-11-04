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
        public override uint GetVersion() => 3;

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
            if (assetPath.EndsWith("Athlete_Anims.fbx"))
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
            if (!assetPath.EndsWith("Athlete_Anims.fbx")) return;
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

        static Dictionary<string, bool> LoopTable()
        {
            var d = new Dictionary<string, bool>();
            var path = AthleteDir + "anims.json";
            if (!File.Exists(path)) return d;
            var root = Craquez.Sim.MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
            if (root == null) return d;
            foreach (var kv in root)
                if (kv.Value is Dictionary<string, object> m && m.TryGetValue("loop", out var l) && l is bool b) d[kv.Key] = b;
            return d;
        }

        /// <summary>Remove curvas de posição/escala (exceto o quadril): o clipe serve a qualquer biotipo.</summary>
        void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!assetPath.EndsWith("Athlete_Anims.fbx")) return;
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
            if (!assetPath.StartsWith(AthleteDir + "Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            string n = Path.GetFileNameWithoutExtension(assetPath);
            bool skin = n.Contains("skinned");
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = !skin;
            ti.maxTextureSize = skin ? 2048 : 1024;
            ti.anisoLevel = 4;
            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = skin ? 1024 : 512;
            android.format = TextureImporterFormat.ASTC_6x6;
            ti.SetPlatformTextureSettings(android);
        }
    }
}
