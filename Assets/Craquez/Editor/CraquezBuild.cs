using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Craquez.EditorTools
{
    /// <summary>
    /// Gera os materiais-base URP (Resources/Materials) que o jogo copia em tempo
    /// de execução — assim as variantes de shader certas entram no build — e
    /// define as builds de Android (APK, IL2CPP ARM64) e Windows (x64).
    /// </summary>
    public class CraquezBuild : IPreprocessBuildWithReport
    {
        const string MatDir = "Assets/Craquez/Resources/Materials";
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report) => PrepareAssets();

        [InitializeOnLoadMethod]
        static void OnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                try { if (!File.Exists(MatDir + "/Lit.mat")) PrepareAssets(); }
                catch (Exception e) { Debug.LogWarning("Craquez: materiais não gerados ainda: " + e.Message); }
            };
        }

        [MenuItem("Craquez/Gerar materiais")]
        public static void PrepareAssets()
        {
            if (!AssetDatabase.IsValidFolder(MatDir))
            {
                Directory.CreateDirectory(MatDir);
                AssetDatabase.Refresh();
            }
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var sky = Shader.Find("Skybox/Procedural");
            if (lit == null) { Debug.LogError("Craquez: shader URP Lit não encontrado"); return; }

            Save("Lit", new Material(lit));

            var cut = new Material(lit);
            cut.SetFloat("_AlphaClip", 1f);
            cut.SetFloat("_Cutoff", 0.4f);
            cut.SetFloat("_Cull", (float)CullMode.Off);
            cut.EnableKeyword("_ALPHATEST_ON");
            cut.renderQueue = (int)RenderQueue.AlphaTest;
            cut.doubleSidedGI = true;
            Save("LitCutout", cut);

            var tr = new Material(lit);
            Transparent(tr);
            Save("LitTransparent", tr);

            var em = new Material(lit);
            em.EnableKeyword("_EMISSION");
            em.SetColor("_EmissionColor", Color.white);
            em.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Save("LitEmissive", em);

            if (unlit != null)
            {
                Save("Unlit", new Material(unlit));
                var ut = new Material(unlit);
                Transparent(ut);
                Save("UnlitTransparent", ut);
            }
            if (particles != null)
            {
                var p = new Material(particles);
                Transparent(p);
                Save("Particles", p);
            }
            if (sky != null)
            {
                var s = new Material(sky);
                s.SetFloat("_SunDisk", 2f);
                Save("Sky", s);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Craquez: materiais-base gerados em " + MatDir);
        }

        static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        static void Save(string name, Material m)
        {
            string path = MatDir + "/" + name + ".mat";
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (old != null)
            {
                old.shader = m.shader;
                old.CopyPropertiesFromMaterial(m);
                old.shaderKeywords = m.shaderKeywords;
                old.renderQueue = m.renderQueue;
                EditorUtility.SetDirty(old);
            }
            else AssetDatabase.CreateAsset(m, path);
        }

        static void Common()
        {
            PlayerSettings.companyName = "Craquez";
            PlayerSettings.productName = "Craquez";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            // sempre em tela cheia
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.allowFullscreenSwitch = false;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            string v = Environment.GetEnvironmentVariable("CRAQUEZ_VERSION");
            if (!string.IsNullOrEmpty(v)) PlayerSettings.bundleVersion = v;
            PrepareAssets();
        }

        static string Out(string def)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-craquezOut") return args[i + 1];
            return def;
        }

        static void Run(BuildTarget target, BuildTargetGroup group, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = path,
                target = target,
                targetGroup = group,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("Craquez build " + target + ": " + report.summary.result + " " + report.summary.totalSize / (1024 * 1024) + " MB, " + report.summary.totalErrors + " erros");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Craquez/Build Android (APK)")]
        public static void BuildAndroid()
        {
            Common();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.hellisonvidal.craquez");
            EditorUserBuildSettings.buildAppBundle = false;
            Run(BuildTarget.Android, BuildTargetGroup.Android, Out("Builds/Android/Craquez.apk"));
        }

        [MenuItem("Craquez/Build Windows")]
        public static void BuildWindows()
        {
            Common();
            Run(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, Out("Builds/Windows/Craquez.exe"));
        }
    }
}
