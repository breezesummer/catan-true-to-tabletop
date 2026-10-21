using System;
using UnityEngine;

// 仅是客户端偏好；不得序列化进权威存档或房间配置。
public sealed class BoardThemeDefinition
{
    public readonly string Id, Name, AssetVersion, Description;
    public readonly Color TableColor, LightColor;
    public readonly float LightIntensity;
    BoardThemeDefinition(string id,string name,string version,string description,Color table,Color light,float intensity)
    {Id=id;Name=name;AssetVersion=version;Description=description;TableColor=table;LightColor=light;LightIntensity=intensity;}
    public static readonly BoardThemeDefinition Tabletop=new BoardThemeDefinition("tabletop","真实桌游","v005","实体组件与原照牌面",Color.white,new Color(1,.95f,.87f),1.22f);
    public static readonly BoardThemeDefinition Ultra=new BoardThemeDefinition("ultra","豪华典藏版ultra","u2-pasture-v002","U2 草原与玉牌样板 · 其他地形及棋子仍为占位",new Color(.30f,.23f,.15f),new Color(1,.89f,.71f),1.35f);
    public static readonly BoardThemeDefinition[] All={Tabletop,Ultra};
    public static BoardThemeDefinition Find(string id)=>Array.Find(All,t=>t.Id==id);
}

public static class BoardThemeController
{
    // 自动验收用独立键，避免两个测试进程改写用户正在游玩的偏好。
    public static readonly string PreferenceKey=PreferenceName();
    static string PreferenceName()
    {
        var args=Environment.GetCommandLineArgs();
        if(Array.IndexOf(args,"--ultra-pasture-verify")>=0)return "catan.board-theme.verification.u2";
        if(Array.IndexOf(args,"--ultra-theme-verify")<0)return "catan.board-theme.v1";
        int role=Array.IndexOf(args,"--ultra-theme-role");
        return "catan.board-theme.verification.v1."+(role>=0&&role+1<args.Length?args[role+1]:"local");
    }
    static BoardThemeDefinition current;
    public static BoardThemeDefinition Current=>current??(current=BoardThemeDefinition.Find(PlayerPrefs.GetString(PreferenceKey,"tabletop"))??BoardThemeDefinition.Tabletop);
    public static bool IsUltra=>Current==BoardThemeDefinition.Ultra;
    public static bool Switching {get;internal set;}
    public static string Status {get;internal set;}="";
    internal static void Select(BoardThemeDefinition theme)=>current=theme??throw new ArgumentNullException(nameof(theme));
    internal static void Persist(){PlayerPrefs.SetString(PreferenceKey,Current.Id);PlayerPrefs.Save();}
}
