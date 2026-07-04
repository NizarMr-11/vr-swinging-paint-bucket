// Debug particle point renderer (plan Phase 6): camera-facing quads per particle,
// colored by particle color with a state tint (Inside normal, Outside dimmed,
// escaped highlighted). Fast bring-up visual, not the final fluid render.
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

            StructuredBuffer<float4> _Block0;
            StructuredBuffer<uint> _PackedColors;
            StructuredBuffer<uint> _Flags;
            float _PointSize;
            uint _ActiveParticleCount;

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
                uint flags = _Flags[instanceId];

                uint packed = _PackedColors[instanceId];
                float3 color = float3(packed & 0xFF, (packed >> 8) & 0xFF, (packed >> 16) & 0xFF) / 255.0;

                bool inside = (flags & 1u) != 0u;
                bool escaped = (flags & 2u) != 0u;
                if (escaped)
                {
                    color = lerp(color, float3(1.0, 0.55, 0.1), 0.35); // escaped: orange tint
                }
                else if (!inside)
                {
                    color *= 0.55; // outside: dimmed
                }

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
