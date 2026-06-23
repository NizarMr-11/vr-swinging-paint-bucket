#ifndef HARMONIC_SPH_SOA_ACCESS_INCLUDED
#define HARMONIC_SPH_SOA_ACCESS_INCLUDED

// Packed SOA (4 buffers, fits D3D11 8-UAV limit):
//   Block0: float4(position.xyz, density)
//   Block1: float4(velocity.xyz, pressure)
//   PackedColors: uint
//   Wetness: float

StructuredBuffer<float4> _Block0;
StructuredBuffer<float4> _Block1;
StructuredBuffer<uint> _PackedColors;
StructuredBuffer<float> _Wetness;

float3 SphLoadPosition(uint i)
{
    return _Block0[i].xyz;
}

float3 SphLoadVelocity(uint i)
{
    return _Block1[i].xyz;
}

float SphLoadDensity(uint i)
{
    return _Block0[i].w;
}

float SphLoadPressure(uint i)
{
    return _Block1[i].w;
}

uint SphLoadPackedColor(uint i)
{
    return _PackedColors[i];
}

float SphLoadWetness(uint i)
{
    return _Wetness[i];
}

#endif // HARMONIC_SPH_SOA_ACCESS_INCLUDED
