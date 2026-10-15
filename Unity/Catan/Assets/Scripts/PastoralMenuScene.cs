using System.Collections.Generic;
using UnityEngine;

// 菜单专属的可复现矢量风景。仅使用本地几何和显示时间，不读取规则、牌库或随机状态。
// 配方：1440×900，海湾与山谷、村舍、麦田和牧场；所有装饰位置由固定序号计算。
public static class PastoralMenuScene
{
    static Material material;
    static readonly List<Vector3> vertices=new List<Vector3>(48000);
    static readonly List<Color> colors=new List<Color>(48000);
    static readonly Color Cream=Hex(0xF8F3DD),Ink=Hex(0x315851);
    static float clock;
    public static void Draw(float time)=>DrawBackground(new Rect(0,0,1440,900),time);
    public static void DrawBackground(Rect full,float time)
    {
        if(Event.current.type!=EventType.Repaint)return;
        if(material==null)
        {
            var shader=Resources.Load<Shader>("PastoralMenu/PastoralMenuVector");
            if(shader==null)shader=Shader.Find("Hidden/Internal-Colored");
            if(shader==null)return;
            material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            material.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull",(int)UnityEngine.Rendering.CullMode.Off);material.SetInt("_ZWrite",0);
        }
        clock=time;vertices.Clear();colors.Clear();Landscape();
        GL.PushMatrix();material.SetPass(0);GL.LoadPixelMatrix(0,Screen.width,Screen.height,0);
        GL.MultMatrix(GUI.matrix*Matrix4x4.TRS(new Vector3(full.x,full.y,0),Quaternion.identity,new Vector3(full.width/1440,full.height/900,1)));
        GL.Begin(GL.TRIANGLES);
        for(int i=0;i<vertices.Count;i++){GL.Color(colors[i]);GL.Vertex(vertices[i]);}
        GL.End();GL.PopMatrix();
    }
    static void Landscape()
    {
        Gradient(0,0,1440,900,Hex(0xB3D5D8),Hex(0xF3EDCD));
        for(int i=8;i>=0;i--)Ellipse(235,155,100+i*10,100+i*10,new Color(1,.97f,.78f,.008f*(9-i)));
        Ellipse(235,155,70,70,Hex(0xFFF5CE));
        Cloud(91+Mathf.Repeat(clock*2.4f,1600)-100,111,1.05f,.62f);
        Cloud(500+Mathf.Repeat(clock*1.6f,1650)-110,171,.68f,.60f);
        Cloud(1070+Mathf.Repeat(clock*2,1600)-110,96,1.2f,.50f);
        Cloud(680+Mathf.Repeat(clock*1.1f,1600)-110,290,.37f,.34f);
        Gradient(0,405,1440,495,Hex(0x86BDB9),Hex(0x438F97));
        // 远山用淡色叠层建立空气透视，近山分别绘制背光、受光和雪线。
        Poly(Hex(0x8AB0A5),0,421,0,353,95,366,188,312,264,358,350,298,451,363,554,302,674,355,805,305,940,385,1440,360,1440,465);
        Poly(Hex(0x7A9A8B),245,439,403,270,524,405,652,245,834,441);
        Poly(Hex(0x8EA994),245,439,403,270,382,386,435,427);
        Poly(Hex(0x6C8E82),403,270,524,405,474,390,444,370);
        Poly(Hex(0x789587),512,430,652,245,644,377,701,430);
        Poly(Hex(0x5D8077),652,245,834,441,744,411,715,372);
        Poly(Hex(0xE0E7D5),357,319,403,270,452,336,422,325,406,334,396,308,382,327,376,313);
        Poly(Hex(0xE6EBDC),603,310,652,245,715,327,685,313,673,326,650,291,637,318,626,305);
        Poly(Hex(0xBBCDC0),652,245,715,327,685,313,673,326,650,291);
        Hill(405,496,190,Hex(0x6A997E),0);
        Hill(670,500,190,Hex(0x7CA785),1);
        Hill(970,510,230,Hex(0x6B9276),2);
        for(int i=0;i<68;i++)
        {
            float x=330+i*10.1f,y=456+Mathf.Sin(i*.23f)*13;
            Pine(x,y,8+Noise(i)*13,Hex(0x608B71));
        }
        // 海面逐条细浪随风平移；只绘制海湾范围。
        for(int i=0;i<73;i++)
        {
            float x=Mathf.Repeat(i*137.7f+clock*(3+i%4),1500)-45;
            float y=434+i*6.3f;
            if(x<350&&y<640||x<140&&y<805)
                Stroke(x,y,x+15+Noise(i)*35,y-1,1.2f,new Color(.87f,.97f,.88f,.17f+Noise(i+20)*.17f));
        }
        Boat(117+Mathf.Sin(clock*.035f)*22,526+Mathf.Sin(clock*.8f)*2,.75f);
        Boat(264+Mathf.Sin(clock*.025f+2)*16,454+Mathf.Sin(clock*.9f)*1,.27f);
        // 岸线与沙滩使用完整曲线带，避免贴图接缝。
        var coast=Smooth(new[]{new Vector2(1440,461),new Vector2(884,466),new Vector2(670,480),new Vector2(511,490),new Vector2(416,514),new Vector2(310,540),new Vector2(302,579),new Vector2(360,607),new Vector2(260,650),new Vector2(177,699),new Vector2(139,751),new Vector2(186,790),new Vector2(233,830),new Vector2(195,900)},5);
        Coast(coast,13,Hex(0xE4D7A4),Hex(0xB1C88C));
        var surf=new List<Vector2>();for(int i=0;i<coast.Length;i++)surf.Add(coast[i]+new Vector2(-8,Mathf.Sin(clock*1.1f-i*.8f)*3));
        for(int i=1;i<surf.Count;i++)Stroke(surf[i-1].x,surf[i-1].y,surf[i].x,surf[i].y,2.1f,new Color(.98f,1,.88f,.40f));
        // 大块有层次的农田与牧场，不覆盖海岸。
        Poly(Hex(0x8EB379),320,548,480,498,678,487,870,516,1440,490,1440,900,197,900,237,826,190,780,147,752,189,711,270,665,369,615);
        Poly(Hex(0xA3BF80),349,606,466,549,623,566,690,639,605,725,472,730,338,664);
        Poly(Hex(0x91AC6D),653,488,816,481,916,567,807,654,708,620,621,560);
        Poly(Hex(0x769D6B),705,638,1440,570,1440,900,588,900,560,795);
        Poly(Hex(0xA1B87A),238,826,344,773,457,815,520,900,195,900);
        River();
        // 田垄跟随坡面展开，包含浅色边缘和金色穗。
        Poly(Hex(0xD5BE76),276,669,414,631,515,670,456,769,305,781,224,745);
        Poly(Hex(0xCBB16B),276,669,414,631,407,681,305,781,224,745);
        for(int row=0;row<11;row++)
        {
            float y=662+row*9.4f,left=286-row*4.4f,right=432+row*4;
            Stroke(left,y,right,y+39,2,new Color(.60f,.48f,.24f,.23f));
            for(int k=0;k<13;k++)
            {
                float x=Mathf.Lerp(left,right,k/12f),yy=y+(x-left)*.17f;
                float h=9+Noise(row*17+k)*6,sway=Mathf.Sin(clock*1.7f+x*.018f+row)*2.5f;
                Wheat(x,yy,h,sway);
            }
        }
        Path(new[]{new Vector2(415,565),new Vector2(437,588),new Vector2(459,616),new Vector2(508,640),new Vector2(550,671)},12,Hex(0xD2C392));
        // 园林和屋舍有独立屋面、侧墙、窗框和投影。
        for(int i=0;i<14;i++)Tree(391+Noise(i*3)*182,530+Noise(i*3+1)*82,.42f+Noise(i+31)*.24f,Hex(0x507D59));
        House(417,546,1.04f,Hex(0xDCA67D),Hex(0xA36350));
        House(496,575,.88f,Hex(0xE7C89C),Hex(0xBD8060));
        House(365,586,.76f,Hex(0xEADBB3),Hex(0xA56E58));
        House(473,521,.62f,Hex(0xDCC7A5),Hex(0x987459));
        Windmill(564,547,.78f);
        Smoke(431,500,.9f);Smoke(508,536,.65f);
        // 植被细节在近景增大；前景坡面与左侧树框出景深。
        for(int i=0;i<31;i++)
        {
            float x=242+Noise(i*7)*564,y=806+Noise(i*7+1)*92;
            if(x>440&&x<590)continue;
            Grass(x,y,6+Noise(i+19)*8,Mathf.Sin(clock*1.1f+i)*2.4f,Hex(0x688F5B));
        }
        Fence(559,697,205,47,7);
        for(int i=0;i<8;i++)
        {
            float pace=Mathf.Sin(clock*.13f+i*1.7f)*12;
            float x=569+(i%4)*49+pace,y=744+(i/4)*47+Mathf.Sin(i*2)*10;
            Sheep(x,y,.72f+(i/4)*.13f,i,clock);
        }
        for(int i=0;i<20;i++)
        {
            float x=228+Noise(i*4)*177,y=814+Noise(i*4+1)*85;
            Ellipse(x,y,3.5f,2,Hex(i%3==0?0xD8AE66:0xEDEBC7));
        }
        Tree(57,857,1.5f,Hex(0x355F52));Tree(2,785,1.7f,Hex(0x3B6B57));
        Poly(new Color(.16f,.32f,.25f,.11f),0,898,1440,866,1440,900,0,900);
        // 飞鸟振翅与缓慢滑翔；画面右侧仍延续背景以供大厅复用。
        for(int i=0;i<5;i++)
        {
            float x=475+i*27+Mathf.Sin(clock*.07f)*22,y=215+Mathf.Sin(i)*17+Mathf.Sin(clock*.5f+i)*3;
            float wing=3+Mathf.Sin(clock*2.4f+i)*2;
            Stroke(x-6,y-wing,x,y,1.2f,new Color(.25f,.41f,.38f,.65f));Stroke(x,y,x+6,y-wing,1.2f,new Color(.25f,.41f,.38f,.65f));
        }
    }
    static void River()
    {
        var center=new List<Vector2>();var widths=new List<float>();
        for(int i=0;i<=70;i++)
        {
            float t=i/70f,y=458+t*470;
            float x=642+Mathf.Sin(t*7.3f-.3f)*(17+t*104)-t*96;
            center.Add(new Vector2(x,y));widths.Add(2+t*t*39);
        }
        Ribbon(center,widths,2,Hex(0xD6D6AC));Ribbon(center,widths,0,Hex(0x65A7A3));
        var thin=new List<float>();for(int i=0;i<widths.Count;i++)thin.Add(widths[i]*.64f);
        Ribbon(center,thin,0,new Color(.59f,.79f,.74f,.55f));
        for(int i=0;i<35;i++)
        {
            float t=Mathf.Repeat(i/35f+clock*.020f,1),y=458+t*470;
            float x=642+Mathf.Sin(t*7.3f-.3f)*(17+t*104)-t*96;
            float w=2+t*t*26;Stroke(x-w*.5f,y,x+w*.5f,y-2,1.1f,new Color(.89f,.98f,.88f,.42f));
        }
        // 小木桥横跨溪流，桥面与栏杆分开。
        Poly(Hex(0x8C7953),652,603,700,611,694,621,646,613);
        for(int i=0;i<8;i++)Stroke(653+i*5.7f,604+i,648+i*5.7f,613+i,1,Hex(0xB5A279));
        Stroke(650,599,700,607,2,Hex(0x756E4D));
        for(int i=0;i<5;i++)Stroke(652+i*11.4f,599+i*1.8f,651+i*11.4f,606+i*1.8f,1.5f,Hex(0x756E4D));
    }
    static void Wheat(float x,float y,float h,float sway)
    {
        var light=Hex(0xF0D591);var stem=Hex(0xA49250);
        Stroke(x,y,x+sway,y-h,1,stem);
        for(int i=0;i<3;i++){float yy=y-h+i*2.4f;Stroke(x+sway,yy,x+sway-2.2f,yy-3.5f,1.5f,light);Stroke(x+sway,yy,x+sway+2.5f,yy-3,1.5f,light);}
    }
    static void Coast(Vector2[] shore,float sand,Color beach,Color land)
    {
        var p=new List<Vector2>(shore);p.Add(new Vector2(1440,900));Polygon(p,beach);
        var inland=new List<Vector2>();for(int i=0;i<shore.Length;i++)inland.Add(shore[i]+new Vector2(sand,3));inland.Add(new Vector2(1440,900));Polygon(inland,land);
    }
    static void Hill(float x,float y,float width,Color color,int variation)
    {
        var p=new List<Vector2>();p.Add(new Vector2(x-width,y+38));
        for(int i=0;i<=30;i++){float t=i/30f;p.Add(new Vector2(x-width+t*width*2,y-Mathf.Sin(t*Mathf.PI)*(54+variation*4)+Mathf.Sin(t*8+variation)*7));}
        p.Add(new Vector2(x+width,y+38));Polygon(p,color);
    }
    static void House(float x,float y,float s,Color wall,Color roof)
    {
        Ellipse(x+3*s,y+6*s,62*s,15*s,new Color(.21f,.34f,.25f,.16f));
        Poly(wall,x-23*s,y-33*s,x+16*s,y-33*s,x+16*s,y,x-23*s,y);
        Poly(Color.Lerp(wall,Ink,.17f),x+16*s,y-33*s,x+29*s,y-40*s,x+29*s,y-8*s,x+16*s,y);
        Poly(roof,x-30*s,y-33*s,x-5*s,y-57*s,x+23*s,y-33*s);
        Poly(Color.Lerp(roof,Ink,.21f),x-5*s,y-57*s,x+11*s,y-65*s,x+36*s,y-41*s,x+23*s,y-33*s);
        Stroke(x-30*s,y-33*s,x-5*s,y-57*s,2*s,Color.Lerp(roof,Cream,.3f));
        Rect(x-2*s,y-22*s,10*s,22*s,Hex(0x6E765A));Rect(x-17*s,y-24*s,9*s,10*s,Hex(0xFFF0BF));
        Stroke(x-12.5f*s,y-24*s,x-12.5f*s,y-14*s,1*s,wall);Stroke(x-17*s,y-19*s,x-8*s,y-19*s,1*s,wall);
        Poly(Hex(0xB9B09B),x+7*s,y-58*s,x+13*s,y-62*s,x+13*s,y-72*s,x+7*s,y-71*s);
        Rect(x-26*s,y,46*s,3*s,Hex(0xA6986F));
    }
    static void Windmill(float x,float y,float s)
    {
        Ellipse(x+6*s,y+8*s,50*s,14*s,new Color(.18f,.31f,.22f,.17f));
        Poly(Hex(0xEEE2B8),x-15*s,y,x-9*s,y-65*s,x+9*s,y-65*s,x+16*s,y);
        Poly(Hex(0xD0C79F),x+3*s,y-65*s,x+9*s,y-65*s,x+16*s,y,x+6*s,y);
        Poly(Hex(0x9D7659),x-14*s,y-63*s,x,y-78*s,x+14*s,y-63*s);
        Rect(x-4*s,y-18*s,8*s,18*s,Hex(0x777C60));
        float angle=clock*.28f;
        for(int i=0;i<4;i++)
        {
            float a=angle+i*Mathf.PI/2;Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),n=new Vector2(-d.y,d.x);Vector2 p=new Vector2(x,y-52*s);
            Polygon(new[]{p+d*5*s+n*2*s,p+d*45*s+n*5*s,p+d*45*s-n*3*s,p+d*5*s-n*2*s},Hex(0xF5ECD0));
            Stroke(p.x,p.y,p.x+d.x*46*s,p.y+d.y*46*s,1.8f*s,Hex(0x807957));
            for(int k=0;k<5;k++){var q=p+d*(18+k*5)*s;Stroke(q.x+n.x*4*s,q.y+n.y*4*s,q.x-n.x*2*s,q.y-n.y*2*s,.8f*s,Hex(0xB1AB88));}
        }
        Ellipse(x,y-52*s,7*s,7*s,Hex(0x8B8562));
    }
    static void Sheep(float x,float y,float s,int id,float time)
    {
        Ellipse(x+1,y+3*s,35*s,10*s,new Color(.19f,.31f,.23f,.16f));
        float phase=time*2.1f+id,walk=Mathf.Sin(time*.13f+id*1.7f);
        for(int i=0;i<4;i++)Stroke(x+(-12+i*7)*s,y-2*s,x+(-12+i*7+Mathf.Sin(phase+i*2)*2*walk)*s,y+7*s,2.5f*s,Hex(0x6A715E));
        Ellipse(x,y-12*s,34*s,22*s,Hex(0xE3DFC6));
        for(int i=0;i<6;i++)Ellipse(x+(-12+i*5)*s,y-16*s-Mathf.Sin(i)*3*s,13*s,15*s,Hex(0xFBF6E1));
        float graze=Mathf.Sin(time*.6f+id)>0?4:0;
        Ellipse(x+18*s,y+(-13+graze)*s,12*s,13*s,Hex(0x787B65));
        Ellipse(x+15*s,y+(-19+graze)*s,8*s,4*s,Hex(0x8A8A70));
        Ellipse(x+21*s,y+(-14+graze)*s,2*s,2*s,Hex(0x384E44));
    }
    static void Tree(float x,float y,float s,Color color)
    {
        Ellipse(x+7*s,y+4*s,51*s,11*s,new Color(.13f,.29f,.22f,.13f));
        Stroke(x,y,x-2*s,y-48*s,5*s,Hex(0x8D8862));Stroke(x-1*s,y-25*s,x-16*s,y-39*s,2*s,Hex(0x8D8862));
        Ellipse(x,y-49*s,49*s,49*s,color);Ellipse(x-18*s,y-39*s,35*s,36*s,color);Ellipse(x+17*s,y-39*s,39*s,39*s,color);
        Ellipse(x-11*s,y-58*s,28*s,28*s,Color.Lerp(color,Hex(0xB4C684),.23f));Ellipse(x+9*s,y-50*s,25*s,25*s,Color.Lerp(color,Hex(0xB4C684),.10f));
    }
    static void Pine(float x,float y,float h,Color c){Poly(c,x,y-h,x-h*.35f,y-h*.28f,x-h*.20f,y-h*.28f,x-h*.48f,y,x+h*.48f,y,x+h*.20f,y-h*.28f,x+h*.35f,y-h*.28f);}
    static void Fence(float x,float y,float w,float rise,int count)
    {
        for(int rail=0;rail<2;rail++)Stroke(x,y+rail*6,x+w,y-rise+rail*6,1.7f,Hex(0xC8C199));
        for(int i=0;i<=count;i++){float xx=x+i*w/count,yy=y-i*rise/count;Stroke(xx,yy-4,xx,yy+13,2.8f,Hex(0xBCB489));}
    }
    static void Smoke(float x,float y,float s)
    {
        for(int i=0;i<5;i++){float t=Mathf.Repeat(clock*.16f+i*.2f,1);Ellipse(x+t*17+Mathf.Sin(clock+i)*3,y-t*45*s,6+t*14*s,7+t*13*s,new Color(.98f,.96f,.84f,(1-t)*.27f));}
    }
    static void Cloud(float x,float y,float s,float alpha)
    {
        Color c=new Color(1,.99f,.91f,alpha);Ellipse(x,y,118*s,28*s,c);Ellipse(x-24*s,y-11*s,53*s,41*s,c);Ellipse(x+14*s,y-18*s,67*s,51*s,c);Ellipse(x+47*s,y-5*s,50*s,30*s,c);
    }
    static void Boat(float x,float y,float s)
    {
        Ellipse(x,y+7*s,59*s,5*s,new Color(.91f,.96f,.87f,.25f));
        Poly(Hex(0x735D44),x-25*s,y,x+26*s,y-2*s,x+15*s,y+8*s,x-16*s,y+8*s);
        Stroke(x,y,x,y-57*s,2*s,Hex(0x8E8B65));Poly(Hex(0xF2ECD3),x-3*s,y-54*s,x-3*s,y-7*s,x-24*s,y-7*s);
        Poly(Hex(0xDFD9BD),x+2*s,y-49*s,x+25*s,y-8*s,x+2*s,y-8*s);
    }
    static void Grass(float x,float y,float h,float sway,Color c)
    {Stroke(x,y,x+sway,y-h,1.3f,c);Stroke(x,y,x-4+sway*.6f,y-h*.7f,1.1f,c);Stroke(x,y,x+5+sway*.7f,y-h*.8f,1.1f,c);}
    static void Ribbon(IList<Vector2> center,IList<float> width,float extra,Color c)
    {
        for(int i=1;i<center.Count;i++)
        {
            Vector2 a=center[i-1],b=center[i],d=(b-a).normalized,n=new Vector2(-d.y,d.x);float w0=width[i-1]+extra,w1=width[i]+extra;
            Triangle(a+n*w0,a-n*w0,b+n*w1,c);Triangle(a-n*w0,b-n*w1,b+n*w1,c);
        }
    }
    static void Path(IList<Vector2> center,float width,Color c){var w=new List<float>();for(int i=0;i<center.Count;i++)w.Add(width/2);Ribbon(center,w,0,c);}
    static void Rect(float x,float y,float w,float h,Color c){Triangle(new Vector2(x,y),new Vector2(x+w,y),new Vector2(x,y+h),c);Triangle(new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h),c);}
    static void Gradient(float x,float y,float w,float h,Color top,Color bottom)
    {Vertex(new Vector2(x,y),top);Vertex(new Vector2(x+w,y),top);Vertex(new Vector2(x,y+h),bottom);Vertex(new Vector2(x+w,y),top);Vertex(new Vector2(x+w,y+h),bottom);Vertex(new Vector2(x,y+h),bottom);}
    static void Ellipse(float x,float y,float w,float h,Color c)
    {int count=32;for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;Triangle(new Vector2(x,y),new Vector2(x+Mathf.Cos(a)*w*.5f,y+Mathf.Sin(a)*h*.5f),new Vector2(x+Mathf.Cos(b)*w*.5f,y+Mathf.Sin(b)*h*.5f),c);}}
    static void Stroke(float x0,float y0,float x1,float y1,float thickness,Color c)
    {Vector2 a=new Vector2(x0,y0),b=new Vector2(x1,y1),d=(b-a).normalized,n=new Vector2(-d.y,d.x)*thickness*.5f;Triangle(a+n,a-n,b+n,c);Triangle(a-n,b-n,b+n,c);}
    static void Poly(Color c,params float[] xy){var p=new Vector2[xy.Length/2];for(int i=0;i<p.Length;i++)p[i]=new Vector2(xy[i*2],xy[i*2+1]);Polygon(p,c);}
    static void Polygon(IList<Vector2> p,Color c)
    {
        // 凹形海岸与田块使用耳切，保证不会出现三角扇跨越河道/海岸的假填充。
        if(p.Count<3)return;float area=0;for(int i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];area+=a.x*b.y-b.x*a.y;}
        var indices=new List<int>();if(area>0)for(int i=0;i<p.Count;i++)indices.Add(i);else for(int i=p.Count-1;i>=0;i--)indices.Add(i);
        int guard=0;while(indices.Count>2&&guard++<p.Count*p.Count)
        {
            bool found=false;for(int i=0;i<indices.Count;i++)
            {
                int ia=indices[(i+indices.Count-1)%indices.Count],ib=indices[i],ic=indices[(i+1)%indices.Count];Vector2 a=p[ia],b=p[ib],cc=p[ic];
                if(Cross(b-a,cc-b)<=.0001f)continue;bool inside=false;
                for(int j=0;j<indices.Count;j++){int k=indices[j];if(k==ia||k==ib||k==ic)continue;var q=p[k];if(Cross(b-a,q-a)>=0&&Cross(cc-b,q-b)>=0&&Cross(a-cc,q-cc)>=0){inside=true;break;}}
                if(inside)continue;Triangle(a,b,cc,c);indices.RemoveAt(i);found=true;break;
            }
            if(!found)break;
        }
    }
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static Vector2[] Smooth(Vector2[] input,int steps)
    {
        var result=new List<Vector2>((input.Length-1)*steps+1);
        for(int i=0;i<input.Length-1;i++)
        {
            Vector2 p0=input[Mathf.Max(0,i-1)],p1=input[i],p2=input[i+1],p3=input[Mathf.Min(input.Length-1,i+2)];
            for(int j=0;j<steps;j++){float t=j/(float)steps,t2=t*t,t3=t2*t;result.Add(.5f*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t2+(-p0+3*p1-3*p2+p3)*t3));}
        }
        result.Add(input[input.Length-1]);return result.ToArray();
    }
    static void Triangle(Vector2 a,Vector2 b,Vector2 c,Color color){Vertex(a,color);Vertex(b,color);Vertex(c,color);}
    static void Vertex(Vector2 p,Color c){vertices.Add(new Vector3(p.x,p.y,0));colors.Add(c);}
    static float Noise(int i)=>Mathf.Repeat(Mathf.Sin(i*78.233f+41.17f)*43758.545f,1);
    static Color Hex(int rgb)=>new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,1);
}
