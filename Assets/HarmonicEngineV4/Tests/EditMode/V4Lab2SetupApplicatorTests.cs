using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Profiles;
using HarmonicEngineV4.Simulation;
using HarmonicEngineV4.UI.Lab2;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4Lab2SetupApplicatorTests
    {
        private GameObject _bucketGo;
        private GameObject _pipelineGo;
        private GameObject _pivotGo;
        private V4Bucket _bucket;
        private V4PipelineRoot _pipeline;
        private V4SphericalPendulumController _pendulum;
        private V4LiquidProfile _lab2Profile;
        private V4LiquidProfile _defaultProfile;

        [SetUp]
        public void SetUp()
        {
            _bucketGo = new GameObject("Bucket");
            _bucket = _bucketGo.AddComponent<V4Bucket>();
            _bucket.height = 0.9f;
            _bucket.innerRadius = 0.5f;
            _bucket.holes.Add(new V4HoleDef
            {
                localPosition = new Vector3(0.1f, 0f, 0f),
                radius = 0.05f,
                outwardNormal = Vector3.down
            });
            _bucket.holes.Add(new V4HoleDef
            {
                localPosition = new Vector3(-0.1f, 0f, 0f),
                radius = 0.03f,
                outwardNormal = Vector3.down
            });

            _pendulum = _bucketGo.AddComponent<V4SphericalPendulumController>();
            _pendulum.ropeLength = 2.6f;
            _bucketGo.AddComponent<V4TorricelliEjectionSettings>();

            _pivotGo = new GameObject("Pendulum Pivot");
            _pivotGo.transform.position = new Vector3(0f, 3f, 0f);
            _pendulum.pivotTransform = _pivotGo.transform;

            _pipelineGo = new GameObject("Pipeline");
            _pipeline = _pipelineGo.AddComponent<V4PipelineRoot>();
            _pipeline.bucket = _bucket;
            _pipeline.canvas = new GameObject("Canvas").AddComponent<V4Canvas>();

            _lab2Profile = ScriptableObject.CreateInstance<V4LiquidProfile>();
            _lab2Profile.profileName = "Lab2 Paint";
            _defaultProfile = ScriptableObject.CreateInstance<V4LiquidProfile>();
            _defaultProfile.profileName = "Default";
            _pipeline.globalProfile = _lab2Profile;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_pipeline.canvas.gameObject);
            Object.DestroyImmediate(_pipelineGo);
            Object.DestroyImmediate(_bucketGo);
            Object.DestroyImmediate(_pivotGo);
            Object.DestroyImmediate(_lab2Profile);
            Object.DestroyImmediate(_defaultProfile);
        }

        [Test]
        public void ApplyFull_SetsRopeLengthCarryRateAndLiquidProfile()
        {
            var config = new V4Lab2SessionConfig();
            config.ropeLength = 2.1f;
            config.carryRate = 18f;
            config.liquidProfilePreset = 0;
            config.liquidTuning = new V4Lab2LiquidTuning
            {
                viscosity = 0.4f,
                cohesion = 0.7f,
                surfaceFriction = 0.3f,
                surfaceRestitution = 0.04f,
                zone0Strength = 2.8f,
                colorDiffusionRate = 0.1f,
                settleEpsilon = 0.03f
            };
            config.ResetLayersToDefaults(_bucket.height);

            var refs = new V4Lab2SetupApplicator.SceneRefs
            {
                pipeline = _pipeline,
                bucket = _bucket,
                pendulum = _pendulum,
                pivotTransform = _pivotGo.transform,
                torricelli = _bucketGo.GetComponent<V4TorricelliEjectionSettings>(),
                lab2LiquidProfile = _lab2Profile,
                defaultLiquidProfile = _defaultProfile
            };

            Assert.IsNull(V4Lab2SetupApplicator.ValidateConfig(config, refs));
            V4Lab2SetupApplicator.ApplyFull(config, refs, reinitializePipeline: false);

            Assert.AreEqual(2.1f, _pendulum.ropeLength, 1e-4f);
            Assert.AreEqual(18f, _pipeline.carryRate, 1e-4f);
            Assert.AreSame(_lab2Profile, _pipeline.globalProfile);
            Assert.AreEqual(0.4f, _lab2Profile.viscosity, 1e-4f);
            Assert.AreEqual(3, _bucket.heightLayers.Count);
            Assert.AreEqual(2.8f, _lab2Profile.ZoneStrength(0), 1e-4f);
        }

        [Test]
        public void ValidateConfig_RejectsLayerSumAboveBucketHeight()
        {
            var config = new V4Lab2SessionConfig();
            config.ResetLayersToDefaults(_bucket.height);
            config.layers[0].thickness = _bucket.height;

            var refs = new V4Lab2SetupApplicator.SceneRefs { bucket = _bucket };
            string error = V4Lab2SetupApplicator.ValidateConfig(config, refs);

            Assert.IsNotNull(error);
            StringAssert.Contains("exceeds bucket height", error);
        }
    }
}
