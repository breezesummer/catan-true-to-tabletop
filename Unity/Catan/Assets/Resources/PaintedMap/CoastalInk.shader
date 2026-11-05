Shader "Catan/CoastalInk"
{
    Properties { [MainTexture] _BaseMap("港口画稿",2D)="white" {} _Coverage("印面",Float)=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float _Coverage;
            struct Input {float4 p:POSITION;float2 uv:TEXCOORD0;};
            struct Output {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
            Output Vert(Input v){Output o;o.p=TransformObjectToHClip(v.p.xyz);o.uv=v.uv;return o;}
            half4 Frag(Output i):SV_Target
            {
                half3 rgb=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                // 水域的青色印墨淡出，保留鱼鳞、货物、木栈桥和沙岸，让动态海水贯穿港口。
                half water=(rgb.g-rgb.r)/max(.02h,rgb.g);
                half opacity=1-smoothstep(.14h,.62h,water);
                return half4(rgb,_Coverage*opacity);
            }
            ENDHLSL
        }
    }
}
