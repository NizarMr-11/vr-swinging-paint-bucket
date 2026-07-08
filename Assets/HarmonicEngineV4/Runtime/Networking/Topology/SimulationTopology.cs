using System;
using HarmonicEngineV4.Networking.Serialization;

namespace HarmonicEngineV4.Networking.State
{
    public sealed class SimulationTopology : IEquatable<SimulationTopology>
    {
        public int activeParticleCount;
        public int maxParticles;
        public int substeps;
        public float globalDensity;
        [PrimitiveSubType(FieldType.Object)]
        public LiquidProfileTopology[] profiles;

        public bool Equals(SimulationTopology other)
        {
            if (other == null)
            {
                return false;
            }

            if (activeParticleCount != other.activeParticleCount
                || maxParticles != other.maxParticles
                || substeps != other.substeps
                || !globalDensity.Equals(other.globalDensity))
            {
                return false;
            }

            if (profiles == null && other.profiles == null)
            {
                return true;
            }

            if (profiles == null || other.profiles == null || profiles.Length != other.profiles.Length)
            {
                return false;
            }

            for (int i = 0; i < profiles.Length; i++)
            {
                if (!Equals(profiles[i], other.profiles[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as SimulationTopology);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = activeParticleCount;
                hash = (hash * 397) ^ maxParticles;
                hash = (hash * 397) ^ substeps;
                hash = (hash * 397) ^ globalDensity.GetHashCode();
                if (profiles != null)
                {
                    foreach (LiquidProfileTopology profile in profiles)
                    {
                        hash = (hash * 397) ^ (profile?.GetHashCode() ?? 0);
                    }
                }

                return hash;
            }
        }
    }
}
