#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Craquez.Tests
{
    /// <summary>Compila de verdade os shaders de cel shading (Vulkan e OpenGL; o OpenGL ES é conferido no build Android) com as variantes principais.</summary>
    public class ShaderTests
    {
        static readonly string[][] Variants =
        {
            new string[0],
            new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT", "_ADDITIONAL_LIGHTS", "_ALPHATEST_ON" },
            new[] { "_MAIN_LIGHT_SHADOWS", "_ADDITIONAL_LIGHTS", "_CLUSTER_LIGHT_LOOP", "_EMISSION", "FOG_LINEAR" },
        };

        [TestCase("Craquez/Toon")]
        [TestCase("Craquez/ToonOutline")]
        public void ToonCompila(string name)
        {
            var s = Shader.Find(name);
            Assert.IsNotNull(s, name + " não encontrado");
            var data = ShaderUtil.GetShaderData(s);
            var sub = data.GetSerializedSubshader(0);
            int checkedCount = 0;
            for (int p = 0; p < sub.PassCount; p++)
            {
                var pass = sub.GetPass(p);
                foreach (var plat in new[] { ShaderCompilerPlatform.Vulkan, ShaderCompilerPlatform.OpenGLCore })
                    foreach (var kw in Variants)
                        foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                        {
                            var r = pass.CompileVariant(stage, kw, plat, BuildTarget.StandaloneLinux64);
                            var errs = r.Messages.Where(m => m.severity == ShaderCompilerMessageSeverity.Error).Select(m => m.message + " (" + m.file + ":" + m.line + ")").ToArray();
                            Assert.IsTrue(r.Success && errs.Length == 0, $"{name} passo {pass.Name} {plat} {stage} [{string.Join(",", kw)}]: {string.Join(" | ", errs)}");
                            checkedCount++;
                        }
            }
            Debug.Log($"{name}: {checkedCount} variantes compiladas");
            Assert.IsFalse(ShaderUtil.ShaderHasError(s), name + " tem erros");
        }
    }
}
#endif
