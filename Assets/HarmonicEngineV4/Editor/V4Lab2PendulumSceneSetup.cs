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

            EnsureComponent<V4GpuBucketDriver>(bucket);
            EnsureComponent<V4BucketMotionSettings>(bucket).mode = V4BucketMotionMode.Pendulum;
            V4BucketMotionSettings motionSettings = bucket.GetComponent<V4BucketMotionSettings>();
            motionSettings.useGpuPendulum = true;
            V4SphericalPendulumController pendulum = EnsureComponent<V4SphericalPendulumController>(bucket);
            Transform pivot = EnsurePivot("Pendulum Pivot", new Vector3(0f, 3f, 0f));
            pendulum.pivotTransform = pivot;
            pendulum.pivotPoint = pivot.position;
            pendulum.adoptManualBucketPoseOnReset = true;
            pendulum.twistAngleDegrees = 0f;
            pendulum.initialTangentialVelocity = Vector3.zero;
            pendulum.PreparePlayInitPose();
            EnsureComponent<V4TorricelliEjectionSettings>(bucket);
            EnsureComponent<V4FluidMassProbe>(bucket);
            EnsureComponent<V4PendulumRopeVisualizer>(bucket);
            V4BucketHangEar hangEar = EnsureComponent<V4BucketHangEar>(bucket);
            hangEar.ropeAttachLift = 0.10f;

            V4Bucket bucketComponent = bucket.GetComponent<V4Bucket>();
            SetupThreeColorLayers(bucketComponent);
            if (bucketComponent.holes.Count > 0)
            {
                V4HoleDef hole = bucketComponent.holes[0];
                hole.radius = 0.05f;
                hole.outwardNormal = Vector3.down;
                bucketComponent.holes[0] = hole;
            }

            for (int i = 1; i < bucketComponent.holes.Count; i++)
            {
                V4HoleDef hole = bucketComponent.holes[i];
                if (hole.radius <= 1e-4f)
                {
                    hole.radius = 0.03f;
                    hole.outwardNormal = Vector3.down;
                    bucketComponent.holes[i] = hole;
                }
            }

            V4PipelineRoot root = pipeline.GetComponent<V4PipelineRoot>();
            if (root != null)
            {
                root.carryRate = 25f;
                root.restrictSpawnToBucketCavity = true;
            }

            RemoveSphereSpawnZones(bucket, root);
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

        private static void SetupThreeColorLayers(V4Bucket bucket)
        {
            float slab = bucket.height / 3f;
            bucket.heightLayers.Clear();
            bucket.heightLayers.Add(new V4LayerDef { thickness = slab, color = Color.black });
            bucket.heightLayers.Add(new V4LayerDef { thickness = slab, color = Color.white });
            bucket.heightLayers.Add(new V4LayerDef { thickness = slab, color = Color.yellow });
            bucket.topBandHeight = slab;
        }

        private static void RemoveSphereSpawnZones(GameObject bucket, V4PipelineRoot root)
        {
            foreach (V4SpawnZone zone in bucket.GetComponentsInChildren<V4SpawnZone>(true))
            {
                Object.DestroyImmediate(zone.gameObject);
            }

            if (root != null && root.spawnZones != null)
            {
                root.spawnZones.Clear();
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
