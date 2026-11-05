Shader "Catan/自然棋子贴地柔影"
{
    Properties
    {
        [MainTexture] _BaseMap("材质引用", 2D) = "white" {}
        _BaseColor("预览颜色", Color) = (1,1,1,1)
        _ShadowColor("接地颜色", Color) = (0.025,0.019,0.012,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-5" }
        Pass
        {
            Name "棋子接地"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings { float4 positionCS : SV_POSITION; half4 soil : COLOR; float2 groundXZ : TEXCOORD0; half shadow : TEXCOORD1; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 ground=TransformObjectToWorld(input.positionOS.xyz);
                output.shadow=step(.99,input.uv.x);
                ground.xz+=output.shadow*input.uv.y*float2(.0032,-.0019);
                output.positionCS=TransformWorldToHClip(ground);
                output.soil=input.color;
                output.groundXZ=ground.xz;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // 土色从墙边向地形渐隐，细碎的颗粒沿棋盘坐标连续分布。
                half3 gravel=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,frac(input.groundXZ*52)).rgb;
                half4 soil=input.soil;
                soil.rgb=lerp(gravel*half3(.92,.80,.65),_ShadowColor.rgb,input.shadow);
                soil.a*=lerp(saturate(.74+gravel.g*.42),1,input.shadow);
                return soil;
            }
            ENDHLSL
        }
    }
}
