using System.Collections.Generic;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

public sealed partial class M6App
{
    const string ZhangLiaoEntry="张辽 · 登锋陷阵", RoadEntry="道路建设 · 基础发展卡";
    readonly Stack<string> bookTrail=new Stack<string>();
    readonly Dictionary<string,Texture2D> bookArt=new Dictionary<string,Texture2D>();
    readonly Dictionary<string,GeneralDefinition> bookGenerals=new Dictionary<string,GeneralDefinition>();
    readonly Dictionary<string,int> bookDevelopment=new Dictionary<string,int>();
    readonly Dictionary<string,int> bookProgress=new Dictionary<string,int>();
    bool IsIllustratedEntry(string name)=>!string.IsNullOrEmpty(name);
    string DetailPath(){
        if(IsBookMode)return BookSections[0]+"  ›  "+bookEntry;
        if(BookRules.ContainsKey(bookEntry))return BookSections[4]+"  ›  "+bookEntry;
        if(IsBookMap)return BookSections[1]+"  ›  "+bookEntry;
        if(bookGenerals.ContainsKey(bookEntry))return BookSections[2];
        return BookSections[3]+"  ›  "+(bookDevelopment.ContainsKey(bookEntry)?"基础发展卡":"城市与骑士");
    }
    void DetailArt(Rect area,string path,bool fit=true){
        if(!bookArt.TryGetValue(path,out var art))bookArt[path]=art=Resources.Load<Texture2D>(path);
        BookTexture(area,art,fit);
    }
    void DetailPage(Rect area)=>BookPicture(area,"ency-detail-page");
    void DetailTab(Rect area,string label,int size){
        BookPicture(area,"ency-tab");
        BookHeading(new Rect(area.x+12,area.y,area.width-24,area.height-5),label,size);
    }
    void DrawBookDetail(){
        if(bookEntry=="航海家"){DrawVoyageEntry();return;}
        if(bookEntry=="穿越沙漠"){DrawDesertEntry();return;}
        if(IsBookMap||IsBookMode||BookRules.ContainsKey(bookEntry)){DrawAtlasEntry();return;}
        if(bookGenerals.TryGetValue(bookEntry,out var general)){
            DrawGeneralSpread(general);
            return;
        }
        bool basic=bookDevelopment.TryGetValue(bookEntry,out int kind);
        var face=basic?RedrawnCardAssets.Base(kind):EntityCardAssets.ProgressFront(bookProgress[bookEntry]);
        DetailPage(new Rect(77,131,545,633));
        if(face!=null){
            var rect=EntityCardAssets.FitCardRect(new Rect(115,145,468,602),(float)face.width/face.height,GUI.matrix);
            BookTexture(rect,face);
            // 卡面插画走百科原色材质；正式标题和描述直接按屏幕像素排字。
            using(var pixels=new PastoralMenuUI.PixelText(rect,1))
                RedrawnCardAssets.DrawText(pixels.Area,face,originalInk:true);
        }
        DetailPage(new Rect(634,131,959,633));
        if(bookEntry==RoadEntry){
            DetailTab(new Rect(695,165,430,80),"逐段放置",40);
            BookLabel(new Rect(696,267,822,78),"先选第一段的合法位置，再从更新后的棋盘选择第二段。",27,TextAnchor.MiddleLeft);
            DetailArt(new Rect(742,335,726,403),SceneArt+"道路建设");
            RelatedHeading();Related(0,528,"道路","ency-detail-road");Related(1,528,"船","mode-sea");
        }else{
            DrawCardExample(basic?BaseCards[kind]:M5App.Cards[bookProgress[bookEntry]]);
        }
    }
    void DrawGeneralSpread(GeneralDefinition general){
        DetailPage(new Rect(78,128,490,632));
        DetailArt(new Rect(95,145,455,596),"GeneralUI/v001/Portraits/"+general.Id);
        DetailPage(new Rect(582,128,1013,632));
        BookHeading(new Rect(620,146,440,88),general.Name,54);
        BookLabel(new Rect(1065,154,455,76),general.Faction+" · "+general.Title,33);
        if(bookEntry==ZhangLiaoEntry){
            DetailTab(new Rect(927,247,318,71),general.Skills[0].Name,38);
            BookHeading(new Rect(620,327,205,57),"发动时机",28);
            BookLabel(new Rect(854,327,679,57),"任意回合的摸牌阶段开始时。",28,TextAnchor.MiddleLeft);
            BookHeading(new Rect(620,393,205,89),"技能效果",28);
            BookLabel(new Rect(854,393,685,89),"每少摸一张牌，可从一名不同的玩家处\n获得一张手牌。",28,TextAnchor.MiddleLeft);
            DetailArt(new Rect(660,466,875,222),SceneArt+"tk-g01-s1");
            BookLabel(new Rect(643,683,894,49),"少摸两张 → 从两名玩家处各获得一张",28);
            RelatedHeading();Related(0,528,"摸牌阶段","ency-detail-draw");Related(1,528,"手牌","ency-detail-hand");
        }else{
            DrawSkillExample(general);
        }
    }
    void DetailRule(float x,float y,float width,string icon,string title,string body){
        DetailPage(new Rect(x,y,width,133));
        BookTexture(new Rect(x+16,y+12,140,109),Art("ency-detail-"+icon),true);
        BookLabel(new Rect(x+172,y+8,width-182,48),title,33,TextAnchor.MiddleLeft,true);
        BookLabel(new Rect(x+172,y+55,width-182,70),body,24,TextAnchor.MiddleLeft);
    }
    void RelatedHeading()=>DetailTab(new Rect(82,799,244,64),"相关词条",30);
    void Related(int slot,float width,string label,string image,bool menuArt=true){
        var r=new Rect(346+slot*(width+18),778,width,109);
        DetailPage(r);
        var icon=new Rect(r.x+12,r.y+11,98,85);
        if(menuArt)BookTexture(icon,Art(image),true);else DetailArt(icon,image);
        BookHeading(new Rect(r.x+116,r.y+2,r.width-178,r.height-4),label,31);
        BookHeading(new Rect(r.xMax-54,r.y+1,40,r.height-4),"›",37);
        if(!Hit(r))return;
        if(bookTrail.Contains(label)){while(bookTrail.Pop()!=label){}}
        else bookTrail.Push(bookEntry);
        bookEntry=label;
    }
    void DrawVoyageEntry(){
        BookPicture(new Rect(76,127,849,629),"ency-detail-voyage");
        DetailPage(new Rect(937,127,659,629));
        DetailTab(new Rect(1035,147,455,81),"航海家",47);
        BookLabel(new Rect(953,230,625,63),"从海岸出发，用船连接新的岛屿。",29);
        DetailRule(955,302,619,"cost","建造船只","一份木材与一份羊毛。");
        DetailRule(955,444,619,"junction","连接航线","在自己的沿海聚落或城市\n连接道路与船。");
        DetailRule(955,586,619,"pirate","海盗","海盗所在海格的边不能新建船只。");
        RelatedHeading();Related(0,396,"船","mode-sea");Related(1,396,"海盗","ency-detail-pirate");
        Related(2,396,"穿越沙漠","ency-detail-dunes");
    }
    void DrawDesertEntry(){
        var map=new Rect(77,130,920,616);
        DetailPage(map);
        DetailArt(new Rect(93,147,887,581),"TabletopUI/HandPainted/sea-wash",false);
        // 详情展示对应人数的剧本图。标记与同一底图共用归一化坐标。
        bool three=PlayerCount==3;
        var atlas=Art("atlas-through-the-desert-"+(three?"3":"4"));
        var area=new Rect(139,208,798,458);
        if(atlas!=null){
            float scale=Mathf.Min(area.width/atlas.width,area.height/atlas.height);
            var size=new Vector2(atlas.width*scale,atlas.height*scale);
            area=new Rect(area.center-size*.5f,size);
            BookTexture(area,atlas);
        }
        MapPin(area,new Vector2(three?.42f:.43f,.62f),new Rect(112,640,228,58),"主岛起始区");
        MapPin(area,new Vector2(three?.17f:.14f,.14f),new Rect(109,161,209,58),"沙漠彼岸");
        MapPin(area,new Vector2(.84f,three?.74f:.64f),new Rect(755,644,184,58),"外岛");
        DetailPage(new Rect(1010,130,585,616));DetailTab(new Rect(1100,150,406,76),"穿越沙漠",44);
        DetailRule(1027,240,552,"settle","开局定居","在主岛起始区放置聚落。");
        DetailRule(1027,393,552,"dunes","跨越沙漠","首次在沙漠彼岸定居，\n额外获得 2 分。");
        DetailRule(1027,546,552,"isle","航向外岛","首次在每座外岛定居，\n额外获得 2 分。");
        RelatedHeading();Related(0,528,"航海家","mode-sea");Related(1,528,"海港与捕鱼","ency-detail-fish");
    }
    void MapPin(Rect map,Vector2 uv,Rect tag,string title){
        Vector2 target=map.min+Vector2.Scale(map.size,uv);
        Vector2 start=new Vector2(tag.center.x,target.y<tag.center.y?tag.y:tag.yMax);
        var matrix=GUI.matrix;
        try{
            GUIUtility.RotateAroundPivot(Vector2.SignedAngle(Vector2.right,target-start),start);
            PastoralMenuUI.Fill(new Rect(start.x,start.y-2,(target-start).magnitude,4),((Color)new Color32(121,75,32,255)).linear);
        }finally{GUI.matrix=matrix;}
        BookHeading(new Rect(target.x-13,target.y-17,26,34),"●",24);
        DetailTab(tag,title,26);
    }
}
