using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class ContainerOrientedBoundsTests
    {
        [Test]
        public void WorldToLocal_FloorPivotIsOrigin()
        {
            Vector3 pivot = new Vector3(1f, 0.5f, -2f);
            Quaternion rotation = Quaternion.Euler(15f, 30f, 0f);
            Matrix4x4 worldToLocal = ContainerOrientedBounds.BuildWorldToLocal(pivot, rotation);

            Vector3 localFloor = ContainerOrientedBounds.WorldToLocal(pivot, worldToLocal);
            Assert.AreEqual(0f, localFloor.x, 1e-4f);
            Assert.AreEqual(0f, localFloor.y, 1e-4f);
            Assert.AreEqual(0f, localFloor.z, 1e-4f);
        }

        [Test]
        public void IsBelowRim_UsesLocalHeight()
        {
            Assert.IsTrue(ContainerOrientedBounds.IsBelowRim(new Vector3(0f, 1.0f, 0f), 1.1f));
            Assert.IsFalse(ContainerOrientedBounds.IsBelowRim(new Vector3(0f, 1.2f, 0f), 1.1f));
        }

        [Test]
        public void ReadRuntimeSnapshot_IncludesSpillOverRimDefault()
        {
            var go = new GameObject("spill-runtime-pipeline");
            var pipeline = go.AddComponent<HarmonicPipelineController>();
            pipeline.SetContainerFluidEnabled(true);

            HarmonicRuntimeTuningSnapshot tuning = pipeline.ReadRuntimeSnapshot();
            Assert.IsFalse(tuning.transferExteriorParticlesToFalling);

            Object.DestroyImmediate(go);
        }
    }
}
