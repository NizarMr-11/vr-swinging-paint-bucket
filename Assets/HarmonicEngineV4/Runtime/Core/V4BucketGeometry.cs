using UnityEngine;

namespace HarmonicEngineV4.Core
{
    /// <summary>
    /// Pure CPU reference for all bucket-local geometry (plan Phase 3, Step 1).
    /// The HLSL in V4BucketZones.hlsl is a one-to-one transliteration of these
    /// functions; GPU/CPU parity tests keep them in lockstep.
    ///
    /// Bucket-local space: origin at the floor center (inside bottom), +Y up.
    /// Inner cavity: radius R, floor y=0, open rim at y=H.
    /// Solid shell: wall from R to R+t (below rim), floor slab from y=-t to 0.
    /// Holes are ignored by collision: the only way through the shell is the
    /// Zone 0 eject, which sets the permanent escape latch.
    /// </summary>
    public static class V4BucketGeometry
    {
        /// <summary>Inside the open cavity (fluid containment region), rim exclusive above.</summary>
        public static bool IsInside(Vector3 localPos, float innerRadius, float height)
        {
            if (localPos.y < 0f || localPos.y > height)
            {
                return false;
            }

            float r2 = localPos.x * localPos.x + localPos.z * localPos.z;
            return r2 <= innerRadius * innerRadius;
        }

        /// <summary>True when the point penetrates the solid shell (wall band below rim, or floor slab).</summary>
        public static bool IsInSolidShell(Vector3 localPos, float innerRadius, float height, float wallThickness)
        {
            float outerRadius = innerRadius + wallThickness;
            float r = Mathf.Sqrt(localPos.x * localPos.x + localPos.z * localPos.z);

            bool inWallBand = r > innerRadius && r < outerRadius && localPos.y >= -wallThickness && localPos.y <= height;
            bool inFloorSlab = r <= innerRadius && localPos.y > -wallThickness && localPos.y < 0f;
            return inWallBand || inFloorSlab;
        }

        /// <summary>
        /// Collision response in bucket-local space. Contains inside particles (wall + floor)
        /// and excludes outside particles from the solid shell, with slide friction and
        /// restitution. Escaped particles must never reach this function.
        /// </summary>
        public static void ResolveCollision(
            ref Vector3 localPos,
            ref Vector3 localVel,
            float innerRadius,
            float height,
            float wallThickness,
            float restitution,
            float friction)
        {
            float outerRadius = innerRadius + wallThickness;
            float r = Mathf.Sqrt(localPos.x * localPos.x + localPos.z * localPos.z);

            // Above the rim: open top, nothing to resolve.
            if (localPos.y > height)
            {
                return;
            }

            if (r <= innerRadius)
            {
                // Inside the cavity footprint: floor clamp (from above) or floor-slab
                // exclusion (from below).
                if (localPos.y < 0f)
                {
                    if (localPos.y > -wallThickness)
                    {
                        // Penetrating the floor slab: resolve to the nearer face.
                        bool fromAbove = localPos.y > -wallThickness * 0.5f;
                        localPos.y = fromAbove ? 0f : -wallThickness;
                        ReflectAxis(ref localVel, Vector3.up, restitution, friction, fromAbove ? 1f : -1f);
                    }

                    return;
                }

                return;
            }

            if (r >= outerRadius)
            {
                return;
            }

            // Inside the wall band: push to the nearer radial face.
            float toInner = r - innerRadius;
            float toOuter = outerRadius - r;
            bool resolveToInner = toInner <= toOuter;

            float targetR = resolveToInner ? innerRadius : outerRadius;
            float safeR = Mathf.Max(r, 1e-6f);
            float scale = targetR / safeR;
            localPos.x *= scale;
            localPos.z *= scale;

            Vector3 radialDir = new Vector3(localPos.x, 0f, localPos.z) / Mathf.Max(targetR, 1e-6f);
            Vector3 wallNormal = resolveToInner ? -radialDir : radialDir;
            ReflectAgainstNormal(ref localVel, wallNormal, restitution, friction);
        }

        private static void ReflectAxis(ref Vector3 vel, Vector3 axis, float restitution, float friction, float sign)
        {
            Vector3 normal = axis * sign;
            ReflectAgainstNormal(ref vel, normal, restitution, friction);
        }

        /// <summary>Removes penetrating velocity along the normal (with restitution) and applies slide friction tangentially.</summary>
        public static void ReflectAgainstNormal(ref Vector3 vel, Vector3 normal, float restitution, float friction)
        {
            float vn = Vector3.Dot(vel, normal);
            if (vn >= 0f)
            {
                return;
            }

            Vector3 normalComponent = normal * vn;
            Vector3 tangential = vel - normalComponent;
            vel = tangential * (1f - friction) - normalComponent * restitution;
        }
    }
}
