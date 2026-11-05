Shader "Catan/FacilityPrint"
{
    Properties { _MainTex ("Print", 2D) = "white" {} }
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
            fixed4 frag(Output i) : SV_Target { fixed4 ink = tex2D(_MainTex, i.uv);
                #ifndef UNITY_COLORSPACE_GAMMA
                ink.rgb = LinearToGammaSpace(ink.rgb);
                #endif
                return ink; }
            ENDCG
        }
    }
}
