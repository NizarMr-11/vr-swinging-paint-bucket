// Debug particle point renderer (plan Phase 6): camera-facing quads per particle,
// colored purely by particle paint color so every liquid (including black) reads as
// its authored color. Fast bring-up visual, not the final fluid render.
//
// TEMP DIAGNOSTIC — optional flag tint (_ShowFlagTint) for pool Inside/Outside flicker
// investigation. Remove after diagnosis.
//   Inside + TopBand → yellow | Inside + other zone → green | Outside → red
Shader "HarmonicEngineV4/DebugPoints"
{
    Properties
    {
        _PointSize ("Point Size", Float) = 0.01
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            // Billboard quads are generated in view space; their winding depends on
            // the camera, so back-face culling must be off or they vanish entirely.
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"
            #include "Include/V4Common.hlsl"

            StructuredBuffer<float4> _Block0;
            StructuredBuffer<uint> _PackedColors;
            StructuredBuffer<uint> _Flags;
            float _PointSize;
            uint _ActiveParticleCount;
            float _ShowFlagTint;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 color : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            static const float2 kCorners[6] =
            {
                float2(-1, -1), float2(1, -1), float2(1, 1),
                float2(-1, -1), float2(1, 1), float2(-1, 1)
            };

            float3 UnpackPaintColor(uint packed)
            {
                return float3(packed & 0xFFu, (packed >> 8) & 0xFFu, (packed >> 16) & 0xFFu) / 255.0;
            }

            // TEMP DIAGNOSTIC — classification tint for flicker investigation.
            float3 FlagDiagnosticColor(uint flags)
            {
                if (!V4IsInside(flags))
                {
                    return float3(1.0, 0.0, 0.0);
                }

                if (V4GetZone(flags) == V4_ZONE_HEIGHT_LAYER)
                {
                    return float3(0.2, 0.85, 1.0);
                }

                return float3(0.0, 1.0, 0.0);
            }

            v2f vert(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
            {
                v2f o;
                if (instanceId >= _ActiveParticleCount)
                {
                    o.pos = float4(0, 0, -10, 1); // clipped
                    o.color = 0;
                    o.uv = 0;
                    return o;
                }

                float4 block0 = _Block0[instanceId];

                float3 paintColor = UnpackPaintColor(_PackedColors[instanceId]);
                float3 flagColor = FlagDiagnosticColor(_Flags[instanceId]);
                float3 color = lerp(paintColor, flagColor, saturate(_ShowFlagTint));

                float2 corner = kCorners[vertexId] * _PointSize;
                float3 camRight = UNITY_MATRIX_V[0].xyz;
                float3 camUp = UNITY_MATRIX_V[1].xyz;
                float3 worldPos = block0.xyz + camRight * corner.x + camUp * corner.y;

                o.pos = UnityWorldToClipPos(worldPos);
                o.color = color;
                o.uv = kCorners[vertexId];
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (dot(i.uv, i.uv) > 1.0)
                {
                    discard; // round points
                }

                return fixed4(i.color, 1.0);
            }
            ENDCG
        }
    }
}
