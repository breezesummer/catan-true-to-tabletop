Shader "Catan/PaintedMap"
{
    Properties
    {
        [MainTexture] _BaseMap("地形图集", 2D) = "white" {}
        _Gravel("潮间带砂砾", 2D) = "white" {}
        _Sand("沙滩",2D)="white" {}
        _Tile("中心与地形", Vector) = (0,0,0,0)
        _RobberHex("强盗陆地", Vector) = (0,0,0,0)
        _PirateHex("海盗海格", Vector) = (0,0,0,0)
        _Glow("本人地块与收取时间", Vector) = (0,-100,0,0)
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Gravel); SAMPLER(sampler_Gravel);
            TEXTURE2D(_Sand); SAMPLER(sampler_Sand);
            #include "Coast/CoastCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tile, _NearA, _NearB, _Glow, _RobberHex, _PirateHex;
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
            half3 Paint(float region, float2 delta, float2 dx, float2 dy)
            {
                float2 cell=float2(fmod(region,4),3-floor(region*.25));
                float2 uv=clamp(.5+delta/.088,.008,.992);
                return SAMPLE_TEXTURE2D_GRAD(_BaseMap,sampler_BaseMap,(cell+uv)*.25,dx,dy).rgb;
            }
            half4 Frag(Output i) : SV_Target
            {
                float2 delta=i.world.xz-_Tile.xy;
                float2 dx=ddx(i.world.xz)/.352, dy=ddy(i.world.xz)/.352;
                half3 color=Paint(_Tile.z,delta,dx,dy);
                float weight=1, shore=0;
                float beach=BeachAmount(i.world.xz);
                const float2 dirs[6]={float2(1,0),float2(.5,.8660254),float2(-.5,.8660254),
                    float2(-1,0),float2(-.5,-.8660254),float2(.5,-.8660254)};
                if (_Tile.z!=8)
                {
                    [unroll] for(int j=0;j<6;j++)
                    {
                        float distance=.034641016-dot(delta,dirs[j]);
                        float neighbor=j<4?_NearA[min(j,3)]:_NearB[max(0,j-4)];
                        float band=(neighbor<0||neighbor==8)?.014f:.0048+.0012*sin(i.world.x*380+i.world.z*225);
                        float blend=1-smoothstep(0,band,distance);
                        if(blend>0)
                        {
                            if(neighbor<0 || neighbor==8)
                            {
                                float width=lerp(.0038,.0105,beach)*(1+ShoreVariation(i.world.xz)*.22);
                                shore=max(shore,1-smoothstep(0,width,distance));
                            }
                            else
                            {
                                color+=blend*Paint(neighbor,delta-dirs[j]*.069282032,dx,dy);
                                weight+=blend;
                            }
                        }
                    }
                }
                color/=weight;
                // 各个岛屿共用世界坐标沙岸纹理，三格交点处保持同一笔触。
                if(shore>0)
                {
                    float chips=sin(i.world.x*1320+i.world.z*530)*cos(i.world.z*910-i.world.x*740);
                    float fleck=sin(i.world.z*390+cos(i.world.x*320)*1.7);
                    // 仅侵蚀贴海的最后一小段颜料，内陆拼缝和规则顶点位置保持精确。
                    clip(1-shore-(.012+.075*saturate(.42+.4*fleck+.18*chips)));
                    half3 rock=SAMPLE_TEXTURE2D(_Gravel,sampler_Gravel,i.world.xz*23).rgb;
                    half3 sand=SAMPLE_TEXTURE2D(_Sand,sampler_Sand,i.world.xz*16).rgb;
                    sand=lerp(rock,sand,beach);
                    sand*=lerp(1,.64,smoothstep(.72,.96,shore));
                    color=lerp(color,sand,smoothstep(.05,.83,shore+.10*fleck));
                }
                float4 hazard = _Tile.z == 8 ? _PirateHex : _RobberHex;
                if (_CatanMapMarksHidden < .5 && i.up > .5 && hazard.z > .5 && all(abs(_Tile.xy - hazard.xy) < .001))
                    color = DangerHex(color,delta);
                else if (_Glow.x > .5 && _CatanMapMarksHidden < .5 && i.up > .5)
                {
                    float2 d = abs(delta);
                    float edge = .034641016 - max(d.x, dot(d, float2(.5,.8660254)));
                    float aa = max(.00018, fwidth(edge) * .6);
                    float edgeStroke = 1 - smoothstep(.00018, .00052 + aa, abs(edge - .0030));
                    float halo = exp2(-abs(edge - .0030) * 950);
                    float age = _Time.y - _Glow.y;
                    float flash = smoothstep(0,.16,age) * (1 - smoothstep(.73,1.65,age));
                    float inner = 1 - smoothstep(.00018,.00048 + aa, abs(edge - lerp(.0038,.0063,saturate(age * 3.5))));
                    half3 jade = half3(.16,.88,.58);
                    half3 gold = half3(1,.52,.055);
                    color = lerp(color, lerp(jade,gold,flash), saturate(.16*halo + edgeStroke*lerp(.71,.98,flash)));
                    color += halo * lerp(jade*.085,gold*.22,flash);
                    color = lerp(color,half3(1,.80,.23),inner*flash*.94);
                }
                return half4(color*lerp(.70,1,saturate(i.up)),1);
            }
            ENDHLSL
        }
    }
}
