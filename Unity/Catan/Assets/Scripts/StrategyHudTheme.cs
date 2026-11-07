using UnityEngine;

// 主题只保存在本地显示偏好中，不进入规则、网络和权威存档。
public static class StrategyHudTheme
{
    const string Preference="catan.ui.theme.v010";
    static int selected=-1;
    public static readonly string[] Names={"铜木","海洋","青玉鎏金","清新手绘"};
    // 保留既有偏好编号：0 铜木、1 海洋、3 青玉、4 清新手绘。旧骑士偏好回到默认海洋。
    public static int Selected
    {
        get
        {
            if(selected<0)
            {
                int saved=PlayerPrefs.GetInt(Preference,1);
                selected=saved==4?3:saved==3?2:saved==0?0:1;
            }
            return selected;
        }
    }
    public static int Version { get; private set; }
    public static void Select(int index)
    {
        index=Mathf.Clamp(index,0,Names.Length-1);if(index==Selected)return;
        selected=index;Version++;PlayerPrefs.SetInt(Preference,index>=2?index+1:index);PlayerPrefs.Save();
    }
    public static bool Ocean => Selected==1;
    public static Color PlayerInk(string player)
    {
        var color=BoardRenderer.SeatColor(player);
        return HandPainted?Color.Lerp(color,Text,.48f):color;
    }
    public static bool HandPainted => Selected==3;
    public static bool Jade => Selected==2;
    public static Color Background=>HandPainted?new Color(.94f,.87f,.72f):Jade?new Color(.035f,.17f,.135f,.98f):Ocean?new Color(.025f,.16f,.20f,.97f):new Color(.22f,.085f,.038f,.98f);
    public static Color Surface=>HandPainted?new Color(.82f,.80f,.63f):Jade?new Color(.12f,.34f,.27f):Ocean?new Color(.045f,.23f,.28f):new Color(.34f,.145f,.065f);
    public static Color Accent=>HandPainted?new Color(.14f,.39f,.29f):Jade?new Color(.94f,.80f,.46f):Ocean?new Color(.38f,.91f,.81f):new Color(.87f,.70f,.40f);
    public static Color Line=>HandPainted?new Color(.59f,.47f,.29f):Jade?new Color(.66f,.77f,.58f):Ocean?new Color(.72f,.61f,.36f):new Color(.64f,.35f,.18f);
    public static Color Text=>HandPainted?new Color(.19f,.15f,.10f):new Color(.97f,.94f,.85f);
    public static Color Muted=>HandPainted?new Color(.32f,.29f,.21f):Jade?new Color(.70f,.83f,.71f):Ocean?new Color(.85f,.90f,.87f):new Color(.86f,.73f,.55f);
}
