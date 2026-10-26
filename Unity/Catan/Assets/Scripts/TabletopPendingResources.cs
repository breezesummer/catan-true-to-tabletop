using System;
using System.Linq;
using Catan.Core;

// 引水渠与金矿共用五资源面板；确认时转回各规则已有的指令格式。
public sealed partial class M4App
{
    ResourceBag resourceChoiceSelection=new ResourceBag();
    bool ResourceChoiceVisible=>!tabletop.OperationHidden&&view?.PendingDecision?.PlayerId==seat&&view.PendingDecision.Kind=="Aqueduct";
    ResourceBag PendingResourceBankB6()
    {var bank=view.Bank.Copy();foreach(int type in view.ThreeKingdoms?.BlockedGainCardTypes??Array.Empty<int>())if(type>=0&&type<5)bank[(Resource)type]=0;return bank;}
    void DrawPendingResources()
    {
        TabletopResourceChoiceUI.Draw(1,resourceChoiceSelection,PendingResourceBankB6(),ConfirmPendingResources,ai.ShowTablePrompt,"引水渠 · 选择资源",tabletop.OperationWindows);
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
    bool ResourceChoiceVisible=>!tabletop.OperationHidden&&view?.PendingDecision?.PlayerId==seat&&(view.PendingDecision.Kind=="Gold"||view.PendingDecision.Kind=="Aqueduct");
    ResourceBag PendingResourceBankB6()
    {var bank=view.Bank.Copy();foreach(int type in view.ThreeKingdoms?.BlockedGainCardTypes??Array.Empty<int>())if(type>=0&&type<5)bank[(Resource)type]=0;return bank;}
    void DrawPendingResources()
    {
        bool gold=view.PendingDecision.Kind=="Gold";var bank=PendingResourceBankB6();
        TabletopResourceChoiceUI.Draw(gold?Math.Min(view.PendingDecision.Amount,bank.Total):1,resourceChoiceSelection,bank,ConfirmPendingResources,ai.ShowTablePrompt,gold?"金矿 · 选择资源":"引水渠 · 选择资源",tabletop.OperationWindows);
    }
    void ConfirmPendingResources()
    {
        var command=New(Catan.Core.M5.CommandKind.ResolveChoice);
        if(view.PendingDecision.Kind=="Gold")command.Resources=resourceChoiceSelection.Copy();
        else command.Resource=Enum.GetValues(typeof(Resource)).Cast<Resource>().First(r=>resourceChoiceSelection[r]>0);
        Submit(command);
    }
}
