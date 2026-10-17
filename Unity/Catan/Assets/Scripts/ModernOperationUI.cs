using System;
using UnityEngine;

// 仅绘制操作界面；所有确认操作仍由各适配器提交到共同规则入口。
public static class ModernOperationUI
{
    public static Color Background => StrategyHudTheme.Background;
    public static Color Surface => StrategyHudTheme.Surface;
    public static Color TextColor => StrategyHudTheme.Text;
    public static Color Muted => StrategyHudTheme.Muted;
    public static Color Accent => StrategyHudTheme.Accent;
    public static Color Line => StrategyHudTheme.Line;
    public static readonly Color Error=new Color(.98f,.53f,.40f);
    static Font currentFont;
    static int themeVersion=-1;
    public static GUIStyle Text, Small, Title, Center;
    public static void Styles(Font font=null)
    {
        if(Text!=null&&(font==null||font==currentFont)&&themeVersion==StrategyHudTheme.Version)return;
        themeVersion=StrategyHudTheme.Version;
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
        // 单次圆角绘制，避免九块贴图在非整数屏幕缩放下出现暗色拼接缝。
        float radius=Mathf.Min(8,Mathf.Min(r.width,r.height)/2);
        GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);
    }
    public static void Panel(Rect r)
    {
        Round(new Rect(r.x-5,r.y+7,r.width+10,r.height+9),new Color(0,0,0,.20f));
        Round(r,Line);Round(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),Background);
    }
    public static void Label(Rect r,string value,GUIStyle style=null){Styles();GUI.Label(r,value,style??Text);}
    public static bool Button(Rect r,string caption,bool enabled=true,bool primary=false,bool selected=false,string playerId=null)
    {
        Styles();bool old=GUI.enabled;GUI.enabled=old&&enabled;
        bool hover=GUI.enabled&&r.Contains(Event.current.mousePosition);
        Color bg=primary?Accent:selected?Color.Lerp(Surface,Accent,.24f):Surface;
        if(hover)bg=Color.Lerp(bg,TextColor,.09f);
        Round(r,GUI.enabled?bg:Color.Lerp(Background,Surface,.6f));
        if(selected)Fill(new Rect(r.x+9,r.yMax-3,r.width-18,2),Accent);
        var label=new GUIStyle(Center);label.normal.textColor=!GUI.enabled?Muted:primary?Background:TextColor;
        if(playerId!=null)label=M6App.PlayerNameStyle(label,playerId);
        GUI.Label(r,caption,label);bool result=GUI.Button(r,GUIContent.none,GUIStyle.none);GUI.enabled=old;
        result=result&&(!MobileTabletop.Enabled||!MobileTabletop.TouchDragged);
        if(result)TabletopAudio.Instance?.Click();return result;
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
                Round(r,i==value?Color.Lerp(Surface,Accent,.24f):Surface);
                OperationIconAssets.DrawGood(new Rect(r.center.x-13,r.y+5,26,26),firstGood+i);
                Label(new Rect(r.x,r.y+31,r.width,25),labels[i],Center);
                if(i==value)Fill(new Rect(r.x+7,r.yMax-3,r.width-14,2),Accent);
                if(GUI.Button(r,GUIContent.none,GUIStyle.none)&&!MobileTabletop.TouchDragged)value=i;
            }
            else if(Button(r,M6App.DisplayPlayerText(labels[i]),true,false,i==value,PlayerIdentityModel.IsPlayerId(labels[i])?labels[i]:null))value=i;
        }
        return value;
    }
    public static void Cost(int[] goods)
    {
        Styles();var r=GUILayoutUtility.GetRect(GUIContent.none,GUIStyle.none,GUILayout.Height(42),GUILayout.ExpandWidth(true));float x=r.x;
        for(int i=0;i<goods.Length&&i<8;i++)if(goods[i]>0)
        {OperationIconAssets.DrawGood(new Rect(x,r.y+4,30,30),i);Label(new Rect(x+33,r.y+4,32,30),"×"+goods[i],Center);x+=72;}
    }
    static readonly string[] GoodNames={"木头","砖块","羊毛","小麦","矿石","布匹","钱币","纸张"};
    public static string ResourceCards(string caption,int[] selected,int[] limits,int totalLimit=int.MaxValue,int count=8,bool singleKind=false)
    {
        Styles();Info(caption+" · 已选 "+System.Linq.Enumerable.Sum(selected)+" 张");Info(TabletopCardInput.Hint);
        int columns=Math.Min(4,count);const float cardWidth=58,height=86,row=115;
        var area=GUILayoutUtility.GetRect(GUIContent.none,GUIStyle.none,GUILayout.Height(((count+columns-1)/columns)*row),GUILayout.ExpandWidth(true));
        string error=null;
        for(int i=0;i<count;i++)
        {
            float cell=area.width/columns;var face=new Rect(area.x+(i%columns)*cell+(cell-cardWidth)/2,area.y+(i/columns)*row,cardWidth,height);
            EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(i));
            Label(new Rect(area.x+(i%columns)*cell,face.yMax,cell,27),GoodNames[i]+" × "+selected[i],Center);
            int delta=TabletopCardInput.Delta(face);
            if(delta!=0)error=OperationCardQuantity.Change(selected,i,delta,limits[i],totalLimit,singleKind);
        }
        return error;
    }
    public static int ResourceKind(string caption,int value,int firstGood,int count)
    {
        Styles();Info(caption+" · "+TabletopCardInput.Hint);
        int columns=Math.Min(4,count);const float height=90,row=118,width=60;
        var area=GUILayoutUtility.GetRect(GUIContent.none,GUIStyle.none,GUILayout.Height(((count+columns-1)/columns)*row),GUILayout.ExpandWidth(true));
        for(int i=0;i<count;i++)
        {
            float cell=area.width/columns;var face=new Rect(area.x+(i%columns)*cell+(cell-width)/2,area.y+(i/columns)*row,width,height);
            EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(firstGood+i));
            Label(new Rect(area.x+(i%columns)*cell,face.yMax,cell,27),GoodNames[firstGood+i]+" × "+(value==i?1:0),Center);
            int delta=TabletopCardInput.Delta(face);if(delta>0)value=i;else if(delta<0&&value==i)value=-1;
        }
        return value;
    }
}
