using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarmonicEngineV4.Core
{
    public enum V4DeviceTier
    {
        /// <summary>Constrained hardware: more passes, fewer simultaneous UAVs per kernel.</summary>
        Split,
        /// <summary>Capable hardware: merged kernels, fewer dispatches.</summary>
        Combined
    }

    [Serializable]
    public sealed class V4PassDef
    {
        public string passId;
        public ComputeShader shader;
        public string kernelName;

        [Tooltip("Buffers bound as read-write (RWStructuredBuffer). These consume UAV slots.")]
        public string[] readWriteBuffers = Array.Empty<string>();

        [Tooltip("Buffers bound read-only (StructuredBuffer). SRVs, do not consume UAV slots.")]
        public string[] readOnlyBuffers = Array.Empty<string>();

        public IEnumerable<string> AllBuffers()
        {
            foreach (string name in readWriteBuffers)
            {
                yield return name;
            }

            foreach (string name in readOnlyBuffers)
            {
                yield return name;
            }
        }
    }

    /// <summary>
    /// Data-driven pass list (spec section 8.2). Buffer-to-pass assignment is authored
    /// data, selectable per device tier, so it can be tuned without touching kernel code.
    /// </summary>
    [CreateAssetMenu(menuName = "HarmonicEngineV4/Pass Manifest", fileName = "V4PassManifest")]
    public sealed class V4PassManifest : ScriptableObject
    {
        public string manifestName = "default";
        public V4DeviceTier tier = V4DeviceTier.Split;

        [Tooltip("Maximum simultaneous UAVs one kernel may bind on the target device (D3D11 cap = 8).")]
        public int uavBudget = 8;

        public List<V4PassDef> passes = new List<V4PassDef>();

        public V4PassDef FindPass(string passId)
        {
            for (int i = 0; i < passes.Count; i++)
            {
                if (passes[i].passId == passId)
                {
                    return passes[i];
                }
            }

            return null;
        }
    }
}
