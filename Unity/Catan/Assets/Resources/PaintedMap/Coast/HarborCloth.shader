Shader "Catan/HarborCloth"
{
    Properties
    {
        [MainTexture] _BaseMap("资源印花",2D)="black" {}
        _Linen("旗布",2D)="white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "DisableBatching"="True" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Linen); SAMPLER(sampler_Linen);
            float3 _PrintCenter, _PrintWidth, _PrintHeight;
            struct Attributes { float3 p:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 p:SV_POSITION; float3 normal:TEXCOORD1; float2 uv:TEXCOORD0; float2 stamp:TEXCOORD2; };
            Varyings Vert(Attributes a)
            {
                Varyings o;
                o.p=TransformObjectToHClip(a.p);
                o.normal=TransformObjectToWorldNormal(a.n);
                o.uv=a.uv;
                // 正交棋盘相机中，印花按同一个屏幕尺度采样；布纹沿真实网格折叠。
                float3x3 toView=(float3x3)mul(UNITY_MATRIX_V,UNITY_MATRIX_M);
                float2 delta=mul(toView,a.p-_PrintCenter).xy;
                float width=abs(mul(toView,_PrintWidth).x);
                float height=abs(mul(toView,_PrintHeight).y);
                o.stamp=.5+delta/max(.000001,min(width,height*1.15));
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                Light sun=GetMainLight();
                half nDotL=dot(normalize(i.normal),sun.direction);
                // 漫反射与背面透光共同托住深褶处的亮度。
                half light=.63+.42*saturate(.35+.65*nDotL)+.12*saturate(-nDotL);
                half3 fabric=SAMPLE_TEXTURE2D(_Linen,sampler_Linen,i.uv).rgb;
                half4 stamp=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,saturate(i.stamp));
                half3 ink=lerp(fabric,stamp.rgb*fabric/SRGBToLinear(half3(228,209,175)/255),stamp.a);
                // 系角附近的遮蔽沿斜折线衰减，中央印花保留本色。
                float edge=min(i.uv.x,1-i.uv.x);
                float span=min(i.uv.y,1-i.uv.y);
                float fold=(edge-.43*span)/.095;
                float tension=exp(-fold*fold)*smoothstep(.015,.19,span)*(1-smoothstep(.26,.49,span));
                return half4(ink*light*(1-.14*tension),1);
            }
            ENDHLSL
        }
    }
}
