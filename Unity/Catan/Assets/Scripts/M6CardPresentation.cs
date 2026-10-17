using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;

public sealed partial class M6App
{
    readonly Queue<GameHistoryEntry> playedCards=new Queue<GameHistoryEntry>();
    long presentedSequence;
    double cardStarted;
    Action afterPlayedCards;
    Action pendingNetworkPresentation;
    long pendingNetworkSequence;
    const double CardRevealDuration=2.15;
    internal bool CardPresentationBusy=>playedCards.Count>0;
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
        playedCards.Clear();afterPlayedCards=null;pendingNetworkPresentation=null;presentedSequence=history?.LastOrDefault()?.Sequence??0;
    }
    // 指令已在规则层完成；显示旧快照至卡牌退场，再统一刷新并衔接现有飞牌／棋盘效果。
    internal bool PresentPlayedCard(GameHistoryEntry[] history,Action applyView)
    {
        foreach(var entry in (history??Array.Empty<GameHistoryEntry>()).Where(e=>e.Sequence>presentedSequence).OrderBy(e=>e.Sequence))
        {
            presentedSequence=entry.Sequence;
            if(entry.Kind=="PlayKnight"||entry.Kind=="PlayRoadBuilding"||entry.Kind=="PlayYearOfPlenty"||entry.Kind=="PlayMonopoly"||entry.Kind=="PlayProgressCard")
            {
                if(playedCards.Count==0)cardStarted=Time.unscaledTimeAsDouble;
                playedCards.Enqueue(entry);
            }
        }
        if(!CardPresentationBusy)return false;
        afterPlayedCards=applyView;return true;
    }
    internal bool WaitForPresentationHistory(long sequence,Action applyView)
    {
        if(sequence<=presentedSequence||(Network.History.LastOrDefault()?.Sequence??0)>=sequence)return false;
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
        var apply=afterPlayedCards;afterPlayedCards=null;apply?.Invoke();
        nextAction=Time.unscaledTime+(float)TabletopAiSpeed.Interval(AiSpeedLevel);
    }
    void DrawPlayedCard()
    {
        if(!CardPresentationBusy)return;
        var entry=playedCards.Peek();
        string card=entry.Action.Replace("打出「","").TrimEnd('」');
        float elapsed=(float)(Time.unscaledTimeAsDouble-cardStarted);
        float enter=Mathf.SmoothStep(0,1,elapsed/.48f),leave=Mathf.SmoothStep(0,1,(elapsed-1.63f)/.52f);
        float x=Mathf.Lerp(-260,610,enter)+leave*1100;
        var face=new Rect(x,260,220,326);
        Panel(new Rect(0,0,1440,900),new Color(0,0,0,.30f));
        Texture2D texture=null;
        if(entry.Kind=="PlayProgressCard")
        {
            string[] names={"炼金术","起重机","工程学","发明","灌溉","医学","采矿","印刷术","道路建设","锻造","商业港口","行会征费","商人","商船队","资源垄断","商品垄断","外交","间谍","鼓舞","谋略","征税","宪法","叛变","婚礼","破坏"};
            int index=Array.IndexOf(names,card);if(index>=0)texture=EntityCardAssets.ProgressFront(index);
        }
        if(texture!=null)EntityCardAssets.DrawCardFace(face,texture);
        else
        {
            ModernOperationUI.Panel(face);
            GUI.Label(new Rect(x+16,380,188,80),card,new GUIStyle(title){alignment=TextAnchor.MiddleCenter,fontSize=32});
            GUI.Label(new Rect(x+16,290,188,40),"发展卡",new GUIStyle(label){alignment=TextAnchor.MiddleCenter});
        }
        StrategyHudArt.Avatar(new Rect(x+75,175,70,70),entry.PlayerId);
        GUI.Label(new Rect(x-240,600,700,70),PlayerDisplayName(entry.PlayerId)+" 使用了「"+card+"」",new GUIStyle(title){alignment=TextAnchor.MiddleCenter,fontSize=29});
        if(Event.current.isMouse)Event.current.Use();
    }
}
