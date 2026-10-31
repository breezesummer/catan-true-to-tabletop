using UnityEngine;

// 反馈只跟随真实 IMGUI 控件捕获；单个活动/释放状态，队列不会增长。
public static class TabletopButtonFeedback
{
    static Vector2 pressedAt, releasedAt;
    static bool pressed;
    static float releasedTime=-10;
    public static bool Button(Rect r,bool sound=true)
    {
        var e=Event.current;var screen=GUIUtility.GUIToScreenPoint(r.center);
        bool over=GUI.enabled&&r.Contains(e.mousePosition);
        if(e.rawType==EventType.MouseDown&&e.button==0&&over){pressedAt=screen;pressed=true;}
        bool down=pressed&&pressedAt==screen&&GUIUtility.hotControl!=0;
        bool hit=GUI.Button(r,GUIContent.none,GUIStyle.none);
        if(e.rawType==EventType.MouseUp&&e.button==0)pressed=false;
        hit=hit&&(!MobileTabletop.Enabled||!MobileTabletop.TouchDragged);
        if(hit){releasedAt=screen;releasedTime=Time.unscaledTime;if(sound)TabletopAudio.Instance?.Click();}
        if(e.type==EventType.Repaint&&GUI.enabled)
        {
            float pulse=releasedAt==screen?Mathf.Clamp01(1-(Time.unscaledTime-releasedTime)/.22f):0;
            if(over||pulse>0)
            {
                var inset=new Rect(r.x+3,r.y+3,r.width-6,r.height-6);
                GUI.DrawTexture(inset,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.94f,.91f,.66f,down?.19f:.045f+pulse*.15f),0,Mathf.Min(12,r.height*.25f));
                if(down)ModernOperationUI.Fill(new Rect(r.x+8,r.y+4,r.width-16,2),new Color(.02f,.07f,.04f,.6f));
            }
        }
        return hit;
    }
}
