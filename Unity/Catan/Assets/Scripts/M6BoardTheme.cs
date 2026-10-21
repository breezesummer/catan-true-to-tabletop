using System;
using UnityEngine;

public sealed partial class M6App
{
    BoardThemeDefinition requestedTheme;
    int themeUnlockFrame;
    internal bool VerificationFailThemeLoad;
    internal long ThemeSwitchResidentBytes;
    public void RequestBoardTheme(string id)
    {
        var theme=BoardThemeDefinition.Find(id);
        if(theme==null){BoardThemeController.Status="无法识别这个主题，当前主题保留。";return;}
        if(BoardThemeController.Switching)return;
        requestedTheme=theme;
    }
    void UpdateBoardTheme()
    {
        if(BoardThemeController.Switching&&Time.frameCount>themeUnlockFrame)BoardThemeController.Switching=false;
        if(requestedTheme==null||BoardThemeController.Switching)return;
        var target=requestedTheme;requestedTheme=null;
        if(target==BoardThemeController.Current)return;
        var original=BoardThemeController.Current;
        var originalTable=ThemeTable();
        var view=originalTable!=null?(TabletopScene.ViewState?)originalTable.CaptureView():null;
        BoardThemeController.Switching=true;themeUnlockFrame=Time.frameCount+1;
        try
        {
            // 同步事务在 Update 中执行，不在 IMGUI 重入中销毁物件。
            // 现有卡牌队列、网络回调保留，后续仍消费最新获准投影。
            ResetBuildPresentation();
            BoardThemeController.Select(target);
            RebuildThemeBoard();
            ThemeSwitchResidentBytes=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            if(VerificationFailThemeLoad){VerificationFailThemeLoad=false;throw new InvalidOperationException("验证注入");}
            BoardThemeController.Persist();
            BoardThemeController.Status="已切换为「"+target.Name+"」。"+(target==BoardThemeDefinition.Ultra?"当前为开发占位模型。":"");
        }
        catch(Exception ex)
        {
            BoardThemeController.Select(original);
            bool recovered=true;
            try {ResetBuildPresentation();RebuildThemeBoard();}
            catch(Exception recovery){recovered=false;Debug.LogError("CATAN_THEME_RECOVERY_FAILED: "+recovery.GetType().Name);}
            BoardThemeController.Status=recovered?"主题加载失败，已恢复「"+original.Name+"」。":"主题恢复失败，请返回主菜单重新进入对局。";
            Debug.LogWarning("CATAN_THEME_LOAD_RECOVERED: "+ex.GetType().Name);
        }
        if(view.HasValue)ThemeTable()?.RestoreView(view.Value);
        if(MenuVisible)visible?.Invoke(false);
        nextAction=Time.unscaledTime+(float)TabletopAiSpeed.Interval(AiSpeedLevel);
    }
    void RebuildThemeBoard()
    {
        if(!Ready)return;
        if(presentation is M2App basic)basic.RebuildTheme();
        else if(presentation is M3App sailing)sailing.RebuildTheme();
        else if(presentation is M4App cities)cities.RebuildTheme();
        else if(presentation is M5App combined)combined.RebuildTheme();
    }
    TabletopScene ThemeTable()=>presentation is M2App b?b.VerificationBoard?.TableScene:
        presentation is M3App s?s.VerificationBoard?.TableScene:presentation is M4App c?c.VerificationBoard?.TableScene:
        presentation is M5App x?x.VerificationBoard?.TableScene:null;
    void DrawBoardThemeSettings(Rect area)
    {
        PastoralMenuUI.Label(new Rect(area.x,area.y,area.width,32),"棋盘主题",22,PastoralMenuUI.Ink,true);
        for(int i=0;i<2;i++)
        {
            var theme=BoardThemeDefinition.All[i];var r=new Rect(area.x+i*(area.width/2+8),area.y+44,area.width/2-8,136);
            if(PastoralMenuUI.Card(r,theme.Name,i==0?"实体桌游组件":"开发占位模型",BoardThemeController.Current==theme)&&!BoardThemeController.Switching)RequestBoardTheme(theme.Id);
            // 简明示意预览，正式资产样板在 U2 替换。
            var preview=new Rect(r.x+20,r.y+93,r.width-40,28);
            PastoralMenuUI.Round(preview,i==0?new Color(.34f,.25f,.17f):theme.TableColor,5);
            for(int j=0;j<4;j++)PastoralMenuUI.Round(new Rect(preview.x+15+j*(preview.width-25)/4,preview.y+5,23,18),i==0?new Color(.63f,.56f,.39f):new Color(.28f+j*.12f,.48f,.25f),i==0?8:1);
        }
        PastoralMenuUI.Label(new Rect(area.x,area.y+191,area.width,44),BoardThemeController.Status.Length>0?BoardThemeController.Status:BoardThemeController.Current.Description,13,PastoralMenuUI.Muted);
        PastoralMenuUI.Label(new Rect(area.x,area.y+252,area.width,28),"界面配色",16,PastoralMenuUI.Ink,true);
        int chosen=PastoralMenuUI.Segments(new Rect(area.x,area.y+291,area.width,40),StrategyHudTheme.Selected,StrategyHudTheme.Names);
        if(chosen!=StrategyHudTheme.Selected)StrategyHudTheme.Select(chosen);
        PastoralMenuUI.Label(new Rect(area.x,area.y+350,area.width,30),"偏好仅保存于本机；联机玩家可使用不同棋盘主题。",13,PastoralMenuUI.Muted);
    }
}
