using SwingingPaintBucket.Materials;
using UnityEngine;

namespace SwingingPaintBucket.Canvas
{
    public class CanvasOverlayBinder : MonoBehaviour
    {
        [Header("Surface Selection")]
        [SerializeField] private CanvasSurfaceType surfaceType = CanvasSurfaceType.Fabric;

        [Header("Surface Materials")]
        [SerializeField] private Material fabricMaterial;
        [SerializeField] private Material woodMaterial;
        [SerializeField] private Material metalMaterial;
        [SerializeField] private Material paperMaterial;

        [Header("Dimensions")]
        [SerializeField] private float canvasWidthMeters = 10f;
        [SerializeField] private float canvasHeightMeters = 6f;
        [SerializeField] private float baseThicknessMeters = 0.08f;
        [SerializeField] private float paintLayerThicknessMeters = 0.01f;
        [SerializeField] private float paintLayerGapMeters = 0.004f;
        [SerializeField] private float canvasTiltDegrees = 0f;

        [Header("Scene References")]
        [SerializeField] private Transform texturedBase;
        [SerializeField] private CanvasController paintLayer;

        [Header("Paint Layer Defaults")]
        [SerializeField] private int textureWidth = 1024;
        [SerializeField] private int textureHeight = 1024;
        [SerializeField] private float materialSpreadMultiplier = 1.5f;
        [SerializeField] private float particleImpactSize = 2.5f;

        private MeshRenderer _baseRenderer;

        public CanvasSurfaceType SurfaceType => surfaceType;

        public float CanvasWidthMeters
        {
            get => canvasWidthMeters;
            set
            {
                canvasWidthMeters = Mathf.Max(0.1f, value);
                ApplyAll();
            }
        }

        public float CanvasHeightMeters
        {
            get => canvasHeightMeters;
            set
            {
                canvasHeightMeters = Mathf.Max(0.1f, value);
                ApplyAll();
            }
        }

        public float CanvasTiltDegrees
        {
            get => canvasTiltDegrees;
            set
            {
                canvasTiltDegrees = value;
                ApplyAll();
            }
        }

        private void Awake()
        {
            CacheReferences();
            ApplyAll();
        }

        private void Start()
        {
            CacheReferences();
            ApplyAll();
        }

        private void OnValidate()
        {
            CacheReferences();
            ApplyAll();
        }

        private void CacheReferences()
        {
            if (texturedBase != null)
                _baseRenderer = texturedBase.GetComponent<MeshRenderer>();
        }

        public void SetSurfaceType(CanvasSurfaceType newSurfaceType)
        {
            surfaceType = newSurfaceType;
            ApplySurfaceType();
        }

        public void SetSurfaceTypeByIndex(int index)
        {
            CanvasSurfaceType[] values = (CanvasSurfaceType[])System.Enum.GetValues(typeof(CanvasSurfaceType));

            if (index < 0 || index >= values.Length)
                index = 0;

            SetSurfaceType(values[index]);
        }

        public void ApplyAll()
        {
            ApplyDimensions();
            ApplySurfaceType();
            ApplyPaintLayerSettings();
        }

        private void ApplyDimensions()
        {
            if (texturedBase != null)
            {
                texturedBase.localPosition = Vector3.zero;
                texturedBase.localRotation = Quaternion.Euler(0f, 0f, 0f);
                texturedBase.localScale = new Vector3(canvasWidthMeters, baseThicknessMeters, canvasHeightMeters);
            }

            if (paintLayer != null)
            {
                Transform paintTransform = paintLayer.transform;

                float yOffset = (baseThicknessMeters * 0.5f) + paintLayerGapMeters + (paintLayerThicknessMeters * 0.5f);

                paintTransform.localPosition = new Vector3(0f, yOffset, 0f);
                paintTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                paintTransform.localScale = new Vector3(canvasWidthMeters, paintLayerThicknessMeters, canvasHeightMeters);
            }

            transform.rotation = Quaternion.Euler(canvasTiltDegrees, 0f, 0f);
        }

        private void ApplySurfaceType()
        {
            CacheReferences();

            if (_baseRenderer != null)
            {
                Material selectedMaterial = GetMaterialForSurface(surfaceType);

                if (selectedMaterial != null)
                    _baseRenderer.sharedMaterial = selectedMaterial;
            }

            if (paintLayer != null)
            {
                paintLayer.SurfaceType = surfaceType;
                paintLayer.ApplyCanvasSettings();
            }
        }

        private void ApplyPaintLayerSettings()
        {
            if (paintLayer == null)
                return;

            paintLayer.SurfaceType = surfaceType;
            paintLayer.SurfaceAxis = CanvasSurfaceAxis.XZ_Plane;
            paintLayer.CanvasWidthMeters = canvasWidthMeters;
            paintLayer.CanvasHeightMeters = canvasHeightMeters;
            paintLayer.CanvasTiltDegrees = canvasTiltDegrees;
            paintLayer.TextureWidth = textureWidth;
            paintLayer.TextureHeight = textureHeight;
            paintLayer.MaterialSpreadMultiplier = materialSpreadMultiplier;
            paintLayer.ParticleImpactSize = particleImpactSize;
            paintLayer.UseTransparentBackground = true;
            paintLayer.ForceTransparentMaterial = true;
            paintLayer.ApplyDimensionsToVisualMesh = false;
            paintLayer.ApplyTiltToVisualMesh = false;
            paintLayer.ApplyCanvasSettings();
        }

        private Material GetMaterialForSurface(CanvasSurfaceType selectedType)
        {
            switch (selectedType)
            {
                case CanvasSurfaceType.Fabric:
                    return fabricMaterial;

                case CanvasSurfaceType.Wood:
                    return woodMaterial;

                case CanvasSurfaceType.Metal:
                    return metalMaterial;

                case CanvasSurfaceType.Paper:
                    return paperMaterial;

                default:
                    return fabricMaterial;
            }
        }
    }
}