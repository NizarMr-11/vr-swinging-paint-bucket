namespace HarmonicEngineV4.Networking
{
  /// <summary>Canonical HLSL buffer names for the V4 particle SoA (networking + GPU).</summary>
  public static class V4SoaBufferNames
  {
    public const string Block0 = "_Block0";
    public const string Block1 = "_Block1";
    public const string PackedColors = "_PackedColors";
    public const string Flags = "_Flags";
    public const string ActiveHoleSdf = "_ActiveHoleSdf";
    public const string HoleOwner = "_HoleOwner";
    public const string RadialSegments = "_RadialSegments";
    public const string ActiveHoleWorldPos = "_ActiveHoleWorldPos";
  }
}
