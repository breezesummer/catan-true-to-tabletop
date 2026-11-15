Shader "Catan/WheatShore"
{
    Properties { _Sand("沙粒",2D)="white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-5" }
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            TEXTURE2D(_Sand); SAMPLER(sampler_Sand);
            struct V {float4 clip:SV_POSITION;float2 p:TEXCOORD0;float dist:TEXCOORD1;};
            V vert(float3 pos:POSITION,float2 uv:TEXCOORD0){
                V o; float3 world=TransformObjectToWorld(pos);
                o.clip=TransformWorldToHClip(world);
                o.p=world.xz/.04; o.dist=uv.x;
                return o;
            }
            half4 frag(V i):SV_Target{
                float2 p=i.p;
                float dist=i.dist;
                float grain=SAMPLE_TEXTURE2D(_Sand,sampler_Sand,p*2.2).r;
                float bend=.035*sin(8*p.x+3*p.y)+.024*sin(15*p.y-5*p.x)+.008*sin(36*p.x+16*p.y);

                float water=dist-.032-bend;
                float reach=.15+.052*sin(6*p.x-3*p.y);
                float wet=smoothstep(-.046,.002,water);
                float shore=smoothstep(-.004,.008,water);
                float depth=smoothstep(.006,reach,water);
                float3 sand=lerp(float3(.76,.50,.22),float3(.45,.37,.19),wet)*(.85+.32*grain);
                float3 sea=lerp(float3(.32,.52,.40),float3(.037,.31,.30),depth);
                float patches=sin(17*p.x+11*p.y)+.71*sin(29*p.y-19*p.x)+.37*sin(58*p.x+37*p.y);
                float crest=water-.010-.004*sin(42*p.x-31*p.y);
                float foam=(1-smoothstep(.0008,.0065,abs(crest)))*smoothstep(.13,.98,patches);
                foam+=.31*(1-smoothstep(.001,.004,abs(crest-.016)))*smoothstep(.84,1.42,patches);
                float alpha=smoothstep(-.087,-.022,dist)*(1-smoothstep(.026,reach,water));
                return half4(lerp(lerp(sand,sea,shore),float3(.78,.84,.68),foam*.64),alpha);
            }
            ENDHLSL
        }
    }
}
