Shader "HarmonicEngine/ParticleDebugPoints"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.5, 0.2, 1)
        _PointSize ("Point Size", Float) = 0.025
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
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

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float psize : PSIZE;
            };

            v2f vert(uint vertexId : SV_VertexID)
            {
                v2f o;
                FluidParticle particle = _Particles[vertexId];
                o.pos = UnityWorldToClipPos(float4(particle.Position, 1.0));
                o.color = _Color;
                o.psize = _PointSize * 1000.0;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
