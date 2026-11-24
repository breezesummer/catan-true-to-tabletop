Shader "Catan/圆门村庄"
{
    Properties
    {
        [MainTexture] _BaseMap("构件固有色", 2D) = "white" {}
        [MainColor] _BaseColor("席位釉色", Color) = (1,1,1,1)
        _DetailMask("陶瓦着釉范围", 2D) = "black" {}
        [Normal] _BumpMap("构件法线", 2D) = "bump" {}
        _BumpScale("纹理深度", Range(0,2)) = .75
        _MetallicGlossMap("金属度与光滑度", 2D) = "white" {}
        _Smoothness("光滑度", Range(0,1)) = 1
        _OcclusionMap("烟熏返潮、接触可见度与抛光", 2D) = "white" {}
        _OcclusionStrength("接触遮蔽", Range(0,1)) = .73
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
            #pragma vertex VillageVertex
            #pragma fragment VillageFragment
            #pragma shader_feature_local_fragment _VILLAGE_GLAZE
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fog
            #define _NORMALMAP 1
            #define _METALLICSPECGLOSSMAP 1
            #define _RECEIVE_SHADOWS_OFF 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            void VillageVertex(Attributes v, out Varyings o, out float2 contactUV : TEXCOORD4)
            {
                o = LitPassVertex(v);
                contactUV = v.staticLightmapUV;
            }

            half4 VillageFragment(Varyings input, float2 contactUV : TEXCOORD4) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surface;
                InitializeStandardLitSurfaceData(input.uv, surface);
                #if defined(_VILLAGE_GLAZE)
                half3 original = surface.albedo / max(_BaseColor.rgb, half3(.001,.001,.001));
                half glaze = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, input.uv).r;
                half shade = clamp(dot(original, half3(.2126,.7152,.0722)) / .42h, .12h, 1.18h);
                surface.albedo = lerp(original, _BaseColor.rgb * shade, glaze);
                #endif
                // UV0 沿构件取材，UV1 为整屋唯一展开。阴角与触摸痕迹跟随建筑位置。
                half3 wear = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, contactUV).rgb;
                surface.albedo *= lerp(half3(1,1,1), half3(.34,.30,.26), wear.r);
                surface.albedo = lerp(surface.albedo, half3(.50,.26,.067), wear.b);
                surface.smoothness = lerp(surface.smoothness, .80h, wear.b);
                surface.occlusion = lerp(1.0h, wear.g, _OcclusionStrength);
                surface.albedo *= lerp(.80h, 1.0h, wear.g);
                InputData lighting;
                InitializeInputData(input, surface.normalTS, lighting);
                InitializeBakedGIData(input, lighting);
                half4 color = UniversalFragmentPBR(lighting, surface);
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
