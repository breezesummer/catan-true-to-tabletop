using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

// 只调整现有棋子的展示姿态；稳定位置、库存、合法性完全取自公开投影。
public sealed class ThreeKingdomsBoardPresentation
{
    sealed class Pose { public Transform Item; public Quaternion Rotation; public Vector3 Scale; }
    readonly List<Pose> changed = new List<Pose>();
    public void Restore()
    {
        foreach(var pose in changed)if(pose.Item!=null){pose.Item.localRotation=pose.Rotation;pose.Item.localScale=pose.Scale;}
        changed.Clear();
    }
    public void Apply(Transform pieces,B5BoardAsset[] assets)
    {
        if(pieces==null||assets==null)return;
        foreach(var asset in assets.Where(a=>a.Damaged))
            foreach(Transform item in pieces)
            {
                if(item.name!=asset.LocationId&&!item.name.EndsWith(" "+asset.LocationId,StringComparison.Ordinal))continue;
                changed.Add(new Pose{Item=item,Rotation=item.localRotation,Scale=item.localScale});
                if(asset.Kind=="Road"||asset.Kind=="Ship")item.localRotation*=Quaternion.Euler(0,90,0);
                else if(asset.Kind=="Settlement")item.localScale=Vector3.Scale(item.localScale,new Vector3(1,.35f,1));
            }
    }
    public static string Describe(B5BoardAsset asset) =>
        (asset.Kind=="Road"?"道路":asset.Kind=="Ship"?"船":asset.Kind=="City"?"城市":asset.Kind=="Settlement"?"村庄":asset.Kind=="Knight"?"骑士":"设施")+
        "（"+asset.LocationId+"）"+(asset.Damaged?(asset.Kind=="City"?"已降级，待修复":asset.Kind=="Settlement"?"倒塌，暂停产出与计分":"横置，暂停连接"):
        asset.Kind=="Knight"?"实际 "+asset.Level+" 级／有效 "+asset.EffectiveLevel+" 级"+(asset.Active?" · 激活":" · 未激活"):"");
}
