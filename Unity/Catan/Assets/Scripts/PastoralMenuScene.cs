using UnityEngine;

// 独立的二维菜单插画；展示时钟与权威规则随机状态完全分离。
public static class PastoralMenuScene
{
    static Texture2D artwork,shade;
    public static bool ArtworkReady => Artwork != null;
    static Texture2D Artwork => artwork != null ? artwork : artwork=Resources.Load<Texture2D>("PastoralMenu/v002/medieval-harbor");
    public static void Draw(float time)=>DrawBackground(new Rect(0,0,1440,900),time);
    public static void DrawBackground(Rect full,float time)
    {
        if(Event.current.type!=EventType.Repaint)return;
        if(Artwork!=null)
        {
            // 以物理视口计算裁切，宽屏和安全区也保持原画比例。
            float aspect=full.width*GUI.matrix.m00/(full.height*GUI.matrix.m11);
            float sourceAspect=(float)artwork.width/artwork.height;
            float w=Mathf.Min(1,aspect/sourceAspect),h=Mathf.Min(1,sourceAspect/aspect);
            float zoom=1.018f+Mathf.Sin(time*.035f)*.004f;w/=zoom;h/=zoom;
            GUI.DrawTextureWithTexCoords(full,artwork,new Rect((1-w)*.52f+Mathf.Sin(time*.025f)*(1-w)*.14f,(1-h)*.5f,w,h));
        }
        else PastoralMenuUI.Fill(full,new Color(.17f,.25f,.22f));
        if(shade==null)
        {
            shade=new Texture2D(256,1,TextureFormat.RGBA32,false){name="菜单阅读渐层 v002",wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            for(int x=0;x<256;x++){float t=x/255f;shade.SetPixel(x,0,new Color(.055f,.105f,.09f,Mathf.Lerp(.83f,0,Mathf.SmoothStep(0,1,t/.62f))));}
            shade.Apply(false,true);
        }
        GUI.DrawTexture(full,shade);
        // 极慢的光尘提供呼吸感，不移动交互区。
        for(int i=0;i<20;i++)
        {
            float x=Mathf.Repeat(i*97.7f+time*(1.6f+i%3),1440),y=240+Mathf.Repeat(i*67.3f-time*(1+i%2),650);
            float a=.08f+.13f*(.5f+.5f*Mathf.Sin(time*.7f+i));
            PastoralMenuUI.Round(new Rect(full.x+x/1440*full.width,full.y+y/900*full.height,2.2f,2.2f),new Color(1,.91f,.65f,a),2);
        }
    }
}
