using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;

// 照片卡面只负责显示，不决定卡牌效果或查看权限。
// 调用方只能登记当前 PlayerView 的手牌及明确获准查看的卡面。
public static class EntityCardAssets
{
    const string Root = "EntityAssets/v001/";
    static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
    static Texture2D paperLight;
    static readonly Card[] goodCards=new Card[8],progressCards=new Card[25],improvementCards=new Card[30],guidanceCards=new Card[2];
    static readonly Card defenderCard=new Card("defender",Root+"defender-card/defender-card-front");
    static readonly string[] GoodIds = { "wood", "brick", "wool", "grain", "ore", "cloth", "coin", "paper" };
    static readonly string[] TrackIds = { "trade", "politics", "science" };
    // Ordered to match the stable M4/M5 ProgressCardKind values, not the printed photo text.
    static readonly string[] ProgressIds = {
        "alchemy", "crane", "engineering", "invention", "irrigation", "medicine", "mining", "printing", "road-building", "smithing",
        "commercial-harbor", "guild-dues", "merchant", "merchant-fleet", "resource-monopoly", "trade-monopoly",
        "diplomacy", "espionage", "encouragement", "intrigue", "taxation", "constitution", "treason", "wedding", "sabotage"
    };
    public sealed class Card
    {
        public readonly string Id, Front, Back;
        public readonly bool ShowBack;
        public Card(string id, string front, string back = null, bool showBack = false)
        { Id = id; Front = front; Back = back; ShowBack = showBack; }
    }
    public static Texture2D Texture(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!Textures.TryGetValue(path, out var texture) || texture == null)
        {
            texture = Resources.Load<Texture2D>(path);
            if (texture != null) Textures[path] = texture;
        }
        return texture;
    }
    public static Card Good(int index)
    {
        if (index < 0 || index >= GoodIds.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if(goodCards[index]!=null)return goodCards[index];
        if (index == 0) return goodCards[index]=new Card("wood", "PhotoSamples/v001/wood-front", "PhotoSamples/v001/resource-back");
        string type = index < 5 ? "resource" : "commodity";
        return goodCards[index]=new Card(GoodIds[index], Root + type + "-cards/" + type + "-" + GoodIds[index] + "-front", index < 5 ? Root + "resource-cards/resource-back-original" : null);
    }
    public static Card Progress(int index)
    {
        if (index < 0 || index >= ProgressIds.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if(progressCards[index]!=null)return progressCards[index];
        string track = index < 10 ? "science" : index < 16 ? "trade" : "politics";
        string folder = "progress-" + track + "/", id = "progress-" + track + "-" + ProgressIds[index];
        string back = track == "politics" ? "EntityAssets/v003/progress-politics/progress-politics-back-original" : Root + folder + "progress-" + track + "-back-original";
        return progressCards[index]=new Card(id, Root + folder + id + "-front", back);
    }
    public static Card Improvement(int track, int level, bool built)
    {
        if (track < 0 || track >= TrackIds.Length || level < 1 || level > 5) throw new ArgumentOutOfRangeException();
        int key=(track*5+level-1)*2+(built?1:0);if(improvementCards[key]!=null)return improvementCards[key];
        string id = "improvement-" + TrackIds[track] + "-" + level;
        return improvementCards[key]=new Card(id, Root + "city-improvements/" + id + "-front", Root + "city-improvements/" + id + "-back", !built);
    }
    public static Card Guidance(bool maximum)
    {
        string id = maximum ? "guidance-max" : "guidance-upgrade";
        int key=maximum?1:0;return guidanceCards[key]??(guidanceCards[key]=new Card(id, Root + "city-improvements/" + id + "-front"));
    }
    public static Card Defender => defenderCard;
    public static IEnumerator Prewarm(string rules)
    {
        bool cities=rules=="cities-knights"||rules=="cities"||rules=="combined";
        var paths=new List<string>();
        for(int good=0;good<(cities?8:5);good++){paths.Add(Good(good).Front);paths.Add(Good(good).Back);}
        if(cities)
        {
            paths.Add(Defender.Front);
            for(int card=0;card<25;card++){paths.Add(Progress(card).Front);paths.Add(Progress(card).Back);}
        }
        const long budget=64*1024*1024;long loaded=0;
        foreach(string path in paths.Where(p=>!string.IsNullOrEmpty(p)).Distinct())
        {
            if(Textures.TryGetValue(path,out var existing)&&existing!=null)continue;
            if(loaded>=budget)yield break;
            var request=Resources.LoadAsync<Texture2D>(path);yield return request;
            var texture=request.asset as Texture2D;if(texture==null)continue;
            // 每帧至多接收一张，只保留预算内的预热引用；正常获准展示仍可按需加载。
            long size=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
            if(loaded+size>budget)yield break;
            Textures[path]=texture;loaded+=size;
        }
    }
    public static Texture2D ResourceFront(string resource)
    {
        resource = (resource ?? "").ToLowerInvariant();
        if (resource == "wheat") resource = "grain";
        int index = Array.IndexOf(GoodIds, resource);
        return index >= 0 && index < 5 ? Texture(Good(index).Front) : null;
    }
    public static Texture2D CommodityFront(string commodity)
    {
        commodity = (commodity ?? "").ToLowerInvariant();
        int index = Array.IndexOf(GoodIds, commodity);
        return index >= 5 ? Texture(Good(index).Front) : null;
    }
    public static Texture2D ResourceFront(int resource) => resource >= 0 && resource < 5 ? Texture(Good(resource).Front) : null;
    public static Texture2D CommodityFront(int commodity) => commodity >= 0 && commodity < 3 ? Texture(Good(commodity + 5).Front) : null;
    public static Texture2D ResourceOrCommodityFront(int good) => good >= 0 && good < 8 ? Texture(Good(good).Front) : null;
    // v007：原始照片和印刷比例保持不变，只裁去实体圆角以外的拍摄背景。
    // 纸芯、投影与整面弱受光属于显示层，不重新加工照片或遮挡规则文字。
    public static void DrawCardFace(Rect area,Texture2D texture,float elevation=0,Color? tint=null)
    {
        if(texture==null)return;
        var rect=FitCardRect(area,(float)texture.width/texture.height,GUI.matrix);
        DrawPaperDepth(rect,elevation);
        float radius=Mathf.Min(rect.width,rect.height)*.095f;
        GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill,true,0,tint??Color.white,0,radius);
        DrawPaperLight(rect,radius);
    }
    public static void DrawCardPaper(Rect rect,Color color,float elevation=0)
    {
        DrawPaperDepth(rect,elevation);
        float radius=Mathf.Min(rect.width,rect.height)*.095f;
        GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);
        DrawPaperLight(rect,radius);
    }
    static void DrawPaperDepth(Rect rect,float elevation)
    {
        if(Event.current.type!=EventType.Repaint||rect.width<=0||rect.height<=0)return;
        float radius=Mathf.Min(rect.width,rect.height)*.095f;
        float scale=Mathf.Clamp(rect.width/108f,.36f,2.4f),lift=Mathf.Clamp01(elevation);
        // 多层低透明度投影提供连续柔边；不添加卡套或高亮装饰横线。
        for(int layer=5;layer>=1;layer--)
        {
            float spread=(.65f+layer*.8f+lift*1.4f)*scale;
            var shadow=new Rect(rect.x-spread+.8f*scale,rect.y-spread+(3.3f+lift*4)*scale,rect.width+2*spread,rect.height+2*spread);
            GUI.DrawTexture(shadow,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.035f,.023f,.013f,.032f),0,radius+spread);
        }
        var core=new Rect(rect.x+.45f*scale,rect.y+1.7f*scale,rect.width,rect.height);
        GUI.DrawTexture(core,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.57f,.50f,.36f),0,radius);
        core.y-=.65f*scale;
        GUI.DrawTexture(core,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.89f,.84f,.70f),0,radius);
    }
    static void DrawPaperLight(Rect rect,float radius)
    {
        if(Event.current.type!=EventType.Repaint)return;
        if(paperLight==null)
        {
            const int width=96,height=144;
            paperLight=new Texture2D(width,height,TextureFormat.RGBA32,false){name="卡纸漫反射薄覆膜 v007",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float u=(x+.5f)/width,v=(y+.5f)/height;
                float glow=Mathf.Exp(-((u-.12f)*(u-.12f)*2.1f+(v-.88f)*(v-.88f)*1.8f))*.075f;
                float shade=Mathf.Pow(1-v,2)*.05f;
                // 固定微颗粒，不读取或消耗规则随机数。
                float grain=(((x*17+y*31+x*y*3)%23)/22f-.5f)*.009f;
                float light=glow-shade+grain;
                pixels[y*width+x]=light>=0?new Color(1,.97f,.90f,light):new Color(.14f,.10f,.06f,-light);
            }
            paperLight.SetPixels32(pixels);paperLight.Apply(false,true);
        }
        GUI.DrawTexture(rect,paperLight,ScaleMode.StretchToFill,true,0,Color.white,0,radius);
    }
    // 在屏幕像素中保持比例；逻辑坐标里的 ScaleToFit 仍会被外层非等比矩阵拉伸。
    public static Rect FitCardRect(Rect area,float aspect,Matrix4x4 matrix)
    {
        float scaleX=matrix.MultiplyVector(Vector3.right).magnitude;
        float scaleY=matrix.MultiplyVector(Vector3.up).magnitude;
        if(scaleX<=0||scaleY<=0||aspect<=0)return area;
        float logicalAspect=aspect*scaleY/scaleX;
        float width=Mathf.Min(area.width,area.height*logicalAspect);
        float height=width/logicalAspect;
        return new Rect(area.center.x-width/2,area.center.y-height/2,width,height);
    }
    public static Texture2D ProgressFront(int card) => Texture(Progress(card).Front);
    public static Texture2D ProgressBack(int track)
    {
        if (track < 0 || track > 2) throw new ArgumentOutOfRangeException(nameof(track));
        return Texture(Progress(track == 0 ? 10 : track == 1 ? 16 : 0).Back);
    }
    public static Texture2D ImprovementFront(int track, int level) => Texture(Improvement(track, level, true).Front);
    public static Texture2D ImprovementBack(int track, int level) => Texture(Improvement(track, level, false).Back);

    // Each local UI owns a Display. A selected private face is reauthorized every frame and
    // immediately cleared by a privacy curtain, a changed seat or a menu transition.
    public sealed class Display
    {
        sealed class VisibleCard { public Card Card; public string Caption; }
        readonly Dictionary<string, VisibleCard> visible = new Dictionary<string, VisibleCard>();
        static readonly Rect PreviewRect = new Rect(716, 167, 282, 563);
        string scope, selected;
        bool flipped;
        public bool HasSelection => selected != null;
        public bool SelectionIsAuthorized => selected != null && visible.ContainsKey(selected);
        public void Clear() { visible.Clear(); selected = null; scope = null; flipped = false; }
        public void Begin(string playerScope)
        {
            if (playerScope == null || playerScope != scope) Clear();
            scope = playerScope; visible.Clear();
        }
        public bool BlocksBoardInput(Vector2 logicalMouse) => selected != null && PreviewRect.Contains(logicalMouse);
        public void Register(Card card, string caption)
        { if (scope != null) visible[card.Id + "|" + caption] = new VisibleCard { Card = card, Caption = caption }; }
        public bool SelectVisible(Card card, string caption)
        {
            string key = card.Id + "|" + caption;
            if (scope == null || !visible.ContainsKey(key)) return false;
            selected = key; flipped = card.ShowBack; return true;
        }
        public void ValidateVisibility()
        { if (selected != null && !visible.ContainsKey(selected)) { selected = null; flipped = false; } }
        public bool Badge(Rect rect, Card card, string caption, GUIStyle style)
        {
            var face = Texture(card.ShowBack ? card.Back : card.Front);
            if (face == null) face = Texture(card.Front);
            if (face == null) return false; // Keep the original textual UI when a face is unavailable.
            string key = card.Id + "|" + caption;
            Register(card, caption);
            DrawCardFace(new Rect(rect.x,rect.y,rect.width,Mathf.Max(1,rect.height-23)),face);
            var strip = new Rect(rect.x, rect.yMax - 21, rect.width, 21);
            GUI.Label(strip, caption, style);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            { if (selected == key) selected = null; else SelectVisible(card, caption); }
            return true;
        }
        public bool LayoutBadge(Card card, string caption, GUIStyle style, float width = 68, float height = 91)
        {
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            bool available = Badge(rect, card, caption, style);
            if (!available) GUI.Label(rect, caption, style);
            return available;
        }
        public void GoodRow(int[] amounts, string[] names, GUIStyle style)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < amounts.Length; i++)
            {
                LayoutBadge(Good(i), names[i] + " " + amounts[i], style, 46, 67);
            }
            GUILayout.EndHorizontal();
        }
        public void Improvements(int[] levels, string[] trackNames, GUIStyle style)
        {
            for (int track = 0; track < 3; track++)
            {
                GUILayout.Label(trackNames[track] + " · 等级 " + levels[track], style);
                GUILayout.BeginHorizontal();
                for (int level = 1; level <= 5; level++)
                    LayoutBadge(Improvement(track, level, level <= levels[track]), level + "级" + (level <= levels[track] ? " 已建" : " 待建"), style, 68, 75);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            LayoutBadge(Guidance(false), "升级提示", style, 170, 94);
            LayoutBadge(Guidance(true), "满级提示", style, 170, 94);
            GUILayout.EndHorizontal();
        }
        public void DrawPreview(GUIStyle label, GUIStyle small, GUIStyle button)
        {
            if (scope == null) { Clear(); return; }
            ValidateVisibility();
            string key = selected;
            if (key == null || !visible.TryGetValue(key, out var item)) return;
            bool back = flipped;
            var face = Texture(back ? item.Card.Back : item.Card.Front);
            if (face == null) face = Texture(item.Card.Front);
            bool hasBack = Texture(item.Card.Back) != null;
            DrawCardFace(new Rect(716,167,282,451),face);
            GUI.Label(new Rect(731,623,250,43),item.Caption,label);
            if (selected != null)
            {
                if (hasBack && GUI.Button(new Rect(731, 681, 118, 33), back ? "查看正面" : "查看背面", button)) flipped = !flipped;
                if (GUI.Button(new Rect(861, 681, 118, 33), "关闭", button)) selected = null;
            }
        }
    }
}
