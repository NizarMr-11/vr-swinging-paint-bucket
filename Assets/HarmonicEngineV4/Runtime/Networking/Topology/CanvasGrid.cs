using System;
using HarmonicEngineV4.Networking.Serialization;
using Unity.Mathematics;

namespace HarmonicEngineV4.Networking.State
{
    public sealed class CanvasGrid : IEquatable<CanvasGrid>
    {
        public int resolutionX;
        public int resolutionY;
        public float cellSize;
        public float3 origin;
        [PrimitiveSubType(FieldType.Object)]
        public CanvasCell[] cells;

        public bool Equals(CanvasGrid other)
        {
            if (other == null)
            {
                return false;
            }

            if (resolutionX != other.resolutionX
                || resolutionY != other.resolutionY
                || !cellSize.Equals(other.cellSize)
                || !origin.Equals(other.origin))
            {
                return false;
            }

            if (cells == null && other.cells == null)
            {
                return true;
            }

            if (cells == null || other.cells == null || cells.Length != other.cells.Length)
            {
                return false;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                if (!Equals(cells[i], other.cells[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as CanvasGrid);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = resolutionX;
                hash = (hash * 397) ^ resolutionY;
                hash = (hash * 397) ^ cellSize.GetHashCode();
                hash = (hash * 397) ^ origin.GetHashCode();
                if (cells != null)
                {
                    foreach (CanvasCell cell in cells)
                    {
                        hash = (hash * 397) ^ (cell?.GetHashCode() ?? 0);
                    }
                }

                return hash;
            }
        }
    }
}
