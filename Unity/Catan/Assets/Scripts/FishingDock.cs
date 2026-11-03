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
        public int CardIndex;
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
    static readonly Rect SkillColumn=new Rect(254,714,78,171);
    Rect Actions=>new Rect(342,650,932,58);
    readonly Catan.Core.ResourceBag resourceSelection=new Catan.Core.ResourceBag();
    FishingView view;
    string owner, revision;
    Option[] options=Array.Empty<Option>();
    Option chosen;
    FishAction? action;
    bool open, boot;
    string[] tokens=Array.Empty<string>();
    string hint;
    readonly List<Rect> cardHits=new List<Rect>();
    public bool Active=>Selected&&(open||chosen!=null||action!=null||boot);
    public bool Contains(Vector2 p)=>view!=null&&Selected&&(SkillColumn.Contains(p)||(cardHits.Any(r=>r.Contains(p))||Actions.Contains(p)||action==FishAction.TakeResource&&TabletopResourceChoiceUI.Area.Contains(p)||action==FishAction.BuyDevelopmentCard&&new Rect(350,460,700,180).Contains(p)));
    public void Reset(){open=false;boot=false;action=null;chosen=null;tokens=Array.Empty<string>();hint=null;}
    public void Bind(FishingView fish,string player,Option[] legal)
    {
        var key=player+":"+fish?.StateSequence+":"+fish?.PendingDecisionId;
        if(key!=revision){Reset();cardHits.Clear();revision=key;}
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
    static string Caption(FishAction a,bool cities)=>a==FishAction.RemoveRobber?"驱盗·2":a==FishAction.StealResource?"偷取·3":a==FishAction.TakeResource?"资源·4":a==FishAction.BuildRoad?"道路·5":"发展·7";
    public void Draw(Font font,bool interactive,bool cities,string robber,Func<string,Vector2> position,string pirate=null)
    {
        cardHits.Clear();
        if(view==null||!Selected)return;
        var style=new GUIStyle(GUI.skin.label){font=font,fontSize=15,alignment=TextAnchor.MiddleCenter};style.normal.textColor=ModernOperationUI.TextColor;
        bool previous=GUI.enabled;GUI.enabled=previous&&interactive;
        bool replacement=view.PendingPlayerId==owner;
        int skillIndex=0;
        foreach(FishAction effect in Enum.GetValues(typeof(FishAction)))
        {
            var hit=new Rect(SkillColumn.x,SkillColumn.y+skillIndex++*28,SkillColumn.width,27);
            if(GeneralFrameUI.Choice(hit,Caption(effect,cities),!replacement&&options.Any(o=>o.Action==effect),action==effect))
            {
                Show();action=effect;boot=false;chosen=null;
                foreach(Catan.Core.Resource r in Enum.GetValues(typeof(Catan.Core.Resource)))resourceSelection[r]=0;
                if(effect==FishAction.BuyDevelopmentCard&&!cities)Choose(options.First(o=>o.Action==effect));
            }
        }
        if(GeneralFrameUI.Choice(new Rect(SkillColumn.x,SkillColumn.y+140,SkillColumn.width,27),"旧靴",options.Any(o=>o.Kind=="boot"),boot))
        {Show();boot=true;action=null;chosen=null;}
        if(!Selected){GUI.enabled=previous;return;}
        const float shift=102;
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
            cardHits.Add(new Rect(r.x-2,r.y-2,r.width+4,r.height+4));
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

        if(replacement)
        {
            GUI.Label(new Rect(354,655,485,34),chosen==null?"点击一张鱼牌换抽，或放弃": "弃掉所选鱼牌，随机换抽一张",style);
            if(ModernOperationUI.Button(new Rect(984,661,100,36),"换抽",chosen!=null)){var submit=chosen.Submit;Reset();submit(Array.Empty<string>());}
            if(ModernOperationUI.Button(new Rect(1108,661,100,36),"放弃")){var submit=options.First(o=>o.Kind=="replace"&&o.Target==null).Submit;Reset();submit(Array.Empty<string>());}
        }
        else
        {
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
                if(GeneralFrameUI.Choice(new Rect(984,661,112,36),"确认",paid>=cost)){var submit=chosen.Submit;var selected=tokens;Reset();submit(selected);}
            }
            else if(action==FishAction.TakeResource)
            {
                var bank=new Catan.Core.ResourceBag();
                foreach(var option in options.Where(o=>o.Action==action))bank[(Catan.Core.Resource)option.CardIndex]=1;
                TabletopResourceChoiceUI.Draw(1,resourceSelection,bank,()=>{
                    var option=options.First(o=>o.Action==FishAction.TakeResource&&resourceSelection[(Catan.Core.Resource)o.CardIndex]>0);
                    Choose(option);var submit=chosen.Submit;var payment=tokens;Reset();submit(payment);
                },null,"选择资源",cancel:Reset);
            }
            else if(action==FishAction.BuyDevelopmentCard)
            {
                var targets=options.Where(o=>o.Action==action).ToArray();
                for(int i=0;i<targets.Length;i++)
                {
                    var face=new Rect(470+i*140,470,88,130);
                    EntityCardAssets.DrawCardFace(face,EntityCardAssets.ProgressBack(targets[i].CardIndex));
                    if(ThreeKingdomsDirectInput.Read(face,interactive)==1)Choose(targets[i]);
                }
            }
            else if(chosen==null&&action.HasValue)
                GUI.Label(new Rect(354,655,500,40),action==FishAction.BuildRoad?"点击道路位置":action==FishAction.StealResource?"点击玩家头像":"点击强盗或海盗",style);

            if((chosen!=null||action.HasValue||boot)&&GeneralFrameUI.Choice(new Rect(1108,661,100,36),"取消"))Reset();
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
