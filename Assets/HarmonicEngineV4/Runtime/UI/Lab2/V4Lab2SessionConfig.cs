using System;
using HarmonicEngineV4.Logging;
using UnityEngine;

namespace HarmonicEngineV4.UI.Lab2
{
    public enum V4Lab2SwingDirection
    {
        Side = 0,
        Forward = 1
    }

    [Serializable]
    public struct V4Lab2LayerConfig
    {
        public float thickness;
        public Color color;
    }

    [Serializable]
    public struct V4Lab2LiquidTuning
    {
        public float viscosity;
        public float cohesion;
        public float surfaceFriction;
        public float surfaceRestitution;
        public float zone0Strength;
        public float colorDiffusionRate;
        public float settleEpsilon;

        public static V4Lab2LiquidTuning FromProfile(Profiles.V4LiquidProfile profile)
        {
            if (profile == null)
            {
                return default;
            }

            return new V4Lab2LiquidTuning
            {
                viscosity = profile.viscosity,
                cohesion = profile.cohesion,
                surfaceFriction = profile.surfaceFriction,
                surfaceRestitution = profile.surfaceRestitution,
                zone0Strength = profile.ZoneStrength(0),
                colorDiffusionRate = profile.colorDiffusionRate,
                settleEpsilon = profile.settleEpsilon
            };
        }
    }

    /// <summary>Serializable Lab2 session snapshot for the setup wizard.</summary>
    [Serializable]
    public sealed class V4Lab2SessionConfig
    {
        public const int LayerCount = 3;
        public const float MaxLayerThickness = 0.1f;

        [Header("Pendulum")]
        public float pivotHeightY = 3f;
        public float ropeLength = 2.6f;
        public float swingSpeed;
        public V4Lab2SwingDirection swingDirection = V4Lab2SwingDirection.Side;
        [Range(0f, 2f)] public float dampingCoefficient = 0.05f;
        public float twistAngleDegrees;
        [Range(0f, 1f)] public float sloshFeedbackScale = 1f;

        [Header("Paint layers")]
        public V4Lab2LayerConfig[] layers = new V4Lab2LayerConfig[LayerCount];

        [Header("Holes")]
        public float hole0Radius = 0.05f;
        public float hole1Radius = 0.03f;
        public Vector2 hole0LocalXZ = new Vector2(0.12f, 0f);
        public Vector2 hole1LocalXZ = new Vector2(-0.19f, 0f);
        public float torricelliHeadHeight = 0.4f;

        [Header("Simulation")]
        public int liquidProfilePreset;
        public V4Lab2LiquidTuning liquidTuning;
        [Min(1000f)] public float globalDensity = 200000f;
        [Min(0f)] public float carryRate = 25f;
        [Range(1, 4)] public int pbfIterations = 2;
        [Range(0f, 1f)] public float boundaryGhostWeight;
        public float gravityY = -9.81f;

        [Header("Logging")]
        public bool loggingEnabled = true;
        [Tooltip("Empty uses the project Logs/Engine2 folder.")]
        public string logBaseDirectory = string.Empty;
        public V4ChannelLogSettings channelLogSettings = new V4ChannelLogSettings();

        public V4Lab2SessionConfig()
        {
            ResetLayersToDefaults(1f);
        }

        public void ResetLayersToDefaults(float bucketHeight)
        {
            float perLayer = Mathf.Min(MaxLayerThickness, bucketHeight / LayerCount);
            layers = new V4Lab2LayerConfig[LayerCount];
            layers[0] = new V4Lab2LayerConfig { thickness = perLayer, color = Color.black };
            layers[1] = new V4Lab2LayerConfig { thickness = perLayer, color = Color.white };
            layers[2] = new V4Lab2LayerConfig { thickness = perLayer, color = Color.yellow };
        }
    }
}
