using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core.TradeModules;
using UnityEngine;

// 轻量桌面组件。只保留提交前草稿；确认后的筹码、随机与待决全部来自授权投影。
public sealed class FishingDock
{
    public sealed class Option
    {
        public string Kind;
        public bool Ship, Pirate;
        public FishAction? Action;
        public string Target, Player, Label;
        public Action<string[]> Submit;
    }
    static readonly Rect Strip=OperationDock.HandViewport;
    public bool Selected {get;private set;}
    public bool Available=>view!=null;
    public int Count=>view?.OwnTokens.Length??0;
    public int Total=>view?.OwnTokens.Sum(t=>t.Value)??0;
    bool sorted;
    readonly Texture2D[] cardArt=new Texture2D[3];
    public void Show(){Selected=true;}
    public void Hide(){Selected=false;Reset();}
    public void Sort(){sorted=true;}
    bool SelectingMap=>chosen==null&&(action==FishAction.BuildRoad||action==FishAction.RemoveRobber);
    Rect Actions=>new Rect(350,SelectingMap?103:594,732,chosen!=null||action==FishAction.TakeResource||action==FishAction.BuyDevelopmentCard||view?.PendingPlayerId==owner?88:48);
    FishingView view;
    string owner, revision;
    Option[] options=Array.Empty<Option>();
    Option chosen;
    FishAction? action;
    bool open, boot;
    string[] tokens=Array.Empty<string>();
    string hint;
    public bool Active=>Selected&&(open||chosen!=null||action!=null||boot);
    public bool Contains(Vector2 p)=>view!=null&&Selected&&(Strip.Contains(p)||Actions.Contains(p));
    public void Reset(){open=false;boot=false;action=null;chosen=null;tokens=Array.Empty<string>();hint=null;}
    public void Bind(FishingView fish,string player,Option[] legal)
    {
        var key=player+":"+fish?.StateSequence+":"+fish?.PendingDecisionId;
        if(key!=revision){Reset();revision=key;}
        if(owner!=player||fish==null)Hide();
        view=fish;owner=player;options=legal??Array.Empty<Option>();
        if(view!=null&&view.PendingPlayerId==owner){open=true;Show();}
    }
    public bool PickPlayer(string player)
    {
        if(!Active||(!boot&&action!=FishAction.StealResource))return false;
        var option=options.FirstOrDefault(o=>o.Player==player&&(boot?o.Kind=="boot":o.Action==action));
        if(option!=null)Choose(option);return true;
    }
    void Choose(Option option)
    {
        chosen=option;tokens=option.Action.HasValue?FishingPayment.Select(view.OwnTokens,option.Action.Value):Array.Empty<string>();
    }
    static string Caption(FishAction a,bool cities)=>a==FishAction.RemoveRobber?"2鱼 · 移出强盗":a==FishAction.StealResource?"3鱼 · 偷取手牌":a==FishAction.TakeResource?"4鱼 · 任意资源":a==FishAction.BuildRoad?"5鱼 · 新建道路":"7鱼 · 发展卡";
    public void Draw(Font font,bool interactive,bool cities,string robber,Func<string,Vector2> position,string pirate=null)
    {
        if(view==null||!Selected)return;
        var style=new GUIStyle(GUI.skin.label){font=font,fontSize=15,alignment=TextAnchor.MiddleCenter};style.normal.textColor=ModernOperationUI.TextColor;
        bool previous=GUI.enabled;GUI.enabled=previous&&interactive;
        bool replacement=view.PendingPlayerId==owner;
        float shift=SelectingMap?-403:88;
        // 鱼牌与资源牌共用尺寸、基线和叠放间距；只改变本地支付草稿。
        for(int value=1;value<=3;value++)if(cardArt[value-1]==null)cardArt[value-1]=Resources.Load<Texture2D>("HarborFishing/Cards/fish-card-"+value);
        var hand=sorted?view.OwnTokens.OrderBy(t=>t.Value).ThenBy(t=>t.Id).ToArray():view.OwnTokens;
        float step=TabletopHandLayout.Step(hand.Length,Strip.width);
        var number=new GUIStyle(style){fontSize=25,fontStyle=FontStyle.Bold};
        number.normal.textColor=new Color(.19f,.26f,.25f);

        for(int i=0;i<hand.Length;i++)
        {
            var token=hand[i];bool selected=tokens.Contains(token.Id)||replacement&&chosen?.Target==token.Id;
            var r=new Rect(Strip.x+i*step,TabletopHandLayout.Baseline-(selected?22:0),TabletopHandLayout.CardWidth,TabletopHandLayout.CardHeight);
            ModernOperationUI.Round(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),selected?new Color(.38f,.83f,.81f):new Color(.45f,.35f,.20f));
            var art=cardArt[Mathf.Clamp(token.Value,1,3)-1];
            if(art!=null)GUI.DrawTexture(r,art,ScaleMode.StretchToFill);
            GUI.Label(new Rect(r.x+7,r.y+5,32,30),token.Value.ToString(),number);
            var hit=r;if(i<hand.Length-1)hit.width=step;
            int input=ThreeKingdomsDirectInput.Read(hit,interactive&&GUI.enabled);
            if(input==2)hint=token.Value+"条鱼；整张支付，多付不找零。";
            if(input==1)
            {
                if(replacement){var option=options.FirstOrDefault(o=>o.Kind=="replace"&&o.Target==token.Id);if(option!=null)Choose(option);}
                else if(chosen?.Action!=null)tokens=tokens.Contains(token.Id)?tokens.Where(t=>t!=token.Id).ToArray():tokens.Concat(new[]{token.Id}).ToArray();
            }
        }
        if(hand.Length==0)GUI.Label(new Rect(Strip.x,740,Strip.width,40),"尚未收获鱼",style);
        ModernOperationUI.Panel(Actions);
        if(replacement)
        {
            GUI.Label(new Rect(365,510+shift,485,34),chosen==null?"点击一张鱼牌换抽，或放弃": "弃掉所选鱼牌，随机换抽一张",style);
            if(ModernOperationUI.Button(new Rect(864,513+shift,96,34),"换抽",chosen!=null)){var submit=chosen.Submit;Reset();submit(Array.Empty<string>());}
            if(ModernOperationUI.Button(new Rect(968,513+shift,96,34),"放弃")){var submit=options.First(o=>o.Kind=="replace"&&o.Target==null).Submit;Reset();submit(Array.Empty<string>());}
        }
        else
        {
            int x=361;
            foreach(FishAction a in Enum.GetValues(typeof(FishAction)))
            {
                if(ModernOperationUI.Button(new Rect(x,512+shift,126,33),(a==FishAction.BuildRoad&&options.Any(o=>o.Ship)?"5鱼 · 道路／船":a==FishAction.RemoveRobber&&options.Any(o=>o.Pirate)?"2鱼 · 移出棋子":Caption(a,cities)),options.Any(o=>o.Action==a),false,action==a))
                {
                    action=a;boot=false;chosen=null;
                    if(a==FishAction.BuyDevelopmentCard&&!cities)Choose(options.First(o=>o.Action==a));
                }
                x+=130;
            }
            if(ModernOperationUI.Button(new Rect(1014,512+shift,60,33),"旧靴",options.Any(o=>o.Kind=="boot"),false,boot)){boot=true;action=null;chosen=null;}
            if(chosen!=null)
            {
                int paid=view.OwnTokens.Where(t=>tokens.Contains(t.Id)).Sum(t=>t.Value),cost=chosen.Action.HasValue?FishingRules.Cost(chosen.Action.Value):0;
                if(chosen.Action==FishAction.BuildRoad&&options.Any(o=>o.Action==action&&o.Target==chosen.Target&&o.Ship!=chosen.Ship))
                {
                    var target=chosen.Target;
                    if(ModernOperationUI.Button(new Rect(365,551+shift,100,32),"新建道路",true,false,!chosen.Ship))Choose(options.First(o=>o.Action==action&&o.Target==target&&!o.Ship));
                    if(ModernOperationUI.Button(new Rect(473,551+shift,100,32),"新建船只",true,false,chosen.Ship))Choose(options.First(o=>o.Action==action&&o.Target==target&&o.Ship));
                }
                else GUI.Label(new Rect(365,551+shift,485,32),chosen.Action.HasValue?chosen.Label+" · 支付 "+paid+" / "+cost+"鱼":chosen.Label,style);
                if(ModernOperationUI.Button(new Rect(863,551+shift,96,32),"确认",paid>=cost)){var submit=chosen.Submit;var selected=tokens;Reset();submit(selected);}
            }
            else if(action==FishAction.TakeResource||action==FishAction.BuyDevelopmentCard)
            {
                var targets=options.Where(o=>o.Action==action).ToArray();for(int i=0;i<targets.Length;i++)if(ModernOperationUI.Button(new Rect(365+i*98,551+shift,92,32),targets[i].Label))Choose(targets[i]);
            }

            if(chosen!=null&&ModernOperationUI.Button(new Rect(968,551+shift,96,32),"取消"))Reset();
            if(chosen==null&&action==FishAction.RemoveRobber)
                foreach(var o in options.Where(o=>o.Action==action)) {var id=o.Pirate?pirate:robber;if(id==null||id=="frame")continue;var p=position(id);if(ModernOperationUI.Button(new Rect(p.x-32,p.y-26,64,52),o.Pirate?"海盗":"强盗"))Choose(o);}
        }
        if(hint!=null)GUI.Label(new Rect(350,Actions.y-32,390,28),hint,style);
        GUI.enabled=previous;
    }
    public void UpdateMap(Vector2 mouse,bool click,Catan.Core.Edge[] edges,Catan.Core.Tile[] tiles,Func<string,Vector2> position,Action<string,bool> preview,Action clear)
    {
        if(!Selected||action!=FishAction.BuildRoad||chosen!=null){clear();return;}
        var roads=options.Where(o=>o.Action==FishAction.BuildRoad&&!o.Ship).Select(o=>o.Target).ToArray();
        var ships=options.Where(o=>o.Action==FishAction.BuildRoad&&o.Ship).Select(o=>o.Target).ToArray();
        TabletopRoadPlacement.Update(mouse,click,edges,tiles,roads,ships,position,
            (id,legal)=>preview(id,false),(id,legal)=>preview(id,true),clear,
            (id,ship)=>{Choose(options.First(o=>o.Action==FishAction.BuildRoad&&o.Target==id&&o.Ship==ship));clear();},null);
    }
    public void DrawMap(Font font,Func<string,Vector2> position)
    {
        if(view==null||!Selected)return;
        var style=new GUIStyle(GUI.skin.label){font=font,fontSize=13,alignment=TextAnchor.MiddleCenter};style.normal.textColor=new Color(.5f,.92f,1f);
        // 地图实体由公开视图刷新；这里只绘制有限鱼池与公开弃置顶面。
        FishingTabletopAssets.DrawIcon(new Rect(752,573,28,28),"fish-back");
        GUI.Label(new Rect(782,576,65,24),view.SupplyCount+"",style);
        if(view.DiscardValues.Length>0)FishingTabletopAssets.DrawIcon(new Rect(865,573,28,28),"fish-"+view.DiscardValues[view.DiscardValues.Length-1]);
        GUI.Label(new Rect(895,576,120,24),"弃置 "+view.DiscardValues.Length,style);
    }
}
