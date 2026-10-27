Shader "GaMo/Dark"
{
    Properties
    {
        [Header(Maps)]
        _BaseMap ("Base Map (Albedo)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        _MetallicMap ("Metallic Map (R)", 2D) = "white" {}
        _Metallic ("Metallic Multiplier", Range(0, 1)) = 1

        _RoughnessMap ("Roughness Map (R)", 2D) = "white" {}
        _Roughness ("Roughness Multiplier", Range(0, 1)) = 1

        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1

        [Header(Shadow Control)]
        // 1 = bóng đổ đậm như mặc định, 0 = không có bóng đổ
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.5
        // Màu/độ sáng bù vào vùng bóng râm (nhân với màu base + metal)
        _ShadowFill ("Shadow Fill Color", Color) = (0.35, 0.35, 0.4, 1)
        // Tăng phản xạ môi trường trong bóng râm để kim loại không bị đen
        _MetalShadowReflection ("Metal Reflection In Shadow", Range(0, 4)) = 1.5

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

        // =====================================================
        // PASS 1: OUTLINE (Inverted Hull)
        // =====================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            // CBUFFER phải giống hệt ở mọi pass để giữ SRP Batcher
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _Metallic;
                half   _Roughness;
                half   _BumpScale;
                half   _ShadowStrength;
                half4  _ShadowFill;
                half   _MetalShadowReflection;
                half4  _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                float4 clipPos = TransformObjectToHClip(input.positionOS.xyz);

                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 normalCS = TransformWorldToHClipDir(normalWS);

                clipPos.xy += normalize(normalCS.xy) * _OutlineWidth * clipPos.w;

                output.positionCS = clipPos;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // =====================================================
        // PASS 2: PBR LIT (Base + Metallic + Roughness + Normal)
        // =====================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
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
                half4  tangentWS  : TEXCOORD3; // w = dấu bitangent
            };

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
                half   _ShadowStrength;
                half4  _ShadowFill;
                half   _MetalShadowReflection;
                half4  _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = nrmInputs.normalWS;
                output.tangentWS = half4(nrmInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                return output;
            }

            // Phản xạ môi trường (reflection probe / skybox)
            half3 SampleEnvironment(half3 reflectVec, half perceptualRoughness)
            {
                half mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
                half4 encoded = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVec, mip);
                return DecodeHDREnvironment(encoded, unity_SpecCube0_HDR);
            }

            // Ánh sáng trực tiếp, bóng đổ đã được làm nhạt theo _ShadowStrength
            half3 DirectLight(BRDFData brdf, Light light, half3 normalWS, half3 viewDirWS)
            {
                light.shadowAttenuation = lerp(1.0h, light.shadowAttenuation, _ShadowStrength);
                return LightingPhysicallyBased(brdf, light, normalWS, viewDirWS);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // ---- Sample 4 map ----
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo  = baseTex.rgb * _BaseColor.rgb;
                half  alpha   = baseTex.a * _BaseColor.a;

                half metallic = saturate(SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, input.uv).r * _Metallic);
                half roughness = saturate(SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, input.uv).r * _Roughness);
                half smoothness = 1.0h - roughness;

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3x3 tbn = half3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                half3 normalWS = normalize(mul(normalTS, tbn));

                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                // ---- BRDF (metal: diffuse ~ 0, specular = màu albedo) ----
                BRDFData brdf;
                InitializeBRDFData(albedo, metallic, half3(0, 0, 0), smoothness, alpha, brdf);

                // ---- Ánh sáng chính + bóng ----
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 color = DirectLight(brdf, mainLight, normalWS, viewDirWS);

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; ++i)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    color += DirectLight(brdf, addLight, normalWS, viewDirWS);
                }
                #endif

                // ---- Ánh sáng gián tiếp (không bị ảnh hưởng bởi shadow) ----
                half3 bakedGI = SampleSH(normalWS);
                half3 indirectDiffuse = bakedGI * brdf.diffuse;

                half3 reflectVec = reflect(-viewDirWS, normalWS);
                half fresnel = Pow4(1.0h - saturate(dot(normalWS, viewDirWS)));
                half3 envSpec = SampleEnvironment(reflectVec, brdf.perceptualRoughness)
                                * EnvironmentBRDFSpecular(brdf, fresnel);

                // ---- Giữ metallic trong bóng râm ----
                // shade: 0 = đang được chiếu sáng, 1 = ngập trong bóng
                half shade = 1.0h - saturate(mainLight.shadowAttenuation);
                shade *= _ShadowStrength;

                // 1) Tăng phản xạ môi trường cho kim loại khi vào bóng
                envSpec *= 1.0h + shade * metallic * _MetalShadowReflection;

                // 2) Bù sáng nhẹ theo màu vật liệu (diffuse + specular color) để vùng bóng không đen
                half3 shadowFill = _ShadowFill.rgb * (brdf.diffuse + brdf.specular) * shade;

                color += indirectDiffuse + envSpec + shadowFill;

                return half4(color, alpha);
            }
            ENDHLSL
        }

        // =====================================================
        // Đổ bóng + depth (dùng pass có sẵn của URP/Lit)
        // =====================================================
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
