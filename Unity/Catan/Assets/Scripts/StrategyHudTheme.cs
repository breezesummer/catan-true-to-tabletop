using UnityEngine;

// 主题只保存在本地显示偏好中，不进入规则、网络和权威存档。
public static class StrategyHudTheme
{
    const string Preference="catan.ui.theme.v010";
    static int selected=-1;
    public static readonly string[] Names={"铜木","海洋","骑士","青玉鎏金"};
    public static int Selected { get { if(selected<0)selected=Mathf.Clamp(PlayerPrefs.GetInt(Preference,0),0,Names.Length-1);return selected; } }
    public static int Version { get; private set; }
    public static void Select(int index)
    {
        index=Mathf.Clamp(index,0,Names.Length-1);if(index==Selected)return;
        selected=index;Version++;PlayerPrefs.SetInt(Preference,index);PlayerPrefs.Save();
    }
    public static bool Jade => Selected==3;
    static Color Pick(Color original,Color ocean,Color knight)=>Selected==0?original:Selected==1?ocean:knight;
    public static Color Background=>Jade?new Color(.035f,.17f,.135f,.98f):Pick(new Color(.22f,.085f,.038f,.98f),new Color(.025f,.09f,.15f,.97f),new Color(.14f,.105f,.026f,.97f));
    public static Color Surface=>Jade?new Color(.12f,.34f,.27f):Pick(new Color(.34f,.145f,.065f),new Color(.055f,.205f,.30f),new Color(.32f,.24f,.065f));
    public static Color Accent=>Jade?new Color(.94f,.80f,.46f):Pick(new Color(.87f,.70f,.40f),new Color(.40f,.81f,.95f),new Color(1f,.81f,.29f));
    public static Color Line=>Jade?new Color(.66f,.77f,.58f):Pick(new Color(.64f,.35f,.18f),new Color(.18f,.44f,.57f),new Color(.57f,.43f,.12f));
    public static Color Text=>new Color(.97f,.94f,.85f);
    public static Color Muted=>Jade?new Color(.70f,.83f,.71f):Pick(new Color(.86f,.73f,.55f),new Color(.62f,.77f,.82f),new Color(.79f,.72f,.52f));
}
