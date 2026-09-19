#if UNITY_EDITOR
using System;
using System.Reflection;
using Unity.Burst;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Class name starts with Aaa so this InitializeOnLoad runs as early as user
// scripts can. BurstLoader still starts first on the opening launch; EditorPrefs
// makes the next Editor restart skip GPU-driven Burst compilation entirely.
[InitializeOnLoad]
static class AaaDisableBurstCompilation
{
    const string BurstCompilationPref = "BurstCompilation";

    static AaaDisableBurstCompilation()
    {
        PersistBurstCompilationOff();
        BurstGpuDrivenLogFilter.Install();
        EditorApplication.delayCall += DisableGpuDrivenRendering;
    }

    static void PersistBurstCompilationOff()
    {
        EditorPrefs.SetBool(BurstCompilationPref, false);
        BurstCompiler.Options.EnableBurstCompilation = false;

        var optionsType = Type.GetType("Unity.Burst.Editor.BurstEditorOptions, Unity.Burst.Editor");
        var property = optionsType?.GetProperty(
            "EnableBurstCompilation",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite)
        {
            property.SetValue(null, false);
        }
    }

    static void DisableGpuDrivenRendering()
    {
        BurstGpuDrivenLogFilter.Install();
        PersistBurstCompilationOff();

        var drawer = typeof(GPUResidentDrawer);
        drawer.GetProperty("MaintainContext", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(null, false);

        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                continue;
            }

            if (asset.gpuResidentDrawerMode != GPUResidentDrawerMode.Disabled)
            {
                asset.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
                EditorUtility.SetDirty(asset);
            }
        }

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset current)
        {
            current.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
        }

        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset fallback)
        {
            fallback.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
        }

        drawer.GetMethod("Reinitialize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?.Invoke(null, null);
        GPUResidentDrawer.ReinitializeIfNeeded();
    }
}
#endif
