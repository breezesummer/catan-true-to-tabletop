Shader "Hidden/Catan/FishingNumberWaterPatch"
{
 Properties { _MainTex("原照",2D)="white"{} _Circle("点数范围",Vector)=(.716,.46,.125,.175) }
 SubShader { Cull Off ZWrite Off ZTest Always
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex; float4 _Circle;
 fixed4 frag(v2f_img i):SV_Target
 {
   float2 p=i.uv;
   float mask=1-smoothstep(.92,1,length((p-_Circle.xy)/_Circle.zw));
   // 用原照另一侧同位置的水纹补回数字区域，原图资源不变。
   return lerp(tex2D(_MainTex,p),tex2D(_MainTex,p+float2(.29-_Circle.x,0)),mask);
 }
 ENDCG }
 }
}
