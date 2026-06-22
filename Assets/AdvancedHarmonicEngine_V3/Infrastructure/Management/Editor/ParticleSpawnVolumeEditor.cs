using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Infrastructure.Management;
using UnityEditor;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management.Editor
{
    [CustomEditor(typeof(ParticleSpawnVolume))]
    public class ParticleSpawnVolumeEditor : UnityEditor.Editor
    {
        private SerializedProperty _pipeline;
        private SerializedProperty _shapeType;
        private SerializedProperty _shapeSource;
        private SerializedProperty _meshFilter;
        private SerializedProperty _boxSize;
        private SerializedProperty _sphereRadius;
        private SerializedProperty _capsuleRadius;
        private SerializedProperty _capsuleHeight;
        private SerializedProperty _cylinderRadius;
        private SerializedProperty _cylinderHeight;
        private SerializedProperty _particleCount;
        private SerializedProperty _restDensity;
        private SerializedProperty _initialVelocity;
        private SerializedProperty _spawnPriority;
        private SerializedProperty _spawnColor;
        private SerializedProperty _seed;
        private SerializedProperty _meshMaxAttemptsPerPoint;
        private SerializedProperty _emitOnStart;
        private SerializedProperty _clearBeforeEmit;
        private SerializedProperty _activateSimulationOnEmit;
        private SerializedProperty _drawGizmo;
        private SerializedProperty _drawGizmoWhenNotSelected;

        private void OnEnable()
        {
            _pipeline = serializedObject.FindProperty("pipeline");
            _shapeType = serializedObject.FindProperty("shapeType");
            _shapeSource = serializedObject.FindProperty("shapeSource");
            _meshFilter = serializedObject.FindProperty("meshFilter");
            _boxSize = serializedObject.FindProperty("boxSize");
            _sphereRadius = serializedObject.FindProperty("sphereRadius");
            _capsuleRadius = serializedObject.FindProperty("capsuleRadius");
            _capsuleHeight = serializedObject.FindProperty("capsuleHeight");
            _cylinderRadius = serializedObject.FindProperty("cylinderRadius");
            _cylinderHeight = serializedObject.FindProperty("cylinderHeight");
            _particleCount = serializedObject.FindProperty("particleCount");
            _restDensity = serializedObject.FindProperty("restDensity");
            _initialVelocity = serializedObject.FindProperty("initialVelocity");
            _spawnPriority = serializedObject.FindProperty("spawnPriority");
            _spawnColor = serializedObject.FindProperty("spawnColor");
            _seed = serializedObject.FindProperty("seed");
            _meshMaxAttemptsPerPoint = serializedObject.FindProperty("meshMaxAttemptsPerPoint");
            _emitOnStart = serializedObject.FindProperty("emitOnStart");
            _clearBeforeEmit = serializedObject.FindProperty("clearBeforeEmit");
            _activateSimulationOnEmit = serializedObject.FindProperty("activateSimulationOnEmit");
            _drawGizmo = serializedObject.FindProperty("drawGizmo");
            _drawGizmoWhenNotSelected = serializedObject.FindProperty("drawGizmoWhenNotSelected");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_pipeline);
            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(_shapeType);
            EditorGUILayout.PropertyField(_shapeSource);
            EditorGUILayout.PropertyField(_meshFilter);

            var shape = (ShapeVolumeType)_shapeType.enumValueIndex;
            EditorGUILayout.Space(4f);
            switch (shape)
            {
                case ShapeVolumeType.Box:
                    EditorGUILayout.PropertyField(_boxSize);
                    break;
                case ShapeVolumeType.Sphere:
                    EditorGUILayout.PropertyField(_sphereRadius);
                    break;
                case ShapeVolumeType.Capsule:
                    EditorGUILayout.PropertyField(_capsuleRadius);
                    EditorGUILayout.PropertyField(_capsuleHeight);
                    break;
                case ShapeVolumeType.Cylinder:
                    EditorGUILayout.PropertyField(_cylinderRadius);
                    EditorGUILayout.PropertyField(_cylinderHeight);
                    break;
                case ShapeVolumeType.Mesh:
                    break;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(_particleCount);
            EditorGUILayout.PropertyField(_restDensity);
            EditorGUILayout.PropertyField(_initialVelocity);
            EditorGUILayout.PropertyField(
                _spawnPriority,
                new GUIContent(
                    "Spawn Priority",
                    "Higher priority spawns first and receives a larger share when capacity is limited."));
            EditorGUILayout.PropertyField(_spawnColor);
            EditorGUILayout.PropertyField(_seed);
            if (shape == ShapeVolumeType.Mesh)
            {
                EditorGUILayout.PropertyField(_meshMaxAttemptsPerPoint);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(_emitOnStart);
            EditorGUILayout.PropertyField(_clearBeforeEmit);
            EditorGUILayout.PropertyField(_activateSimulationOnEmit);
            EditorGUILayout.PropertyField(_drawGizmo);
            EditorGUILayout.PropertyField(_drawGizmoWhenNotSelected);

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            var volume = (ParticleSpawnVolume)target;
            if (!volume.enabled)
            {
                return;
            }

            serializedObject.Update();
            Transform src = volume.transform;
            if (_shapeSource.objectReferenceValue is Transform shapeTransform)
            {
                src = shapeTransform;
            }

            Handles.color = _spawnColor.colorValue;
            var shape = (ShapeVolumeType)_shapeType.enumValueIndex;

            switch (shape)
            {
                case ShapeVolumeType.Box:
                    EditorGUI.BeginChangeCheck();
                    Vector3 box = _boxSize.vector3Value;
                    Vector3 newBox = Handles.ScaleHandle(
                        box, src.position, src.rotation, HandleUtility.GetHandleSize(src.position));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _boxSize.vector3Value = Vector3.Max(newBox, Vector3.one * 0.01f);
                    }

                    break;
                case ShapeVolumeType.Sphere:
                    EditorGUI.BeginChangeCheck();
                    float radius = _sphereRadius.floatValue * GetMaxScale(src);
                    float newRadius = Handles.RadiusHandle(Quaternion.identity, src.position, radius);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _sphereRadius.floatValue = Mathf.Max(newRadius / GetMaxScale(src), 0.01f);
                    }

                    break;
                case ShapeVolumeType.Cylinder:
                    DrawCylinderHandles(src);
                    break;
                case ShapeVolumeType.Capsule:
                    DrawCapsuleHandles(src);
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCylinderHandles(Transform src)
        {
            EditorGUI.BeginChangeCheck();
            float radialScale = Mathf.Max(Mathf.Abs(src.lossyScale.x), Mathf.Abs(src.lossyScale.z));
            float heightScale = Mathf.Abs(src.lossyScale.y);
            float radius = _cylinderRadius.floatValue * radialScale;
            float height = _cylinderHeight.floatValue * heightScale;

            float newRadius = Handles.RadiusHandle(src.rotation, src.position, radius);
            Vector3 top = src.position + src.up * (height * 0.5f);
            Vector3 bottom = src.position - src.up * (height * 0.5f);
            float handleSize = HandleUtility.GetHandleSize(src.position) * 0.08f;
            Vector3 newTop = Handles.FreeMoveHandle(top, handleSize, Vector3.zero, Handles.DotHandleCap);
            Vector3 newBottom = Handles.FreeMoveHandle(bottom, handleSize, Vector3.zero, Handles.DotHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                _cylinderRadius.floatValue = Mathf.Max(newRadius / radialScale, 0.01f);
                float newHeight = Vector3.Distance(newTop, newBottom);
                _cylinderHeight.floatValue = Mathf.Max(newHeight / heightScale, 0.01f);
            }
        }

        private void DrawCapsuleHandles(Transform src)
        {
            EditorGUI.BeginChangeCheck();
            float radialScale = Mathf.Max(Mathf.Abs(src.lossyScale.x), Mathf.Abs(src.lossyScale.z));
            float heightScale = Mathf.Abs(src.lossyScale.y);
            float radius = _capsuleRadius.floatValue * radialScale;
            float newRadius = Handles.RadiusHandle(src.rotation, src.position, radius);
            float height = _capsuleHeight.floatValue * heightScale;
            Vector3 top = src.position + src.up * (height * 0.5f - radius);
            Vector3 bottom = src.position - src.up * (height * 0.5f - radius);
            float handleSize = HandleUtility.GetHandleSize(src.position) * 0.08f;
            Vector3 newTop = Handles.FreeMoveHandle(top, handleSize, Vector3.zero, Handles.DotHandleCap);
            Vector3 newBottom = Handles.FreeMoveHandle(bottom, handleSize, Vector3.zero, Handles.DotHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                _capsuleRadius.floatValue = Mathf.Max(newRadius / radialScale, 0.01f);
                float newHeight = Vector3.Distance(newTop, newBottom) + (2f * newRadius / radialScale);
                _capsuleHeight.floatValue = Mathf.Max(newHeight / heightScale, 0.01f);
            }
        }

        private static float GetMaxScale(Transform src) =>
            Mathf.Max(src.lossyScale.x, Mathf.Max(src.lossyScale.y, src.lossyScale.z));
    }
}
