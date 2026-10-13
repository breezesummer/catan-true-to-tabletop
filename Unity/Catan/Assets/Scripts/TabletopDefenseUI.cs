using UnityEngine;

// 固定屏幕状态区，只消费 PlayerView 的公开兵力与航程，不参与结算。
public sealed class TabletopDefenseUI
{
    public static readonly Rect Area=new Rect(18,730,220,104);
    int position=-1,strength,threat;
    static Texture2D ship,shield;
    public void Bind(int position,int strength,int threat)
    {this.position=position;this.strength=strength;this.threat=threat;}
    public bool BlocksInput(Vector2 point)=>position>=0&&Area.Contains(point);
    public void Draw()
    {
        if(position<0)return;
        ModernOperationUI.Styles();ModernOperationUI.Panel(Area);
        if(ship==null){ship=Icon(false);shield=Icon(true);}
        var ink=ModernOperationUI.TextColor;
        var title=new GUIStyle(ModernOperationUI.Small){fontSize=13,padding=new RectOffset(),alignment=TextAnchor.MiddleLeft,wordWrap=false};
        var count=new GUIStyle(title){alignment=TextAnchor.MiddleRight,fontStyle=FontStyle.Bold};
        title.normal.textColor=ink;count.normal.textColor=ModernOperationUI.Accent;
        DrawIcon(new Rect(Area.x+12,Area.y+9,27,27),ship,ModernOperationUI.Accent);
        GUI.Label(new Rect(Area.x+47,Area.y+7,105,29),"蛮族航程",title);
        GUI.Label(new Rect(Area.x+154,Area.y+7,52,29),position+" / 7",count);
        for(int i=0;i<7;i++)
            ModernOperationUI.Round(new Rect(Area.x+14+i*28,Area.y+40,24,5),i<position?ModernOperationUI.Accent:ModernOperationUI.Surface);
        var defenseColor=threat==0||strength>=threat?new Color(.47f,.78f,.67f):ModernOperationUI.Error;
        DrawIcon(new Rect(Area.x+13,Area.y+58,25,29),shield,defenseColor);
        GUI.Label(new Rect(Area.x+47,Area.y+54,130,26),"防御  /  蛮族力量",title);
        count.normal.textColor=defenseColor;
        GUI.Label(new Rect(Area.x+47,Area.y+76,159,23),strength+"  /  "+threat,count);
    }
    static void DrawIcon(Rect rect,Texture2D texture,Color color)
    {var previous=GUI.color;GUI.color=color;GUI.DrawTexture(rect,texture,ScaleMode.ScaleToFit);GUI.color=previous;}
    static Texture2D Icon(bool defense)
    {
        const int size=64;var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float u=(x+.5f)/size,v=(y+.5f)/size;
            bool filled;
            if(defense)
            {
                float half=v<.40f?.40f*(v-.06f)/.34f:.40f;
                filled=v>.06f&&v<.91f&&Mathf.Abs(u-.5f)<half;
                if(v>.40f&&v<.79f&&Mathf.Abs(u-.5f)<.055f)filled=false;
                if(v>.54f&&v<.65f&&Mathf.Abs(u-.5f)<.23f)filled=false;
            }
            else
            {
                filled=v>.13f&&v<.29f&&u>.14f-(v-.13f)*.5f&&u<.86f+(v-.13f)*.5f;
                filled|=u>.46f&&u<.51f&&v>.28f&&v<.94f;
                filled|=u>.53f&&v>.34f&&v<.86f&&(u-.53f)/.37f+(v-.34f)/.52f<1;
                filled|=u<.43f&&u>.13f&&v>.34f&&v<.80f&&(.43f-u)/.30f+(v-.34f)/.46f<1;
            }
            pixels[y*size+x]=filled?Color.white:Color.clear;
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name=defense?"防御盾牌图标 v008":"蛮族帆船图标 v008",filterMode=FilterMode.Bilinear};
        texture.SetPixels(pixels);texture.Apply(false,true);return texture;
    }
}
