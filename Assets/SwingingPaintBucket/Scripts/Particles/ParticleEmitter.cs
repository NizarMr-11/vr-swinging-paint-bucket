using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Canvas;
using SwingingPaintBucket.Core;
using UnityEngine;

namespace SwingingPaintBucket.Particles
{
    public class ParticleEmitter : MonoBehaviour
    {

        private BucketController _bucket;

        // ---- Particle Settings ----
        [Header("Particle Settings")]
        public int MaxParticles = 100000;

        [Tooltip("Size of a single particle — determines number of particles generated")]
        public float VolumePerParticle = 0.0001f;


        private PaintParticle[] _particles;

        private float _leftOver = 0f;
        public CanvasController Canvas;


        private void Start()
        {

            _particles = new PaintParticle[MaxParticles];
            _bucket = GetComponent<BucketController>();
        }

        private void FixedUpdate()
        {
            if (_bucket == null || !_bucket.HasPaint) return;

            float dt = Time.fixedDeltaTime;


            float rawCount = (_bucket.VolumeThisFrame / VolumePerParticle) + _leftOver;


            int particleCount = (int)Mathf.Floor(rawCount);


            _leftOver = rawCount - particleCount;


            for (int i = 0; i < particleCount; i++)
            {
                SpawnParticle();

            }
            UpdateParticles();
        }


        private int FindFreeSlot()
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].IsActive)
                    return i;
            }
            return -1;
        }

        private void SpawnParticle()
        {
            int slot = FindFreeSlot();
            if (slot == -1) return;

            Vector3 spawnPosition = transform.position + Vector3.down * 0.5f;
            Vector3 spawnVelocity = _bucket.GetParticleInitialVelocity();
            float mass = VolumePerParticle * _bucket.Density;

            _particles[slot] = new PaintParticle(
                spawnPosition,
                spawnVelocity,
                mass: mass,
                color: _bucket.CurrentPaintColor,
                viscosity: _bucket.Viscosity,
                density: _bucket.Density
            );

        }

        private void UpdateParticles()
        {
            float dt = Time.fixedDeltaTime;

            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].IsActive) continue;

                // تطبيق الجاذبية على الجسيم
                _particles[i].Acceleration = new Vector3(0f, -9.81f, 0f);

                // تحديث الموقع والسرعة
                _particles[i].Step(dt);

                // التحقق من وصول الجسيم للوحة
                if (Canvas != null && _particles[i].Position.y <= Canvas.transform.position.y)
                {
                    // إخبار اللوحة برسم البقعة
                    Canvas.OnParticleHit(
                        _particles[i].Position,
                        _particles[i].Color,
                        _particles[i].Viscosity
                    );

                    // إيقاف الجسيم
                    _particles[i].IsActive = false;
                }
            }
        }


        /// Reset all particles

        public void ResetParticles()
        {
            for (int i = 0; i < _particles.Length; i++)
                _particles[i].IsActive = false;
            _leftOver = 0f;
        }


        // Returns the particle array for external reading 

        public PaintParticle[] GetParticles() => _particles;


        public int GetActiveCount()
        {
            int count = 0;
            for (int i = 0; i < _particles.Length; i++)
                if (_particles[i].IsActive) count++;
            return count;
        }
    }
}