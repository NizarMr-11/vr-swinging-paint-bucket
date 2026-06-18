using HarmonicEngine.Infrastructure.PlaybackStreaming;
using UnityEngine;
using System.IO;
using UnityEngine.InputSystem;

namespace SwingingPaintBucket.Canvas
{
    public class CanvasController : MonoBehaviour
    {
        [Header("canvas quality")]
        public int TextureWidth = 1024;
        public int TextureHeight = 1024;

        [Header("Impasto")]
        [SerializeField] private bool useImpastoShader = true;
        [SerializeField] private float impastoDisplacementScale = 0.025f;

        [Header("Paint drying")]
        [SerializeField, Min(0f)] private float wetnessDryRate = 0.35f;
        [SerializeField, Range(0f, 1f)] private float dryDesaturation = 0.25f;
        [SerializeField, Range(0f, 1f)] private float dryDarkening = 0.08f;

        private Texture2D _canvasTexture;
        private Color[] _pixels;
        private float[] _wetnessMap;
        private Renderer _renderer;
        private Material _impastoMaterial;

        private float _canvasWidth;
        private float _canvasHeight;

        // Tracks the last paint hit to connect strokes (classic canvas mode).
        private Vector2? _lastHitPixel;

        public Texture2D AlbedoTexture => _canvasTexture;

        private void Start()
        {
            _canvasWidth = transform.localScale.x;
            _canvasHeight = transform.localScale.y;

            _canvasTexture = new Texture2D(TextureWidth, TextureHeight);
            _pixels = new Color[TextureWidth * TextureHeight];
            _wetnessMap = new float[TextureWidth * TextureHeight];

            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = Color.white;
                _wetnessMap[i] = 0f;
            }

            _canvasTexture.SetPixels(_pixels);
            _canvasTexture.Apply();

            _renderer = GetComponent<Renderer>();
            if (useImpastoShader)
            {
                ApplyImpastoMaterial();
            }
            else
            {
                _renderer.material.mainTexture = _canvasTexture;
            }

            Debug.Log($"[CanvasController] canvas ready: {TextureWidth}×{TextureHeight} pixel");
        }

        private void Update()
        {
            DecayWetnessAndDry(Time.deltaTime);

            if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
            {
                SaveArtworkToPNG();
            }
        }

        public void ConfigureImpasto(HighScaleFramePresenter presenter)
        {
            if (_impastoMaterial == null)
            {
                ApplyImpastoMaterial();
            }

            presenter?.BindRenderer(_renderer, _impastoMaterial);
        }

        public bool TryWorldToUv(Vector3 worldPosition, out Vector2 uv)
        {
            float u = (worldPosition.x - transform.position.x + _canvasWidth * 0.5f) / _canvasWidth;
            float v = (worldPosition.z - transform.position.z + _canvasHeight * 0.5f) / _canvasHeight;
            uv = new Vector2(u, v);
            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        public void OnParticleHit(Vector3 hitPosition, Color color, float viscosity)
        {
            OnParticleHit(hitPosition, color, viscosity, wetnessDeposit: 1f);
        }

        public void OnParticleHit(Vector3 hitPosition, Color color, float viscosity, float wetnessDeposit)
        {
            if (!TryWorldToUv(hitPosition, out Vector2 uv))
            {
                _lastHitPixel = null;
                return;
            }

            int pixelX = (int)(uv.x * TextureWidth);
            int pixelY = (int)(uv.y * TextureHeight);
            int baseRadius = Mathf.Max(2, (int)(10f / viscosity));
            Vector2 currentHitPixel = new Vector2(pixelX, pixelY);
            float wetBoost = Mathf.Clamp01(wetnessDeposit);

            if (_lastHitPixel != null)
            {
                float distance = Vector2.Distance(_lastHitPixel.Value, currentHitPixel);
                float stepSize = Mathf.Max(1f, baseRadius * 0.5f);
                int steps = Mathf.CeilToInt(distance / stepSize);

                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    Vector2 basePos = Vector2.Lerp(_lastHitPixel.Value, currentHitPixel, t);
                    int currentRadius = baseRadius + Random.Range(-1, 2);
                    float jitter = 1.5f;
                    int xPos = (int)(basePos.x + Random.Range(-jitter, jitter));
                    int yPos = (int)(basePos.y + Random.Range(-jitter, jitter));
                    DrawSplat(xPos, yPos, Mathf.Max(1, currentRadius), color, wetBoost);

                    if (Random.value > 0.7f)
                    {
                        float splashSpread = baseRadius * 3.0f;
                        int splashX = (int)(basePos.x + Random.Range(-splashSpread, splashSpread));
                        int splashY = (int)(basePos.y + Random.Range(-splashSpread, splashSpread));
                        int splashRadius = Random.Range(1, 3);
                        Color splashColor = color;
                        splashColor.a = Random.Range(0.3f, 0.7f);
                        DrawSplat(splashX, splashY, splashRadius, splashColor, wetBoost * 0.5f);
                    }
                }
            }
            else
            {
                DrawSplat(pixelX, pixelY, baseRadius, color, wetBoost);
            }

            _lastHitPixel = currentHitPixel;
            _canvasTexture.SetPixels(_pixels);
            _canvasTexture.Apply(false);

            if (_impastoMaterial != null)
            {
                _impastoMaterial.SetTexture("_MainTex", _canvasTexture);
            }
        }

