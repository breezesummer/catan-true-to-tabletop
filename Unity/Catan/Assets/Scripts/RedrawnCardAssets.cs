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
    public static void DrawText(Rect r,Texture2D texture)
    {
        if(!labels.TryGetValue(texture,out int index))return;
        if(heading==null){var font=MobileTabletop.LoadFont();heading=new GUIStyle(GUI.skin.label){font=font,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,wordWrap=false};body=new GUIStyle(heading){fontStyle=FontStyle.Normal,wordWrap=true};heading.normal.textColor=body.normal.textColor=new Color(.12f,.085f,.04f);}
        string title=index<25?Catan.Core.DevelopmentCardText.Names[index]:baseNames[index-25];
        string description=index<25?Catan.Core.DevelopmentCardText.Descriptions[index]:baseDescriptions[index-25];
        heading.fontSize=Mathf.Max(5,Mathf.RoundToInt(r.width*.105f));
        float titleWidth=r.width*.65f;
        GUI.Label(new Rect(r.center.x-titleWidth*.5f,r.y+r.height*.045f,titleWidth,r.height*.105f),title,heading);
        if(r.width<75)return;
        var area=new Rect(r.x+r.width*.09f,r.y+r.height*.67f,r.width*.82f,r.height*.25f);
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
