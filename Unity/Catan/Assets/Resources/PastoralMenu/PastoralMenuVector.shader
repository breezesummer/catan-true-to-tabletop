Shader "Hidden/Catan/PastoralVector"
{
    Properties { _SrcBlend ("Source blend", Float) = 5 _DstBlend ("Destination blend", Float) = 10 _Cull ("Cull", Float) = 0 _ZWrite ("Depth write", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            Cull Off ZWrite Off ZTest Always
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; };
            struct v2f { float4 position:SV_POSITION; fixed4 color:COLOR; };
            v2f vert(appdata v) { v2f o; o.position=UnityObjectToClipPos(v.vertex); o.color=v.color; return o; }
            fixed4 frag(v2f i):SV_Target { return i.color; }
            ENDCG
        }
    }
}
