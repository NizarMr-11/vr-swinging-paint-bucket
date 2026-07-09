using HarmonicEngineV4.Simulation;
using UnityEngine;

namespace HarmonicEngineV4.Logging
{
    /// <summary>
    /// Scene component that records the pipeline run for the lifetime of the object.
    /// Logs are written under {project}/Logs/Engine2/run_*/ (manifest + channels/*.log).
    /// </summary>
    public sealed class V4RunRecording : MonoBehaviour
    {
        public V4PipelineRoot pipeline;
        [Min(1)] public int sampleEveryNFrames = 10;
        public bool recordingEnabled = true;
        [Tooltip("Empty uses the project Logs/Engine2 folder.")]
        public string directoryOverride;
        public V4ChannelLogSettings channelSettings = new V4ChannelLogSettings();

        private V4RunRecorder _recorder;

        public V4RunRecorder Recorder => _recorder;

        private void Start()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<V4PipelineRoot>();
            }

            TryCreate();
        }

        private void Update()
        {
            // Pipeline may initialize a frame later than this component.
            if (_recorder == null)
            {
                TryCreate();
            }
        }

        public void ApplySessionSettings(bool enabled, string baseDirectory, V4ChannelLogSettings settings)
        {
            recordingEnabled = enabled;
            directoryOverride = string.IsNullOrWhiteSpace(baseDirectory) ? null : baseDirectory.Trim();
            channelSettings = settings ?? new V4ChannelLogSettings();
        }

        public void ResetRecorder()
        {
            _recorder?.Dispose();
            _recorder = null;
        }

        private void TryCreate()
        {
            if (!recordingEnabled || _recorder != null)
            {
                return;
            }

            if (pipeline != null && pipeline.Initialized)
            {
                _recorder = new V4RunRecorder(
                    pipeline,
                    sampleEveryNFrames,
                    directoryOverride: directoryOverride,
                    channelSettings: channelSettings);
            }
        }

        private void OnDestroy()
        {
            _recorder?.Dispose();
            _recorder = null;
        }
    }
}
