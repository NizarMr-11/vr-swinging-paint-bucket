using System;
using System.Collections.Generic;
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Profiles;
using HarmonicEngineV4.Simulation;
using UnityEngine;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Builds a complete headless V4 pipeline (root + bucket + canvas + spawn zones)
    /// for integration tests. Deterministic: no randomness anywhere in the setup.
    /// </summary>
    public sealed class V4TestRig : IDisposable
    {
        public V4PipelineRoot Root { get; private set; }
        public V4Bucket Bucket { get; private set; }
        public V4Canvas Canvas { get; private set; }
        public readonly List<V4SpawnZone> Zones = new List<V4SpawnZone>();

        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<V4LiquidProfile> _runtimeProfiles = new List<V4LiquidProfile>();

        public sealed class Config
        {
            public float BucketRadius = 0.3f;
            public float BucketHeight = 0.6f;
            public float WallThickness = 0.05f;
            public float TopBandHeight = 0.12f;
            public float RingSpacing = 0.06f;
            public List<V4HoleDef> Holes = new List<V4HoleDef>();
            public float CanvasY = -1f;
            public float CanvasSize = 3f;
            public float GlobalDensity = 120000f;
            public List<(Vector3 localPos, float radius, Color color)> SpawnZones =
                new List<(Vector3, float, Color)> { (new Vector3(0f, 0.25f, 0f), 0.12f, Color.red) };
            public Action<V4LiquidProfile> ConfigureProfile;
        }

        public static V4TestRig Create(Config config = null)
        {
            config = config ?? new Config();
            var rig = new V4TestRig();

            var bucketGo = new GameObject("V4TestBucket");
            rig._objects.Add(bucketGo);
            rig.Bucket = bucketGo.AddComponent<V4Bucket>();
            rig.Bucket.innerRadius = config.BucketRadius;
            rig.Bucket.height = config.BucketHeight;
            rig.Bucket.wallThickness = config.WallThickness;
            rig.Bucket.topBandHeight = config.TopBandHeight;
            rig.Bucket.ringSpacing = config.RingSpacing;
            rig.Bucket.holes = new List<V4HoleDef>(config.Holes);

            var canvasGo = new GameObject("V4TestCanvas");
            rig._objects.Add(canvasGo);
            canvasGo.transform.position = new Vector3(0f, config.CanvasY, 0f);
            rig.Canvas = canvasGo.AddComponent<V4Canvas>();
            rig.Canvas.width = config.CanvasSize;
            rig.Canvas.depth = config.CanvasSize;

            var profile = ScriptableObject.CreateInstance<V4LiquidProfile>();
            profile.profileName = "TestLiquid";
            config.ConfigureProfile?.Invoke(profile);
            rig._runtimeProfiles.Add(profile);

            var rootGo = new GameObject("V4TestPipelineRoot");
            rig._objects.Add(rootGo);
            var root = rootGo.AddComponent<V4PipelineRoot>();
            root.autoRun = false;
            root.bucket = rig.Bucket;
            root.canvas = rig.Canvas;
            root.globalDensity = config.GlobalDensity;
            root.globalProfile = profile;

            foreach ((Vector3 localPos, float radius, Color color) zone in config.SpawnZones)
            {
                var zoneGo = new GameObject("V4TestSpawnZone");
                rig._objects.Add(zoneGo);
                zoneGo.transform.position = bucketGo.transform.TransformPoint(zone.localPos);
                var spawnZone = zoneGo.AddComponent<V4SpawnZone>();
                spawnZone.radius = zone.radius;
                spawnZone.color = zone.color;
                root.spawnZones.Add(spawnZone);
                rig.Zones.Add(spawnZone);
            }

            root.Initialize();
            rig.Root = root;
            return rig;
        }

        public void Step(int frames, float dt = 1f / 60f)
        {
            for (int i = 0; i < frames; i++)
            {
                Root.Step(dt);
            }
        }

        public Vector4[] ReadPositions()
        {
            var data = new Vector4[Root.ActiveParticleCount];
            if (data.Length > 0)
            {
                Root.Soa.ReadBlock0.GetData(data, 0, 0, data.Length);
            }

            return data;
        }

        public Vector4[] ReadVelocities()
        {
            var data = new Vector4[Root.ActiveParticleCount];
            if (data.Length > 0)
            {
                Root.Soa.ReadBlock1.GetData(data, 0, 0, data.Length);
            }

            return data;
        }

        public uint[] ReadFlags()
        {
            var data = new uint[Root.ActiveParticleCount];
            if (data.Length > 0)
            {
                Root.Soa.ReadFlags.GetData(data, 0, 0, data.Length);
            }

            return data;
        }

        public uint[] ReadColors()
        {
            var data = new uint[Root.ActiveParticleCount];
            if (data.Length > 0)
            {
                Root.Soa.ReadColors.GetData(data, 0, 0, data.Length);
            }

            return data;
        }

        public Vector4[] ReadCanvasGrid()
        {
            var data = new Vector4[Root.CanvasGridSize.x * Root.CanvasGridSize.y];
            Root.CanvasGridBuffer.GetData(data);
            return data;
        }

        public void Dispose()
        {
            foreach (GameObject go in _objects)
            {
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            foreach (V4LiquidProfile profile in _runtimeProfiles)
            {
                if (profile != null)
                {
                    UnityEngine.Object.DestroyImmediate(profile);
                }
            }

            _objects.Clear();
            _runtimeProfiles.Clear();
        }
    }
}
