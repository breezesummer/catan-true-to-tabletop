Shader "Catan/PierWaterShadow"
{
    Properties
    {
        _CoastMask("海岸",2D)="black"{}
        _MaskRect("海岸范围",Vector)=(0,0,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+2" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_CoastMask); SAMPLER(sampler_CoastMask);
            float4 _MaskRect;
            struct Input { float3 p:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 clip:SV_POSITION; float2 mm:TEXCOORD0; };
            Output Vert(Input i)
            {
                Output o;
                float3 p=TransformObjectToWorld(i.p);
                float2 coast=SAMPLE_TEXTURE2D_LOD(_CoastMask,sampler_CoastMask,(p.xz-_MaskRect.xy)/_MaskRect.zw,0).rg;
                float wave=sin(dot(p.xz,float2(26,17))-_Time.y*.85)*.0008+sin(dot(p.xz,float2(-19,34))-_Time.y*1.1)*.0004;
                p.y+=.00057+wave*smoothstep(.06,.45,coast.r)*(1-coast.g*.8);
                o.clip=TransformWorldToHClip(p);
                o.mm=(i.uv-.5)*68;
                return o;
            }
            float Shade(float2 offset,float2 halfSize)
            {
                return 1-smoothstep(-1.6,2.0,max(abs(offset.x)-halfSize.x,abs(offset.y)-halfSize.y));
            }
            half4 Frag(Output i):SV_Target
            {
                float deck=Shade(i.mm-float2(0,-5),float2(5.6,16.5));
                float walk=Shade(i.mm-float2(-14.6,-20.8),float2(.70,.70));
                // 水线附近散开的细弧只落在木桩周围。
                float row=clamp(round((i.mm.y+20.4)/9.5),0,3);
                float2 post=float2(abs(i.mm.x)-5.05,i.mm.y+20.4-row*9.5);
                float2 mast=i.mm-float2(-14.6,-20.8);
                float2 ripple=dot(post,post)<dot(mast,mast)?post:mast;
                float angle=atan2(ripple.y,ripple.x);
                float phase=_Time.y*.85+row*.71;
                float d=length(ripple)+.12*sin(angle*3+phase)+.08*cos(angle*2-phase*.60);
                float contact=1-smoothstep(.14,.38,abs(d-.86));
                float crest=(1-smoothstep(.10,.32,abs(d-1.64-.14*sin(phase))))*
                    smoothstep(-.76,.65,cos(angle+phase*.32));
                float broken=.25+.75*smoothstep(-.85,.60,sin(angle+phase*.24)+.3*cos(3*angle-phase));
                float foam=.31*contact*broken+.17*crest;
                float2 streak=ripple-float2(.20,-.95);
                float submerged=.30*(1-smoothstep(.35,1.6,length(streak*float2(1.1,.52))));
                float shade=max(submerged,.16*max(deck,walk));
                float alpha=foam+shade*(1-foam);
                half3 color=(half3(.008,.065,.055)*shade*(1-foam)+half3(.42,.70,.60)*foam)/max(alpha,.0001);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
