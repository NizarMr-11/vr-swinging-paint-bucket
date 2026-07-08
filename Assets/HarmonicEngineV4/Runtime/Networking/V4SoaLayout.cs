namespace HarmonicEngineV4.Networking
{
  /// <summary>Immutable stride/capacity description for the V4 networking SoA.</summary>
  public readonly struct V4SoaLayout
  {
    public const int StrideBlock0 = 16;
    public const int StrideBlock1 = 16;
    public const int StridePackedColors = 4;
    public const int StrideFlags = 4;
    public const int StrideActiveHoleSdf = 4;
    public const int StrideHoleOwner = 4;
    public const int StrideRadialSegments = 16;
    public const int StrideActiveHoleWorldPos = 12;

    public readonly int capacity;

    public V4SoaLayout(int capacity)
    {
      this.capacity = capacity > 0 ? capacity : 1;
    }
  }
}
