using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VG.EditorTools
{
    /// <summary>
    /// One-time URP bootstrap. The project was created from a bare manifest (no template), so the
    /// URP PACKAGE is installed but no pipeline ASSET was ever assigned — GraphicsSettings sat on
    /// the Built-in RP, and every "RenderPipeline"="UniversalPipeline" shader (VG/Toon, VG/Outline)
    /// silently rendered nothing. Runs on domain reload inside the open editor; no-ops once assigned.
    /// </summary>
    [InitializeOnLoad]
    public static class UrpAutoSetup
    {
        private const string SettingsDir = "Assets/Settings";
        private const string RendererPath = SettingsDir + "/UniversalRenderer.asset";
        private const string PipelinePath = SettingsDir + "/UniversalRP.asset";

        static UrpAutoSetup()
        {
            EditorApplication.delayCall += Setup;
        }

        private static void Setup()
        {
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)
                return; // already URP — no-op forever after

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                if (!AssetDatabase.IsValidFolder(SettingsDir))
                    AssetDatabase.CreateFolder("Assets", "Settings");

                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);

                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                AssetDatabase.SaveAssets();
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            AssetDatabase.SaveAssets();

            Debug.Log("VG: URP pipeline asset created and assigned (Graphics + Quality). " +
                      "VG/Toon + VG/Outline now render. This ran once; it no-ops from now on.");
        }
    }
}
