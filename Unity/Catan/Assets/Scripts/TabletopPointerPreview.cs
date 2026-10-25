#nullable disable
// 鼠标预览只描述公开棋子外观；不读取规则状态，也不参与指令裁决。
public sealed class TabletopPointerPreview
{
    public string Kind;
    public int Level=1;
    public bool Active;
    public static TabletopPointerPreview ForCommand(string command,bool setupCity=false,int knightLevel=1,bool knightActive=false)
    {
        string kind;
        switch(command)
        {
            case "SetupSettlement": kind=setupCity?"city":"settlement";break;
            case "BuildSettlement": kind="settlement";break;
            case "BuildCity": kind="city";break;
            case "BuildWall": kind="wall";break;
            case "SetupRoad": case "BuildRoad": case "PlaceFreeRoad": kind="road";break;
            case "SetupShip": case "BuildShip": case "PlaceFreeShip": case "MoveShip": kind="ship";break;
            case "MoveRobber": kind="robber";break;
            case "MovePirate": kind="pirate";break;
            case "RecruitKnight": case "PromoteKnight": case "ActivateKnight": case "MoveKnight": case "DisplaceKnight": case "ChaseRobber": case "ChasePirate": kind="knight";break;
            case "SetupPort": case "PlacePort": kind="port";break;
            default: kind="target";break;
        }
        return new TabletopPointerPreview{Kind=kind,Level=System.Math.Max(1,System.Math.Min(3,knightLevel)),Active=command=="ActivateKnight"||command!="RecruitKnight"&&knightActive};
    }
    public static TabletopPointerPreview ForEffect(string card,string decision=null,int knightLevel=1,bool knightActive=false)
    {
        string kind;
        switch(card??decision)
        {
            case "Engineering": kind="wall";break;
            case "Medicine": kind="city";break;
            case "Merchant": kind="merchant";break;
            case "Diplomacy": kind="road";break;
            case "Smithing": case "Intrigue": case "Treason": case "DisplacedKnight": case "ProgressTreasonRemove": case "ProgressTreasonPlace": kind="knight";break;
            case "Metropolis": case "B5MetropolisRelocate": kind="metropolis";break;
            // 税收、发明家及劫掠只选择已有目标，不应伪装成拿起强盗或新建村庄。
            default: kind="target";break;
        }
        return new TabletopPointerPreview{Kind=kind,Level=System.Math.Max(1,System.Math.Min(3,knightLevel)),Active=knightActive};
    }
}
