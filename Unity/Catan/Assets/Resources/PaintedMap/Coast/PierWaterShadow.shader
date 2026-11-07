Shader "Catan/PierWaterShadow"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+2" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float3 p:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 clip:SV_POSITION; float2 mm:TEXCOORD0; };
            Output Vert(Input i)
            {
                Output o;
                o.clip=TransformObjectToHClip(i.p);
                o.mm=(i.uv-.5)*38;
                return o;
            }
            float Shade(float2 offset,float2 halfSize)
            {
                return 1-smoothstep(-1.6,2.0,max(abs(offset.x)-halfSize.x,abs(offset.y)-halfSize.y));
            }
            half4 Frag(Output i):SV_Target
            {
                float deck=Shade(i.mm-float2(0,-8.0),float2(12.2,7.6));
                float walk=Shade(i.mm-float2(0,5.3),float2(2.9,5.8));
                return half4(.008,.038,.034,.38*max(deck,walk));
            }
            ENDHLSL
        }
    }
}
