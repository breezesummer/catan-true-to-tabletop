using System.Collections.Generic;
using UnityEngine;

// 图集只保留框与按钮材质；名字、技能与座次均来自当前可见投影。
public static class GeneralFrameUI
{
    const string Root="GeneralUI/v001/";
    static Material portraitInk;
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
        if(portraitInk==null)portraitInk=new Material(Resources.Load<Shader>("GeneralUI/v002/PortraitInk")){hideFlags=HideFlags.HideAndDontSave};
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
    public static void Portrait(Rect area,string id,string name,string faction,int position,bool highlighted,bool faceDown,int life,bool own=false)
    {
        Ensure();float portraitScale=area.width/138f;
        string camp=faction=="魏"?"wei":faction=="蜀"?"shu":faction=="吴"?"wu":faction=="晋"?"jin":faction=="神"?"shen":"qun";
        if(!frames.TryGetValue(camp,out var frame)){frame=Resources.Load<Texture2D>("GeneralUI/v002/frame-"+camp);frames[camp]=frame;}
        if(StrategyHudTheme.Ocean)OceanTabletopSkin.Panel(area);
        else if(frame!=null)GUI.DrawTexture(area,frame,ScaleMode.StretchToFill,true);
        // 整个框面和流光都在立绘之后，顶边遇到发冠自然断开。
        if(own&&StrategyHudTheme.Ocean)Flow(new Rect(area.x+3,area.y+3,area.width-6,area.height-6),true);
        if(highlighted)Flow(area,false);
        Texture2D portrait=null;
        if(!string.IsNullOrEmpty(id)&&!portraits.TryGetValue(id,out portrait)){portrait=Resources.Load<Texture2D>(Root+"Portraits/"+id);portraits[id]=portrait;}
        if(portrait!=null&&!faceDown)
        {
            // 原图中间约 83% 是印刷画面，两侧透明区也可能有发丝、兵器，整幅绘制。
            float fullWidth=area.width*.86f/.8275f;
            float lift=area.height*.073f;
            var image=new Rect(area.x+area.width*.12f-fullWidth*.08f,area.y-lift,fullWidth,area.height+lift-2*portraitScale);
            if(Event.current.type==EventType.Repaint)
                Graphics.DrawTexture(image,portrait,new Rect(0,0,1,1),0,0,0,0,Color.white,portraitInk);
        }
        else Text(new Rect(area.x+area.width*.2f,area.y+area.height*.25f,area.width*.75f,area.height*.4f),faceDown?"暗置":name??"",22);
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
    public static void Flow(Rect r,bool blue)
    {
        if(Event.current.type!=EventType.Repaint)return;
        var color=blue?new Color(.15f,.66f,1f):new Color(1f,.82f,.16f);
        GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,blue?2.6f:1.6f,6);
        // 两束亮光绕框行走；顶、底边与两侧共享同一条连续路径。
        float length=2*(r.width+r.height);
        for(int i=0;i<12;i++)
        {
            float distance=Mathf.Repeat(Time.unscaledTime*82+(i/6)*length*.5f-(i%6)*2.5f,length);
            Vector2 p=distance<r.width?new Vector2(r.x+distance,r.y):distance<r.width+r.height?new Vector2(r.xMax,r.y+distance-r.width):
                distance<2*r.width+r.height?new Vector2(r.xMax-(distance-r.width-r.height),r.yMax):new Vector2(r.x,r.yMax-(distance-2*r.width-r.height));
            var glint=Color.Lerp(color,Color.white,.85f);glint.a=1-(i%6)/6f;
            GUI.DrawTexture(new Rect(p.x-2,p.y-2,4,4),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,glint,0,2);
        }
    }
    public static void Avatar(Rect area,string id,bool faceDown)
    {
        Ensure();
        if(faceDown){Text(area,"暗置",12);return;}
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
            if(StrategyHudTheme.Ocean)
            {
                bool prior=GUI.enabled;GUI.enabled=prior&&enabled;OceanTabletopSkin.Panel(area,state==1,false);GUI.enabled=prior;
            }
            else Slice(buttons[state],new Rect(28,115,990,530),area,1045,669);
        }
        int size=Mathf.Min(name.Length>3?23:27,Mathf.FloorToInt((area.width-18)/Mathf.Max(1,name.Length)),Mathf.FloorToInt(area.height*.6f));
        Text(new Rect(area.x+6,area.y,area.width-12,area.height*.82f),name,size,active&&!enabled?new Color(.56f,.51f,.32f):Gold);
    }
    public static bool Choice(Rect area,string name,bool enabled=true,bool selected=false)
    {
        Skill(area,name,true,enabled,selected,false);
        return ThreeKingdomsDirectInput.Read(area,enabled&&GUI.enabled,false)==1;
    }
}
