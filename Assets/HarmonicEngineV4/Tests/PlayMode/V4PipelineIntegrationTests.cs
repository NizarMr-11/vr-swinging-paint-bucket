using System.Collections.Generic;
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Layer 3 integration/invariant tests (plan Testing Strategy): full pipeline,
    /// deterministic seeds, fixed dt. Each invariant that V3 violated gets its own test.
    /// </summary>
    public sealed class V4PipelineIntegrationTests
    {
        private const float Dt = 1f / 60f;

        private static V4TestRig.Config BottomHoleConfig()
        {
            return new V4TestRig.Config
            {
                Holes = new List<V4HoleDef>
                {
                    new V4HoleDef
                    {
                        localPosition = Vector3.zero,
                        radius = 0.05f,
                        outwardNormal = Vector3.down
                    }
                }
            };
        }

        private static void AssertNoNaN(Vector4[] positions, string context)
        {
            foreach (Vector4 p in positions)
            {
                Assert.IsFalse(
                    float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z),
                    $"NaN position detected ({context})");
            }
        }

        [Test]
        public void Initialization_SpawnsExpectedParticles_InsideZone()
        {
            using var rig = V4TestRig.Create();
            Assert.IsTrue(rig.Root.Initialized);
            Assert.IsTrue(rig.Root.BakeSucceeded);
            Assert.Greater(rig.Root.ActiveParticleCount, 100);
            Assert.AreEqual(rig.Root.SpawnedTotal, rig.Root.ActiveParticleCount);

            Vector4[] positions = rig.ReadPositions();
            Vector3 zoneCenter = rig.Zones[0].transform.position;
            float zoneRadius = rig.Zones[0].radius;
            foreach (Vector4 p in positions)
            {
                Assert.LessOrEqual(
                    Vector3.Distance(new Vector3(p.x, p.y, p.z), zoneCenter),
                    zoneRadius + 1e-4f,
                    "spawned particle outside its zone");
            }
        }

        [Test]
        public void Conservation_SpawnedEqualsLivePlusSettled_EveryFrame()
        {
            using var rig = V4TestRig.Create(BottomHoleConfig());
            for (int frame = 0; frame < 240; frame++)
            {
                rig.Step(1, Dt);
                Assert.AreEqual(
                    rig.Root.SpawnedTotal,
                    rig.Root.ActiveParticleCount + rig.Root.SettledTotal,
                    $"conservation violated at frame {frame}: live={rig.Root.ActiveParticleCount} settled={rig.Root.SettledTotal}");
            }
        }

        [Test]
        public void RestStability_StillBucketNoHoles_NothingLeaksOrSettles()
        {
            using var rig = V4TestRig.Create();
            rig.Step(300, Dt);

            Assert.AreEqual(0, rig.Root.EscapedTotal, "still bucket with no holes must never eject particles");
            Assert.AreEqual(0, rig.Root.SettledTotal, "nothing can reach the canvas from a sealed bucket");

            Vector4[] positions = rig.ReadPositions();
            AssertNoNaN(positions, "rest stability");

            float radiusLimit = rig.Bucket.innerRadius + 1e-3f;
            foreach (Vector4 p in positions)
            {
                Vector3 local = rig.Bucket.transform.InverseTransformPoint(new Vector3(p.x, p.y, p.z));
                float r = Mathf.Sqrt(local.x * local.x + local.z * local.z);
                Assert.LessOrEqual(r, radiusLimit, $"particle leaked through the wall (r={r})");
                Assert.GreaterOrEqual(local.y, -1e-3f, $"particle leaked through the floor (y={local.y})");
                Assert.LessOrEqual(local.y, rig.Bucket.height + 0.2f, "particle flew far above the rim at rest");
            }
        }

        [Test]
        public void EscapeLatch_ViolentMotion_NeverUnEscapes()
        {
            using var rig = V4TestRig.Create(BottomHoleConfig());

            int previousEscapedFlagCount = 0;
            int previousEscapedTotal = 0;
            float time = 0f;

            for (int frame = 0; frame < 500; frame++)
            {
                time += Dt;
                // Violent shake + tilt + spin.
                rig.Bucket.transform.position = new Vector3(Mathf.Sin(time * 10f) * 0.3f, 0f, Mathf.Cos(time * 7f) * 0.2f);
                rig.Bucket.transform.rotation =
                    Quaternion.Euler(Mathf.Sin(time * 7f) * 30f, time * 90f, Mathf.Cos(time * 9f) * 25f);

                rig.Step(1, Dt);

                Assert.GreaterOrEqual(rig.Root.EscapedTotal, previousEscapedTotal,
                    $"escaped total decreased at frame {frame}");
                previousEscapedTotal = rig.Root.EscapedTotal;

                if (frame % 25 != 0)
                {
                    continue;
                }

                uint[] flags = rig.ReadFlags();
                int escapedFlagCount = 0;
                foreach (uint f in flags)
                {
                    if (!V4ParticleFlags.HasEscaped(f))
                    {
                        continue;
                    }

                    escapedFlagCount++;
                    Assert.IsFalse(V4ParticleFlags.IsInside(f),
                        $"escaped particle re-classified as Inside at frame {frame} - the latch failed");
                }

                // Live escaped count can only drop when escaped particles settle and are
                // removed; it can never drop because a latch flipped back.
                int settledSinceLastSample = rig.Root.SettledTotal;
                Assert.GreaterOrEqual(escapedFlagCount + settledSinceLastSample, previousEscapedFlagCount,
                    $"escaped flag count fell without matching settles at frame {frame}");
                previousEscapedFlagCount = escapedFlagCount;
            }

            Assert.Greater(rig.Root.EscapedTotal, 0, "violent motion over a bottom hole should eject at least one particle");
        }

        [Test]
        public void Carry_ConstantSpin_EntrainsInsideParticles()
        {
            using var rig = V4TestRig.Create();
            const float omega = 3f; // rad/s about +Y

            float time = 0f;
            for (int frame = 0; frame < 180; frame++)
            {
                time += Dt;
                rig.Bucket.transform.rotation = Quaternion.Euler(0f, omega * Mathf.Rad2Deg * time, 0f);
                rig.Step(1, Dt);
            }

            Vector4[] positions = rig.ReadPositions();
            Vector4[] velocities = rig.ReadVelocities();
            uint[] flags = rig.ReadFlags();

            float ratioSum = 0f;
            int sampleCount = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                if (!V4ParticleFlags.IsInside(flags[i]))
                {
                    continue;
                }

                var radial = new Vector3(positions[i].x, 0f, positions[i].z);
                float r = radial.magnitude;
                if (r < 0.08f)
                {
                    continue;
                }

                Vector3 tangentDir = Vector3.Cross(Vector3.up, radial).normalized;
                float tangential = Vector3.Dot(new Vector3(velocities[i].x, 0f, velocities[i].z), tangentDir);
                float expected = omega * r;
                ratioSum += tangential / expected;
                sampleCount++;
            }

            Assert.Greater(sampleCount, 20, "not enough inside particles away from the axis to sample");
            float meanRatio = ratioSum / sampleCount;
            Assert.Greater(meanRatio, 0.25f, $"fluid is not being carried by the spinning bucket (mean ratio {meanRatio:F3})");
            Assert.Less(meanRatio, 1.5f, $"carry overshoots the rigid frame (mean ratio {meanRatio:F3})");
        }

        [Test]
        public void HoleDrain_ParticlesEscape_FallAndSettleOnCanvas()
        {
            using var rig = V4TestRig.Create(BottomHoleConfig());

            int frame = 0;
            const int maxFrames = 900;
            while (frame < maxFrames && rig.Root.SettledTotal == 0)
            {
                rig.Step(1, Dt);
                frame++;
            }

            Assert.Greater(rig.Root.EscapedTotal, 0, "no particle ever escaped through the bottom hole");
            Assert.Greater(rig.Root.SettledTotal, 0, $"no particle settled on the canvas within {maxFrames} frames");
            Assert.Less(rig.Root.ActiveParticleCount, rig.Root.SpawnedTotal, "live count must shrink after settles");

            Vector4[] canvasCells = rig.ReadCanvasGrid();
            bool anyPaint = false;
            foreach (Vector4 cell in canvasCells)
            {
                if (cell.w > 0f)
                {
                    anyPaint = true;
                    break;
                }
            }

            Assert.IsTrue(anyPaint, "settles were counted but no paint depth landed in the canvas grid");
            AssertNoNaN(rig.ReadPositions(), "hole drain");
        }

        [Test]
        public void Determinism_SameConfigTwice_IdenticalStateAndCanvas()
        {
            Vector4[] positionsA;
            Vector4[] canvasA;
            int escapedA;
            int settledA;
            using (var rigA = V4TestRig.Create(BottomHoleConfig()))
            {
                rigA.Step(240, Dt);
                positionsA = rigA.ReadPositions();
                canvasA = rigA.ReadCanvasGrid();
                escapedA = rigA.Root.EscapedTotal;
                settledA = rigA.Root.SettledTotal;
            }

            using var rigB = V4TestRig.Create(BottomHoleConfig());
            rigB.Step(240, Dt);
            Vector4[] positionsB = rigB.ReadPositions();
            Vector4[] canvasB = rigB.ReadCanvasGrid();

            Assert.AreEqual(escapedA, rigB.Root.EscapedTotal, "escape counts diverged between identical runs");
            Assert.AreEqual(settledA, rigB.Root.SettledTotal, "settle counts diverged between identical runs");
            Assert.AreEqual(positionsA.Length, positionsB.Length);
            for (int i = 0; i < positionsA.Length; i++)
            {
                Assert.AreEqual(positionsA[i], positionsB[i], $"position diverged at particle {i}");
            }

            Assert.AreEqual(canvasA.Length, canvasB.Length);
            for (int i = 0; i < canvasA.Length; i++)
            {
                Assert.AreEqual(canvasA[i], canvasB[i], $"canvas cell {i} diverged between identical runs");
            }
        }

        [Test]
        public void ColorBlend_TwoColors_StaysInsideColorHull()
        {
            var config = new V4TestRig.Config
            {
                SpawnZones = new List<(Vector3, float, Color)>
                {
                    (new Vector3(-0.08f, 0.25f, 0f), 0.09f, Color.red),
                    (new Vector3(0.08f, 0.25f, 0f), 0.09f, Color.blue)
                },
                ConfigureProfile = p => p.colorDiffusionRate = 0.15f
            };

            using var rig = V4TestRig.Create(config);
            rig.Step(150, Dt);

            uint[] colors = rig.ReadColors();
            foreach (uint packed in colors)
            {
                uint g = (packed >> 8) & 0xFFu;
                // Red (255,0,0) and blue (0,0,255): any mix has green == 0 (+1 rounding slack).
                Assert.LessOrEqual(g, 2u, $"blended color left the red-blue hull: {packed:X8}");
            }
        }
    }
}
