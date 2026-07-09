using System.Collections.Generic;
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Profiles;
using HarmonicEngineV4.Rendering;
using HarmonicEngineV4.Simulation;
using UnityEngine;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Applies <see cref="V4Lab2SessionConfig"/> to scene components.</summary>
    public sealed class V4Lab2SetupApplicator
    {
        public sealed class SceneRefs
        {
            public V4PipelineRoot pipeline;
            public V4Bucket bucket;
            public V4SphericalPendulumController pendulum;
            public Transform pivotTransform;
            public V4TorricelliEjectionSettings torricelli;
            public V4LiquidProfile lab2LiquidProfile;
            public V4LiquidProfile defaultLiquidProfile;
        }

        public static string ValidateConfig(V4Lab2SessionConfig config, SceneRefs refs)
        {
            if (config == null || refs?.bucket == null)
            {
                return "Missing bucket reference.";
            }

            float layerSum = 0f;
            for (int i = 0; i < V4Lab2SessionConfig.LayerCount; i++)
            {
                layerSum += Mathf.Max(config.layers[i].thickness, 0.001f);
            }

            if (layerSum > refs.bucket.height + 1e-4f)
            {
                return $"Layer thickness sum ({layerSum:F3}m) exceeds bucket height ({refs.bucket.height:F3}m).";
            }

            if (config.ropeLength < 0.1f)
            {
                return "Rope length must be at least 0.1m.";
            }

            if (config.globalDensity < 1000f)
            {
                return "Particle density must be at least 1000.";
            }

            var tempBucket = refs.bucket;
            ApplyLayers(config, tempBucket);
            V4BucketBake.Result bake = tempBucket.BakeBucket();
            if (!bake.SpacingOk)
            {
                return bake.Errors.Count > 0 ? bake.Errors[0] : "Bucket bake validation failed.";
            }

            return null;
        }

        public static void ApplyFull(V4Lab2SessionConfig config, SceneRefs refs, bool reinitializePipeline)
        {
            if (config == null || refs == null)
            {
                return;
            }

            ApplyPendulum(config, refs);
            ApplyLayers(config, refs.bucket);
            ApplyHoles(config, refs.bucket);
            ApplyTorricelli(config, refs.torricelli);
            ApplyPipeline(config, refs);
            ApplyLiquidProfile(config, refs);

            if (refs.pendulum != null)
            {
                refs.pendulum.enableKeyboardReset = false;
                refs.pendulum.PreparePlayInitPose();
                refs.pendulum.ResetSimulation();
            }

            if (refs.pipeline != null && reinitializePipeline)
            {
                refs.pipeline.Reinitialize();
            }
        }

        public static void CaptureFromScene(SceneRefs refs, V4Lab2SessionConfig config)
        {
            if (refs == null || config == null)
            {
                return;
            }

            if (refs.pivotTransform != null)
            {
                config.pivotHeightY = refs.pivotTransform.position.y;
            }
            else if (refs.pendulum != null)
            {
                config.pivotHeightY = refs.pendulum.PivotPoint.y;
            }

            if (refs.pendulum != null)
            {
                config.ropeLength = refs.pendulum.ropeLength;
                config.dampingCoefficient = refs.pendulum.dampingCoefficient;
                config.twistAngleDegrees = refs.pendulum.twistAngleDegrees;
                config.sloshFeedbackScale = refs.pendulum.sloshFeedbackScale;
                config.swingSpeed = refs.pendulum.initialTangentialVelocity.magnitude;
                config.swingDirection = Mathf.Abs(refs.pendulum.initialTangentialVelocity.z) >
                                        Mathf.Abs(refs.pendulum.initialTangentialVelocity.x)
                    ? V4Lab2SwingDirection.Forward
                    : V4Lab2SwingDirection.Side;
            }

            if (refs.bucket != null)
            {
                if (refs.bucket.heightLayers != null && refs.bucket.heightLayers.Count >= V4Lab2SessionConfig.LayerCount)
                {
                    config.layers = new V4Lab2LayerConfig[V4Lab2SessionConfig.LayerCount];
                    for (int i = 0; i < V4Lab2SessionConfig.LayerCount; i++)
                    {
                        V4LayerDef layer = refs.bucket.heightLayers[i];
                        config.layers[i] = new V4Lab2LayerConfig
                        {
                            thickness = layer.thickness,
                            color = layer.color
                        };
                    }
                }
                else
                {
                    config.ResetLayersToDefaults(refs.bucket.height);
                }

                if (refs.bucket.holes.Count > 0)
                {
                    config.hole0Radius = refs.bucket.holes[0].radius;
                }

                if (refs.bucket.holes.Count > 1)
                {
                    config.hole1Radius = refs.bucket.holes[1].radius;
                }
            }

            if (refs.torricelli != null)
            {
                config.torricelliHeadHeight = refs.torricelli.headHeight;
            }

            if (refs.pipeline != null)
            {
                config.globalDensity = refs.pipeline.globalDensity;
                config.carryRate = refs.pipeline.carryRate;
                config.pbfIterations = refs.pipeline.pbfIterations;
                config.boundaryGhostWeight = refs.pipeline.boundaryGhostWeight;
                config.gravityY = refs.pipeline.gravity.y;

                V4LiquidProfile active = refs.pipeline.globalProfile;
                if (active == refs.lab2LiquidProfile)
                {
                    config.liquidProfilePreset = 0;
                }
                else if (active == refs.defaultLiquidProfile)
                {
                    config.liquidProfilePreset = 1;
                }

                config.liquidTuning = V4Lab2LiquidTuning.FromProfile(active);
            }
        }

        private static void ApplyPendulum(V4Lab2SessionConfig config, SceneRefs refs)
        {
            if (refs.pivotTransform != null)
            {
                Vector3 pivotPos = refs.pivotTransform.position;
                pivotPos.y = config.pivotHeightY;
                refs.pivotTransform.position = pivotPos;
            }

            if (refs.pendulum == null)
            {
                return;
            }

            refs.pendulum.pivotPoint = refs.pivotTransform != null
                ? refs.pivotTransform.position
                : new Vector3(0f, config.pivotHeightY, 0f);
            refs.pendulum.ropeLength = config.ropeLength;
            refs.pendulum.dampingCoefficient = config.dampingCoefficient;
            refs.pendulum.twistAngleDegrees = config.twistAngleDegrees;
            refs.pendulum.sloshFeedbackScale = config.sloshFeedbackScale;
            refs.pendulum.initialTangentialVelocity = BuildSwingVelocity(config);

            if (refs.pendulum.adoptManualBucketPoseOnReset && refs.bucket != null)
            {
                Vector3 pivot = refs.pendulum.GetPivotWorld();
                Vector3 delta = refs.bucket.transform.position - pivot;
                if (delta.sqrMagnitude > 1e-8f)
                {
                    Vector3 dir = delta.normalized;
                    refs.bucket.transform.position = pivot + dir * config.ropeLength;
                    refs.pendulum.CaptureRestPoseFromScene();
                }
            }
        }

        private static Vector3 BuildSwingVelocity(V4Lab2SessionConfig config)
        {
            Vector3 dir = config.swingDirection == V4Lab2SwingDirection.Forward
                ? Vector3.forward
                : Vector3.right;
            return dir * Mathf.Max(0f, config.swingSpeed);
        }

        private static void ApplyLayers(V4Lab2SessionConfig config, V4Bucket bucket)
        {
            if (bucket == null)
            {
                return;
            }

            bucket.heightLayers.Clear();
            for (int i = 0; i < V4Lab2SessionConfig.LayerCount; i++)
            {
                V4Lab2LayerConfig layer = config.layers[i];
                bucket.heightLayers.Add(new V4LayerDef
                {
                    thickness = Mathf.Max(layer.thickness, 0.001f),
                    color = layer.color
                });
            }

            if (config.layers.Length > 0)
            {
                bucket.topBandHeight = config.layers[config.layers.Length - 1].thickness;
            }
        }

        private static void ApplyHoles(V4Lab2SessionConfig config, V4Bucket bucket)
        {
            if (bucket == null || bucket.holes.Count == 0)
            {
                return;
            }

            V4HoleDef hole0 = bucket.holes[0];
            hole0.radius = Mathf.Max(0f, config.hole0Radius);
            hole0.outwardNormal = Vector3.down;
            bucket.holes[0] = hole0;

            if (bucket.holes.Count > 1)
            {
                V4HoleDef hole1 = bucket.holes[1];
                hole1.radius = Mathf.Max(0f, config.hole1Radius);
                hole1.outwardNormal = Vector3.down;
                bucket.holes[1] = hole1;
            }
        }

        private static void ApplyTorricelli(V4Lab2SessionConfig config, V4TorricelliEjectionSettings torricelli)
        {
            if (torricelli == null)
            {
                return;
            }

            torricelli.headHeight = Mathf.Max(0f, config.torricelliHeadHeight);
        }

        private static void ApplyPipeline(V4Lab2SessionConfig config, SceneRefs refs)
        {
            if (refs.pipeline == null)
            {
                return;
            }

            refs.pipeline.globalDensity = config.globalDensity;
            refs.pipeline.carryRate = config.carryRate;
            refs.pipeline.pbfIterations = config.pbfIterations;
            refs.pipeline.boundaryGhostWeight = config.boundaryGhostWeight;
            refs.pipeline.gravity = new Vector3(0f, config.gravityY, 0f);

            if (refs.pendulum != null)
            {
                refs.pendulum.gravity = Mathf.Abs(config.gravityY);
            }
        }

        private static void ApplyLiquidProfile(V4Lab2SessionConfig config, SceneRefs refs)
        {
            if (refs.pipeline == null)
            {
                return;
            }

            V4LiquidProfile profile = config.liquidProfilePreset == 0
                ? refs.lab2LiquidProfile
                : refs.defaultLiquidProfile;

            if (profile == null)
            {
                profile = refs.lab2LiquidProfile != null
                    ? refs.lab2LiquidProfile
                    : refs.defaultLiquidProfile;
            }

            if (profile == null)
            {
                return;
            }

            profile.viscosity = config.liquidTuning.viscosity;
            profile.cohesion = config.liquidTuning.cohesion;
            profile.surfaceFriction = config.liquidTuning.surfaceFriction;
            profile.surfaceRestitution = config.liquidTuning.surfaceRestitution;
            profile.colorDiffusionRate = config.liquidTuning.colorDiffusionRate;
            profile.settleEpsilon = config.liquidTuning.settleEpsilon;
            SetZone0Strength(profile, config.liquidTuning.zone0Strength);
            refs.pipeline.globalProfile = profile;
        }

        private static void SetZone0Strength(V4LiquidProfile profile, float strength)
        {
            if (profile?.zoneStrengthByLevel == null)
            {
                return;
            }

            AnimationCurve curve = profile.zoneStrengthByLevel;
            var keys = new List<Keyframe>(curve.keys);
            bool found = false;
            for (int i = 0; i < keys.Count; i++)
            {
                if (Mathf.Approximately(keys[i].time, 0f))
                {
                    Keyframe key = keys[i];
                    key.value = strength;
                    keys[i] = key;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                keys.Insert(0, new Keyframe(0f, strength));
            }

            profile.zoneStrengthByLevel = new AnimationCurve(keys.ToArray());
        }
    }
}
