Shader "Catan/DiceSelection"
{
    Properties { _Chosen ("已选", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Offset -1, -1
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local __ FLAT_FACE
            #include "UnityCG.cginc"
            float _Chosen;
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 local:TEXCOORD1; };
            V vert(appdata_base v)
            {
                V o;
                o.pos=UnityObjectToClipPos(v.vertex);
                o.local=v.vertex.xyz;
                o.uv=v.texcoord.xy;
                return o;
            }
            fixed4 frag(V i):SV_Target
            {
                float2 uv=i.uv;
                float2 q=abs(uv-.5)-.32;
                float distanceIn=.18-length(max(q,0))-min(max(q.x,q.y),0);
                float angle=atan2(uv.y-.5,uv.x-.5);
                float level=uv.y;
                #ifndef FLAT_FACE
                distanceIn=min(min(uv.x,1-uv.x),min(uv.y,1-uv.y));
                float3 p=i.local/max(max(abs(i.local.x),abs(i.local.y)),max(abs(i.local.z),.000001));
                angle=atan2(p.z,p.x);
                level=p.y*.5+.5;
                #else
                clip(distanceIn);
                #endif
                float phase=_Time.y*3.2;
                float pulse=.90+.10*sin(phase*.9);
                float traveling=.5+.5*cos(angle-phase);
                float rim=exp(-max(0,distanceIn)*lerp(10,17,_Chosen));
                float core=exp(-max(0,distanceIn)*35);
                float path=.49+.26*sin(angle-phase);
                float ribbon=exp(-abs(level-path)*lerp(9,25,_Chosen))*lerp(lerp(.68,.18,_Chosen),1,traveling*traveling);
                float intensity=lerp(pulse,1,_Chosen);
                float alpha=saturate(rim*lerp(1.9,1.5,_Chosen)+ribbon*lerp(1.0,.82,_Chosen))*intensity;
                float3 tint=lerp(float3(0,.86,.89),float3(1,.60,.055),_Chosen);
                return float4(lerp(tint,1,saturate(core*lerp(.14,.75,_Chosen)+ribbon*lerp(.12,.35,_Chosen))),alpha);
            }
            ENDCG
        }
    }
}
