using System.Collections.Generic;
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Simulation;
using UnityEngine;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Estimates how many particles the current wizard config will spawn.</summary>
    public static class V4Lab2ParticleCountEstimator
    {
        public struct Result
        {
            public int spawnCount;
            public int volumeEstimate;
            public bool hasFillVolume;
        }

        public static Result Estimate(V4Lab2SessionConfig config, V4Bucket bucket)
        {
            Result result = default;
            if (config == null || bucket == null)
            {
                return result;
            }

            var layerDefs = new List<V4LayerDef>(V4Lab2SessionConfig.LayerCount);
            for (int i = 0; i < V4Lab2SessionConfig.LayerCount; i++)
            {
                V4Lab2LayerConfig layer = config.layers[i];
                float thickness = Mathf.Clamp(layer.thickness, 0f, V4Lab2SessionConfig.MaxLayerThickness);
                if (thickness <= 1e-4f)
                {
                    continue;
                }

                layerDefs.Add(new V4LayerDef
                {
                    thickness = Mathf.Max(thickness, 0.001f),
                    color = layer.color
                });
            }

            if (layerDefs.Count == 0)
            {
                return result;
            }

            float particleRadius = V4SpawnMath.ParticleRadiusFromDensity(config.globalDensity);
            float spawnRadius = Mathf.Max(0f, bucket.innerRadius - particleRadius);
            float spawnYMin = particleRadius;
            float spawnYMax = V4BucketBake.ComputeAuthoredFillHeight(layerDefs, bucket.height) - particleRadius;
            if (spawnYMax <= spawnYMin + 1e-6f || spawnRadius <= 1e-6f)
            {
                return result;
            }

            float spawnVolume = V4SpawnMath.CylinderSlabVolume(spawnRadius, spawnYMin, spawnYMax);
            result.volumeEstimate = V4SpawnMath.ExpectedCount(spawnVolume, config.globalDensity);
            result.spawnCount = V4SpawnMath.LatticeFillCylinder(
                spawnRadius,
                spawnYMin,
                spawnYMax,
                config.globalDensity).Count;
            result.hasFillVolume = true;
            return result;
        }

        public static string FormatSummary(Result result)
        {
            if (!result.hasFillVolume)
            {
                return "Particles at start: 0 (no paint volume)";
            }

            return $"Particles at start: {result.spawnCount:N0}  (volume estimate {result.volumeEstimate:N0})";
        }
    }
}
