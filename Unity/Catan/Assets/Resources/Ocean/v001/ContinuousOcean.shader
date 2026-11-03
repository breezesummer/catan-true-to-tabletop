Shader "Catan/ContinuousOcean"
{
    Properties
    {
        _CoastMask("公开海岸", 2D) = "white" {}
        _MaskRect("遮罩范围", Vector) = (0,0,1,1)
        _WaterHeight("水位", Float) = 0.0045
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_CoastMask); SAMPLER(sampler_CoastMask);
            CBUFFER_START(UnityPerMaterial)
            float4 _MaskRect;
            float _WaterHeight;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; };
            float2 Coast(float2 p)
            {
                float2 uv=(p-_MaskRect.xy)/_MaskRect.zw;
                if(any(uv<0)||any(uv>1)) return float2(1,0);
                return SAMPLE_TEXTURE2D_LOD(_CoastMask,sampler_CoastMask,uv,0).rg;
            }
            float Wave(float2 p, float t)
            {
                return sin(dot(p,float2(26,17))-t*.85)*.0008 + sin(dot(p,float2(-19,34))-t*1.1)*.0004;
            }
            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 p=TransformObjectToWorld(input.positionOS.xyz);
                float2 coast=Coast(p.xz);
                p.y=_WaterHeight+Wave(p.xz,_Time.y)*smoothstep(.06,.45,coast.r)*(1-coast.g*.8);
                o.world=p; o.positionCS=TransformWorldToHClip(p); return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 p=i.world.xz;
                float t=_Time.y;
                float2 coast=Coast(p);
                float d=coast.r*.08;
                float a=dot(p,float2(26,17))-t*.85;
                float b=dot(p,float2(-19,34))-t*1.1;
                float ripple=sin(p.x*181+p.y*143+sin(p.y*51-t*.6)*1.5-t*1.2);
                float ripple2=cos(p.x*109-p.y*207+sin(p.x*42+t*.3)-t*.8);
                float damp=smoothstep(.002,.025,d);
                float2 slope=(cos(a)*float2(26,17)*.0008+cos(b)*float2(-19,34)*.0004)*damp;
                slope+=float2(ripple,ripple2)*.16*lerp(.3,1,damp);
                float3 n=normalize(float3(-slope.x,1,-slope.y));
                float3 view=GetWorldSpaceNormalizeViewDir(i.world);
                Light sun=GetMainLight();
                float fresnel=.04+.40*pow(1-saturate(dot(n,view)),4);
                float spec=pow(saturate(dot(n,normalize(sun.direction+view))),80);
                float broad=sin(p.x*12+sin(p.y*18)*.6+t*.08)*sin(p.y*16-t*.07);
                float3 water=lerp(float3(.16,.40,.43),float3(.045,.19,.26),smoothstep(0,.065,d));
                water+=broad*float3(.004,.009,.012)+(ripple+ripple2)*.0025;
                float3 sky=lerp(float3(.18,.32,.42),float3(.55,.68,.74),saturate(n.z*3+.5));
                water=lerp(water,sky,fresnel)+sun.color*spec*.32;
                float foamBand=sin(d*950+t*1.25+sin(p.x*74+p.y*83)*.65);
                float foam=smoothstep(.78,.97,foamBand)*(1-smoothstep(.002,.013,d));
                foam*=smoothstep(.0003,.002,d)*smoothstep(-.15,.65,sin(p.x*267+p.y*193+t*.22)*sin(p.y*163-p.x*97+t*.31));
                water=lerp(water,float3(.68,.80,.76),foam*.65);
                water=lerp(water,float3(.34,.56,.58),coast.g*.42);
                return half4(water,1);
            }
            ENDHLSL
        }
    }
}
