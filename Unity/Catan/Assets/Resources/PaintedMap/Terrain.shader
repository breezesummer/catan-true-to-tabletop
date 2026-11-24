Shader "Catan/PaintedMap"
{
    Properties
    {
        [MainTexture] _BaseMap("地形图集", 2D) = "white" {}
        _RoadEdge("共边土路",2D)="white" {}
        _RoadVerges("草石沙土路肩",2D)="white" {}
        _BeachEdge("沙滩岸线",2D)="white" {}
        _Tile("中心与地形", Vector) = (0,0,0,0)
        _RobberHex("强盗陆地", Vector) = (0,0,0,0)
        _RobberContact("强盗靴底接触阴影", 2D) = "white" {}
        _PirateHex("海盗海格", Vector) = (0,0,0,0)
        _Glow("本人地块与常态倍产", Vector) = (0,-100,0,0)
        [HideInInspector] _HarvestClock("收取显示时钟", Float) = 0
        _NearA("邻格一至四", Vector) = (-1,-1,-1,-1)
        _NearB("邻格五至六", Vector) = (-1,-1,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RoadEdge); SAMPLER(sampler_RoadEdge);
            TEXTURE2D(_RoadVerges); SAMPLER(sampler_RoadVerges);
            TEXTURE2D(_BeachEdge); SAMPLER(sampler_BeachEdge);
            TEXTURE2D(_RobberContact); SAMPLER(sampler_RobberContact);
            CBUFFER_START(UnityPerMaterial)
            float4 _Tile, _NearA, _NearB, _Glow, _RobberHex, _PirateHex;
            float _HarvestClock;
            CBUFFER_END
            #include "DangerHex.hlsl"
            struct Input { float4 pos : POSITION; float3 normal : NORMAL; };
            struct Output { float4 pos : SV_POSITION; float3 world : TEXCOORD0; float up : TEXCOORD1; };
            Output Vert(Input v)
            {
                Output o;
                o.world=TransformObjectToWorld(v.pos.xyz);
                o.pos=TransformWorldToHClip(o.world);
                o.up=TransformObjectToWorldNormal(v.normal).y;
                return o;
            }
            half3 Paint(float region,float2 uv)
            {
                float2 cell=float2(fmod(region,4),3-floor(region*.25));
                return SAMPLE_TEXTURE2D_GRAD(_BaseMap,sampler_BaseMap,(cell+clamp(uv,.008,.992))*.25,
                    ddx(uv)*.25,ddy(uv)*.25).rgb;
            }
            half4 Frag(Output i) : SV_Target
            {
                float2 delta=i.world.xz-_Tile.xy;
                // 原画路框中心约为 (0.5,0.51)，横向半宽约 0.405，纵向半径约 0.45。
                // 将框内完整地形放在路肩以内，最外一小段延展路肩底色，避免露出原画路框。
                float radius=max(abs(delta.x),.5*abs(delta.x)+.8660254*abs(delta.y));
                float2 terrainUV=float2(.5,.51)+delta*float2(.382,.425*.8660254)/max(.0328,radius);
                half3 color=Paint(_Tile.z,_Tile.z<8 ? terrainUV : .5+delta/.088);
                const float2 dirs[6]={float2(1,0),float2(.5,.8660254),float2(-.5,.8660254),
                    float2(-1,0),float2(-.5,-.8660254),float2(.5,-.8660254)};
                if (_Tile.z<8)
                {
                    int side=0;
                    float projected=dot(delta,dirs[0]);
                    [unroll] for(int j=1;j<6;j++)
                    {
                        float candidate=dot(delta,dirs[j]);
                        if(candidate>projected){projected=candidate;side=j;}
                    }
                    float inward=.034641016-projected;
                    float neighbor=side<4?_NearA[min(side,3)]:_NearB[max(side-4,0)];
                    if((neighbor<0 || neighbor==8) && i.up<.65) discard;
                    float2 normal=dirs[side], tangent=float2(-normal.y,normal.x);
                    float along=dot(delta,tangent)/.04;
                    if(inward<.010)
                    {
                        // 共边中点和正向切线在两块地上相同，路心与宽度在接缝处连续。
                        float orientation=side<3?1:-1;
                        float u=saturate(.5+along*orientation);
                        float2 mid=_Tile.xy+normal*.034641016;
                        float phase=dot(mid,float2(94,137));
                        float ends=smoothstep(0,.20,min(u,1-u));
                        float bend=.00023*sin(3.141593*u)*sin(3.141593*u+phase);
                        float width=.0078*(1+ends*.05*sin(7*u+phase));
                        if(neighbor>=0 && neighbor!=8)
                        {
                            // 三路相接处摊开一小块裸土；两侧路肩由各自的地表颜色自然收边。
                            float junction=1-smoothstep(0,.18,min(u,1-u));
                            float grain=sin(dot(i.world.xz,float2(361,249)))*
                                        sin(dot(i.world.xz,float2(-187,473)));
                            float halfLane=.00115+.00115*junction+.00016*grain;
                            float distance=abs(orientation*inward-bend);
                            // 路心仍用验收土路；两侧分别沿本格地形铺入草丛、碎石或沙土笔触。
                            float reach=(_Tile.z==5 ? .0033 : .0027)*(1+.24*grain);
                            float verge=(distance-halfLane*.55)/reach;
                            if(verge<1)
                            {
                                float soilV=.50+.072*clamp((orientation*inward-bend)/halfLane,-1,1);
                                // 路口切换边方向时 UV 跳变，显式使用当前边的梯度以免误取粗级透明纹理。
                                half3 earth=SAMPLE_TEXTURE2D_GRAD(_RoadEdge,sampler_RoadEdge,float2(lerp(.10,.90,u),soilV),float2(dot(ddx(i.world.xz),tangent)*20,0),float2(dot(ddy(i.world.xz),tangent)*20,0)).rgb;
                                float row=_Tile.z==5 ? 2 : (_Tile.z==1 ? 3 : ((_Tile.z==4 || _Tile.z==6) ? 1 : 0));
                                float alongVerge=1-abs(2*frac(u*.85+phase*.13+row*.21)-1);
                                float2 vergeUV=float2(alongVerge,1-(row+(1+30*saturate(verge))/32)*.25);
                                half3 shoulder=SAMPLE_TEXTURE2D(_RoadVerges,sampler_RoadVerges,vergeUV).rgb;
                                if(_Tile.z==2)
                                {
                                    float bunch=smoothstep(.22,.62,sin(u*29+phase)*.65+sin(u*51-phase)*.35);
                                    float blades=smoothstep(-.55,.5,sin(u*192+verge*7)*.65+sin(u*117-verge*4)*.35);
                                    float grass=bunch*blades*smoothstep(.07,.38,verge);
                                    shoulder=lerp(earth,lerp(shoulder,color,.84),grass);
                                    verge+=grass*.24;
                                }
                                if(_Tile.z==1 || _Tile.z==4)
                                {
                                    // 只在坡脚一侧画散石。相邻小块各有偏移，空出的路肩露出本地土色。
                                    float2 grid=float2(u*30+phase,verge*3.1);
                                    float2 cell=floor(grid);
                                    float2 seed=frac(sin(float2(dot(cell,float2(12.7,37.1)),dot(cell,float2(25.3,17.9))))*4375.3);
                                    float2 p=frac(grid)-(.3+.4*seed);
                                    p.x+=p.y*(seed.y-.5);
                                    float shape=abs(p.x)*1.25+abs(p.y);
                                    float cluster=saturate(.5+.32*sin(u*25+phase)+.23*sin(u*47-phase));
                                    float radius=(.15+.28*seed.x)*smoothstep(.25,.72,cluster);
                                    float aa=max(.035,fwidth(grid.x)*.45);
                                    float stone=(1-smoothstep(radius-aa,radius+aa,shape))*smoothstep(.08,.35,verge);
                                    half3 mud=earth*(_Tile.z==1 ? half3(.80,.61,.49) : half3(.79,.76,.70));
                                    half3 mineral=_Tile.z==1 ? half3(.43,.15,.069) : half3(.32,.32,.28);
                                    shoulder=lerp(lerp(mud,shoulder,.23),mineral*(1.0-.42*sign(p.y+.42*p.x)),stone);
                                }
                                half3 track=lerp(earth,shoulder,smoothstep(0,.43,verge));
                                color=lerp(track,color,smoothstep(.34,1,verge));
                            }
                        }
                        else
                        {
                            // 沙面兼作沿岸土路。岸带向陆侧收，在三岔口回到公共顶点。
                            float shoreWidth=width*.92;
                            float center=.30*shoreWidth*smoothstep(0,.42,min(u,1-u))+orientation*bend;
                            float v=.47+(inward-center)/shoreWidth;
                            if(v<1)
                            {
                                float2 uv=saturate(float2(.5+along,v));
                                half4 sand=SAMPLE_TEXTURE2D(_BeachEdge,sampler_BeachEdge,uv);
                                if(v<.42) clip(sand.a-.12);
                                color=lerp(color,sand.rgb,sand.a);
                            }
                        }
                    }
                }
                // 同一地块上的六只靴底与杖端各自压暗脚下，边缘渐隐。
                if (_RobberHex.z>.5 && all(abs(_Tile.xy-_RobberHex.xy)<.001))
                    color*=SAMPLE_TEXTURE2D(_RobberContact,sampler_RobberContact,.5+delta/.08).r;
                half sunlit=MainLightRealtimeShadow(TransformWorldToShadowCoord(i.world));
                color*=lerp(half3(.57,.62,.67),half3(1,1,1),sunlit);
                return half4(color*lerp(.70,1,saturate(i.up)),1);
            }
            ENDHLSL
        }
    }
}
