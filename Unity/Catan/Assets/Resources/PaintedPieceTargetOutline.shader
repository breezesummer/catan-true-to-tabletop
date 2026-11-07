Shader "Catan/PaintedPieceTargetOutline"
{
    Properties { _BaseMap("棋子原画",2D)="white"{} _Atlas("图集",Float)=0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent"}
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float _Atlas; float4 _BaseMap_TexelSize;
            CBUFFER_END
            struct Input { float4 pos:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            Output Vert(Input v){Output o;o.pos=TransformObjectToHClip(v.pos.xyz);o.uv=v.uv;return o;}
            half4 Frag(Output i):SV_Target
            {
                float2 cells=lerp(float2(1,1),float2(3,2),_Atlas);
                float2 cell=floor(clamp(i.uv,0.0001,0.9999)*cells)/cells;
                float2 lo=cell+_BaseMap_TexelSize.xy*0.5, hi=cell+1/cells-_BaseMap_TexelSize.xy*0.5;
                float2 dx=ddx(i.uv)*2.5, dy=ddy(i.uv)*2.5;
                half center=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a;
                half edge=0;
                [unroll] for(int n=0;n<8;n++){
                    float a=n*0.78539816;
                    float2 offset=dx*cos(a)+dy*sin(a);
                    half sampleAlpha=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,clamp(i.uv+offset,lo,hi)).a;
                    edge=max(edge,sampleAlpha);
                }
                half alpha=saturate(edge-center)*(.88+.1*sin(_Time.y*4));
                clip(alpha-.025);
                return half4(.045,1.0,.23,alpha);
            }
            ENDHLSL
        }
    }
}
