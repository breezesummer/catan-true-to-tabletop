using System;

internal static class M7RoomEntry
{
    internal static bool IsJoined(string roomId,string serverAddress,string serverPin,string currentRoomId,string endpoint,string fingerprint,bool hasSeat)
    {
        if(!hasSeat||string.IsNullOrEmpty(roomId)||roomId!=currentRoomId)return false;
        // 局域网地址可有本机／网卡别名；证书身份相同才是同一台服务。
        if(!string.IsNullOrEmpty(serverPin))return string.Equals(serverPin,fingerprint,StringComparison.OrdinalIgnoreCase);
        return Uri.TryCreate(serverAddress,UriKind.Absolute,out var server)&&Uri.TryCreate(endpoint,UriKind.Absolute,out var current)&&server.Authority==current.Authority;
    }
    internal static bool NeedsPassword(bool hasPassword,bool remembered,bool started)=>hasPassword&&!(remembered&&started);
}
