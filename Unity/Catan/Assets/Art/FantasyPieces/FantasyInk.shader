Shader "Catan/FantasyInk"
{
    Properties
    {
        [MainTexture] _BaseMap("棋子原画", 2D) = "white" {}
        _Pigment("阵营颜料", Color) = (1,1,1,1)
        _Swap("替换蓝色", Float) = 0
        _Atlas("六格图集", Float) = 0
        _BaseColor("预览色", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _Pigment;
            float _Swap, _Atlas;
            CBUFFER_END
            struct Input { float4 pos:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            Output Vert(Input v)
            {
                Output o; o.pos=TransformObjectToHClip(v.pos.xyz); o.uv=v.uv; return o;
            }
            half4 Frag(Output i):SV_Target
            {
                half4 ink=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                half blue=ink.b;
                half mask=smoothstep(.09,.35,(blue-max(ink.r,ink.g))/max(blue,.001));
                half3 dyed=_Pigment.rgb*(.09+1.22*blue);
                ink.rgb=lerp(ink.rgb,dyed,mask*_Swap)*lerp(1,_BaseColor.rgb,.7);
                // 将紧贴石基或船身的暗边与地面合成，缩小后仍能辨认轮廓。
                float2 offset=float2(-.012,.019)*lerp(float2(1,1),float2(1./3.,.5),_Atlas);
                half shadow=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv+offset).a*.36;
                half alpha=ink.a+shadow*(1-ink.a);
                clip(alpha-.015);
                return half4((ink.rgb*ink.a+half3(.028,.018,.010)*shadow*(1-ink.a))/alpha,alpha);
            }
            ENDHLSL
        }
    }
}
