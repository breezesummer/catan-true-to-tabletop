using System.Collections.Generic;
using UnityEngine;

// 图集只保留框与按钮材质；名字、技能与座次均来自当前可见投影。
public static class GeneralFrameUI
{
    const string Root="GeneralUI/v001/";
    static Texture2D atlas,buttons;
    static Font brush;
    static GUIStyle lettering;
    static readonly Dictionary<string,Texture2D> portraits=new Dictionary<string,Texture2D>();
    static readonly Color Gold=new Color(1f,.82f,.36f);
    static readonly string[] SeatNumbers={"一","二","三","四"};
    static readonly Rect[] ButtonRegions={new Rect(252,51,750,270),new Rect(232,320,780,317),new Rect(252,652,750,272),new Rect(252,953,750,265)};
    static void Ensure()
    {
        if(atlas==null)atlas=Resources.Load<Texture2D>(Root+"frame-atlas");
        if(buttons==null)buttons=Resources.Load<Texture2D>(Root+"skill-buttons");
        if(lettering==null){brush=Resources.Load<Font>(Root+"SkillCalligraphy");lettering=new GUIStyle{font=brush??MobileTabletop.LoadFont(),alignment=TextAnchor.MiddleCenter,wordWrap=false};}
    }
    static void Slice(Texture2D texture,Rect source,Rect target,float width,float height)
    {
        if(texture!=null)GUI.DrawTextureWithTexCoords(target,texture,new Rect(source.x/width,1-source.yMax/height,source.width/width,source.height/height),true);
    }
    public static void Text(Rect area,string text,int size,Color? color=null)
    {
        Ensure();lettering.fontSize=size;lettering.normal.textColor=new Color(.12f,.09f,.04f,.95f);
        GUI.Label(new Rect(area.x+1,area.y+2,area.width,area.height),text,lettering);
        lettering.normal.textColor=color??Gold;GUI.Label(area,text,lettering);
    }
    public static void Portrait(Rect area,string id,string name,string faction,int position,bool highlighted,bool faceDown,int life)
    {
        Ensure();
        // 外发光与普通边框使用同一实际边界，避免切换回合时跳动。
        if(highlighted)Slice(atlas,new Rect(500,26,510,734),new Rect(area.x-11,area.y-15,area.width+22,area.height+30),1448,1086);
        Slice(atlas,new Rect(63,82,436,630),area,1448,1086);
        Texture2D portrait=null;
        if(!string.IsNullOrEmpty(id)&&!portraits.TryGetValue(id,out portrait)){portrait=Resources.Load<Texture2D>(Root+"Portraits/"+id);portraits[id]=portrait;}
        if(portrait!=null&&!faceDown)
        {
            // 源图400×472，左右透明边与顶部露头保留；头部高于卡框约12像素。
            var image=new Rect(area.x+area.width*.23f,area.y-12,area.width*.75f,area.height+10);
            GUI.DrawTextureWithTexCoords(image,portrait,new Rect(.08f,0,.8275f,1),true);
        }
        else Text(new Rect(area.x+26,area.y+40,area.width-30,64),faceDown?"暗置":name??"待登场",22);
        // 姓名条盖在人物左侧，顶部不覆盖露出的头饰。
        Slice(atlas,new Rect(70,105,94,575),new Rect(area.x+3,area.y+6,area.width*.21f,area.height-12),1448,1086);
        Text(new Rect(area.x+2,area.y+3,area.width*.22f,23),faction??"",17,new Color(.67f,.86f,.63f));
        if(name!=null)for(int i=0;i<name.Length;i++)Text(new Rect(area.x+2,area.y+28+i*22,area.width*.22f,24),name[i].ToString(),22,Color.white);
        if(life>=0)Text(new Rect(area.x+2,area.yMax-49,area.width*.22f,22),life.ToString(),17);
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
    public static void Skill(Rect area,string name,bool active,bool enabled,bool selected,bool flashing)
    {
        Ensure();bool hover=area.Contains(Event.current.mousePosition);
        if(active)
        {
            int state=!enabled?3:selected?2:hover||flashing?1:0;
            var source=ButtonRegions[state];
            Slice(buttons,source,area,1254,1254);
        }
        Text(area,name,name.Length>3?23:29,active&&!enabled?new Color(.56f,.51f,.32f):Gold);
    }
}
