using System;
using HarmonicEngineV4.Networking.Serialization;
using Unity.Mathematics;

namespace HarmonicEngineV4.Networking.State
{
    public sealed class BucketTopology : IEquatable<BucketTopology>
    {
        public float3 center;
        public float4 rotation;
        public float innerRadius;
        public float outerRadius;
        public float height;
        public float wallThickness;
        public float bottomThickness;
        public float topBandHeight;
        public float topBandOffset;
        [PrimitiveSubType(FieldType.Object)]
        public HoleDefinition[] holes;

        public bool Equals(BucketTopology other)
        {
            if (other == null)
            {
                return false;
            }

            if (!center.Equals(other.center)
                || !rotation.Equals(other.rotation)
                || !innerRadius.Equals(other.innerRadius)
                || !outerRadius.Equals(other.outerRadius)
                || !height.Equals(other.height)
                || !wallThickness.Equals(other.wallThickness)
                || !bottomThickness.Equals(other.bottomThickness)
                || !topBandHeight.Equals(other.topBandHeight)
                || !topBandOffset.Equals(other.topBandOffset))
            {
                return false;
            }

            if (holes == null && other.holes == null)
            {
                return true;
            }

            if (holes == null || other.holes == null || holes.Length != other.holes.Length)
            {
                return false;
            }

            for (int i = 0; i < holes.Length; i++)
            {
                if (!Equals(holes[i], other.holes[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as BucketTopology);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = center.GetHashCode();
                hash = (hash * 397) ^ rotation.GetHashCode();
                hash = (hash * 397) ^ innerRadius.GetHashCode();
                hash = (hash * 397) ^ outerRadius.GetHashCode();
                hash = (hash * 397) ^ height.GetHashCode();
                hash = (hash * 397) ^ wallThickness.GetHashCode();
                hash = (hash * 397) ^ bottomThickness.GetHashCode();
                hash = (hash * 397) ^ topBandHeight.GetHashCode();
                hash = (hash * 397) ^ topBandOffset.GetHashCode();
                if (holes != null)
                {
                    foreach (HoleDefinition hole in holes)
                    {
                        hash = (hash * 397) ^ (hole?.GetHashCode() ?? 0);
                    }
                }

                return hash;
            }
        }
    }
}
