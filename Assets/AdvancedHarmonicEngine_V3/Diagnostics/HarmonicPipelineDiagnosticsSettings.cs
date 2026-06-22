using System;
using UnityEngine;

namespace HarmonicEngine.Diagnostics
{
    [Serializable]
    public struct HarmonicPipelineDiagnosticsSettings
    {
        [Header("Hub")]
        public bool enableDiagnostics;
        public bool enableFileLog;
        public bool enableTelemetry;

        [Header("Console")]
        public bool logToUnityConsole;
        public bool logSphToConsole;
        public bool muteSphTelemetry;

        [Header("Pipeline logs")]
        public bool verbosePipelineDiagnostics;
        [Min(1)] public int frameDiagnosticInterval;
        [Min(0)] public int positionSampleInterval;
        [Min(1)] public int positionSampleCount;
        public bool logStencilNeighborCount;

        [Header("Perf sampling")]
        public bool perfDiagnosticsMuted;

        [Header("Profile telemetry")]
        public bool enableProfileTelemetry;
        [Min(1)] public int profileLogInterval;
        public bool enableFrameTimingStats;
        [Min(1f)] public float spikeThresholdMs;

        [Header("Overlay")]
        public bool showOverlay;
        public int overlayFontSize;
        [Min(1)] public int smoothingFrames;

        public static HarmonicPipelineDiagnosticsSettings CreateDefault() => new()
        {
            enableDiagnostics = true,
            enableFileLog = true,
            enableTelemetry = true,
            logToUnityConsole = true,
            logSphToConsole = true,
            muteSphTelemetry = false,
            verbosePipelineDiagnostics = true,
            frameDiagnosticInterval = 15,
            positionSampleInterval = 10,
            positionSampleCount = 64,
            logStencilNeighborCount = false,
            perfDiagnosticsMuted = false,
            enableProfileTelemetry = true,
            profileLogInterval = 15,
            enableFrameTimingStats = true,
            spikeThresholdMs = 22f,
            showOverlay = true,
            overlayFontSize = 14,
            smoothingFrames = 30
        };
    }
}
