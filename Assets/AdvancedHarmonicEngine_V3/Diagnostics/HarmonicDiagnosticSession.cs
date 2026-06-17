using HarmonicEngine.Infrastructure.Management;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarmonicEngine.Diagnostics
{
    public sealed class HarmonicDiagnosticSession
    {
        public PipelineExecutionController Pipeline { get; internal set; }
        public int FrameIndex { get; internal set; }
        public float StartTime { get; }
        public string LogDirectory { get; }

        internal HarmonicDiagnosticSession(PipelineExecutionController pipeline, string logDirectory)
        {
            Pipeline = pipeline;
            LogDirectory = logDirectory;
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
                $"logDirectory={LogDirectory}\n" +
                $"maxCapacity={Pipeline?.MaxCapacity ?? 0}\n" +
                $"worldFallingOnly={Pipeline?.WorldFallingOnly ?? false}";
        }
    }
}
