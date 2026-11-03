Shader "Catan/OceanReef"
{
    Properties { _RockDetail("海蚀灰岩", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_RockDetail); SAMPLER(sampler_RockDetail);
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct V {float4 positionCS:SV_POSITION;float3 p:TEXCOORD0;float3 n:TEXCOORD1;float3 local:TEXCOORD2;};
            float Hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
            float Noise(float3 p)
            {
                float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(i),Hash(i+float3(1,0,0)),f.x),lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),lerp(Hash(i+float3(0,1,1)),Hash(i+1),f.x),f.y),f.z);
            }
            V Vert(A a){V o;o.p=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.p);o.n=TransformObjectToWorldNormal(a.normalOS);o.local=a.positionOS.xyz;return o;}
            half4 Frag(V i):SV_Target
            {
                float3 p=i.local;
                float grain=Noise(p*370)*.6+Noise(p*1130)*.4;
                // 模型不增加UV接缝；三个方向投射同一灰岩贴图，边缘小面积也保留矿物细节。
                float3 blend=pow(abs(normalize(TransformWorldToObjectDir(i.n))),4);blend/=max(.001,blend.x+blend.y+blend.z);
                float3 rock=SAMPLE_TEXTURE2D(_RockDetail,sampler_RockDetail,p.yz*96).rgb*blend.x
                    +SAMPLE_TEXTURE2D(_RockDetail,sampler_RockDetail,p.xz*96).rgb*blend.y
                    +SAMPLE_TEXTURE2D(_RockDetail,sampler_RockDetail,p.xy*96).rgb*blend.z;
                rock*=lerp(.82,1.18,grain);
                float up=saturate(normalize(i.n).y);
                // 受光岩面偏浅米灰，背光面保留海水反射的青灰色。
                rock=lerp(rock*float3(.77,.91,.93),rock*1.32+float3(.09,.085,.065),smoothstep(.12,.9,up));
                float wet=1-smoothstep(.005,.025,i.p.y);
                rock*=lerp(float3(1,1,1),float3(.36,.55,.56),wet);
                Light sun=GetMainLight();float light=.47+.7*saturate(dot(normalize(i.n),sun.direction));
                return half4(rock*sun.color*light,1);
            }
            ENDHLSL
        }
    }
}
