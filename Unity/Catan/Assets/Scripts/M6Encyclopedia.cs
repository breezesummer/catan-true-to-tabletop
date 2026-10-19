using UnityEngine;

public sealed partial class M6App
{
    bool encyclopediaOpen;
    Vector2 encyclopediaScroll;
    int encyclopediaCategory;
    void DrawEncyclopedia()
    {
        DrawTablePauseBackdrop();
        TablePauseHeading(new Rect(180,50,1080,800),"翻 开 岛 屿 的 故 事","游戏百科 · 发展卡","随时查阅卡牌效果，再从容回到牌桌。" );
        for(int i=0;i<2;i++)if(PastoralMenuUI.Button(new Rect(212+i*513,250,501,40),i==0?"基础版 / 航海家":"城市与骑士 / 组合",true,false,encyclopediaCategory==i))
        {encyclopediaCategory=i;encyclopediaScroll=Vector2.zero;}
        string[] names=encyclopediaCategory==0?new[]{"骑士","道路建设","丰收","垄断","胜利点"}:M5App.Cards;
        string[] help=encyclopediaCategory==0?new[]{"移动强盗并向相邻的一名对手随机拿取一张资源；航海家可选择移动海盗。","免费建设两段道路；航海家可选择道路或船，逐段选择合法位置。","从银行领取两张资源，可选同一种；受银行剩余资源限制。","选择一种资源，所有对手将该种资源全部交给你。","持有即计 1 分；在自己回合达到胜利条件时公开。"}:M5App.CardHelp;
        var viewport=new Rect(212,310,1014,370);float contentHeight=names.Length*100;
        encyclopediaScroll=GUI.BeginScrollView(viewport,encyclopediaScroll,new Rect(0,0,988,contentHeight));
        for(int i=0;i<names.Length;i++)
        {
            PastoralMenuUI.Label(new Rect(12,i*100+8,954,28),names[i],21,null,true,TextAnchor.MiddleCenter);
            PastoralMenuUI.Label(new Rect(24,i*100+43,930,49),help[i],16,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
            PastoralMenuUI.Fill(new Rect(24,i*100+98,930,1),new Color(.48f,.54f,.42f,.15f));
        }
        GUI.EndScrollView();
        PastoralMenuUI.ScrollIndicator(viewport,contentHeight,encyclopediaScroll.y);
        PastoralMenuUI.Label(new Rect(230,700,980,50),encyclopediaCategory==0?"基础发展卡：新买的卡本回合不能使用；每回合最多使用一张（胜利点卡除外）。":"发展卡：通常在自己回合行动阶段使用；炼金术在掷骰前使用。只有超过四张时才可弃置。",13,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
        if(PastoralMenuUI.Button(new Rect(526,775,388,48),"返回游戏菜单",true,true))encyclopediaOpen=false;
    }
}
