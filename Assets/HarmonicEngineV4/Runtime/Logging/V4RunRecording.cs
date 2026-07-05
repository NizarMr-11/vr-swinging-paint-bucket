using HarmonicEngineV4.Simulation;
using UnityEngine;

namespace HarmonicEngineV4.Logging
{
    /// <summary>Scene component that records the pipeline run for the lifetime of the object (spec section 12).</summary>
    public sealed class V4RunRecording : MonoBehaviour
    {
        public V4PipelineRoot pipeline;
        [Min(1)] public int sampleEveryNFrames = 10;
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

        private void TryCreate()
        {
            if (pipeline != null && pipeline.Initialized)
            {
                _recorder = new V4RunRecorder(pipeline, sampleEveryNFrames, channelSettings: channelSettings);
            }
        }

        private void OnDestroy()
        {
            _recorder?.Dispose();
            _recorder = null;
        }
    }
}
