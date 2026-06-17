using HarmonicEngine.Infrastructure.Management;
using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Canvas;
using SwingingPaintBucket.Core;
using UnityEngine;

namespace SwingingPaintBucket.Particles
{
    public class ParticleEmitter : MonoBehaviour
    {
        private BucketController _bucket;

        [Header("Particle Settings")]
        public int MaxParticles = 100000;

        [Tooltip("Size of a single particle — determines number of particles generated")]
        public float VolumePerParticle = 0.0001f;

        [Header("Harmonic GPU Pipeline (V3)")]
        [Tooltip("When enabled, spawned particles are ingested by AdvancedHarmonicEngine_V3 instead of CPU integration.")]
        public bool UseHarmonicGpuPipeline;

        [Tooltip("GPU pipeline controller in the scene (HarmonicPipelineRoot).")]
        public PipelineExecutionController HarmonicPipeline;

        [Tooltip("Optional bridge component; created automatically if missing.")]
        public HarmonicGpuEmitterBridge HarmonicBridge;

        public CanvasController Canvas;

        private PaintParticle[] _particles;
        private float _leftOver;

        private void Start()
        {
            _particles = new PaintParticle[MaxParticles];
            _bucket = GetComponent<BucketController>();

            if (UseHarmonicGpuPipeline)
            {
                EnsureHarmonicBridge();
            }
        }

        private void FixedUpdate()
        {
            if (_bucket == null || !_bucket.HasPaint)
            {
                return;
            }

            float rawCount = (_bucket.VolumeThisFrame / VolumePerParticle) + _leftOver;
            int particleCount = (int)Mathf.Floor(rawCount);
            _leftOver = rawCount - particleCount;

            for (int i = 0; i < particleCount; i++)
            {
                SpawnParticle();
            }

            if (!UseHarmonicGpuPipeline)
            {
                UpdateParticlesCpu();
            }
        }

        private void EnsureHarmonicBridge()
        {
            if (HarmonicPipeline == null)
            {
                HarmonicPipeline = FindFirstObjectByType<PipelineExecutionController>();
            }

            if (HarmonicBridge == null)
            {
                HarmonicBridge = GetComponent<HarmonicGpuEmitterBridge>();
                if (HarmonicBridge == null)
                {
                    HarmonicBridge = gameObject.AddComponent<HarmonicGpuEmitterBridge>();
                }
            }

            if (HarmonicPipeline != null)
            {
                HarmonicBridge.Bind(HarmonicPipeline, transform);
            }
            else
            {
                Debug.LogWarning("[ParticleEmitter] UseHarmonicGpuPipeline is enabled but no PipelineExecutionController was found.");
            }
        }

        private int FindFreeSlot()
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].IsActive)
                {
                    return i;
                }
            }

            return -1;
        }

        private void SpawnParticle()
        {
            Vector3 spawnPosition = transform.position + Vector3.down * 0.5f;
            Vector3 spawnVelocity = _bucket.GetParticleInitialVelocity();

            if (UseHarmonicGpuPipeline && HarmonicBridge != null)
            {
                HarmonicBridge.TryIngestSpawn(spawnPosition, spawnVelocity, _bucket.Density);
                return;
            }

            int slot = FindFreeSlot();
            if (slot == -1)
            {
                return;
            }

            float mass = VolumePerParticle * _bucket.Density;
            _particles[slot] = new PaintParticle(
                spawnPosition,
                spawnVelocity,
                mass: mass,
                color: _bucket.PaintColor,
                viscosity: _bucket.Viscosity,
                density: _bucket.Density);
        }

        private void UpdateParticlesCpu()
        {
            float dt = Time.fixedDeltaTime;

            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].IsActive)
                {
                    continue;
                }

                _particles[i].Acceleration = new Vector3(0f, -9.81f, 0f);
                _particles[i].Step(dt);

                if (Canvas != null && _particles[i].Position.y <= Canvas.transform.position.y)
                {
                    Canvas.OnParticleHit(
                        _particles[i].Position,
                        _particles[i].Color,
                        _particles[i].Viscosity);

                    _particles[i].IsActive = false;
                }
            }
        }

        public void ResetParticles()
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i].IsActive = false;
            }

            _leftOver = 0f;
            HarmonicPipeline?.ClearAllParticles();
        }

        public PaintParticle[] GetParticles() => _particles;

        public int GetActiveCount()
        {
            if (UseHarmonicGpuPipeline && HarmonicPipeline != null)
            {
                return (int)HarmonicPipeline.GetActiveParticleCount();
            }

            int count = 0;
            for (int i = 0; i < _particles.Length; i++)
            {
                if (_particles[i].IsActive)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
