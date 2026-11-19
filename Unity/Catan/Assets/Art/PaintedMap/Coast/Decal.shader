Shader "Catan/CoastDecal"
{
    Properties { [MainTexture] _BaseMap("沿岸画稿",2D)="white" {} _Tint("颜色",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _Tint;
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Input {float4 pos:POSITION; float2 uv:TEXCOORD0;};
            struct Output {float4 pos:SV_POSITION; float2 uv:TEXCOORD0;};
            Output Vert(Input i){Output o;o.pos=TransformObjectToHClip(i.pos.xyz);o.uv=i.uv;return o;}
            half4 Frag(Output i):SV_Target {
                half4 ink=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                clip(ink.a-.007);
                return ink*_Tint;
            }
            ENDHLSL
        }
    }
}
