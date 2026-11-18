using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class M6App
{
    readonly Dictionary<string,string> bookMapIds=new Dictionary<string,string>();
    static readonly string[] BookGoods={"木材","砖块","羊毛","粮食","矿石","布","钱币","纸"};
    bool IsBookMap=>bookEntry=="标准岛屿"||bookMapIds.ContainsKey(bookEntry);
    bool IsBookMode=>bookEntry=="基础版"||Array.IndexOf(BookModes,bookEntry)>=0;
    const string Scenes="Encyclopedia/规则示意/", Covers="Encyclopedia/玩法主图/", Pieces="OperationUI/v002/pieces/", Menu="AcceptedMenu/";
    static readonly Dictionary<string,(string image,string text)> BookRules=new Dictionary<string,(string,string)>{
        {"道路",(Menu+"ency-detail-road","在合法陆地边上延伸自己的路线。相邻路段在顶点连接，对手的建筑会阻断连接。")},
        {"船",("AcceptedMenu/mode-sea","花费一份木材和一份羊毛建船。在自己的沿海聚落或城市连接道路与船。")},
        {"聚落",(Pieces+"settlement-red","在合法交点建设聚落。相邻地块的点数掷出时，聚落可获得对应产出。")},
        {"城市",(Pieces+"city-red","将自己的聚落升级为城市，提高相邻地块产出。")},
        {"城墙",(Pieces+"wall-red","在自己的城市修建城墙，增加掷出七点时的安全手牌上限。")},
        {"骑士激活",(Scenes+"骑士激活","支付粮食激活骑士。激活的骑士可参与抵御蛮族。")},
        {"蛮族进攻",(Scenes+"蛮族进攻","蛮族船抵达时，比较蛮族实力与全岛激活骑士的总战力。")},
        {"武将隐退与接任",(Scenes+"武将隐退与接任","武将隐退后，按对局给出的候选武将完成接任。后续行动使用新武将的技能。")},
        {"港口计分",(Scenes+"港口聚落与城市","港口聚落计一个港口点，港口城市计两个港口点。港口点用于争夺海港大师。")},
        {"渔场产鱼",(Scenes+"渔场产鱼","沿海建筑相邻渔场的点数掷出时，从鱼堆领取鱼。")},
        {"湖泊产鱼",(Scenes+"湖泊产鱼","湖泊对应的点数掷出时，沿湖建筑可以领取鱼。")},
        {"支付鱼",(Scenes+"支付鱼","选择捕鱼行动，并用持有的鱼支付所需数量。")},
        {"海盗",(Scenes+"海盗封锁","海盗所在海格的边不能新建或移走船。")},
        {"手牌",(Menu+"ency-detail-hand","手牌由玩家持有，卡面记载用途。资源和商品可以用于建设、交易和城市改良。")},
        {"摸牌阶段",(Menu+"ency-detail-draw","按本回合的产出确定可领取的牌，在对应时机结算武将技能。")},
        {"商人",("FantasyPieces/Views/merchant","移动商人到自己建筑相邻的地块，使用该地块对应的交易优惠。")},
        {"大都会",(Scenes+"大都会","在城市改良中争夺对应领域的大都会，获得额外胜利点。")},
        {"旧靴",("HarborFishing/v002/old-boot","捕鱼时可能摸到旧靴。持有旧靴的玩家需要额外一个胜利点才能获胜。")},
        {"强盗",("FantasyPieces/Views/robber","强盗占据的地块暂停生产。移动强盗后，可从符合条件的相邻对手处随机获得一张牌。")},
        {"纸",("", "城市生产的商品，可用于对应领域的城市改良或交易。")},
        {"钱币",("", "城市生产的商品，可用于对应领域的城市改良或交易。")},
        {"布",("", "城市生产的商品，可用于对应领域的城市改良或交易。")},
        {"矿石",("", "对应地形生产的基础资源，可用于建设或交易。")},
        {"粮食",("", "对应地形生产的基础资源，可用于建设或交易。")},
        {"羊毛",("", "对应地形生产的基础资源，可用于建设或交易。")},
        {"砖块",("", "对应地形生产的基础资源，可用于建设或交易。")},
        {"木材",("", "对应地形生产的基础资源，可用于建设或交易。")},
    };
    static readonly Dictionary<string,string[]> ModeTopics=new Dictionary<string,string[]>{
        {"基础版",new[]{"聚落","道路","城市"}},{"城市与骑士",new[]{"骑士激活","蛮族进攻","城墙"}},
        {"三国争霸",new[]{"武将隐退与接任","摸牌阶段","手牌"}},{"海港与捕鱼",new[]{"港口计分","渔场产鱼","湖泊产鱼","支付鱼"}}
    };
    void DrawAtlasEntry(){
        bool map=IsBookMap,mode=IsBookMode;
        var entry=bookIndex.Find(item=>item.name==bookEntry);
        string image;
        if(map)image=bookMapIds.TryGetValue(bookEntry,out var id)?Menu+"atlas-"+id+"-"+PlayerCount:"MenuPreviews/v001/base-standard-"+PlayerCount;
        else image=mode?Covers+bookEntry:BookRules[bookEntry].image;
        DetailPage(new Rect(76,130,860,622));
        var artArea=new Rect(95,150,822,581);
        int good=Array.IndexOf(BookGoods,bookEntry);
        if(good>=0)BookTexture(artArea,EntityCardAssets.ResourceOrCommodityFront(good),true);
        else DetailArt(artArea,image);
        DetailPage(new Rect(950,130,644,622));
        DetailTab(new Rect(995,150,553,85),bookEntry,40);
        var area=new Rect(990,255,565,mode?128:165);
        var style=new GUIStyle(BookStyle(29)){alignment=TextAnchor.MiddleLeft};
        string description=entry.body;
        if(map&&bookEntry==CurrentBookMap)description+="\n本局胜利目标："+TargetVictoryPoints+" 分。";
        TabletopTextFit.Fit(style,description,area,20,true);GUI.Label(area,description,style);
        if(mode){
            int row=0;
            foreach(string topic in ModeTopics[bookEntry]){
                var box=new Rect(982,385+row++*84,580,77);
                DetailPage(box);DetailArt(new Rect(box.x+14,box.y+5,128,64),BookRules[topic].image);
                BookHeading(new Rect(box.x+150,box.y,349,75),topic,28);
                BookHeading(new Rect(box.xMax-52,box.y,40,75),"›",35);
                if(Hit(box)){bookTrail.Push(bookEntry);bookEntry=topic;}
            }
        }else if(map){
            DetailArt(new Rect(988,438,563,277),bookEntry=="标准岛屿"?Covers+"基础版":"Encyclopedia/地图示意/"+bookEntry);
            RelatedHeading();Related(0,528,bookEntry=="标准岛屿"?"基础版":"航海家",bookEntry=="标准岛屿"?"mode-base":"mode-sea");
        }else{
            string parent=good>=5||bookEntry=="大都会"||bookEntry=="商人"?"城市与骑士":bookEntry=="旧靴"?"海港与捕鱼":"基础版";
            foreach(var group in ModeTopics)if(Array.IndexOf(group.Value,bookEntry)>=0){parent=group.Key;break;}
            if(bookEntry=="海盗"||bookEntry=="船")parent="航海家";
            RelatedHeading();Related(0,528,parent,parent=="航海家"?Menu+"mode-sea":Covers+parent,false);
        }
    }
}
