// ============================================================
// File : PendulumSimulator.cs
// Folder : Scripts/Pendulum/
// Purpose : Simulates spherical pendulum movement (swinging bucket)
//           without any use of Unity Physics
//
// Equations Used:
//   α = -(g / L) × sin(θ) - (b × ω)     Angular acceleration with damping
//   ω = ω + α × dt                        Update angular velocity
//   θ = θ + ω × dt                        Update angle
//   x = L × sin(θ)                        Convert to 3D space
//   y = -L × cos(θ)
//
// Why FixedUpdate and not Update?
//   FixedUpdate is called every 0.02 seconds constantly regardless of device speed
//   This is necessary because physics equations depend on constant and reliable dt
//
// Dependencies : SimulationConstants
// ============================================================

using HarmonicEngine.Core.Mathematics.Integrators;
using Unity.Jobs;
using UnityEngine;
using SwingingPaintBucket.Core;

namespace SwingingPaintBucket.Pendulum
{
    public enum PendulumIntegrationMode
    {
        ExplicitEuler = 0,
        RungeKutta4 = 1
    }

    public class PendulumSimulator : MonoBehaviour
    {
        // ---- Adjustable settings from Inspector ----

        [Header("Rope Properties")]
        [Tooltip("Rope length in meters")]
        [Range(0.5f, 20f)]
        public float RopeLength = 5f;

        [Header("Environment")]
        [Tooltip("Gravity value — can be changed to simulate different environments")]
        [Range(0f, 20f)]
        public float Gravity = SimulationConstants.DefaultGravity;

        [Tooltip("Damping coefficient — represents air resistance and rope friction")]
        [Range(0f, 1f)]
        public float DampingCoefficient = 0.05f;

        [Header("Initial State")]
        [Tooltip("Initial angle in degrees")]
        [Range(-180f, 180f)]
        public float InitialAngleDegrees = 45f;

        [Tooltip("Initial angular velocity")]
        public float InitialAngularVelocity = 0f;

        [Header("Integration")]
        public PendulumIntegrationMode IntegrationMode = PendulumIntegrationMode.RungeKutta4;

        [Tooltip("When RK4 is selected, integrate on a Burst worker thread via PendulumRk4Job.")]
        public bool UseBurstRk4Job = true;

        [Header("Pivot Point")]
        public Vector3 PivotPoint = Vector3.zero;

        // ---- Internal simulation variables ----

        /// <summary>Current angle in radians</summary>
        private float _theta;

        /// <summary>Current angular velocity</summary>
        private float _omega;

        // ---- Properties for external reading ----

        /// <summary>Current angle — read from BucketController to generate particles</summary>
        public float Theta => _theta;

        /// <summary>Current angular velocity</summary>
        public float Omega => _omega;

        /// <summary>Angular acceleration from the most recent FixedUpdate step.</summary>
        public float LastAngularAcceleration { get; private set; }

        /// <summary>
        /// Bucket velocity as a 3D vector
        /// Derived from position equations:
        ///   x = L × sin(θ)  →  vx = L × cos(θ) × ω
        ///   y = -L × cos(θ) →  vy = L × sin(θ) × ω
        /// </summary>
        public Vector3 BucketVelocity
        {
            get
            {
                float vx = RopeLength * Mathf.Cos(_theta) * _omega;
                float vy = RopeLength * Mathf.Sin(_theta) * _omega;
                return new Vector3(vx, vy, 0f);
            }
        }

        // ---- Unity Methods ----

        private void Start()
        {
            // Convert angle from degrees to radians
            _theta = InitialAngleDegrees * Mathf.Deg2Rad;
            _omega = InitialAngularVelocity;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            if (IntegrationMode == PendulumIntegrationMode.RungeKutta4)
            {
                if (UseBurstRk4Job)
                {
                    var job = new PendulumRk4Job
                    {
                        Theta = _theta,
                        Omega = _omega,
                        RopeLength = RopeLength,
                        Gravity = Gravity,
                        Damping = DampingCoefficient,
                        DeltaTime = dt
                    };
                    job.Schedule().Complete();
                    _theta = job.ResultTheta;
                    _omega = job.ResultOmega;
                    LastAngularAcceleration = job.ResultAngularAcceleration;
                }
                else
                {
                    PendulumRk4Integrator.Step(
                        ref _theta,
                        ref _omega,
                        out float angularAcceleration,
                        RopeLength,
                        Gravity,
                        DampingCoefficient,
                        dt);
                    LastAngularAcceleration = angularAcceleration;
                }
            }
            else
            {
                float angularAcceleration = -(Gravity / RopeLength) * Mathf.Sin(_theta)
                                            - (DampingCoefficient * _omega);
                LastAngularAcceleration = angularAcceleration;
                _omega += angularAcceleration * dt;
                _theta += _omega * dt;
            }

            UpdatePosition();
        }

        // ---- Private Methods ----

        private void UpdatePosition()
        {
            float x = RopeLength * Mathf.Sin(_theta);
            float y = -RopeLength * Mathf.Cos(_theta);

            transform.position = PivotPoint + new Vector3(x, y, 0f);
        }

        // ---- Public Methods ----

        /// <summary>
        /// Reset the pendulum to its initial state
        /// </summary>
        public void ResetSimulation()
        {
            _theta = InitialAngleDegrees * Mathf.Deg2Rad;
            _omega = InitialAngularVelocity;
            UpdatePosition();
        }
    }
}
