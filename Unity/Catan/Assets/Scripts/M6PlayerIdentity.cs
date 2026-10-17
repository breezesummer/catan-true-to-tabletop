using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class M6App
{
    const string PlayerNamePreference="catan.player-name.v1";
    static M6App identityOwner;
    string[] localPlayerNames=Array.Empty<string>(),playerActionOrder=Array.Empty<string>();
    double openingOrderUntil;
    public string LocalPlayerName
    {
        get=>PlayerIdentityModel.NormalizeName(PlayerPrefs.GetString(PlayerNamePreference,"玩家"));
        set{string name=PlayerIdentityModel.NormalizeName(value);PlayerPrefs.SetString(PlayerNamePreference,name);PlayerPrefs.Save();Network?.SetDisplayName(name);}
    }
    public static string ColorLabel(string id)=>PlayerIdentityModel.ColorLabel(id);
    public string PlayerName(string id)=>PlayerDisplayName(id);
    public static string PlayerLabel(string id)=>identityOwner!=null?identityOwner.PlayerDisplayName(id):ColorLabel(id);
    public static string DisplayPlayerText(string value)=>PlayerIdentityModel.ReplaceIds(value,PlayerLabel);
    public static GUIStyle PlayerNameStyle(GUIStyle basis,string id)
    {
        var style=new GUIStyle(basis){richText=false};
        if(PlayerIdentityModel.IsPlayerId(id))
        {
            var color=BoardRenderer.SeatColor(id.ToUpperInvariant());
            style.normal.textColor=style.hover.textColor=style.active.textColor=style.focused.textColor=color;
        }
        return style;
    }
    internal static string[] OrderedPlayers(IEnumerable<string> ids)
    {
        var available=(ids??Array.Empty<string>()).ToArray();
        return identityOwner!=null&&identityOwner.playerActionOrder.Length==available.Length&&available.All(identityOwner.playerActionOrder.Contains)
            ?identityOwner.playerActionOrder.Where(available.Contains).ToArray():available;
    }
    void ResetPlayerIdentity()
    {
        identityOwner=this;playerActionOrder=Array.Empty<string>();openingOrderUntil=0;
        localPlayerNames=PlayerIdentityModel.CreateNames(PlayerCount,HumanSeat,LocalPlayerName,Guid.NewGuid().GetHashCode());
    }
    internal void ObservePlayerOrder(string startingPlayer,IEnumerable<string> ids)
    {
        if(playerActionOrder.Length>0||string.IsNullOrEmpty(startingPlayer))return;
        identityOwner=this;playerActionOrder=PlayerIdentityModel.ActionOrder(startingPlayer,ids);
        openingOrderUntil=Time.unscaledTimeAsDouble+8;
    }
    internal string OpeningOrderMessage
    {
        get
        {
            int place=Array.IndexOf(playerActionOrder,HumanSeat)+1;
            return place>0&&Time.unscaledTimeAsDouble<openingOrderUntil?"你是第"+place+"个行动的玩家":"";
        }
    }
    void DrawOpeningOrder()
    {
        string message=OpeningOrderMessage;if(message.Length==0||tableMenu||CardPresentationBusy)return;
        var style=new GUIStyle(title){fontSize=30,alignment=TextAnchor.MiddleCenter,wordWrap=false};
        style.normal.textColor=new Color(.95f,.055f,.035f);
        GUI.Label(new Rect(365,224,710,48),message,style);
    }
}
