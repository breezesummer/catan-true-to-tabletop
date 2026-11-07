using System.Collections.Generic;
using UnityEngine;

// 图集只保留框与按钮材质；名字、技能与座次均来自当前可见投影。
public static class GeneralFrameUI
{
    const string Root="GeneralUI/v001/";
    public enum TurnGlow { Green, Yellow, Blue }
    static readonly string[] glowPaths={"GeneralUI/v002/turn-glow","GeneralUI/v002/turn-glow-yellow","GeneralUI/v002/turn-glow-blue"};
    static readonly Texture2D[] glowSheets=new Texture2D[glowPaths.Length];
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
    public static void Text(Rect area,string text,int size,Color? color=null)
    {
        Ensure();lettering.fontSize=size;TabletopTextFit.Fit(lettering,text,area,8);lettering.normal.textColor=new Color(.12f,.09f,.04f,.95f);
        GUI.Label(new Rect(area.x+1,area.y+2,area.width,area.height),text,lettering);
        lettering.normal.textColor=color??Gold;GUI.Label(area,text,lettering);
    }
    public static void Portrait(Rect area,string id,string name,string faction,int position,bool highlighted,bool faceDown,int life)
    {
        Ensure();float portraitScale=area.width/138f;
        string camp=faction=="魏"?"wei":faction=="蜀"?"shu":faction=="吴"?"wu":faction=="晋"?"jin":faction=="神"?"shen":"qun";
        if(!frames.TryGetValue(camp,out var frame)){frame=Resources.Load<Texture2D>("GeneralUI/v002/frame-"+camp);frames[camp]=frame;}
        if(highlighted)
        {
            if(StrategyHudTheme.HandPainted)HandPaintedTabletopSkin.TurnBorder(area);
            else DrawPortraitGlow(area,StrategyHudTheme.Ocean?TurnGlow.Blue:TurnGlow.Green);
        }
        var skin=faceDown?null:GeneralSkinArt.Get(id);
        bool dynamic=GeneralSkinArt.IdleReady(skin);
        if(dynamic)
        {
            // 手杀动皮使用独立势力顶弧，保留左侧姓名栏。
            Slice(frame,new Rect(0,0,29,228),new Rect(area.x,area.y,area.width*29/170,area.height),170,228);
            var inner=Rect.MinMaxRect(area.x+area.width*29/170,area.y-area.height*.09f,
                area.x+area.width*167/170,area.y+area.height*225/228);
            GeneralSkinArt.Idle(inner,skin);
        }
        else if(frame!=null)GUI.DrawTexture(area,frame,ScaleMode.StretchToFill,true);
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
        if(name!=null&&area.width>=70)for(int i=0;i<name.Length;i++)Text(new Rect(area.x+1,area.y+(22+i*19)*portraitScale,area.width*.19f,22*portraitScale),name[i].ToString(),Mathf.Max(6,Mathf.RoundToInt(20*portraitScale)),Color.white);
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
    static void DrawPortraitGlow(Rect area,TurnGlow color)
    {
        if(Event.current.type!=EventType.Repaint)return;
        var sheet=GlowSheet(color);
        if(sheet==null)return;
        // 手杀光带上方敞开。沿中线翻转下半帧，将三条亮边闭合成完整外圈。
        // 每帧左右亮脊位于 x=52、187，底部位于 y=251，对准势力框外沿。
        float pad=area.width*2/138f;
        float sx=(area.width+2*pad)/(187-52);
        float sy=(area.height*.5f+pad)/(251-140);
        // 两半共用一个物理像素边界，避免非整数缩放时在光带中段漏出一行底色。
        var seam=GUIUtility.GUIToScreenPoint(area.center);
        seam.y=Mathf.Round(seam.y);
        float middle=GUIUtility.ScreenToGUIPoint(seam).y;
        var half=new Rect(area.x-pad-52*sx,middle,240*sx,area.center.y+130*sy-middle);
        float u=GlowFrame()/10f;
        GUI.DrawTextureWithTexCoords(half,sheet,new Rect(u,0,.1f,130/270f));
        half.yMin=area.center.y-130*sy;
        half.yMax=middle;
        GUI.DrawTextureWithTexCoords(half,sheet,new Rect(u,130/270f,.1f,-130/270f));
    }
    public static void DrawTargetGlow(Rect area)
    {
        if(Event.current.type!=EventType.Repaint)return;
        var clip=new Rect(area.x+2,area.y+2,area.width-4,area.height-4);
        var prior=GUI.color;
        GUI.BeginGroup(clip);
        GUI.color=prior*new Color(1f,.075f,.09f,.77f);
        DrawPortraitGlow(new Rect(3,3,clip.width-6,clip.height-6),TurnGlow.Yellow);
        GUI.color=prior;
        GUI.EndGroup();
    }
    static int GlowFrame()=>Mathf.FloorToInt(Mathf.Repeat(Time.unscaledTime,.9f)*10f/.9f);
    static Texture2D GlowSheet(TurnGlow color)
    {
        int index=(int)color;
        if(glowSheets[index]==null)glowSheets[index]=Resources.Load<Texture2D>(glowPaths[index]);
        return glowSheets[index];
    }
    public static void DrawTurnGlow(Rect area,TurnGlow color)
    {
        if(Event.current.type!=EventType.Repaint)return;
        var bounds=new Rect(area.x-area.width*.26f,area.y-area.height*.15f,area.width*1.52f,area.height*1.18f);
        Slice(GlowSheet(color),new Rect(GlowFrame()*240,0,240,270),bounds,2400,270);
    }
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
    public static void Skill(Rect area,string name,bool active,bool enabled,bool selected,bool flashing)
    {
        Ensure();bool hover=area.Contains(Event.current.mousePosition);
        if(active)
        {
            int state=!enabled?2:selected||hover||flashing?1:0;
            // 原图含透明留边；只裁去留边，不拉伸或裁掉按钮轮廓。
            if(StrategyHudTheme.HandPainted || StrategyHudTheme.Ocean)
            {
                bool prior=GUI.enabled;GUI.enabled=prior&&enabled;CopperTabletopSkin.Panel(area,StrategyHudTheme.Surface,state==1,false);GUI.enabled=prior;
            }
            else Slice(buttons[state],new Rect(28,115,990,530),area,1045,669);
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
        Text(new Rect(area.x+6,area.y,area.width-12,area.height*.82f),name,size,active&&!enabled?new Color(.56f,.51f,.32f):Gold);
    }
    public static bool Choice(Rect area,string name,bool enabled=true,bool selected=false)
    {
        Skill(area,name,true,enabled,selected,false);
        return ThreeKingdomsDirectInput.Read(area,enabled&&GUI.enabled,false)==1;
    }
}
