using System.Collections.Generic;
using UnityEngine;

// 圆片、金属装饰和简洁符号由代码绘制；商品主体使用单独透明抠图。
public static class StrategyHudArt
{
    static Texture2D ornament,bank,handshake,portraits;
    static readonly Dictionary<int,Texture2D> commodities=new Dictionary<int,Texture2D>();
    public static readonly Color Gold=new Color(.84f,.67f,.35f);
    public static void Avatar(Rect rect,string seat)
    {
        if(portraits==null)portraits=Resources.Load<Texture2D>("TabletopUI/v010/leaders-atlas");
        if(portraits==null)return;
        int index=int.TryParse(seat?.Substring(1),out int value)?Mathf.Clamp(value-1,0,3):0;
        GUI.DrawTextureWithTexCoords(rect,portraits,new Rect(index%2*.5f,index<2?.5f:0,.5f,.5f));
    }
    public static void Disc(Rect rect,Color face,bool hover=false)
    {
        if(ornament==null)ornament=Border();
        TabletopUI.Token(new Rect(rect.x-3,rect.y-1,rect.width+6,rect.height+6),new Color(.045f,.04f,.028f,.9f));
        TabletopUI.Token(new Rect(rect.x+3,rect.y+3,rect.width-6,rect.height-6),hover?Color.Lerp(face,Color.white,.14f):face);
        var old=GUI.color;GUI.color=hover?new Color(1,.9f,.62f):Gold;GUI.DrawTexture(rect,ornament);GUI.color=old;
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
    static Texture2D Border()
    {
        const int size=256;var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float u=(x+.5f-size/2f)/(size/2f),v=(y+.5f-size/2f)/(size/2f);
            float radius=Mathf.Sqrt(u*u+v*v),angle=Mathf.Atan2(v,u);
            float scallop=.915f+.018f*Mathf.Cos(angle*24);
            bool ring=Mathf.Abs(radius-.85f)<.014f||Mathf.Abs(radius-.955f)<.015f||Mathf.Abs(radius-scallop)<.016f;
            bool gem=radius>.875f&&radius<.95f&&Mathf.Cos(angle*12)>.89f;
            float light=.65f+.35f*Mathf.Clamp01((-u+v+2)/4);
            pixels[y*size+x]=ring||gem?new Color(light,light,light,1):Color.clear;
        }
        var result=new Texture2D(size,size,TextureFormat.RGBA32,true){name="镂金圆片边框 v010",filterMode=FilterMode.Trilinear};result.SetPixels(pixels);result.Apply(true,true);return result;
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
