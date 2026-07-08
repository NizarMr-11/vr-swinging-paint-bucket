using HarmonicEngineV4.Simulation;
using UnityEngine;

namespace HarmonicEngineV4.Rendering
{
    /// <summary>
    /// Procedural debug renderer (plan Phase 6): one camera-facing quad per live particle,
    /// bound directly to the V4 SOA buffers. Zero per-frame CPU work beyond the draw call.
    /// </summary>
    public sealed class V4DebugPointRenderer : MonoBehaviour
    {
        public V4PipelineRoot pipeline;

        [Tooltip("Half-size of each point quad in world meters. 0 = use particle radius.")]
        [Min(0f)] public float pointSize = 0f;

        public void ApplyOptions(V4PipelineRoot pipelineRoot, float size)
        {
            if (pipelineRoot != null)
            {
                pipeline = pipelineRoot;
            }

            pointSize = size;
        }

        private Material _material;

        private void Start()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<V4PipelineRoot>();
            }

            Shader shader = Resources.Load<Shader>("HarmonicEngineV4/V4DebugPoints");
            if (shader == null)
            {
                Debug.LogError("V4DebugPoints shader not found in Resources.");
                enabled = false;
                return;
            }

            _material = new Material(shader);
        }

        private void OnRenderObject()
        {
            if (pipeline == null || !pipeline.Initialized || pipeline.ActiveParticleCount <= 0 || _material == null)
            {
                return;
            }

            _material.SetBuffer("_Block0", pipeline.Soa.ReadBlock0);
            _material.SetBuffer("_PackedColors", pipeline.Soa.ReadColors);
            _material.SetFloat("_PointSize", pointSize > 0f ? pointSize : pipeline.ParticleRadius);
            _material.SetInt("_ActiveParticleCount", pipeline.ActiveParticleCount);
            _material.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, pipeline.ActiveParticleCount);
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }
    }
}
