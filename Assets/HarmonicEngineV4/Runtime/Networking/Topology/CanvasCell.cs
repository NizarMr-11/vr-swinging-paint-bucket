using System;
using Unity.Mathematics;

namespace HarmonicEngineV4.Networking.State
{
    public sealed class CanvasCell : IEquatable<CanvasCell>
    {
        public int cellIndex;
        public float3 worldPosition;
        public float splatRadius;
        public uint packedColor;
        public float opacity;

        public bool Equals(CanvasCell other)
        {
            if (other == null)
            {
                return false;
            }

            return cellIndex == other.cellIndex
                && worldPosition.Equals(other.worldPosition)
                && splatRadius.Equals(other.splatRadius)
                && packedColor == other.packedColor
                && opacity.Equals(other.opacity);
        }

        public override bool Equals(object obj) => Equals(obj as CanvasCell);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = cellIndex;
                hash = (hash * 397) ^ worldPosition.GetHashCode();
                hash = (hash * 397) ^ splatRadius.GetHashCode();
                hash = (hash * 397) ^ (int)packedColor;
                hash = (hash * 397) ^ opacity.GetHashCode();
                return hash;
            }
        }
    }
}
