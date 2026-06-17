using System;
using HarmonicEngine.Domain.IO;
using HarmonicEngine.Infrastructure.Management;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarmonicEngine.Infrastructure.PlaybackStreaming
{
    /// <summary>
    /// Optional cinematic bake path: async GPU readback of quantized falling particles to disk.
    /// </summary>
    public class HarmonicBakeRecorder : MonoBehaviour
    {
        [SerializeField] private PipelineExecutionController pipeline;
        [SerializeField] private string outputDirectory = "HarmonicBakeFrames";
        [SerializeField] private bool recordEveryFrame;
        [SerializeField] private int recordEveryNthFrame = 2;

        private CompressedDiskWriter _writer;
        private int _frameIndex;
        private bool _readbackInFlight;

        private void Awake()
        {
            _writer = new CompressedDiskWriter();
        }

        private void LateUpdate()
        {
            if (pipeline == null || !pipeline.IsSimulationActive || !recordEveryFrame || _readbackInFlight)
            {
                return;
            }

            if (_frameIndex % Mathf.Max(1, recordEveryNthFrame) != 0)
            {
                _frameIndex++;
                return;
            }

            QueueCurrentFrame();
            _frameIndex++;
        }

        public void QueueCurrentFrame()
        {
            if (pipeline == null || _writer == null || _readbackInFlight)
            {
                return;
            }

            uint count = pipeline.LastFallingQuantizeCount;
            ComputeBuffer source = pipeline.QuantizedBakeBuffer;
            if (count == 0 || source == null)
            {
                return;
            }

            int byteCount = (int)count * 16;
            _readbackInFlight = true;
            uint frameIndex = (uint)_frameIndex;
            AsyncGPUReadback.Request(source, byteCount, 0, request =>
            {
                _readbackInFlight = false;
                if (request.hasError)
                {
                    Debug.LogWarning("[HarmonicBakeRecorder] Async GPU readback failed.");
                    return;
                }

                NativeArray<byte> data = request.GetData<byte>();
                byte[] particleBytes = data.ToArray();
                byte[] payload = QuantizedFrameEncoder.BuildFramePayload(
                    particleBytes,
                    count,
                    frameIndex,
                    (ulong)DateTime.UtcNow.Ticks);

                string path = System.IO.Path.Combine(
                    Application.persistentDataPath,
                    outputDirectory,
                    $"frame_{frameIndex:D6}.bin");
                _writer.WriteFrame(payload, path);
            });
        }

        private void OnDestroy()
        {
            _writer?.Dispose();
        }
    }
}
