using System.Collections.Generic;
using System.Linq;
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
        PastoralMenuUI.Round(r,new Color(.34f,.22f,.11f),9);
        PastoralMenuUI.Round(InsetMenuRect(r,2),new Color(.92f,.77f,.51f),7);
        PastoralMenuUI.Round(InsetMenuRect(r,4),new Color(.62f,.43f,.24f),5);
        GUI.DrawTexture(InsetMenuRect(r,6),Art("parchment"));
    }
    static Rect InsetMenuRect(Rect r,float inset){
        return new Rect(r.x+inset,r.y+inset,r.width-2*inset,r.height-2*inset);
    }
    void MenuMark(float x,float y,float halfWidth){
        var bronze=new Color(.54f,.32f,.13f);
        PastoralMenuUI.Fill(new Rect(x-halfWidth,y,halfWidth-6,2),bronze);
        PastoralMenuUI.Fill(new Rect(x+6,y,halfWidth-6,2),bronze);
        Illustration(new Rect(x-5,y-4,10,10),"diamond");
    }
    float mapPlaqueHover,startPlaqueHover;
    GUIStyle setupPlaqueText;
    Font inscriptionFont;
    string engravedMapName;
    Texture2D engravedMap;
    internal Vector2? SetupReviewPointer;

    bool SetupPlaque(Rect r,string label,bool start,bool enabled){
        if(Event.current.type==EventType.Repaint){
            var pointer=SetupReviewPointer??Event.current.mousePosition;
            bool over=enabled&&GUI.enabled&&r.Contains(pointer);
            ref float blend=ref (start?ref startPlaqueHover:ref mapPlaqueHover);
            blend=Mathf.MoveTowards(blend,over?1:0,Time.unscaledDeltaTime/.12f);
            var face=Art(start?"setup-start":"setup-map");
            var old=GUI.color;
            GUI.color=new Color(.20f,.10f,.04f,.36f*old.a);
            GUI.DrawTexture(new Rect(r.x,r.y+2.5f,r.width,r.height),face);
            GUI.color=enabled?old:new Color(.78f,.76f,.70f,old.a);
            GUI.DrawTexture(r,face);
            if(blend>0){
                var tint=GUI.color;tint.a*=blend;GUI.color=tint;
                GUI.DrawTexture(r,Art(start?"setup-start-hover":"setup-map-hover"));
            }
            GUI.color=old;
            if(!start)Picture(new Rect(r.x+r.width*.888f,r.y+r.height*.44f,r.width*.042f,r.height*.18f),"setup-arrow");
            var title=r;
            title.height-=start?3:2;
            if(!start){title.x+=20;title.width-=58;}
            if(start)PlateLettering(title,label,41,PlateInk.Gold);
            else DrawMapEngraving(title,label);
        }
        return enabled&&Hit(r,false);
    }
    void DrawMapEngraving(Rect area,string label){
        if(engravedMapName!=label){
            engravedMapName=label;
            engravedMap=Art("setup-engraved-"+label);
        }
        if(engravedMap==null){PlateLettering(area,label,23,PlateInk.Wood);return;}
        using(var pixels=new PastoralMenuUI.PixelText(area,25)){
            // 刻字母版每字 100 像素，以屏幕字号等比缩放，给木牌两端留白。
            float scale=Mathf.Min(pixels.Size/100f,pixels.Area.width/engravedMap.width,pixels.Area.height/engravedMap.height);
            float width=engravedMap.width*scale,height=engravedMap.height*scale;
            GUI.DrawTexture(new Rect(Mathf.Round(pixels.Area.center.x-width*.5f),Mathf.Round(pixels.Area.center.y-height*.5f),width,height),engravedMap);
        }
    }
    enum PlateInk{Wood,Dark,Jade,Gold,Brass}
    void PlateLettering(Rect r,string label,int size,PlateInk ink,bool strengthen=false){
        if(setupPlaqueText==null){
            setupPlaqueText=new GUIStyle(Type(24));
            inscriptionFont=pastoralMenuFont;
            foreach(var name in Font.GetOSInstalledFontNames()){
                if(name!="KaiTi"&&name!="楷体")continue;
                inscriptionFont=Font.CreateDynamicFontFromOSFont(name,42);break;
            }
        }
        setupPlaqueText.font=ink==PlateInk.Gold||ink==PlateInk.Brass?inscriptionFont:pastoralMenuFont;
        bool gilded=ink==PlateInk.Jade||ink==PlateInk.Gold;
        using(var pixels=new PastoralMenuUI.PixelText(r,size)){
            setupPlaqueText.fontSize=pixels.Size;
            var relief=pixels.Area;relief.y+=1;
            setupPlaqueText.normal.textColor=gilded?new Color32(64,37,16,145):new Color32(255,229,178,80);
            GUI.Label(relief,label,setupPlaqueText);
            Color32 faceInk=ink==PlateInk.Gold?new Color32(255,228,176,255):
                ink==PlateInk.Jade?new Color32(255,239,203,255):
                ink==PlateInk.Wood?new Color32(92,54,28,255):new Color32(90,58,31,255);
            if(ink==PlateInk.Wood||ink==PlateInk.Gold){
                // 只在刻痕下缘补一层淡墨或金箔，保留原字号、字距和细笔的收锋。
                var edge=pixels.Area;edge.y+=1;
                var edgeInk=faceInk;edgeInk.a=ink==PlateInk.Wood?(byte)95:(byte)75;
                setupPlaqueText.normal.textColor=edgeInk;
                GUI.Label(edge,label,setupPlaqueText);
            }
            if(strengthen){
                var stroke=pixels.Area;stroke.x+=1;
                var strokeInk=faceInk;strokeInk.a=125;
                setupPlaqueText.normal.textColor=strokeInk;
                GUI.Label(stroke,label,setupPlaqueText);
            }
            setupPlaqueText.normal.textColor=faceInk;
            GUI.Label(pixels.Area,label,setupPlaqueText);
        }
    }
    bool SmallPlate(Rect r,string asset,string label,int size){
        Picture(r,asset);
        var text=r;text.height-=4;
        PlateLettering(text,label,size,asset=="setup-return"?PlateInk.Brass:PlateInk.Dark);
        return Hit(r,false);
    }
    bool BackPlaque(Rect r,string label)=>SmallPlate(r,"setup-return",label,24);

    bool ModePlate(int i,bool selected){
        float x=854+i*130;
        var hit=new Rect(x,170,130,154);
        Illustration(new Rect(x+5,174,122,109),ModeIcons[i]);
        var plate=new Rect(x+1,283,125,40);
        Picture(plate,selected?"setup-jade":"setup-neutral");
        var title=plate;title.height-=4;
        PlateLettering(title,ModeTitles[i],17,selected?PlateInk.Jade:PlateInk.Dark);
        return Hit(hit,false);
    }
    void PlayerSelector(Rect r,bool edit){
        Picture(r,menuPlayers==3?"setup-segment":"setup-players");
        for(int count=3;count<=4;count++){
            var half=new Rect(r.x+(count-3)*r.width/2,r.y,r.width/2,r.height);
            bool allowed=!edit||Network.Room.Seats.Skip(count).All(s=>!s.Occupied||s.IsAi);
            bool selected=menuPlayers==count;
            var title=half;title.height-=6;
            PlateLettering(title,count==3?"3 人":"4 人",24,selected?PlateInk.Jade:PlateInk.Dark,strengthen:true);
            if(allowed&&Hit(half,false))menuPlayers=count;
        }
    }
    bool ScoreKey(Rect r,bool plus,bool enabled){
        bool held=enabled&&TabletopButtonFeedback.Held(r);
        if(held||!enabled){
            var face=r;
            if(held)face.y+=1;
            var tint=GUI.color;
            GUI.color=held?new Color(.91f,.83f,.71f,tint.a):new Color(.86f,.84f,.79f,tint.a);
            Picture(face,"setup-knob");
            GUI.color=tint;
        }
        var center=r.center;if(held)center.y+=1;
        var ink=enabled?new Color32(79,45,18,255):new Color32(158,135,102,255);
        PastoralMenuUI.Fill(new Rect(center.x-7.5f,center.y-1.2f,15,2.4f),ink);
        if(plus)PastoralMenuUI.Fill(new Rect(center.x-1.2f,center.y-7.5f,2.4f,15),ink);
        return enabled&&Hit(r,false);
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
        Picture(new Rect(x,y+16,14,14),"setup-bullet");
        Caption(new Rect(x+25,y,200,46),value,25,TextAnchor.MiddleLeft);
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
        if(net){
            FieldLabel(858,385,"房间设置");
            FieldLabel(858,447,"房间名");FieldLabel(858,504,"密码");FieldLabel(858,557,"公开房间");
            FieldLabel(858,616,"人数");FieldLabel(858,677,"胜利分数");
            RuleLine(852,435,638);
        }else{
            FieldLabel(850,362,"地图");FieldLabel(850,428,"人数");
            FieldLabel(850,495,"胜利分数");FieldLabel(850,558,"AI 行动节奏");
        }
        PastoralMenuUI.Fill(new Rect(830,175,1,net?551:432),LineGold);
        if(net)RuleLine(171,754,1315);
    }
}
