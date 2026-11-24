Shader "Catan/商港仓屋"
{
    Properties
    {
        [MainTexture] _BaseMap("港仓固有色",2D)="white"{}
        _RoofColor("占港玩家色",Color)=(0,0,0,0)
        [HideInInspector] _PlaqueGlint("交易牌可用、指向、扫光相位与点按截止时间",Vector)=(0,0,0,0)
        [HideInInspector] _IsTradePlaque("屋面交易牌",Float)=0
        _RoofAtlas("屋瓦图集横界（起点、整格终点、上半格终点）",Vector)=(0,0,0,0)
        [Toggle(_ORE_WHARF)] _OreWharf("原矿港天光与潮痕",Float)=0
        [MainColor] _BaseColor("材质本色",Color)=(1,1,1,1)
        [Normal] _BumpMap("切线法线",2D)="bump"{}
        _BumpScale("表面凹凸",Range(0,2))=.82
        _MetallicGlossMap("金属度与光滑度",2D)="white"{}
        _Smoothness("光滑度",Range(0,1))=1
        [HideInInspector] _Metallic("金属度",Float)=0
        [HideInInspector] _Cull("背面剔除",Float)=2
        [HideInInspector] _ZWrite("深度写入",Float)=1
        [HideInInspector] _SrcBlend("源混合",Float)=1
        [HideInInspector] _DstBlend("目标混合",Float)=0
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
            #pragma vertex HarborVertex
            #pragma fragment HarborFragment
            #pragma shader_feature_local _ORE_WHARF
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fog
            #define _NORMALMAP 1
            #define _METALLICSPECGLOSSMAP 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            half4 _RoofColor;
            float4 _RoofAtlas;
            float4 _PlaqueGlint;
            half _IsTradePlaque;

            void HarborVertex(Attributes v, out Varyings o, out half4 details : TEXCOORD4)
            {
                o=LitPassVertex(v);
                details=half4(v.staticLightmapUV,v.dynamicLightmapUV);
            }
            half4 HarborFragment(Varyings input, half4 details : TEXCOORD4) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surface;
                InitializeStandardLitSurfaceData(input.uv,surface);
                // 六套图集的板岩都在第二行，部分变体只占相邻格的上半幅。
                float2 uv=input.uv;
                if (_RoofColor.a>0 && uv.y>=.5 && uv.y<.75 && uv.x>=_RoofAtlas.x &&
                    uv.x<(uv.y>=.625 ? _RoofAtlas.z : _RoofAtlas.y))
                {
                    half shade=clamp(Luminance(surface.albedo)/.095h,.32h,1.20h);
                    surface.albedo=lerp(surface.albedo,_RoofColor.rgb*shade,.90h);
                }
                #if defined(_ORE_WHARF)
                half2 exposure=details.xy;
                // 拱膛、货斗与檐下的遮蔽来自整港几何，毛细潮痕自桩脚和块石水线向上衰减。
                surface.occlusion=lerp(.14h,1.0h,exposure.x);
                surface.albedo*=lerp(.47h,1.0h,exposure.x)*lerp(half3(1,1,1),half3(.34,.44,.37),exposure.y);
                surface.smoothness=lerp(surface.smoothness,max(.65h,surface.smoothness),exposure.y*.83h);
                #endif
                InputData light;
                InitializeInputData(input,surface.normalTS,light);
                InitializeBakedGIData(input,light);
                #if defined(_ORE_WHARF)
                // 海侧接受天空漫射，背阳立面的木纹仍可辨认；仓内光照受烘焙可见度约束。
                half sky=saturate(light.normalWS.y*.5h+.5h);
                half3 coastSky=half3(.33h,.36h,.40h)*lerp(.68h,1.10h,sky)*lerp(.29h,1.0h,exposure.x);
                light.bakedGI=max(light.bakedGI,coastSky);
                #endif
                half4 color=UniversalFragmentPBR(light,surface);
                #if defined(_ORE_WHARF)
                // 以迎光矿港的漆面和铸字为基准，给背光港补上柔和反射。铜边单独接收交易扫光。
                if (_IsTradePlaque>.5h && details.z<.01h && uv.y<.25)
                {
                    bool enamel=uv.x>=.5 && uv.x<.625 && uv.y>=.125;
                    bool bronze=uv.x>=.25 && uv.x<.5;
                    half facing=saturate(dot(light.normalWS,light.viewDirectionWS));
                    if (enamel)
                    {
                        half sheen=.5h+.5h*cos((uv.x-.5h)*56.h+(uv.y-.125h)*32.h);
                        half lacquer=.11h+.025h*facing+.13h*sheen+.46h*(surface.smoothness-.47h)+Luminance(surface.albedo);
                        color.rgb=max(color.rgb,half3(1,.96h,.82h)*lacquer*lerp(.82h,1.h,exposure.x));
                    }
                    else if (bronze)
                    {
                        half polish=saturate((surface.smoothness-.49h)*5.h);
                        half3 brass=lerp(half3(.64h,.36h,.12h),half3(1.35h,1.20h,.94h),polish);
                        color.rgb=max(color.rgb,brass*lerp(.49h,1.h,facing*facing)*lerp(.78h,1.h,exposure.x));
                    }
                }
                #endif
                // UV3 的有效像素来自原框条。直接提亮金属表面，视角、遮挡和圆角均随网格。
                if (details.z>.99h)
                {
                    float scan=frac((_Time.y+_PlaqueGlint.z)/5.2)*5.2/.95;
                    half streak=1-smoothstep(.015,.14,abs(details.w-(scan*1.30-.15)));
                    half press=saturate((_PlaqueGlint.w-_Time.y)/.19);
                    half strength=max(press,_PlaqueGlint.x*lerp(.32+.60*streak,.78,_PlaqueGlint.y));
                    color.rgb=lerp(color.rgb,max(color.rgb,half3(1,.79,.43)),strength);
                }
                color.rgb=MixFog(color.rgb,light.fogCoord);
                return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
