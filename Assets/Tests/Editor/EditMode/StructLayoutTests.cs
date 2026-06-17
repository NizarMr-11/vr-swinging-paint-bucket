using HarmonicEngine.Domain.Models;
using NUnit.Framework;
using System.Runtime.InteropServices;

namespace HarmonicEngine.Tests
{
    public class StructLayoutTests
    {
        [Test]
        public void FluidParticle_Size_Is32Bytes()
        {
            Assert.AreEqual(32, Marshal.SizeOf<FluidParticle>());
        }

        [Test]
        public void QuantizedBakeParticle_Size_Is16Bytes()
        {
            Assert.AreEqual(16, Marshal.SizeOf<QuantizedBakeParticle>());
        }

        [Test]
        public void GridKeyPair_Size_Is8Bytes()
        {
            Assert.AreEqual(8, Marshal.SizeOf<GridKeyPair>());
        }

        [Test]
        public void HashCellGridRange_Size_Is8Bytes()
        {
            Assert.AreEqual(8, Marshal.SizeOf<HashCellGridRange>());
        }

        [Test]
        public void VoxelDragCell_Size_Is16Bytes()
        {
            Assert.AreEqual(16, Marshal.SizeOf<VoxelDragCell>());
        }
    }
}
