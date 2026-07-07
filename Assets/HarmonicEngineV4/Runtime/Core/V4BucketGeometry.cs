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

        /// <summary>
        /// Footprint containment for wall/floor collision (not classification). Any
        /// particle under the rim inside the inner cylinder radius is pulled back to the
        /// cavity, including brief y &lt; 0 lag when the bucket moves and fluid trails
        /// in world space. Deep sub-floor positions (free-fall spawns, canvas particles)
        /// are excluded. Above the open rim is never contained.
        /// </summary>
        public static bool ShouldContainInside(Vector3 localPos, float innerRadius, float height, float wallThickness)
        {
            if (localPos.y > height)
            {
                return false;
            }

            float floorLag = Mathf.Max(wallThickness * 4f, 0.02f);
            if (localPos.y < -floorLag)
            {
                return false;
            }

            float r2 = localPos.x * localPos.x + localPos.z * localPos.z;
            return r2 <= innerRadius * innerRadius;
        }

        /// <summary>
        /// Containment decision for a mid-frame position (GPU: V4ComputeContainInside).
        /// Callers pass frame-start flags, current local position, and frame-start local
        /// reference position (from _Block0). The reference footprint with face tolerance
        /// recovers particles pinned at exactly r = R that flicker Outside in classification.
        /// </summary>
        public static bool ComputeContainInside(uint flags, Vector3 localPos, Vector3 refLocalPos, float innerRadius, float height, float wallThickness)
        {
            if (V4ParticleFlags.IsInside(flags))
            {
                return true;
            }

            if (ShouldContainInside(localPos, innerRadius, height, wallThickness))
            {
                return true;
            }

            const float faceEps = 1e-4f;
            return ShouldContainInside(refLocalPos, innerRadius + faceEps, height, wallThickness);
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
        ///
        /// Caller contract: containInside = (frame-start Inside flag) OR ShouldContainInside
        /// footprint. The flag half is load-bearing: a particle that penetrates past the wall
        /// mid-plane in a single step (impact jet, fast bucket sweep) is geometrically outside
        /// the footprint, and nearest-face resolution would eject it through the wall - the
        /// Inside flag pulls it back to the inner face no matter how deep it penetrated.
        /// Genuinely-outside particles use nearest-face resolution: wall/floor-touching
        /// particles drift epsilon outside through float rounding and get classified Outside
        /// for a frame - they must resolve back to the inside face, not be ejected. The open
        /// top is the only unguarded exit.
        /// </summary>
        public static void ResolveCollision(
            ref Vector3 localPos,
            ref Vector3 localVel,
            float innerRadius,
            float height,
            float wallThickness,
            float restitution,
            float friction,
            bool containInside,
            Vector3 contactVelLocal = default)
        {
            float outerRadius = innerRadius + wallThickness;
            float r = Mathf.Sqrt(localPos.x * localPos.x + localPos.z * localPos.z);

            // Above the rim: open top, nothing to resolve.
            if (localPos.y > height)
            {
                return;
            }

            if (containInside)
            {
                if (localPos.y < 0f)
                {
                    localPos.y = 0f;
                    ReflectAgainstNormalInMovingFrame(ref localVel, Vector3.up, contactVelLocal, restitution, friction);
                }

                // Face-contact band (not just r > R): the solver-loop position clamps pin
                // pressurized particles at exactly r = R, so the finalize pass never sees
                // them beyond the face - without the band their outward jet velocity is
                // never reflected, accumulates across frames, and one flag-flicker frame
                // hops them past the wall mid-plane where nearest-face resolution ejects.
                if (r >= innerRadius - 1e-4f)
                {
                    if (r > innerRadius)
                    {
                        float safeR = Mathf.Max(r, 1e-6f);
                        float scale = innerRadius / safeR;
                        localPos.x *= scale;
                        localPos.z *= scale;
                    }

                    float rNow = Mathf.Max(Mathf.Sqrt(localPos.x * localPos.x + localPos.z * localPos.z), 1e-6f);
                    Vector3 radialDir = new Vector3(localPos.x, 0f, localPos.z) / rNow;
                    ReflectAgainstNormalInMovingFrame(ref localVel, -radialDir, contactVelLocal, restitution, friction);
                }

                return;
            }

            if (r <= innerRadius)
            {
                // Outside-flagged particle in the cavity footprint: floor clamp (from
                // above) or floor-slab exclusion (from below). Nearest-face resolution
                // is required here: particles resting exactly on the floor drift a hair
                // below y=0 through float rounding, get classified Outside for a frame,
                // and must resolve back UP to the floor, not down through the slab.
                if (localPos.y < 0f)
                {
                    if (localPos.y > -wallThickness)
                    {
                        bool fromAbove = localPos.y > -wallThickness * 0.5f;
                        localPos.y = fromAbove ? 0f : -wallThickness;
                        Vector3 normal = fromAbove ? Vector3.up : Vector3.down;
                        ReflectAgainstNormalInMovingFrame(ref localVel, normal, contactVelLocal, restitution, friction);
                    }

                    return;
                }

                return;
            }

            if (r >= outerRadius)
            {
                return;
            }

            // Outside-flagged particle in the wall band: resolve to the nearer face.
            // Particles pressed against the inner wall sit at exactly r = innerRadius
            // and float noise can classify them Outside for a frame - nearest-face
            // brings them back inside instead of ejecting them through the wall.
            // Lateral slosh can also push them slightly below the floor while still
            // in the wall band; lift to the inner floor before the radial resolve.
            {
                if (localPos.y < 0f)
                {
                    localPos.y = 0f;
                    ReflectAgainstNormalInMovingFrame(ref localVel, Vector3.up, contactVelLocal, restitution, friction);
                }

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
                ReflectAgainstNormalInMovingFrame(ref localVel, wallNormal, contactVelLocal, restitution, friction);
            }
        }

        /// <summary>Reflect in the wall/floor's moving frame at the contact point.</summary>
        public static void ReflectAgainstNormalInMovingFrame(
            ref Vector3 vel,
            Vector3 normal,
            Vector3 contactVel,
            float restitution,
            float friction)
        {
            Vector3 relVel = vel - contactVel;
            ReflectAgainstNormal(ref relVel, normal, restitution, friction);
            vel = relVel + contactVel;
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
