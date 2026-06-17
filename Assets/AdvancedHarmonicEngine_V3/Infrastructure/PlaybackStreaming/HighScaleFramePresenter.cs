using HarmonicEngine.Infrastructure.PlaybackStreaming;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.PlaybackStreaming
{
    public class HighScaleFramePresenter : MonoBehaviour
    {
        [SerializeField] private Material targetMaterial;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private string heightMapProperty = "_HeightMap";
        [SerializeField] private string quantizedBufferProperty = "_QuantizedFrameBuffer";

        private Texture2D _heightMap;
        private SlidingWindowDiskQueue _preloadQueue;

        private void Awake()
        {
            _preloadQueue = new SlidingWindowDiskQueue(8);
            EnsureHeightMap(512, 512);
        }

        public void Present(ComputeBuffer quantizedFrameBuffer)
        {
            if (targetMaterial == null || quantizedFrameBuffer == null)
            {
                return;
            }

            targetMaterial.SetBuffer(quantizedBufferProperty, quantizedFrameBuffer);
        }

        public void ApplyHeightMap(Texture2D heightMap)
        {
            if (heightMap == null)
            {
                return;
            }

            _heightMap = heightMap;
            if (targetMaterial != null)
            {
                targetMaterial.SetTexture(heightMapProperty, _heightMap);
            }
        }

        public void BindRenderer(Renderer renderer, Material material)
        {
            targetRenderer = renderer;
            targetMaterial = material;
            if (_heightMap != null && targetMaterial != null)
            {
                targetMaterial.SetTexture(heightMapProperty, _heightMap);
            }
        }

        public void ClearHeightMap()
        {
            EnsureHeightMap(512, 512);
            var clear = new Color[_heightMap.width * _heightMap.height];
            for (int i = 0; i < clear.Length; i++)
            {
                clear[i] = Color.black;
            }

            _heightMap.SetPixels(clear);
            _heightMap.Apply(false);
            ApplyHeightMap(_heightMap);
        }

        public void StampImpastoAtUv(Vector2 uv, float radius, float intensity)
        {
            EnsureHeightMap(512, 512);
            int cx = Mathf.Clamp((int)(uv.x * _heightMap.width), 0, _heightMap.width - 1);
            int cy = Mathf.Clamp((int)(uv.y * _heightMap.height), 0, _heightMap.height - 1);
            int r = Mathf.Max(1, (int)radius);

            Color[] pixels = _heightMap.GetPixels();
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    int px = cx + x;
                    int py = cy + y;
                    if (px < 0 || py < 0 || px >= _heightMap.width || py >= _heightMap.height)
                    {
                        continue;
                    }

                    float dist = Mathf.Sqrt(x * x + y * y) / r;
                    if (dist > 1f)
                    {
                        continue;
                    }

                    int index = py * _heightMap.width + px;
                    float falloff = 1f - dist;
                    pixels[index].r = Mathf.Clamp01(pixels[index].r + intensity * falloff);
                }
            }

            _heightMap.SetPixels(pixels);
            _heightMap.Apply(false);
            ApplyHeightMap(_heightMap);
        }

        public void QueueFrameFile(string framePath)
        {
            _preloadQueue?.Enqueue(framePath);
        }

        public bool TryConsumePreloadedFrame(out string framePath)
        {
            if (_preloadQueue != null && _preloadQueue.TryDequeue(out framePath))
            {
                return true;
            }

            framePath = string.Empty;
            return false;
        }

        private void EnsureHeightMap(int width, int height)
        {
            if (_heightMap != null && _heightMap.width == width && _heightMap.height == height)
            {
                return;
            }

            _heightMap = new Texture2D(width, height, TextureFormat.RFloat, false);
            var clear = new Color[width * height];
            for (int i = 0; i < clear.Length; i++)
            {
                clear[i] = Color.black;
            }

            _heightMap.SetPixels(clear);
            _heightMap.Apply(false);
            ApplyHeightMap(_heightMap);

            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            if (targetRenderer != null && targetMaterial != null)
            {
                targetRenderer.material = targetMaterial;
            }
        }

        private void OnDestroy()
        {
            _preloadQueue?.Dispose();
        }
    }
}
