using System;
using System.Linq;
using Catan.Core;

// 引水渠与金矿共用五资源面板；确认时转回各规则已有的指令格式。
public sealed partial class M4App
{
    ResourceBag resourceChoiceSelection=new ResourceBag();
    bool ResourceChoiceVisible=>view?.PendingDecision?.Kind=="Aqueduct";
    void DrawPendingResources()
    {
        TabletopResourceChoiceUI.Draw(1,resourceChoiceSelection,view.Bank,ConfirmPendingResources,ai.ShowTablePrompt,"引水渠 · 选择资源");
    }
    void ConfirmPendingResources()
    {
        var command=New(Catan.Core.M4.CommandKind.ResolveChoice);
        command.Resource=Enum.GetValues(typeof(Resource)).Cast<Resource>().First(r=>resourceChoiceSelection[r]>0);
        Submit(command);
    }
}
public sealed partial class M5App
{
    ResourceBag resourceChoiceSelection=new ResourceBag();
    bool ResourceChoiceVisible=>view?.PendingDecision?.Kind=="Gold"||view?.PendingDecision?.Kind=="Aqueduct";
    void DrawPendingResources()
    {
        bool gold=view.PendingDecision.Kind=="Gold";
        TabletopResourceChoiceUI.Draw(gold?view.PendingDecision.Amount:1,resourceChoiceSelection,view.Bank,ConfirmPendingResources,ai.ShowTablePrompt,gold?"金矿 · 选择资源":"引水渠 · 选择资源");
    }
    void ConfirmPendingResources()
    {
        var command=New(Catan.Core.M5.CommandKind.ResolveChoice);
        if(view.PendingDecision.Kind=="Gold")command.Resources=resourceChoiceSelection.Copy();
        else command.Resource=Enum.GetValues(typeof(Resource)).Cast<Resource>().First(r=>resourceChoiceSelection[r]>0);
        Submit(command);
    }
}
