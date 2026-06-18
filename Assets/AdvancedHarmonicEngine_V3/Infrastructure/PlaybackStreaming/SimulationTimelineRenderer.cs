using HarmonicEngine.Domain.Models;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.PlaybackStreaming
{
    /// <summary>
    /// Draws a single recorded simulation frame (particle world positions) as GPU points.
    /// Used by the timeline scrubber to "send a frame to the view" during playback,
    /// independent of the live pipeline buffers.
    /// </summary>
    [ExecuteAlways]
    public class SimulationTimelineRenderer : MonoBehaviour
    {
        [SerializeField] private Material particleDebugMaterial;
        [SerializeField, Min(0.001f)] private float pointSize = 0.03f;
        [SerializeField] private Color color = new(1f, 0.45f, 0.1f, 1f);

        private static readonly int ParticlesId = Shader.PropertyToID("_Particles");
        private static readonly int ParticleCountId = Shader.PropertyToID("_ParticleCount");
        private static readonly int PointSizeId = Shader.PropertyToID("_PointSize");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private ComputeBuffer _buffer;
        private FluidParticle[] _scratch;
        private int _displayCount;
        private bool _visible;

        public bool HasFrame => _visible && _displayCount > 0;

        private void OnEnable()
        {
            EnsureMaterial();
        }

        public void Display(float3[] positions, int count)
        {
            if (positions == null || count <= 0)
            {
                _visible = false;
                _displayCount = 0;
                return;
            }

            count = Mathf.Min(count, positions.Length);
            EnsureCapacity(count);

            for (int i = 0; i < count; i++)
            {
                _scratch[i] = new FluidParticle
                {
                    Position = positions[i],
                    Velocity = float3.zero,
                    Density = 1000f,
                    Pressure = 0f,
                    PackedColorRGBA = 0xFFFFFFFFu
                };
            }

            _buffer.SetData(_scratch, 0, 0, count);
            _displayCount = count;
            _visible = true;
        }

        public void Hide()
        {
            _visible = false;
            _displayCount = 0;
        }

        public void SetColor(Color value) => color = value;

        private void EnsureMaterial()
        {
            if (particleDebugMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find("HarmonicEngine/ParticleDebugPoints");
            if (shader != null)
            {
                particleDebugMaterial = new Material(shader);
            }
        }

        private void EnsureCapacity(int count)
        {
            if (_scratch == null || _scratch.Length < count)
            {
                _scratch = new FluidParticle[Mathf.NextPowerOfTwo(count)];
            }

            if (_buffer == null || _buffer.count < count)
            {
                _buffer?.Release();
                _buffer = new ComputeBuffer(Mathf.NextPowerOfTwo(count), sizeof(float) * 12, ComputeBufferType.Structured);
            }
        }

        private void OnRenderObject()
        {
            if (!_visible || _displayCount <= 0 || _buffer == null)
            {
                return;
            }

            EnsureMaterial();
            if (particleDebugMaterial == null)
            {
                return;
            }

            particleDebugMaterial.SetBuffer(ParticlesId, _buffer);
            particleDebugMaterial.SetInt(ParticleCountId, _displayCount);
            particleDebugMaterial.SetFloat(PointSizeId, pointSize);
            particleDebugMaterial.SetColor(ColorId, color);
            particleDebugMaterial.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Points, _displayCount, 1);
        }

        private void OnDisable()
        {
            _buffer?.Release();
            _buffer = null;
        }
    }
}
