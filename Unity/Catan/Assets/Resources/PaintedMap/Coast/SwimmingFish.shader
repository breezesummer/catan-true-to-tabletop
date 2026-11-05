Shader "Catan/SwimmingFish"
{
    Properties { [MainTexture] _BaseMap("银鱼",2D)="white" {} }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+1" "RenderType"="Transparent"}
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Input {float4 p:POSITION;float2 uv:TEXCOORD0;float4 center:TEXCOORD1;float4 course:TEXCOORD2;};
            struct Output {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float depth:TEXCOORD1;float phase:TEXCOORD2;};
            Output Vert(Input i)
            {
                Output o;
                float t=_Time.y*(.28+.025*sin(i.center.z))+i.center.z;
                float2 along=i.course.xy, across=float2(-along.y,along.x);
                float2 location=i.center.xy+along*cos(t)*i.course.w*.065+across*sin(t)*i.course.w*.025;
                float2 heading=normalize(-along*sin(t)*.065+across*cos(t)*.025);
                float2 side=float2(-heading.y,heading.x);
                float2 local=(i.uv-.5)*float2(i.center.w,i.center.w*.36);
                float2 world=location+heading*local.x+side*local.y;
                o.p=TransformWorldToHClip(float3(world.x,.0068,world.y));
                o.uv=i.uv;o.depth=i.course.z;o.phase=i.center.z;
                return o;
            }
            half4 Frag(Output i):SV_Target
            {
                float tail=pow(1-i.uv.x,2);
                float2 uv=i.uv;
                uv.y+=sin(_Time.y*5.0+i.phase+uv.x*5)*tail*.055;
                half4 ink=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
                half gray=dot(ink.rgb,half3(.30,.59,.11));
                ink.rgb=lerp(lerp(ink.rgb,gray.xxx,.24)*1.38,half3(.025,.32,.31),i.depth*.12);
                ink.a*=.97-i.depth*.20;
                return ink;
            }
            ENDHLSL
        }
    }
}
