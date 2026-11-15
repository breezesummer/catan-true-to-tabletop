using UnityEngine;

public sealed partial class M6App
{
    GUIStyle settingsSliderTrack,settingsSliderThumb;

    void SettingsHeading(){
        RuleLine(419,164,268);RuleLine(986,164,269);
        Caption(new Rect(630,122,410,70),"游戏设置",48);
    }
    void SettingsSurface(Rect r,string key,int side,int vertical){
        if(Event.current.type!=EventType.Repaint)return;
        if(!menuSurfaces.TryGetValue(key,out var style)){
            style=new GUIStyle{border=new RectOffset(side,side,vertical,vertical)};
            style.normal.background=Art(key);menuSurfaces[key]=style;
        }
        style.Draw(r,GUIContent.none,false,false,false,false);
    }
    bool SettingsPlate(Rect r,string label,bool selected=false,int size=25,bool available=true){
        var tint=GUI.color;
        if(!available)GUI.color=new Color(.80f,.77f,.70f,tint.a);
        SettingsSurface(InsetMenuRect(r,-6),selected?"setup-settings-jade":"setup-settings-wood",36,20);
        GUI.color=tint;
        // 两端不拉伸；金字的下缘暗影与木牌刻痕分别按屏幕像素绘制。
        PlateLettering(new Rect(r.x+20,r.y,r.width-40,r.height-4),label,size,
            selected?PlateInk.Jade:PlateInk.Wood);
        return available&&Hit(r,false);
    }
    bool SettingsDone(){
        var r=new Rect(585,764,503,86);
        Picture(InsetMenuRect(r,-10),"setup-settings-done");
        PlateLettering(new Rect(r.x+95,r.y-2,r.width-190,r.height),"完成",45,PlateInk.Gold);
        return Hit(r,false);
    }
    float SettingsSlider(int y,string name,float value,string display){
        const float left=520,width=760,knobSize=40;
        value=Mathf.Clamp01(value);
        // 原生滑杆保留焦点、键盘与拖动捕获；隐藏默认皮肤，画面与命中共用圆钮中心。
        if(settingsSliderTrack==null){
            settingsSliderTrack=new GUIStyle(GUIStyle.none){padding=new RectOffset(0,0,0,0)};
            settingsSliderThumb=new GUIStyle(GUIStyle.none){fixedWidth=knobSize,fixedHeight=knobSize};
        }
        GUI.SetNextControlName("settings-slider-"+name);
        float next=GUI.HorizontalSlider(new Rect(left-knobSize/2,y-knobSize/2,width+knobSize,knobSize),
            value,0,1,settingsSliderTrack,settingsSliderThumb);
        Picture(new Rect(274,y-9,18,18),"setup-bullet");
        Caption(new Rect(322,y-29,195,58),name,33,TextAnchor.MiddleLeft);
        SettingsSurface(new Rect(left,y-12,width,24),"setup-settings-rail",12,10);
        float filled=(width-12)*next;
        if(filled>0){
            // 裁切完整漆面而非拉扁端头，归零时不会剩下一截绿条。
            GUI.BeginGroup(new Rect(left+6,y-6,filled,12));
            SettingsSurface(new Rect(0,0,width-12,12),"setup-settings-fill",6,5);
            GUI.EndGroup();
        }
        Picture(new Rect(left+width*next-21,y-21,43,44),"setup-settings-knob");
        Caption(new Rect(1327,y-29,107,58),display,33);
        return next;
    }
}
