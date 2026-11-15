Shader "Catan/ForestCove"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+3" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite On
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float3 position:POSITION; float4 color:COLOR; float3 normal:NORMAL; };
            struct Output { float4 position:SV_POSITION; float4 color:COLOR; float3 world:TEXCOORD0; };
            Output Vert(Input i)
            {
                Output o;
                o.position=TransformObjectToHClip(i.position);
                float3 n=TransformObjectToWorldNormal(i.normal);
                o.world=TransformObjectToWorld(i.position);
                o.color=float4(pow(saturate(i.color.rgb),3.6)*(.77+.23*abs(dot(n,normalize(float3(-.4,1,.4))))),i.color.a);
                return o;
            }
            half4 Frag(Output i):SV_Target { clip(i.color.a-.015); float2 p=i.world.xz*1650;
                float mottling=sin(p.x+sin(p.y*1.8))*sin(p.y*.81)+.45*sin(p.x*4.2+p.y);
                float grain=frac(sin(dot(floor(p*9),float2(17.17,93.41)))*4375.7);
                i.color.rgb *= .94+.09*mottling+.08*grain;
                i.color.a *= lerp(.72,1,smoothstep(.75,1,i.color.a));
                return i.color; }
            ENDHLSL
        }
    }
}

