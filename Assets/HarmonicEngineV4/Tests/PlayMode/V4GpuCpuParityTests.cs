using HarmonicEngineV4.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Layer 2 GPU/CPU parity tests (plan Testing Strategy): every geometric/zone/flag
    /// function exists twice - C# reference and HLSL - and these tests prove they agree
    /// on thousands of seeded sample points. This is the layer V3 never had.
    /// </summary>
    public sealed class V4GpuCpuParityTests
    {
        private const float BucketRadius = 0.5f;
        private const float BucketHeight = 1.0f;
        private const float WallThickness = 0.05f;
        private const float TopBandHeight = 0.15f;
        private const int SampleCount = 4096;

        private static ComputeShader LoadTestShader()
        {
            var shader = Resources.Load<ComputeShader>("HarmonicEngineV4Tests/V4TestKernels");
            Assert.IsNotNull(shader, "V4TestKernels.compute not found in test Resources");
            return shader;
        }

        private static V4BakedHole[] MakeHoles()
        {
            return new[]
            {
                new V4BakedHole
                {
                    localPosition = new Vector3(0.5f, 0.3f, 0f),
                    radius = 0.04f,
                    outwardNormal = Vector3.right,
                    d0 = 0.04f, d1 = 0.1f, d2 = 0.16f
                },
                new V4BakedHole
                {
                    localPosition = new Vector3(0f, 0f, 0f),
                    radius = 0.05f,
                    outwardNormal = Vector3.down,
                    d0 = 0.05f, d1 = 0.12f, d2 = 0.19f
                }
            };
        }

        /// <summary>Seeded local-space sample points covering cavity, shell, rims, holes and outside.</summary>
        private static Vector4[] MakeSamplePositions(int count, int seed)
        {
            var rng = new System.Random(seed);
            var positions = new Vector4[count];
            for (int i = 0; i < count; i++)
            {
                // Bias sampling around the interesting shells: r in [0, R+2T], y in [-2T, H+0.2].
                float r = (float)rng.NextDouble() * (BucketRadius + WallThickness * 2f);
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float y = -WallThickness * 2f + (float)rng.NextDouble() * (BucketHeight + 0.2f + WallThickness * 2f);
                positions[i] = new Vector4(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r, 0f);
            }

            return positions;
        }

        [Test]
        public void GeometryAndZones_GpuMatchesCpu()
        {
            ComputeShader shader = LoadTestShader();
            V4BakedHole[] holes = MakeHoles();
            Vector4[] positions = MakeSamplePositions(SampleCount, 12345);

            int kernel = shader.FindKernel("GeometryParityKernel");
            var posBuffer = new ComputeBuffer(SampleCount, 16);
            var holesBuffer = new ComputeBuffer(holes.Length, 48);
            var outInside = new ComputeBuffer(SampleCount, 4);
            var outShell = new ComputeBuffer(SampleCount, 4);
            var outZone = new ComputeBuffer(SampleCount, 4);
            var outHole = new ComputeBuffer(SampleCount, 4);
            var outForce = new ComputeBuffer(SampleCount, 16);

            try
            {
                posBuffer.SetData(positions);
                holesBuffer.SetData(holes);

                shader.SetBuffer(kernel, "_TestPositions", posBuffer);
                shader.SetBuffer(kernel, "_Holes", holesBuffer);
                shader.SetBuffer(kernel, "_OutUint0", outInside);
                shader.SetBuffer(kernel, "_OutUint1", outShell);
                shader.SetBuffer(kernel, "_OutUint2", outZone);
                shader.SetBuffer(kernel, "_OutUint3", outHole);
                shader.SetBuffer(kernel, "_OutFloat0", outForce);
                shader.SetFloat("_BucketInnerRadius", BucketRadius);
                shader.SetFloat("_BucketHeight", BucketHeight);
                shader.SetFloat("_BucketWallThickness", WallThickness);
                shader.SetFloat("_TopBandHeight", TopBandHeight);
                shader.SetInt("_HoleCount", holes.Length);
                shader.SetInt("_TestCount", SampleCount);
                shader.Dispatch(kernel, Mathf.CeilToInt(SampleCount / 64f), 1, 1);

                var gpuInside = new uint[SampleCount];
                var gpuShell = new uint[SampleCount];
                var gpuZone = new uint[SampleCount];
                var gpuHole = new uint[SampleCount];
                var gpuForce = new Vector4[SampleCount];
                outInside.GetData(gpuInside);
                outShell.GetData(gpuShell);
                outZone.GetData(gpuZone);
                outHole.GetData(gpuHole);
                outForce.GetData(gpuForce);

                int boundarySkips = 0;
                for (int i = 0; i < SampleCount; i++)
                {
                    Vector3 p = positions[i];

                    bool cpuInside = V4BucketGeometry.IsInside(p, BucketRadius, BucketHeight);
                    bool cpuShell = V4BucketGeometry.IsInSolidShell(p, BucketRadius, BucketHeight, WallThickness);
                    V4ZoneMath.ZoneResult cpuZone = V4ZoneMath.Classify(p, cpuInside, holes, holes.Length, BucketHeight, TopBandHeight);

                    // Points within float epsilon of a threshold may legitimately differ
                    // between fp32 GPU and CPU rounding; skip only provably-borderline cases.
                    if (IsNearAnyBoundary(p, holes))
                    {
                        boundarySkips++;
                        continue;
                    }

                    Assert.AreEqual(cpuInside ? 1u : 0u, gpuInside[i], $"inside mismatch at {p} (sample {i})");
                    Assert.AreEqual(cpuShell ? 1u : 0u, gpuShell[i], $"shell mismatch at {p} (sample {i})");
                    Assert.AreEqual((uint)cpuZone.Zone, gpuZone[i], $"zone mismatch at {p} (sample {i})");

                    if (cpuZone.Zone == V4Zone.HoleZone0 || cpuZone.Zone == V4Zone.HoleZone1 || cpuZone.Zone == V4Zone.HoleZone2)
                    {
                        Assert.AreEqual((uint)cpuZone.HoleIndex, gpuHole[i], $"hole owner mismatch at {p} (sample {i})");

                        Vector3 cpuDir = V4ZoneMath.ForceDirection(p, holes[cpuZone.HoleIndex], cpuZone.Zone);
                        Vector3 gpuDir = gpuForce[i];
                        Assert.Less(Vector3.Distance(cpuDir, gpuDir), 1e-4f, $"force dir mismatch at {p} (sample {i})");
                    }
                }

                Assert.Less(boundarySkips, SampleCount / 10, "too many samples fell on boundaries; sampling is broken");
            }
            finally
            {
                posBuffer.Release();
                holesBuffer.Release();
                outInside.Release();
                outShell.Release();
                outZone.Release();
                outHole.Release();
                outForce.Release();
            }
        }

        private static bool IsNearAnyBoundary(Vector3 p, V4BakedHole[] holes)
        {
            const float eps = 1e-4f;
            float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
            if (Mathf.Abs(r - BucketRadius) < eps || Mathf.Abs(r - (BucketRadius + WallThickness)) < eps)
            {
                return true;
            }

            if (Mathf.Abs(p.y) < eps || Mathf.Abs(p.y - BucketHeight) < eps ||
                Mathf.Abs(p.y + WallThickness) < eps ||
                Mathf.Abs(p.y - (BucketHeight - TopBandHeight)) < eps)
            {
                return true;
            }

            foreach (V4BakedHole hole in holes)
            {
                float dist = Vector3.Distance(p, hole.localPosition);
                if (Mathf.Abs(dist - hole.d0) < eps || Mathf.Abs(dist - hole.d1) < eps || Mathf.Abs(dist - hole.d2) < eps)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void Collision_GpuMatchesCpu()
        {
            ComputeShader shader = LoadTestShader();
            const float restitution = 0.3f;
            const float friction = 0.2f;

            Vector4[] positions = MakeSamplePositions(SampleCount, 777);
            var rng = new System.Random(778);
            var velocities = new Vector4[SampleCount];
            for (int i = 0; i < SampleCount; i++)
            {
                velocities[i] = new Vector4(
                    (float)rng.NextDouble() * 4f - 2f,
                    (float)rng.NextDouble() * 4f - 2f,
                    (float)rng.NextDouble() * 4f - 2f,
                    0f);
            }

            int kernel = shader.FindKernel("CollisionParityKernel");
            var posBuffer = new ComputeBuffer(SampleCount, 16);
            var velBuffer = new ComputeBuffer(SampleCount, 16);
            var outPos = new ComputeBuffer(SampleCount, 16);
            var outVel = new ComputeBuffer(SampleCount, 16);

            try
            {
                posBuffer.SetData(positions);
                velBuffer.SetData(velocities);
                shader.SetBuffer(kernel, "_TestPositions", posBuffer);
                shader.SetBuffer(kernel, "_TestVelocities", velBuffer);
                shader.SetBuffer(kernel, "_OutFloat0", outPos);
                shader.SetBuffer(kernel, "_OutFloat1", outVel);
                shader.SetFloat("_BucketInnerRadius", BucketRadius);
                shader.SetFloat("_BucketHeight", BucketHeight);
                shader.SetFloat("_BucketWallThickness", WallThickness);
                shader.SetFloat("_Restitution", restitution);
                shader.SetFloat("_Friction", friction);
                shader.SetInt("_TestCount", SampleCount);
                shader.Dispatch(kernel, Mathf.CeilToInt(SampleCount / 64f), 1, 1);

                var gpuPos = new Vector4[SampleCount];
                var gpuVel = new Vector4[SampleCount];
                outPos.GetData(gpuPos);
                outVel.GetData(gpuVel);

                int boundarySkips = 0;
                for (int i = 0; i < SampleCount; i++)
                {
                    Vector3 p = positions[i];
                    if (IsNearAnyBoundary(p, new V4BakedHole[0]))
                    {
                        boundarySkips++;
                        continue;
                    }

                    Vector3 cpuPos = p;
                    Vector3 cpuVel = (Vector3)(Vector4)velocities[i];
                    V4BucketGeometry.ResolveCollision(ref cpuPos, ref cpuVel, BucketRadius, BucketHeight, WallThickness, restitution, friction);

                    Assert.Less(Vector3.Distance(cpuPos, gpuPos[i]), 1e-4f, $"collision pos mismatch at {p} (sample {i})");
                    Assert.Less(Vector3.Distance(cpuVel, gpuVel[i]), 1e-4f, $"collision vel mismatch at {p} (sample {i})");
                }

                Assert.Less(boundarySkips, SampleCount / 10);
            }
            finally
            {
                posBuffer.Release();
                velBuffer.Release();
                outPos.Release();
                outVel.Release();
            }
        }

        [Test]
        public void FlagPacking_GpuMatchesCpu()
        {
            ComputeShader shader = LoadTestShader();
            const int count = 1024;
            var rng = new System.Random(31);
            var inputs = new uint[count];
            for (int i = 0; i < count; i++)
            {
                uint zone = (uint)rng.Next(0, 5);
                uint hole = (uint)rng.Next(0, 32);
                uint profile = (uint)rng.Next(0, 256);
                inputs[i] = zone | (hole << 8) | (profile << 16);
            }

            int kernel = shader.FindKernel("FlagsParityKernel");
            var inBuffer = new ComputeBuffer(count, 4);
            var outFlags = new ComputeBuffer(count, 4);
            var outZone = new ComputeBuffer(count, 4);
            var outHole = new ComputeBuffer(count, 4);
            var outProfile = new ComputeBuffer(count, 4);

            try
            {
                inBuffer.SetData(inputs);
                shader.SetBuffer(kernel, "_TestUintInput", inBuffer);
                shader.SetBuffer(kernel, "_OutUint0", outFlags);
                shader.SetBuffer(kernel, "_OutUint1", outZone);
                shader.SetBuffer(kernel, "_OutUint2", outHole);
                shader.SetBuffer(kernel, "_OutUint3", outProfile);
                shader.SetInt("_TestCount", count);
                shader.Dispatch(kernel, Mathf.CeilToInt(count / 64f), 1, 1);

                var gpuFlags = new uint[count];
                var gpuZone = new uint[count];
                var gpuHole = new uint[count];
                var gpuProfile = new uint[count];
                outFlags.GetData(gpuFlags);
                outZone.GetData(gpuZone);
                outHole.GetData(gpuHole);
                outProfile.GetData(gpuProfile);

                for (int i = 0; i < count; i++)
                {
                    uint zone = inputs[i] & 0xFFu;
                    uint hole = (inputs[i] >> 8) & 0xFFu;
                    uint profile = (inputs[i] >> 16) & 0xFFu;

                    uint cpuFlags = V4ParticleFlags.SetInside(0u, true);
                    cpuFlags = V4ParticleFlags.SetZone(cpuFlags, (V4Zone)zone);
                    cpuFlags = V4ParticleFlags.SetHole(cpuFlags, (int)hole);
                    cpuFlags = V4ParticleFlags.SetProfile(cpuFlags, (int)profile);

                    Assert.AreEqual(cpuFlags, gpuFlags[i], $"flag pack mismatch for input {inputs[i]:X8}");
                    Assert.AreEqual(zone, gpuZone[i]);
                    Assert.AreEqual(hole, gpuHole[i]);
                    Assert.AreEqual(profile, gpuProfile[i]);
                }
            }
            finally
            {
                inBuffer.Release();
                outFlags.Release();
                outZone.Release();
                outHole.Release();
                outProfile.Release();
            }
        }

        [Test]
        public void ColorPacking_GpuRoundTripIsLossless()
        {
            ComputeShader shader = LoadTestShader();
            const int count = 2048;
            var rng = new System.Random(99);
            var inputs = new uint[count];
            for (int i = 0; i < count; i++)
            {
                inputs[i] = (uint)rng.Next() | 0xFF000000u;
            }

            int kernel = shader.FindKernel("ColorPackParityKernel");
            var inBuffer = new ComputeBuffer(count, 4);
            var outBuffer = new ComputeBuffer(count, 4);
            try
            {
                inBuffer.SetData(inputs);
                shader.SetBuffer(kernel, "_TestUintInput", inBuffer);
                shader.SetBuffer(kernel, "_OutUint0", outBuffer);
                shader.SetInt("_TestCount", count);
                shader.Dispatch(kernel, Mathf.CeilToInt(count / 64f), 1, 1);

                var results = new uint[count];
                outBuffer.GetData(results);
                for (int i = 0; i < count; i++)
                {
                    Assert.AreEqual(inputs[i], results[i], $"color round-trip lost data for {inputs[i]:X8}");
                }
            }
            finally
            {
                inBuffer.Release();
                outBuffer.Release();
            }
        }
    }
}
