Shader "HarmonicEngine/SSFluidRender"
{
    Properties
    {
        _SplatRadius ("Splat Radius", Float) = 0.05
        _ThicknessWeight ("Thickness Weight", Range(0.001, 0.2)) = 0.035
        _UseParticleColor ("Use Particle Color", Float) = 1
        _MaxEyeDepth ("Max Eye Depth (Debug)", Float) = 12

        _BlurFalloff ("Blur Falloff", Float) = 0.05
        _BlurRadius ("Blur Radius", Float) = 3.0
        _NormalScale ("Normal Scale", Float) = 100
        _FluidColor ("Fluid Color", Color) = (0.1, 0.4, 0.8, 1)
        _SpecularPower ("Specular Power", Float) = 250
        _SpecularIntensity ("Specular Intensity", Float) = 1.5
        _ThicknessAbsorption ("Thickness Absorption", Float) = 2.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Transparent" }

        // Pass 0 — sphere impostors into depth + thickness MRT
        Pass
        {
            Name "DepthAndThickness"
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend 0 One Zero
            Blend 1 SrcAlpha One

            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment fragDepthThickness
            #pragma target 4.5
            #include "UnityCG.cginc"

            struct FluidParticle
            {
                float3 Position;
                float Density;
                float3 Velocity;
                float Pressure;
                uint PackedColorRGBA;
                float3 _Padding;
            };

            StructuredBuffer<FluidParticle> _Particles;
            uint _ParticleCount;
            float _SplatRadius;
            float4 _FluidColor;
            float _ThicknessWeight;
            float _UseParticleColor;

            float3 UnpackUintToFloat3(uint packed)
            {
                float r = (float)(packed & 0xFFu);
                float g = (float)((packed >> 8) & 0xFFu);
                float b = (float)((packed >> 16) & 0xFFu);
                return float3(r, g, b) / 255.0;
            }

            struct v2g
            {
                float3 worldCenter : TEXCOORD0;
                float3 particleRgb : TEXCOORD1;
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldCenter : TEXCOORD1;
                float3 particleRgb : TEXCOORD2;
                float3 worldCorner : TEXCOORD3;
            };

            struct FragOut
            {
                float4 depth : SV_Target0;
                float4 thickness : SV_Target1;
            };

            v2g vert(uint id : SV_VertexID)
            {
                v2g o;
                FluidParticle p = _Particles[id];
                o.worldCenter = p.Position;
                o.particleRgb = UnpackUintToFloat3(p.PackedColorRGBA);
                return o;
            }

            [maxvertexcount(4)]
            void geom(point v2g input[1], inout TriangleStream<g2f> triStream)
            {
                float3 worldCenter = input[0].worldCenter;
                float3 viewRight = UNITY_MATRIX_V[0].xyz * _SplatRadius;
                float3 viewUp = UNITY_MATRIX_V[1].xyz * _SplatRadius;

                float3 corners[4] = {
                    worldCenter - viewRight - viewUp,
                    worldCenter + viewRight - viewUp,
                    worldCenter - viewRight + viewUp,
                    worldCenter + viewRight + viewUp
                };

                float2 uvs[4] = {
                    float2(0.0, 0.0),
                    float2(1.0, 0.0),
                    float2(0.0, 1.0),
                    float2(1.0, 1.0)
                };

                g2f o;
                o.worldCenter = worldCenter;
                o.particleRgb = input[0].particleRgb;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    o.worldCorner = corners[i];
                    o.pos = UnityWorldToClipPos(float4(corners[i], 1.0));
                    o.uv = uvs[i];
                    triStream.Append(o);
                }

                triStream.RestartStrip();
            }

            bool IntersectSphere(float3 rayOrigin, float3 rayDir, float3 center, float radius, out float3 hitWorld)
            {
                float3 oc = rayOrigin - center;
                float b = dot(rayDir, oc);
                float c = dot(oc, oc) - radius * radius;
                float disc = b * b - c;
                if (disc < 0.0)
                {
                    hitWorld = 0;
                    return false;
                }

                float t = -b - sqrt(disc);
                if (t <= 0.0)
                {
                    t = -b + sqrt(disc);
                }

                if (t <= 0.0)
                {
                    hitWorld = 0;
                    return false;
                }

                hitWorld = rayOrigin + rayDir * t;
                return true;
            }

            FragOut fragDepthThickness(g2f i, out float outDepth : SV_Depth)
            {
                FragOut o;
                o.depth = 0;
                o.thickness = 0;

                float3 worldCamera = _WorldSpaceCameraPos;
                float3 worldCenter = i.worldCenter;
                float3 rayDir = normalize(i.worldCorner - worldCamera);

                float3 hitWorld;
                if (!IntersectSphere(worldCamera, rayDir, worldCenter, _SplatRadius, hitWorld))
                {
                    discard;
                }

                float3 hitView = mul(UNITY_MATRIX_V, float4(hitWorld, 1.0)).xyz;
                float eyeDepth = -hitView.z;

                float4 hitClip = UnityWorldToClipPos(float4(hitWorld, 1.0));
                outDepth = hitClip.z / hitClip.w;

                float3 rgb = lerp(_FluidColor.rgb, i.particleRgb, saturate(_UseParticleColor));
                float weight = _ThicknessWeight;

                o.depth = float4(eyeDepth, 0.0, 0.0, 1.0);
                o.thickness = float4(rgb * weight, weight);
                return o;
            }
            ENDCG
        }

        // Pass 1 — bilateral blur of linear eye depth
        Pass
        {
            Name "BilateralBlur"
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragBlur
            #pragma target 5.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurFalloff;
            float _BlurRadius;

            float4 fragBlur(v2f_img i) : SV_Target
            {
                float centerDepth = tex2D(_MainTex, i.uv).r;
                if (centerDepth <= 0.0)
                {
                    return 0.0;
                }

                float sum = 0.0;
                float weightSum = 0.0;

                for (int x = -2; x <= 2; x++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        float2 offset = float2(x, y) * _MainTex_TexelSize.xy * _BlurRadius;
                        float sampleDepth = tex2D(_MainTex, i.uv + offset).r;

                        if (sampleDepth > 0.0)
                        {
                            float spatialW = exp(-(x * x + y * y) / 8.0);
                            float depthDiff = sampleDepth - centerDepth;
                            float rangeW = exp(-(depthDiff * depthDiff) / _BlurFalloff);
                            float w = spatialW * rangeW;
                            sum += sampleDepth * w;
                            weightSum += w;
                        }
                    }
                }

                float blurredDepth = sum / max(weightSum, 0.0001);
                return float4(blurredDepth, 0.0, 0.0, 1.0);
            }
            ENDCG
        }

        // Pass 2 — normal reconstruction, lighting, alpha composite
        Pass
        {
            Name "Composite"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragComposite
            #pragma target 5.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _FluidThicknessTexture;

            float _NormalScale;
            float4 _FluidColor;
            float _SpecularPower;
            float _SpecularIntensity;
            float _ThicknessAbsorption;

            float4 fragComposite(v2f_img i) : SV_Target
            {
                float depth = tex2D(_MainTex, i.uv).r;
                if (depth <= 0.0)
                {
                    return float4(0.0, 0.0, 0.0, 0.0);
                }

                float dzdx = ddx(depth) * _NormalScale;
                float dzdy = ddy(depth) * _NormalScale;
                float3 viewNormal = normalize(float3(-dzdx, -dzdy, 1.0));

                float4 thicknessSample = tex2D(_FluidThicknessTexture, i.uv);
                float thickness = thicknessSample.a;
                float3 particleColor = thicknessSample.rgb / max(thickness, 0.0001);
                float3 baseColor = lerp(_FluidColor.rgb, particleColor, 0.5);

                float3 viewDir = float3(0.0, 0.0, 1.0);
                float3 lightDir = normalize(float3(0.5, 0.7, -1.0));
                float NdotL = max(0.0, dot(viewNormal, lightDir));
                float3 halfVector = normalize(lightDir + viewDir);
                float NdotH = max(0.0, dot(viewNormal, halfVector));
                float specular = pow(NdotH, _SpecularPower) * _SpecularIntensity;
                float fresnel = pow(1.0 - max(0.0, dot(viewNormal, viewDir)), 3.0);

                float3 finalColor = (baseColor * NdotL) + specular + (fresnel * 0.3);
                float alpha = saturate(thickness * _ThicknessAbsorption);

                return float4(finalColor, alpha);
            }
            ENDCG
        }

        // Pass 3 — debug visualization of Pass 0 linear eye depth (near=white, far=black)
        Pass
        {
            Name "DebugDepthVis"
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vertFull
            #pragma fragment fragDebugDepth
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _FluidDepth;
            float _MaxEyeDepth;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vertFull(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 fragDebugDepth(v2f i) : SV_Target
            {
                float eyeDepth = tex2D(_FluidDepth, i.uv).r;
                if (eyeDepth <= 1e-5)
                {
                    return 0;
                }

                float gray = 1.0 - saturate(eyeDepth / max(_MaxEyeDepth, 1e-3));
                return float4(gray, gray, gray, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
