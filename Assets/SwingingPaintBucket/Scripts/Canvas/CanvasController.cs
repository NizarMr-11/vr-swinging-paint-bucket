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

        private Texture2D _canvasTexture;
        private Color[] _pixels;
        private Renderer _renderer;
        private float _canvasWidth;
        private float _canvasHeight;

        // ---- Tracks the last place the paint hit to connect the dots ----
        private Vector2? _lastHitPixel = null;

        // ---- Unity Methods ----
        private void Start()
        {
            _canvasWidth = transform.localScale.x;
            _canvasHeight = transform.localScale.y;

            //  Texture2D Empty
            _canvasTexture = new Texture2D(TextureWidth, TextureHeight);
            _pixels = new Color[TextureWidth * TextureHeight];

            // Fill with white
            for (int i = 0; i < _pixels.Length; i++)
                _pixels[i] = Color.white;

            _canvasTexture.SetPixels(_pixels);
            _canvasTexture.Apply();

            // Attach with quads
            _renderer = GetComponent<Renderer>();
            _renderer.material.mainTexture = _canvasTexture;

            Debug.Log($"[CanvasController] canvas ready: {TextureWidth}×{TextureHeight} pixel");
        }

        private void Update()
        {
            // The shortcut to test PNG saving
            if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
            {
                SaveArtworkToPNG();
            }
        }

        public void OnParticleHit(Vector3 hitPosition, Color color, float viscosity)
        {
            float u = (hitPosition.x - transform.position.x + _canvasWidth * 0.5f) / _canvasWidth;
            float v = (hitPosition.z - transform.position.z + _canvasHeight * 0.5f) / _canvasHeight;

            // If the paint hits outside the canvas, break the line
            if (u < 0f || u > 1f || v < 0f || v > 1f)
            {
                _lastHitPixel = null;
                return;
            }

            int pixelX = (int)(u * TextureWidth);
            int pixelY = (int)(v * TextureHeight);
            int baseRadius = Mathf.Max(2, (int)(10f / viscosity));

            Vector2 currentHitPixel = new Vector2(pixelX, pixelY);

            // ---- ORGANIC INTERPOLATION LOGIC ----
            if (_lastHitPixel != null)
            {
                float distance = Vector2.Distance(_lastHitPixel.Value, currentHitPixel);
                float stepSize = Mathf.Max(1f, baseRadius * 0.5f);
                int steps = Mathf.CeilToInt(distance / stepSize);

                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    Vector2 basePos = Vector2.Lerp(_lastHitPixel.Value, currentHitPixel, t);

                    // 1. Radius Pulsing: Slightly vary the thickness
                    int currentRadius = baseRadius + UnityEngine.Random.Range(-1, 2);

                    // 2. Line Jitter: Wiggle the brush stroke off the perfect center
                    float jitter = 1.5f;
                    int xPos = (int)(basePos.x + UnityEngine.Random.Range(-jitter, jitter));
                    int yPos = (int)(basePos.y + UnityEngine.Random.Range(-jitter, jitter));

                    DrawSplat(xPos, yPos, Mathf.Max(1, currentRadius), color);

                    // 3. Micro-Splashes: 30% chance to throw tiny droplets outside the stroke
                    if (UnityEngine.Random.value > 0.7f)
                    {
                        // Calculate a random splash position
                        float splashSpread = baseRadius * 3.0f;
                        int splashX = (int)(basePos.x + UnityEngine.Random.Range(-splashSpread, splashSpread));
                        int splashY = (int)(basePos.y + UnityEngine.Random.Range(-splashSpread, splashSpread));

                        // Splashes are tiny!
                        int splashRadius = UnityEngine.Random.Range(1, 3);

                        // Lower the alpha so splashes blend smoothly
                        Color splashColor = color;
                        splashColor.a = UnityEngine.Random.Range(0.3f, 0.7f);

                        DrawSplat(splashX, splashY, splashRadius, splashColor);
                    }
                }
            }
            else
            {
                DrawSplat(pixelX, pixelY, baseRadius, color);
            }

            _lastHitPixel = currentHitPixel;
            _canvasTexture.SetPixels(_pixels);
            _canvasTexture.Apply(false);
        }

        public void ClearCanvas()
        {
            for (int i = 0; i < _pixels.Length; i++)
                _pixels[i] = Color.white;

            _lastHitPixel = null; // Reset the line memory when clearing

            _canvasTexture.SetPixels(_pixels);
            _canvasTexture.Apply(false);
        }

        private void DrawSplat(int centerX, int centerY, int radius, Color color)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    // out of boundaries
                    if (x < 0 || x >= TextureWidth || y < 0 || y >= TextureHeight)
                        continue;

                    // distance from center
                    float dist = Vector2.Distance(
                        new Vector2(x, y),
                        new Vector2(centerX, centerY)
                    );

                    // draw within the radius
                    if (dist <= radius)
                    {
                        // smooth edges
                        float alpha = 1f - (dist / radius);

                        int index = y * TextureWidth + x;

                        // mixing colors
                        _pixels[index] = Color.Lerp(_pixels[index], color, alpha);
                    }
                }
            }
        }

        public void SaveArtworkToPNG()
        {
            byte[] textureBytes = _canvasTexture.EncodeToPNG();
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // Saves automatically to the user's default Pictures folder
            string saveDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures);
            string filePath = saveDirectory + "/SimulationResult_" + timestamp + ".png";

            File.WriteAllBytes(filePath, textureBytes);
            Debug.Log($"[CanvasSaver] Artwork successfully saved to: {filePath}");
        }

        // دالة حساب المساحة الحقيقية الملوّنة
        public float CalculateRealPaintedArea()
        {
            int paintedPixels = 0;
            float colorThreshold = 0.95f;

            for (int i = 0; i < _pixels.Length; i++)
            {
                if (_pixels[i].r < colorThreshold || _pixels[i].g < colorThreshold || _pixels[i].b < colorThreshold)
                {
                    paintedPixels++;
                }
            }

            float paintedRatio = (float)paintedPixels / _pixels.Length;
            float realArea = paintedRatio * _canvasWidth * _canvasHeight;
            return realArea;
        }
    }
}