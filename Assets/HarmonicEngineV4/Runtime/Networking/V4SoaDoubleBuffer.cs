using System;

namespace HarmonicEngineV4.Networking
{
  /// <summary>
  /// Ping-pong SoA for Milestone A networking. Upload targets the write side;
  /// <see cref="Flip"/> promotes write to read without copying GPU memory.
  /// </summary>
  public sealed class V4SoaDoubleBuffer : IDisposable
  {
    private V4SoaBuffers _read;
    private V4SoaBuffers _write;
    private int _readCount;
    private int _writeCount;

    public V4SoaLayout Layout { get; }

    public V4SoaBuffers ActiveRead => _read;
    public V4SoaBuffers ActiveWrite => _write;

    public V4SoaDoubleBuffer(V4SoaLayout layout)
    {
      Layout = layout;
      _read = new V4SoaBuffers(layout);
      _write = new V4SoaBuffers(layout);
    }

    public void UploadSoa(V4SoaCpuSnapshot snapshot)
    {
      _write.Upload(snapshot);
      _writeCount = snapshot.count;
    }

    public V4SoaCpuSnapshot ReadActiveSnapshot() => _read.Download(_readCount);

    public void ApplyPatchToWrite(V4SoaCpuSnapshot patchSnapshot)
    {
      _write.Upload(patchSnapshot);
      _writeCount = patchSnapshot.count;
    }

    public void Flip()
    {
      (_read, _write) = (_write, _read);
      _readCount = _writeCount;
    }

    public void Dispose()
    {
      _read?.Dispose();
      _write?.Dispose();
      _read = null;
      _write = null;
    }
  }
}
