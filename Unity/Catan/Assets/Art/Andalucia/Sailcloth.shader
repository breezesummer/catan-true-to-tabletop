Shader "Catan/双面亚麻帆"
{
    Properties
    {
        [MainTexture] _BaseMap("帆布裁片", 2D)="white"{}
        [MainColor] _BaseColor("玩家染色", Color)=(1,1,1,1)
        [Normal] _BumpMap("织纹与张力", 2D)="bump"{}
        _BumpScale("褶纹强度", Range(0,2))=1
        _Smoothness("光滑度", Range(0,1))=.16
        [HideInInspector] _Metallic("金属度", Float)=0
        [HideInInspector] _Cull("双面", Float)=0
        [HideInInspector] _ZWrite("深度写入", Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment SailFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #define _NORMALMAP 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            half4 SailFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData cloth;
                InitializeStandardLitSurfaceData(input.uv, cloth);
                InputData data;
                InitializeInputData(input, cloth.normalTS, data);
                // 翻到帆背时，褶皱两侧的法线一起翻转。
                data.normalWS *= IS_FRONT_VFACE(face, 1.0h, -1.0h);
                data.bakedGI = SampleSH(data.normalWS);
                data.shadowMask = half4(1,1,1,1);
                half4 color = UniversalFragmentPBR(data, cloth);
                Light sun = GetMainLight(data.shadowCoord, data.positionWS, data.shadowMask);
                half backlight = saturate(-dot(data.normalWS, sun.direction));
                // 透过纤维的主光保留染料颜色，并受桅杆和其他帆面遮挡。
                color.rgb += cloth.albedo * sun.color * (.30h * backlight)
                           * sun.shadowAttenuation * sun.distanceAttenuation;
                color.rgb = MixFog(color.rgb, data.fogCoord);
                return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
