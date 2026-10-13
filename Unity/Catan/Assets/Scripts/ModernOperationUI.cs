using System;
using UnityEngine;

// 仅绘制操作界面；所有确认操作仍由各适配器提交到共同规则入口。
public static class ModernOperationUI
{
    public static readonly Color Background=new Color(.055f,.095f,.10f,.98f), Surface=new Color(.105f,.155f,.16f),
        TextColor=new Color(.94f,.93f,.86f), Muted=new Color(.60f,.69f,.68f), Accent=new Color(.84f,.69f,.40f),
        Line=new Color(.24f,.32f,.31f), Error=new Color(.98f,.53f,.40f);
    static Font currentFont;
    static Texture2D round;
    public static GUIStyle Text, Small, Title, Center;
    public static void Styles(Font font=null)
    {
        if(Text!=null&&(font==null||font==currentFont))return;
        if(font!=null)currentFont=font;
        Text=new GUIStyle(GUI.skin.label){font=currentFont,fontSize=16,wordWrap=true};Text.normal.textColor=TextColor;
        Small=new GUIStyle(Text){fontSize=12};Small.normal.textColor=Muted;
        Title=new GUIStyle(Text){fontSize=22,fontStyle=FontStyle.Bold};
        Center=new GUIStyle(Text){fontSize=14,alignment=TextAnchor.MiddleCenter};
    }
    public static void Fill(Rect r,Color color)
    {var old=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    public static void Round(Rect r,Color color)
    {
        if(r.width<=0||r.height<=0)return;
        if(round==null)
        {
            round=new Texture2D(32,32,TextureFormat.RGBA32,false);round.filterMode=FilterMode.Bilinear;round.wrapMode=TextureWrapMode.Clamp;
            var pixels=new Color[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {
                float dx=x-15.5f,dy=y-15.5f;
                pixels[y*32+x]=new Color(1,1,1,Mathf.Clamp01(16-Mathf.Sqrt(dx*dx+dy*dy)));
            }
            round.SetPixels(pixels);round.Apply();
        }
        // 九宫格：只有中间与四边延展，四个角始终占用 8 个逻辑像素。
        // 不能把完整圆角图拉伸到面板尺寸，否则大面板的边缘会变成大块透明区。
        float radius=Mathf.Min(8,Mathf.Min(r.width,r.height)/2);
        var old=GUI.color;GUI.color=color;
        GUI.DrawTexture(new Rect(r.x+radius,r.y+radius,r.width-2*radius,r.height-2*radius),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x+radius,r.y,r.width-2*radius,radius),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x+radius,r.yMax-radius,r.width-2*radius,radius),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x,r.y+radius,radius,r.height-2*radius),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax-radius,r.y+radius,radius,r.height-2*radius),Texture2D.whiteTexture);
        GUI.DrawTextureWithTexCoords(new Rect(r.x,r.y,radius,radius),round,new Rect(0,.5f,.5f,.5f));
        GUI.DrawTextureWithTexCoords(new Rect(r.xMax-radius,r.y,radius,radius),round,new Rect(.5f,.5f,.5f,.5f));
        GUI.DrawTextureWithTexCoords(new Rect(r.x,r.yMax-radius,radius,radius),round,new Rect(0,0,.5f,.5f));
        GUI.DrawTextureWithTexCoords(new Rect(r.xMax-radius,r.yMax-radius,radius,radius),round,new Rect(.5f,0,.5f,.5f));
        GUI.color=old;
    }
    public static void Panel(Rect r)
    {
        Round(new Rect(r.x-5,r.y+7,r.width+10,r.height+9),new Color(0,0,0,.20f));
        Round(r,Line);Round(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),Background);
        Fill(new Rect(r.x+20,r.y+1,r.width-40,1),new Color(.68f,.72f,.64f,.20f));
    }
    public static void Label(Rect r,string value,GUIStyle style=null){Styles();GUI.Label(r,value,style??Text);}
    public static bool Button(Rect r,string caption,bool enabled=true,bool primary=false,bool selected=false)
    {
        Styles();bool old=GUI.enabled;GUI.enabled=old&&enabled;
        bool hover=GUI.enabled&&r.Contains(Event.current.mousePosition);
        Color bg=primary?Accent:selected?new Color(.22f,.31f,.28f):Surface;
        if(hover)bg=Color.Lerp(bg,TextColor,.09f);
        Round(r,GUI.enabled?bg:Color.Lerp(Background,Surface,.6f));
        if(selected)Fill(new Rect(r.x+9,r.yMax-3,r.width-18,2),Accent);
        var label=new GUIStyle(Center);label.normal.textColor=!GUI.enabled?Muted:primary?Background:TextColor;
        GUI.Label(r,caption,label);bool result=GUI.Button(r,GUIContent.none,GUIStyle.none);GUI.enabled=old;return result;
    }
    public static bool LayoutButton(string caption,bool enabled=true,bool primary=false,float height=36)
    {return Button(GUILayoutUtility.GetRect(GUIContent.none,GUIStyle.none,GUILayout.Height(height),GUILayout.ExpandWidth(true)),caption,enabled,primary);}
    public static void Info(string value,bool error=false)
    {Styles();var style=new GUIStyle(Small);if(error)style.normal.textColor=Error;GUILayout.Label(value,style);}
    public static int Choices(string caption,int value,string[] labels,int firstGood=-1)
    {
        Styles();if(labels.Length==0)return 0;value=Mathf.Clamp(value,0,labels.Length-1);
        GUILayout.Space(5);GUILayout.Label(caption,Small);
        int columns=Math.Min(firstGood>=0?4:3,labels.Length),rows=(labels.Length+columns-1)/columns;
        var area=GUILayoutUtility.GetRect(GUIContent.none,GUIStyle.none,GUILayout.Height(rows*(firstGood>=0?65:40)),GUILayout.ExpandWidth(true));
        for(int i=0;i<labels.Length;i++)
        {
            float width=area.width/columns;var r=new Rect(area.x+(i%columns)*width,area.y+(i/columns)*(firstGood>=0?65:40),width-5,firstGood>=0?60:35);
            if(firstGood>=0&&firstGood+i<8&&labels[i]!="不取")
            {
                Round(r,i==value?new Color(.22f,.31f,.28f):Surface);
                OperationIconAssets.DrawGood(new Rect(r.center.x-13,r.y+5,26,26),firstGood+i);
                Label(new Rect(r.x,r.y+31,r.width,25),labels[i],Center);
                if(i==value)Fill(new Rect(r.x+7,r.yMax-3,r.width-14,2),Accent);
                if(GUI.Button(r,GUIContent.none,GUIStyle.none))value=i;
            }
            else if(Button(r,labels[i],true,false,i==value))value=i;
        }
        return value;
    }
    public static void Cost(int[] goods)
    {
        Styles();var r=GUILayoutUtility.GetRect(GUIContent.none,GUIStyle.none,GUILayout.Height(42),GUILayout.ExpandWidth(true));float x=r.x;
        for(int i=0;i<goods.Length&&i<8;i++)if(goods[i]>0)
        {OperationIconAssets.DrawGood(new Rect(x,r.y+4,30,30),i);Label(new Rect(x+33,r.y+4,32,30),"×"+goods[i],Center);x+=72;}
    }
}
