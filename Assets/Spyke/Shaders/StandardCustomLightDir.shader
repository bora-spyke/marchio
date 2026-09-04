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

    // Hand-written vertex/fragment passes. This shader used to be a `#pragma surface` shader, which made
    // Unity generate ForwardBase + ForwardAdd + Deferred + Meta with the full multi_compile_fwdbase /
    // multi_compile_fwdadd_fullshadows keyword sets — 660k variants for a shader that can only ever be
    // rendered one way. The Game scene has exactly one realtime directional light with soft shadows,
    // skybox ambient and the skybox reflection probe — no lightmaps, no baked light probes, no fog, no
    // point/spot lights, forward only. So the passes below run the same math the surface shader ran on
    // that setup (same UNITY_BRDF_PBS, same DiffuseAndSpecularFromMetallic, same SH ambient, same
    // reflection-probe sampling) with the dead paths deleted instead of keyworded. Note that none of the
    // kept features needed a keyword: the variants were pure surface-shader codegen.
    //
    // Variants: forward = instancing(2) x SHADOWS_SCREEN(2), shadow caster = instancing(2).

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        // Property-driven blend state — opaque by default (One/Zero, ZWrite On), or alpha-fade when a
        // material switches its blend factors + render queue. Alpha is _Color.a * _MainTex.a.
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
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0

            // Per-instance tint (see the Props buffer below): blocks share mesh + material and are batched
            // by GPU instancing (Built-in RP has no SRP Batcher).
            #pragma multi_compile_instancing
            // Realtime directional shadows — the only lighting keyword this shader compiles.
            #pragma multi_compile _ SHADOWS_SCREEN

            // Skybox ambient (SH) as a plain define instead of the LIGHTPROBE_SH keyword: it is what gates
            // UNITY_SHOULD_SAMPLE_SH inside ShadeSHPerVertex/ShadeSHPerPixel, and ambient is always wanted,
            // so there is no reason to pay a variant for it.
            #define LIGHTPROBE_SH 1

            #include "UnityCG.cginc"
            #include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            sampler2D _MetallicMap;
            sampler2D _SmoothnessMap;
            sampler2D _BumpMap;
            sampler2D _OcclusionMap;
            sampler2D _EmissionMap;

            // Tiling/offset is applied per map in the fragment shader rather than interpolated per map:
            // 6 extra float2 interpolators would blow the target 3.0 budget, and only a couple of materials
            // use non-default tiling at all.
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
            fixed4  _OcclusionColor;

            float   _SpecularHighlights;
            float   _GlossyReflections;
            float   _OrthoSpecular;
            float   _UseCustomLightDir;
            float   _LightYaw;
            float   _LightPitch;
            fixed4  _CustomLightColor;

            float   _UseCustomLight2;
            float   _LightYaw2;
            float   _LightPitch2;
            fixed4  _CustomLightColor2;

            float   _UseCustomLight3;
            float   _LightYaw3;
            float   _LightPitch3;
            fixed4  _CustomLightColor3;

            float   _UseCustomSpec4;
            float   _SpecYaw4;
            float   _SpecPitch4;
            fixed4  _CustomSpecColor4;

            // Per-instance tint (set per-renderer via MaterialPropertyBlock in KnockOutBlockView.ApplyTint).
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _EmissionColor)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _SpecularColor)
                // In the instancing buffer, not a plain uniform, so a per-instance MaterialPropertyBlock write
                // does not break batching. NOTE: anything written per instance must carry the WHOLE buffer —
                // unsupplied members read as zero, which is what once blacked out untinted blocks.
                UNITY_DEFINE_INSTANCED_PROP(float, _ClipBelowY)
            UNITY_INSTANCING_BUFFER_END(Props)

            // Surface description, filled in the fragment shader before shading. Normal is WORLD space
            // (the surface-shader framework used to do the tangent→world conversion for us).
            struct SurfaceData
            {
                fixed3 Albedo;
                fixed3 Normal;
                half3  Emission;
                half   Metallic;
                half   Smoothness;
                half   Occlusion;
                fixed  Alpha;
                half3  SpecularTint; // per-instance tint from the _SpecularColor instancing buffer
            };

            float3 AnglesToDir(float yawDeg, float pitchDeg)
            {
                float y = yawDeg   * UNITY_PI / 180.0;
                float p = pitchDeg * UNITY_PI / 180.0;
                return float3(cos(p) * sin(y),
                              sin(p),
                              cos(p) * cos(y));
            }

            half4 ShadeCustomStandard(SurfaceData s, half3 viewDir, UnityGI gi)
            {
                if (_SpecularHighlights < 0.5)
                    s.Smoothness = 0;

                // Ortho mode: use the camera's constant view direction for all fragments.
                // UNITY_MATRIX_V row 2 xyz = -cameraForward = surface→camera direction for ortho cameras.
                // Identical for every fragment → no position-dependent specular variation.
                half3 v = _OrthoSpecular > 0.5
                    ? normalize(half3(UNITY_MATRIX_V[2][0], UNITY_MATRIX_V[2][1], UNITY_MATRIX_V[2][2]))
                    : viewDir;

                // Main light, with _SpecularColor injected onto the specular term.
                half mainOneMinusRefl;
                half3 mainSpecColor;
                half3 mainAlbedo = DiffuseAndSpecularFromMetallic(s.Albedo, s.Metallic, /*out*/ mainSpecColor, /*out*/ mainOneMinusRefl);
                half3 instanceSpecularColor = s.SpecularTint;
                mainSpecColor *= instanceSpecularColor;
                half4 result = UNITY_BRDF_PBS(mainAlbedo, mainSpecColor, mainOneMinusRefl, s.Smoothness, s.Normal, v, gi.light, gi.indirect);
                result.a = s.Alpha;

                // Second custom light — additive BRDF contribution, no GI, no shadows
                if (_UseCustomLight2 > 0.5)
                {
                    half oneMinusRefl2;
                    half3 specColor2;
                    half3 diffColor2 = DiffuseAndSpecularFromMetallic(
                        s.Albedo, s.Metallic, /*out*/ specColor2, /*out*/ oneMinusRefl2);
                    specColor2 *= instanceSpecularColor;

                    UnityLight light2;
                    light2.dir   = AnglesToDir(_LightYaw2, _LightPitch2);
                    light2.color = _CustomLightColor2.rgb;
                    light2.ndotl = max(0, dot(s.Normal, light2.dir));

                    UnityIndirect noIndirect2;
                    noIndirect2.diffuse  = 0;
                    noIndirect2.specular = 0;

                    result.rgb += UNITY_BRDF_PBS(
                        diffColor2, specColor2, oneMinusRefl2, s.Smoothness,
                        s.Normal, v, light2, noIndirect2).rgb;
                }

                // Third custom light
                if (_UseCustomLight3 > 0.5)
                {
                    half oneMinusRefl3;
                    half3 specColor3;
                    half3 diffColor3 = DiffuseAndSpecularFromMetallic(
                        s.Albedo, s.Metallic, /*out*/ specColor3, /*out*/ oneMinusRefl3);
                    specColor3 *= instanceSpecularColor;

                    UnityLight light3;
                    light3.dir   = AnglesToDir(_LightYaw3, _LightPitch3);
                    light3.color = _CustomLightColor3.rgb;
                    light3.ndotl = max(0, dot(s.Normal, light3.dir));

                    UnityIndirect noIndirect3;
                    noIndirect3.diffuse  = 0;
                    noIndirect3.specular = 0;

                    result.rgb += UNITY_BRDF_PBS(
                        diffColor3, specColor3, oneMinusRefl3, 0,
                        s.Normal, v, light3, noIndirect3).rgb;
                }

                // Custom Spec 4 — specular-only additive contribution, no diffuse, no GI, no shadows
                if (_UseCustomSpec4 > 0.5)
                {
                    half oneMinusReflSpec4;
                    half3 specColorSpec4;
                    DiffuseAndSpecularFromMetallic(
                        s.Albedo, s.Metallic, /*out*/ specColorSpec4, /*out*/ oneMinusReflSpec4);
                    specColorSpec4 *= instanceSpecularColor;

                    UnityLight spec4;
                    spec4.dir   = AnglesToDir(_SpecYaw4, _SpecPitch4);
                    spec4.color = _CustomSpecColor4.rgb;
                    spec4.ndotl = max(0, dot(s.Normal, spec4.dir));

                    UnityIndirect noIndirectSpec4;
                    noIndirectSpec4.diffuse  = 0;
                    noIndirectSpec4.specular = 0;

                    result.rgb += UNITY_BRDF_PBS(
                        half3(0,0,0), specColorSpec4, oneMinusReflSpec4, s.Smoothness,
                        s.Normal, v, spec4, noIndirectSpec4).rgb;
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
                float4 pos      : SV_POSITION;
                // .xy = _MainTex UV transformed in the vertex shader (matches what the surface shader
                // interpolated, bit for bit, even at extreme tiling); .zw = raw UV0 for the other maps.
                float4 uv       : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                // Rows of the tangent→world matrix. Full float, like the surface shader's tSpace0..2: at
                // half precision the reconstructed normal drifts enough to move specular highlights.
                float3 tspace0  : TEXCOORD2;
                float3 tspace1  : TEXCOORD3;
                float3 tspace2  : TEXCOORD4;
                half3  ambient  : TEXCOORD5; // SH L2 per vertex, L0/L1 finished per pixel
                UNITY_SHADOW_COORDS(6)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.pos      = UnityObjectToClipPos(v.vertex);
                o.uv       = float4(TRANSFORM_TEX(v.uv, _MainTex), v.uv);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                float3 worldNormal  = UnityObjectToWorldNormal(v.normal);
                float3 worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                float  tangentSign  = v.tangent.w * unity_WorldTransformParams.w;
                float3 worldBinorm  = cross(worldNormal, worldTangent) * tangentSign;
                o.tspace0 = float3(worldTangent.x, worldBinorm.x, worldNormal.x);
                o.tspace1 = float3(worldTangent.y, worldBinorm.y, worldNormal.y);
                o.tspace2 = float3(worldTangent.z, worldBinorm.z, worldNormal.z);

                o.ambient = ShadeSHPerVertex(worldNormal, half3(0, 0, 0));

                UNITY_TRANSFER_SHADOW(o, v.uv);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // World-Y clip mask (#283). Hides a generator wave while it is still under the platform slab.
                // v2f already carries worldPos for the lighting, so this costs no extra interpolator. Default
                // -99999 => always positive => never discards, so every other material is unaffected.
                clip(i.worldPos.y - UNITY_ACCESS_INSTANCED_PROP(Props, _ClipBelowY));

                // --- surface (was surf()) ---
                SurfaceData s;

                fixed4 c = tex2D(_MainTex, i.uv.xy) * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                s.Albedo = c.rgb;
                s.Alpha  = c.a;

                fixed4 mm    = tex2D(_MetallicMap,   i.uv.zw * _MetallicMap_ST.xy   + _MetallicMap_ST.zw);
                fixed4 sm    = tex2D(_SmoothnessMap, i.uv.zw * _SmoothnessMap_ST.xy + _SmoothnessMap_ST.zw);
                s.Metallic   = mm.r * _Metallic;
                s.Smoothness = sm.r * _Glossiness;

                half3 tangentNormal = UnpackScaleNormal(
                    tex2D(_BumpMap, i.uv.zw * _BumpMap_ST.xy + _BumpMap_ST.zw), _BumpScale);
                s.Normal = normalize(float3(dot(i.tspace0, tangentNormal),
                                            dot(i.tspace1, tangentNormal),
                                            dot(i.tspace2, tangentNormal)));

                half occ    = tex2D(_OcclusionMap, i.uv.zw * _OcclusionMap_ST.xy + _OcclusionMap_ST.zw).g;
                s.Occlusion = LerpOneTo(occ, _OcclusionStrength);

                s.Emission = tex2D(_EmissionMap, i.uv.zw * _EmissionMap_ST.xy + _EmissionMap_ST.zw).rgb
                           * UNITY_ACCESS_INSTANCED_PROP(Props, _EmissionColor).rgb;
                s.SpecularTint = UNITY_ACCESS_INSTANCED_PROP(Props, _SpecularColor).rgb;

                // --- lighting inputs (was LightingCustomStandard_GI + the framework's GI setup) ---
                half3 worldViewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);

                UnityGI gi;
                ResetUnityGI(gi);

                gi.light.dir   = _WorldSpaceLightPos0.xyz;
                gi.light.color = _LightColor0.rgb * atten;
                if (_UseCustomLightDir > 0.5)
                {
                    // Custom direction, still attenuated by the scene light's shadows.
                    gi.light.dir   = AnglesToDir(_LightYaw, _LightPitch);
                    gi.light.color = _CustomLightColor.rgb * atten;
                }
                gi.light.ndotl = max(0, dot(s.Normal, gi.light.dir));

                // Indirect diffuse = skybox ambient SH with colored occlusion (Unity's own GI applies a
                // grayscale occlusion; this replaces it with a tintable one).
                gi.indirect.diffuse = ShadeSHPerPixel(s.Normal, i.ambient, i.worldPos)
                                    * lerp(_OcclusionColor.rgb, half3(1, 1, 1), s.Occlusion);

                // Indirect specular = the reflection probe (the skybox probe when a scene has none of its
                // own). This is the same cubemap sampling LightingStandard_GI did, and it costs no
                // variants: UNITY_SPECCUBE_* are platform config defines, not shader keywords.
                Unity_GlossyEnvironmentData glossIn = UnityGlossyEnvironmentSetup(
                    s.Smoothness, worldViewDir, s.Normal,
                    lerp(unity_ColorSpaceDielectricSpec.rgb, s.Albedo, s.Metallic));

                UnityGIInput giInput;
                UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
                giInput.worldPos     = i.worldPos;
                giInput.worldViewDir = worldViewDir;
                giInput.probeHDR[0]  = unity_SpecCube0_HDR;
                giInput.probeHDR[1]  = unity_SpecCube1_HDR;
                #if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
                    giInput.boxMin[0] = unity_SpecCube0_BoxMin;
                #endif
                #ifdef UNITY_SPECCUBE_BOX_PROJECTION
                    giInput.boxMax[0]        = unity_SpecCube0_BoxMax;
                    giInput.probePosition[0] = unity_SpecCube0_ProbePosition;
                    giInput.boxMin[1]        = unity_SpecCube1_BoxMin;
                    giInput.boxMax[1]        = unity_SpecCube1_BoxMax;
                    giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
                #endif

                gi.indirect.specular = UnityGI_IndirectSpecular(giInput, s.Occlusion, glossIn)
                                     * _GlossyReflections;

                half4 col = ShadeCustomStandard(s, worldViewDir, gi);
                col.rgb += s.Emission;
                return col;
            }
            ENDCG
        }

        // Casts into the directional light's shadow map (and into the camera depth texture). No keywords:
        // SHADOWS_CUBE (point-light shadows) can't happen here, and the fade materials cast opaque shadows
        // exactly as they did under the surface shader.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            // Override the SubShader's property-driven state: a shadow caster must write depth and must not
            // blend. Without this, every fade material (_ZWrite = 0) silently stops casting shadows — the
            // surface-shader codegen emitted its own state here for the same reason.
            ZWrite On
            ZTest LEqual
            Blend Off

            CGPROGRAM
            #pragma vertex   vertShadow
            #pragma fragment fragShadow
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            // The FORWARD pass's Props buffer, member for member. The clip plane is a per-instance property,
            // so this pass has to declare it too — and it declares the WHOLE buffer so the instancing layout
            // and per-batch instance count stay identical to FORWARD's.
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _EmissionColor)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _SpecularColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _ClipBelowY)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct v2fShadow
            {
                V2F_SHADOW_CASTER;
                float3 worldPos : TEXCOORD1; // only for the world-Y clip mask
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2fShadow vertShadow(appdata_base v)
            {
                v2fShadow o;
                UNITY_INITIALIZE_OUTPUT(v2fShadow, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                // Unbiased world position — TRANSFER_SHADOW_CASTER_NORMALOFFSET pushes the vertex along its
                // normal for the depth it writes, and the mask must cut at the same world Y FORWARD cuts at.
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 fragShadow(v2fShadow i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Same world-Y clip mask as FORWARD (#283): without it, the part of a rising generator
                // formation still hidden under the platform slab keeps casting its shadow.
                clip(i.worldPos.y - UNITY_ACCESS_INSTANCED_PROP(Props, _ClipBelowY));

                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}
