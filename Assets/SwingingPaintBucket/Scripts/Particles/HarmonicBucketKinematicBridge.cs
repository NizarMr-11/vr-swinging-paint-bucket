using HarmonicEngine.Infrastructure.Management;
using SwingingPaintBucket.Pendulum;
using UnityEngine;

namespace SwingingPaintBucket.Particles
{
    [DisallowMultipleComponent]
    public class HarmonicBucketKinematicBridge : MonoBehaviour, IBucketKinematicProvider
    {
        [SerializeField] private PendulumSimulator pendulum;

        public Vector3 AngularVelocityWorld =>
            pendulum != null
                ? new Vector3(0f, 0f, pendulum.Omega)
                : Vector3.zero;

        public Vector3 AngularAccelerationWorld =>
            pendulum != null
                ? new Vector3(0f, 0f, pendulum.LastAngularAcceleration)
                : Vector3.zero;

        public Vector3 BucketWorldVelocity =>
            pendulum != null
                ? pendulum.BucketVelocity
                : Vector3.zero;

        public void BindPendulum(PendulumSimulator source) => pendulum = source;

        private void Awake()
        {
            if (pendulum == null)
            {
                pendulum = GetComponent<PendulumSimulator>();
            }
        }
    }
}
