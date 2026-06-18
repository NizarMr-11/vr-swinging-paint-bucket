using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Infrastructure.PlaybackStreaming;
using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Canvas;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwingingPaintBucket.Particles
{
    /// <summary>
    /// Reads compact GPU canvas hits and applies albedo splats + impasto height stamps.
    /// </summary>
    [DisallowMultipleComponent]
    public class HarmonicCanvasHitBridge : MonoBehaviour
    {
        [SerializeField] private PipelineExecutionController pipeline;
        [SerializeField] private CanvasController canvas;
        [SerializeField] private HighScaleFramePresenter impastoPresenter;
        [SerializeField] private BucketController bucket;
        [SerializeField] private float impastoRadius = 8f;
        [SerializeField] private float impastoIntensityScale = 0.15f;

        private bool _readbackInFlight;

        private void Awake()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
            }

            if (canvas == null)
            {
                canvas = FindFirstObjectByType<CanvasController>();
            }

            if (bucket == null)
            {
                bucket = GetComponent<BucketController>();
            }
        }

        public void Bind(
            PipelineExecutionController harmonicPipeline,
            CanvasController canvasController,
            HighScaleFramePresenter presenter,
            BucketController bucketController)
        {
            pipeline = harmonicPipeline;
            canvas = canvasController;
            impastoPresenter = presenter;
            bucket = bucketController;

            if (canvas != null && pipeline != null)
            {
                pipeline.SetCanvasPlaneY(canvas.transform.position.y);
            }
        }

        private void LateUpdate()
        {
            if (pipeline == null || canvas == null || _readbackInFlight)
            {
                return;
            }

            if (!pipeline.TryGetCanvasHitBuffer(out ComputeBuffer hitBuffer, out uint hitCount) || hitCount == 0)
            {
                return;
            }

            int byteCount = (int)hitCount * 32;
            _readbackInFlight = true;
            AsyncGPUReadback.Request(hitBuffer, byteCount, 0, request =>
            {
                _readbackInFlight = false;
                if (request.hasError)
                {
                    return;
                }

                ApplyHits(request.GetData<CanvasPaintHit>());
            });
        }

        private void ApplyHits(NativeArray<CanvasPaintHit> hits)
        {
            Color fallbackColor = bucket != null ? bucket.CurrentPaintColor : Color.red;
            float viscosity = bucket != null ? bucket.Viscosity : 1f;

            for (int i = 0; i < hits.Length; i++)
            {
                CanvasPaintHit hit = hits[i];
                Color paintColor = hit.PackedColorRGBA != 0
                    ? FluidParticleFactory.UnpackColor(hit.PackedColorRGBA)
                    : fallbackColor;

                canvas.OnParticleHit(hit.WorldPosition, paintColor, viscosity, hit.WetnessDeposit);

                if (impastoPresenter != null && canvas.TryWorldToUv(hit.WorldPosition, out Vector2 uv))
                {
                    float intensity = Mathf.Clamp01(hit.PaintWeight * impastoIntensityScale);
                    impastoPresenter.StampImpastoAtUv(uv, impastoRadius, intensity);
                }
            }
        }
    }
}
