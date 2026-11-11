using System.Collections.Generic;
using UnityEngine;

public sealed partial class M6App
{
    // 页面的几何、文字、选中态和焦点由客户端绘制。九宫格只包含无字的材质与包边。
    static readonly Dictionary<string,GUIStyle> menuSurfaces=new Dictionary<string,GUIStyle>();
    static readonly Color MenuInk=new Color(.23f,.14f,.08f);
    static readonly Color LineGold=new Color(.62f,.43f,.24f,.50f);
    static readonly string[] ModeIcons={"mode-base","mode-sea","mode-city","mode-kingdom","mode-harbor"};
    static readonly string[] ModeTitles={"基础版","航海家","城市与骑士","三国争霸","海港与捕鱼"};
    static readonly string[] SettingsTitles={"玩家","声音","桌面外观","主页风景"};

    internal static void Surface(Rect r,string key){
        if(Event.current.type!=EventType.Repaint)return;
        if(!menuSurfaces.TryGetValue(key,out var style)){
            int edge=key=="gilt-frame"?35:12;
            style=new GUIStyle{border=new RectOffset(edge,edge,edge,edge)};
            style.normal.background=Resources.Load<Texture2D>("AcceptedMenu/"+key);menuSurfaces[key]=style;
        }
        style.Draw(r,GUIContent.none,false,false,false,false);
    }
    void Sheet(Rect r){
        PastoralMenuUI.Round(new Rect(r.x+2,r.y+6,r.width,r.height),new Color(.11f,.06f,.03f,.27f),15);
        GUI.DrawTexture(new Rect(r.x+5,r.y+5,r.width-10,r.height-10),Art("parchment"));
        Surface(r,"gilt-frame");
    }
    void Illustration(Rect r,string key){
        var tex=Art(key);
        if(tex==null)return;
        float ratio=(float)tex.width/tex.height*Mathf.Abs(GUI.matrix.m11/GUI.matrix.m00);
        float width=Mathf.Min(r.width,r.height*ratio),height=width/ratio;
        GUI.DrawTexture(new Rect(r.center.x-width/2,r.center.y-height/2,width,height),tex);
    }
    void RuleLine(float x,float y,float w){
        PastoralMenuUI.Fill(new Rect(x,y,w,1),LineGold);
        PastoralMenuUI.Fill(new Rect(x,y+1,w,1),new Color(1,.98f,.88f,.6f));
    }
    void FieldLabel(float x,float y,string value){
        PastoralMenuUI.Hex(new Vector2(x+7,y+23),6,new Color(.67f,.46f,.24f));
        var style=Type(25);var align=style.alignment;style.alignment=TextAnchor.MiddleLeft;
        GUI.Label(new Rect(x+25,y,200,46),value,style);style.alignment=align;
    }
    void TitleBand(string title){
        BandedHeading(new Rect(panelBounds.x,124,panelBounds.width,65),title,40);
    }
    void BandedHeading(Rect r,string title,int size){
        float gap=Type(size).CalcSize(new GUIContent(title)).x*.5f+34;
        float length=Mathf.Min(170,r.width*.18f), y=r.center.y;
        float left=r.center.x-gap,right=r.center.x+gap;
        RuleLine(left-length,y,length);RuleLine(right,y,length);
        Illustration(new Rect(left-7,y-7,14,14),"diamond");
        Illustration(new Rect(right-7,y-7,14,14),"diamond");
        Caption(r,title,size);
    }
    void SetupFurniture(bool net){
        Sheet(panelBounds);
        FieldLabel(851,129,"模式");
        for(int i=0;i<ModeIcons.Length;i++){
            var x=855+i*130;
            Illustration(new Rect(x+4,174,122,109),ModeIcons[i]);
            Caption(new Rect(x-5,287,141,39),ModeTitles[i],21);
        }
        Surface(new Rect(848,330,646,44),"card");
        if(net){
            FieldLabel(858,385,"房间设置");
            FieldLabel(858,447,"房间名");FieldLabel(858,504,"密码");FieldLabel(858,557,"公开房间");
            FieldLabel(858,616,"人数");FieldLabel(858,677,"胜利分数");
            RuleLine(852,435,638);
        }else{
            FieldLabel(858,390,"地图");FieldLabel(858,447,"人数");
            FieldLabel(858,502,"胜利分数");FieldLabel(858,562,"AI 行动节奏");
            Surface(new Rect(156,628,1321,120),"card");
        }
        PastoralMenuUI.Fill(new Rect(830,175,1,net?551:432),LineGold);
        RuleLine(171,754,1315);
    }
}
