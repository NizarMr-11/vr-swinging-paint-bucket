using UnityEngine;
using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Core;

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
                color: _bucket.PaintColor,
                viscosity: _bucket.Viscosity,
                density: _bucket.Density
            );
            
        }


        
        /// Reset all particles
        
        public void ResetParticles()
        {
            for (int i = 0; i < _particles.Length; i++)
                _particles[i].IsActive = false;
            _leftOver = 0f;
        }

        
        // Returns the particle array for external reading (for rendering later)
        
        public PaintParticle[] GetParticles() => _particles;

        
        /// Count of currently active particles
        /// </summary>
        public int GetActiveCount()
        {
            int count = 0;
            for (int i = 0; i < _particles.Length; i++)
                if (_particles[i].IsActive) count++;
            return count;
        }
    }
}