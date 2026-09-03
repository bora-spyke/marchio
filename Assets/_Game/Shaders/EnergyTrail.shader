Shader "Marchio/EnergyTrail"
{
    Properties
    {
        _CoreColor ("Core Color", Color) = (0.9, 0.98, 1, 1)
        _EdgeColor ("Edge Color", Color) = (0.15, 0.55, 1, 1)
        _CoreWidth ("Core Width", Range(0.05, 1)) = 0.4
        _GlowIntensity ("Glow Intensity", Float) = 2.0
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.35
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
                float _CoreWidth;
                float _GlowIntensity;
                float _EdgeSoftness;
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

            half4 frag(Varyings IN) : SV_Target
            {
                float v = abs(IN.uv.y - 0.5) * 2.0;

                // bright core fading out into the edge color
                float core = 1.0 - smoothstep(0.0, _CoreWidth, v);
                float3 col = lerp(_EdgeColor.rgb, _CoreColor.rgb, core);

                float edgeMask = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, v);
                float alpha = edgeMask * IN.color.a;

                float3 outColor = col * _GlowIntensity * alpha;
                return half4(outColor, alpha);
            }
            ENDHLSL
        }
    }
}
