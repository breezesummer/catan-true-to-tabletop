Shader "Catan/ShipWake"
{
    Properties
    {
        _CoastMask("海岸",2D)="black"{}
        _MaskRect("海岸范围",Vector)=(0,0,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_CoastMask); SAMPLER(sampler_CoastMask);
            CBUFFER_START(UnityPerMaterial)
            float4 _MaskRect;
            CBUFFER_END
            struct Input { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 clip:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };
            Output Vert(Input i)
            {
                Output o;
                float3 p=TransformObjectToWorld(i.positionOS.xyz);
                o.world=p.xz;
                float2 coast=SAMPLE_TEXTURE2D_LOD(_CoastMask,sampler_CoastMask,(p.xz-_MaskRect.xy)/_MaskRect.zw,0).rg;
                float wave=sin(dot(p.xz,float2(26,17))-_Time.y*.85)*.0008
                    +sin(dot(p.xz,float2(-19,34))-_Time.y*1.1)*.0004;
                p.y+=.00023+wave*smoothstep(.06,.45,coast.r)*(1-coast.g*.8);
                o.clip=TransformWorldToHClip(p);o.uv=i.uv;return o;
            }
            half4 Frag(Output i):SV_Target
            {
                float x=i.uv.x, t=i.uv.y;
                float phase=dot(i.world,float2(93,211));
                float flow=t*32-_Time.y*2+phase;
                float bend=.063*sin(t*5+phase)+.016*sin(flow*.46);
                float side=abs(x-bend);
                float crest=.28+.34*t+.045*sin(flow*.52+sign(x)*1.4);
                float aa=max(fwidth(x),.028);
                float ribbon=1-smoothstep(.012,.075+aa,abs(side-crest));
                float lace=smoothstep(-.38,.78,sin(flow*.91+sign(x)*1.9+sin(phase+t*9)));
                float ripples=pow(saturate(.5+.5*cos(flow*1.4-side*9)),7)
                    *(1-smoothstep(.06,.72,side));
                float fade=(1-smoothstep(.30,1,t))*smoothstep(0,.055,t);
                float shore=SAMPLE_TEXTURE2D(_CoastMask,sampler_CoastMask,(i.world-_MaskRect.xy)/_MaskRect.zw).r;
                float foam=ribbon*(.24+.76*lace)+ripples*.27;
                float wash=.085*(1-smoothstep(.10,.8,side));
                float alpha=saturate(foam*.65+wash)*fade*smoothstep(.002,.045,shore);
                return half4(lerp(float3(.08,.36,.32),float3(.65,.80,.71),saturate(foam)),alpha);
            }
            ENDHLSL
        }
    }
}