        public void ClearCanvas()
        {
            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = Color.white;
                _wetnessMap[i] = 0f;
            }

            _lastHitPixel = null;

            _canvasTexture.SetPixels(_pixels);
            _canvasTexture.Apply(false);

            if (_impastoMaterial != null)
            {
                _impastoMaterial.SetTexture("_MainTex", _canvasTexture);
            }
        }

        private void ApplyImpastoMaterial()
        {
            Shader shader = Shader.Find("HarmonicEngine/ImpastoCanvasDisplace");
            if (shader == null || _renderer == null)
            {
                return;
            }

            _impastoMaterial = new Material(shader);
            _impastoMaterial.SetTexture("_MainTex", _canvasTexture);
            _impastoMaterial.SetFloat("_DisplacementScale", impastoDisplacementScale);
            _renderer.material = _impastoMaterial;
        }

        private void DrawSplat(int centerX, int centerY, int radius, Color color, float wetnessDeposit = 1f)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    if (x < 0 || x >= TextureWidth || y < 0 || y >= TextureHeight)
                    {
                        continue;
                    }

                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    if (dist <= radius)
                    {
                        float alpha = 1f - (dist / radius);
                        int index = y * TextureWidth + x;
                        _pixels[index] = Color.Lerp(_pixels[index], color, alpha);
                        _wetnessMap[index] = Mathf.Max(_wetnessMap[index], wetnessDeposit * alpha);
                    }
                }
            }
        }

        private void DecayWetnessAndDry(float deltaTime)
        {
            if (_wetnessMap == null || wetnessDryRate <= 0f)
            {
                return;
            }

            float decay = wetnessDryRate * deltaTime;
            bool changed = false;

            for (int i = 0; i < _wetnessMap.Length; i++)
            {
                float wetness = _wetnessMap[i];
                if (wetness <= 0.001f)
                {
                    continue;
                }

                float previous = wetness;
                wetness = Mathf.Max(0f, wetness - decay);
                _wetnessMap[i] = wetness;

                float driedFraction = previous - wetness;
                if (driedFraction > 0f)
                {
                    Color dryTarget = _pixels[i];
                    float gray = dryTarget.grayscale;
                    dryTarget.r = Mathf.Lerp(dryTarget.r, gray, dryDesaturation);
                    dryTarget.g = Mathf.Lerp(dryTarget.g, gray, dryDesaturation);
                    dryTarget.b = Mathf.Lerp(dryTarget.b, gray, dryDesaturation);
                    dryTarget.r *= 1f - dryDarkening;
                    dryTarget.g *= 1f - dryDarkening;
                    dryTarget.b *= 1f - dryDarkening;
                    _pixels[i] = Color.Lerp(_pixels[i], dryTarget, driedFraction);
                    changed = true;
                }
            }

            if (changed)
            {
                _canvasTexture.SetPixels(_pixels);
                _canvasTexture.Apply(false);

                if (_impastoMaterial != null)
                {
                    _impastoMaterial.SetTexture("_MainTex", _canvasTexture);
                }
            }
        }

        public void SaveArtworkToPNG()
        {
            byte[] textureBytes = _canvasTexture.EncodeToPNG();
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string saveDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures);
            string filePath = saveDirectory + "/SimulationResult_" + timestamp + ".png";
            File.WriteAllBytes(filePath, textureBytes);
            Debug.Log($"[CanvasSaver] Artwork successfully saved to: {filePath}");
        }
    }
}
