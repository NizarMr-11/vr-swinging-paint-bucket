using System;
using UnityEngine;

namespace HarmonicEngineV4.Networking
{
  /// <summary>GPU buffer set for one side of the V4 networking SoA double buffer.</summary>
  public sealed class V4SoaBuffers : IDisposable
  {
    public ComputeBuffer Block0 { get; }
    public ComputeBuffer Block1 { get; }
    public ComputeBuffer PackedColors { get; }
    public ComputeBuffer Flags { get; }
    public ComputeBuffer ActiveHoleSdf { get; }
    public ComputeBuffer HoleOwner { get; }
    public ComputeBuffer RadialSegments { get; }
    public ComputeBuffer ActiveHoleWorldPos { get; }

    public int Capacity { get; }

    public V4SoaBuffers(V4SoaLayout layout)
    {
      Capacity = layout.capacity;
      Block0 = new ComputeBuffer(Capacity, V4SoaLayout.StrideBlock0, ComputeBufferType.Structured);
      Block1 = new ComputeBuffer(Capacity, V4SoaLayout.StrideBlock1, ComputeBufferType.Structured);
      PackedColors = new ComputeBuffer(Capacity, V4SoaLayout.StridePackedColors, ComputeBufferType.Structured);
      Flags = new ComputeBuffer(Capacity, V4SoaLayout.StrideFlags, ComputeBufferType.Structured);
      ActiveHoleSdf = new ComputeBuffer(Capacity, V4SoaLayout.StrideActiveHoleSdf, ComputeBufferType.Structured);
      HoleOwner = new ComputeBuffer(Capacity, V4SoaLayout.StrideHoleOwner, ComputeBufferType.Structured);
      RadialSegments = new ComputeBuffer(Capacity, V4SoaLayout.StrideRadialSegments, ComputeBufferType.Structured);
      ActiveHoleWorldPos = new ComputeBuffer(Capacity, V4SoaLayout.StrideActiveHoleWorldPos, ComputeBufferType.Structured);
    }

    public void Upload(V4SoaCpuSnapshot snapshot)
    {
      if (snapshot == null)
      {
        throw new ArgumentNullException(nameof(snapshot));
      }

      if (snapshot.count > Capacity)
      {
        throw new ArgumentException($"count {snapshot.count} exceeds capacity {Capacity}");
      }

      int count = snapshot.count;
      if (count > 0)
      {
        Block0.SetData(snapshot.block0, 0, 0, count);
        Block1.SetData(snapshot.block1, 0, 0, count);
        PackedColors.SetData(snapshot.packedColors, 0, 0, count);
        Flags.SetData(snapshot.flags, 0, 0, count);
        ActiveHoleSdf.SetData(snapshot.activeHoleSdf, 0, 0, count);
        HoleOwner.SetData(snapshot.holeOwner, 0, 0, count);
        RadialSegments.SetData(snapshot.radialSegments, 0, 0, count);
        ActiveHoleWorldPos.SetData(snapshot.activeHoleWorldPos, 0, 0, count);
      }
    }

    public V4SoaCpuSnapshot Download(int count)
    {
      if (count > Capacity)
      {
        throw new ArgumentException($"count {count} exceeds capacity {Capacity}");
      }

      var snapshot = new V4SoaCpuSnapshot { count = count };
      if (count <= 0)
      {
        return snapshot;
      }

      snapshot.block0 = new Vector4[count];
      snapshot.block1 = new Vector4[count];
      snapshot.packedColors = new uint[count];
      snapshot.flags = new uint[count];
      snapshot.activeHoleSdf = new float[count];
      snapshot.holeOwner = new uint[count];
      snapshot.radialSegments = new Vector4[count];
      snapshot.activeHoleWorldPos = new Vector3[count];

      Block0.GetData(snapshot.block0, 0, 0, count);
      Block1.GetData(snapshot.block1, 0, 0, count);
      PackedColors.GetData(snapshot.packedColors, 0, 0, count);
      Flags.GetData(snapshot.flags, 0, 0, count);
      ActiveHoleSdf.GetData(snapshot.activeHoleSdf, 0, 0, count);
      HoleOwner.GetData(snapshot.holeOwner, 0, 0, count);
      RadialSegments.GetData(snapshot.radialSegments, 0, 0, count);
      ActiveHoleWorldPos.GetData(snapshot.activeHoleWorldPos, 0, 0, count);
      return snapshot;
    }

    public void Dispose()
    {
      Block0?.Release();
      Block1?.Release();
      PackedColors?.Release();
      Flags?.Release();
      ActiveHoleSdf?.Release();
      HoleOwner?.Release();
      RadialSegments?.Release();
      ActiveHoleWorldPos?.Release();
    }
  }
}
