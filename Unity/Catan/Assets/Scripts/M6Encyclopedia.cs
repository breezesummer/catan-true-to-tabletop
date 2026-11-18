using System;
using System.Collections.Generic;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

public sealed partial class M6App
{
    bool encyclopediaOpen;
    Vector2 encyclopediaScroll;
    int bookSection=-1;
    string bookQuery="",bookEntry="";
    RenderTexture bookMap;
    bool attemptedBookMap;
    static readonly string[] BookSections={"扩展与模式","地图玩法","武将图鉴","卡牌图鉴","棋子与规则"};
    static readonly string[] BookModes={"航海家","城市与骑士","三国争霸","海港与捕鱼"};
    static readonly string[] BookModeArt={"mode-sea","mode-city","mode-kingdom","mode-harbor"};
    static readonly string[] BookModeRules={"建造船只，沿航线探索新的岛屿。","发展城市，激活骑士抵御蛮族。","选择武将，在对应时机发动技能。","在港口争夺海港大师，从渔场或湖泊获得鱼，支付鱼使用捕鱼行动。"};
    static readonly string[] BaseCards={"骑士","道路建设","丰收","垄断","胜利点"};
    static readonly string[] BaseCardRules={"移动强盗并向相邻的一名对手随机拿取一张资源；航海家可选择移动海盗。","免费建设两段道路；航海家可选择道路或船，逐段选择合法位置。","从银行领取两张资源，可选同一种；受银行剩余资源限制。","选择一种资源，所有对手将该种资源全部交给你。","持有即计 1 分；在自己回合达到胜利条件时公开。"};
    readonly List<(int section,string name,string body)> bookIndex=new List<(int,string,string)>();

