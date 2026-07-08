using System;
using Unity.Mathematics;

namespace HarmonicEngineV4.Networking.State
{
    public sealed class WorldTopology : IEquatable<WorldTopology>
    {
        public float3 gravity;
        public float fixedDeltaTime;
        public int randomSeed;

        public bool Equals(WorldTopology other)
        {
            if (other == null)
            {
                return false;
            }

            return gravity.Equals(other.gravity)
                && fixedDeltaTime.Equals(other.fixedDeltaTime)
                && randomSeed == other.randomSeed;
        }

        public override bool Equals(object obj) => Equals(obj as WorldTopology);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = gravity.GetHashCode();
                hash = (hash * 397) ^ fixedDeltaTime.GetHashCode();
                hash = (hash * 397) ^ randomSeed;
                return hash;
            }
        }
    }
}
