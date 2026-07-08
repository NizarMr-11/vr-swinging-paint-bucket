using HarmonicEngineV4.Simulation;
using UnityEngine;

namespace HarmonicEngineV4.Rendering
{
    [System.Serializable]
    public struct V4ScreenSpaceFluidOptions
    {
        [Range(0.5f, 5f)] public float splatRadiusMultiplier;
        [Range(0.001f, 0.5f)] public float thicknessWeight;
        public Color fluidColor;
        public bool useParticleColor;
        [Min(0.001f)] public float blurFalloff;
        [Min(0.5f)] public float blurRadius;
        [Min(1)] public int blurIterations;
        public float normalScale;
        [Min(1f)] public float specularPower;
        [Range(0f, 2f)] public float specularIntensity;
        [Min(0.1f)] public float thicknessAbsorption;
        public bool halfResolutionFluid;

        public static V4ScreenSpaceFluidOptions LabDefault => new V4ScreenSpaceFluidOptions
        {
            splatRadiusMultiplier = 2.2f,
            thicknessWeight = 0.15f,
            fluidColor = new Color(0.1f, 0.4f, 0.8f, 1f),
            useParticleColor = true,
            blurFalloff = 0.05f,
            blurRadius = 3f,
            blurIterations = 4,
            normalScale = 100f,
            specularPower = 250f,
            specularIntensity = 1.5f,
            thicknessAbsorption = 40f,
            halfResolutionFluid = true
        };
    }

    /// <summary>
    /// Central fluid display settings on the pipeline object. Pushes toggles and tuning
    /// to <see cref="V4ScreenSpaceFluidRenderer"/> / <see cref="V4DebugPointRenderer"/> on
    /// the target camera each frame (and in the editor via OnValidate).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(V4PipelineRoot))]
    [DefaultExecutionOrder(-100)]
    public sealed class V4RenderingSettings : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("Camera that draws particles. Defaults to Camera.main when empty.")]
        public Camera targetCamera;

        [Header("Fluid display mode")]
        [Tooltip("Screen-space fluid surface (SSFR). Best for liquid appearance.")]
        public bool showScreenSpaceFluid = true;

        [Tooltip("Colored debug quads per particle. Best for containment debugging.")]
        public bool showDebugPoints;

        [Header("Debug points")]
        [Tooltip("Quad half-size in meters. 0 = particle radius.")]
        [Min(0f)] public float debugPointSize;

        [Header("Screen-space fluid")]
        public V4ScreenSpaceFluidOptions screenSpaceFluid = V4ScreenSpaceFluidOptions.LabDefault;

        private V4PipelineRoot _pipeline;
        private V4ScreenSpaceFluidRenderer _ssfr;
        private V4DebugPointRenderer _debugPoints;

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Apply();
        }

        public void Apply()
        {
            if (!ResolvePipeline())
            {
                return;
            }

            if (!ResolveCameraRenderers())
            {
                return;
            }

            if (_ssfr != null)
            {
                _ssfr.SetPipeline(_pipeline);
                _ssfr.ApplyOptions(screenSpaceFluid);
                _ssfr.enabled = showScreenSpaceFluid;
            }

            if (_debugPoints != null)
            {
                _debugPoints.ApplyOptions(_pipeline, debugPointSize);
                _debugPoints.enabled = showDebugPoints;
            }
        }

        private bool ResolvePipeline()
        {
            if (_pipeline == null)
            {
                _pipeline = GetComponent<V4PipelineRoot>();
            }

            return _pipeline != null;
        }

        private bool ResolveCameraRenderers()
        {
            Camera camera = targetCamera != null ? targetCamera : Camera.main;
            if (camera == null)
            {
                _ssfr = null;
                _debugPoints = null;
                return false;
            }

            if (targetCamera == null)
            {
                targetCamera = camera;
            }

            _ssfr = camera.GetComponent<V4ScreenSpaceFluidRenderer>();
            _debugPoints = camera.GetComponent<V4DebugPointRenderer>();

            if (showScreenSpaceFluid && _ssfr == null)
            {
                _ssfr = camera.gameObject.AddComponent<V4ScreenSpaceFluidRenderer>();
            }

            if (showDebugPoints && _debugPoints == null)
            {
                _debugPoints = camera.gameObject.AddComponent<V4DebugPointRenderer>();
            }

            return _ssfr != null || _debugPoints != null;
        }
    }
}
