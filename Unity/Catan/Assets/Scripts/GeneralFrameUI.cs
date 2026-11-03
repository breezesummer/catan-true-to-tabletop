using System.Collections.Generic;
using UnityEngine;

// 图集只保留框与按钮材质；名字、技能与座次均来自当前可见投影。
public static class GeneralFrameUI
{
    const string Root="GeneralUI/v001/";
    static Texture2D glow;
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
        if(glow==null)glow=Resources.Load<Texture2D>("GeneralUI/v002/turn-glow");
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
        // 外发光与普通边框使用同一实际边界，避免切换回合时跳动。
        if(highlighted&&!StrategyHudTheme.Ocean)Slice(glow,new Rect(((int)(Time.unscaledTime*10)%10)*240,0,240,270),new Rect(area.x-area.width*.26f,area.y-area.height*.15f,area.width*1.52f,area.height*1.18f),2400,270);
        string camp=faction=="魏"?"wei":faction=="蜀"?"shu":faction=="吴"?"wu":faction=="晋"?"jin":faction=="神"?"shen":"qun";
        if(!frames.TryGetValue(camp,out var frame)){frame=Resources.Load<Texture2D>("GeneralUI/v002/frame-"+camp);frames[camp]=frame;}
        if(StrategyHudTheme.Ocean)OceanTabletopSkin.Panel(area,highlighted);
        else if(frame!=null)GUI.DrawTexture(area,frame,ScaleMode.StretchToFill,true);
        Texture2D portrait=null;
        if(!string.IsNullOrEmpty(id)&&!portraits.TryGetValue(id,out portrait)){portrait=Resources.Load<Texture2D>(Root+"Portraits/"+id);portraits[id]=portrait;}
        if(portrait!=null&&!faceDown)
        {
            // 源图400×472，左右透明边与顶部露头保留；头部高于卡框约12像素。
            var image=new Rect(area.x+area.width*.155f,area.y-12*portraitScale,area.width*.83f,area.height+10*portraitScale);
            GUI.DrawTextureWithTexCoords(image,portrait,new Rect(.08f,0,.8275f,1),true);
        }
        else Text(new Rect(area.x+area.width*.2f,area.y+area.height*.25f,area.width*.75f,area.height*.4f),faceDown?"暗置":name??"",22);
        if(StrategyHudTheme.Ocean)OceanTabletopSkin.Frame(area,highlighted,true);
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
