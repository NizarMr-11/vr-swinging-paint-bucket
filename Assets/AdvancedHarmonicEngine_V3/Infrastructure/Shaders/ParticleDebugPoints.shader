Shader "HarmonicEngine/ParticleDebugPoints"
{
    Properties
    {
        _Color ("Color", Color) = (0.25, 0.55, 0.95, 0.85)
        _PointSize ("Point Size (world radius)", Float) = 0.18
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            struct FluidParticle
            {
                float3 Position;
                float Density;
                float3 Velocity;
                float Pressure;
            };

            StructuredBuffer<FluidParticle> _Particles;
            uint _ParticleCount;
            float _PointSize;
            fixed4 _Color;

            struct v2g
            {
                float3 worldPos : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2g vert(uint vertexId : SV_VertexID)
            {
                v2g o;
                FluidParticle particle = _Particles[vertexId];
                o.worldPos = particle.Position;
                o.color = _Color;
                return o;
            }

            [maxvertexcount(4)]
            void geom(point v2g input[1], inout TriangleStream<g2f> triStream)
            {
                float3 worldPos = input[0].worldPos;
                float3 viewRight = UNITY_MATRIX_V[0].xyz * _PointSize;
                float3 viewUp = UNITY_MATRIX_V[1].xyz * _PointSize;

                float3 corners[4] = {
                    worldPos - viewRight - viewUp,
                    worldPos + viewRight - viewUp,
                    worldPos - viewRight + viewUp,
                    worldPos + viewRight + viewUp
                };

                float2 uvs[4] = {
                    float2(0.0, 0.0),
                    float2(1.0, 0.0),
                    float2(0.0, 1.0),
                    float2(1.0, 1.0)
                };

                g2f o;
                o.color = input[0].color;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    o.pos = UnityWorldToClipPos(float4(corners[i], 1.0));
                    o.uv = uvs[i];
                    triStream.Append(o);
                }

                triStream.RestartStrip();
            }

            fixed4 frag(g2f i) : SV_Target
            {
                float2 centered = i.uv * 2.0 - 1.0;
                float distSq = dot(centered, centered);
                if (distSq > 1.0)
                {
                    discard;
                }

                float edge = 1.0 - distSq;
                float alpha = edge * edge;
                return fixed4(i.color.rgb, i.color.a * alpha);
            }
            ENDCG
        }
    }
}
