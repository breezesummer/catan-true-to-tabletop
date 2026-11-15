using System.Collections.Generic;
using UnityEngine;

// 图集只保留框与按钮材质；名字、技能与座次均来自当前可见投影。
public static class GeneralFrameUI
{
    const string Root="GeneralUI/v001/";
    static readonly Texture2D[,] portraitGlows=new Texture2D[2,3];
    static readonly string[] glowColors={"green","blue","yellow"};
    static Texture2D panelGlow;
    static readonly Dictionary<string,Texture2D> frames=new Dictionary<string,Texture2D>();
    static readonly Texture2D[] buttons=new Texture2D[3];
    static Texture2D hpGreen,hpYellow,hpRed,hpEmpty;
    static Font brush,nameBrush;
    static GUIStyle lettering;
    static readonly Dictionary<string,Texture2D> portraits=new Dictionary<string,Texture2D>();
    static readonly Color Gold=new Color(1f,.82f,.36f);
    static readonly string[] SeatNumbers={"一","二","三","四"};
    static void Ensure()
    {
        for(int i=0;i<3;i++)if(buttons[i]==null)buttons[i]=Resources.Load<Texture2D>("GeneralUI/v002/skill-"+i);
        if(nameBrush==null)nameBrush=Resources.Load<Font>("GeneralUI/v002/NameCalligraphy");
        if(hpGreen==null)hpGreen=Resources.Load<Texture2D>(Root+"hp-green");
        if(hpYellow==null)hpYellow=Resources.Load<Texture2D>(Root+"hp-yellow");
        if(hpRed==null)hpRed=Resources.Load<Texture2D>(Root+"hp-red");
        if(hpEmpty==null)hpEmpty=Resources.Load<Texture2D>(Root+"hp-empty");
        if(lettering==null){brush=Resources.Load<Font>("GeneralUI/v002/SkillCalligraphy");lettering=new GUIStyle{font=brush??nameBrush??MobileTabletop.LoadFont(),alignment=TextAnchor.MiddleCenter,wordWrap=false};}
    }
    static void Slice(Texture2D texture,Rect source,Rect target,float width,float height)
    {
        if(texture!=null)GUI.DrawTextureWithTexCoords(target,texture,new Rect(source.x/width,1-source.yMax/height,source.width/width,source.height/height),true);
    }
    public static void Text(Rect area,string text,int size,Color? color=null,bool shadow=true)
    {
        Ensure();lettering.fontSize=size;TabletopTextFit.Fit(lettering,text,area,8);lettering.normal.textColor=new Color(.12f,.09f,.04f,.95f);
        if(shadow)GUI.Label(new Rect(area.x+1,area.y+2,area.width,area.height),text,lettering);
        lettering.normal.textColor=color??Gold;GUI.Label(area,text,lettering);
    }
    public static void Portrait(Rect area,string id,string name,string faction,int position,bool highlighted,bool faceDown,int life)
    {
        Ensure();float portraitScale=area.width/138f;
        string camp=faction=="魏"?"wei":faction=="蜀"?"shu":faction=="吴"?"wu":faction=="晋"?"jin":faction=="神"?"shen":"qun";
        var skin=faceDown?null:GeneralSkinArt.Get(id);
        bool dynamic=GeneralSkinArt.IdleReady(skin);
        if(highlighted)DrawPortraitGlow(area,dynamic);
        if(dynamic)GeneralSkinArt.Idle(area,skin);
        else
        {
            if(!frames.TryGetValue(camp,out var frame)){frame=Resources.Load<Texture2D>("GeneralUI/v002/frame-"+camp);frames[camp]=frame;}
            if(frame!=null)GUI.DrawTexture(area,frame,ScaleMode.StretchToFill,true);
        }
        Texture2D portrait=null;
        if(!string.IsNullOrEmpty(id)&&!portraits.TryGetValue(id,out portrait)){portrait=Resources.Load<Texture2D>(Root+"Portraits/"+id);portraits[id]=portrait;}
        if(!dynamic&&portrait!=null&&!faceDown)
        {
            // 170×228 势力框的画芯是 (29,4) 到 (167,225)。皮肤前 29 行是透明露头区。
            float top=area.y+area.height*4/228f;
            float bottom=area.y+area.height*225/228f;
            float lift=(bottom-top)*29/442f;
            var image=Rect.MinMaxRect(area.x+area.width*29/170f,top-lift,
                area.x+area.width*167/170f,bottom);
            Slice(portrait,new Rect(32,0,331,471),image,400,472);
        }
        else if(!dynamic)Text(new Rect(area.x+area.width*.2f,area.y+area.height*.25f,area.width*.75f,area.height*.4f),faceDown?"暗置":name??"",22);
        // 姓名条盖在人物左侧，顶部不覆盖露出的头饰。
        var previousFont=lettering.font;lettering.font=nameBrush??previousFont;
        float labelWidth=area.width*(dynamic ? .16f : .19f);
        if(dynamic&&!string.IsNullOrEmpty(faction)&&area.width>=70)
            Text(new Rect(area.x+1,area.y+area.height*.09f,labelWidth,18*portraitScale),faction,
                Mathf.Max(6,Mathf.RoundToInt(15*portraitScale)),FactionInk(camp),shadow:false);
        float nameTop=dynamic?area.height*.19f:22*portraitScale;
        if(name!=null&&area.width>=70)for(int i=0;i<name.Length;i++)Text(new Rect(area.x+1,area.y+nameTop+i*19*portraitScale,labelWidth,22*portraitScale),name[i].ToString(),Mathf.Max(6,Mathf.RoundToInt(20*portraitScale)),Color.white);
        lettering.font=previousFont;
        // 勾玉限制在左侧姓名栏内，宽度随武将框缩放，避免压到立绘或外框。
        if(life>=0)Health(new Rect(area.x+3,area.y+area.height*.60f,Mathf.Max(1,area.width*.155f-6),area.height*.30f),life,3);
        if(position>=0&&position<SeatNumbers.Length)
        {
            // 只画中文数字，黑字浅色描边压在框底，不再使用“号位”底牌。
            var number=new Rect(area.x+22,area.yMax-21,area.width-25,30);
            lettering.fontSize=30;lettering.normal.textColor=new Color(.95f,.93f,.83f);
            for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                if(x!=0||y!=0)GUI.Label(new Rect(number.x+x,number.y+y,number.width,number.height),SeatNumbers[position],lettering);
            lettering.normal.textColor=new Color(.025f,.025f,.02f);GUI.Label(number,SeatNumbers[position],lettering);
        }
    }
    static Color FactionInk(string camp)
    {
        switch(camp)
        {
            case "wei": return new Color32(128,188,248,255);
            case "shu": return new Color32(255,130,127,255);
            case "wu": return new Color32(143,220,136,255);
            case "jin": return new Color32(211,167,244,255);
            case "shen": return Gold;
            default: return new Color32(222,212,181,255);
        }
    }
    static void DrawPortraitGlow(Rect area,bool dynamic)
    {
        if(Event.current.type!=EventType.Repaint)return;
        int shape=dynamic?1:0,color=StrategyHudTheme.Ocean?1:StrategyHudTheme.HandPainted?2:0;
        var sheet=portraitGlows[shape,color];
        if(sheet==null)
        {
            string name=dynamic?"dynamic":"static";
            portraitGlows[shape,color]=sheet=Resources.Load<Texture2D>("GeneralUI/v002/portrait-glow-"+name+"-"+glowColors[color]);
        }
        DrawGlowFrame(area,sheet,900f);
    }
    static void DrawGlowFrame(Rect area,Texture2D sheet,float bottomLimit=float.PositiveInfinity)
    {
        if(sheet==null)return;
        int frame=Mathf.FloorToInt(Mathf.Repeat(Time.unscaledTime,.9f)*10f/.9f);
        float dx=area.width*40/256,dy=area.height*40/352;
        var bounds=new Rect(area.x-dx,area.y-dy,area.width+2*dx,area.height+2*dy);
        var uv=new Rect((frame%5)/5f,(1-frame/5)/2f,.2f,.5f);
        if(bounds.yMax<=bottomLimit)
        {
            GUI.DrawTextureWithTexCoords(bounds,sheet,uv,true);
            return;
        }
        // 保留牌面轮廓的位置，把框外的下沿辉光收进底部留白。
        float lowerUV=uv.height*dy/bounds.height;
        GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(bounds.x,bounds.y,bounds.xMax,area.yMax),sheet,
            Rect.MinMaxRect(uv.x,uv.y+lowerUV,uv.xMax,uv.yMax),true);
        if(bottomLimit>area.yMax)
            GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(bounds.x,area.yMax,bounds.xMax,bottomLimit),sheet,
                new Rect(uv.x,uv.y,uv.width,lowerUV),true);
    }
    static void DrawPanelHalo(Rect area,Color color)
    {
        if(Event.current.type!=EventType.Repaint)return;
        var previous=GUI.color;
        GUI.color=previous*color;
        if(panelGlow==null)panelGlow=Resources.Load<Texture2D>("GeneralUI/v002/portrait-glow-seat-yellow");
        DrawGlowFrame(area,panelGlow);
        GUI.color=previous;
    }
    public static void DrawTargetGlow(Rect area)=>DrawPanelHalo(area,new Color(1,.12f,.12f));
    public static void DrawTurnGlow(Rect area)=>DrawPanelHalo(area,Color.white);
    public static void Avatar(Rect area,string id,bool faceDown)
    {
        Ensure();
        if(faceDown){Text(area,"暗置",12);return;}
        var skin=GeneralSkinArt.Get(id);
        if(GeneralSkinArt.IdleReady(skin)){GeneralSkinArt.Idle(area,skin);return;}
        if(!portraits.TryGetValue(id,out var portrait)){portrait=Resources.Load<Texture2D>(Root+"Portraits/"+id);portraits[id]=portrait;}
        if(portrait!=null)GUI.DrawTextureWithTexCoords(area,portrait,new Rect(.08f,.40f,.8275f,.60f),true);
    }
    static void Health(Rect area,int current,int maximum)
    {
        if(maximum<=0)return;
        float size=Mathf.Min(18,area.width,area.height/maximum);
        var filled=current>=maximum?hpGreen:current>1?hpYellow:hpRed;
        for(int i=0;i<maximum;i++)GUI.DrawTexture(new Rect(area.center.x-size/2,area.y+i*size,size,size),i<current?filled:hpEmpty,ScaleMode.ScaleToFit,true);
    }
    public static void SkillBackground(Rect area,int state)
    {
        Ensure();
        if(StrategyHudTheme.HandPainted || StrategyHudTheme.Ocean)
            CopperTabletopSkin.Panel(area,StrategyHudTheme.Surface,state==1,false);
        else Slice(buttons[state],new Rect(28,115,990,530),area,1045,669);
    }
    public static void Skill(Rect area,string name,bool active,bool enabled,bool selected,bool flashing,bool largeCentered=false)
    {
        Ensure();bool hover=area.Contains(Event.current.mousePosition);
        if(active)
        {
            bool prior=GUI.enabled;
            if(StrategyHudTheme.HandPainted || StrategyHudTheme.Ocean)GUI.enabled=prior&&enabled;
            SkillBackground(area,!enabled?2:selected||hover||flashing?1:0);
            GUI.enabled=prior;
        }
        var ink=active&&!enabled?new Color(.56f,.51f,.32f):Gold;
        if(ModernOperationUI.DrawWindowSymbol(area,name,ink))return;
        if(largeCentered)
        {
            Text(new Rect(area.x+4,area.y,area.width-8,area.height),name,Mathf.FloorToInt(area.height*.82f),
                StrategyHudTheme.HandPainted?(enabled?StrategyHudTheme.Text:StrategyHudTheme.Muted):ink);
            return;
        }
        int size=Mathf.Min(name.Length>3?23:27,Mathf.FloorToInt((area.width-18)/Mathf.Max(1,name.Length)),Mathf.FloorToInt(area.height*.6f));
        if(active && StrategyHudTheme.HandPainted)
        {
            var caption=new Rect(area.x+6,area.y,area.width-12,area.height*.82f);
            lettering.fontSize=size;
            TabletopTextFit.Fit(lettering,name,caption,8);
            lettering.normal.textColor=enabled?StrategyHudTheme.Text:StrategyHudTheme.Muted;
            GUI.Label(caption,name,lettering);
            return;
        }
        Text(new Rect(area.x+6,area.y,area.width-12,StrategyHudTheme.Ocean?area.height:area.height*.82f),name,size,ink);
    }
    public static bool Choice(Rect area,string name,bool enabled=true,bool selected=false,bool largeCentered=false)
    {
        Skill(area,name,true,enabled,selected,false,largeCentered);
        return ThreeKingdomsDirectInput.Read(area,enabled&&GUI.enabled,false)==1;
    }
}
