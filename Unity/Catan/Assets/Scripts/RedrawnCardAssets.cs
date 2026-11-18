using System;
using System.Collections.Generic;
using UnityEngine;

// 插画与正式规则文字分离，卡面显示不参与规则或隐藏信息授权。
public static class RedrawnCardAssets
{
    public const string Root="CardRedraw/v002/";
    static readonly Dictionary<Texture2D,int> labels=new Dictionary<Texture2D,int>();
    static readonly string[] baseIds={"knight","road-building","year-of-plenty","monopoly","victory-point"};
    static readonly string[] baseNames={"骑士","道路建设","丰收","垄断","胜利点"};
    static readonly string[] baseDescriptions={"移动强盗，并从相邻玩家手中随机抽取一张资源牌。","免费建造两条道路；航海家中可以建造道路或船只。","从银行获得任意两张资源牌。","选择一种资源，其他玩家交出该种资源的全部手牌。","1分。在你的回合达到获胜条件时展示。"};
    static GUIStyle heading,body;
    static readonly GUIContent[] contents=new GUIContent[30];
    static readonly Vector2[] fittedAreas=new Vector2[30];
    static readonly int[] fittedSizes=new int[30];
    // 按原图标定图标右缘与右端花纹之间的彩色顶栏，不能用整张牌的统一比例代替。
    static Rect Bar(float left,float top,float right,float bottom,bool wide=false)=>new Rect(left/(wide?1086:1024),top/(wide?1448:1536),(right-left)/(wide?1086:1024),(bottom-top)/(wide?1448:1536));
    static readonly Rect[] titleBars={
        Bar(214,76,850,174,true), // 炼金术士
        Bar(232,77,889,194,true), // 起重机
        Bar(222,76,854,173,true), // 工程师
        Bar(214,80,844,185,true), // 发明家
        Bar(217,79,830,186,true), // 灌溉
        Bar(212,89,795,201), // 药物
        Bar(223,81,875,188,true), // 采矿
        Bar(222,81,875,188,true), // 印刷厂
        Bar(210,68,777,182), // 兴建道路
        Bar(217,77,791,198), // 锻冶匠
        Bar(213,79,776,207), // 商港
        Bar(210,80,786,208), // 大商家
        Bar(212,76,845,187,true), // 商人
        Bar(214,86,778,211), // 贸易船队
        Bar(212,60,858,175,true), // 垄断资源
        Bar(196,60,882,190,true), // 垄断商品
        Bar(218,78,785,210), // 外交官
        Bar(219,78,784,210), // 间谍
        Bar(219,80,782,209), // 指挥官
        Bar(220,78,791,207), // 权谋
        Bar(221,80,786,213), // 主教
        Bar(222,80,790,212), // 石碑
        Bar(218,81,786,213), // 叛徒
        Bar(222,81,783,210), // 婚礼
        Bar(221,81,791,211), // 破坏者
        Bar(211,62,773,165), // 骑士
        Bar(211,62,774,165), // 道路建设
        Bar(211,62,776,165), // 丰收
        Bar(211,62,776,165), // 垄断
        Bar(211,62,775,165) // 胜利点
    };
    struct TitleMetrics { public Rect Ink; public Vector2 Offset; }
    static readonly Dictionary<int,TitleMetrics> titleMetrics=new Dictionary<int,TitleMetrics>();
    static readonly GUIContent[] titleContents=new GUIContent[30];
    static Rect TitleArea(Rect face,int index)
    {var bar=titleBars[index];return new Rect(face.x+face.width*bar.x,face.y+face.height*bar.y,face.width*bar.width,face.height*bar.height);}
    static TitleMetrics MeasureTitle(int index,string text,int size)
    {
        int key=index*129+size;
        if(titleMetrics.TryGetValue(key,out var metrics))return metrics;
        var font=heading.font; font.RequestCharactersInTexture(text,size,FontStyle.Bold);
        float pen=0,minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;
        foreach(char character in text)if(font.GetCharacterInfo(character,out var glyph,size,FontStyle.Bold))
        {
            minX=Mathf.Min(minX,pen+glyph.minX);maxX=Mathf.Max(maxX,pen+glyph.maxX);
            minY=Mathf.Min(minY,glyph.minY);maxY=Mathf.Max(maxY,glyph.maxY);pen+=glyph.advance;
        }
        var content=titleContents[index]??(titleContents[index]=new GUIContent(text));
        heading.fontSize=size;heading.contentOffset=Vector2.zero;
        var line=heading.CalcSize(content);
        float ascent=font.ascent*size/(float)Mathf.Max(1,font.fontSize);
        float baselineBias=index==8?.05f:index==16||index==19?-.045f:0; // 兴建道路、外交官、权谋的字图基线微调。
        // IMGUI 已按实际排字宽度水平居中；宋体粗体字图的空白边再校正垂直基线。
        metrics=new TitleMetrics{Ink=Rect.MinMaxRect(minX,minY,maxX,maxY),Offset=new Vector2(0,line.y*.5f-ascent+(minY+maxY)*.5f+size*(baselineBias-.045f))};
        titleMetrics.Add(key,metrics);return metrics;
    }
    static void DrawTitle(Rect face,int index,string text)
    {
        var area=TitleArea(face,index);
        int size=Mathf.Clamp(Mathf.RoundToInt(face.width*.088f),5,128);
        var metrics=MeasureTitle(index,text,size);
        while(size>5&&(metrics.Ink.height>area.height*.78f||metrics.Ink.width>area.width*.90f))metrics=MeasureTitle(index,text,--size);
        heading.fontSize=size;heading.contentOffset=metrics.Offset;
        GUI.Label(area,titleContents[index],heading);
        heading.contentOffset=Vector2.zero;
    }
    public static string BasePath(int kind)=>Root+"base-"+baseIds[kind];
    public static void Register(Texture2D texture,int index){if(texture!=null)labels[texture]=index;}
    public static Texture2D Base(int kind)
    {
        if(kind<0||kind>=baseIds.Length)return null;
        var texture=EntityCardAssets.Texture(BasePath(kind));Register(texture,25+kind);return texture;
    }
    public static Texture2D Hand(OperationHandCard card)
    {
        if(card.Progress)return card.ArtIndex>=0?EntityCardAssets.ProgressFront(card.ArtIndex):null;
        int kind;return int.TryParse((card.Id??"").Split(':')[0],out kind)?Base(kind):null;
    }
    public static Texture2D Base(string name)=>Base(name=="修路"?1:Array.IndexOf(baseNames,name));
    public static void DrawText(Rect r,Texture2D texture,bool originalInk=false)
    {
        if(!labels.TryGetValue(texture,out int index))return;
        if(heading==null){var font=Resources.Load<Font>("Fonts/NotoSerifCJKsc-Regular");heading=new GUIStyle(GUI.skin.label){font=font,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,wordWrap=false,padding=new RectOffset(0,0,0,0)};body=new GUIStyle(heading){wordWrap=true};}
        var ink=new Color(.12f,.085f,.04f);
        heading.normal.textColor=body.normal.textColor=originalInk&&QualitySettings.activeColorSpace==ColorSpace.Linear?ink.linear:ink;
        string title=index<25?Catan.Core.DevelopmentCardText.Names[index]:baseNames[index-25];
        string description=index<25?Catan.Core.DevelopmentCardText.Descriptions[index]:baseDescriptions[index-25];
        DrawTitle(r,index,title);
        if(r.width<75)return;
        var area=new Rect(r.x+r.width*.105f,r.y+r.height*.675f,r.width*.79f,r.height*.205f);
        var content=contents[index]??(contents[index]=new GUIContent(description));
        if(fittedSizes[index]==0||Mathf.Abs(fittedAreas[index].x-area.width)>.5f||Mathf.Abs(fittedAreas[index].y-area.height)>.5f)
        {
            body.fontSize=Mathf.Max(5,Mathf.RoundToInt(r.width*.065f));
            while(body.fontSize>5&&body.CalcHeight(content,area.width)>area.height)body.fontSize--;
            fittedSizes[index]=body.fontSize;fittedAreas[index]=area.size;
        }
        body.fontSize=fittedSizes[index];GUI.Label(area,content,body);
    }
}
