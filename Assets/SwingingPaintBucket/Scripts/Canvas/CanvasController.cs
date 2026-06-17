using HarmonicEngine.Infrastructure.PlaybackStreaming;
using UnityEngine;

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

        private Texture2D _canvasTexture;
        private Color[] _pixels;
        private Renderer _renderer;
        private Material _impastoMaterial;

        private float _canvasWidth;
        private float _canvasHeight;

        public Texture2D AlbedoTexture => _canvasTexture;

        private void Start()
        {
            _canvasWidth = transform.localScale.x;
            _canvasHeight = transform.localScale.y;

            _canvasTexture = new Texture2D(TextureWidth, TextureHeight);
            _pixels = new Color[TextureWidth * TextureHeight];

            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = Color.white;
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
            if (!TryWorldToUv(hitPosition, out Vector2 uv))
            {
                return;
            }

            int pixelX = (int)(uv.x * TextureWidth);
            int pixelY = (int)(uv.y * TextureHeight);
            int splatRadius = Mathf.Max(2, (int)(10f / viscosity));
            DrawSplat(pixelX, pixelY, splatRadius, color);

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
            }

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

        private void DrawSplat(int centerX, int centerY, int radius, Color color)
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
                    }
                }
            }
        }
    }
}
