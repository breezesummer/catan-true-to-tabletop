Shader "Catan/地块荧光"
{
    Properties { _Center("地块中心与金色",Vector)=(0,0,0,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Center, _RobberHex;
            float _FlowClock;
            CBUFFER_END
            float _CatanMapMarksHidden;
            struct Input { float4 position:POSITION; };
            struct Output { float4 position:SV_POSITION; float2 delta:TEXCOORD0; };
            Output Vert(Input i)
            {
                Output o;
                float3 world=TransformObjectToWorld(i.position.xyz);
                o.position=TransformWorldToHClip(world);
                o.delta=world.xz-_Center.xy;
                return o;
            }
            half4 Frag(Output i):SV_Target
            {
                if(_CatanMapMarksHidden>.5 || (_Center.w<.5 && _RobberHex.z>.5 && all(abs(_Center.xy-_RobberHex.xy)<.001))) discard;
                float2 p=i.delta, a=abs(p);
                float hex=max(a.x,dot(a,float2(.5,.8660254)));
                // 柔化转角，让光丝贴边拐弯而非形成僵硬折线。
                float corner=saturate(.5+.5*(a.x-dot(a,float2(.5,.8660254)))/.002);
                float edge=lerp(dot(a,float2(.5,.8660254)),a.x,corner)+.002*corner*(1-corner)-.034641016;
                // 相邻边共用同一周长坐标，光头越过六个角时连续、不折返。
                float angle=atan2(p.y,p.x);
                float side=floor(angle/1.04719755+.5);
                float theta=side*1.04719755;
                float2 perimeter=p*(.034641016/max(hex,.0001));
                float u=(side+dot(perimeter,float2(-sin(theta),cos(theta)))/.04)/6;
                float seed=dot(_Center.xy,float2(11.7,23.1));
                float t=_FlowClock;
                float wave=sin(u*25.132741-t*2.1+seed);
                float wave2=sin(u*43.982297+t*1.7+seed);
                float path=edge+.0009+.0015*wave;
                float aa=max(.00010,fwidth(edge)*.6);
                float pulse=frac(u*3-t*.63+seed);
                float comet=exp2(-pulse*7);
                float head=exp2(-pow(min(pulse,1-pulse)*21,2));
                float width=.00020+aa+.00022*comet;
                float core=exp2(-pow(path/width,2));
                float silkA=exp2(-pow((edge+.0014+.0022*wave2)/(.00013+aa),2));
                float silkB=exp2(-pow((edge+.0007+.0024*sin(u*56.54867-t*2.7+seed))/ (.00012+aa),2));
                float nearGlow=exp2(-abs(path)*720);
                float mist=exp2(-pow(path/.0042,2));
                float flow=.30+.95*comet+.8*head;
                half3 tint=lerp(half3(.015,1,.36),half3(1,.48,.015),_Center.z);
                half3 light=lerp(half3(.45,1,.77),half3(1,.89,.43),_Center.z);
                tint=lerp(tint,half3(1,.018,.028),_Center.w);
                light=lerp(light,half3(1,.24,.19),_Center.w);
                half3 emission=tint*(nearGlow*.27+mist*.10)*flow;
                emission+=light*core*flow*1.2;
                emission+=tint*(silkA+silkB)*(.35+.85*comet);
                emission+=light*head*nearGlow*.65;
                // 光丝附近的细小荧光亮点随周长方向一起移动。
                float beads=pow(saturate(sin((u-t*.21)*339.292+seed)),24);
                emission+=light*beads*exp2(-pow((path-.0022*wave2)/(.00016+aa),2))*.32;
                return half4(emission,1);
            }
            ENDHLSL
        }
    }
}
