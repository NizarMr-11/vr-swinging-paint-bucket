using System;

namespace HarmonicEngineV4.Networking.State
{
    public sealed class LiquidProfileTopology : IEquatable<LiquidProfileTopology>
    {
        public int profileIndex;
        public float restDensity;
        public float particleRadius;
        public float smoothingRadius;
        public float viscosity;
        public float surfaceTension;
        public float cohesion;
        public uint packedTint;

        public bool Equals(LiquidProfileTopology other)
        {
            if (other == null)
            {
                return false;
            }

            return profileIndex == other.profileIndex
                && restDensity.Equals(other.restDensity)
                && particleRadius.Equals(other.particleRadius)
                && smoothingRadius.Equals(other.smoothingRadius)
                && viscosity.Equals(other.viscosity)
                && surfaceTension.Equals(other.surfaceTension)
                && cohesion.Equals(other.cohesion)
                && packedTint == other.packedTint;
        }

        public override bool Equals(object obj) => Equals(obj as LiquidProfileTopology);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = profileIndex;
                hash = (hash * 397) ^ restDensity.GetHashCode();
                hash = (hash * 397) ^ particleRadius.GetHashCode();
                hash = (hash * 397) ^ smoothingRadius.GetHashCode();
                hash = (hash * 397) ^ viscosity.GetHashCode();
                hash = (hash * 397) ^ surfaceTension.GetHashCode();
                hash = (hash * 397) ^ cohesion.GetHashCode();
                hash = (hash * 397) ^ (int)packedTint;
                return hash;
            }
        }
    }
}
