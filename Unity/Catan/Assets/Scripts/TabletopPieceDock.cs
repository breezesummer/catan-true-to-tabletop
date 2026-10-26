using UnityEngine;

// 库存使用固定屏幕框：模型轮廓和数量各占独立区域，地图相机不改变其尺寸。
public static class TabletopPieceDock
{
    static Texture2D frame;
    public static Rect GroupLayout(int pieceCount,int index)
    {
        // 双扩展八组库存用两列，保持银行下沿与底部武将框之间的安全空间。
        int rows=pieceCount>6?(pieceCount+1)/2:pieceCount;
        int column=rows==0?0:index/rows,row=rows==0?0:index%rows;
        return new Rect(22+column*122,632-(rows-1-row)*58,112,52);
    }
    public static Vector2 PiecePosition(Rect group) => new Vector2(group.x + 29, group.y + 26);
    public static Rect ModelArea(Rect group) => new Rect(group.x + 5, group.y + 6, 48, 40);
    public static Rect CountArea(Rect group) => new Rect(group.x + 63, group.y + 8, 43, 36);

    public static void DrawFrame(Rect area, bool selected, bool available)
    {
        var accent=ModernOperationUI.Accent;
        var line=Color.Lerp(ModernOperationUI.Line,accent,selected?.88f:.28f);
        var fill=Color.Lerp(ModernOperationUI.Background,ModernOperationUI.Surface,selected?.44f:.18f);
        if(!available)fill=Color.Lerp(fill,ModernOperationUI.Background,.35f);
        // IMGUI 在三维 HUD 相机之后绘制；轮廓纹理中间透明，绝不能用整块底色盖住模型。
        DrawOutline(new Rect(area.x,area.y+3,area.width,area.height),new Color(0,0,0,.32f));
        DrawOutline(area,line);
        ModernOperationUI.Round(new Rect(area.x+62,area.y+4,area.width-66,area.height-8),fill);
        ModernOperationUI.Fill(new Rect(area.x+12,area.y+3,area.width-24,1),new Color(accent.r,accent.g,accent.b,.23f));
        ModernOperationUI.Fill(new Rect(area.x+58,area.y+12,1,area.height-24),new Color(line.r,line.g,line.b,.62f));
        var corner=new Color(accent.r,accent.g,accent.b,selected?.95f:.62f);
        foreach(int side in new[]{0,1})
        {
            float x=side==0?area.x+5:area.xMax-6;
            foreach(int end in new[]{0,1})
            {
                float y=end==0?area.y+5:area.yMax-6;
                ModernOperationUI.Fill(new Rect(side==0?x:x-7,y,8,1),corner);
                ModernOperationUI.Fill(new Rect(x,end==0?y:y-7,1,8),corner);
            }
        }
    }

    static void DrawOutline(Rect area,Color color)
    {
        if(frame==null)
        {
            // 以解析圆角矩形距离生成透明描边，不保存或改写任何实体参考资产。
            const int width=224,height=104;
            frame=new Texture2D(width,height,TextureFormat.RGBA32,false){name="库存圆角透明镶边 v011",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color32[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                var point=new Vector2(Mathf.Abs((x+.5f)/2-56)-48,Mathf.Abs((y+.5f)/2-26)-18);
                var outside=new Vector2(Mathf.Max(point.x,0),Mathf.Max(point.y,0));
                float distance=outside.magnitude+Mathf.Min(Mathf.Max(point.x,point.y),0)-8;
                pixels[y*width+x]=new Color(1,1,1,Mathf.Clamp01(1.1f-Mathf.Abs(distance+.8f)));
            }
            frame.SetPixels32(pixels);frame.Apply(false,true);
        }
        var prior=GUI.color;GUI.color=color;GUI.DrawTexture(area,frame);GUI.color=prior;
    }

    public static void DrawCount(Rect group, int count, GUIStyle source)
    {
        var area=CountArea(group);
        var value=new GUIStyle(source){fontSize=23,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,
            wordWrap=false,padding=new RectOffset(),clipping=TextClipping.Overflow};
        value.normal.textColor=ModernOperationUI.TextColor;
        GUI.Label(area,count.ToString(),value);
        var times=new GUIStyle(value){fontSize=12,fontStyle=FontStyle.Normal};
        times.normal.textColor=ModernOperationUI.Muted;
        GUI.Label(new Rect(group.x+59,group.y+18,10,16),"×",times);
    }
}
