using HarmonicEngine.Infrastructure.Management;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarmonicEngine.Diagnostics
{
    public sealed class HarmonicDiagnosticSession
    {
        public PipelineExecutionController Pipeline { get; internal set; }
        public HarmonicPipelineDiagnosticsSettings DiagnosticsSettings { get; internal set; }
        public int FrameIndex { get; internal set; }
        public float StartTime { get; }

        /// <summary>Root log folder (parent of all run directories).</summary>
        public string LogDirectory { get; }

        /// <summary>Per-run folder containing channel log files and manifest.json.</summary>
        public string RunDirectory { get; }

        internal HarmonicDiagnosticSession(
            PipelineExecutionController pipeline,
            string logRootDirectory,
            string runDirectory,
            HarmonicPipelineDiagnosticsSettings diagnosticsSettings)
        {
            Pipeline = pipeline;
            DiagnosticsSettings = diagnosticsSettings;
            LogDirectory = logRootDirectory;
            RunDirectory = runDirectory;
            StartTime = Time.realtimeSinceStartup;
        }

        public float ElapsedSeconds => Time.realtimeSinceStartup - StartTime;

        public uint ReadActiveParticleCount() =>
            Pipeline != null ? Pipeline.GetActiveParticleCount() : 0u;

        public string BuildHeader()
        {
            return
                $"unityVersion={Application.unityVersion}\n" +
                $"platform={Application.platform}\n" +
                $"scene={SceneManager.GetActiveScene().name}\n" +
                $"gpu={SystemInfo.graphicsDeviceName}\n" +
                $"logRoot={LogDirectory}\n" +
                $"runDirectory={RunDirectory}\n" +
                $"maxCapacity={Pipeline?.MaxCapacity ?? 0}\n" +
                $"worldFallingOnly={Pipeline?.WorldFallingOnly ?? false}";
        }
    }
}
