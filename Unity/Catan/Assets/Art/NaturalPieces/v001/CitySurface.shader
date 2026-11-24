Shader "Catan/王城石材与板岩"
{
    Properties
    {
        [MainTexture] _BaseMap("构件固有色", 2D) = "white" {}
        [MainColor] _BaseColor("席位或石材色", Color) = (1,1,1,1)
        _DetailMask("板岩着色范围", 2D) = "black" {}
        [Normal] _BumpMap("构件法线", 2D) = "bump" {}
        _BumpScale("纹理深度", Range(0,2)) = .75
        _MetallicGlossMap("金属度与光滑度", 2D) = "white" {}
        _Smoothness("光滑度", Range(0,1)) = 1
        [HideInInspector] _Metallic("金属度", Float) = 0
        [HideInInspector] _SpecularHighlights("镜面高光", Float) = 1
        [HideInInspector] _ReceiveShadows("接收投影", Float) = 0
        [HideInInspector] _Cull("背面剔除", Float) = 2
        [HideInInspector] _ZWrite("深度写入", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex CityVertex
            #pragma fragment CityFragment
            #pragma shader_feature_local_fragment _CITY_TINTED_SLATE
            #pragma shader_feature_local_fragment _METROPOLIS_FINISH
            #pragma shader_feature_local_fragment _PALACE_COURTYARD
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fog
            #define _NORMALMAP 1
            #define _METALLICSPECGLOSSMAP 1
            #define _RECEIVE_SHADOWS_OFF 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            void CityVertex(Attributes v, out Varyings output, out half2 daylight : TEXCOORD4)
            {
                output = LitPassVertex(v);
                daylight = v.staticLightmapUV;
            }

            half4 CityFragment(Varyings input, half2 daylight : TEXCOORD4) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surface;
                InitializeStandardLitSurfaceData(input.uv, surface);
                #if defined(_CITY_TINTED_SLATE)
                half3 slate = surface.albedo / max(_BaseColor.rgb, .001h);
                half pigment = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, input.uv).r;
                half value = clamp(dot(slate, half3(.2126,.7152,.0722)) / .29h, .12h, 1.18h);
                #if defined(_METROPOLIS_FINISH)
                // 深色板岩和铜板仍显出瓦纹，同时让棋盘视距下的屋面读得出席位色。
                value = .50h + .62h * value;
                pigment = sqrt(pigment);
                #endif
                surface.albedo = lerp(slate, _BaseColor.rgb * value, pigment);
                #endif
                #if defined(_METROPOLIS_FINISH)
                // 按最亮色道留出高光余量，浅石和金饰保留原有色相。
                half peak = max(surface.albedo.r, max(surface.albedo.g, surface.albedo.b));
                surface.albedo *= 1.55h / (1.0h + .55h * saturate(peak));
                #endif
                // 石脚返潮与檐底积尘跟随构件位置。窗洞和飞扶壁下的遮蔽由整城离线求得。
                surface.albedo *= lerp(half3(1,1,1),half3(.74,.71,.65),daylight.y);
                surface.occlusion = lerp(.38h,1.0h,daylight.x);
                InputData light;
                InitializeInputData(input, surface.normalTS, light);
                InitializeBakedGIData(input, light);
                half sky = saturate(light.normalWS.y * .5h + .5h);
                half3 diffuseSky = half3(.38,.41,.45) * lerp(.66h,1.14h,sky);
                #if defined(_PALACE_COURTYARD)
                // 浅石庭院反光补亮背光立面，窗膛和柱头下的遮蔽来自各自的天光烘焙。
                half facing = 1.0h - abs(light.normalWS.y);
                diffuseSky += half3(.27h,.248h,.215h) * facing * lerp(.38h,1.0h,daylight.x);
                #endif
                light.bakedGI = max(light.bakedGI, diffuseSky);
                half4 color = UniversalFragmentPBR(light, surface);
                color.rgb = MixFog(color.rgb, light.fogCoord);
                return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
