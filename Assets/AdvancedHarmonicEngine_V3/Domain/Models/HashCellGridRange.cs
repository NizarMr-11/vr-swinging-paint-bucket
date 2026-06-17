using System.Runtime.InteropServices;

namespace HarmonicEngine.Domain.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public struct HashCellGridRange
    {
        public int StartIndex;
        public int EndIndex;
    }
}
