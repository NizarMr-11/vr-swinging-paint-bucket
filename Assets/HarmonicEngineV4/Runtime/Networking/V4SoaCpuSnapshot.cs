using System;
using UnityEngine;

namespace HarmonicEngineV4.Networking
{
  /// <summary>CPU-side snapshot of all V4 networking SoA buffers.</summary>
  public sealed class V4SoaCpuSnapshot
  {
    public Vector4[] block0 = Array.Empty<Vector4>();
    public Vector4[] block1 = Array.Empty<Vector4>();
    public uint[] packedColors = Array.Empty<uint>();
    public uint[] flags = Array.Empty<uint>();
    public float[] activeHoleSdf = Array.Empty<float>();
    public uint[] holeOwner = Array.Empty<uint>();
    public Vector4[] radialSegments = Array.Empty<Vector4>();
    public Vector3[] activeHoleWorldPos = Array.Empty<Vector3>();
    public int count;

    public V4SoaCpuSnapshot Clone()
    {
      return new V4SoaCpuSnapshot
      {
        block0 = (Vector4[])block0.Clone(),
        block1 = (Vector4[])block1.Clone(),
        packedColors = (uint[])packedColors.Clone(),
        flags = (uint[])flags.Clone(),
        activeHoleSdf = (float[])activeHoleSdf.Clone(),
        holeOwner = (uint[])holeOwner.Clone(),
        radialSegments = (Vector4[])radialSegments.Clone(),
        activeHoleWorldPos = (Vector3[])activeHoleWorldPos.Clone(),
        count = count
      };
    }
  }
}
