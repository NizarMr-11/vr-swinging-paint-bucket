using UnityEngine;

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

        // ---- Unity Methods ----

        private void Start()
        {

            _canvasWidth = transform.localScale.x;
            _canvasHeight = transform.localScale.y;

            //  Texture2D Empty
            _canvasTexture = new Texture2D(TextureWidth, TextureHeight);
            _pixels = new Color[TextureWidth * TextureHeight];

            // Fill with whiter
            for (int i = 0; i < _pixels.Length; i++)
                _pixels[i] = Color.white;

            _canvasTexture.SetPixels(_pixels);
            _canvasTexture.Apply();

            // Attach with quads
            _renderer = GetComponent<Renderer>();
            _renderer.material.mainTexture = _canvasTexture;

            Debug.Log($"[CanvasController] canvas ready: {TextureWidth}×{TextureHeight} pixel");
        }


        public void OnParticleHit(Vector3 hitPosition, Color color, float viscosity)
        {

            float u = (hitPosition.x - transform.position.x + _canvasWidth * 0.5f) / _canvasWidth;
            float v = (hitPosition.z - transform.position.z + _canvasHeight * 0.5f) / _canvasHeight;


            if (u < 0f || u > 1f || v < 0f || v > 1f) return;


            int pixelX = (int)(u * TextureWidth);
            int pixelY = (int)(v * TextureHeight);


            int splatRadius = Mathf.Max(2, (int)(10f / viscosity));


            DrawSplat(pixelX, pixelY, splatRadius, color);


            _canvasTexture.SetPixels(_pixels);
            _canvasTexture.Apply(false); 
        }


        public void ClearCanvas()
        {
            for (int i = 0; i < _pixels.Length; i++)
                _pixels[i] = Color.white;

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
    }
}
