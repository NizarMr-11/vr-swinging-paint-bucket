using HarmonicEngine.Core.Attributes;
using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Infrastructure.Rendering;
using UnityEngine;

namespace HarmonicEngine.Domain.Models
{
    [CreateAssetMenu(fileName = "FluidProfile", menuName = "HarmonicEngine/Fluid Profile")]
    public sealed class HarmonicFluidProfile : ScriptableObject
    {
        [Header("Semantic dimensions")]
        [Range(0f, 1f)] [SerializeField] private float thickness = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float surfaceTension = 0.4f;
        [Range(0f, 1f)] [SerializeField] private float bounciness = 0.1f;
        [Range(0f, 1f)] [SerializeField] private float incompressibility = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float adhesion = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float visualGloss = 0.7f;

        [Header("Derived physics (read-only)")]
        [SerializeField, ReadOnly] private float derivedViscosity;
        [SerializeField, ReadOnly] private float derivedDamping;
        [SerializeField, ReadOnly] private float derivedCohesion;
        [SerializeField, ReadOnly] private int derivedIterations;
        [SerializeField, ReadOnly] private float derivedMaxPositionDelta;
        [SerializeField, ReadOnly] private float derivedRestitution;
        [SerializeField, ReadOnly] private float derivedFriction;

        [Header("Derived render (read-only)")]
        [SerializeField, ReadOnly] private float derivedThicknessWeight;
        [SerializeField, ReadOnly] private float derivedThicknessAbsorption;
        [SerializeField, ReadOnly] private float derivedBlurRadius;
        [SerializeField, ReadOnly] private float derivedBlurFalloff;
        [SerializeField, ReadOnly] private float derivedSpecularPower;
        [SerializeField, ReadOnly] private float derivedSpecularIntensity;

        public float Thickness
        {
            get => thickness;
            set => thickness = Mathf.Clamp01(value);
        }

        public float SurfaceTension
        {
            get => surfaceTension;
            set => surfaceTension = Mathf.Clamp01(value);
        }

        public float Bounciness
        {
            get => bounciness;
            set => bounciness = Mathf.Clamp01(value);
        }

        public float Incompressibility
        {
            get => incompressibility;
            set => incompressibility = Mathf.Clamp01(value);
        }

        public float Adhesion
        {
            get => adhesion;
            set => adhesion = Mathf.Clamp01(value);
        }

        public float VisualGloss
        {
            get => visualGloss;
            set => visualGloss = Mathf.Clamp01(value);
        }

        public float DerivedViscosity => derivedViscosity;
        public float DerivedDamping => derivedDamping;
        public float DerivedCohesion => derivedCohesion;
        public int DerivedIterations => derivedIterations;
        public float DerivedMaxPositionDelta => derivedMaxPositionDelta;

        private void OnValidate() => RebuildDerived();

        public void RebuildDerived()
        {
            derivedViscosity = 3.375f + thickness * 20.25f;
            derivedDamping = Mathf.Clamp(0.99f - thickness * 0.4f, 0.5f, 0.99f);
            derivedCohesion = Mathf.Clamp01(0.05f + surfaceTension * 0.8f);
            derivedIterations = Mathf.Max(2, Mathf.Clamp(Mathf.RoundToInt(incompressibility * 10f / 3f), 1, 8));
            derivedMaxPositionDelta = 0.01f + incompressibility * 0.0125f;
            derivedRestitution = Mathf.Clamp01(bounciness);
            derivedFriction = Mathf.Clamp01(1f - adhesion * 0.3f);

            derivedThicknessWeight = Mathf.Lerp(0.02f, 0.08f, thickness);
            derivedThicknessAbsorption = Mathf.Lerp(2f, 10f, thickness);
            derivedBlurRadius = Mathf.Lerp(2f, 5f, surfaceTension);
            derivedBlurFalloff = Mathf.Lerp(0.03f, 0.08f, surfaceTension);
            derivedSpecularPower = Mathf.Lerp(100f, 400f, visualGloss);
            derivedSpecularIntensity = Mathf.Lerp(0.5f, 2f, visualGloss);
        }

        public void ApplyTo(PipelineExecutionController pipeline, HarmonicScreenSpaceFluidRenderer renderer)
        {
            RebuildDerived();
            if (pipeline == null)
            {
                return;
            }

            if (!pipeline.ContainerFluidEnabled)
            {
                pipeline.SetContainerFluidEnabled(true);
            }

            pipeline.SetUsePbf(true);
            pipeline.SetContainerViscosity(derivedViscosity);
            pipeline.SetPbfVelocityDamping(derivedDamping);
            pipeline.SetPbfCohesion(derivedCohesion);
            pipeline.SetPbfIterations(derivedIterations);
            pipeline.SetPbfMaxPositionDelta(derivedMaxPositionDelta);
            pipeline.SetContainerRestitution(derivedRestitution);
            pipeline.SetContainerFriction(derivedFriction);

            if (renderer == null)
            {
                return;
            }

            renderer.ThicknessWeight = derivedThicknessWeight;
            renderer.ThicknessAbsorption = derivedThicknessAbsorption;
            renderer.BlurRadius = derivedBlurRadius;
            renderer.BlurFalloff = derivedBlurFalloff;
            renderer.SpecularPower = derivedSpecularPower;
            renderer.SpecularIntensity = derivedSpecularIntensity;
        }
    }
}
