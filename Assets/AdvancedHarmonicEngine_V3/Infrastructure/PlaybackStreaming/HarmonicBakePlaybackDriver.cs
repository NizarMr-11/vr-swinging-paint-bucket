using System.IO;
using HarmonicEngine.Domain.IO;
using SwingingPaintBucket.Canvas;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.PlaybackStreaming
{
    /// <summary>
    /// Consumes baked frame files from disk and replays impasto stamps from decoded particle positions.
    /// </summary>
    public class HarmonicBakePlaybackDriver : MonoBehaviour
    {
        [SerializeField] private HighScaleFramePresenter presenter;
        [SerializeField] private CanvasController canvasController;
        [SerializeField] private string bakeDirectory = "HarmonicBakeFrames";
        [SerializeField] private float playbackFps = 30f;
        [SerializeField] private Vector3 quantizationOrigin;
        [SerializeField] private bool useCanvasWorldOrigin = true;
        [SerializeField] private float impastoRadius = 6f;
        [SerializeField] private float impastoIntensity = 0.12f;

        private SlidingWindowDiskQueue _queue;
        private float _playbackTimer;
        private int _playbackFrameIndex;
        private float3[] _positionScratch;

        private void Awake()
        {
            _queue = new SlidingWindowDiskQueue(4);
            if (canvasController == null)
            {
                canvasController = FindFirstObjectByType<CanvasController>();
            }
        }

        private void Update()
        {
            if (presenter == null)
            {
                return;
            }

            _playbackTimer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, playbackFps);
            if (_playbackTimer < frameDuration)
            {
                return;
            }

            _playbackTimer = 0f;
            string framePath = BuildFramePath(_playbackFrameIndex);
            if (!File.Exists(framePath))
            {
                return;
            }

            presenter.QueueFrameFile(framePath);
            if (presenter.TryConsumePreloadedFrame(out string loadedPath))
            {
                ApplyFrameFile(loadedPath);
            }

            _playbackFrameIndex++;
        }

        public void ResetPlayback()
        {
            _playbackFrameIndex = 0;
            _playbackTimer = 0f;
        }

        public void SetQuantizationOrigin(Vector3 origin) => quantizationOrigin = origin;

        private string BuildFramePath(int frameIndex) =>
            Path.Combine(Application.persistentDataPath, bakeDirectory, $"frame_{frameIndex:D6}.bin");

        private void ApplyFrameFile(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < QuantizedFrameEncoder.HeaderByteSize)
            {
                return;
            }

            QuantizedFrameEncoder.ReadHeader(bytes, out uint particleCount, out uint frameIndex, out _);
            if (particleCount == 0)
            {
                return;
            }

            float3 origin = ResolveOrigin();
            int capacity = (int)particleCount;
            _positionScratch ??= new float3[capacity];
            if (_positionScratch.Length < capacity)
            {
                _positionScratch = new float3[capacity];
            }

            int decoded = QuantizedBakeDecoder.DecodeAllParticles(bytes, origin, _positionScratch, capacity);
            StampDecodedParticles(decoded, frameIndex);
        }

        private float3 ResolveOrigin()
        {
            if (useCanvasWorldOrigin && canvasController != null)
            {
                Vector3 p = canvasController.transform.position;
                return new float3(p.x, p.y, p.z);
            }

            return quantizationOrigin;
        }

        private void StampDecodedParticles(int count, uint frameIndex)
        {
            if (presenter == null || canvasController == null)
            {
                Debug.Log($"[HarmonicBakePlaybackDriver] Frame {frameIndex}: {count} particles (no presenter/canvas).");
                return;
            }

            int stamped = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 world = _positionScratch[i];
                if (!canvasController.TryWorldToUv(world, out Vector2 uv))
                {
                    continue;
                }

                presenter.StampImpastoAtUv(uv, impastoRadius, impastoIntensity);
                stamped++;
            }

            Debug.Log($"[HarmonicBakePlaybackDriver] Frame {frameIndex}: stamped {stamped}/{count} impasto splats.");
        }

        private void OnDestroy()
        {
            _queue?.Dispose();
        }
    }
}
