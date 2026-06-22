using UnityEditor;
using UnityEngine;

namespace HarmonicEngine.Diagnostics.Editor
{
    [CustomEditor(typeof(HarmonicPipelineDiagnosticsController))]
    public sealed class HarmonicPipelineDiagnosticsControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty settings = serializedObject.FindProperty("settings");

            DrawSection("Hub", () =>
            {
                DrawRelative(settings, "enableDiagnostics");
                DrawRelative(settings, "enableFileLog");
                DrawRelative(settings, "enableTelemetry");
            });

            DrawSection("Console", () =>
            {
                DrawRelative(settings, "logToUnityConsole");
                DrawRelative(settings, "logSphToConsole");
                DrawRelative(settings, "muteSphTelemetry");
            });

            DrawSection("Pipeline logs", () =>
            {
                DrawRelative(settings, "verbosePipelineDiagnostics");
                DrawRelative(settings, "frameDiagnosticInterval");
                DrawRelative(settings, "positionSampleInterval");
                DrawRelative(settings, "positionSampleCount");
                DrawRelative(settings, "logStencilNeighborCount");
            });

            DrawSection("Perf sampling", () =>
            {
                var muted = settings.FindPropertyRelative("perfDiagnosticsMuted");
                EditorGUILayout.PropertyField(muted, new GUIContent("Mute expensive GPU readbacks"));
            });

            DrawSection("Profile telemetry", () =>
            {
                DrawRelative(settings, "enableProfileTelemetry");
                DrawRelative(settings, "profileLogInterval");
                DrawRelative(settings, "enableFrameTimingStats");
                DrawRelative(settings, "spikeThresholdMs");
            });

            DrawSection("Overlay", () =>
            {
                DrawRelative(settings, "showOverlay");
                DrawRelative(settings, "overlayFontSize");
                DrawRelative(settings, "smoothingFrames");
            });

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "High grid/sort ms → spatial hash bottleneck. High density/integration + substeps → CFL pressure. " +
                "High gpuMs with low CPU markers → GPU-bound (SSFR/compute). perf.log records spikes when frame ms exceeds threshold.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawSection(string title, System.Action drawFields)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            drawFields();
        }

        private static void DrawRelative(SerializedProperty parent, string name) =>
            EditorGUILayout.PropertyField(parent.FindPropertyRelative(name));
    }
}
