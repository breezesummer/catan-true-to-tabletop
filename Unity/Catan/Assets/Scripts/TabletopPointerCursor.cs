using UnityEngine;

// 鼠标可操作区域统一使用手形；触屏不显示光标。
public sealed class TabletopPointerCursor : MonoBehaviour
{
    static int requested=-10;
    static Texture2D hand;
    bool showing;
    public static void Request(){requested=Time.frameCount;}
    public static void Hover(Rect rect,bool enabled=true)
    {if(enabled&&GUI.enabled&&rect.Contains(Event.current.mousePosition))Request();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if(FindFirstObjectByType<TabletopPointerCursor>()!=null)return;
        var root=new GameObject("交互手形光标");DontDestroyOnLoad(root);root.AddComponent<TabletopPointerCursor>();
    }
    void LateUpdate()
    {
        bool next=!MobileTabletop.Enabled&&requested>=Time.frameCount-1;
        if(next==showing)return;showing=next;
        if(next&&hand==null)
        {
            const int size=32;var pixels=new Color[size*size];
            var polygon=new[]{new Vector2(10,2),new Vector2(14,2),new Vector2(15,13),new Vector2(18,11),new Vector2(21,12),new Vector2(24,13),new Vector2(27,16),new Vector2(26,25),new Vector2(22,30),new Vector2(12,30),new Vector2(4,20),new Vector2(4,17),new Vector2(7,16),new Vector2(10,20)};
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var q=new Vector2(x+.5f,y+.5f);bool inside=false;float edge=99;
                for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
                {
                    var a=polygon[i];var b=polygon[j];
                    if((a.y>q.y)!=(b.y>q.y)&&q.x<(b.x-a.x)*(q.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
                    var d=b-a;edge=Mathf.Min(edge,Vector2.Distance(q,a+d*Mathf.Clamp01(Vector2.Dot(q-a,d)/d.sqrMagnitude)));
                }
                pixels[(size-1-y)*size+x]=inside?(edge<1.3f?Color.black:Color.white):Color.clear;
            }
            hand=new Texture2D(size,size,TextureFormat.RGBA32,false);hand.SetPixels(pixels);hand.Apply();
        }
        Cursor.SetCursor(next?hand:null,next?new Vector2(12,3):Vector2.zero,CursorMode.Auto);
    }
    void OnDestroy(){Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);}
}
