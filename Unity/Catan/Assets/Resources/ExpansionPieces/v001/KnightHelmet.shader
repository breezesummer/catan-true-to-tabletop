Shader "Catan/骑士盔漆"
{
    Properties {
        _BaseMap("原金属纹理", 2D) = "white" {}
        _Pigment("玩家色", Color) = (1,1,1,1)
        _Helmet("盔壳底高、顶高、半径", Vector) = (0,0,0,0)
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _Pigment, _Helmet;
            CBUFFER_END
            struct V { float3 pos:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct F { float4 clip:SV_POSITION; float3 local:TEXCOORD0; float3 world:TEXCOORD1; float3 normal:TEXCOORD2; float2 uv:TEXCOORD3; };
            F Vert(V v) {
                F o; o.local=v.pos; o.world=TransformObjectToWorld(v.pos);
                o.clip=TransformWorldToHClip(o.world); o.normal=TransformObjectToWorldNormal(v.normal); o.uv=v.uv; return o;
            }
            half4 Frag(F i):SV_Target {
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                bool lacquer = i.local.y>=_Helmet.x && i.local.y<=_Helmet.y && length(i.local.xz)<_Helmet.z;
                if(lacquer)
                    albedo=_Pigment.rgb*clamp(.65+Luminance(albedo),.65,1.2);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=albedo; surface.metallic=lacquer ? .06 : .49; surface.smoothness=lacquer ? .28 : .48; surface.occlusion=1; surface.alpha=1;
                InputData input=(InputData)0;
                input.positionWS=i.world; input.normalWS=normalize(i.normal); input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);
                input.shadowCoord=TransformWorldToShadowCoord(i.world); input.bakedGI=SampleSH(input.normalWS);
                return UniversalFragmentPBR(input,surface);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
