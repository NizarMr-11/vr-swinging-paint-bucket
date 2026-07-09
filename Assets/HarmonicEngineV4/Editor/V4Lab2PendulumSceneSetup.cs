#if UNITY_EDITOR
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarmonicEngineV4.Editor
{
    public static class V4Lab2PendulumSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/HarmonicEngineLab2.unity";

        [MenuItem("HarmonicEngineV4/Setup Lab2 Pendulum Scene")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject bucket = GameObject.Find("Bucket");
            GameObject pipeline = GameObject.Find("V4 Pipeline");
            if (bucket == null || pipeline == null)
            {
                Debug.LogError("[V4Lab2PendulumSceneSetup] Bucket or V4 Pipeline not found.");
                return;
            }

            EnsureComponent<V4BucketMotionSettings>(bucket).mode = V4BucketMotionMode.Pendulum;
            V4SphericalPendulumController pendulum = EnsureComponent<V4SphericalPendulumController>(bucket);
            Transform pivot = EnsurePivot("Pendulum Pivot", new Vector3(0f, 3f, 0f));
            pendulum.pivotTransform = pivot;
            pendulum.pivotPoint = pivot.position;
            pendulum.adoptManualBucketPoseOnReset = true;
            pendulum.twistAngleDegrees = 0f;
            pendulum.SyncSceneSetupPose(moveBucket: false);
            EnsureComponent<V4TorricelliEjectionSettings>(bucket);
            EnsureComponent<V4FluidMassProbe>(bucket);
            EnsureComponent<V4PendulumRopeVisualizer>(bucket);

            V4Bucket bucketComponent = bucket.GetComponent<V4Bucket>();
            if (bucketComponent.holes.Count > 0)
            {
                V4HoleDef hole = bucketComponent.holes[0];
                hole.radius = 0.03f;
                hole.outwardNormal = Vector3.down;
                bucketComponent.holes[0] = hole;
            }

            V4PipelineRoot root = pipeline.GetComponent<V4PipelineRoot>();
            if (root != null)
            {
                root.carryRate = 25f;
                root.restrictSpawnToBucketCavity = true;
            }

            ReparentSpawnZones(bucket, root);
            pendulum.CaptureRestPoseFromScene();
            pendulum.ResetSimulation();

            V4BucketMotionController keyboard = bucket.GetComponent<V4BucketMotionController>();
            if (keyboard != null)
            {
                keyboard.enabled = false;
                keyboard.inputEnabled = false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[V4Lab2PendulumSceneSetup] Applied pendulum setup to HarmonicEngineLab2.");
        }

        private static void ReparentSpawnZones(GameObject bucket, V4PipelineRoot root)
        {
            if (root == null || root.spawnZones == null)
            {
                return;
            }

            foreach (V4SpawnZone zone in root.spawnZones)
            {
                if (zone == null)
                {
                    continue;
                }

                Transform t = zone.transform;
                Vector3 worldPos = t.position;
                t.SetParent(bucket.transform, worldPositionStays: true);
                t.localPosition = bucket.transform.InverseTransformPoint(worldPos);
            }
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static Transform EnsurePivot(string name, Vector3 worldPosition)
        {
            GameObject existing = GameObject.Find(name);
            if (existing == null)
            {
                existing = new GameObject(name);
            }

            existing.transform.position = worldPosition;
            EnsureComponent<V4PendulumPivot>(existing);
            return existing.transform;
        }
    }
}
#endif
