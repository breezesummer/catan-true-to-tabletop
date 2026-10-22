Shader "Hidden/Catan/LivingLandscape"
{
    Properties { _MainTex ("风景原画", 2D) = "white" {} _Clock ("展示时钟",Float)=0 _Theme ("主题",Float)=0 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Clock, _Theme;
            float oval(float2 p,float2 c,float2 r) { return 1-smoothstep(.64,1,length((p-c)/r)); }
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv=i.uv,p=float2(uv.x,1-uv.y);float t=_Clock;
                float water=0,grass=0,leaves=0;
                if(_Theme>4.5)
                {
                    // 原版港湾：动画限定在开阔水域、前景麦田和左上树叶，避开城堡与桥梁。
                    water=max(oval(p,float2(.88,.385),float2(.105,.052)),oval(p,float2(.90,.605),float2(.092,.095)));
                    water=max(water,oval(p,float2(.79,.713),float2(.065,.025)));
                    grass=smoothstep(.79,.87,p.y)*smoothstep(.52,.67,p.x);
                    leaves=(1-smoothstep(.32,.51,p.x))*(1-smoothstep(.15,.28,p.y));
                }
                else if(_Theme<.5)water=oval(p,float2(.53,.63),float2(.18,.24));
                else if(_Theme<1.5)grass=smoothstep(.58,.78,p.y)*smoothstep(.26,.40,p.x);
                else if(_Theme<2.5){water=oval(p,float2(.79,.78),float2(.23,.28));leaves=(1-smoothstep(.25,.51,p.x))*smoothstep(.12,.8,p.y);}
                else if(_Theme>3.5)water=oval(p,float2(.57,.73),float2(.31,.27));
                float wave=sin(p.y*185-t*1.75+sin(p.x*21+t*.5))*.0016+sin(p.y*317+t*1.1)*.00065;
                uv.x+=wave*water;
                uv.y+=cos(p.x*47+p.y*73-t*1.4)*.00085*water;
                // 麦穗与林木局部随风弯曲，整体镜头保持稳定。
                uv.x+=(sin(p.x*21+p.y*9-t*1.35)*.0019+sin(p.y*29-t*.8)*.0011)*grass;
                uv.y+=sin(p.x*17-t*1.5)*.0012*grass;
                uv.x+=sin(p.y*23+t*.9)*.00075*leaves;
                fixed4 c=tex2D(_MainTex,uv);
                float shimmer=sin(p.y*237-t*2+sin(p.x*37))*sin(p.x*61+t*.8);
                c.rgb+=water*shimmer*.010;
                // 暖窗呼吸只作用于原画的暖亮像素，水面倒影同步变化。
                if(_Theme>2.5&&_Theme<4.5)
                {
                    float warm=saturate((c.r-c.b)*5)*smoothstep(.35,.75,c.r);
                    c.rgb+=warm*(.035*sin(t*1.7+p.x*31)+.016*sin(t*3.1+p.y*23))*float3(1,.62,.22);
                }
                c.a=1;return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
