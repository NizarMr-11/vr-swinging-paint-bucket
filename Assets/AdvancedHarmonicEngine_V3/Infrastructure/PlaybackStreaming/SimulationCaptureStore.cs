using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;

namespace HarmonicEngine.Infrastructure.PlaybackStreaming
{
    /// <summary>
    /// Random-access store of recorded simulation frames (particle world positions).
    /// Acts as the "tape" for the calculate -> save -> view (scrub) workflow: the recorder
    /// appends frames during the calculate phase, the store is persisted to disk (save), and a
    /// timeline player reads any frame instantly for YouTube-style scrubbing (view).
    /// </summary>
    public sealed class SimulationCaptureStore
    {
        private const uint MagicHbk1 = 0x314B4248; // "HBK1" little-endian

        private readonly List<float3[]> _frames = new();
        private readonly List<int> _counts = new();
        private readonly List<float> _times = new();

        public float CaptureFps { get; private set; }
        public int FrameCount => _frames.Count;
        public float Duration => _times.Count > 0 ? _times[_times.Count - 1] : 0f;
        public int MaxParticleCount { get; private set; }

        public SimulationCaptureStore(float captureFps = 30f)
        {
            CaptureFps = UnityEngine.Mathf.Max(1f, captureFps);
        }

        public void Clear()
        {
            _frames.Clear();
            _counts.Clear();
            _times.Clear();
            MaxParticleCount = 0;
        }

        /// <summary>Appends a defensive copy of the first <paramref name="count"/> positions.</summary>
        public void AddFrame(float3[] positions, int count, float timeSeconds)
        {
            if (positions == null)
            {
                count = 0;
            }

            count = Math.Max(0, Math.Min(count, positions?.Length ?? 0));
            var copy = new float3[count];
            if (count > 0)
            {
                Array.Copy(positions, copy, count);
            }

            _frames.Add(copy);
            _counts.Add(count);
            _times.Add(timeSeconds);
            if (count > MaxParticleCount)
            {
                MaxParticleCount = count;
            }
        }

        public int GetFrameParticleCount(int frameIndex)
        {
            return IsValidFrame(frameIndex) ? _counts[frameIndex] : 0;
        }

        public float GetFrameTime(int frameIndex)
        {
            return IsValidFrame(frameIndex) ? _times[frameIndex] : 0f;
        }

        /// <summary>Returns the backing position array for a frame (do not mutate).</summary>
        public float3[] GetFramePositions(int frameIndex)
        {
            return IsValidFrame(frameIndex) ? _frames[frameIndex] : Array.Empty<float3>();
        }

        /// <summary>Maps a playback time in seconds to the nearest frame index.</summary>
        public int FrameIndexForTime(float timeSeconds)
        {
            if (FrameCount == 0)
            {
                return 0;
            }

            int index = UnityEngine.Mathf.RoundToInt(timeSeconds * CaptureFps);
            return UnityEngine.Mathf.Clamp(index, 0, FrameCount - 1);
        }

        public void SaveToFile(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);
            writer.Write(MagicHbk1);
            writer.Write(CaptureFps);
            writer.Write(FrameCount);
            for (int f = 0; f < FrameCount; f++)
            {
                int count = _counts[f];
                writer.Write(_times[f]);
                writer.Write(count);
                float3[] positions = _frames[f];
                for (int i = 0; i < count; i++)
                {
                    writer.Write(positions[i].x);
                    writer.Write(positions[i].y);
                    writer.Write(positions[i].z);
                }
            }
        }

        public static SimulationCaptureStore LoadFromFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);
            uint magic = reader.ReadUInt32();
            if (magic != MagicHbk1)
            {
                throw new InvalidDataException($"Not a Harmonic capture file: {path}");
            }

            float fps = reader.ReadSingle();
            int frameCount = reader.ReadInt32();
            var store = new SimulationCaptureStore(fps);
            for (int f = 0; f < frameCount; f++)
            {
                float time = reader.ReadSingle();
                int count = reader.ReadInt32();
                var positions = new float3[count];
                for (int i = 0; i < count; i++)
                {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();
                    positions[i] = new float3(x, y, z);
                }

                store._frames.Add(positions);
                store._counts.Add(count);
                store._times.Add(time);
                if (count > store.MaxParticleCount)
                {
                    store.MaxParticleCount = count;
                }
            }

            return store;
        }

        private bool IsValidFrame(int frameIndex) => frameIndex >= 0 && frameIndex < _frames.Count;
    }
}
