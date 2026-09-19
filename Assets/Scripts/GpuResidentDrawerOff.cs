using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class GpuResidentDrawerOff
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Disable()
    {
        BurstGpuDrivenLogFilter.Install();

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset
            ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            urp.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
        }

        typeof(GPUResidentDrawer)
            .GetProperty("MaintainContext", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(null, false);
        GPUResidentDrawer.ReinitializeIfNeeded();
    }
}

static class BurstGpuDrivenLogFilter
{
    static bool _installed;

    public static void Install()
    {
        if (_installed)
        {
            return;
        }

        Debug.unityLogger.logHandler = new Handler(Debug.unityLogger.logHandler);
        _installed = true;
    }

    sealed class Handler : ILogHandler
    {
        readonly ILogHandler _inner;

        public Handler(ILogHandler inner)
        {
            _inner = inner;
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            if (IsUnityGpuBurstFalsePositive(Format(format, args)))
            {
                return;
            }

            _inner.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            if (exception != null && IsUnityGpuBurstFalsePositive(exception.ToString()))
            {
                return;
            }

            _inner.LogException(exception, context);
        }

        static string Format(string format, object[] args)
        {
            if (string.IsNullOrEmpty(format))
            {
                return string.Empty;
            }

            if (args == null || args.Length == 0)
            {
                return format;
            }

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        static bool IsUnityGpuBurstFalsePositive(string message)
        {
            if (string.IsNullOrEmpty(message) || !message.Contains("not a known Burst entry point"))
            {
                return false;
            }

            return message.Contains("WorldProcessorBurst")
                || message.Contains("ClassifyMaterials")
                || message.Contains("GPUDriven")
                || message.Contains("GPUResidentDrawer")
                || message.Contains("LightMinMaxZJob")
                || message.Contains("ReflectionProbeMinMaxZJob")
                || message.Contains("ZBinningJob")
                || message.Contains("TilingJob")
                || message.Contains("TileRangeExpansionJob")
                || message.Contains("ForJobStruct");
        }
    }
}
