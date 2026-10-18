#nullable disable
// 收起只影响本地界面，保留选择草稿与权威待决。新指令/换座后重新展示。
public sealed class OperationWindowState
{
    string scope, revision;
    public bool Hidden { get; private set; }
    public string Caption { get; private set; }
    public void Describe(string caption){if(!Hidden)Caption=caption;}
    public void Sync(string nextScope,string nextRevision)
    {if(scope!=nextScope||revision!=nextRevision){Clear();scope=nextScope;revision=nextRevision;}}
    public void Hide(string caption){Caption=caption;Hidden=true;}
    public void Open(){Hidden=false;}
    public void Clear(){Hidden=false;Caption=null;}
}
