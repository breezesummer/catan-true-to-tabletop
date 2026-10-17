using UnityEngine;

// 固定屏幕状态区，只消费 PlayerView 的公开兵力、航程与最长道路奖励，不参与结算。
public sealed class TabletopDefenseUI
{
    public static readonly Rect Area=new Rect(1182,207,246,108);
    int position=-1,strength,threat;
    string roadPlayer;
    int roadLength;
    bool roadBound;
    public string MobileSummary => (position>=0?"蛮族 "+position+"/7 · "+(position==7?"本次防御 ":"防御 ")+strength+"/"+threat+"\n":"")+(roadBound?"最长路线 · "+(string.IsNullOrEmpty(roadPlayer)?"暂无":M6App.PlayerLabel(roadPlayer)+" "+roadLength+" 段"):"");
    static Texture2D ship,shield;
    public void Bind(int position,int strength,int threat)
    {this.position=position;this.strength=strength;this.threat=threat;}
    public void BindLongestRoad(string playerId,int length)
    {roadBound=true;roadPlayer=playerId;roadLength=string.IsNullOrEmpty(playerId)?0:Mathf.Max(0,length);}
    Rect CurrentArea=>position>=0?Area:new Rect(Area.x,Area.y,Area.width,47);
    public bool BlocksInput(Vector2 point)=>(position>=0||roadBound)&&CurrentArea.Contains(point);
    public void Draw()
    {
        if(position<0&&!roadBound)return;
        ModernOperationUI.Styles();ModernOperationUI.Panel(CurrentArea);
        if(ship==null){ship=Icon(false);shield=Icon(true);}
        var ink=ModernOperationUI.TextColor;
        var title=new GUIStyle(ModernOperationUI.Small){fontSize=13,padding=new RectOffset(),alignment=TextAnchor.MiddleLeft,wordWrap=false};
        var count=new GUIStyle(title){alignment=TextAnchor.MiddleRight,fontStyle=FontStyle.Bold};
        title.normal.textColor=ink;count.normal.textColor=ModernOperationUI.Accent;
        if(position>=0)
        {
            DrawIcon(new Rect(Area.x+12,Area.y+9,25,25),ship,ModernOperationUI.Accent);
            GUI.Label(new Rect(Area.x+45,Area.y+6,125,27),"蛮族航程",title);
            GUI.Label(new Rect(Area.x+174,Area.y+6,57,27),position+" / 7",count);
            for(int i=0;i<7;i++)
                ModernOperationUI.Round(new Rect(Area.x+14+i*32,Area.y+36,27,4),i<position?ModernOperationUI.Accent:ModernOperationUI.Surface);
            var defenseColor=threat==0||strength>=threat?new Color(.47f,.78f,.67f):ModernOperationUI.Error;
            DrawIcon(new Rect(Area.x+15,Area.y+47,19,23),shield,defenseColor);
            GUI.Label(new Rect(Area.x+45,Area.y+46,145,25),position==7?"本次防御 / 蛮族":"防御 / 蛮族力量",new GUIStyle(title){fontSize=12});
            count.normal.textColor=defenseColor;
            GUI.Label(new Rect(Area.x+179,Area.y+46,52,25),strength+" / "+threat,count);
            ModernOperationUI.Fill(new Rect(Area.x+13,Area.y+76,Area.width-26,1),ModernOperationUI.Line);
        }
        if(roadBound)DrawLongestRoad(position>=0?Area.y+81:Area.y+10,title,count);
    }
    void DrawLongestRoad(float y,GUIStyle title,GUIStyle count)
    {
        var gold=StrategyHudArt.Gold;
        // 一条折线路径代表最长道路；公开奖励归属按 PlayerView 原样展示。
        var a=new Vector2(Area.x+14,y+18);var b=new Vector2(Area.x+21,y+8);var c=new Vector2(Area.x+30,y+17);var d=new Vector2(Area.x+37,y+6);
        StrategyHudArt.Line(a,b,gold,2);StrategyHudArt.Line(b,c,gold,2);StrategyHudArt.Line(c,d,gold,2);
        GUI.Label(new Rect(Area.x+45,y-2,66,25),"最长道路",title);
        var result=new Rect(Area.x+109,y-2,122,25);
        count.normal.textColor=string.IsNullOrEmpty(roadPlayer)?ModernOperationUI.Muted:gold;
        string resultText=string.IsNullOrEmpty(roadPlayer)?"暂无归属":M6App.PlayerLabel(roadPlayer)+" · "+roadLength+" 段";
        var resultStyle=new GUIStyle(count){fontSize=10,wordWrap=true};GUI.Label(result,resultText,resultStyle);
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
