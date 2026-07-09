using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Core;
using HarmonicEngineV4.Networking;
using HarmonicEngineV4.Networking.State;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4HoleBridgeTests
    {
        [Test]
        public void CloneHoles_DeepCopiesFields()
        {
            var source = new[]
            {
                new HoleDefinition
                {
                    holeIndex = 0,
                    localCenter = new float3(0.1f, 0f, 0.2f),
                    radius = 0.03f,
                    outwardNormal = new float3(0f, -1f, 0f),
                    d0 = 0.02f,
                    d1 = 0.1f,
                    d2 = 0.18f,
                    sdfBias = 0.001f,
                    isActive = true
                }
            };

            HoleDefinition[] clone = V4HoleBridge.CloneHoles(source);
            source[0].d0 = 0.5f;

            Assert.AreEqual(0.02f, clone[0].d0, 1e-5f);
            Assert.AreEqual(new float3(0f, -1f, 0f), clone[0].outwardNormal);
        }

        [Test]
        public void BakedHole_RoundTrip_PreservesZoneRings()
        {
            var baked = new[]
            {
                new V4BakedHole
                {
                    localPosition = new Vector3(0.1f, 0f, 0.2f),
                    radius = 0.03f,
                    outwardNormal = Vector3.down,
                    d0 = 0.02f,
                    d1 = 0.1f,
                    d2 = 0.18f,
                    pad0 = 0.001f
                }
            };

            HoleDefinition[] dto = new[]
            {
                new HoleDefinition
                {
                    holeIndex = 0,
                    localCenter = new float3(baked[0].localPosition.x, baked[0].localPosition.y, baked[0].localPosition.z),
                    radius = baked[0].radius,
                    outwardNormal = new float3(baked[0].outwardNormal.x, baked[0].outwardNormal.y, baked[0].outwardNormal.z),
                    d0 = baked[0].d0,
                    d1 = baked[0].d1,
                    d2 = baked[0].d2,
                    sdfBias = baked[0].pad0,
                    isActive = true
                }
            };

            var restored = new V4BakedHole[dto.Length];
            for (int i = 0; i < dto.Length; i++)
            {
                HoleDefinition hole = dto[i];
                restored[i] = new V4BakedHole
                {
                    localPosition = new Vector3(hole.localCenter.x, hole.localCenter.y, hole.localCenter.z),
                    radius = hole.radius,
                    outwardNormal = new Vector3(hole.outwardNormal.x, hole.outwardNormal.y, hole.outwardNormal.z),
                    d0 = hole.d0,
                    d1 = hole.d1,
                    d2 = hole.d2,
                    pad0 = hole.sdfBias
                };
            }

            Assert.AreEqual(baked[0].d1, restored[0].d1, 1e-5f);
            Assert.AreEqual(baked[0].outwardNormal, restored[0].outwardNormal);
        }
    }
}
