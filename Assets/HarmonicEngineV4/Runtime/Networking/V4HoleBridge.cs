using System;
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Core;
using HarmonicEngineV4.Networking.State;
using HarmonicEngineV4.Simulation;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngineV4.Networking
{
    /// <summary>Maps between baked <see cref="V4BakedHole"/> GPU data and networking <see cref="HoleDefinition"/> DTOs.</summary>
    public static class V4HoleBridge
    {
        public static HoleDefinition[] CaptureHoles(V4PipelineRoot pipeline)
        {
            if (pipeline == null || !pipeline.Initialized)
            {
                return Array.Empty<HoleDefinition>();
            }

            var baked = pipeline.BakedHoles;
            if (baked == null || baked.Count == 0)
            {
                return Array.Empty<HoleDefinition>();
            }

            var holes = new HoleDefinition[baked.Count];
            for (int i = 0; i < baked.Count; i++)
            {
                V4BakedHole hole = baked[i];
                holes[i] = new HoleDefinition
                {
                    holeIndex = i,
                    localCenter = new float3(hole.localPosition.x, hole.localPosition.y, hole.localPosition.z),
                    radius = hole.radius,
                    outwardNormal = new float3(hole.outwardNormal.x, hole.outwardNormal.y, hole.outwardNormal.z),
                    d0 = hole.d0,
                    d1 = hole.d1,
                    d2 = hole.d2,
                    sdfBias = hole.pad0,
                    isActive = true
                };
            }

            return holes;
        }

        public static void ApplyHoles(V4PipelineRoot pipeline, HoleDefinition[] holes)
        {
            if (pipeline == null || !pipeline.Initialized)
            {
                return;
            }

            if (holes == null || holes.Length == 0)
            {
                pipeline.ApplyNetworkBakedHoles(Array.Empty<V4BakedHole>());
                return;
            }

            var baked = new V4BakedHole[holes.Length];
            for (int i = 0; i < holes.Length; i++)
            {
                HoleDefinition hole = holes[i];
                baked[i] = new V4BakedHole
                {
                    localPosition = new Vector3(hole.localCenter.x, hole.localCenter.y, hole.localCenter.z),
                    radius = hole.radius,
                    outwardNormal = new Vector3(hole.outwardNormal.x, hole.outwardNormal.y, hole.outwardNormal.z),
                    d0 = hole.d0,
                    d1 = hole.d1,
                    d2 = hole.d2,
                    pad0 = hole.sdfBias,
                    pad1 = 0f
                };
            }

            pipeline.ApplyNetworkBakedHoles(baked);
        }

        public static HoleDefinition[] CloneHoles(HoleDefinition[] source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new HoleDefinition[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                HoleDefinition hole = source[i];
                clone[i] = new HoleDefinition
                {
                    holeIndex = hole.holeIndex,
                    localCenter = hole.localCenter,
                    radius = hole.radius,
                    outwardNormal = hole.outwardNormal,
                    d0 = hole.d0,
                    d1 = hole.d1,
                    d2 = hole.d2,
                    sdfBias = hole.sdfBias,
                    isActive = hole.isActive
                };
            }

            return clone;
        }
    }
}
