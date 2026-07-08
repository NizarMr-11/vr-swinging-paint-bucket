using System.Collections.Generic;
using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Canvas;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Simulation;
using UnityEngine;

namespace SwingingPaintBucket.Particles
{
    public class ParticleEmitter : MonoBehaviour
    {
        [Header("Particle Settings")]
        public int MaxParticles = 100000;

        [Tooltip("Paint volume represented by one particle, in litres.")]
        public float VolumePerParticle = 0.0001f;

        [Header("Nozzle / Emission Point")]
        [Tooltip("Optional real nozzle point. If empty, the script uses Local Nozzle Offset from the bucket transform.")]
        public Transform NozzlePoint;

        [Tooltip("Fallback nozzle position in local bucket coordinates. For the current placeholder bucket, Y=-0.55 usually places it under the bucket.")]
        public Vector3 LocalNozzleOffset = new Vector3(0f, -0.55f, 0f);

        [Tooltip("Small random radius around the nozzle, in metres, used to avoid perfectly artificial one-pixel lines.")]
        [Range(0f, 0.08f)] public float NozzleJitterRadius = 0.015f;

        [Header("Physical Paint Projection")]
        [Tooltip("Draws one predicted impact per frame using the same ballistic path as paint particles. This replaces the old vertical bucket-center fallback.")]
        public bool EnableBallisticPaintProjection = true;

        [Tooltip("Emergency fallback that projects vertically from the nozzle if ballistic projection misses. Keep disabled for physics testing; enable only if you need guaranteed marks during a demo.")]
        public bool EnableVerticalEmergencyFallback = false;

        [Tooltip("Maximum projected flight time sampled for direct physical projection, in seconds.")]
        [Range(0.1f, 5f)] public float ProjectionMaxTime = 2.5f;

        [Tooltip("Simulation step used only for predicted paint impact. Smaller is more accurate but slightly slower.")]
        [Range(0.005f, 0.08f)] public float ProjectionTimeStep = 0.02f;

        [Tooltip("How many projected splats can be drawn per physics frame when paint is flowing.")]
        [Range(1, 8)] public int DirectFallbackSplatsPerFrame = 1;

        [Header("Runtime Debug")]
        [SerializeField] private int ParticlesSpawnedThisFrameDebug;
        [SerializeField] private int ActiveParticlesDebug;
        [SerializeField] private int CanvasHitsThisFrameDebug;
        [SerializeField] private int TotalCanvasHitsDebug;
        [SerializeField] private bool DirectProjectionInsideCanvasDebug;
        [SerializeField] private bool BallisticProjectionHitCanvasDebug;
        [SerializeField] private Vector3 NozzleWorldPositionDebug;
        [SerializeField] private Vector3 LastPredictedHitWorldDebug;

        [Header("References")]
        public CanvasController Canvas;
        public EnvironmentController Environment;
        public SimulationManager SimulationManager;

        private PaintParticle[] _particles;
        private BucketController _bucket;
        private float _leftOver;

        // Particles that never hit the canvas (e.g. they fly past its edge) used to stay "active"
        // forever, and every new spawn had to linearly scan the whole array looking for a free slot.
        // As more particles piled up over the course of a run, that scan got slower and slower — the
        // simulation would start fine and visibly bog down over time. A free-list + active-list makes
        // both allocation and iteration O(1) / O(active count) regardless of how many particles miss.
        private Stack<int> _freeSlots;
        private List<int> _activeSlots;

        private void Awake()
        {
            CacheReferences();
        }

        private void Start()
        {
            CacheReferences();
            EnsureCapacity();
        }

        private void EnsureCapacity()
        {
            if (_particles != null && _particles.Length == MaxParticles)
                return;

            _particles = new PaintParticle[MaxParticles];
            _activeSlots = new List<int>(MaxParticles);
            _freeSlots = new Stack<int>(MaxParticles);
            for (int i = MaxParticles - 1; i >= 0; i--)
                _freeSlots.Push(i);
        }

