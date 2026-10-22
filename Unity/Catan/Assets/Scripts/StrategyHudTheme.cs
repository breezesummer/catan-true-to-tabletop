using UnityEngine;

// 主题只保存在本地显示偏好中，不进入规则、网络和权威存档。
public static class StrategyHudTheme
{
    const string Preference="catan.ui.theme.v010";
    static int selected=-1;
    public static readonly string[] Names={"原版","海洋","骑士"};
    public static int Selected { get { if(selected<0)selected=Mathf.Clamp(PlayerPrefs.GetInt(Preference,0),0,2);return selected; } }
    public static int Version { get; private set; }
    public static void Select(int index)
    {
        index=Mathf.Clamp(index,0,2);if(index==Selected)return;
        selected=index;Version++;PlayerPrefs.SetInt(Preference,index);PlayerPrefs.Save();
    }
    static Color Pick(Color original,Color ocean,Color knight)=>Selected==0?original:Selected==1?ocean:knight;
    public static Color Background=>Pick(new Color(.105f,.067f,.045f,.97f),new Color(.025f,.09f,.15f,.97f),new Color(.14f,.105f,.026f,.97f));
    public static Color Surface=>Pick(new Color(.24f,.155f,.105f),new Color(.055f,.205f,.30f),new Color(.32f,.24f,.065f));
    public static Color Accent=>Pick(new Color(.87f,.70f,.40f),new Color(.40f,.81f,.95f),new Color(1f,.81f,.29f));
    public static Color Line=>Pick(new Color(.46f,.32f,.18f),new Color(.18f,.44f,.57f),new Color(.57f,.43f,.12f));
    public static Color Text=>new Color(.97f,.94f,.85f);
    public static Color Muted=>Pick(new Color(.73f,.64f,.51f),new Color(.62f,.77f,.82f),new Color(.79f,.72f,.52f));
}
