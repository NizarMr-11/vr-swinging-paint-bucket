using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class HarmonicFluidProfileTests
    {
        [Test]
        public void PaintProfile_DerivesExpectedPhysicsValues()
        {
            var profile = ScriptableObject.CreateInstance<HarmonicFluidProfile>();
            profile.Thickness = 0.5f;
            profile.SurfaceTension = 0.4f;
            profile.Bounciness = 0.1f;
            profile.Incompressibility = 0.6f;
            profile.Adhesion = 0.5f;
            profile.VisualGloss = 0.7f;
            profile.RebuildDerived();

            Assert.AreEqual(13.5f, profile.DerivedViscosity, 1e-3f);
            Assert.AreEqual(0.79f, profile.DerivedDamping, 1e-3f);
            Assert.AreEqual(0.37f, profile.DerivedCohesion, 1e-3f);
            Assert.AreEqual(2, profile.DerivedIterations);
            Assert.AreEqual(0.0175f, profile.DerivedMaxPositionDelta, 1e-4f);

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void ApplyTo_PaintProfile_WritesPipelineTuning()
        {
            var profile = ScriptableObject.CreateInstance<HarmonicFluidProfile>();
            profile.Thickness = 0.5f;
            profile.SurfaceTension = 0.4f;
            profile.Bounciness = 0.1f;
            profile.Incompressibility = 0.6f;
            profile.Adhesion = 0.5f;
            profile.VisualGloss = 0.7f;

            var go = new GameObject("fluid-profile-pipeline");
            var pipeline = go.AddComponent<HarmonicPipelineController>();
            pipeline.SetContainerFluidEnabled(true);
            profile.ApplyTo(pipeline, null);

            HarmonicRuntimeTuningSnapshot tuning = pipeline.ReadRuntimeSnapshot();
            Assert.AreEqual(13.5f, tuning.viscosity, 1e-3f);
            Assert.AreEqual(0.79f, tuning.pbfVelocityDamping, 1e-3f);
            Assert.AreEqual(0.37f, tuning.pbfCohesion, 1e-3f);
            Assert.AreEqual(2, tuning.pbfIterations);
            Assert.AreEqual(0.0175f, tuning.pbfMaxPositionDelta, 1e-4f);
            Assert.IsTrue(tuning.openTopCylinderUsePbf);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void WaterProfile_IsRunnierThanPaint()
        {
            var water = ScriptableObject.CreateInstance<HarmonicFluidProfile>();
            water.Thickness = 0.1f;
            water.SurfaceTension = 0.2f;
            water.Incompressibility = 0.3f;
            water.RebuildDerived();

            var paint = ScriptableObject.CreateInstance<HarmonicFluidProfile>();
            paint.Thickness = 0.5f;
            paint.SurfaceTension = 0.4f;
            paint.Incompressibility = 0.6f;
            paint.RebuildDerived();

            Assert.Less(water.DerivedViscosity, paint.DerivedViscosity);
            Assert.Greater(water.DerivedDamping, paint.DerivedDamping);
            Assert.Less(water.DerivedCohesion, paint.DerivedCohesion);
            Assert.Less(water.DerivedIterations, paint.DerivedIterations);

            Object.DestroyImmediate(water);
            Object.DestroyImmediate(paint);
        }

        [Test]
        public void ThicknessChange_UpdatesDerivedViscosityImmediately()
        {
            var profile = ScriptableObject.CreateInstance<HarmonicFluidProfile>();
            profile.Thickness = 0.5f;
            profile.RebuildDerived();
            float paintViscosity = profile.DerivedViscosity;

            profile.Thickness = 0.1f;
            profile.RebuildDerived();

            Assert.Less(profile.DerivedViscosity, paintViscosity);
            Assert.AreEqual(5.4f, profile.DerivedViscosity, 1e-3f);

            Object.DestroyImmediate(profile);
        }
    }
}
