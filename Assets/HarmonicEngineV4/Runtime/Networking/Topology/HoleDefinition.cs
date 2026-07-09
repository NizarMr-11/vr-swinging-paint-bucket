using System;
using Unity.Mathematics;

namespace HarmonicEngineV4.Networking.State
{
    public sealed class HoleDefinition : IEquatable<HoleDefinition>
    {
        public int holeIndex;
        public float3 localCenter;
        public float radius;
        public float3 outwardNormal;
        public float d0;
        public float d1;
        public float d2;
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
                && outwardNormal.Equals(other.outwardNormal)
                && d0.Equals(other.d0)
                && d1.Equals(other.d1)
                && d2.Equals(other.d2)
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
                hash = (hash * 397) ^ outwardNormal.GetHashCode();
                hash = (hash * 397) ^ d0.GetHashCode();
                hash = (hash * 397) ^ d1.GetHashCode();
                hash = (hash * 397) ^ d2.GetHashCode();
                hash = (hash * 397) ^ sdfBias.GetHashCode();
                hash = (hash * 397) ^ isActive.GetHashCode();
                return hash;
            }
        }
    }
}
