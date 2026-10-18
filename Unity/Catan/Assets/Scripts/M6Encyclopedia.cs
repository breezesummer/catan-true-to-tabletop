using UnityEngine;

public sealed partial class M6App
{
    bool encyclopediaOpen;
    Vector2 encyclopediaScroll;
    int encyclopediaCategory;
    void DrawEncyclopedia()
    {
        ModernOperationUI.Styles(font);
        Panel(new Rect(0,0,1440,900),new Color(0,0,0,.64f));
        ModernOperationUI.Panel(new Rect(180,80,1080,740));
        ModernOperationUI.Label(new Rect(212,100,760,44),"游戏百科 · 发展卡",ModernOperationUI.Title);
        if(ModernOperationUI.Button(new Rect(1060,104,166,38),"返回 · Esc"))encyclopediaOpen=false;
        for(int i=0;i<2;i++)if(ModernOperationUI.Button(new Rect(212+i*252,158,240,38),i==0?"基础版 / 航海家":"城市与骑士 / 组合",true,false,encyclopediaCategory==i))
        {encyclopediaCategory=i;encyclopediaScroll=Vector2.zero;}
        string[] names=encyclopediaCategory==0?new[]{"骑士","道路建设","丰收","垄断","胜利点"}:M5App.Cards;
        string[] help=encyclopediaCategory==0?new[]{"移动强盗并向相邻的一名对手随机拿取一张资源；航海家可选择移动海盗。","免费建设两段道路；航海家可选择道路或船，逐段选择合法位置。","从银行领取两张资源，可选同一种；受银行剩余资源限制。","选择一种资源，所有对手将该种资源全部交给你。","持有即计 1 分；在自己回合达到胜利条件时公开。"}:M5App.CardHelp;
        encyclopediaScroll=GUI.BeginScrollView(new Rect(212,212,1014,536),encyclopediaScroll,new Rect(0,0,988,names.Length*100));
        for(int i=0;i<names.Length;i++)
        {
            ModernOperationUI.Label(new Rect(12,i*100+8,960,28),names[i],ModernOperationUI.Title);
            ModernOperationUI.Label(new Rect(12,i*100+40,950,54),help[i],ModernOperationUI.Text);
        }
        GUI.EndScrollView();
        ModernOperationUI.Label(new Rect(212,758,1014,42),encyclopediaCategory==0?"基础发展卡：新买的卡本回合不能使用；每回合最多使用一张（胜利点卡除外）。":"发展卡：通常在自己回合行动阶段使用；炼金术在掷骰前使用。只有超过四张时才可弃置。",ModernOperationUI.Small);
    }
}
