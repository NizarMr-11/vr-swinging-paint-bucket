Shader "HarmonicEngineV4/SSFluidRender"
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

        _BlurCoveragePass ("", Float) = 0
        _BlurDepthPass ("", Float) = 0
        _BlurThicknessOutput ("", Float) = 0
        _FluidDepth ("", 2D) = "black" {}
        _FluidThicknessTexture ("", 2D) = "black" {}
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
            #include "Include/V4Common.hlsl"

            // SOA position/velocity/color/flags buffers bound from C#.
            StructuredBuffer<float4> _Block0;
            StructuredBuffer<float4> _Block1;
            StructuredBuffer<uint> _Flags;
            StructuredBuffer<uint> _PackedColors;
            uint _ParticleCount;
            float _SplatRadius;
            float _VelocityStretchScale;
            float _VelocityStretchMax;
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
                float3 worldVelocity : TEXCOORD2;
                uint particleFlags : TEXCOORD3;
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldCenter : TEXCOORD1;
                float3 particleRgb : TEXCOORD2;
                float3 worldCorner : TEXCOORD3;
                float3 stretchDir : TEXCOORD4;
                float3 perpDir : TEXCOORD5;
                float stretchFactor : TEXCOORD6;
            };

            struct FragOut
            {
                float4 depth : SV_Target0;
                float4 thickness : SV_Target1;
            };

            v2g vert(uint id : SV_VertexID)
            {
                v2g o;
                float4 b0 = _Block0[id];
                o.worldCenter = b0.xyz;
                o.worldVelocity = _Block1[id].xyz;
                o.particleFlags = _Flags[id];
                o.particleRgb = UnpackUintToFloat3(_PackedColors[id]);
                return o;
            }

            [maxvertexcount(4)]
            void geom(point v2g input[1], inout TriangleStream<g2f> triStream)
            {
                float3 worldCenter = input[0].worldCenter;
                float3 worldVelocity = input[0].worldVelocity;
                bool inside = V4IsInside(input[0].particleFlags);
                float3 viewRightUnit = UNITY_MATRIX_V[0].xyz;
                float3 viewUpUnit = UNITY_MATRIX_V[1].xyz;
                float3 viewForward = -UNITY_MATRIX_V[2].xyz;

                float stretchFactor = 1.0;
                if (!inside)
                {
                    float speed = length(worldVelocity);
                    stretchFactor = clamp(1.0 + speed * _VelocityStretchScale, 1.0, _VelocityStretchMax);
                }

                float3 velView = mul((float3x3)UNITY_MATRIX_V, worldVelocity);
                float2 velScreen = velView.xy;
                float planarSpeed = length(velScreen);

                float3 stretchAxis;
                float3 perpAxis;
                float3 stretchDir;
                float3 perpDir;

                if (inside || planarSpeed < 1e-6)
                {
                    stretchDir = viewRightUnit;
                    perpDir = viewUpUnit;
                    stretchAxis = viewRightUnit * _SplatRadius;
                    perpAxis = viewUpUnit * _SplatRadius;
                    stretchFactor = 1.0;
                }
                else
                {
                    float2 velDirScreen = velScreen / planarSpeed;
                    stretchDir = normalize(viewRightUnit * velDirScreen.x + viewUpUnit * velDirScreen.y);
                    perpDir = normalize(cross(viewForward, stretchDir));
                    stretchAxis = stretchDir * (_SplatRadius * stretchFactor);
                    perpAxis = perpDir * _SplatRadius;
                }

                float3 corners[4] = {
                    worldCenter - stretchAxis - perpAxis,
                    worldCenter + stretchAxis - perpAxis,
                    worldCenter - stretchAxis + perpAxis,
                    worldCenter + stretchAxis + perpAxis
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
                o.stretchDir = stretchDir;
                o.perpDir = perpDir;
                o.stretchFactor = stretchFactor;

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

            bool IntersectViewAlignedEllipsoid(
                float3 rayOrigin,
                float3 rayDir,
                float3 center,
                float3 stretchDir,
                float3 perpDir,
                float3 viewForward,
                float baseRadius,
                float stretchFactor,
                out float3 hitWorld)
            {
                float a = baseRadius * stretchFactor;
                float b = baseRadius;

                float3 ro = rayOrigin - center;
                float3 rd = rayDir;

                float3 oL = float3(dot(ro, stretchDir), dot(ro, perpDir), dot(ro, viewForward));
                float3 dL = float3(dot(rd, stretchDir), dot(rd, perpDir), dot(rd, viewForward));

                float3 oN = float3(oL.x / a, oL.y / b, oL.z / b);
                float3 dN = float3(dL.x / a, dL.y / b, dL.z / b);

                float A = dot(dN, dN);
                float B = 2.0 * dot(oN, dN);
                float C = dot(oN, oN) - 1.0;
                float disc = B * B - 4.0 * A * C;
                if (disc < 0.0)
                {
                    hitWorld = 0;
                    return false;
                }

                float sqrtDisc = sqrt(disc);
                float t = (-B - sqrtDisc) / max(A, 1e-6);
                if (t <= 0.0)
                {
                    t = (-B + sqrtDisc) / max(A, 1e-6);
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
                float3 viewForward = -UNITY_MATRIX_V[2].xyz;

                float3 hitWorld;
                if (!IntersectViewAlignedEllipsoid(
                    worldCamera,
                    rayDir,
                    worldCenter,
                    i.stretchDir,
                    i.perpDir,
                    viewForward,
                    _SplatRadius,
                    i.stretchFactor,
                    hitWorld))
                {
                    discard;
                }

                float3 hitView = mul(UNITY_MATRIX_V, float4(hitWorld, 1.0)).xyz;
                float eyeDepth = -hitView.z;

                float4 hitClip = UnityWorldToClipPos(float4(hitWorld, 1.0));
                outDepth = hitClip.z / hitClip.w;

                float3 rgb = lerp(_FluidColor.rgb, i.particleRgb, saturate(_UseParticleColor));
                float weight = _ThicknessWeight;

                o.depth = float4(eyeDepth, 1.0, 0.0, 1.0);
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
            sampler2D _FluidDepth;
            float4 _MainTex_TexelSize;
            float _BlurFalloff;
            float _BlurRadius;
            float _BlurCoveragePass;
            float _BlurDepthPass;
            float _BlurThicknessOutput;

            void BilateralBlurThickness(
                float2 uv,
                float centerDepth,
                out float4 blurredThickness)
            {
                float3 sumThicknessRgb = 0.0;
                float sumThicknessA = 0.0;
                float weightSum = 0.0;

                for (int x = -2; x <= 2; x++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        float2 offset = float2(x, y) * _MainTex_TexelSize.xy * _BlurRadius;
                        float2 sampleUv = uv + offset;
                        float sampleDepth = tex2D(_FluidDepth, sampleUv).r;

                        if (sampleDepth > 0.0)
                        {
                            float spatialW = exp(-(x * x + y * y) / 8.0);
                            float depthDiff = sampleDepth - centerDepth;
                            float rangeW = exp(-(depthDiff * depthDiff) / _BlurFalloff);
                            float w = spatialW * rangeW;
                            float4 sampleThickness = tex2D(_MainTex, sampleUv);
                            sumThicknessRgb += sampleThickness.rgb * w;
                            sumThicknessA += sampleThickness.a * w;
                            weightSum += w;
                        }
                    }
                }

                float invWeight = 1.0 / max(weightSum, 0.0001);
                blurredThickness = float4(sumThicknessRgb * invWeight, sumThicknessA * invWeight);
            }

            float BilateralBlurDepth(float2 uv, float centerDepth)
            {
                float sumDepth = 0.0;
                float weightSum = 0.0;

                for (int x = -2; x <= 2; x++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        float2 offset = float2(x, y) * _MainTex_TexelSize.xy * _BlurRadius;
                        float sampleDepth = tex2D(_MainTex, uv + offset).r;

                        if (sampleDepth > 0.0)
                        {
                            float spatialW = exp(-(x * x + y * y) / 8.0);
                            float depthDiff = sampleDepth - centerDepth;
                            float rangeW = exp(-(depthDiff * depthDiff) / _BlurFalloff);
                            float w = spatialW * rangeW;
                            sumDepth += sampleDepth * w;
                            weightSum += w;
                        }
                    }
                }

                return sumDepth / max(weightSum, 0.0001);
            }

            float4 fragBlur(v2f_img i) : SV_Target
            {
                if (_BlurThicknessOutput > 0.5)
                {
                    float centerDepth = tex2D(_FluidDepth, i.uv).r;
                    if (centerDepth <= 0.0)
                    {
                        return 0.0;
                    }

                    float4 blurredThickness;
                    BilateralBlurThickness(i.uv, centerDepth, blurredThickness);
                    return blurredThickness;
                }

                float4 center = tex2D(_MainTex, i.uv);
                float centerDepth = center.r;

                if (_BlurCoveragePass > 0.5)
                {
                    if (centerDepth <= 0.0)
                    {
                        return 0.0;
                    }

                    float outDepth = centerDepth;
                    if (_BlurDepthPass > 0.5)
                    {
                        outDepth = BilateralBlurDepth(i.uv, centerDepth);
                    }

                    float validSpatialSum = 0.0;
                    float totalSpatialSum = 0.0;
                    for (int x = -2; x <= 2; x++)
                    {
                        for (int y = -2; y <= 2; y++)
                        {
                            float spatialW = exp(-(x * x + y * y) / 8.0);
                            totalSpatialSum += spatialW;

                            float2 offset = float2(x, y) * _MainTex_TexelSize.xy * _BlurRadius;
                            float sampleDepth = tex2D(_MainTex, i.uv + offset).r;
                            if (sampleDepth > 0.0)
                            {
                                validSpatialSum += spatialW;
                            }
                        }
                    }

                    float silhouetteCoverage = validSpatialSum / max(totalSpatialSum, 0.0001);
                    if (outDepth <= 0.0)
                    {
                        return 0.0;
                    }

                    return float4(outDepth, silhouetteCoverage, 0.0, 1.0);
                }

                if (centerDepth <= 0.0)
                {
                    return 0.0;
                }

                float blurredDepth = BilateralBlurDepth(i.uv, centerDepth);
                if (blurredDepth <= 0.0)
                {
                    return 0.0;
                }

                return float4(blurredDepth, center.g, 0.0, 1.0);
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
            #pragma vertex vertFullscreenTriangle
            #pragma fragment fragComposite
            #pragma target 5.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _FluidThicknessTexture;

            float _NormalScale;
            float4 _FluidColor;
            float _UseParticleColor;
            float _SpecularPower;
            float _SpecularIntensity;
            float _ThicknessAbsorption;

            struct v2f_composite
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f_composite vertFullscreenTriangle(uint vid : SV_VertexID)
            {
                v2f_composite o;
                float2 uv = float2((vid << 1) & 2, vid & 2);
                o.pos = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                o.uv = uv;
                if (_MainTex_TexelSize.y < 0.0)
                {
                    o.uv.y = 1.0 - o.uv.y;
                }

                return o;
            }

            float4 fragComposite(v2f_composite i) : SV_Target
            {
                float4 depthSample = tex2D(_MainTex, i.uv);
                float depth = depthSample.r;
                if (depth <= 0.0)
                {
                    return float4(0.0, 0.0, 0.0, 0.0);
                }

                float smoothedCoverage = depthSample.g;

                float dzdx = ddx(depth) * _NormalScale;
                float dzdy = ddy(depth) * _NormalScale;
                float3 viewNormal = normalize(float3(-dzdx, -dzdy, 1.0));

                float4 thicknessSample = tex2D(_FluidThicknessTexture, i.uv);
                float thickness = thicknessSample.a;
                float3 particleColor = thicknessSample.rgb / max(thickness, 0.0001);
                // Honor the particle's paint color fully when enabled; blending toward
                // _FluidColor tinted every liquid blue and made black paint impossible.
                float3 baseColor = lerp(_FluidColor.rgb, particleColor, saturate(_UseParticleColor));

                // Light and view live in the same +Z-toward-viewer space as viewNormal;
                // an ambient floor keeps flat fluid from going black.
                float3 viewDir = float3(0.0, 0.0, 1.0);
                float3 lightDir = normalize(float3(0.5, 0.7, 1.0));
                float NdotL = max(0.0, dot(viewNormal, lightDir));
                float3 halfVector = normalize(lightDir + viewDir);
                float NdotH = max(0.0, dot(viewNormal, halfVector));
                float specular = pow(NdotH, _SpecularPower) * _SpecularIntensity;
                float fresnel = pow(1.0 - max(0.0, dot(viewNormal, viewDir)), 3.0);

                float3 finalColor = baseColor * (0.35 + 0.65 * NdotL) + specular + (fresnel * 0.3);
                float alpha = saturate(thickness * _ThicknessAbsorption);
                alpha *= smoothstep(0.0, 0.25, smoothedCoverage);

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
            #pragma vertex vertFullscreenTriangleDebug
            #pragma fragment fragDebugDepth
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _FluidDepth;
            float4 _FluidDepth_TexelSize;
            float _MaxEyeDepth;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vertFullscreenTriangleDebug(uint vid : SV_VertexID)
            {
                v2f o;
                float2 uv = float2((vid << 1) & 2, vid & 2);
                o.pos = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                o.uv = uv;
                if (_FluidDepth_TexelSize.y < 0.0)
                {
                    o.uv.y = 1.0 - o.uv.y;
                }

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
