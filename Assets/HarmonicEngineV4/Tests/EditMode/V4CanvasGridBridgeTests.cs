using HarmonicEngineV4.Networking;
using HarmonicEngineV4.Networking.State;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4CanvasGridBridgeTests
    {
        [Test]
        public void PackRgb_UnpackColor_RoundTrip()
        {
            var cell = new Vector4(0.2f, 0.4f, 0.6f, 1f);
            uint packed = V4CanvasGridBridge.PackRgb(cell);
            float3 rgb = V4CanvasGridBridge.UnpackColor(packed);

            Assert.AreEqual(0.2f, rgb.x, 1f / 255f);
            Assert.AreEqual(0.4f, rgb.y, 1f / 255f);
            Assert.AreEqual(0.6f, rgb.z, 1f / 255f);
        }

        [Test]
        public void CloneCanvasGrid_DeepCopiesCells()
        {
            var source = new CanvasGrid
            {
                resolutionX = 4,
                resolutionY = 4,
                cellSize = 0.01f,
                origin = new float3(0f, -1f, 0f),
                cells = new[]
                {
                    new CanvasCell
                    {
                        cellIndex = 3,
                        worldPosition = new float3(0.1f, -1f, 0.2f),
                        splatRadius = 0.005f,
                        packedColor = 0xFF8040FFu,
                        opacity = 0.5f
                    }
                }
            };

            CanvasGrid clone = V4CanvasGridBridge.CloneCanvasGrid(source);
            source.cells[0].opacity = 0.1f;

            Assert.AreNotSame(source.cells, clone.cells);
            Assert.AreEqual(0.5f, clone.cells[0].opacity, 1e-5f);
            Assert.AreEqual(3, clone.cells[0].cellIndex);
        }
    }
}
