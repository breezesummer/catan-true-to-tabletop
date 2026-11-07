Shader "Catan/PieceTargetOutline"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Cull Front ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct output { float4 pos:SV_POSITION; };
            output vert(appdata v)
            {
                output o;
                o.pos=UnityObjectToClipPos(v.vertex);
                float3 n=mul((float3x3)UNITY_MATRIX_V,UnityObjectToWorldNormal(v.normal));
                float2 dir=n.xy;
                o.pos.xy+=dir/max(length(dir),0.001)*3.4*2.0/_ScreenParams.xy*o.pos.w;
                return o;
            }
            half4 frag(output i):SV_Target
            {
                return half4(0.08,1.0,0.30,0.84+0.13*sin(_Time.y*4));
            }
            ENDHLSL
        }
    }
}
