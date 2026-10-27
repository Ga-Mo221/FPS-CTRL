Shader "GaMo/URPOutlinePBR"
{
    Properties
    {
        [Header(Maps)]
        _BaseMap      ("Base Map", 2D) = "white" {}
        _BaseColor    ("Base Color", Color) = (1,1,1,1)

        _MetallicMap  ("Metallic Map (R)", 2D) = "white" {}
        _Metallic     ("Metallic Multiplier", Range(0,1)) = 1

        _RoughnessMap ("Roughness Map (R)", 2D) = "white" {}
        _Roughness    ("Roughness Multiplier", Range(0,1)) = 1

        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale    ("Normal Strength", Range(0,2)) = 1

        [Header(Overall Light)]
        // Nhân sáng toàn bộ model sau khi đã tính ánh sáng
        _Brightness   ("Overall Brightness", Range(0,4)) = 1
        // Tăng phản xạ môi trường ở MỌI điều kiện (không chỉ trong bóng)
        _EnvBoost     ("Environment Reflection Boost", Range(0,4)) = 0

        [Header(Fallback Reflection)]
        // Khi probe/skybox của scene quá tối, shader tự dùng môi trường giả kiểu studio
        _EnvIntensity ("Fallback Intensity", Range(0,4)) = 1
        _EnvSky       ("Fallback Sky Color", Color) = (0.75, 0.85, 1.0, 1)
        _EnvGround    ("Fallback Ground Color", Color) = (0.04, 0.03, 0.025, 1)
        // Càng cao: ranh giới trời/đất càng sắc => kim loại càng "gắt" và rõ
        _EnvContrast  ("Fallback Contrast", Range(0.5,6)) = 3
        // Đốm sáng phản chiếu đèn chính
        _EnvHighlight ("Fallback Sun Highlight", Range(0,16)) = 4

        [Header(Shadow Visibility)]
        _ShadowLift        ("Shadow Brightness Lift", Range(0,1)) = 0.25
        _ShadowMetalBoost  ("Shadow Metallic Reflection", Range(0,4)) = 1.0
        _FillLightStrength ("Fill Light Strength", Range(0,3)) = 0.6
        _FillLightColor    ("Fill Light Color", Color) = (1,1,1,1)
        _FillLightDir      ("Fill Light Direction (View Space)", Vector) = (-0.35, 0.5, 0.8, 0)

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.05)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MetallicMap);  SAMPLER(sampler_MetallicMap);
        TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
        TEXTURE2D(_BumpMap);      SAMPLER(sampler_BumpMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Metallic;
            half   _Roughness;
            half   _BumpScale;
            half   _Brightness;
            half   _EnvBoost;
            half   _EnvIntensity;
            half4  _EnvSky;
            half4  _EnvGround;
            half   _EnvContrast;
            half   _EnvHighlight;
            half   _ShadowLift;
            half   _ShadowMetalBoost;
            half   _FillLightStrength;
            half4  _FillLightColor;
            float4 _FillLightDir;
            half4  _OutlineColor;
            float  _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        // ------------------------------------------------------------
        // PASS 1: OUTLINE (Inverted Hull)
        // ------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual
            Blend Off
            Offset 1, 1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                float4 clipPos  = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 normalCS = TransformWorldToHClipDir(normalWS);

                float2 dir = normalize(normalCS.xy);
                dir.x *= _ScreenParams.y / _ScreenParams.x;
                clipPos.xy += dir * _OutlineWidth * clipPos.w;

                output.positionCS = clipPos;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // PASS 2: FORWARD LIT
        // ------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half4  tangentWS  : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD5;
                #endif
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vi = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   ni = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vi.positionCS;
                output.positionWS = vi.positionWS;
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS   = ni.normalWS;
                output.tangentWS  = half4(ni.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.fogFactor  = ComputeFogFactor(vi.positionCS.z);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    output.shadowCoord = GetShadowCoord(vi);
                #endif

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // ---------- Maps ----------
                half4 baseTex    = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo     = baseTex.rgb * _BaseColor.rgb;
                half  metallic   = saturate(SAMPLE_TEXTURE2D(_MetallicMap,  sampler_MetallicMap,  input.uv).r * _Metallic);
                half  roughness  = saturate(SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, input.uv).r * _Roughness);
                half  smoothness = 1.0h - roughness;

                // ---------- Normal map ----------
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 nWS      = normalize(input.normalWS);
                half3 bitan    = cross(nWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3x3 tbn    = half3x3(input.tangentWS.xyz, bitan, nWS);
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, tbn));

                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // ---------- InputData ----------
                InputData inputData = (InputData)0;
                inputData.positionWS      = input.positionWS;
                inputData.normalWS        = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    inputData.shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                inputData.fogCoord       = input.fogFactor;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI        = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask     = half4(1, 1, 1, 1);

                // ---------- SurfaceData ----------
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = albedo;
                surfaceData.metallic   = metallic;
                surfaceData.specular   = half3(0, 0, 0);
                surfaceData.smoothness = smoothness;
                surfaceData.occlusion  = 1.0h;
                surfaceData.normalTS   = normalTS;
                surfaceData.emission   = half3(0, 0, 0);
                surfaceData.alpha      = 1.0h;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                // ---------- Mức tối (theo đèn chính + bóng đổ) ----------
                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half lit   = saturate(Luminance(mainLight.color) * mainLight.shadowAttenuation
                                      * mainLight.distanceAttenuation * NdotL);
                half dark  = 1.0h - lit;

                // ---------- 1) Nâng sáng diffuse cho phi kim khi trong bóng ----------
                half3 diffuseLift = albedo * (1.0h - metallic) * _ShadowLift * dark;

                // ---------- 2) Phản xạ môi trường: probe + fallback ----------
                half3 reflectVec = reflect(-viewDirWS, normalWS);
                half  mip = PerceptualRoughnessToMipmapLevel(roughness);
                half4 envEnc = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVec, mip);
                half3 envProbe = DecodeHDREnvironment(envEnc, unity_SpecCube0_HDR);

                // Môi trường giả tương phản cao: trời sáng / đất tối / vạch chân trời / đốm đèn chính
                half  ry   = reflectVec.y;
                half  w    = 0.5h / max(_EnvContrast, 0.5h);
                half  tSky = smoothstep(0.5h - w, 0.5h + w, ry * 0.5h + 0.5h);
                half3 fb   = lerp(_EnvGround.rgb, _EnvSky.rgb, tSky);
                fb += (_EnvSky.rgb * 0.5h + 0.5h) * exp(-ry * ry * 40.0h) * 0.6h;

                // Càng nhám thì càng mờ về màu trung bình
                half3 avgEnv = 0.5h * (_EnvSky.rgb + _EnvGround.rgb);
                fb = lerp(fb, avgEnv, roughness * roughness);

                // Phản chiếu đèn chính: nhẵn => đốm nhỏ, sắc; nhám => đốm to, mờ
                float rr     = max(roughness, 0.06h);
                float sunExp = min(2.0 / (rr * rr), 4096.0);
                float sunDot = saturate(dot(reflectVec, mainLight.direction));
                fb += mainLight.color * (half)(pow(sunDot, sunExp)) * _EnvHighlight;

                // Fallback chỉ được dùng khi probe của scene quá tối
                half probeLum = saturate(Luminance(envProbe) * 2.0h);
                half3 fbContribution = fb * _EnvIntensity * (1.0h - probeLum);

                half3 F0 = lerp(half3(0.04h, 0.04h, 0.04h), albedo, metallic);
                half  NdotV = saturate(dot(normalWS, viewDirWS));
                half3 fresnel = F0 + (1.0h - F0) * pow(1.0h - NdotV, 5.0h);
                half  surfaceReduction = 1.0h / (roughness * roughness + 1.0h);

                half3 envBoost = (envProbe * (_EnvBoost + _ShadowMetalBoost * dark) + fbContribution)
                                 * fresnel * surfaceReduction;

                // ---------- 4) Fill light theo camera, chỉ hiện ở vùng tối ----------
                BRDFData brdf;
                half brdfAlpha = 1.0h;
                InitializeBRDFData(albedo, metallic, half3(0, 0, 0), smoothness, brdfAlpha, brdf);

                Light fill = (Light)0;
                half3 dirVS    = normalize(_FillLightDir.xyz);
                fill.direction = normalize(mul((float3x3)UNITY_MATRIX_I_V, dirVS));
                fill.color     = _FillLightColor.rgb * _FillLightStrength;
                fill.distanceAttenuation = 1.0h;
                fill.shadowAttenuation   = 1.0h;

                half3 fillLighting = LightingPhysicallyBased(brdf, fill, normalWS, viewDirWS) * dark;

                // ---------- Tổng hợp ----------
                half3 finalColor = color.rgb + diffuseLift + envBoost + fillLighting;
                finalColor *= _Brightness;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // PASS 3: DEPTH ONLY
        // ------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // PASS 4: DEPTH NORMALS
        // ------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                return half4(normalize(input.normalWS), 0.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // PASS 5: SHADOW CASTER
        // ------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
