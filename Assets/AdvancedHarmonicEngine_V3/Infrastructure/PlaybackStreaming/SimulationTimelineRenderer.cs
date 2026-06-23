using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Infrastructure.Rendering;
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

        private static readonly int PointSizeId = Shader.PropertyToID("_PointSize");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int UseParticleColorId = Shader.PropertyToID("_UseParticleColor");

        private ComputeBuffer _block0;
        private ComputeBuffer _packedColors;
        private Vector4[] _scratchBlock0;
        private uint[] _scratchColors;
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

            uint packed = 0xFFFFFFFFu;
            for (int i = 0; i < count; i++)
            {
                _scratchBlock0[i] = new Vector4(positions[i].x, positions[i].y, positions[i].z, 0f);
                _scratchColors[i] = packed;
            }

            _block0.SetData(_scratchBlock0, 0, 0, count);
            _packedColors.SetData(_scratchColors, 0, 0, count);
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
            int capacity = Mathf.NextPowerOfTwo(count);
            if (_scratchBlock0 == null || _scratchBlock0.Length < count)
            {
                _scratchBlock0 = new Vector4[capacity];
                _scratchColors = new uint[capacity];
            }

            if (_block0 == null || _block0.count < count)
            {
                ReleaseBuffers();
                _block0 = new ComputeBuffer(capacity, sizeof(float) * 4, ComputeBufferType.Structured);
                _packedColors = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            }
        }

        private void OnRenderObject()
        {
            if (!_visible || _displayCount <= 0 || _block0 == null)
            {
                return;
            }

            EnsureMaterial();
            if (particleDebugMaterial == null)
            {
                return;
            }

            var soa = new ParticleSoaBuffers
            {
                Block0 = _block0,
                PackedColors = _packedColors
            };
            HarmonicParticleSoaShaderBindings.BindSoa(particleDebugMaterial, soa, _displayCount);
            particleDebugMaterial.SetFloat(PointSizeId, pointSize);
            particleDebugMaterial.SetColor(ColorId, color);
            particleDebugMaterial.SetFloat(UseParticleColorId, 0f);
            particleDebugMaterial.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Points, _displayCount, 1);
        }

        private void OnDestroy()
        {
            ReleaseBuffers();
        }

        private void ReleaseBuffers()
        {
            _block0?.Release();
            _packedColors?.Release();
            _block0 = null;
            _packedColors = null;
        }
    }
}
