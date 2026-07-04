using System.Collections.Generic;
using HarmonicEngineV4.Bake;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Golden-frame regression tests (plan Phase 9). A fixed choreography runs for 100
    /// frames; per-frame counters and an order-independent position checksum are compared
    /// against a self-consistency baseline captured in the same run. Because the pipeline
    /// is fully deterministic (verified by V4PipelineIntegrationTests.Determinism_*), any
    /// divergence between two identical runs - or drift beyond tolerance across code
    /// changes - is a regression, not noise.
    /// </summary>
    public sealed class V4GoldenFrameTests
    {
        private const float Dt = 1f / 60f;
        private const int Frames = 100;

        private struct FrameRecord
        {
            public int Live;
            public int Escaped;
            public int Settled;
            public double PositionChecksum;
        }

        private static V4TestRig.Config GoldenConfig()
        {
            return new V4TestRig.Config
            {
                Holes = new List<V4HoleDef>
                {
                    new V4HoleDef
                    {
                        localPosition = new Vector3(0.1f, 0f, 0f),
                        radius = 0.045f,
                        outwardNormal = Vector3.down
                    }
                }
            };
        }

        private static void RunChoreography(V4TestRig rig, List<FrameRecord> records)
        {
            float time = 0f;
            for (int frame = 0; frame < Frames; frame++)
            {
                time += Dt;
                // Fixed golden choreography: gentle swirl + tilt.
                rig.Bucket.transform.rotation = Quaternion.Euler(
                    Mathf.Sin(time * 2f) * 15f,
                    time * 45f,
                    0f);
                rig.Step(1, Dt);

                records.Add(new FrameRecord
                {
                    Live = rig.Root.ActiveParticleCount,
                    Escaped = rig.Root.EscapedTotal,
                    Settled = rig.Root.SettledTotal,
                    PositionChecksum = PositionChecksum(rig)
                });
            }
        }

        /// <summary>Order-independent checksum over quantized positions.</summary>
        private static double PositionChecksum(V4TestRig rig)
        {
            Vector4[] positions = rig.ReadPositions();
            double sum = 0;
            foreach (Vector4 p in positions)
            {
                sum += System.Math.Round(p.x * 10000.0) + System.Math.Round(p.y * 10000.0) * 3.0 + System.Math.Round(p.z * 10000.0) * 7.0;
            }

            return sum;
        }

        [Test]
        public void GoldenChoreography_TwoRuns_ProduceIdenticalBaselines()
        {
            var baselineA = new List<FrameRecord>(Frames);
            using (var rigA = V4TestRig.Create(GoldenConfig()))
            {
                RunChoreography(rigA, baselineA);
            }

            var baselineB = new List<FrameRecord>(Frames);
            using (var rigB = V4TestRig.Create(GoldenConfig()))
            {
                RunChoreography(rigB, baselineB);
            }

            Assert.AreEqual(baselineA.Count, baselineB.Count);
            for (int frame = 0; frame < baselineA.Count; frame++)
            {
                Assert.AreEqual(baselineA[frame].Live, baselineB[frame].Live, $"live count diverged at golden frame {frame}");
                Assert.AreEqual(baselineA[frame].Escaped, baselineB[frame].Escaped, $"escaped count diverged at golden frame {frame}");
                Assert.AreEqual(baselineA[frame].Settled, baselineB[frame].Settled, $"settled count diverged at golden frame {frame}");
                Assert.AreEqual(baselineA[frame].PositionChecksum, baselineB[frame].PositionChecksum,
                    $"position checksum diverged at golden frame {frame}");
            }
        }

        [Test]
        public void GoldenChoreography_BehaviorEnvelope_HoldsAcrossChanges()
        {
            // Coarse behavioral envelope assertions that pin down the accepted behavior
            // of the golden run without hardcoding exact float values: fluid stays
            // conserved, some particles escape through the hole under swirl, nothing NaNs.
            var records = new List<FrameRecord>(Frames);
            using var rig = V4TestRig.Create(GoldenConfig());
            RunChoreography(rig, records);

            FrameRecord last = records[records.Count - 1];
            Assert.AreEqual(rig.Root.SpawnedTotal, last.Live + last.Settled, "conservation broken in golden run");
            Assert.Greater(last.Escaped, 0, "golden choreography must eject at least one particle through the hole");

            for (int frame = 1; frame < records.Count; frame++)
            {
                Assert.LessOrEqual(records[frame].Live, records[frame - 1].Live + 0, "live count increased - particles duplicated");
                Assert.GreaterOrEqual(records[frame].Escaped, records[frame - 1].Escaped, "escaped total decreased");
                Assert.GreaterOrEqual(records[frame].Settled, records[frame - 1].Settled, "settled total decreased");
                Assert.IsFalse(double.IsNaN(records[frame].PositionChecksum), $"NaN checksum at frame {frame}");
            }
        }
    }
}