        private void FixedUpdate()
        {
            ParticlesSpawnedThisFrameDebug = 0;
            CanvasHitsThisFrameDebug = 0;
            ActiveParticlesDebug = GetActiveCount();
            DirectProjectionInsideCanvasDebug = false;
            BallisticProjectionHitCanvasDebug = false;
            NozzleWorldPositionDebug = GetNozzleWorldPosition();

            if (SimulationManager != null && !SimulationManager.IsRunning)
                return;

            if (_bucket == null || !_bucket.HasPaint)
            {
                UpdateParticles();
                ActiveParticlesDebug = GetActiveCount();
                return;
            }

            float rawCount = (_bucket.VolumeThisFrame / Mathf.Max(0.000001f, VolumePerParticle)) + _leftOver;
            int particleCount = Mathf.FloorToInt(rawCount);
            _leftOver = rawCount - particleCount;

            for (int i = 0; i < particleCount; i++)
            {
                SpawnParticle();
                ParticlesSpawnedThisFrameDebug++;
            }

            UpdateParticles();
            PaintPhysicalProjectionIfNeeded(particleCount);
            ActiveParticlesDebug = GetActiveCount();
        }

        public void ResetParticles()
        {
            EnsureCapacity();

            for (int i = 0; i < _particles.Length; i++)
                _particles[i].IsActive = false;

            _activeSlots.Clear();
            _freeSlots.Clear();
            for (int i = _particles.Length - 1; i >= 0; i--)
                _freeSlots.Push(i);

            _leftOver = 0f;
        }

        public PaintParticle[] GetParticles() => _particles;

        public int GetActiveCount() => _activeSlots != null ? _activeSlots.Count : 0;

        private void PaintPhysicalProjectionIfNeeded(int emittedParticleCount)
        {
            if ((!EnableBallisticPaintProjection && !EnableVerticalEmergencyFallback) || Canvas == null || _bucket == null)
                return;

            if (_bucket.VolumeThisFrame <= 0f)
                return;

            int splatCount = Mathf.Clamp(Mathf.Max(1, emittedParticleCount / 8), 1, DirectFallbackSplatsPerFrame);
            for (int i = 0; i < splatCount; i++)
            {
                Vector3 nozzlePosition = GetNozzleWorldPosition() + GetNozzleJitterWorldOffset();
                Vector3 initialVelocity = _bucket.GetParticleInitialVelocity();

                bool painted = false;

                if (EnableBallisticPaintProjection)
                    painted = TryPaintPredictedBallisticImpact(nozzlePosition, initialVelocity);

                if (!painted && EnableVerticalEmergencyFallback)
                    painted = TryPaintVerticalEmergencyImpact(nozzlePosition);

                if (painted)
                {
                    CanvasHitsThisFrameDebug++;
                    TotalCanvasHitsDebug++;
                }
            }
        }

        private bool TryPaintPredictedBallisticImpact(Vector3 startPosition, Vector3 initialVelocity)
        {
            // Must match the gravity actually driving the pendulum/bucket (BucketController.Gravity),
            // not the fixed default constant. Otherwise, changing the Gravity slider desyncs the
            // predicted paint path from where the bucket really is, and the paint lands in the wrong spot.
            float gravityMagnitude = _bucket != null ? _bucket.Gravity : SimulationConstants.DefaultGravity;
            Vector3 gravity = new Vector3(0f, -gravityMagnitude, 0f);
            Vector3 wind = Environment != null ? Environment.WindForce : Vector3.zero;
            Vector3 acceleration = gravity + wind;

            Vector3 previousPosition = startPosition;
            Vector3 velocity = initialVelocity;
            float dt = Mathf.Max(0.005f, ProjectionTimeStep);
            int steps = Mathf.Max(1, Mathf.CeilToInt(ProjectionMaxTime / dt));

            for (int step = 0; step < steps; step++)
            {
                velocity += acceleration * dt;
                Vector3 currentPosition = previousPosition + velocity * dt;

                if (Canvas != null && Canvas.TryGetParticleHitPoint(previousPosition, currentPosition, out Vector3 hitPoint))
                {
                    LastPredictedHitWorldDebug = hitPoint;
                    BallisticProjectionHitCanvasDebug = true;
                    DirectProjectionInsideCanvasDebug = true;
                    return Canvas.OnParticleHit(hitPoint, _bucket.CurrentPaintColor, _bucket.EffectiveViscosity);
                }

                previousPosition = currentPosition;
            }

            return false;
        }

