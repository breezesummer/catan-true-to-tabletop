using UnityEngine;

// 发展牌的选择留在手牌上方；坐标使用桌面的逻辑像素，棋盘输入避开同一范围。
internal static class ProgressTableUI
{
    internal static readonly Rect Area=new Rect(344,569,906,108);
    internal static Rect Item(int index,int count)
    {
        float width=Mathf.Min(76,(Area.width-16)/Mathf.Max(1,count));
        return new Rect(Area.center.x+(index-count*.5f)*width+2,Area.y+6,width-4,Area.height-12);
    }
    internal static bool Card(int index,int count,Texture2D face,string caption,bool enabled=true)
    {
        var r=Item(index,count);
        var color=GUI.color;
        if(!enabled)GUI.color=new Color(color.r,color.g,color.b,color.a*.45f);
        if(face!=null)EntityCardAssets.DrawCardFace(new Rect(r.x+4,r.y,Mathf.Max(8,r.width-8),r.height-22),face);
        GUI.Label(new Rect(r.x,r.yMax-23,r.width,23),caption,ModernOperationUI.Center);
        GUI.color=color;
        return ThreeKingdomsDirectInput.Read(r,enabled,false)==1;
    }

    internal static void Mark(Vector2 point,bool selected,int? number)
    {
        var fill=selected?new Color(1,.80f,.31f):new Color(.64f,1,.88f);
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
