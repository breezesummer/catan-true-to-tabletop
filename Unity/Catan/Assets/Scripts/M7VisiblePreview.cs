using System;
using System.Linq;

public readonly struct M7Preview
{
    public readonly bool Success;
    public readonly string Message;
    public M7Preview(bool success,string message){Success=success;Message=message;}
}

// These are server-generated legal targets. No rule session is created to draw previews.
internal static class M7VisiblePreview
{
    internal static bool Try(object command,object view,out M7Preview result)
    {
        bool? legal=null;
        if(command is Catan.Core.M2.Command b&&view is Catan.Core.M2.PlayerView bv)
        {
            switch(b.Kind)
            {
                case Catan.Core.M2.CommandKind.SetupSettlement:case Catan.Core.M2.CommandKind.BuildSettlement:legal=bv.LegalVertexIds.Contains(b.TargetId);break;
                case Catan.Core.M2.CommandKind.BuildCity:legal=bv.LegalCityVertexIds.Contains(b.TargetId);break;
                case Catan.Core.M2.CommandKind.SetupRoad:case Catan.Core.M2.CommandKind.BuildRoad:case Catan.Core.M2.CommandKind.PlaceFreeRoad:legal=bv.LegalEdgeIds.Contains(b.TargetId);break;
            }
        }
        else if(command is Catan.Core.M3.Command s&&view is Catan.Core.M3.PlayerView sv)
        {
            switch(s.Kind)
            {
                case Catan.Core.M3.CommandKind.SetupSettlement:case Catan.Core.M3.CommandKind.BuildSettlement:legal=sv.LegalVertexIds.Contains(s.TargetId);break;
                case Catan.Core.M3.CommandKind.BuildCity:legal=sv.LegalCityVertexIds.Contains(s.TargetId);break;
                case Catan.Core.M3.CommandKind.SetupRoad:case Catan.Core.M3.CommandKind.BuildRoad:case Catan.Core.M3.CommandKind.PlaceFreeRoad:legal=sv.LegalEdgeIds.Contains(s.TargetId);break;
                case Catan.Core.M3.CommandKind.SetupShip:case Catan.Core.M3.CommandKind.BuildShip:case Catan.Core.M3.CommandKind.PlaceFreeShip:legal=sv.LegalShipEdgeIds.Contains(s.TargetId);break;
                case Catan.Core.M3.CommandKind.PlacePort:case Catan.Core.M3.CommandKind.SetupPort:legal=sv.LegalPortEdgeIds.Contains(s.TargetId);break;
                case Catan.Core.M3.CommandKind.ChooseInvasionRoute:legal=sv.LegalInvasionRootVertexIds.Contains(s.TargetId);break;
            }
        }
        else if(command is Catan.Core.M4.Command c&&view is Catan.Core.M4.PlayerView cv)
        {
            legal=BasicTarget(c.Kind.ToString(),c.TargetId,cv.LegalVertexIds,cv.LegalCityVertexIds,cv.LegalEdgeIds);
            if(!legal.HasValue&&ExpansionSpatial(c.Kind.ToString()))legal=cv.LegalActions.Any(a=>a.Kind==c.Kind&&a.TargetId==c.TargetId&&a.SourceId==c.SourceId);
        }
        else if(command is Catan.Core.M5.Command x&&view is Catan.Core.M5.PlayerView xv)
        {
            legal=BasicTarget(x.Kind.ToString(),x.TargetId,xv.LegalVertexIds,xv.LegalCityVertexIds,xv.LegalEdgeIds);
            if(x.Kind==Catan.Core.M5.CommandKind.SetupShip||x.Kind==Catan.Core.M5.CommandKind.BuildShip)legal=xv.LegalShipEdgeIds.Contains(x.TargetId);
            if(!legal.HasValue&&ExpansionSpatial(x.Kind.ToString()))legal=xv.LegalActions.Any(a=>a.Kind==x.Kind&&a.TargetId==x.TargetId&&a.SourceId==x.SourceId);
        }
        result=new M7Preview(legal==true,legal==true?"服务端已提供合法目标。":"该位置不在本次合法目标中。");
        return legal.HasValue;
    }
    static bool? BasicTarget(string kind,string target,string[] vertices,string[] cities,string[] edges)
    {
        switch(kind)
        {
            case "SetupSettlement":case "BuildSettlement":return vertices.Contains(target);
            case "BuildCity":return cities.Contains(target);
            case "SetupRoad":case "BuildRoad":return edges.Contains(target);
            default:return null;
        }
    }
    static bool ExpansionSpatial(string kind)
    {
        switch(kind)
        {
            case "BuildWall":case "RecruitKnight":case "PromoteKnight":case "ActivateKnight":case "MoveKnight":case "DisplaceKnight":case "ChaseRobber":return true;
            default:return false;
        }
    }
}