        private bool TryPaintVerticalEmergencyImpact(Vector3 startPosition)
        {
            DirectProjectionInsideCanvasDebug = Canvas.TryProjectPointOntoCanvas(startPosition, Vector3.down, out Vector3 projectedHit);
            if (!DirectProjectionInsideCanvasDebug)
                return false;

            LastPredictedHitWorldDebug = projectedHit;
            return Canvas.OnParticleHit(projectedHit, _bucket.CurrentPaintColor, _bucket.EffectiveViscosity);
        }

        private void SpawnParticle()
        {
            EnsureCapacity();

            if (_freeSlots.Count == 0)
                return; // buffer full — every slot is currently an active, in-flight particle

            int slot = _freeSlots.Pop();

            Vector3 spawnPosition = GetNozzleWorldPosition() + GetNozzleJitterWorldOffset();
            Vector3 spawnVelocity = _bucket.GetParticleInitialVelocity();
            float mass = VolumePerParticle * _bucket.Density;

            _particles[slot] = new PaintParticle(
                spawnPosition,
                spawnVelocity,
                mass,
                _bucket.CurrentPaintColor,
                _bucket.EffectiveViscosity,
                _bucket.Density
            );
            _activeSlots.Add(slot);
        }

        private Vector3 GetNozzleWorldPosition()
        {
            if (NozzlePoint != null)
                return NozzlePoint.position;

            return transform.TransformPoint(LocalNozzleOffset);
        }

        private Vector3 GetNozzleJitterWorldOffset()
        {
            if (NozzleJitterRadius <= 0f)
                return Vector3.zero;

            Vector2 circle = Random.insideUnitCircle * NozzleJitterRadius;
            return transform.right * circle.x + transform.forward * circle.y;
        }

        private void UpdateParticles()
        {
            if (_particles == null || _activeSlots == null)
                return;

            float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : SimulationConstants.FixedTimeStep;
            float gravityMagnitude = _bucket != null ? _bucket.Gravity : SimulationConstants.DefaultGravity;
            Vector3 windForce = Environment != null ? Environment.WindForce : Vector3.zero;

            // Iterate backwards so a swap-remove (moving the last element into the removed slot)
            // never skips an element or disturbs indices we haven't visited yet.
            for (int idx = _activeSlots.Count - 1; idx >= 0; idx--)
            {
                int i = _activeSlots[idx];
                Vector3 previousPosition = _particles[i].Position;

                _particles[i].Acceleration = new Vector3(0f, -gravityMagnitude, 0f) + windForce;
                _particles[i].Step(dt);

                bool hit = Canvas != null && Canvas.TryPaintParticle(
                    previousPosition,
                    _particles[i].Position,
                    _particles[i].Color,
                    _particles[i].Viscosity);

                if (hit)
                {
                    _particles[i].IsActive = false;
                    CanvasHitsThisFrameDebug++;
                    TotalCanvasHitsDebug++;

                    // swap-remove from the active list, then return the slot to the free pool
                    int lastIdx = _activeSlots.Count - 1;
                    _activeSlots[idx] = _activeSlots[lastIdx];
                    _activeSlots.RemoveAt(lastIdx);
                    _freeSlots.Push(i);
                }
            }
        }

        private void CacheReferences()
        {
            if (_bucket == null)
                _bucket = GetComponent<BucketController>();

            if (Canvas == null)
                Canvas = FindAnyObjectByType<CanvasController>();

            if (Environment == null)
                Environment = FindAnyObjectByType<EnvironmentController>();

            if (SimulationManager == null)
                SimulationManager = FindAnyObjectByType<SimulationManager>();
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 nozzle = Application.isPlaying ? NozzleWorldPositionDebug : GetNozzleWorldPosition();

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(nozzle, 0.06f);
            Gizmos.DrawLine(transform.position, nozzle);

            if (Application.isPlaying && DirectProjectionInsideCanvasDebug)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(LastPredictedHitWorldDebug, 0.08f);
                Gizmos.DrawLine(nozzle, LastPredictedHitWorldDebug);
            }
        }
    }
}
