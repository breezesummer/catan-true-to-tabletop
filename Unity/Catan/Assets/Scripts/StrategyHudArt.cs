using System.Collections.Generic;
using UnityEngine;

// 铜框与圆片使用独立美术贴图；简洁符号由代码绘制，商品主体使用透明抠图。
public static class StrategyHudArt
{
    static Texture2D bank,handshake,portraits,victory;
    static readonly Dictionary<int,Texture2D> commodities=new Dictionary<int,Texture2D>();
    public static readonly Color Gold=new Color(.84f,.67f,.35f);
    // 屏幕空间雕边金属控件；与卡牌、三维棋子资产分开，不参与规则或存档。
    public static void Plaque(Rect rect,Color face,Color trim,bool hover=false)
    {
        CopperTabletopSkin.Panel(rect,face,hover);
    }

    public static bool HandControl(Rect rect,string caption,int kind,bool selected=false,TabletopHandCounter? counter=null)
    {
        ModernOperationUI.Styles();
        bool hover=GUI.enabled&&rect.Contains(Event.current.mousePosition);
        Color trim=selected?ModernOperationUI.Accent:Color.Lerp(Gold,ModernOperationUI.Line,.56f);
        Color face=selected?Color.Lerp(ModernOperationUI.Surface,ModernOperationUI.Accent,.20f):ModernOperationUI.Surface;
        if(!GUI.enabled){face=Color.Lerp(ModernOperationUI.Background,face,.4f);trim=ModernOperationUI.Line;}
        Plaque(rect,face,trim,hover);
        var icon=counter.HasValue?new Rect(rect.x+8,rect.center.y-16,30,32):new Rect(rect.x+11,rect.center.y-10,19,20);
        Color ink=GUI.enabled?(selected?new Color(1,.94f,.75f):ModernOperationUI.TextColor):ModernOperationUI.Muted;
        HandSymbol(icon,kind,ink);
        var label=new GUIStyle(ModernOperationUI.Center){fontSize=kind==2?11:12,fontStyle=FontStyle.Bold,wordWrap=false,padding=new RectOffset(),alignment=TextAnchor.MiddleCenter};
        label.normal.textColor=ink;
        if(counter.HasValue)
        {
            label.fontSize=10;label.normal.textColor=GUI.enabled?ModernOperationUI.Muted:ModernOperationUI.Line;
            GUI.Label(new Rect(rect.x+39,rect.y+3,rect.width-43,16),caption,label);
            label.fontSize=14;label.normal.textColor=counter.Value.OverLimit?ModernOperationUI.Error:ink;
            GUI.Label(new Rect(rect.x+38,rect.y+19,rect.width-42,20),counter.Value.Caption,label);
        }
        else GUI.Label(new Rect(rect.x+32,rect.y+2,rect.width-39,rect.height-4),caption,label);
        return TabletopButtonFeedback.Button(rect);
    }
    static void HandSymbol(Rect rect,int kind,Color ink)
    {
        // 卡页采用三张扇形牌背，整理采用三张齐列卡纸和向下归拢箭头。
        if(kind==2)
        {
            for(int i=0;i<3;i++)
            {
                var card=new Rect(rect.x+i*6,rect.y+(i==1?0:3),5,11);
                ModernOperationUI.Round(card,ink);
                ModernOperationUI.Fill(new Rect(card.x+1,card.y+2,card.width-2,6),ModernOperationUI.Surface);
            }
            ModernOperationUI.Fill(new Rect(rect.x+3,rect.yMax-4,14,1),ink);
            Line(new Vector2(rect.x+8,rect.yMax-7),new Vector2(rect.x+10,rect.yMax-5),ink,1.5f);
            Line(new Vector2(rect.x+10,rect.yMax-5),new Vector2(rect.x+12,rect.yMax-7),ink,1.5f);
            return;
        }
        HandCardIcon(rect,kind==1);
    }
    public static void HandCardIcon(Rect rect,bool special)
    {
        // 先在图标内建立等比像素坐标，再围绕共同底部旋转，避免外层缩放把枢轴移到桌面。
        // 三色发展卡背或三张相同资源牌背只表示类别，不表示隐藏手牌构成。
        var matrix=GUI.matrix;
        float scaleX=matrix.MultiplyVector(Vector3.right).magnitude,scaleY=matrix.MultiplyVector(Vector3.up).magnitude;
        if(rect.width<=0||rect.height<=0||scaleX<=0||scaleY<=0)return;
        const float angle=24;
        float sin=Mathf.Sin(angle*Mathf.Deg2Rad),cos=Mathf.Cos(angle*Mathf.Deg2Rad),aspect=0;
        var resource=special?null:EntityCardAssets.Texture(EntityCardAssets.Good(0).Back);
        for(int i=0;i<3;i++)
        {
            var texture=special?EntityCardAssets.ProgressBack(i):resource;
            if(texture!=null)aspect=Mathf.Max(aspect,(float)texture.width/texture.height);
        }
        if(aspect<=0)return;
        float top=Mathf.Max(1,cos+aspect*.5f*sin),bottom=aspect*.5f*sin;
        // 旋转后所有角点及纸边投影都留在原图标范围内。
        float height=Mathf.Min(Mathf.Max(0,rect.width*scaleX-4)/(aspect*cos+2*sin),Mathf.Max(0,rect.height*scaleY-4)/(top+bottom));
        var local=matrix*Matrix4x4.Translate(rect.center)*Matrix4x4.Scale(new Vector3(1/scaleX,1/scaleY,1))
            *Matrix4x4.Translate(new Vector3(0,(top-bottom)*height/2,0));
        try
        {
            for(int i=0;i<3;i++)
            {
                var texture=special?EntityCardAssets.ProgressBack(i):resource;
                if(texture==null)continue;
                float width=height*texture.width/texture.height;
                GUI.matrix=local*Matrix4x4.Rotate(Quaternion.Euler(0,0,(i-1)*angle));
                EntityCardAssets.DrawCardFace(new Rect(-width/2,-height,width,height),texture);
            }
        }
        finally{GUI.matrix=matrix;}
    }
    public static void VictoryIcon(Rect rect)
    {
        if(victory==null)victory=VictoryTexture();
        GUI.DrawTexture(rect,victory,ScaleMode.ScaleToFit);
    }
    static Texture2D VictoryTexture()
    {
        // 无对应透明素材的分数徽章：超采样绘制金质奖杯、耳环与底座，保留小尺寸清晰轮廓。
        const int size=128,samples=2;var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float coverage=0;
            for(int sy=0;sy<samples;sy++)for(int sx=0;sx<samples;sx++)
            {
                float u=(x+(sx+.5f)/samples)/size,v=(y+(sy+.5f)/samples)/size;
                float cupHalf=.07f+.23f*Mathf.Clamp01((v-.38f)/.43f);
                bool cup=v>.38f&&v<.81f&&Mathf.Abs(u-.5f)<cupHalf;
                bool rim=v>.79f&&v<.87f&&u>.17f&&u<.83f;
                bool stem=v>.19f&&v<.42f&&Mathf.Abs(u-.5f)<.055f;
                bool basePlate=v>.085f&&v<.18f&&u>.23f&&u<.77f;
                float left=Mathf.Sqrt(Mathf.Pow((u-.24f)/.16f,2)+Mathf.Pow((v-.66f)/.17f,2));
                float right=Mathf.Sqrt(Mathf.Pow((u-.76f)/.16f,2)+Mathf.Pow((v-.66f)/.17f,2));
                bool handles=v>.48f&&v<.84f&&(left>.67f&&left<1||right>.67f&&right<1);
                if(cup||rim||stem||basePlate||handles)coverage+=1f/(samples*samples);
            }
            float light=Mathf.Clamp01(.5f+(y/(float)size)*.42f+Mathf.Sin(x/(float)size*10)*.18f);
            var gold=Color.Lerp(new Color(.42f,.24f,.07f),new Color(1f,.90f,.57f),light);gold.a=coverage;
            pixels[y*size+x]=gold;
        }
        var result=new Texture2D(size,size,TextureFormat.RGBA32,true){name="金质分数奖杯 v012",filterMode=FilterMode.Trilinear};
        result.SetPixels(pixels);result.Apply(true,true);return result;
    }
    public static void Line(Vector2 from,Vector2 to,Color color,float width=1)
    {
        var matrix=GUI.matrix;var delta=to-from;
        // 先在逻辑坐标中定位/旋转，再应用屏幕缩放；非等比宽屏下也保持端点准确。
        GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(from.x,from.y,0),Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg),Vector3.one);
        try{ModernOperationUI.Fill(new Rect(0,-width/2,delta.magnitude,width),color);}
        finally{GUI.matrix=matrix;}
    }
    public static void Avatar(Rect rect,string seat)
    {
        if(portraits==null)portraits=Resources.Load<Texture2D>("TabletopUI/v010/leaders-atlas");
        if(portraits==null)return;
        int index=int.TryParse(seat?.Substring(1),out int value)?Mathf.Clamp(value-1,0,3):0;
        GUI.DrawTextureWithTexCoords(rect,portraits,new Rect(index%2*.5f,index<2?.5f:0,.5f,.5f));
    }
    public static void Disc(Rect rect,Color face,bool hover=false)
    {
        CopperTabletopSkin.Disc(rect,face,hover);
    }

    public static void Icon(Rect rect,int index)
    {
        Texture2D texture;
        if(index<2)
        {
            if(bank==null){bank=Symbol(false);handshake=Symbol(true);}
            texture=index==0?bank:handshake;
            var old=GUI.color;GUI.color=new Color(.98f,.91f,.73f);GUI.DrawTexture(rect,texture,ScaleMode.ScaleToFit);GUI.color=old;
        }
        else
        {
            if(!commodities.TryGetValue(index,out texture)||texture==null)
            {texture=Resources.Load<Texture2D>("TabletopUI/v010/"+(index==2?"cloth":index==3?"coin":"paper")+"-cutout");commodities[index]=texture;}
            if(texture!=null)GUI.DrawTexture(rect,texture,ScaleMode.ScaleToFit);
            else OperationIconAssets.DrawGood(rect,index+3);
        }
    }
    static Texture2D Symbol(bool hands)
    {
        const int size=128;var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float u=x/(float)size,v=y/(float)size;bool mark;
            if(!hands)
            {
                mark=v>.17f&&v<.24f&&u>.11f&&u<.89f||v>.29f&&v<.34f&&u>.16f&&u<.84f;
                mark|=v>.36f&&v<.68f&&(u>.2f&&u<.29f||u>.455f&&u<.545f||u>.71f&&u<.80f);
                mark|=v>.72f&&v<.9f&&Mathf.Abs(u-.5f)<(.92f-v)*2.2f;
                mark|=v>.67f&&v<.73f&&u>.13f&&u<.87f;
            }
            else
            {
                // 交握轮廓：两端袖口、交叠的手掌及四道指节。
                mark=u>.08f&&u<.25f&&v>.38f&&v<.70f||u>.75f&&u<.92f&&v>.36f&&v<.67f;
                mark|=u>.25f&&u<.73f&&Mathf.Abs(v-(.60f-.25f*(u-.25f)))<.105f;
                mark|=u>.32f&&u<.68f&&Mathf.Abs(v-(.38f+.33f*(u-.32f)))<.065f;
                if(u>.46f&&u<.76f&&v>.30f&&v<.48f&&Mathf.Repeat(u+v*.42f,.075f)<.016f)mark=false;
                mark|=u>.37f&&u<.56f&&Mathf.Abs(v-(.57f+.60f*(u-.37f)))<.05f;
            }
            pixels[y*size+x]=mark?Color.white:Color.clear;
        }
        var result=new Texture2D(size,size,TextureFormat.RGBA32,true){name=hands?"玩家握手图标 v010":"银行图标 v010",filterMode=FilterMode.Trilinear};result.SetPixels(pixels);result.Apply(true,true);return result;
    }
}
