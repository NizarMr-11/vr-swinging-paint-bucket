using Unity.Mathematics;

namespace HarmonicEngine.Domain.Adapters
{
    public enum ShapeVolumeType
    {
        Box,
        Sphere,
        Capsule,
        Cylinder,
        Mesh
    }

    /// <summary>
    /// Pure, deterministic uniform volume sampling for primitive shapes. Produces world-space
    /// positions that are evenly distributed throughout the shape's volume so an equal share of
    /// particles fills the shape. Mesh volumes are handled by <see cref="MeshVolumeSampler"/>.
    /// </summary>
    public static class ShapeVolumeSampler
    {
        /// <summary>Uniformly fills an oriented box. <paramref name="size"/> is the full extent.</summary>
        public static int SampleBox(
            float3 center,
            float3 size,
            quaternion rotation,
            int count,
            uint seed,
            float3[] outPositions)
        {
            int n = Clamp(count, outPositions);
            float3 half = size * 0.5f;
            var rng = NewRandom(seed);
            for (int i = 0; i < n; i++)
            {
                float3 local = new float3(
                    rng.NextFloat(-half.x, half.x),
                    rng.NextFloat(-half.y, half.y),
                    rng.NextFloat(-half.z, half.z));
                outPositions[i] = center + math.mul(rotation, local);
            }

            return n;
        }

        /// <summary>Uniformly fills a solid sphere (ball), not just its surface.</summary>
        public static int SampleSphere(
            float3 center,
            float radius,
            int count,
            uint seed,
            float3[] outPositions)
        {
            int n = Clamp(count, outPositions);
            var rng = NewRandom(seed);
            for (int i = 0; i < n; i++)
            {
                outPositions[i] = center + RandomInBall(ref rng, radius);
            }

            return n;
        }

        /// <summary>Uniformly fills a capsule whose axis is local +Y, oriented by <paramref name="rotation"/>.</summary>
        public static int SampleCapsule(
            float3 center,
            float radius,
            float height,
            quaternion rotation,
            int count,
            uint seed,
            float3[] outPositions)
        {
            int n = Clamp(count, outPositions);
            radius = math.max(radius, 1e-5f);
            float cylinderHeight = math.max(0f, height - 2f * radius);

            float cylinderVolume = math.PI * radius * radius * cylinderHeight;
            float capVolume = (4f / 3f) * math.PI * radius * radius * radius;
            float total = math.max(cylinderVolume + capVolume, 1e-9f);
            float cylinderFraction = cylinderVolume / total;

            var rng = NewRandom(seed);
            for (int i = 0; i < n; i++)
            {
                float3 local;
                if (rng.NextFloat() < cylinderFraction)
                {
                    float2 disk = RandomInDisk(ref rng, radius);
                    float y = rng.NextFloat(-cylinderHeight * 0.5f, cylinderHeight * 0.5f);
                    local = new float3(disk.x, y, disk.y);
                }
                else
                {
                    float3 inBall = RandomInBall(ref rng, radius);
                    float yOffset = cylinderHeight * 0.5f;
                    local = inBall.y >= 0f
                        ? new float3(inBall.x, inBall.y + yOffset, inBall.z)
                        : new float3(inBall.x, inBall.y - yOffset, inBall.z);
                }

                outPositions[i] = center + math.mul(rotation, local);
            }

            return n;
        }

        /// <summary>Uniformly fills a solid cylinder (flat caps) whose axis is local +Y.</summary>
        public static int SampleCylinder(
            float3 center,
            float radius,
            float height,
            quaternion rotation,
            int count,
            uint seed,
            float3[] outPositions)
        {
            int n = Clamp(count, outPositions);
            radius = math.max(radius, 1e-5f);
            height = math.max(height, 1e-5f);
            float halfHeight = height * 0.5f;

            var rng = NewRandom(seed);
            for (int i = 0; i < n; i++)
            {
                float2 disk = RandomInDisk(ref rng, radius);
                float y = rng.NextFloat(-halfHeight, halfHeight);
                float3 local = new float3(disk.x, y, disk.y);
                outPositions[i] = center + math.mul(rotation, local);
            }

            return n;
        }

        private static float3 RandomInBall(ref Random rng, float radius)
        {
            float3 dir = rng.NextFloat3Direction();
            float r = radius * math.pow(rng.NextFloat(), 1f / 3f);
            return dir * r;
        }

        private static float2 RandomInDisk(ref Random rng, float radius)
        {
            float angle = rng.NextFloat(0f, 2f * math.PI);
            float r = radius * math.sqrt(rng.NextFloat());
            return new float2(math.cos(angle) * r, math.sin(angle) * r);
        }

        private static Random NewRandom(uint seed)
        {
            // Unity.Mathematics.Random rejects a zero seed.
            return new Random(seed == 0u ? 0x6E624EB7u : seed);
        }

        private static int Clamp(int count, float3[] outPositions)
        {
            if (outPositions == null)
            {
                return 0;
            }

            return math.min(math.max(count, 0), outPositions.Length);
        }
    }
}
