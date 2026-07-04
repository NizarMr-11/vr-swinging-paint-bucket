using System.Collections.Generic;
using HarmonicEngineV4.Core;
using HarmonicEngineV4.Logging;
using UnityEditor;
using UnityEngine;

namespace HarmonicEngineV4.Editor
{
    /// <summary>
    /// Inspector for V4PassManifest that surfaces UAV-budget and wiring problems at
    /// data-authoring time (spec section 8.4).
    /// </summary>
    [CustomEditor(typeof(V4PassManifest))]
    public sealed class V4PassManifestEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var manifest = (V4PassManifest)target;
            List<V4ManifestValidation.Issue> issues = V4ManifestValidation.Validate(manifest);

            EditorGUILayout.Space();
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("Manifest is valid. All passes are within the UAV budget.", MessageType.Info);
                return;
            }

            foreach (V4ManifestValidation.Issue issue in issues)
            {
                MessageType type = issue.Severity switch
                {
                    V4LogLevel.Error => MessageType.Error,
                    V4LogLevel.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                EditorGUILayout.HelpBox(issue.ToString(), type);
            }
        }
    }
}
