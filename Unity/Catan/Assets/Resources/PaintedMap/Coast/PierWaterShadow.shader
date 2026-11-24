Shader "Catan/PierWaterShadow"
{
    Properties
    {
        _CoastMask("海岸",2D)="black"{}
        _MaskRect("海岸范围",Vector)=(0,0,1,1)
        [PerRendererData] _CompactLanding("仓储港泊位",Float)=0
        [PerRendererData] _WoolQuay("羊毛码头水线",Float)=0
        [PerRendererData] _QuayLanding("商港驳岸水线",Float)=0
        [PerRendererData] _BrickQuay("砖材商港石岸",Float)=0
        [PerRendererData] _GrainQuay("谷仓木桩与砌岸",Float)=0
        [PerRendererData] _OreWharf("矿港桩埠",Float)=0
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
            float _WoolQuay;
            float _CompactLanding, _BrickQuay, _QuayLanding, _GrainQuay;
            struct Input { float3 p:POSITION; float2 uv:TEXCOORD0; };
            float _OreWharf;
            struct Output { float4 clip:SV_POSITION; float2 mm:TEXCOORD0; };
            Output Vert(Input i)
            {
                Output o;
                float3 p=TransformObjectToWorld(i.p);
                float2 coast=SAMPLE_TEXTURE2D_LOD(_CoastMask,sampler_CoastMask,(p.xz-_MaskRect.xy)/_MaskRect.zw,0).rg;
                float wave=sin(dot(p.xz,float2(26,17))-_Time.y*.85)*.0008+sin(dot(p.xz,float2(-19,34))-_Time.y*1.1)*.0004;
                float clearance=(_BrickQuay>.5 || _QuayLanding>.5 || _WoolQuay>.5) ? .00012 : .00057;
                p.y+=clearance+wave*smoothstep(.06,.45,coast.r)*(1-coast.g*.8);
                o.clip=TransformWorldToHClip(p);
                o.mm=(i.uv-.5)*(_CompactLanding>.5 ? 32 : 68);
                return o;
            }
            float Shade(float2 offset,float2 halfSize)
            {
                return 1-smoothstep(-1.6,2.0,max(abs(offset.x)-halfSize.x,abs(offset.y)-halfSize.y));
            }
            half4 Frag(Output i):SV_Target
            {
                if (_WoolQuay > .5)
                {
                    float2 p=i.mm;
                    // 左侧挑桥的六根木桩与右侧石驳岸分别接水。
                    float2 pier=p-float2(-8.535,-2.1);
                    float2 pile=float2(abs(pier.x)-2.315,p.y-clamp(round((p.y+5.19)/3.05),0,2)*3.05+5.19);
                    float pulse=sin(dot(p,float2(.79,1.04))-_Time.y*.77);
                    float angle=atan2(pile.y,pile.x);
                    float ring=length(pile)+.042*sin(3*angle-_Time.y*.61)+.035*pulse;
                    float2 frontWall=abs(p-float2(3.09,3.78))-float2(8.98,5.79);
                    float2 tower=abs(p-float2(-8.15,5.28))-float2(3.82,4.20);
                    float bank=min(max(frontWall.x,frontWall.y),max(tower.x,tower.y));
                    float spray=(1-smoothstep(.05,.32,abs(bank-.11)))*smoothstep(-.62,.79,pulse);
                    float swell=.38+.62*smoothstep(-.70,.78,cos(angle+_Time.y*.30)+.28*pulse);
                    float foam=.15*spray+.16*swell*(1-smoothstep(.035,.15,abs(ring-.48)));
                    foam+=.046*swell*(1-smoothstep(.04,.15,abs(ring-.87)));
                    float shade=max(.24*(1-smoothstep(-.12,.92,bank)),.20*Shade(pier,float2(2.65,3.57)));
                    float alpha=foam+shade*(1-foam);
                    return half4((half3(.018,.072,.060)*shade*(1-foam)+half3(.53,.68,.61)*foam)/max(alpha,.0001),alpha);
                }
                if (_OreWharf > .5)
                {
                    // 短桥桩脚留弧形细沫，重货驳岸的浪线沿真实石墙转角折回。
                    float2 p=i.mm;
                    float postX=p.x < -8.17 ? -10.79 : -5.55;
                    float2 front=p-float2(postX,-4.03);
                    float2 tip=p-float2(p.x < -7.57 ? -9.49 : -5.65,-6.30);
                    float2 nearPost=dot(front,front)<dot(tip,tip) ? front : tip;
                    float angle=atan2(nearPost.y,nearPost.x);
                    float phase=_Time.y*.84+postX;
                    float ring=length(nearPost)+.065*sin(3*angle+phase);
                    float foam=.19*(1-smoothstep(.06,.22,abs(ring-.53)))*smoothstep(-.76,.68,cos(angle+phase*.31));
                    float2 wall=abs(p-float2(3.13,2.83))-float2(8.24,7.40);
                    float edge=max(wall.x,wall.y);
                    foam+=.16*(1-smoothstep(.06,.31,abs(edge-.135)))*smoothstep(-.68,.80,sin(p.x*.93+p.y*.71-phase));
                    float shade=max(.21*Shade(p-float2(0,2.84),float2(11.32,7.34)),
                                    .16*Shade(p-float2(-7.57,-5.65),float2(2.35,1.15)));
                    float alpha=foam+shade*(1-foam);
                    return half4((half3(.015,.062,.058)*shade*(1-foam)+half3(.46,.68,.61)*foam)/max(alpha,.0001),alpha);
                }
                if (_QuayLanding > .5)
                {
                    float2 p=i.mm;
                    bool timber=_QuayLanding>1.5;
                    float2 wall=timber ? abs(p-float2(.07,-.785))-float2(12.35,2.905) :
                                         abs(p-float2(0,2.11))-float2(11.64,7.52);
                    float quay=max(wall.x,wall.y);
                    if (timber)
                    {
                        // 驳岸只围住前方的装卸走道，后方水线沿仓房自己的墙脚收进。
                        float2 store=abs(p-float2(.055,5.57))-float2(11.93,4.35);
                        quay=min(quay,max(store.x,store.y));
                    }
                    float spray=1-smoothstep(.08,.43,abs(quay-.18));
                    spray*=smoothstep(-.68,.71,sin(p.x*1.16+p.y*.67-_Time.y*.80));
                    float2 pierCenter=timber ? float2(-7.895,-5.35) : float2(-7.65,-8.52);
                    float2 pierHalf=timber ? float2(2.595,1.65) : float2(2.34,3.45);
                    float2 pile=p-pierCenter;
                    if (timber)
                        pile-=float2(pile.x<0 ? -2.085 : 2.085,p.y < -5.11 ? -1.01 : 1.49);
                    else
                        pile-=float2(clamp(round(pile.x/1.90),-1,1)*1.90,p.y < -8.7 ? -2.96 : 2.42);
                    float radius=length(pile)+.055*sin(3*atan2(pile.y,pile.x)+_Time.y);
                    float arc=.22+.78*smoothstep(-.50,.72,cos(atan2(pile.y,pile.x)+_Time.y*.25));
                    float foam=.16*spray+.23*arc*(1-smoothstep(.07,.24,abs(radius-.55)));
                    float shade=max(.25*(1-smoothstep(-.22,.95,quay)),
                        .24*Shade(p-pierCenter,pierHalf));
                    if (timber)
                    {
                        float tide=_Time.y*.81+dot(p-pile,float2(.41,.29));
                        float angle=atan2(pile.y,pile.x);
                        float drift=.09*sin(3*angle+tide)+.035*cos(5*angle-tide);
                        float crest=.085*(1-smoothstep(.035,.18,abs(radius-.97-drift)));
                        foam=.57*foam+crest*smoothstep(.03,.82,cos(angle+tide*.43));
                        float wash=.09*(1-smoothstep(.045,.25,abs(quay-.28-.12*cos(tide+p.x*.69))));
                        foam+=wash*smoothstep(.12,.80,cos(p.x*.93+p.y*.78-tide));
                        shade=max(shade,.19*(1-smoothstep(.30,1.65,quay)));
                    }
                    float alpha=foam+shade*(1-foam);
                    return half4((half3(.016,.069,.058)*shade*(1-foam)+half3(.51,.70,.62)*foam)/max(alpha,.0001),alpha);
                }
                if (_GrainQuay > .5)
                {
                    float2 p=i.mm;
                    float2 pile=p-float2(p.x < -8.08 ? -9.96 : -6.20,p.y < -3.44 ? -4.97 : -1.91);
                    float phase=_Time.y*.84+pile.x*.71;
                    float angle=atan2(pile.y,pile.x);
                    float radius=length(pile)+.045*sin(3*angle+phase);
                    float ripple=(1-smoothstep(.04,.17,abs(radius-.42)))*.23+
                                 (1-smoothstep(.04,.21,abs(radius-.98-.07*sin(phase))))*.11;
                    ripple*=smoothstep(-.55,.76,cos(angle+phase*.37));
                    float pier=.21*Shade(p-float2(-8.08,-2.06),float2(2.30,3.39));
                    float quay=.22*Shade(p-float2(3.60,3.73),float2(7.56,6.46));
                    float alpha=ripple+max(pier,quay)*(1-ripple);
                    return half4(lerp(half3(.018,.080,.070),half3(.53,.71,.61),ripple/max(alpha,.0001)),alpha);
                }
                if (_BrickQuay > .5)
                {
                    float2 p=i.mm;
                    float quay=max(abs(p.x)-12.12,abs(p.y-4.36)-6.31);
                    quay=max(quay,dot(p-float2(-12.12,4.92),float2(-.942,.336)));
                    quay=max(quay,dot(p-float2(12.10,6.15),float2(.942,.334)));
                    float2 post=float2(abs(p.x+7.17)-1.94,p.y-(p.y < -3.69 ? -4.94 : -2.44));
                    float phase=_Time.y*.83;
                    float angle=atan2(post.y,post.x);
                    float r=length(post)+.038*sin(4*angle+phase);
                    float swash=(1-smoothstep(.055,.29,abs(quay-.18))) *
                        smoothstep(-.55,.78,sin(p.x*1.23+p.y*.72-phase));
                    float rim=(1-smoothstep(.035,.14,abs(r-.45))) *
                        smoothstep(-.78,.62,cos(angle-.62*phase));
                    float foam=.15*swash+.15*rim;
                    float shade=max(.25*(1-smoothstep(-.17,1.06,quay)),
                        .22*Shade(p-float2(-7.17,-3.41),float2(2.24,1.94)));
                    float alpha=foam+shade*(1-foam);
                    return half4((half3(.015,.067,.060)*shade*(1-foam)+half3(.54,.71,.64)*foam)/max(alpha,.0001),alpha);
                }
                if (_CompactLanding > .5)
                {
                    float2 p=i.mm;
                    float2 post=float2(abs(p.x)-4.60,p.y-(p.y < -2.27 ? -6.10 : 1.56));
                    float angle=atan2(post.y,post.x);
                    float tide=_Time.y*.79+p.y*.16;
                    float radius=length(post)+.075*sin(angle*3+tide);
                    float foam=(1-smoothstep(.07,.26,abs(radius-.92)))*.30;
                    foam+=(1-smoothstep(.06,.26,abs(radius-1.52-.09*sin(tide))))*.13;
                    foam*=smoothstep(-.68,.62,cos(angle+tide*.26));
                    float2 hull=(p-float2(-7.84,-1.34))/float2(1.55,5.16);
                    float hullRadius=length(hull);
                    foam+=(1-smoothstep(.025,.13,abs(hullRadius-1.02)))*
                        smoothstep(-.20,.85,sin(hull.y*8+tide*.30)+hull.x*.45)*.18;
                    float shade=.28*(1-smoothstep(.72,1.27,hullRadius));
                    shade=max(shade,.22*Shade(p-float2(.10,-2.20),float2(4.85,4.76)));
                    shade=max(shade,.17*Shade(p-float2(.23,6.64),float2(2.80,4.70)));
                    float alpha=foam+shade*(1-foam);
                    return half4((half3(.015,.072,.061)*shade*(1-foam)+half3(.46,.66,.58)*foam)/max(alpha,.0001),alpha);
                }
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