    void OpenBook(){
        bookSkillChoices.Clear();bookSkillScroll.Clear();bookTrail.Clear();BuildBookIndex();bookQuery=bookEntry="";bookSection=-1;encyclopediaScroll=Vector2.zero;
        ReleaseBookMap();attemptedBookMap=false;encyclopediaOpen=true;
    }
    void BookBack(){
        if(bookEntry.Length>0)bookEntry=bookTrail.Count>0?bookTrail.Pop():"";
        else if(bookSection>=0||bookQuery.Length>0){bookSection=-1;bookQuery="";}
        else {encyclopediaOpen=false;ReleaseBookMap();}
        if(Event.current!=null)GUI.FocusControl(null);
    }
    void ReleaseBookMap(){
        if(bookMap!=null){bookMap.Release();Destroy(bookMap);bookMap=null;}
    }
    void BuildBookIndex(){
        if(bookIndex.Count>0)return;
        for(int i=0;i<BookModes.Length;i++)bookIndex.Add((0,BookModes[i],BookModeRules[i]));
        bookIndex.Add((0,"基础版","建设道路、聚落和城市，通过交易与发展卡取得胜利点。"));
        bookIndex.Add((1,"标准岛屿","围绕主岛建设聚落、道路和城市。"));
        foreach(var pair in mapTitles){
            bookMapIds[pair.Value]=pair.Key;
            var scenario=Catan.Core.M3.SeafarersScenarios.Create(pair.Key,4);
            string overview=scenario.Description.Replace("；"+scenario.TargetVictoryPoints+" 分获胜","");
            bookIndex.Add((1,pair.Value,overview));
        }
        foreach(var general in GeneralCatalog.Definitions){
            string name=general.Name+" · "+general.Title;
            bookGenerals.Add(name,general);
            bookIndex.Add((2,name,GeneralDescription(general)));
        }
        for(int i=0;i<BaseCards.Length;i++){
            string name=BaseCards[i]+" · 基础发展卡";
            bookDevelopment.Add(name,i);
            bookIndex.Add((3,name,BaseCardRules[i]));
        }
        for(int i=0;i<M5App.Cards.Length;i++){
            string name=M5App.Cards[i]+" · 城市与骑士";
            bookProgress.Add(name,i);
            bookIndex.Add((3,name,M5App.CardHelp[i]));
        }
        foreach(var rule in BookRules)bookIndex.Add((4,rule.Key,rule.Value.text));
    }
    bool SeaBookMap=>RuleSet=="seafarers"||RuleSet=="combined";
    string CurrentBookMap=>SeaBookMap?MapTitle(ScenarioId):"标准岛屿";
    bool BookModeActive(int i){
        switch(i){case 0:return SeaBookMap;case 1:return RuleSet=="cities-knights"||RuleSet=="combined";
            case 2:return ThreeKingdoms;default:return Harbormaster||Fishing;}
    }
    void CaptureBookMapIfNeeded(){
        if(!encyclopediaOpen||!tableMenu||attemptedBookMap)return;
        attemptedBookMap=true;
        var scene=FindFirstObjectByType<TabletopScene>();
        if(scene!=null)bookMap=scene.CaptureEncyclopediaMap();
    }
    Material bookImageMaterial;
    void BookPicture(Rect area,string key)=>BookTexture(area,Art(key));
    void BookTexture(Rect area,Texture image,bool fit=false){
        if(image==null||Event.current.type!=EventType.Repaint)return;
        if(bookImageMaterial==null)bookImageMaterial=new Material(Resources.Load<Shader>("EncyclopediaPaper"));
        if(fit){
            float scale=Mathf.Min(area.width/image.width,area.height/image.height);
            var size=new Vector2(image.width*scale,image.height*scale);
            area=new Rect(area.center-size*.5f,size);
        }
        Graphics.DrawTexture(area,image,bookImageMaterial);
    }
    readonly Dictionary<int,GUIStyle> bookStyles=new Dictionary<int,GUIStyle>();
    GUIStyle BookStyle(int size){
        if(bookStyles.TryGetValue(size,out var style))return style;
        style=new GUIStyle(Type(size));
        var ink=style.normal.textColor;
        if(QualitySettings.activeColorSpace==ColorSpace.Linear)ink=ink.linear;
        style.normal.textColor=style.focused.textColor=ink;
        bookStyles.Add(size,style);
        return style;
    }
    void BookHeading(Rect r,string text,int size)=>BookLabel(r,text,size,bold:true);
    void BookLabel(Rect r,string text,int size,TextAnchor align=TextAnchor.MiddleCenter,bool bold=false){
        using(var pixels=new PastoralMenuUI.PixelText(r,size)){
            var style=BookStyle(pixels.Size);
            style.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
            style.alignment=align;
            GUI.Label(pixels.Area,text,style);
            style.fontStyle=FontStyle.Normal;
            style.alignment=TextAnchor.MiddleCenter;
        }
    }
    void DrawEncyclopedia(){
        DrawTablePauseBackdrop();
        var previous=GUI.matrix;
        var viewport=MobileTabletop.Viewport;
        float scale=Mathf.Min(viewport.Width/1672f,viewport.Height/941f);
        GUI.matrix=Matrix4x4.TRS(new Vector3(viewport.Left+(viewport.Width-1672*scale)/2,viewport.Top+(viewport.Height-941*scale)/2),Quaternion.identity,new Vector3(scale,scale,1));
        try{
            BookPicture(new Rect(0,0,1672,941),"ency-shell");
            BookHeading(new Rect(155,13,337,89),"游戏百科",65);
            if(IsIllustratedEntry(bookEntry)){
                BookPicture(new Rect(916,21,474,73),"ency-detail-path");
                BookLabel(new Rect(930,21,445,68),DetailPath(),26);
            }else{
                var search=new Rect(1002,26,354,59);
                GUI.SetNextControlName("BookSearch");
                string query=GUI.TextField(search,bookQuery,64,BookStyle(28));
                if(query!=bookQuery){bookTrail.Clear();bookQuery=query;bookEntry="";bookSection=-1;encyclopediaScroll=Vector2.zero;}
                if(bookQuery.Length==0&&GUI.GetNameOfFocusedControl()!="BookSearch")BookLabel(search,"搜索词条",29,TextAnchor.MiddleLeft);
            }
            if(Hit(new Rect(1404,24,175,69)))BookBack();
            if(bookEntry.Length==0&&bookSection<0&&bookQuery.Length==0)DrawBookHome();
            else if(IsIllustratedEntry(bookEntry))DrawBookDetail();
            else DrawBookResults();
        }finally{GUI.matrix=previous;}
    }
    void DrawBookHome(){
        var map=new Rect(76,128,994,481);
        BookPicture(map,"ency-map-frame");
        var inner=new Rect(84,139,978,456);
        if(bookMap!=null)BookTexture(inner,bookMap);
        else{
            var art=Art(SeaBookMap?"atlas-"+ScenarioId+"-"+PlayerCount:"map-标准岛屿");
            if(art!=null)BookTexture(inner,art,true);
        }
        BookPicture(new Rect(97,119,241,72),"ency-tab");BookHeading(new Rect(104,121,225,59),"本局地图",36);
        BookPicture(new Rect(415,541,317,64),"ency-map-name");BookHeading(new Rect(437,537,272,69),CurrentBookMap,34);
        if(Hit(map))bookEntry=CurrentBookMap;
        BookPicture(new Rect(1091,129,504,480),"ency-paper");
        BookPicture(new Rect(1091,119,250,72),"ency-tab");BookHeading(new Rect(1102,121,228,59),"本局玩法",36);
        int row=0;
        if(RuleSet=="base")DrawBookModeRow(row++,-1);
        for(int i=0;i<BookModes.Length;i++)if(BookModeActive(i))DrawBookModeRow(row++,i);
        for(int i=0;i<BookSections.Length;i++){
            var r=new Rect(72+i*307,628,305,269);
            BookPicture(r,"ency-section-"+i);
            BookHeading(new Rect(r.x+5,812,r.width-10,72),BookSections[i],39);
            if(Hit(r)){bookSection=i;encyclopediaScroll=Vector2.zero;}
        }
    }
    void DrawBookModeRow(int row,int index){
        var r=new Rect(1105,189+row*80,470,79);
        BookPicture(r,"ency-row");
        if(index==3)DetailArt(new Rect(r.x+20,r.y+3,124,70),"Encyclopedia/模式入口/海港与捕鱼");
        else{
            var icon=Art(index<0?"mode-base":BookModeArt[index]);
            if(icon!=null)BookTexture(new Rect(r.x+20,r.y+3,124,70),icon,true);
        }
        string name=index<0?"基础版":BookModes[index];
        BookHeading(new Rect(r.x+164,r.y+3,250,68),name,36);
        if(Hit(r))bookEntry=name;
    }
    void DrawBookResults(){
        BuildBookIndex();
        BookPicture(new Rect(80,132,1510,753),"ency-paper");
        BookLabel(new Rect(140,149,1370,72),bookEntry.Length>0?bookEntry:bookSection<0?"搜索结果":BookSections[bookSection],39);
        if(bookEntry.Length>0){
            var entry=bookIndex.Find(item=>item.name==bookEntry);
            var style=new GUIStyle(BookStyle(25)){wordWrap=true,alignment=TextAnchor.UpperLeft};
            string body=entry.body??"";
            if(entry.section==1&&entry.name==CurrentBookMap)body+="\n本局胜利目标："+TargetVictoryPoints+" 分。";
            GUI.Label(new Rect(183,257,1290,569),body,style);
            return;
        }
        var area=new Rect(163,241,1340,591);
        bool Match((int section,string name,string body) e)=>(bookSection<0||e.section==bookSection)
            &&(bookQuery.Length==0||e.name.Contains(bookQuery)||(e.body??"").Contains(bookQuery));
        int count=0;
        foreach(var e in bookIndex)if(Match(e))count++;
        encyclopediaScroll=GUI.BeginScrollView(area,encyclopediaScroll,new Rect(0,0,1310,count*59));
        int row=0;
        foreach(var e in bookIndex){
            if(!Match(e))continue;
            var r=new Rect(8,row++*59,1288,56);
            GUI.Label(r,e.name,BookStyle(26));
            PastoralMenuUI.Fill(new Rect(r.x,r.yMax,r.width,1),new Color(.4f,.25f,.1f,.25f));
            if(Hit(r))bookEntry=e.name;
        }
        GUI.EndScrollView();
        if(count==0)BookLabel(area,"未找到相关词条",26);
    }
}
