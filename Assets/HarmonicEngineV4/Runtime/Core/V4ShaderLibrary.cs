using System;
using UnityEngine;

namespace HarmonicEngineV4.Core
{
    /// <summary>Loads V4 compute shaders from Resources so runtime code and tests need no inspector wiring.</summary>
    public static class V4ShaderLibrary
    {
        public const string RadixSort = "HarmonicEngineV4/V4RadixSort";
        public const string SpatialHash = "HarmonicEngineV4/V4SpatialHash";
        public const string Classification = "HarmonicEngineV4/V4Classification";
        public const string ExternalForces = "HarmonicEngineV4/V4ExternalForces";
        public const string PbfSolver = "HarmonicEngineV4/V4PbfSolver";
        public const string CanvasSplat = "HarmonicEngineV4/V4CanvasSplat";

        public static ComputeShader Load(string resourcePath)
        {
            var shader = Resources.Load<ComputeShader>(resourcePath);
            if (shader == null)
            {
                throw new InvalidOperationException($"Compute shader not found at Resources/{resourcePath}");
            }

            return shader;
        }
    }
}
