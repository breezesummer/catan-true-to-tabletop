using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;

public sealed partial class M6App
{
    readonly Queue<TabletopCardReveal> playedCards=new Queue<TabletopCardReveal>();
    readonly TabletopCardRevealTimeline cardReveals=new TabletopCardRevealTimeline();
    double cardStarted;
    Action afterPlayedCards;
    Action pendingNetworkPresentation;
    long pendingNetworkSequence;
    const double CardRevealDuration=2.15;
    internal GameHistoryEntry CardPresentationRoll { get; private set; }
    internal bool CardPresentationBusy=>playedCards.Count>0;
    internal TabletopCardReveal PresentedCard=>playedCards.Count>0?playedCards.Peek():null;
    public bool TableInspectionBlocked=>tableMenu;
    public bool TablePresentationBlocked=>tableMenu||CardPresentationBusy||pendingNetworkPresentation!=null||buildFlights.Any(f=>f.Piece.Previous!=null);
    public string PlayerDisplayName(string seat)
    {
        if(seat=="*")return "所有玩家";
        var named=NetworkGame?Network.Room?.Seats.FirstOrDefault(s=>s.SeatId==seat):null;
        if(named!=null&&!string.IsNullOrWhiteSpace(named.DisplayName))return named.DisplayName;
        if(seat==HumanSeat)return LocalPlayerName;
        int index;
        return seat!=null&&seat.Length==2&&int.TryParse(seat.Substring(1),out index)&&index>=1&&index<=localPlayerNames.Length?localPlayerNames[index-1]:ColorLabel(seat);
    }
    void ResetCardPresentation(GameHistoryEntry[] history=null)
    {
        playedCards.Clear();afterPlayedCards=null;pendingNetworkPresentation=null;CardPresentationRoll=null;cardReveals.Reset(history);
    }
    // 指令已在规则层完成；显示旧快照至卡牌退场，再统一刷新并衔接现有飞牌／棋盘效果。
    internal bool PresentPlayedCard(GameHistoryEntry[] history,Action applyView)
    {
        foreach(var reveal in cardReveals.Observe(history))
        {
            if(playedCards.Count==0)cardStarted=Time.unscaledTimeAsDouble+(reveal.Roll!=null?TabletopDicePresentation.Duration:0);
            if(reveal.Roll!=null)CardPresentationRoll=reveal.Roll;
            playedCards.Enqueue(reveal);
        }
        if(!CardPresentationBusy)return false;
        afterPlayedCards=applyView;return true;
    }
    internal bool WaitForPresentationHistory(long sequence,Action applyView)
    {
        if(sequence<=cardReveals.Sequence||(Network.History.LastOrDefault()?.Sequence??0)>=sequence)return false;
        pendingNetworkSequence=sequence;pendingNetworkPresentation=applyView;return true;
    }
    void UpdateCardPresentation()
    {
        if(pendingNetworkPresentation!=null&&(Network.History.LastOrDefault()?.Sequence??0)>=pendingNetworkSequence)
        {var networkApply=pendingNetworkPresentation;pendingNetworkPresentation=null;networkApply();}
        if(!CardPresentationBusy)return;
        if(MenuVisible||tableMenu){cardStarted+=Time.unscaledDeltaTime;return;}
        if(Time.unscaledTimeAsDouble-cardStarted<CardRevealDuration)return;
        playedCards.Dequeue();cardStarted=Time.unscaledTimeAsDouble;
        if(CardPresentationBusy)return;
        var apply=afterPlayedCards;afterPlayedCards=null;apply?.Invoke();CardPresentationRoll=null;
        nextAction=Time.unscaledTime+(float)TabletopAiSpeed.Interval(AiSpeedLevel);
    }
    void DrawPlayedCard()
    {
        if(!CardPresentationBusy)return;
        var entry=playedCards.Peek();
        string card=entry.CardName;
        float elapsed=(float)(Time.unscaledTimeAsDouble-cardStarted);
        if(elapsed<0)return;
        float enter=Mathf.SmoothStep(0,1,elapsed/.48f),leave=Mathf.SmoothStep(0,1,(elapsed-1.63f)/.52f);
        float x=Mathf.Lerp(-260,610,enter)+leave*1100;
        var face=new Rect(x,260,220,326);
        if(entry.ImmediateVictory)
        {
            int place=Array.IndexOf(HistoryPlayers,entry.PlayerId);
            var destination=TabletopUI.PlayerAvatarRect(Math.Max(0,place),PlayerCount);
            face=new Rect(Mathf.Lerp(Mathf.Lerp(-260,610,enter),destination.center.x-22,leave),Mathf.Lerp(260,destination.yMax+3,leave),Mathf.Lerp(220,44,leave),Mathf.Lerp(326,65,leave));
        }
        Panel(new Rect(0,0,1440,900),new Color(0,0,0,.30f));
        Texture2D texture=null;
        if(entry.CardName=="卡坦保卫者")texture=EntityCardAssets.Texture(EntityCardAssets.Defender.Front);
        if(entry.Progress&&entry.ProgressIndex>=0)texture=EntityCardAssets.ProgressFront(entry.ProgressIndex);
        if(texture!=null)EntityCardAssets.DrawCardFace(face,texture);
        else
        {
            ModernOperationUI.Panel(face);
            GUI.Label(new Rect(x+16,380,188,80),card,new GUIStyle(title){alignment=TextAnchor.MiddleCenter,fontSize=32});
            GUI.Label(new Rect(x+16,290,188,40),"发展卡",new GUIStyle(label){alignment=TextAnchor.MiddleCenter});
        }
        StrategyHudArt.Avatar(new Rect(x+75,175,70,70),entry.PlayerId);
        var nameStyle=PlayerNameStyle(new GUIStyle(title){alignment=TextAnchor.MiddleCenter,fontSize=29},entry.PlayerId);
        GUI.Label(new Rect(370,600,700,42),PlayerDisplayName(entry.PlayerId),nameStyle);
        if(!entry.ImmediateVictory)GUI.Label(new Rect(370,646,700,70),"使用了「"+card+"」",new GUIStyle(title){alignment=TextAnchor.MiddleCenter,fontSize=27});

    }
}
