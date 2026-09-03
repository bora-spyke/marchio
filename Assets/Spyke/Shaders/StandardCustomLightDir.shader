Shader "Spyke/StandardCustomLightDir"
{
    Properties
    {
        // --- Albedo ---
        _Color          ("Color", Color)                        = (1,1,1,1)
        _MainTex        ("Albedo (RGB)", 2D)                    = "white" {}

        // --- World-Y clip mask (generator platform, Trello #283) ---
        // Fragments below this WORLD Y are discarded. The default is a sentinel far below any level, so every
        // existing material is unaffected. A generator wave sets it per instance to the table's world Y while
        // it rises, which masks the part of the formation still under the slab.
        _ClipBelowY     ("Clip Below World Y", Float)           = -99999

        // --- Metallic / Smoothness ---
        _Metallic       ("Metallic", Range(0,1))                = 0.0
        _MetallicMap    ("Metallic (RGB)", 2D)                  = "white" {}
        _SmoothnessMap  ("Smoothness (RGB)", 2D)                = "white" {}
        _Glossiness     ("Smoothness", Range(0,1))              = 0.5

        // --- Normal Map ---
        _BumpScale      ("Normal Scale", Float)                 = 1.0
        [Normal] _BumpMap ("Normal Map", 2D)                   = "bump" {}

        // --- Occlusion ---
        _OcclusionStrength ("Occlusion Strength", Range(0,1))  = 1.0
        _OcclusionMap   ("Occlusion", 2D)                      = "white" {}
        _OcclusionColor ("Occlusion Color", Color)             = (0,0,0,1)

        // --- Emission ---
        [HDR] _EmissionColor ("Emission Color", Color)         = (0,0,0,1)
        _EmissionMap    ("Emission", 2D)                       = "black" {}

        // --- Alpha Fade (render state) ---
        // Defaults keep the material OPAQUE (One/Zero + ZWrite On), identical to before. To fade, switch a
        // material to SrcAlpha / OneMinusSrcAlpha + ZWrite Off and set its render queue to Transparent (3000);
        // alpha then comes from _Color.a (drive it per-instance via a MaterialPropertyBlock to fade out).
        [Header(Alpha Fade)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1   // One
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0   // Zero
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1

        // --- Forward Rendering Options ---
        [Header(Forward Rendering Options)]
        [Toggle] _SpecularHighlights ("Specular Highlights", Float) = 1
        _GlossyReflections  ("Reflections", Range(0,1))             = 1
        [Toggle] _OrthoSpecular ("Ortho Specular (position-independent)", Float) = 0
        [HDR] _SpecularColor ("Specular Color", Color) = (1,1,1,1)

        // --- Custom Light 1 ---
        [Header(Custom Light 1)]
        [Toggle] _UseCustomLightDir ("Enable Light 1", Float) = 0
        // Yaw  : 0 = +Z (forward), 90 = +X (right), 180 = -Z (back), 270 = -X (left)
        // Pitch: 0 = horizon, 90 = straight up, -90 = straight down
        _LightYaw   ("Yaw   (Horizontal 0-360)", Range(0, 360))  = 0
        _LightPitch ("Pitch (Vertical -90..90)", Range(-90, 90)) = 45
        [HDR] _CustomLightColor ("Light Color", Color)         = (1,1,1,1)

        // --- Custom Light 2 ---
        [Header(Custom Light 2)]
        [Toggle] _UseCustomLight2 ("Enable Light 2", Float) = 0
        _LightYaw2   ("Yaw   (Horizontal 0-360)", Range(0, 360))  = 180
        _LightPitch2 ("Pitch (Vertical -90..90)", Range(-90, 90)) = 45
        [HDR] _CustomLightColor2 ("Light Color", Color)        = (1,1,1,1)

        // --- Custom Light 3 ---
        [Header(Custom Light 3)]
        [Toggle] _UseCustomLight3 ("Enable Light 3", Float) = 0
        _LightYaw3   ("Yaw   (Horizontal 0-360)", Range(0, 360))  = 270
        _LightPitch3 ("Pitch (Vertical -90..90)", Range(-90, 90)) = 45
        [HDR] _CustomLightColor3 ("Light Color", Color)        = (1,1,1,1)

        // --- Custom Spec 4 ---
        [Header(Custom Spec 4)]
        [Toggle] _UseCustomSpec4 ("Enable Spec 4", Float) = 0
        _SpecYaw4   ("Yaw   (Horizontal 0-360)", Range(0, 360))  = 180
        _SpecPitch4 ("Pitch (Vertical -90..90)", Range(-90, 90)) = 45
        [HDR] _CustomSpecColor4 ("Spec Color", Color)          = (1,1,1,1)

        _StencilRef ("Stencil Ref", Int) = 1
    }

    // Ported from the Built-in Render Pipeline to URP (see git history for the CGPROGRAM/surf() original).
    // Same feature set: one directional main light (URP's GetMainLight, soft realtime shadows), skybox SH
    // ambient, skybox reflection probe (no probe blending/box projection — this project uses only the sky
    // probe), plus the same 3 additive fake lights + fake specular light the original had. The PBR shading
    // itself is a hand-rolled minimalist Cook-Torrance BRDF (same family as Unity's own mobile BRDF) instead
    // of URP's BRDFData/LightingPhysicallyBased, so this file has no dependency on URP-internal API surface
    // that could shift between package versions.
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Stencil
        {
            Ref [_StencilRef]
            Comp Always
            Pass Replace
        }

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
            TEXTURE2D(_MetallicMap);   SAMPLER(sampler_MetallicMap);
            TEXTURE2D(_SmoothnessMap); SAMPLER(sampler_SmoothnessMap);
            TEXTURE2D(_BumpMap);       SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OcclusionMap);  SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap);   SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MetallicMap_ST;
                float4 _SmoothnessMap_ST;
                float4 _BumpMap_ST;
                float4 _OcclusionMap_ST;
                float4 _EmissionMap_ST;

                half    _Metallic;
                half    _Glossiness;
                half    _BumpScale;
                half    _OcclusionStrength;
                half4   _OcclusionColor;

                float   _SpecularHighlights;
                float   _GlossyReflections;
                float   _OrthoSpecular;
                float   _UseCustomLightDir;
                float   _LightYaw;
                float   _LightPitch;
                half4   _CustomLightColor;

                float   _UseCustomLight2;
                float   _LightYaw2;
                float   _LightPitch2;
                half4   _CustomLightColor2;

                float   _UseCustomLight3;
                float   _LightYaw3;
                float   _LightPitch3;
                half4   _CustomLightColor3;

                float   _UseCustomSpec4;
                float   _SpecYaw4;
                float   _SpecPitch4;
                half4   _CustomSpecColor4;
            CBUFFER_END

            // Per-instance tint: blocks share mesh + material and are batched by GPU instancing.
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half4, _EmissionColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _SpecularColor)
                // In the instancing buffer, not a plain uniform, so a per-instance MaterialPropertyBlock write
                // does not break batching. NOTE: anything written per instance must carry the WHOLE buffer —
                // unsupplied members read as zero, which is what once blacked out untinted blocks.
                UNITY_DEFINE_INSTANCED_PROP(float, _ClipBelowY)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct CustomSurfaceData
            {
                half3 Albedo;
                half3 Normal;
                half3 Emission;
                half  Metallic;
                half  Smoothness;
                half  Occlusion;
                half  Alpha;
                half3 SpecularTint; // per-instance tint from the _SpecularColor instancing buffer
            };

            float3 AnglesToDir(float yawDeg, float pitchDeg)
            {
                float y = yawDeg   * PI / 180.0;
                float p = pitchDeg * PI / 180.0;
                return float3(cos(p) * sin(y),
                              sin(p),
                              cos(p) * cos(y));
            }

            half3 DiffuseAndSpecularFromMetallic(half3 albedo, half metallic, out half3 specColor, out half oneMinusReflectivity)
            {
                half3 kDielectricSpec = half3(0.04, 0.04, 0.04);
                specColor = lerp(kDielectricSpec, albedo, metallic);
                oneMinusReflectivity = (1.0 - 0.04) * (1.0 - metallic);
                return albedo * oneMinusReflectivity;
            }

            // Minimalist Cook-Torrance term — same family as Unity's own mobile BRDF approximation, hand-rolled
            // so this shader does not depend on URP's internal BRDFData/LightingPhysicallyBased API surface.
            half3 BRDF(half3 diffColor, half3 specColor, half smoothness,
                       half3 normalWS, half3 viewDirWS, half3 lightDirWS, half3 lightColor, half nl)
            {
                half3 halfDir = normalize(lightDirWS + viewDirWS);
                half nh = saturate(dot(normalWS, halfDir));
                half lh = saturate(dot(lightDirWS, halfDir));

                half roughness = max(1.0 - smoothness, 0.002);
                half a2 = roughness * roughness * roughness * roughness;

                half d = nh * nh * (a2 - 1.0) + 1.00001;
                half specularTerm = a2 / (max(0.1, lh * lh) * (roughness + 0.5) * (d * d) * 4.0);
                specularTerm = max(0, specularTerm);

                return (diffColor + specularTerm * specColor) * lightColor * nl;
            }

            half3 SampleReflectionProbe(half3 reflectVectorWS, half perceptualRoughness)
            {
                half mip = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness) * 6.0;
                half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVectorWS, mip);
                return DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);
            }

            half3 ShadeCustomStandard(CustomSurfaceData s, half3 viewDir,
                                       half3 mainLightDir, half3 mainLightColor, half mainNdotL,
                                       half3 indirectDiffuse, half3 indirectSpecular)
            {
                // Ortho mode: use the camera's constant view direction for all fragments.
                // UNITY_MATRIX_V row 2 xyz = -cameraForward = surface->camera direction for ortho cameras.
                // Identical for every fragment -> no position-dependent specular variation.
                half3 v = _OrthoSpecular > 0.5
                    ? normalize(half3(UNITY_MATRIX_V[2][0], UNITY_MATRIX_V[2][1], UNITY_MATRIX_V[2][2]))
                    : viewDir;

                half smoothness = _SpecularHighlights < 0.5 ? 0 : s.Smoothness;

                half3 specColor;
                half oneMinusRefl;
                half3 diffColor = DiffuseAndSpecularFromMetallic(s.Albedo, s.Metallic, /*out*/ specColor, /*out*/ oneMinusRefl);
                specColor *= s.SpecularTint;

                half3 result = diffColor * indirectDiffuse;
                result += specColor * indirectSpecular;
                result += BRDF(diffColor, specColor, smoothness, s.Normal, v, mainLightDir, mainLightColor, mainNdotL);

                // Second custom light — additive BRDF contribution, no GI, no shadows
                if (_UseCustomLight2 > 0.5)
                {
                    half3 dir2 = AnglesToDir(_LightYaw2, _LightPitch2);
                    half nl2 = max(0, dot(s.Normal, dir2));
                    result += BRDF(diffColor, specColor, smoothness, s.Normal, v, dir2, _CustomLightColor2.rgb, nl2);
                }

                // Third custom light
                if (_UseCustomLight3 > 0.5)
                {
                    half3 dir3 = AnglesToDir(_LightYaw3, _LightPitch3);
                    half nl3 = max(0, dot(s.Normal, dir3));
                    result += BRDF(diffColor, specColor, 0, s.Normal, v, dir3, _CustomLightColor3.rgb, nl3);
                }

                // Custom Spec 4 — specular-only additive contribution, no diffuse, no GI, no shadows
                if (_UseCustomSpec4 > 0.5)
                {
                    half3 dir4 = AnglesToDir(_SpecYaw4, _SpecPitch4);
                    half nl4 = max(0, dot(s.Normal, dir4));
                    result += BRDF(half3(0, 0, 0), specColor, smoothness, s.Normal, v, dir4, _CustomSpecColor4.rgb, nl4);
                }

                return result;
            }

            struct appdata
            {
                float4 vertex  : POSITION;
                float3 normal  : NORMAL;
                float4 tangent : TANGENT;
                float2 uv      : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos         : SV_POSITION;
                // .xy = _MainTex UV transformed in the vertex shader; .zw = raw UV0 for the other maps.
                float4 uv          : TEXCOORD0;
                float3 worldPos    : TEXCOORD1;
                float3 tspace0     : TEXCOORD2;
                float3 tspace1     : TEXCOORD3;
                float3 tspace2     : TEXCOORD4;
                half3  ambient     : TEXCOORD5;
                float4 shadowCoord : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                VertexPositionInputs posInputs = GetVertexPositionInputs(v.vertex.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(v.normal, v.tangent);

                o.pos = posInputs.positionCS;
                o.worldPos = posInputs.positionWS;
                o.uv = float4(TRANSFORM_TEX(v.uv, _MainTex), v.uv);

                o.tspace0 = float3(normInputs.tangentWS.x, normInputs.bitangentWS.x, normInputs.normalWS.x);
                o.tspace1 = float3(normInputs.tangentWS.y, normInputs.bitangentWS.y, normInputs.normalWS.y);
                o.tspace2 = float3(normInputs.tangentWS.z, normInputs.bitangentWS.z, normInputs.normalWS.z);

                o.ambient = SampleSH(normInputs.normalWS);
                o.shadowCoord = TransformWorldToShadowCoord(posInputs.positionWS);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // World-Y clip mask (#283). Hides a generator wave while it is still under the platform slab.
                clip(i.worldPos.y - UNITY_ACCESS_INSTANCED_PROP(Props, _ClipBelowY));

                CustomSurfaceData s;

                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy) * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                s.Albedo = c.rgb;
                s.Alpha  = c.a;

                half4 mm = SAMPLE_TEXTURE2D(_MetallicMap,   sampler_MetallicMap,   i.uv.zw * _MetallicMap_ST.xy   + _MetallicMap_ST.zw);
                half4 sm = SAMPLE_TEXTURE2D(_SmoothnessMap, sampler_SmoothnessMap, i.uv.zw * _SmoothnessMap_ST.xy + _SmoothnessMap_ST.zw);
                s.Metallic   = mm.r * _Metallic;
                s.Smoothness = sm.r * _Glossiness;

                half4 packedNormal = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv.zw * _BumpMap_ST.xy + _BumpMap_ST.zw);
                half3 tangentNormal = UnpackNormalScale(packedNormal, _BumpScale);
                s.Normal = normalize(half3(dot(i.tspace0, tangentNormal),
                                            dot(i.tspace1, tangentNormal),
                                            dot(i.tspace2, tangentNormal)));

                half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, i.uv.zw * _OcclusionMap_ST.xy + _OcclusionMap_ST.zw).g;
                s.Occlusion = lerp(1.0, occ, _OcclusionStrength);

                s.Emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv.zw * _EmissionMap_ST.xy + _EmissionMap_ST.zw).rgb
                           * UNITY_ACCESS_INSTANCED_PROP(Props, _EmissionColor).rgb;
                s.SpecularTint = UNITY_ACCESS_INSTANCED_PROP(Props, _SpecularColor).rgb;

                half3 viewDirWS = normalize(GetWorldSpaceViewDir(i.worldPos));

                Light mainLight = GetMainLight(i.shadowCoord);
                half3 lightDir   = mainLight.direction;
                half3 lightColor = mainLight.color * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                if (_UseCustomLightDir > 0.5)
                {
                    // Custom direction, still attenuated by the scene light's shadows.
                    lightDir   = AnglesToDir(_LightYaw, _LightPitch);
                    lightColor = _CustomLightColor.rgb * mainLight.shadowAttenuation;
                }
                half nl = max(0, dot(s.Normal, lightDir));

                // Indirect diffuse = skybox ambient SH with colored occlusion (replaces Unity's grayscale
                // occlusion with a tintable one).
                half3 indirectDiffuse = i.ambient * lerp(_OcclusionColor.rgb, half3(1, 1, 1), s.Occlusion);

                // Indirect specular = the skybox reflection probe.
                half3 reflectVector = reflect(-viewDirWS, s.Normal);
                half perceptualRoughness = 1.0 - s.Smoothness;
                half3 indirectSpecular = SampleReflectionProbe(reflectVector, perceptualRoughness) * _GlossyReflections * s.Occlusion;

                half3 col = ShadeCustomStandard(s, viewDirWS, lightDir, lightColor, nl, indirectDiffuse, indirectSpecular);
                col += s.Emission;

                return half4(col, s.Alpha);
            }
            ENDHLSL
        }

        // Casts into the directional light's shadow map (and into the camera depth texture). No keywords:
        // point-light shadows can't happen here, and fade materials cast opaque shadows exactly as before.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            // Override the SubShader's property-driven state: a shadow caster must write depth and must not
            // blend. Without this, every fade material (_ZWrite = 0) silently stops casting shadows.
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // The FORWARD pass's Props buffer, member for member. The clip plane is a per-instance property,
            // so this pass has to declare it too — and it declares the WHOLE buffer so the instancing layout
            // and per-batch instance count stay identical to FORWARD's.
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half4, _EmissionColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _SpecularColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _ClipBelowY)
            UNITY_INSTANCING_BUFFER_END(Props)

            float3 _LightDirection; // set by URP's shadow-caster render pass

            struct appdataShadow
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2fShadow
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0; // only for the world-Y clip mask
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2fShadow vertShadow(appdataShadow v)
            {
                v2fShadow o;
                UNITY_INITIALIZE_OUTPUT(v2fShadow, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 positionWS = TransformObjectToWorld(v.vertex.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normal);

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                o.pos = positionCS;
                // Unbiased world position: the mask must cut at the same world Y FORWARD cuts at.
                o.worldPos = positionWS;
                return o;
            }

            half4 fragShadow(v2fShadow i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Same world-Y clip mask as FORWARD (#283): without it, the part of a rising generator
                // formation still hidden under the platform slab keeps casting its shadow.
                clip(i.worldPos.y - UNITY_ACCESS_INSTANCED_PROP(Props, _ClipBelowY));

                return 0;
            }
            ENDHLSL
        }
    }
}
