Shader "Catan/本回合建造光环 v012"
{
    Properties
    {
        [MainColor] _BaseColor("金色光环", Color) = (1,0.66,0.10,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Name "建造光环与柔光"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float radius=length((input.uv-.5)*2);
                float distance=abs(radius-.74);
                half core=1-smoothstep(.035,.070,distance);
                half halo=exp2(-distance*22)*.52;
                half fade=1-smoothstep(.96,1,radius);
                half alpha=saturate(core+halo)*fade*_BaseColor.a;
                return half4(_BaseColor.rgb,alpha);
            }
            ENDHLSL
        }
    }
}
