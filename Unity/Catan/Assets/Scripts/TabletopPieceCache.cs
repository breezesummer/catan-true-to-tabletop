using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 只缓存公开棋子对应的显示对象；保持根节点名称和层级，供拾取、移动与高亮共用。
public sealed class TabletopPieceCache
{
    sealed class Entry
    {
        public string Appearance;
        public GameObject[] Roots;
    }
    readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
    readonly HashSet<string> touched=new HashSet<string>();
    Transform parent;
    string prefix;

    public void Begin(string group,Transform root)
    {
        parent=root;prefix=group+"/";touched.Clear();
    }
    public Change Update(string key,string appearance)
    {
        key=prefix+key;touched.Add(key);
        if(entries.TryGetValue(key,out var old)&&old.Appearance==appearance&&old.Roots.All(r=>r!=null))
            return new Change(null,null,null,0);
        Remove(key);
        return new Change(this,key,appearance,parent.childCount);
    }
    public void End()
    {
        foreach(var key in entries.Keys.Where(k=>k.StartsWith(prefix,StringComparison.Ordinal)&&!touched.Contains(k)).ToArray())Remove(key);
    }
    void Remove(string key)
    {
        if(!entries.TryGetValue(key,out var old))return;
        foreach(var root in old.Roots)if(root!=null)
        {
            root.SetActive(false);root.transform.SetParent(null,true);UnityEngine.Object.Destroy(root);
        }
        entries.Remove(key);
    }
    public sealed class Change : IDisposable
    {
        readonly TabletopPieceCache owner;
        readonly string key,appearance;
        readonly int first;
        public bool Changed=>owner!=null;
        internal Change(TabletopPieceCache owner,string key,string appearance,int first)
        {this.owner=owner;this.key=key;this.appearance=appearance;this.first=first;}
        public void Dispose()
        {
            if(owner==null)return;
            var roots=new List<GameObject>();
            for(int i=first;i<owner.parent.childCount;i++)roots.Add(owner.parent.GetChild(i).gameObject);
            owner.entries[key]=new Entry{Appearance=appearance,Roots=roots.ToArray()};
        }
    }
}
