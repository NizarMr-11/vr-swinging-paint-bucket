using SwingingPaintBucket.Materials;
using UnityEngine;

namespace SwingingPaintBucket.Canvas
{
    /// <summary>
    /// Keeps a textured physical canvas base and a transparent paint layer aligned.
    /// Attach this to an empty parent object, for example PaintCanvasRoot.
    /// No Rigidbody or Collider is required.
    /// </summary>
    [ExecuteAlways]
    public class CanvasOverlayBinder : MonoBehaviour
    {
        [Header("Surface Selection")]
        public CanvasSurfaceType SurfaceType = CanvasSurfaceType.Fabric;
        public Material FabricMaterial;
        public Material WoodMaterial;
        public Material MetalMaterial;
        public Material PaperMaterial;

        [Header("Dimensions")]
        [Min(0.5f)] public float CanvasWidthMeters = 10f;
        [Min(0.5f)] public float CanvasHeightMeters = 6f;
        [Min(0.01f)] public float BaseThicknessMeters = 0.08f;
        [Min(0.001f)] public float PaintLayerThicknessMeters = 0.01f;
        [Min(0.001f)] public float PaintLayerGapMeters = 0.004f;
        [Range(-60f, 60f)] public float CanvasTiltDegrees = 0f;

        [Header("Scene References")]
        public Transform TexturedBase;
        public CanvasController PaintLayer;

        [Header("Paint Layer Defaults")]
        public int TextureWidth = 1024;
        public int TextureHeight = 1024;
        public float MaterialSpreadMultiplier = 1.5f;
        public float ParticleImpactSize = 2.5f;

        private void Reset()
        {
            TryAutoFindReferences();
        }

        private void OnValidate()
        {
            CanvasWidthMeters = Mathf.Max(0.5f, CanvasWidthMeters);
            CanvasHeightMeters = Mathf.Max(0.5f, CanvasHeightMeters);
            BaseThicknessMeters = Mathf.Max(0.01f, BaseThicknessMeters);
            PaintLayerThicknessMeters = Mathf.Max(0.001f, PaintLayerThicknessMeters);
            PaintLayerGapMeters = Mathf.Max(0.001f, PaintLayerGapMeters);
            Apply();
        }

        private void Awake()
        {
            TryAutoFindReferences();
            Apply();
        }

        private void Update()
        {
            if (!Application.isPlaying)
                Apply();
        }

        public void Apply()
        {
            TryAutoFindReferences();

            transform.rotation = Quaternion.Euler(CanvasTiltDegrees, 0f, 0f);

            if (TexturedBase != null)
            {
                TexturedBase.localPosition = Vector3.zero;
                TexturedBase.localRotation = Quaternion.identity;
                TexturedBase.localScale = new Vector3(CanvasWidthMeters, BaseThicknessMeters, CanvasHeightMeters);

                Renderer baseRenderer = TexturedBase.GetComponent<Renderer>();
                if (baseRenderer != null)
                {
                    Material selected = GetSelectedMaterial();
                    if (selected != null)
                        baseRenderer.sharedMaterial = selected;
                }
            }

            if (PaintLayer != null)
            {
                Transform paintTransform = PaintLayer.transform;
                float paintY = (BaseThicknessMeters * 0.5f) + (PaintLayerThicknessMeters * 0.5f) + PaintLayerGapMeters;

                paintTransform.localPosition = new Vector3(0f, paintY, 0f);
                paintTransform.localRotation = Quaternion.identity;
                paintTransform.localScale = new Vector3(CanvasWidthMeters, PaintLayerThicknessMeters, CanvasHeightMeters);

                PaintLayer.SurfaceType = SurfaceType;
                PaintLayer.SurfaceAxis = CanvasSurfaceAxis.XZ_Plane;
                PaintLayer.CanvasWidthMeters = CanvasWidthMeters;
                PaintLayer.CanvasHeightMeters = CanvasHeightMeters;
                PaintLayer.CanvasTiltDegrees = 0f;
                PaintLayer.ApplyDimensionsToVisualMesh = false;
                PaintLayer.ApplyTiltToVisualMesh = false;
                PaintLayer.TextureWidth = TextureWidth;
                PaintLayer.TextureHeight = TextureHeight;
                PaintLayer.MaterialSpreadMultiplier = MaterialSpreadMultiplier;
                PaintLayer.ParticleImpactSize = ParticleImpactSize;
                PaintLayer.UseTransparentBackground = true;
                PaintLayer.ForceTransparentMaterial = true;
            }
        }

        private Material GetSelectedMaterial()
        {
            switch (SurfaceType)
            {
                case CanvasSurfaceType.Fabric: return FabricMaterial;
                case CanvasSurfaceType.Wood: return WoodMaterial;
                case CanvasSurfaceType.Metal: return MetalMaterial;
                case CanvasSurfaceType.Paper: return PaperMaterial;
                default: return FabricMaterial;
            }
        }

        private void TryAutoFindReferences()
        {
            if (TexturedBase == null)
            {
                Transform found = transform.Find("CanvasSurfaceBase");
                if (found != null)
                    TexturedBase = found;
            }

            if (PaintLayer == null)
            {
                Transform found = transform.Find("PaintCanvas3D");
                if (found != null)
                    PaintLayer = found.GetComponent<CanvasController>();
            }
        }
    }
}
