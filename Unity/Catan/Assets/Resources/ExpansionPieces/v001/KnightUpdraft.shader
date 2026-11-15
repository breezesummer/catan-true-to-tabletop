Shader "Catan/骑士上升气流"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct V { float3 pos:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct F { float4 pos:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            F Vert(V v)
            {
                F o; o.pos=TransformObjectToHClip(v.pos); o.color=v.color; o.uv=v.uv; return o;
            }
            half4 Frag(F i):SV_Target
            {
                float2 p=i.uv*2-1;
                half density=saturate(1-dot(p,p));
                half core=density*density*density;
                return half4(lerp(i.color.rgb,half3(1,1,1),.40*core), i.color.a*density);
            }
            ENDHLSL
        }
    }
}
