Shader "Catan/PortraitInk"
{
    Properties { _MainTex ("Portrait", 2D) = "white" {} }
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
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            Output vert(Input v) { Output o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            fixed4 frag(Output i) : SV_Target
            {
                fixed4 art=tex2D(_MainTex,i.uv);
                float x=(i.uv.x-.08)/.8275;
                #ifndef UNITY_COLORSPACE_GAMMA
                art.rgb=LinearToGammaSpace(art.rgb);
                #endif
                float side=smoothstep(0,.15,x)*smoothstep(0,.10,1-x);
                // 顶部使用原始透明轮廓。只在上框以内渐隐肩侧及底沿。
                float body=side*smoothstep(0,.12,i.uv.y);
                art.a*=lerp(body,1,smoothstep(.84,.93,i.uv.y));
                return art;
            }
            ENDCG
        }
    }
}
