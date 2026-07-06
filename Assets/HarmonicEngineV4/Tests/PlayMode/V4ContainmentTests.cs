using System.Collections.Generic;
using HarmonicEngineV4.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Regression tests for bucket containment under motion and canvas impact splats.
    /// Guards the bugs where a moving bucket let particles tunnel through the wall
    /// (nearest-face resolution) or fall out through the floor slab, and where fast
    /// canvas impacts bounced instead of being absorbed as paint.
    /// </summary>
    public sealed class V4ContainmentTests
    {
        private const float Dt = 1f / 60f;
        private const float ContainmentSlack = 2e-3f;

        /// <summary>
        /// Every non-escaped live particle must be inside the bucket cavity (with slack)
        /// or above the rim (open top). Nothing may sit in or beyond the wall band, and
        /// nothing may be below the floor.
        /// </summary>
        private static void AssertAllContained(V4TestRig rig, string context)
        {
            Vector4[] positions = rig.ReadPositions();
            uint[] flags = rig.ReadFlags();

            for (int i = 0; i < positions.Length; i++)
            {
                if (V4ParticleFlags.HasEscaped(flags[i]))
                {
                    continue;
                }

                Vector3 world = new Vector3(positions[i].x, positions[i].y, positions[i].z);
                Vector3 local = rig.Bucket.transform.InverseTransformPoint(world);
                if (local.y > rig.Bucket.height)
                {
                    continue; // above the open top - legitimate slosh
                }

                float r = Mathf.Sqrt(local.x * local.x + local.z * local.z);
                Assert.LessOrEqual(r, rig.Bucket.innerRadius + ContainmentSlack,
                    $"{context}: particle {i} outside the wall (r={r:F4}, local={local})");
                Assert.GreaterOrEqual(local.y, -ContainmentSlack,
                    $"{context}: particle {i} below the floor (localY={local.y:F4})");
            }
        }

        [Test]
        public void LateralShake_NoParticleLeaksThroughWall()
        {
            using var rig = V4TestRig.Create();
            float time = 0f;
            for (int frame = 0; frame < 240; frame++)
            {
                time += Dt;
                // ~1 m/s peak sway, like dragging the bucket left/right.
                rig.Bucket.transform.position = new Vector3(Mathf.Sin(time * 4f) * 0.25f, 0f, 0f);
                rig.Step(1, Dt);

                if (frame % 30 == 0)
                {
                    AssertAllContained(rig, $"lateral shake frame {frame}");
                }
            }

            AssertAllContained(rig, "lateral shake final");
            Assert.AreEqual(0, rig.Root.EscapedTotal, "no holes - nothing may escape during lateral movement");
        }

        [Test]
        public void UpwardMove_NoParticleFallsThroughFloor()
        {
            using var rig = V4TestRig.Create();
            for (int frame = 0; frame < 150; frame++)
            {
                // 1.5 m/s straight up - the floor slab sweeps through resting fluid.
                rig.Bucket.transform.position += new Vector3(0f, 1.5f * Dt, 0f);
                rig.Step(1, Dt);

                if (frame % 30 == 0)
                {
                    AssertAllContained(rig, $"upward move frame {frame}");
                }
            }

            AssertAllContained(rig, "upward move final");
            Assert.AreEqual(0, rig.Root.EscapedTotal, "no holes - nothing may escape during upward movement");
            Assert.AreEqual(0, rig.Root.SettledTotal, "nothing may reach the canvas from a sealed moving bucket");
        }

        [Test]
        public void FastCanvasImpact_IsAbsorbedAsPaintSplat()
        {
            // Spawn a blob in free space below the bucket: it falls ~0.5 m onto the
            // canvas and must be absorbed on impact (explosive splat), not bounce and
            // wait for the slow settle path.
            var config = new V4TestRig.Config
            {
                RestrictSpawnToBucketCavity = false,
                SpawnZones = new List<(Vector3, float, Color)>
                {
                    (new Vector3(0f, -0.5f, 0f), 0.1f, Color.black)
                }
            };

            using var rig = V4TestRig.Create(config);
            int spawned = rig.Root.SpawnedTotal;
            Assert.Greater(spawned, 50, "not enough particles spawned to make the test meaningful");

            rig.Step(120, Dt);

            Assert.Greater(rig.Root.SettledTotal, spawned / 2,
                $"most particles should be absorbed on impact (settled={rig.Root.SettledTotal}/{spawned})");
            Assert.Less(rig.Root.ActiveParticleCount, spawned,
                "live count must shrink as impacts are absorbed");

            Vector4[] canvasCells = rig.ReadCanvasGrid();
            float maxDepth = 0f;
            foreach (Vector4 cell in canvasCells)
            {
                maxDepth = Mathf.Max(maxDepth, cell.w);
            }

            Assert.Greater(maxDepth, 0f, "impact splats must add a paint layer to the canvas grid");
        }
    }
}
