#ifndef HARMONIC_SPH_SOA_STORE_INCLUDED
#define HARMONIC_SPH_SOA_STORE_INCLUDED

// Write-side helpers (included only by integration kernels).

RWStructuredBuffer<float4> _WriteBlock0;
RWStructuredBuffer<float4> _WriteBlock1;
RWStructuredBuffer<uint> _WritePackedColors;
RWStructuredBuffer<float> _WriteWetness;

void SphStoreParticle(
    uint i,
    float3 pos,
    float3 vel,
    float density,
    float pressure,
    uint packedColor,
    float wetness)
{
    _WriteBlock0[i] = float4(pos, density);
    _WriteBlock1[i] = float4(vel, pressure);
    _WritePackedColors[i] = packedColor;
    _WriteWetness[i] = wetness;
}

AppendStructuredBuffer<float4> _InternalBlock0;
AppendStructuredBuffer<float4> _InternalBlock1;
AppendStructuredBuffer<uint> _InternalPackedColors;
AppendStructuredBuffer<float> _InternalWetness;

AppendStructuredBuffer<float4> _FallingBlock0;
AppendStructuredBuffer<float4> _FallingBlock1;
AppendStructuredBuffer<uint> _FallingPackedColors;
AppendStructuredBuffer<float> _FallingWetness;

void SphAppendInternal(
    float3 pos,
    float3 vel,
    float density,
    float pressure,
    uint packedColor,
    float wetness)
{
    _InternalBlock0.Append(float4(pos, density));
    _InternalBlock1.Append(float4(vel, pressure));
    _InternalPackedColors.Append(packedColor);
    _InternalWetness.Append(wetness);
}

void SphAppendFalling(
    float3 pos,
    float3 vel,
    float density,
    float pressure,
    uint packedColor,
    float wetness)
{
    _FallingBlock0.Append(float4(pos, density));
    _FallingBlock1.Append(float4(vel, pressure));
    _FallingPackedColors.Append(packedColor);
    _FallingWetness.Append(wetness);
}

#endif // HARMONIC_SPH_SOA_STORE_INCLUDED
