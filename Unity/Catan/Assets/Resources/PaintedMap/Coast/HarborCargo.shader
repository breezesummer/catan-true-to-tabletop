Shader "Catan/HarborCargo"
{
    Properties
    {
        [MainTexture] _BaseMap("货物颜料",2D)="white" {}
        _SurfaceMap("微表面与光泽",2D)="gray" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
            struct Input { float3 p:POSITION; float3 n:NORMAL; float4 tangent:TANGENT; float2 uv:TEXCOORD0; half4 pigment:COLOR; };
            struct Varyings { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half3 n:TEXCOORD1; half4 tangent:TEXCOORD2; float3 world:TEXCOORD3; half2 finish:TEXCOORD4; };
            Varyings Vert(Input v)
            {
                Varyings o;
                o.world=TransformObjectToWorld(v.p);o.p=TransformWorldToHClip(o.world);
                VertexNormalInputs basis=GetVertexNormalInputs(v.n,v.tangent);
                o.n=basis.normalWS;o.tangent=half4(basis.tangentWS,v.tangent.w*GetOddNegativeScale());
                o.uv=v.uv;o.finish=v.pigment.ra;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                half4 surface=SAMPLE_TEXTURE2D(_SurfaceMap,sampler_SurfaceMap,i.uv);
                int tile=(2-(int)floor(i.uv.y*3))*4+(int)floor(i.uv.x*4);
                // 棋盘一像素覆盖数条纹理时，保留材料在远处最有用的主色和断面关系。
                half distant=smoothstep(.00025,.00068,unity_OrthoParams.y/_ScreenParams.y);
                if(tile==0) base*=lerp(half3(1,1,1),half3(.84,.68,.61),distant);
                if(tile==1) base*=lerp(half3(1,1,1),half3(1.23,1.16,.97),distant);
                if(tile==3) base=lerp(base,half3(.020,.031,.050),distant*.64);
                if(tile==9) base=lerp(base,half3(.66,.76,.84),distant*.63);
                if(tile==11) base=lerp(base,half3(.14,.19,.23),distant*.48);
                // 久晒的木板灰白，沥青养护过的桶偏深；新补的桶板保留原色。
                half sunBleached=saturate((i.finish.y-.50)*2);
                half seasoned=saturate((.50-i.finish.y)*2);
                if(tile==8)
                {
                    half grain=Luminance(base);
                    base=lerp(base,half3(.84,.80,.70)*(grain+.085),sunBleached*.80);
                    base*=1-seasoned*half3(.31,.42,.47);
                }
                if(tile==7)
                    base=lerp(base,base*half3(1.50,.83,.60),seasoned*.74);
                half2 xy=(surface.rg*2-1)*.76;
                half3 geom=normalize(i.n);
                half3 n=normalize(i.tangent.xyz*xy.x+cross(geom,i.tangent.xyz)*(i.tangent.w*xy.y)
                                +geom*sqrt(saturate(1-dot(xy,xy))));
                Light sun=GetMainLight();
                half3 view=GetWorldSpaceNormalizeViewDir(i.world);
                half3 halfway=SafeNormalize(view+sun.direction);
                half diffuse=saturate(dot(n,sun.direction));
                half spec=pow(saturate(dot(n,halfway)),lerp(96,8,surface.b));
                half sky=.49+.13*n.y;
                half ao=i.finish.x*i.finish.x;
                half3 color=base*(sky+.72*diffuse)*ao;
                color+=lerp(half3(.13,.14,.14),base+.16,surface.a)*spec*(1-surface.b)*(.30+.72*surface.a)*ao;
                if(tile==3 || tile==9 || tile==11)
                {
                    // 以真实断面法线反射海湾天光；新断口强，氧化壳弱。
                    half skyBand=pow(saturate(dot(reflect(-view,n),normalize(half3(-.43,.85,-.27)))),5);
                    half reflectance=tile==9 ? .46 : tile==11 ? .20 : .07;
                    color+=half3(.73,.85,1)*skyBand*reflectance*ao;
                }
                // 未染羊毛的绒面散射使边缘泛暖，保留纤维凹陷中的接触阴影。
                if(tile==5)
                    color=base*(.52+.63*diffuse+.14*pow(1-saturate(dot(n,view)),3))*lerp(ao*ao,ao,.38);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
