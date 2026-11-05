Shader "Catan/FloatingNet"
{
    Properties { [MainTexture] _BaseMap("自然散网",2D)="white" {} }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+2" "RenderType"="Transparent"}
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            struct Input {float4 p:POSITION;float2 uv:TEXCOORD0;};
            struct Output {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
            Output Vert(Input i)
            {
                Output o;float3 p=TransformObjectToWorld(i.p.xyz);
                p.x+=sin(_Time.y*.7+p.z*170)*.00015*i.uv.y;
                o.p=TransformWorldToHClip(p);o.uv=i.uv;return o;
            }
            half4 Frag(Output i):SV_Target
            {
                half4 ink=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                ink.rgb=lerp(ink.rgb,half3(.04,.22,.17),i.uv.y*.20);
                ink.a*=.92;return ink;
            }
            ENDHLSL
        }
    }
}
