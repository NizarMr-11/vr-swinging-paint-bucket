using HarmonicEngineV4.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4DeviceCapsTests
    {
        [Test]
        public void ChooseManifest_ReturnsManifestMatchingDetectedTier()
        {
            var split = ScriptableObject.CreateInstance<V4PassManifest>();
            split.manifestName = "split";
            split.tier = V4DeviceTier.Split;
            var combined = ScriptableObject.CreateInstance<V4PassManifest>();
            combined.manifestName = "combined";
            combined.tier = V4DeviceTier.Combined;

            try
            {
                V4DeviceTier tier = V4DeviceCaps.DetectTier();
                V4PassManifest chosen = V4DeviceCaps.ChooseManifest(split, combined);

                Assert.IsNotNull(chosen);
                Assert.AreEqual(tier == V4DeviceTier.Combined ? "combined" : "split", chosen.manifestName);
            }
            finally
            {
                Object.DestroyImmediate(split);
                Object.DestroyImmediate(combined);
            }
        }

        [Test]
        public void ChooseManifest_FallsBackWhenPreferredVariantMissing()
        {
            var onlySplit = ScriptableObject.CreateInstance<V4PassManifest>();
            onlySplit.manifestName = "split";
            try
            {
                V4PassManifest chosen = V4DeviceCaps.ChooseManifest(onlySplit, null);
                Assert.AreSame(onlySplit, chosen);
            }
            finally
            {
                Object.DestroyImmediate(onlySplit);
            }
        }
    }
}
