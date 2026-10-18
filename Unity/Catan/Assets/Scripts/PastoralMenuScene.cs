using UnityEngine;

// 原版港湾与五幅风景原画；展示时间独立于规则、存档和权威随机状态。
public static class PastoralMenuScene
{
    public const int ThemeCount=6;
    public const int ClassicHarbor=5;
    public static readonly string[] Names={"晨光港湾","金色麦田","林间溪谷","雪山城堡","月夜灯港","经典港湾"};
    public static readonly string[] Descriptions={"波光与海鸟，陪你启航","风过麦田，丰收正当时","溪流与落叶，山林慢时光","轻雪落城，灯火待归人","月光入港，星灯映海面","海风入港，麦浪归田"};
    static readonly string[] Paths={"dawn-harbor","golden-fields","forest-stream","snow-castle","moonlit-port"};
    static readonly Texture2D[] Artworks=new Texture2D[ThemeCount];
    static Texture2D shade;
    static Material landscape;
    static RenderTexture animatedFrame;
    static int selected=-1,lastFrame=-1,lastTheme=-1,audioTheme=-1;
    static bool? animated;
    // 本轮首次打开恢复经典原画，后续继续记住所选风景；旧五景编号不变。
    public static int Selected { get { if(selected<0)selected=Mathf.Clamp(PlayerPrefs.GetInt("Catan.Menu.Theme.Classic.v001",ClassicHarbor),0,ThemeCount-1);return selected; } }
    public static bool Animated
    {
        get { if(!animated.HasValue)animated=PlayerPrefs.GetInt("Catan.Menu.Animated.v003",1)!=0;return animated.Value; }
        set { animated=value;PlayerPrefs.SetInt("Catan.Menu.Animated.v003",value?1:0);PlayerPrefs.Save(); }
    }
    public static bool ArtworkReady=>Preview(Selected)!=null;
    public static void Select(int index)
    {
        selected=Mathf.Clamp(index,0,ThemeCount-1);
        PlayerPrefs.SetInt("Catan.Menu.Theme.Classic.v001",selected);PlayerPrefs.Save();SyncAudio();
    }
    public static Texture2D Preview(int index)
    {
        index=Mathf.Clamp(index,0,ThemeCount-1);
        // 直接复用保留的原图，不重绘或覆盖源文件。
        if(Artworks[index]==null)Artworks[index]=Resources.Load<Texture2D>(index==ClassicHarbor?"PastoralMenu/v002/medieval-harbor":"PastoralMenu/v003/"+Paths[index]);
        return Artworks[index];
    }
    static void SyncAudio()
    {
        if(TabletopAudio.Instance!=null&&audioTheme!=Selected)
        {TabletopAudio.Instance.SetMenuTheme(Selected==ClassicHarbor?0:Selected);audioTheme=Selected;}
    }
    public static void Draw(float time)=>DrawBackground(new Rect(0,0,1440,900),time);
    public static void DrawBackground(Rect full,float time)
    {
        SyncAudio();if(Event.current.type!=EventType.Repaint)return;
        var artwork=Preview(Selected);
        if(artwork!=null)
        {
            float aspect=full.width*Mathf.Abs(GUI.matrix.m00)/(full.height*Mathf.Abs(GUI.matrix.m11));
            float sourceAspect=(float)artwork.width/artwork.height;
            float w=Mathf.Min(1,aspect/sourceAspect),h=Mathf.Min(1,sourceAspect/aspect);
            GUI.DrawTextureWithTexCoords(full,Animated?Animate(artwork,time):artwork,new Rect((1-w)*.52f,(1-h)*.5f,w,h));
            if(Animated)DrawAtmosphere(full,time,Selected);
        }
        else PastoralMenuUI.Fill(full,new Color(.17f,.25f,.22f));
        if(shade==null)
        {
            shade=new Texture2D(256,1,TextureFormat.RGBA32,false){name="菜单阅读渐层 v003",wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            for(int x=0;x<256;x++){float t=x/255f;shade.SetPixel(x,0,new Color(.035f,.075f,.065f,Mathf.Lerp(.84f,0,Mathf.SmoothStep(0,1,t/.57f))));}
            shade.Apply(false,true);
        }
        GUI.DrawTexture(full,shade);
    }
    static Texture Animate(Texture2D artwork,float time)
    {
        if(landscape==null)
        {
            var shader=Resources.Load<Shader>("PastoralMenu/v003/LivingLandscape");
            if(shader==null||!shader.isSupported)return artwork;
            landscape=new Material(shader){name="动态风景 v003",hideFlags=HideFlags.HideAndDontSave};
        }
        // 只有一张循环使用的离屏纹理。主题切换不新建渲染目标。
        if(animatedFrame==null)
        {
            int width=Application.isMobilePlatform?1024:1600;
            animatedFrame=new RenderTexture(width,Mathf.RoundToInt(width*.625f),0,RenderTextureFormat.ARGB32)
            {name="动态主页帧 v003",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            animatedFrame.Create();
        }
        if(lastFrame!=Time.frameCount||lastTheme!=Selected)
        {
            landscape.SetFloat("_Clock",time);landscape.SetFloat("_Theme",Selected);
            var active=RenderTexture.active;Graphics.Blit(artwork,animatedFrame,landscape);RenderTexture.active=active;
            lastFrame=Time.frameCount;lastTheme=Selected;
        }
        return animatedFrame;
    }
    static void DrawAtmosphere(Rect full,float time,int theme)
    {
        if(theme==3)
        {
            // 远近三层雪花以不同速度侧向飘落，不闪屏。
            for(int i=0;i<66;i++)
            {
                float depth=.45f+(i%3)*.28f;
                float x=Mathf.Repeat(i*.618034f+time*.008f*depth+Mathf.Sin(time*.35f+i)*.012f,1);
                float y=Mathf.Repeat(i*.381966f+time*.043f*depth,1);
                float size=(1.3f+depth*2.8f)*full.width/1440;
                PastoralMenuUI.Round(new Rect(full.x+x*full.width,full.y+y*full.height,size,size),new Color(.90f,.95f,1,.24f+depth*.32f),size);
            }
            return;
        }
        if(theme==0||theme==1||theme==ClassicHarbor)
        {
            for(int i=0;i<4;i++)
            {
                float x=Mathf.Repeat(time*(.013f+i*.0012f)+i*.137f,1.3f)-.15f;
                float y=.17f+i*.035f+Mathf.Sin(time*.32f+i)*.012f;
                float wing=Mathf.Sin(time*(2.8f+i*.15f)+i)*3.1f;
                var center=new Vector2(full.x+x*full.width,full.y+y*full.height);
                float size=(6+i%2*2)*full.width/1440;
                Color c=new Color(.16f,.23f,.24f,.48f);
                Line(center,new Vector2(center.x-size,center.y-wing),c,1.3f);
                Line(center,new Vector2(center.x+size,center.y-wing),c,1.3f);
            }
        }
        if(theme==1||theme==2)
        {
            for(int i=0;i<(theme==2?14:22);i++)
            {
                float x=Mathf.Repeat(i*.618034f+time*(theme==2?.018f:.009f),1);
                float y=theme==2?Mathf.Repeat(i*.271f+time*.017f,1):.46f+Mathf.Repeat(i*.271f-time*.008f,.53f);
                float size=theme==2?4.8f:2.6f;
                var r=new Rect(full.x+x*full.width,full.y+(y+Mathf.Sin(time*.8f+i)*.018f)*full.height,size,size*.5f);
                var m=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Sin(time+i)*50,r.center);
                PastoralMenuUI.Round(r,theme==2?new Color(.72f,.78f,.36f,.50f):new Color(1,.91f,.57f,.45f),3);GUI.matrix=m;
            }
        }
        if(theme==4)
        {
            for(int i=0;i<18;i++)
            {
                float x=.33f+Mathf.Repeat(i*.381966f,.64f),y=.04f+Mathf.Repeat(i*.1732f,.21f);
                float a=.07f+.26f*Mathf.Pow(.5f+.5f*Mathf.Sin(time*.65f+i*2.1f),3);
                PastoralMenuUI.Round(new Rect(full.x+x*full.width,full.y+y*full.height,2.3f,2.3f),new Color(.80f,.90f,1,a),2);
            }
        }
    }
    static void Line(Vector2 from,Vector2 to,Color color,float width)
    {
        var matrix=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg,from);
        PastoralMenuUI.Fill(new Rect(from.x,from.y-width*.5f,Vector2.Distance(from,to),width),color);GUI.matrix=matrix;
    }
}
