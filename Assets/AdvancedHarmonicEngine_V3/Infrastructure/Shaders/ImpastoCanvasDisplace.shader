Shader "HarmonicEngine/ImpastoCanvasDisplace"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _HeightMap ("Impasto Height", 2D) = "black" {}
        _DisplacementScale ("Displacement Scale", Range(0, 0.2)) = 0.025
        _HeightNormalStrength ("Normal Strength", Range(0, 4)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _HeightMap;
            float4 _MainTex_ST;
            float _DisplacementScale;
            float _HeightNormalStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float height = tex2Dlod(_HeightMap, float4(v.uv, 0, 0)).r;
                float3 displaced = v.vertex.xyz + v.normal * height * _DisplacementScale;
                o.pos = UnityObjectToClipPos(float4(displaced, 1.0));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, float4(displaced, 1.0)).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 texel = float2(1.0 / 1024.0, 1.0 / 1024.0);
                float hL = tex2D(_HeightMap, i.uv + float2(-texel.x, 0)).r;
                float hR = tex2D(_HeightMap, i.uv + float2(texel.x, 0)).r;
                float hD = tex2D(_HeightMap, i.uv + float2(0, -texel.y)).r;
                float hU = tex2D(_HeightMap, i.uv + float2(0, texel.y)).r;
                float3 tangentNormal = normalize(float3(hL - hR, hD - hU, _HeightNormalStrength));
                float3 n = normalize(i.worldNormal + tangentNormal * 0.35);
                fixed4 albedo = tex2D(_MainTex, i.uv);
                float lighting = saturate(dot(n, normalize(float3(0.4, 1.0, 0.2))));
                return albedo * (0.35 + 0.65 * lighting);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
