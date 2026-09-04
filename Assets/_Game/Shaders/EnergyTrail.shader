Shader "Marchio/EnergyTrail"
{
    Properties
    {
        _CoreColor ("Core Color", Color) = (0.9, 0.98, 1, 1)
        _EdgeColor ("Edge Color", Color) = (0.15, 0.55, 1, 1)
        _GradientPower ("Core Gradient Power", Range(0.2, 6)) = 2.0
        _GlowIntensity ("Glow Intensity", Float) = 2.0
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.35

        _NoiseScale ("Noise Scale", Float) = 4.0
        _NoiseSpeed ("Noise Speed", Float) = 0.8
        _NoiseAmount ("Noise Wobble Amount", Range(0, 0.5)) = 0.12
        _ShimmerAmount ("Shimmer Amount", Range(0, 1)) = 0.15

        _DangerColor ("Danger Color", Color) = (1, 0.15, 0.1, 1)
        _DangerThreshold ("Danger Start (0-1 of lifetime)", Range(0, 1)) = 0.75
        _DangerT ("Danger T (set from script)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _EdgeColor;
                float _GradientPower;
                float _GlowIntensity;
                float _EdgeSoftness;
                float _NoiseScale;
                float _NoiseSpeed;
                float _NoiseAmount;
                float _ShimmerAmount;
                float4 _DangerColor;
                float _DangerThreshold;
                float _DangerT;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y * _NoiseSpeed;

                // the bright core gently drifts side to side along the length, instead of sitting dead still
                float wobbleN = valueNoise(float2(IN.uv.x * _NoiseScale + t, 3.1));
                float centerOffset = (wobbleN - 0.5) * _NoiseAmount;

                float vSigned = (IN.uv.y - 0.5) - centerOffset;
                float v = saturate(abs(vSigned) * 2.0);

                // continuous gradient from the core out to the edge (no flat plateau)
                float core = pow(saturate(1.0 - v), _GradientPower);
                float3 col = lerp(_EdgeColor.rgb, _CoreColor.rgb, core);

                // whole line reddens together once past the threshold, full red near the length limit
                float dangerAmt = smoothstep(_DangerThreshold, 1.0, _DangerT);
                col = lerp(col, _DangerColor.rgb, dangerAmt);

                // slow brightness shimmer so the energy feels alive, not a static print
                float shimmerN = valueNoise(float2(IN.uv.x * _NoiseScale * 0.6 - t * 1.3, 9.7));
                float shimmer = lerp(1.0 - _ShimmerAmount, 1.0 + _ShimmerAmount, shimmerN);

                float edgeMask = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, v);
                float alpha = edgeMask * IN.color.a;

                float3 outColor = col * _GlowIntensity * shimmer * alpha;
                return half4(outColor, alpha);
            }
            ENDHLSL
        }
    }
}
