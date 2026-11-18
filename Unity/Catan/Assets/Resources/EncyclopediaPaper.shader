Shader "Catan/百科原色贴图"
{
    Properties { _MainTex ("图像", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(appdata_img v)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }
            // sRGB 纹理由采样器解码，目标缓冲负责最终编码。
            half4 frag(Varyings i) : SV_Target { return tex2D(_MainTex, i.uv); }
            ENDCG
        }
    }
}
