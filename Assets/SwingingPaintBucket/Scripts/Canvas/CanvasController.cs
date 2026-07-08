using System.IO;
using SwingingPaintBucket.Materials;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SwingingPaintBucket.Canvas
{
    public enum CanvasSurfaceAxis
    {
        Auto,
        XY_Quad,
        XZ_Plane
    }

    public class CanvasController : MonoBehaviour
    {
        [Header("Canvas Surface")]
        public CanvasSurfaceType SurfaceType = CanvasSurfaceType.Fabric;

        [Tooltip("Auto is recommended. Use XY_Quad for Unity Quad meshes. Use XZ_Plane for Unity Plane meshes.")]
        public CanvasSurfaceAxis SurfaceAxis = CanvasSurfaceAxis.Auto;

        [Tooltip("Canvas physical width in metres. This is used for painting coordinates and report area.")]
        [Range(0.5f, 10f)] public float CanvasWidthMeters = 3f;

        [Tooltip("Canvas physical height/depth in metres. This is used for painting coordinates and report area.")]
        [Range(0.5f, 10f)] public float CanvasHeightMeters = 3f;

        [Tooltip("0° = horizontal canvas. Positive values incline the canvas around its local X axis.")]
        [Range(-60f, 60f)] public float CanvasTiltDegrees = 0f;

        [Tooltip("When enabled, the script scales the visual mesh to match Canvas Width/Height. Supports Unity Quad and Unity Plane meshes.")]
        public bool ApplyDimensionsToVisualMesh = true;

        [Tooltip("When enabled, the script rotates the visible canvas to match Canvas Tilt.")]
        public bool ApplyTiltToVisualMesh = true;

        [Header("Canvas Quality")]
        public int TextureWidth = 1024;
        public int TextureHeight = 1024;

        [Header("Painting Physics")]
        [Tooltip("Global spread multiplier applied after the selected surface type.")]
        public float MaterialSpreadMultiplier = 1.0f;

        [Tooltip("The physical volume/impact size of a paint particle hitting the canvas.")]
        public float ParticleImpactSize = 1.0f;

        [Header("Paint Layer Rendering")]
        [Tooltip("Use this when the canvas object is a transparent paint overlay above a textured canvas base.")]
        public bool UseTransparentBackground = false;

        [Tooltip("Automatically configures the material for alpha blending when Use Transparent Background is enabled.")]
        public bool ForceTransparentMaterial = true;

        [Tooltip("Clear color used when Use Transparent Background is disabled.")]
        public Color OpaqueBackgroundColor = Color.white;

        private Texture2D _canvasTexture;
        private Color[] _pixels;
        private Renderer _renderer;
        private MeshFilter _meshFilter;
        private Vector3 _initialEulerAngles;
        private bool _capturedInitialRotation;
        private Vector2? _lastHitPixel;
        private int _paintPathCount;

        public int PaintPathCount => _paintPathCount;

        private void Awake()
        {
            CaptureInitialRotation();
            CacheRendererReferences();
        }

        private void Start()
        {
            InitializeTexture();
            ApplyCanvasSettings();
            Debug.Log($"[CanvasController] Canvas ready: {TextureWidth}×{TextureHeight} pixels. Surface axis: {ResolveSurfaceAxis()}.");
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
                SaveArtworkToPNG();
        }

        private void OnValidate()
        {
            CanvasWidthMeters = Mathf.Max(0.1f, CanvasWidthMeters);
            CanvasHeightMeters = Mathf.Max(0.1f, CanvasHeightMeters);
            MaterialSpreadMultiplier = Mathf.Max(0.01f, MaterialSpreadMultiplier);
            ParticleImpactSize = Mathf.Max(0.01f, ParticleImpactSize);
            TextureWidth = Mathf.Max(128, TextureWidth);
            TextureHeight = Mathf.Max(128, TextureHeight);
            OpaqueBackgroundColor.a = 1f;
        }

        public void ApplyCanvasSettings()
        {
            CaptureInitialRotation();
            CacheRendererReferences();

            if (ApplyDimensionsToVisualMesh)
            {
                Bounds localBounds = GetLocalSurfaceBounds();
                CanvasSurfaceAxis resolvedAxis = ResolveSurfaceAxis();
                Vector3 scale = transform.localScale;

                if (resolvedAxis == CanvasSurfaceAxis.XY_Quad)
                {
                    float meshWidth = Mathf.Max(0.0001f, localBounds.size.x);
                    float meshHeight = Mathf.Max(0.0001f, localBounds.size.y);

                    scale.x = CanvasWidthMeters / meshWidth;
                    scale.y = CanvasHeightMeters / meshHeight;

                    // Unity Quad has zero thickness on Z. Do not let old XZ scaling leave this at huge values like 3000.
                    scale.z = 1f;
                }
                else
                {
                    float meshWidth = Mathf.Max(0.0001f, localBounds.size.x);
                    float meshHeight = Mathf.Max(0.0001f, localBounds.size.z);

                    scale.x = CanvasWidthMeters / meshWidth;
                    scale.z = CanvasHeightMeters / meshHeight;

                    // Unity Plane lies in XZ. Keep Y as harmless visual thickness/scale.
                    scale.y = Mathf.Clamp(scale.y, 0.01f, 10f);
                }

                transform.localScale = scale;
            }

            if (ApplyTiltToVisualMesh)
            {
                transform.rotation = Quaternion.Euler(
                    _initialEulerAngles.x + CanvasTiltDegrees,
                    _initialEulerAngles.y,
                    _initialEulerAngles.z
                );
            }
        }

        public void SetCanvasDimensions(float widthMeters, float heightMeters)
        {
            CanvasWidthMeters = Mathf.Max(0.1f, widthMeters);
            CanvasHeightMeters = Mathf.Max(0.1f, heightMeters);
            ApplyCanvasSettings();
        }

        public void SetCanvasTilt(float tiltDegrees)
        {
            CanvasTiltDegrees = Mathf.Clamp(tiltDegrees, -60f, 60f);
            ApplyCanvasSettings();
        }


        public bool TryProjectPointOntoCanvas(Vector3 sourceWorldPosition, Vector3 projectionDirection, out Vector3 hitPoint)
        {
            hitPoint = sourceWorldPosition;
            Vector3 normal = GetWorldSurfaceNormal();
            Vector3 direction = projectionDirection.sqrMagnitude > 0.000001f
                ? projectionDirection.normalized
                : Vector3.down;

            float denominator = Vector3.Dot(direction, normal);
            if (Mathf.Abs(denominator) < 0.0001f)
                return false;

            Vector3 surfaceOrigin = GetWorldSurfaceOrigin();
            float distance = Vector3.Dot(surfaceOrigin - sourceWorldPosition, normal) / denominator;
            if (distance < 0f)
                return false;

            hitPoint = sourceWorldPosition + direction * distance;
            return IsWorldPointInsideCanvas(hitPoint);
        }

        public bool IsWorldPointInsideCanvas(Vector3 worldPoint)
        {
            Vector3 localHit = transform.InverseTransformPoint(worldPoint);
            Bounds localBounds = GetLocalSurfaceBounds();
            CanvasSurfaceAxis resolvedAxis = ResolveSurfaceAxis();

            float u;
            float v;

            if (resolvedAxis == CanvasSurfaceAxis.XY_Quad)
            {
                u = Mathf.InverseLerp(localBounds.min.x, localBounds.max.x, localHit.x);
                v = Mathf.InverseLerp(localBounds.min.y, localBounds.max.y, localHit.y);
            }
            else
            {
                u = Mathf.InverseLerp(localBounds.min.x, localBounds.max.x, localHit.x);
                v = Mathf.InverseLerp(localBounds.min.z, localBounds.max.z, localHit.z);
            }

            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        public bool PaintProjectedPoint(Vector3 sourceWorldPosition, Color color, float viscosity)
        {
            if (!TryProjectPointOntoCanvas(sourceWorldPosition, Vector3.down, out Vector3 hitPoint))
                return false;

            return OnParticleHit(hitPoint, color, viscosity);
        }

        public bool TryPaintParticle(Vector3 previousPosition, Vector3 currentPosition, Color color, float viscosity)
        {
            if (!TryGetParticleHitPoint(previousPosition, currentPosition, out Vector3 hitPoint))
                return false;

            return OnParticleHit(hitPoint, color, viscosity);
        }

        public bool TryGetParticleHitPoint(Vector3 previousPosition, Vector3 currentPosition, out Vector3 hitPoint)
        {
            hitPoint = currentPosition;

            Vector3 normal = GetWorldSurfaceNormal();
            Vector3 surfaceOrigin = GetWorldSurfaceOrigin();
            float previousDistance = Vector3.Dot(previousPosition - surfaceOrigin, normal);
            float currentDistance = Vector3.Dot(currentPosition - surfaceOrigin, normal);

            bool crossedCanvasPlane =
                (previousDistance >= 0f && currentDistance <= 0f) ||
                (previousDistance <= 0f && currentDistance >= 0f);

            if (!crossedCanvasPlane)
                return false;

            float denominator = previousDistance - currentDistance;
            if (Mathf.Abs(denominator) < 0.0001f)
                return false;

            float t = previousDistance / denominator;
            if (t < 0f || t > 1f)
                return false;

            hitPoint = Vector3.Lerp(previousPosition, currentPosition, t);
            return IsWorldPointInsideCanvas(hitPoint);
        }

        public bool OnParticleHit(Vector3 hitPosition, Color color, float viscosity)
        {
            EnsureTextureReady();

            Vector3 localHit = transform.InverseTransformPoint(hitPosition);
            Bounds localBounds = GetLocalSurfaceBounds();
            CanvasSurfaceAxis resolvedAxis = ResolveSurfaceAxis();

            float u;
            float v;

            if (resolvedAxis == CanvasSurfaceAxis.XY_Quad)
            {
                u = Mathf.InverseLerp(localBounds.min.x, localBounds.max.x, localHit.x);
                v = Mathf.InverseLerp(localBounds.min.y, localBounds.max.y, localHit.y);
            }
            else
            {
                u = Mathf.InverseLerp(localBounds.min.x, localBounds.max.x, localHit.x);
                v = Mathf.InverseLerp(localBounds.min.z, localBounds.max.z, localHit.z);
            }

            if (u < 0f || u > 1f || v < 0f || v > 1f)
            {
                _lastHitPixel = null;
                return false;
            }

            int pixelX = Mathf.Clamp((int)(u * TextureWidth), 0, TextureWidth - 1);
            int pixelY = Mathf.Clamp((int)(v * TextureHeight), 0, TextureHeight - 1);

            float spread = CanvasSurfacePreset.GetSpreadMultiplier(SurfaceType) * MaterialSpreadMultiplier;
            float opacity = CanvasSurfacePreset.GetOpacityMultiplier(SurfaceType);
            float absorption = CanvasSurfacePreset.GetAbsorptionMultiplier(SurfaceType);
            float splash = CanvasSurfacePreset.GetSplashMultiplier(SurfaceType);

            color.a = Mathf.Clamp01(color.a * opacity * (1f - absorption * 0.35f));

            float safeViscosity = Mathf.Max(0.1f, viscosity);
            float calculatedRadius = (10f / safeViscosity) * spread * ParticleImpactSize;
            int baseRadius = Mathf.Max(2, Mathf.RoundToInt(calculatedRadius));

            Vector2 currentHitPixel = new Vector2(pixelX, pixelY);

            if (_lastHitPixel == null)
            {
                _paintPathCount++;
                DrawSplat(pixelX, pixelY, baseRadius, color);
            }
            else
            {
                DrawConnectedStroke(_lastHitPixel.Value, currentHitPixel, baseRadius, color, splash);
            }

            _lastHitPixel = currentHitPixel;
            _canvasTexture.SetPixels(_pixels);
            _canvasTexture.Apply(false);
            return true;
        }

        public void ClearCanvas()
        {
            EnsureTextureReady();

            Color clearColor = GetClearColor();
            for (int i = 0; i < _pixels.Length; i++)
                _pixels[i] = clearColor;

            _lastHitPixel = null;
            _paintPathCount = 0;
            _canvasTexture.SetPixels(_pixels);
            _canvasTexture.Apply(false);
        }

        public void SaveArtworkToPNG()
        {
            EnsureTextureReady();

            byte[] textureBytes = _canvasTexture.EncodeToPNG();
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string saveDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures);
            string filePath = Path.Combine(saveDirectory, "SimulationResult_" + timestamp + ".png");

            File.WriteAllBytes(filePath, textureBytes);
            Debug.Log($"[CanvasSaver] Artwork saved to: {filePath}");
        }

        public float CalculateRealPaintedArea()
        {
            EnsureTextureReady();

            int paintedPixels = 0;
            const float colorThreshold = 0.95f;

            for (int i = 0; i < _pixels.Length; i++)
            {
                if (UseTransparentBackground)
                {
                    if (_pixels[i].a > 0.03f)
                        paintedPixels++;
                }
                else if (_pixels[i].r < colorThreshold || _pixels[i].g < colorThreshold || _pixels[i].b < colorThreshold)
                {
                    paintedPixels++;
                }
            }

            float paintedRatio = (float)paintedPixels / _pixels.Length;
            return paintedRatio * CanvasWidthMeters * CanvasHeightMeters;
        }

        private void DrawConnectedStroke(Vector2 previous, Vector2 current, int baseRadius, Color color, float splashMultiplier)
        {
            float distance = Vector2.Distance(previous, current);
            float stepSize = Mathf.Max(1f, baseRadius * 0.5f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / stepSize));

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector2 basePos = Vector2.Lerp(previous, current, t);

                int currentRadius = Mathf.Max(1, baseRadius + Random.Range(-1, 2));
                float jitter = 1.5f * splashMultiplier;
                int xPos = (int)(basePos.x + Random.Range(-jitter, jitter));
                int yPos = (int)(basePos.y + Random.Range(-jitter, jitter));

                DrawSplat(xPos, yPos, currentRadius, color);

                float splashProbability = Mathf.Clamp01(0.22f * splashMultiplier);
                if (Random.value < splashProbability)
                {
                    float splashSpread = baseRadius * 3.0f * splashMultiplier;
                    int splashX = (int)(basePos.x + Random.Range(-splashSpread, splashSpread));
                    int splashY = (int)(basePos.y + Random.Range(-splashSpread, splashSpread));
                    int splashRadius = Random.Range(1, 3);
                    Color splashColor = color;
                    splashColor.a = Random.Range(0.25f, 0.65f) * color.a;
                    DrawSplat(splashX, splashY, splashRadius, splashColor);
                }
            }
        }

        private void DrawSplat(int centerX, int centerY, int radius, Color color)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    if (x < 0 || x >= TextureWidth || y < 0 || y >= TextureHeight)
                        continue;

                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    if (dist > radius)
                        continue;

                    float alpha = Mathf.Clamp01((1f - (dist / radius)) * color.a);
                    int index = y * TextureWidth + x;
                    _pixels[index] = AlphaBlend(_pixels[index], color, alpha);
                }
            }
        }

        private Color GetClearColor()
        {
            return UseTransparentBackground ? new Color(0f, 0f, 0f, 0f) : OpaqueBackgroundColor;
        }

        private static Color AlphaBlend(Color destination, Color source, float alphaMultiplier)
        {
            float sourceAlpha = Mathf.Clamp01(source.a * alphaMultiplier);
            float destinationAlpha = Mathf.Clamp01(destination.a);
            float outputAlpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);

            if (outputAlpha <= 0.0001f)
                return Color.clear;

            Color output = new Color
            {
                r = ((source.r * sourceAlpha) + (destination.r * destinationAlpha * (1f - sourceAlpha))) / outputAlpha,
                g = ((source.g * sourceAlpha) + (destination.g * destinationAlpha * (1f - sourceAlpha))) / outputAlpha,
                b = ((source.b * sourceAlpha) + (destination.b * destinationAlpha * (1f - sourceAlpha))) / outputAlpha,
                a = outputAlpha
            };

            return output;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
                return;

            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;

            if (material.HasProperty("_Color"))
            {
                Color color = material.color;
                color.a = 1f;
                material.color = color;
            }
        }

        private void InitializeTexture()
        {
            CacheRendererReferences();

            _canvasTexture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
            _pixels = new Color[TextureWidth * TextureHeight];

            Color clearColor = GetClearColor();
            for (int i = 0; i < _pixels.Length; i++)
                _pixels[i] = clearColor;

            _canvasTexture.SetPixels(_pixels);
            _canvasTexture.Apply();

            if (_renderer != null)
            {
                if (UseTransparentBackground && ForceTransparentMaterial)
                    ConfigureTransparentMaterial(_renderer.material);

                _renderer.material.mainTexture = _canvasTexture;
            }
            else
            {
                Debug.LogWarning("[CanvasController] No Renderer found on canvas object.");
            }
        }

        private void EnsureTextureReady()
        {
            if (_canvasTexture == null || _pixels == null || _pixels.Length != TextureWidth * TextureHeight)
                InitializeTexture();
        }

        private void CacheRendererReferences()
        {
            if (_renderer == null)
                _renderer = GetComponent<Renderer>();

            if (_meshFilter == null)
                _meshFilter = GetComponent<MeshFilter>();
        }

        private void CaptureInitialRotation()
        {
            if (_capturedInitialRotation)
                return;

            _initialEulerAngles = transform.rotation.eulerAngles;
            _capturedInitialRotation = true;
        }

        private CanvasSurfaceAxis ResolveSurfaceAxis()
        {
            if (SurfaceAxis != CanvasSurfaceAxis.Auto)
                return SurfaceAxis;

            Bounds bounds = GetLocalSurfaceBounds();

            // Unity Quad meshes lie on local XY and have almost no Z thickness.
            if (bounds.size.z < 0.0001f && bounds.size.y > 0.0001f)
                return CanvasSurfaceAxis.XY_Quad;

            // Unity Plane meshes lie on local XZ and have almost no Y thickness.
            return CanvasSurfaceAxis.XZ_Plane;
        }


        private Vector3 GetWorldSurfaceOrigin()
        {
            CanvasSurfaceAxis resolvedAxis = ResolveSurfaceAxis();
            Bounds bounds = GetLocalSurfaceBounds();
            Vector3 localPoint = bounds.center;

            // For a Cube used as a 3D canvas, the paintable face is the top surface,
            // not the cube center. For a Plane, max.y is normally 0, so this stays correct.
            if (resolvedAxis == CanvasSurfaceAxis.XZ_Plane)
                localPoint.y = bounds.max.y;
            else
                localPoint.z = bounds.max.z;

            return transform.TransformPoint(localPoint);
        }

        private Vector3 GetWorldSurfaceNormal()
        {
            CanvasSurfaceAxis resolvedAxis = ResolveSurfaceAxis();

            if (resolvedAxis == CanvasSurfaceAxis.XY_Quad)
                return transform.forward.normalized;

            return transform.up.normalized;
        }

        private Bounds GetLocalSurfaceBounds()
        {
            CacheRendererReferences();

            if (_meshFilter != null && _meshFilter.sharedMesh != null)
                return _meshFilter.sharedMesh.bounds;

            // Fallback for a normal 1×1 quad-like mesh.
            return new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.0001f));
        }
    }
}
