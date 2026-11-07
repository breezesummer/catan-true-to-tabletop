Shader "Catan/PierTimber"
{
    Properties
    {
        [MainTexture] _BaseMap("木纹",2D)="white" {}
        _CargoContact("货堆接触阴影",2D)="white" {}
        _ContactCell("图集位置",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_CargoContact); SAMPLER(sampler_CargoContact);
            float4 _ContactCell;
            struct Attributes { float3 p:POSITION; float3 n:NORMAL; half4 pigment:COLOR; float2 grain:TEXCOORD0; float2 deck:TEXCOORD1; };
            struct Varyings { float4 clip:SV_POSITION; half3 albedo:COLOR; half3 normal:TEXCOORD0; float2 grain:TEXCOORD1; float height:TEXCOORD2; float2 contact:TEXCOORD3; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.clip=TransformObjectToHClip(v.p);
                o.normal=TransformObjectToWorldNormal(v.n);
                // FBX 顶点颜料以 sRGB 保存；进入线性光照前先解码。
                o.albedo=SRGBToLinear(v.pigment.rgb);
                o.height=TransformObjectToWorld(v.p).y;
                o.grain=v.grain;
                o.contact=(clamp(v.deck,.005,.995)+_ContactCell.xy)/float2(3,2);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                Light sun=GetMainLight();
                half diffuse=saturate(dot(normalize(i.normal),sun.direction));
                half grain=Luminance(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.grain).rgb);
                half detail=clamp(.49+grain*5.8,.58,1.38);
                half3 wet=lerp(half3(.33,.43,.40),half3(1,1,1),smoothstep(.0046,.0076,i.height));
                half contact=SAMPLE_TEXTURE2D(_CargoContact,sampler_CargoContact,i.contact).r;
                return half4(i.albedo*wet*detail*(.40+.69*diffuse)*contact,1);
            }
            ENDHLSL
        }
    }
}
