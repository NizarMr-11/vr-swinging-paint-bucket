using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public interface IBucketKinematicProvider
    {
        Vector3 AngularVelocityWorld { get; }
        Vector3 AngularAccelerationWorld { get; }
        Vector3 BucketWorldVelocity { get; }
    }
}
