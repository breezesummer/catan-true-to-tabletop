Shader "Catan/FishingWater"
{
    Properties { [MainTexture] _BaseMap("浅水砂石",2D)="white" {} }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            float4 _ShoreA,_ShoreB;
            float _Width;
            float2 ShoreDistance(float2 p,float4 edge)
            {
                float2 v=edge.zw-edge.xy;
                float u=saturate(dot(p-edge.xy,v)/dot(v,v));
                return float2(length(p-edge.xy-v*u),u);
            }
            struct Input {float4 p:POSITION;float2 uv:TEXCOORD0;float2 ends:TEXCOORD1;};
            struct Output {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;float2 ends:TEXCOORD2;};
            Output Vert(Input i){Output o;float3 p=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(p);o.world=p.xz;o.uv=i.uv;o.ends=i.ends;return o;}
            half4 Frag(Output i):SV_Target
            {
                float2 p=i.world;
                float fringe=sin(p.x*236+p.y*171)*.06+cos(p.y*377-p.x*92)*.035;
                float2 first=ShoreDistance(p,_ShoreA),second=ShoreDistance(p,_ShoreB);
                float depth=min(first.x,second.x)/_Width+fringe;
                float endFade=first.x<second.x?smoothstep(0,.14,first.y):smoothstep(0,.14,1-second.y);
                half3 bed=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p*21).rgb;
                float moss=smoothstep(.67,.86,sin(p.x*314+sin(p.y*171))*cos(p.y*291+sin(p.x*163))*.5+.5);
                half3 color=lerp(bed*half3(.65,1.45,1.03),half3(.015,.33,.265),smoothstep(.12,1,depth)*.77);
                color=lerp(color,half3(.055,.20,.09),moss*.36);
                float caustic=pow(saturate(sin(p.x*670+sin(p.y*250+_Time.y*.3)*2)*cos(p.y*720-p.x*260-_Time.y*.25)),12);
                color+=caustic*half3(.10,.18,.13);
                float ends=lerp(1,smoothstep(0,.18,i.uv.x),i.ends.x)*lerp(1,smoothstep(0,.18,1-i.uv.x),i.ends.y);
                float alpha=(1-smoothstep(.54,.94,depth))*ends*endFade*.83*smoothstep(0,.16,i.uv.y)*smoothstep(1,.88,i.uv.y);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
