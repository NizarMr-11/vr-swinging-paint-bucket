using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>Pure 3D spherical pendulum math (testable without Unity lifecycle).</summary>
    public static class V4SphericalPendulumMath
    {
        public static Vector3 NormalizeDirection(Vector3 direction, Vector3 fallback)
        {
            if (direction.sqrMagnitude < 1e-8f)
            {
                direction = fallback.sqrMagnitude > 1e-8f ? fallback : Vector3.down;
            }

            return direction.normalized;
        }

        public static Vector3 ProjectOntoTangentPlane(Vector3 vector, Vector3 unitDirection)
        {
            return vector - Vector3.Dot(vector, unitDirection) * unitDirection;
        }

        public static Vector3 ComputeGravityTangent(Vector3 unitDirection, float gravity)
        {
            Vector3 gravityAccel = new Vector3(0f, -gravity, 0f);
            return ProjectOntoTangentPlane(gravityAccel, unitDirection);
        }

        public static Vector3 ComputeSloshAcceleration(
            Vector3 unitDirection,
            Vector3 comOffsetWorld,
            float fluidMass,
            float bucketMass,
            float ropeLength,
            float gravity,
            float feedbackScale)
        {
            if (fluidMass <= 1e-6f || feedbackScale <= 1e-6f)
            {
                return Vector3.zero;
            }

            float totalMass = Mathf.Max(bucketMass + fluidMass, 1e-6f);
            Vector3 weight = new Vector3(0f, -fluidMass * gravity, 0f);
            Vector3 torque = Vector3.Cross(comOffsetWorld, weight);
            Vector3 angularAccel = torque / (totalMass * ropeLength * ropeLength);
            Vector3 tangential = ProjectOntoTangentPlane(angularAccel, unitDirection);
            return tangential * feedbackScale;
        }

        public static void IntegrateStep(
            ref Vector3 unitDirection,
            ref Vector3 tangentialVelocity,
            float ropeLength,
            float gravity,
            float damping,
            float deltaTime,
            Vector3 sloshAcceleration)
        {
            Vector3 gravityTangent = ComputeGravityTangent(unitDirection, gravity);
            Vector3 dampingAccel = -damping * tangentialVelocity;
            tangentialVelocity += (gravityTangent + dampingAccel + sloshAcceleration) * deltaTime;

            unitDirection += (tangentialVelocity / Mathf.Max(ropeLength, 1e-6f)) * deltaTime;
            unitDirection = NormalizeDirection(unitDirection, Vector3.down);
            tangentialVelocity = ProjectOntoTangentPlane(tangentialVelocity, unitDirection);
        }

        public static Vector3 ComputeAngularVelocity(Vector3 unitDirection, Vector3 tangentialVelocity, float ropeLength)
        {
            return Vector3.Cross(unitDirection, tangentialVelocity) / Mathf.Max(ropeLength, 1e-6f);
        }

        /// <summary>
        /// Bucket local +Y (floor center → rim) aligns with the rope toward the pivot.
        /// Optional omega twists about the rope axis.
        /// </summary>
        public static Quaternion ComputeBucketRotation(Vector3 unitDirection, float twistDegrees = 0f)
        {
            Vector3 ropeDir = NormalizeDirection(unitDirection, Vector3.down);
            Vector3 bucketUp = -ropeDir;
            Vector3 referenceForward = Mathf.Abs(Vector3.Dot(bucketUp, Vector3.up)) > 0.99f
                ? Vector3.forward
                : Vector3.up;
            Vector3 tangent = Vector3.Cross(referenceForward, bucketUp);
            if (tangent.sqrMagnitude < 1e-8f)
            {
                tangent = Vector3.right;
            }

            tangent.Normalize();
            Quaternion baseRotation = Quaternion.LookRotation(tangent, bucketUp);
            if (Mathf.Abs(twistDegrees) < 1e-4f)
            {
                return baseRotation;
            }

            return Quaternion.AngleAxis(twistDegrees, ropeDir) * baseRotation;
        }

        /// <summary>Azimuth α (degrees, around world Y) and polar β (degrees from world +Y) for rope direction.</summary>
        public static void SphericalAlphaBetaFromDirection(Vector3 unitDirection, out float alphaDegrees, out float betaDegrees)
        {
            Vector3 dir = NormalizeDirection(unitDirection, Vector3.down);
            betaDegrees = Mathf.Acos(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            alphaDegrees = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        public static void DecomposePose(
            Vector3 pivotWorld,
            Vector3 bucketWorld,
            float twistDegrees,
            out float ropeLength,
            out Vector3 unitDirection,
            out float alphaDegrees,
            out float betaDegrees,
            out float omegaDegrees)
        {
            Vector3 delta = bucketWorld - pivotWorld;
            ropeLength = delta.magnitude;
            unitDirection = ropeLength > 1e-6f ? delta / ropeLength : Vector3.down;
            SphericalAlphaBetaFromDirection(unitDirection, out alphaDegrees, out betaDegrees);
            omegaDegrees = twistDegrees;
        }

        /// <summary>True when bucket +Y aligns with the vector from bucket toward pivot.</summary>
        public static bool BucketAxisAlignsWithHangPoint(Quaternion bucketRotation, Vector3 bucketWorld, Vector3 pivotWorld)
        {
            Vector3 towardPivot = pivotWorld - bucketWorld;
            if (towardPivot.sqrMagnitude < 1e-8f)
            {
                return true;
            }

            Vector3 bucketUp = bucketRotation * Vector3.up;
            float alignment = Vector3.Dot(bucketUp.normalized, towardPivot.normalized);
            return alignment > 0.999f;
        }

        public static Vector3 WorldPosition(Vector3 pivot, float ropeLength, Vector3 unitDirection)
        {
            return pivot + unitDirection * ropeLength;
        }
    }
}
