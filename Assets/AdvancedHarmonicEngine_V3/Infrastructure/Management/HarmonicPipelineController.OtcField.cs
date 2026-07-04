using HarmonicEngine.Infrastructure.Management.Gpu;
using System;
using HarmonicEngine.Domain.Models;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class HarmonicPipelineController
    {
        [SerializeField] private ComputeShader otcParticleFieldShader;

        private const int OtcMaxHoles = 16;
        private const float OtcHoleClipDepthScaleDefault = 1.0f;

        // Phase 3 smooth participation blend width (metres). 0.08 chosen to exceed the worst-case
        // single-frame sweep penetration (~8 cm/frame at ~5 m/s); tune at rake step 7. 0 => boolean parity.
        [SerializeField] private float otcParticipationFadeWidth = 0.08f;
        internal float OtcParticipationFadeWidth => otcParticipationFadeWidth;

        private int _otcHoleCount;
        private readonly Vector4[] _otcHolesUniform = new Vector4[OtcMaxHoles];
        private readonly Vector4[] _otcHoleAxisUniform = new Vector4[OtcMaxHoles];

        internal ComputeShader OtcParticleFieldShader => otcParticleFieldShader;
        internal int KernelOtcClassify => _kernelOtcClassify;

        private int _kernelOtcClassify = -1;

        /// <summary>Uploads container + hole uniforms required by OtcParticleField.hlsl consumers.</summary>
        internal void ApplyOtcFieldUniforms(ComputeShader shader)
        {
            ApplyContainerPbfUniforms(shader);
            shader.SetVector(HarmonicShaderPropertyIds.ContainerCenter, openTopCylinder.center);
            shader.SetFloat(HarmonicShaderPropertyIds.ContainerRadius, openTopCylinder.radius);
            shader.SetFloat(HarmonicShaderPropertyIds.ContainerFloorY, openTopCylinder.floorY);
            shader.SetFloat(HarmonicShaderPropertyIds.ContainerRimY, openTopCylinder.rimY);
            shader.SetFloat(HarmonicShaderPropertyIds.ContainerRestitution, openTopCylinder.restitution);
            shader.SetFloat(HarmonicShaderPropertyIds.ContainerFriction, openTopCylinder.friction);
            ApplyOtcHoleUniforms(shader);
        }

        internal void ApplyOtcHoleUniforms(ComputeShader shader)
        {
            shader.SetInt(HarmonicShaderPropertyIds.HoleCount, _otcHoleCount);
            shader.SetFloat(HarmonicShaderPropertyIds.HoleClipDepthScale, OtcHoleClipDepthScaleDefault);
            shader.SetVectorArray(HarmonicShaderPropertyIds.Holes, _otcHolesUniform);
            shader.SetVectorArray(HarmonicShaderPropertyIds.HoleAxis, _otcHoleAxisUniform);
            // Not hole-specific, but colocated at the shared field bind point so every OtcParticleField.hlsl
            // consumer (classify, carry, PBF) sees a consistent participation width. No consumer reads it yet.
            shader.SetFloat(HarmonicShaderPropertyIds.OtcParticipationFadeWidth, otcParticipationFadeWidth);
        }

        /// <summary>
        /// Projects side holes onto the wall, packs the fixed 16-slot GPU arrays, and stores them for
        /// classify + Apply (both read via <see cref="ApplyOtcHoleUniforms"/>).
        /// </summary>
        public void SetContainerHoles(OtcContainerHole[] holes, float containerRadius, float containerHeight)
        {
            _otcHoleCount = OtcHoleSetupUtility.PackProjectedHoles(
                holes ?? Array.Empty<OtcContainerHole>(),
                containerRadius,
                containerHeight,
                _otcHolesUniform,
                _otcHoleAxisUniform);
        }

        internal int OtcHoleCount => _otcHoleCount;

        private void ResolveOtcParticleFieldShader()
        {
            if (otcParticleFieldShader != null)
            {
                return;
            }

#if UNITY_EDITOR
            otcParticleFieldShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/OtcParticleField.compute");
#endif
        }

        private void CacheOtcFieldKernel()
        {
            _kernelOtcClassify = otcParticleFieldShader != null
                ? otcParticleFieldShader.FindKernel("ClassifyParticleFieldKernel")
                : -1;
        }
    }
}
