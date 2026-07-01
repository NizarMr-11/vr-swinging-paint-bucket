using System;
using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Tests.PlayMode
{
    internal static class HarmonicGoldenFrameCapture
    {
        public const int DefaultFrameCount = 100;
        public const float DefaultDeltaTime = 0.008f;
        public const float ToleranceFraction = 0.10f;

        public static void RunSimulationFrames(HarmonicPipelineController pipeline)
        {
            for (int frame = 0; frame < DefaultFrameCount; frame++)
            {
                pipeline.ExecutePipelineFrame(DefaultDeltaTime);
            }
        }

        public static HarmonicGoldenFrameMetrics Capture(HarmonicPipelineController pipeline)
        {
            uint activeCount = pipeline.GetActiveParticleCount();
            if (activeCount == 0
                || !pipeline.TryGetDensityCacheBuffers(out ComputeBuffer densities, out _, out _)
                || !pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out _)
                || densities == null
                || soa == null)
            {
                return HarmonicGoldenFrameMetrics.Create(0f, 0f, 0f, 0u, 0, DefaultDeltaTime);
            }

            int sampleCount = (int)activeCount;
            var densityData = new float[sampleCount];
            densities.GetData(densityData, 0, 0, sampleCount);

            float rho0 = pipeline.RestDensity;
            float sumC = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                sumC += densityData[i] / rho0 - 1f;
            }

            FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(soa, sampleCount);
            float sumSpeed = 0f;
            float maxY = float.MinValue;
            for (int i = 0; i < particles.Length; i++)
            {
                float y = particles[i].Position.y;
                maxY = Mathf.Max(maxY, y);
                sumSpeed += math.length(particles[i].Velocity);
            }

            return HarmonicGoldenFrameMetrics.Create(
                sumC / sampleCount,
                sumSpeed / sampleCount,
                maxY,
                activeCount,
                DefaultFrameCount,
                DefaultDeltaTime);
        }

        public static HarmonicPipelineController CreateLabEquivalentPipeline()
        {
            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 200_000, autoRun: false);
            pipeline.SetUsePbf(true);
            pipeline.SetUseRadixSort(true);
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetContainerFluid(
                new Vector3(0f, 0.55f, 0f),
                radius: 0.55f,
                floorY: 0f,
                rimY: 1.1f,
                restitution: 0.1f,
                friction: 0.85f,
                wallStiffness: 400f);
            pipeline.SetPbfIterations(2);
            pipeline.SetPbfVelocityDamping(0.85f);
            pipeline.SetPbfMaxPositionDelta(0.015f);
            pipeline.SetPbfCohesion(0.3f);
            pipeline.ApplyDiagnosticsSettings(new HarmonicPipelineDiagnosticsSettings
            {
                positionSampleInterval = 1,
                positionSampleCount = 256,
                perfDiagnosticsMuted = true,
                mutePbfTelemetry = true
            });

            int spawned = pipeline.TrySpawnContainerLatticeFill();
            if (spawned <= 0)
            {
                throw new InvalidOperationException("Golden frame setup failed: lattice spawn returned 0 particles.");
            }

            return pipeline;
        }

        public static void AssertWithinTolerance(
            HarmonicGoldenFrameMetrics actual,
            HarmonicGoldenFrameMetrics expected,
            float toleranceFraction = ToleranceFraction)
        {
            AssertWithinTolerance(actual.avgC, expected.avgC, toleranceFraction, nameof(actual.avgC));
            AssertWithinTolerance(actual.avgSpeed, expected.avgSpeed, toleranceFraction, nameof(actual.avgSpeed));
            AssertWithinTolerance(actual.maxY, expected.maxY, toleranceFraction, nameof(actual.maxY));
            AssertWithinTolerance(actual.activeCount, expected.activeCount, toleranceFraction, nameof(actual.activeCount));
        }

        private static void AssertWithinTolerance(
            float actual,
            float expected,
            float toleranceFraction,
            string label)
        {
            float tolerance = Mathf.Max(Mathf.Abs(expected) * toleranceFraction, 1e-4f);
            if (Mathf.Abs(actual - expected) > tolerance)
            {
                throw new AssertionException(
                    $"{label} expected {expected:F6} ±{tolerance:F6} but was {actual:F6}");
            }
        }

        private static void AssertWithinTolerance(
            uint actual,
            uint expected,
            float toleranceFraction,
            string label)
        {
            float tolerance = Mathf.Max(expected * toleranceFraction, 1f);
            if (Mathf.Abs(actual - expected) > tolerance)
            {
                throw new AssertionException(
                    $"{label} expected {expected} ±{tolerance:F1} but was {actual}");
            }
        }
    }
}
