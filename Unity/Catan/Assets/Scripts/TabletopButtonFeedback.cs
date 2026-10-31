using System;
using UnityEngine;

// 原始矩形负责捕获输入；绘制作用域只移动外观，不移动命中区域。
public static class TabletopButtonFeedback
{
    static Vector2 pressedAt,releasedAt;
    static bool pressed,pressedInside;
    static float releasedTime=-10;
    public static bool Held(Rect r)=>GUI.enabled&&pressed&&pressedAt==GUIUtility.GUIToScreenPoint(r.center)&&GUIUtility.hotControl!=0&&pressedInside&&!MobileTabletop.TouchDragged;
    public readonly struct DrawScope : IDisposable
    {
        readonly Matrix4x4 matrix;
        public DrawScope(Rect r,bool enabled)
        {
            matrix=GUI.matrix;if(!enabled)return;
            bool down=Held(r);float elapsed=Time.unscaledTime-releasedTime;
            bool released=releasedAt==GUIUtility.GUIToScreenPoint(r.center)&&elapsed<.18f;
            float depth=down?1:released?Mathf.Pow(1-elapsed/.18f,2):0;
            if(depth<=0)return;
            float scale=1-.045f*depth;
            GUI.matrix=matrix*Matrix4x4.Translate(new Vector3(r.center.x,r.center.y+3.5f*depth,0))*Matrix4x4.Scale(new Vector3(scale,scale,1))*Matrix4x4.Translate(-r.center);
        }
        public void Dispose(){GUI.matrix=matrix;}
    }
    public static DrawScope Begin(Rect r,bool enabled=true)=>new DrawScope(r,GUI.enabled&&enabled);
    public static bool Button(Rect r,bool sound=true,bool round=false)
    {
        var e=Event.current;var screen=GUIUtility.GUIToScreenPoint(r.center);
        bool over=GUI.enabled&&r.Contains(e.mousePosition);
        if(e.type==EventType.MouseDown&&e.button==0&&over){pressedAt=screen;pressed=true;pressedInside=true;}
        if(pressed&&pressedAt==screen&&(e.type==EventType.MouseDrag||e.type==EventType.MouseMove))pressedInside=over;
        bool down=Held(r);
        bool hit=GUI.Button(r,GUIContent.none,GUIStyle.none);
        if(e.rawType==EventType.MouseUp&&e.button==0)pressed=false;
        hit=hit&&(!MobileTabletop.Enabled||!MobileTabletop.TouchDragged);
        if(hit){releasedAt=screen;releasedTime=Time.unscaledTime;if(sound)TabletopAudio.Instance?.Click();}
        if(e.type==EventType.Repaint&&GUI.enabled)
        {
            float pulse=releasedAt==screen?Mathf.Clamp01(1-(Time.unscaledTime-releasedTime)/.24f):0;
            if(over||pulse>0||down)
            {
                using(Begin(r))
                {
                    float inset=round?r.width*.12f:5;
                    var face=new Rect(r.x+inset,r.y+inset,r.width-2*inset,r.height-2*inset);
                    float radius=round?face.height/2:Mathf.Min(8,face.height/2);
                    Color tint=down?new Color(.01f,.045f,.025f,.30f):new Color(1,.91f,.62f,.055f+pulse*.19f);
                    GUI.DrawTexture(face,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,tint,0,radius);
                    if(down&&!round)ModernOperationUI.Fill(new Rect(face.x+5,face.y+1,face.width-10,2),new Color(.015f,.06f,.03f,.65f));
                }
            }
        }
        return hit;
    }
}
