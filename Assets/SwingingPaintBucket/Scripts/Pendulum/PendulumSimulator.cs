using SwingingPaintBucket.Core;
using SwingingPaintBucket.Simulation;
using UnityEngine;

namespace SwingingPaintBucket.Pendulum
{
    public class PendulumSimulator : MonoBehaviour
    {
        [Header("خصائص الحبل")]
        public float RopeLength = 5f;

        [Header("Pendulum")]
        [Range(0.1f, 50f)]
        public float Mass = 1f;

        [Header("البيئة")]
        [Range(0f, 20f)]
        public float Gravity = SimulationConstants.DefaultGravity;

        [Range(0f, 1f)]
        public float DampingCoefficient = 0.05f;

        [Header("الحالة الابتدائية")]
        [Range(-180f, 180f)]
        public float InitialAngleDegrees = 45f;
        public float InitialPhiDegrees = 0f;
        public float InitialAngularVelocity = 0f;

        [Header("نقطة التعليق")]
        public Vector3 PivotPoint = Vector3.zero;

        private float _theta;
        private float _omega;
        private float _phi;
        private float _phiOmega;

        public float Theta => _theta;
        public float Omega => _omega;
        public float Phi => _phi;
        public float PhiOmega => _phiOmega;
        public EnvironmentController Environment;


        public Vector3 BucketVelocity {
            get {
                float sinT = Mathf.Sin(_theta);
                float cosT = Mathf.Cos(_theta);
                float sinP = Mathf.Sin(_phi);
                float cosP = Mathf.Cos(_phi);

                float vx = RopeLength * (_omega * cosT * cosP
                          - _phiOmega * sinT * sinP);
                float vy = RopeLength * _omega * sinT;
                float vz = RopeLength * (_omega * cosT * sinP
                          + _phiOmega * sinT * cosP);

                return new Vector3(vx, vy, vz);
            }
        }

        public Vector3 Momentum => BucketVelocity * Mass;

        private void Start()
        {
            _theta = InitialAngleDegrees * Mathf.Deg2Rad;
            _omega = InitialAngularVelocity;
            _phi = InitialPhiDegrees * Mathf.Deg2Rad;
            _phiOmega = 0f;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float sinTheta = Mathf.Sin(_theta);
            float cosTheta = Mathf.Cos(_theta);
            float cosPhi = Mathf.Cos(_phi);
            float sinPhi = Mathf.Sin(_phi);
            float safeSin = Mathf.Max(Mathf.Abs(sinTheta), 0.001f);
            float windHorizontal = Environment != null ? Mathf.Sqrt(Environment.WindForce.x * Environment.WindForce.x +Environment.WindForce.z * Environment.WindForce.z): 0f;

            float thetaAcc = -(Gravity / RopeLength) * sinTheta
                 + _phiOmega * _phiOmega * sinTheta * cosTheta
                 - DampingCoefficient * _omega
                 + (windHorizontal / RopeLength) * sinTheta;

            float phiAcc = -2f * _omega * _phiOmega * cosTheta / safeSin
                           - DampingCoefficient * _phiOmega;

            _omega += thetaAcc * dt;
            _phiOmega += phiAcc * dt;

            _theta += _omega * dt;
            _phi += _phiOmega * dt;

            float x = RopeLength * sinTheta * cosPhi;
            float y = -RopeLength * cosTheta;
            float z = RopeLength * sinTheta * sinPhi;

            transform.position = PivotPoint + new Vector3(x, y, z);
        }

        public void ResetSimulation()
        {
            _theta = InitialAngleDegrees * Mathf.Deg2Rad;
            _omega = 0f;
            _phi = InitialPhiDegrees * Mathf.Deg2Rad;
            _phiOmega = 0f;
        }
    }
}