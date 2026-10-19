#nullable disable
using System;
// 收起保留回合按钮入口；关闭只隐藏本地窗口，权威待决仍由规则入口完成。
public sealed class OperationWindowState
{
    string scope, revision;
    Action reopen;
    public bool Hidden { get; private set; }
    public bool Closed { get; private set; }
    public bool Suppressed => Hidden || Closed;
    public string Caption { get; private set; }
    public void Describe(string caption){if(!Suppressed)Caption=caption;}
    public void Sync(string nextScope,string nextRevision)
    {if(scope!=nextScope)Clear();else if(revision!=nextRevision)ResetInteraction();scope=nextScope;revision=nextRevision;}
    public void Hide(string caption){Hide(caption,null);}
    public void Hide(string caption,Action restore){Caption=caption;Hidden=true;Closed=false;reopen=restore;}
    public void Close(){Caption=null;Hidden=false;Closed=true;reopen=null;}
    // 新开窗口不恢复之前收起的另一个窗口；只有回合按钮显式恢复。
    public void Open(){Hidden=false;Closed=false;reopen=null;}
    public void Resume(){var restore=reopen;Open();restore?.Invoke();}
    public void ResetInteraction(){if(!Hidden||reopen==null)Clear();}
    public void Clear(){Hidden=false;Closed=false;Caption=null;reopen=null;}
}
