Shader "Catan/PierTimber"
{
    Properties
    {
        [MainTexture] _BaseMap("木纹",2D)="white" {}
        [HideInInspector] _Cull("背面剔除",Float)=2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Attributes { float3 p:POSITION; float3 n:NORMAL; half4 pigment:COLOR; float2 grain:TEXCOORD0; };
            struct Varyings { float4 clip:SV_POSITION; half3 albedo:COLOR; half3 normal:TEXCOORD0; float2 grain:TEXCOORD1; float3 world:TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.clip=TransformObjectToHClip(v.p);
                o.normal=TransformObjectToWorldNormal(v.n);
                // FBX 顶点颜料以 sRGB 保存；进入线性光照前先解码。
                o.albedo=SRGBToLinear(v.pigment.rgb);
                o.world=TransformObjectToWorld(v.p);
                o.grain=v.grain;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half diffuse=saturate(dot(normalize(i.normal),sun.direction));
                half grain=Luminance(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.grain).rgb);
                half detail=clamp(.49+grain*5.8,.58,1.38);
                half3 wet=lerp(half3(.33,.43,.40),half3(1,1,1),smoothstep(.0046,.0076,i.world.y));
                return half4(i.albedo*wet*detail*(half3(.35,.38,.40)+.55*sun.color*diffuse*sun.shadowAttenuation),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
