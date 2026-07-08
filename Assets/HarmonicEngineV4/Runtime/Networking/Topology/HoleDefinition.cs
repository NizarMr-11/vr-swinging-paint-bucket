using System;
using Unity.Mathematics;

namespace HarmonicEngineV4.Networking.State
{
    public sealed class HoleDefinition : IEquatable<HoleDefinition>
    {
        public int holeIndex;
        public float3 localCenter;
        public float radius;
        public float sdfBias;
        public bool isActive;

        public bool Equals(HoleDefinition other)
        {
            if (other == null)
            {
                return false;
            }

            return holeIndex == other.holeIndex
                && localCenter.Equals(other.localCenter)
                && radius.Equals(other.radius)
                && sdfBias.Equals(other.sdfBias)
                && isActive == other.isActive;
        }

        public override bool Equals(object obj) => Equals(obj as HoleDefinition);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = holeIndex;
                hash = (hash * 397) ^ localCenter.GetHashCode();
                hash = (hash * 397) ^ radius.GetHashCode();
                hash = (hash * 397) ^ sdfBias.GetHashCode();
                hash = (hash * 397) ^ isActive.GetHashCode();
                return hash;
            }
        }
    }
}
