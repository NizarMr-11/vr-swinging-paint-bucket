using UnityEngine;
using SwingingPaintBucket.Core;

namespace SwingingPaintBucket.Pendulum
{
    public class PendulumSimulator : MonoBehaviour
    {
        // ---- Adjustable settings from Inspector ----

        [Header("Rope Properties")]
        public float RopeLength = 5f;

        [Header("Pendulum")]
        [Tooltip("Pendulum mass in kg")]
        [Range(0.1f, 50f)]
        public float Mass = 1f;

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

        /// <summary>
        /// Linear momentum of the bucket = mass × velocity
        /// </summary>
        public Vector3 Momentum => BucketVelocity * Mass;

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

            // 1. Calculate angular acceleration
            //    First component  : -(g/L) × sin(θ)  Gravity force returning to center
            //    Second component : -(b × ω) / Mass   Damping force (heavier = less damping effect)
            float angularAcceleration = -(Gravity / RopeLength) * Mathf.Sin(_theta)
                                        - (DampingCoefficient * _omega / Mass);

            // 2. Update angular velocity
            _omega += angularAcceleration * dt;

            // 3. Update angle
            _theta += _omega * dt;

            // 4. Convert angle to position in space and move the GameObject
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
