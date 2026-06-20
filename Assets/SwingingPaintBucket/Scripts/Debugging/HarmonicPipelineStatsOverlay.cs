using System.Text;
using HarmonicEngine.Infrastructure.Management;
using Unity.Profiling;
using UnityEngine;

namespace SwingingPaintBucket.Debugging
{
    /// <summary>
    /// Lightweight on-screen overlay for validating engine performance: shows FPS, frame time,
    /// active particle count, the per-frame spatial-hash sort size (the main optimization knob),
    /// and the CPU submit cost of the GPU pipeline stages via ProfilerRecorders.
    /// </summary>
    [DisallowMultipleComponent]
    public class HarmonicPipelineStatsOverlay : MonoBehaviour
    {
        [SerializeField] private PipelineExecutionController pipeline;
        [SerializeField] private bool visible = true;

        public bool IsVisible => visible;

        public void SetVisible(bool value) => visible = value;
        [SerializeField] private int fontSize = 16;
        [SerializeField, Min(1)] private int smoothingFrames = 30;

        private ProfilerRecorder _gridRecorder;
        private ProfilerRecorder _sortRecorder;
        private ProfilerRecorder _densityRecorder;
        private ProfilerRecorder _integrationRecorder;
        private ProfilerRecorder _containerRecorder;

        private float _smoothedDeltaMs;
        private readonly StringBuilder _sb = new(256);
        private GUIStyle _style;

        private void Awake()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
            }
        }

        private void OnEnable()
        {
            _gridRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.SpatialHashGrid");
            _sortRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.BitonicSort");
            _densityRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.SphDensity");
            _integrationRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.SphIntegration");
            _containerRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.ContainerFluidFrame");
        }

        private void OnDisable()
        {
            _gridRecorder.Dispose();
            _sortRecorder.Dispose();
            _densityRecorder.Dispose();
            _integrationRecorder.Dispose();
            _containerRecorder.Dispose();
        }

        private void Update()
        {
            float t = 1f / Mathf.Max(1, smoothingFrames);
            _smoothedDeltaMs = Mathf.Lerp(_smoothedDeltaMs, Time.unscaledDeltaTime * 1000f, t);
        }

        private static double ToMs(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.LastValue * 1e-6 : 0.0;
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.UpperLeft,
                fontStyle = FontStyle.Bold
            };
            _style.normal.textColor = Color.white;

            float fps = _smoothedDeltaMs > 0.001f ? 1000f / _smoothedDeltaMs : 0f;
            uint active = pipeline != null ? pipeline.GetActiveParticleCount() : 0;
            int sortSize = pipeline != null ? pipeline.FrameSortSize : 0;
            int maxSort = pipeline != null ? pipeline.PaddedSortSize : 0;

            _sb.Clear();
            _sb.AppendLine($"FPS {fps:F0}   frame {_smoothedDeltaMs:F2} ms");
            _sb.AppendLine($"Active particles: {active}");
            _sb.AppendLine($"Sort size: {sortSize} / {maxSort} (cap)");
            _sb.AppendLine($"Pipeline CPU: {ToMs(_containerRecorder):F3} ms");
            _sb.AppendLine($"  grid {ToMs(_gridRecorder):F3}  sort {ToMs(_sortRecorder):F3}");
            _sb.AppendLine($"  density {ToMs(_densityRecorder):F3}  integ {ToMs(_integrationRecorder):F3}");

            GUI.Label(new Rect(12f, 12f, 460f, 220f), _sb.ToString(), _style);
        }
    }
}
