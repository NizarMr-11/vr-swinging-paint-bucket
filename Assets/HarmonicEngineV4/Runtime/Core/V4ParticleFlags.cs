namespace HarmonicEngineV4.Core
{
    public enum V4Zone : uint
    {
        None = 0,
        HoleZone0 = 1,
        HeightLayer = 2,
        HoleZone1 = 3,
        HoleZone2 = 4,
        TopBand = 5
    }

    /// <summary>
    /// CPU mirror of the particle flag bit layout in V4Common.hlsl. Parity tests assert
    /// both sides pack/unpack identically.
    ///   bit 0      : Inside bucket volume this frame (reversible)
    ///   bit 1      : hasEscaped latch (permanent; only the classification kernel sets it)
    ///   bits 2-4   : zone assignment this frame (HeightLayer stores layer index in hole bits)
    ///   bits 5-9   : owning hole index
    ///   bits 10-17 : liquid profile index
    /// </summary>
    public static class V4ParticleFlags
    {
        public const uint InsideBit = 1u << 0;
        public const uint EscapedBit = 1u << 1;
        public const int ZoneShift = 2;
        public const uint ZoneMask = 7u << ZoneShift;
        public const int HoleShift = 5;
        public const uint HoleMask = 31u << HoleShift;
        public const int ProfileShift = 10;
        public const uint ProfileMask = 255u << ProfileShift;

        public const int MaxHoles = 32;
        public const int MaxProfiles = 256;

        public static bool IsInside(uint flags) => (flags & InsideBit) != 0;
        public static bool HasEscaped(uint flags) => (flags & EscapedBit) != 0;
        public static V4Zone GetZone(uint flags) => (V4Zone)((flags & ZoneMask) >> ZoneShift);
        public static int GetHole(uint flags) => (int)((flags & HoleMask) >> HoleShift);

        /// <summary>Layer index when zone is <see cref="V4Zone.HeightLayer"/>.</summary>
        public static int GetLayerIndex(uint flags) => GetHole(flags);
        public static int GetProfile(uint flags) => (int)((flags & ProfileMask) >> ProfileShift);

        public static uint SetInside(uint flags, bool inside) =>
            inside ? flags | InsideBit : flags & ~InsideBit;

        public static uint SetEscaped(uint flags) => flags | EscapedBit;

        public static uint SetZone(uint flags, V4Zone zone) =>
            (flags & ~ZoneMask) | (((uint)zone << ZoneShift) & ZoneMask);

        public static uint SetHole(uint flags, int hole) =>
            (flags & ~HoleMask) | (((uint)hole << HoleShift) & HoleMask);

        public static uint SetProfile(uint flags, int profile) =>
            (flags & ~ProfileMask) | (((uint)profile << ProfileShift) & ProfileMask);
    }
}
