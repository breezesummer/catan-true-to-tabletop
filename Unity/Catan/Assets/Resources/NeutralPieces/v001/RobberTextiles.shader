Shader "Catan/强盗团织物与肖像"
{
    Properties
    {
        [MainTexture] _BaseMap("染色与肤色", 2D) = "white" {}
        [MainColor] _BaseColor("底色", Color) = (1,1,1,1)
        [Normal] _BumpMap("纤维与皮纹", 2D) = "bump" {}
        [NoScaleOffset] _FibreMap("可平铺的纱线与绒毛", 2D) = "gray" {}
        _BumpScale("纹理起伏", Range(0,2)) = 1
        _MetallicGlossMap("金属度、遮蔽、绒面、光滑度", 2D) = "white" {}
        _Smoothness("光滑度倍率", Range(0,1)) = 1
        _OcclusionStrength("局部遮蔽", Range(0,1)) = .84
        [HideInInspector] _OcclusionMap("同图集遮蔽", 2D) = "white" {}
        [HideInInspector] _Cull("剔除", Float) = 0
        [HideInInspector] _Surface("表面", Float) = 0
        [HideInInspector] _Cutoff("透明裁切", Float) = .5
        [HideInInspector] _ReceiveShadows("接收阴影", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ClothVertex
            #pragma fragment ClothFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #define _SPECULAR_SETUP 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            TEXTURE2D(_FibreMap);
            SAMPLER(sampler_FibreMap);

            struct ClothVaryings
            {
                Varyings lit;
                float2 fibreUV : TEXCOORD4;
            };

            ClothVaryings ClothVertex(Attributes vertex)
            {
                ClothVaryings result;
                result.lit = LitPassVertex(vertex);
                result.fibreUV = vertex.staticLightmapUV;
                return result;
            }

            half4 ClothFragment(ClothVaryings pixel) : SV_Target
            {
                Varyings input = pixel.lit;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 dye = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).rgb * _BaseColor.rgb;
                half4 finish = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                half nap = finish.b;
                half4 fibres = SAMPLE_TEXTURE2D(_FibreMap, sampler_FibreMap, pixel.fibreUV);
                half thread = lerp(fibres.r, fibres.g, smoothstep(.37h,.43h,nap));
                thread = lerp(thread, fibres.b, smoothstep(.60h,.66h,nap));
                thread = lerp(thread, fibres.a, smoothstep(.86h,.92h,nap));
                thread = (thread-.5h) * smoothstep(.08h,.15h,nap);
                dye *= 1 + .83h * thread;
                half dielectric = lerp(kDielectricSpec.r, 0.010h, nap);
                SurfaceData surface = (SurfaceData)0;
                surface.alpha = 1;
                surface.specular = lerp(dielectric.xxx, dye, finish.r);
                half diffuseShare = (1 - dielectric) * (1 - finish.r);
                surface.albedo = dye * (diffuseShare / max(0.001h, 1 - ReflectivitySpecular(surface.specular)));
                surface.smoothness = finish.a * _Smoothness;
                surface.occlusion = lerp(1.0h, finish.g, _OcclusionStrength);
                surface.normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
                InputData lighting;
                InitializeInputData(input, surface.normalTS, lighting);
                float3 dpdx = ddx(lighting.positionWS), dpdy = ddy(lighting.positionWS);
                float3 tangentX = cross(dpdy, lighting.normalWS);
                float3 tangentY = cross(lighting.normalWS, dpdx);
                float det = dot(dpdx, tangentX);
                float3 gradient = (ddx(thread)*tangentX + ddy(thread)*tangentY) * sign(det) / max(abs(det), 1e-14);
                lighting.normalWS = normalize(lighting.normalWS - .0000035 * gradient);
                InitializeBakedGIData(input, lighting);
                half4 color = UniversalFragmentPBR(lighting, surface);
                // 呢面绒毛在擦过纤维的光下呈染料本色。布面中央的镜反射由介电项控制。
                half grazing = 1 - saturate(dot(lighting.normalWS, lighting.viewDirectionWS));
                Light key = GetMainLight(lighting.shadowCoord);
                half wrapped = saturate(.45h + .55h * dot(lighting.normalWS, key.direction));
                half3 irradiance = lighting.bakedGI + wrapped * key.color * key.distanceAttenuation * key.shadowAttenuation;
                color.rgb += dye * irradiance * (0.23h * nap * grazing * grazing * surface.occlusion);
                color.rgb = MixFog(color.rgb, lighting.fogCoord);
                return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
