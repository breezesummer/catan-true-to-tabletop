Shader "Catan/大都会屋顶光点"
{
    Properties
    {
        [PerRendererData] _MainTex ("精灵纹理", 2D) = "white" {}
        _LampTint ("路线色", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        // 仅给通过深度测试、实际发光的像素标记点数避让。
        Stencil { Ref 1 ReadMask 1 WriteMask 1 Comp Always Pass Replace }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            half4 _LampTint;
            struct V { float2 uv : TEXCOORD0; };
            struct F { float4 pos : SV_POSITION; float2 offset : TEXCOORD0; };
            F Vert(V input)
            {
                F o;
                o.offset=input.uv*2-1;
                o.pos=TransformObjectToHClip(float3(0,0,0));
                // 全盘也留出光核周围的晕光，近景随建筑一同放大。
                float width=length(TransformObjectToWorldDir(float3(1,0,0),false));
                float pixelDiameter=max(width*abs(UNITY_MATRIX_P._m11)*_ScreenParams.y/(2*o.pos.w),
                    max(36.0,_ScreenParams.y*.054));
                o.pos.xy+=o.offset*float2(1,_ProjectionParams.x)*pixelDiameter/_ScreenParams.xy*o.pos.w;
                return o;
            }
            half4 Frag(F i) : SV_Target
            {
                half r2=dot(i.offset,i.offset);
                half core=exp2(-36*r2);
                half body=exp2(-9*r2);
                half halo=exp2(-4.5*r2)*(1-smoothstep(.55,1,r2));
                // 内部亮度向球心累积。外晕只向背景叠光，在深浅地块的交界处仍能透出棋盘纹理。
                half3 coreTint=lerp(_LampTint.rgb,half3(1,1,1),.94);
                half3 emission=coreTint*core+_LampTint.rgb*(.93*body+.50*halo);
                clip(max(emission.r,max(emission.g,emission.b)) - .012h);
                return half4(emission,saturate(.90*body));
            }
            ENDHLSL
        }
    }
}
