using UnityEngine;

// 发展牌的选择在屏幕中央排列；坐标使用桌面的逻辑像素，棋盘输入避开同一范围。
internal static class ProgressTableUI
{
    internal static readonly Rect Area=new Rect(220,336,1000,228);
    internal static Rect Item(int index,int count)
    {
        float step=Mathf.Min(176,Area.width/Mathf.Max(1,count));
        float width=step*(152f/176),height=width*1.5f;
        return new Rect(Area.center.x+(index-(count-1)*.5f)*step-width/2,Area.center.y-height/2,width,height);
    }
    internal static Rect ButtonArea(int index,int count)
    {
        var slot=Item(index,count);
        return new Rect(slot.x,slot.center.y-20,slot.width,40);
    }
    internal static bool Card(int index,int count,Texture2D face,bool enabled=true,int selected=0)
    {
        var r=Item(index,count);
        var color=GUI.color;
        if(!enabled)GUI.color=new Color(color.r,color.g,color.b,color.a*.45f);
        if(face!=null)EntityCardAssets.DrawCardFace(r,face);
        GUI.color=color;
        if(selected>0)GUI.Label(new Rect(r.x,r.yMax-30,r.width,30),"×"+selected,ModernOperationUI.Center);
        return ThreeKingdomsDirectInput.Read(r,enabled,false)==1;
    }
    internal static readonly Color Ivory = new Color(1f,.93f,.73f);
    internal static readonly Color Gold = new Color(1f,.72f,.28f);
    internal static void Edge(Vector2 a, Vector2 b, bool selected,float transition=1)
    {
        if(Event.current.type!=EventType.Repaint)return;
        var end=Vector2.Lerp(a,b,selected?Mathf.Lerp(.26f,1,transition):.26f*transition);
        StrategyHudArt.Line(a,end,new Color(.20f,.12f,.055f,.87f),4);
        StrategyHudArt.Line(a,end,selected?Gold:Ivory,2);
        if(!selected){
            var start=Vector2.Lerp(b,a,.26f*transition);
            StrategyHudArt.Line(start,b,new Color(.20f,.12f,.055f,.87f),4);
            StrategyHudArt.Line(start,b,Ivory,2);
        }
    }
    static readonly Material[] flatGlow=new Material[2];
    internal static void FaceGlow(Rect face,bool selected)
    {
        if(Event.current.type!=EventType.Repaint)return;
        int index=selected?1:0;
        if(flatGlow[index]==null)
        {
            var material=new Material(Resources.Load<Shader>("DiceSelection")){hideFlags=HideFlags.HideAndDontSave};
            material.EnableKeyword("FLAT_FACE");
            material.SetFloat("_Chosen",index);
            flatGlow[index]=material;
        }
        Graphics.DrawTexture(face,Texture2D.whiteTexture,flatGlow[index]);
    }

    static readonly int[] PipMasks={0,16,257,273,325,341,365};
    internal static void DiceFace(Rect r,int value,bool red,bool selected)
    {
        if(Event.current.type!=EventType.Repaint)return;
        float size=Mathf.Min(r.width-4,r.height-6);
        var face=new Rect(r.center.x-size/2,r.center.y-size/2-(selected?2:0),size,size);
        var shadow=face;shadow.y+=2;
        PastoralMenuUI.Round(shadow,new Color(.19f,.12f,.075f,.9f),size*.19f);
        PastoralMenuUI.Round(face,red?new Color(.60f,.19f,.12f):Ivory,size*.19f);
        var dots=red?Ivory:new Color(.23f,.16f,.11f);
        float d=size*.145f;
        int mask=PipMasks[Mathf.Clamp(value,0,6)];
        for(int i=0;i<9;i++){
            if((mask&(1<<i))==0)continue;
            var center=face.center+new Vector2(i%3-1,i/3-1)*size*.245f;
            PastoralMenuUI.Round(new Rect(center.x-d/2,center.y-d/2,d,d),dots,d/2);
        }
        FaceGlow(face,selected);
    }
    internal static void Mark(Vector2 point,bool selected,int? number)
    {
        var fill=selected?Gold:Ivory;
        if(number.HasValue)
        {
            float y=TabletopMapMarks.Visible?6:-13;
            var r=new Rect(point.x-13,point.y+y-(selected?4:0),26,26);
            ModernOperationUI.Round(r,fill);
            ModernOperationUI.Round(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),new Color(.06f,.18f,.16f));
            GUI.Label(r,number.Value.ToString(),ModernOperationUI.Center);
            return;
        }
        var bounds=new Rect(point.x-19,point.y-23,38,30);
        for(int i=0;i<4;i++)
        {
            bool left=(i&1)==0,top=i<2;
            Stroke(new Rect(left?bounds.x:bounds.xMax-9,top?bounds.y:bounds.yMax-2,9,2),fill);
            Stroke(new Rect(left?bounds.x:bounds.xMax-2,top?bounds.y:bounds.yMax-8,2,8),fill);
        }
    }

    static void Stroke(Rect r,Color color)
    {
        ModernOperationUI.Fill(new Rect(r.x-1,r.y-1,r.width+2,r.height+2),new Color(.025f,.16f,.13f,.90f));
        ModernOperationUI.Fill(r,color);
    }
}
