Shader "Marchio/EnergyTrail"
{
    Properties
    {
        _CoreColor ("Core Color", Color) = (0.6, 0.9, 1, 1)
        _EdgeColor ("Edge Color", Color) = (0.05, 0.15, 0.9, 1)
        _GlowIntensity ("Glow Intensity", Float) = 2.2
        _NoiseScale ("Noise Scale", Float) = 2.5
        _FlowSpeed ("Flow Speed", Float) = 0.5
        _WarpAmount ("Warp Amount", Float) = 0.8
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.4
        _AlphaPower ("Alpha Power", Float) = 1.6
        _RimColor ("Rim Color", Color) = (0.8, 0.97, 1, 1)
        _RimWidth ("Rim Width", Range(0.01, 0.5)) = 0.12
        _RimIntensity ("Rim Intensity", Float) = 1.8
        _PulseSpeed ("Pulse Speed", Float) = 1.6
        _PulseCount ("Pulse Count", Float) = 4
        _PulseWidth ("Pulse Width", Range(0.02, 1)) = 0.22
        _PulseIntensity ("Pulse Intensity", Float) = 2.2
        _FlickerSpeed ("Flicker Speed", Float) = 20
        _FlickerAmount ("Flicker Amount", Range(0, 1)) = 0.4
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
                float3 positionWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _EdgeColor;
                float _GlowIntensity;
                float _NoiseScale;
                float _FlowSpeed;
                float _WarpAmount;
                float _EdgeSoftness;
                float _AlphaPower;
                float4 _RimColor;
                float _RimWidth;
                float _RimIntensity;
                float _PulseSpeed;
                float _PulseCount;
                float _PulseWidth;
                float _PulseIntensity;
                float _FlickerSpeed;
                float _FlickerAmount;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
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

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    v += amp * valueNoise(p);
                    p *= 2.02;
                    amp *= 0.5;
                }
                return v;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y * _FlowSpeed;
                // gameplay plane is XZ (top-down camera looks down world Y) — sample noise on XZ, not XY
                float2 p = IN.positionWS.xz * _NoiseScale;

                float2 flow1 = float2(t * 0.7, -t * 0.9);
                float2 flow2 = float2(-t * 0.5, t * 0.6);

                float q = fbm(p + flow1);
                float r = fbm(p + q * _WarpAmount + flow2);
                float n = fbm(p + r * _WarpAmount);

                float edge = abs(IN.uv.y - 0.5) * 2.0;
                float edgeMask = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, edge);

                float3 col = lerp(_EdgeColor.rgb, _CoreColor.rgb, saturate(n * 1.3));
                float energy = lerp(0.55, 1.0, pow(saturate(n), _AlphaPower));

                // bright containment boundary, like a shield's outer skin
                float rimOuter = smoothstep(1.0 - _RimWidth, 1.0, edge);
                float rimFade = 1.0 - smoothstep(1.0, 1.0 + _RimWidth * 0.4, edge);
                float rim = rimOuter * rimFade;
                col += _RimColor.rgb * rim * _RimIntensity;

                // traveling pulse of light running along the trail's length (size/glow over "lifetime")
                float pulsePhase = frac(IN.uv.x * _PulseCount - _Time.y * _PulseSpeed);
                float pulseDist = abs(pulsePhase - 0.5) * 2.0;
                float pulse = pow(saturate(1.0 - pulseDist / _PulseWidth), 3.0);
                col += _CoreColor.rgb * pulse * _PulseIntensity * edgeMask;

                // fast noise-driven jitter so it reads as electricity, not a smooth static glow
                float flickerN = valueNoise(float2(IN.uv.x * 30.0 + t * 4.0, _Time.y * _FlickerSpeed));
                float flicker = lerp(1.0 - _FlickerAmount, 1.0 + _FlickerAmount, flickerN);

                float alpha = edgeMask * IN.color.a;
                float3 outColor = col * _GlowIntensity * energy * flicker * alpha;

                return half4(outColor, alpha);
            }
            ENDHLSL
        }
    }
}
