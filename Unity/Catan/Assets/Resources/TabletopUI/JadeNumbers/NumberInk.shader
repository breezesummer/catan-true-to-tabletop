Shader "Catan/BoardNumberInk"
{
    Properties
    {
        [MainTexture] _BaseMap ("点数字面", 2D) = "white" {}
        [HideInInspector] _BoardHeight ("棋面深度基准", Float) = .009
        [HideInInspector] _DepthTest ("深度比较", Float) = 4
        [HideInInspector] _StencilRef ("模板参考值", Float) = 2
        [HideInInspector] _ReadMask ("模板读掩码", Float) = 1
        [HideInInspector] _WriteMask ("模板写掩码", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+50" "RenderType"="Transparent" }
        ZWrite Off
        ZTest [_DepthTest]
        // 位 0 是可见光效，位 1 是已画清晰字面的像素；虚字只补在其余位置。
        Stencil { Ref [_StencilRef] ReadMask [_ReadMask] WriteMask [_WriteMask] Comp Equal Pass Replace }
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma multi_compile_local_fragment _ SOFT_NUMBER_INK
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float _BoardHeight;
            struct Input { float3 position : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Output Vert(Input v)
            {
                Output o;
                float3 world = TransformObjectToWorld(v.position);
                // 沿正交镜头视线落到棋面。投影中心和字形不动，笔画深度随脚下棋面变化。
                float3 toEye = UNITY_MATRIX_V[2].xyz;
                world += toEye * ((_BoardHeight - world.y) / max(toEye.y, .001));
                o.position = TransformWorldToHClip(world);
                o.uv = v.uv;
                return o;
            }
            half4 Frag(Output i) : SV_Target
            {
                #if defined(SOFT_NUMBER_INK)
                    half4 ink = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, i.uv, .35);
                    ink.a *= .72h;
                    return ink;
                #else
                    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                #endif
            }
            ENDHLSL
        }
    }
}
