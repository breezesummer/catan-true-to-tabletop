using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using UnityEngine;
using Catan.Core.M5;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyTileGlow(string[] args)
    {
        mode = "combined";
        string path = args[Array.IndexOf(args,"--tile-glow") + 1];
        var save = JsonUtility.FromJson<M6App.SavedGame>(File.ReadAllText(path));
        var full = CombinedGameSession.Load(save.Authority);
        bool danger = args.Contains("--danger-hex");
        var history = full.GetHistory(save.HumanSeat);
        var target = danger ? history.Last(e => e.Kind == "MoveRobber") : history.Last(e => e.OwnMultiHarvestTiles().Any());
        GameState state;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(save.Authority)))
            state = (GameState)new DataContractJsonSerializer(typeof(GameState)).ReadObject(stream);
        var replay = CombinedGameSession.Create(state.ScenarioId,4,state.InitialSeed,state.TargetVictoryPoints,true,
            threeKingdomsPackageVersion:state.ThreeKingdoms.PackageVersion,
            tradeModules:new Catan.Core.TradeModules.TradeModuleOptions { Harbormaster=true, Fishing=true });
        int index = (int)target.Sequence - 1;
        for (int i=0;i<index;i++)
        {
            Require(replay.Execute(state.ProcessedCommands[i].Command).Success,"验收局指令重放失败");
            if (i % 30 == 0) yield return null;
        }
        app.StartGame(mode,state.ScenarioId,4,save.HumanSeat,true,replay.Save(),threeKingdoms:true);
        var client = GetComponent<M5App>();
        yield return null; yield return null;
        Require(app.ScenarioId=="through-the-desert" && app.ThreeKingdoms && app.Harbormaster && app.Fishing,"验收模式不符");
        while (!Table.Ocean.MaskReady || !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
        yield return new WaitForSeconds(1);
        if (danger)
        {
            var run = VerifyDangerHex(client,state.ProcessedCommands[index].Command);
            while (run.MoveNext()) yield return run.Current;
            yield break;
        }
        var glow = Hud.TileGlow;
        string tile = target.OwnMultiHarvestTiles().First();
        Debug.Log("CATAN_TILE_BASELINE seat="+save.HumanSeat+" tile="+tile+" owned="+string.Join(",",glow.Owned)+" last="+glow.LastGoldTile);
        Table.Frame(BoardCamera,false,1);
        yield return new WaitForEndOfFrame(); SaveOceanFrame("01-常驻玉光.png");
        Require(glow.Owned.Contains(tile) && glow.LastGoldTile == null,"读档后地块归属或历史基线错误");
        bool verifyMarks = args.Contains("--map-marks");
        if (verifyMarks)
        {
            var button = M6App.MapMarksButton;
            Require(button.x == M6App.ReactionButton.x && button.yMax < M6App.ReactionButton.y && app.ReactionOwnsPointer(button.center),"显隐按钮位置或命中错误");
            var before = Authority;
            Dock.Fishing.Show();
            yield return null;
            Require(!Dock.Fishing.Contains(button.center), "鱼资源操作挡住显隐按钮");
            app.ReactionClick(M6App.ReactionButton.center, 0);
            Require(app.ReactionMenu && !button.Overlaps(M6App.EggOption) && !button.Overlaps(M6App.FlowerOption),"互动道具遮住显隐按钮");
            yield return new WaitForEndOfFrame(); SaveOceanFrame("捕鱼与互动.png");
            app.ReactionClick(button.center, 0);
            Dock.Fishing.Hide();
            yield return null;
            CheckMapMarks(false);
            Require(!app.ReactionMenu && before == Authority, "显隐改变对局或互动选项未收起");
            yield return new WaitForEndOfFrame(); SaveOceanFrame("02-隐藏.png");
        }
        bool film = args.Contains("--tile-film");
        if (film)
        {
            var center = (Vector2)BoardCamera.WorldToScreenPoint(Position(tile) + Vector3.up * EntityBoardAssets.BoardSurfaceHeight);
            Table.PanFromScreen(center, BoardCamera.pixelRect.center); Table.EndPan();
            ScrollBoard(7,false,BoardCamera.pixelRect.center);
            yield return null; yield return new WaitForEndOfFrame();
        }
        var command = state.ProcessedCommands[index].Command;
        var result = client.VerificationHost.Submit(command);
        Require(result.Success && !result.IsDuplicate,"收取指令失败");
        client.M6Refresh();
        if (film)
        {
            var center = BoardCamera.WorldToScreenPoint(Position(tile) + Vector3.up * EntityBoardAssets.BoardSurfaceHeight);
            var frame = new Texture2D(540,336,TextureFormat.RGB24,false);
            var rect = new Rect(Mathf.Clamp(Mathf.Round(center.x-270),0,Screen.width-540),
                Mathf.Clamp(Mathf.Round(center.y-168),0,Screen.height-336),540,336);
            double begin = Time.realtimeSinceStartupAsDouble;
            int indexInFilm = 0;
            try
            {
                while (Time.realtimeSinceStartupAsDouble-begin < 3.6)
                {
                    yield return new WaitForEndOfFrame();
                    int ms = (int)((Time.realtimeSinceStartupAsDouble-begin)*1000);
                    if (ms >= indexInFilm * 65)
                    {
                        RenderTexture.active=null;
                        frame.ReadPixels(rect,0,0,false); frame.Apply(false);
                        File.WriteAllBytes(Path.Combine(output,"frame-"+ms.ToString("D5")+".png"),frame.EncodeToPNG());
                        indexInFilm = ms/65 + 1;
                    }
                    yield return null;
                }
            }
            finally { Destroy(frame); }
            Require(target.OwnMultiHarvestTiles().Contains(glow.LastGoldTile),"录像未捕捉到实际收取");
            configurations++; yield break;
        }
        double until = Time.realtimeSinceStartupAsDouble + 15;
        while ((glow.LastGoldTile == null || Time.time < glow.LastGoldAt + .26f) && Time.realtimeSinceStartupAsDouble < until)
            yield return null;
        Require(target.OwnMultiHarvestTiles().Contains(glow.LastGoldTile),"本人的结算未点亮目标地块");
        if (verifyMarks)
        {
            CheckMapMarks(false);
            yield return new WaitForEndOfFrame(); SaveOceanFrame("隐藏期间收牌.png");
            yield return new WaitForSeconds(1.8f);
            var before = Authority;
            app.ReactionClick(M6App.MapMarksButton.center, 0);
            yield return null;
            CheckMapMarks(true);
            Require(before == Authority && Time.time - glow.LastGoldAt > 1.65f,"恢复显示时重播金纹或改变了对局");
            yield return new WaitForEndOfFrame(); SaveOceanFrame("03-恢复显示.png");
            configurations++; yield break;
        }
        yield return new WaitForEndOfFrame(); SaveOceanFrame("02-收取金纹.png");
        var id = glow.LastGoldTile;
        FocusDetail(Position(id),false);
        yield return null; yield return new WaitForEndOfFrame(); SaveOceanFrame("03-金纹近景.png");
        yield return new WaitForSeconds(1.8f);
        yield return new WaitForEndOfFrame(); SaveOceanFrame("04-退回玉光.png");
        float last = glow.LastGoldAt;
        Require(client.VerificationHost.Submit(command).IsDuplicate,"重复命令未去重");
        client.M6Refresh(); yield return null; yield return null;
        Require(glow.LastGoldAt == last,"重复命令重播金纹");
        Debug.Log("CATAN_TILE_GLOW_OK turn="+target.Turn+" seat="+save.HumanSeat+" tile="+id+" tiles="+glow.Owned.Count()+" seq="+target.Sequence);
        configurations++;
    }

    IEnumerator VerifyDangerHex(M5App client, Command move)
    {
        var board = client.VerificationBoard;
        var view = client.M6View.Board;
        var actualPirate = view.PirateTileId;
        Require(actualPirate != null && actualPirate != "frame", "实局需要有海盗海格");
        var surface = Table.Ocean.GetComponentsInChildren<Renderer>().Select(r=>r.sharedMaterial)
            .First(m=>m.shader.name=="Catan/ContinuousOcean");
        var terrain = board.transform.Find(move.TargetId).GetComponentInChildren<Renderer>().sharedMaterial;
        Vector4 Hex(string id)
        {
            if (id==null || id=="frame") return Vector4.zero;
            var p=board.Position(id); return new Vector4(p.x,p.z,1,0);
        }
        Require(surface.GetVector(PaintedMap.PirateHex)==Hex(actualPirate) &&
            terrain.GetVector(PaintedMap.RobberHex)==Hex(view.RobberTileId),"读档的危险格不匹配");
        Require(view.RobberTileId!=move.TargetId && client.VerificationHost.Submit(move).Success,"强盗移动未通过规则确认");
        client.M6Refresh();
        yield return null; yield return null;
        Require(terrain.GetVector(PaintedMap.RobberHex)==Hex(move.TargetId),"强盗移动后危险格未更新");
        Table.Frame(BoardCamera,false,1);
        yield return new WaitForEndOfFrame(); SaveOceanFrame("陆地与海洋-全盘.png");
        app.ReactionClick(M6App.MapMarksButton.center,0);
        yield return null; yield return new WaitForEndOfFrame();
        Require(!TabletopMapMarks.Visible,"未隐藏危险格");
        SaveOceanFrame("隐藏危险光.png");
        app.ReactionClick(M6App.MapMarksButton.center,0);
        foreach(var id in new[]{move.TargetId,actualPirate})
        {
            FocusDetail(Position(id),false);
            ScrollBoard(-6,false,BoardCamera.pixelRect.center);
            yield return null; yield return new WaitForEndOfFrame();
            SaveOceanFrame(id==actualPirate ? "海盗海格.png" : "强盗地块.png");
        }
        var another = view.Tiles.First(t=>t.Resource=="sea" && t.Id!=actualPirate);
        foreach(var id in new[]{another.Id,"frame",null,actualPirate})
        {
            board.RefreshSeafarers(view.Ships,id);
            Require(surface.GetVector(PaintedMap.PirateHex)==Hex(id) && terrain.GetVector(PaintedMap.PirateHex)==Hex(id),
                "海盗移格或离场后的危险光位置错误");
        }
        Debug.Log("CATAN_DANGER_HEX_OK robber="+move.TargetId+" pirate="+actualPirate);
        configurations++;
    }

    void CheckMapMarks(bool visible)
    {
        var slips = GetComponent<M5App>().VerificationBoard.GetComponentsInChildren<JadeNumberSlip>();
        foreach (var prefix in new[] { "点数 ", "湖泊点数 ", "渔港点数 " })
            Require(slips.Any(s => s.name.StartsWith(prefix,StringComparison.Ordinal)), "缺少验收玉签 "+prefix);
        var ports = slips.Where(s => s.GetComponent<HarborSign>() != null).ToArray();
        Require(ports.Length > 0 && ports.All(s => s.GetComponent<Renderer>().enabled), "港口贸易签不可见");
        Require(slips.Except(ports).All(s => s.GetComponent<Renderer>().enabled == visible), "点数玉签显示状态不一致");
        Require(TabletopMapMarks.Visible == visible && Shader.GetGlobalFloat("_CatanMapMarksHidden") == (visible ? 0 : 1),"地块光效显示状态不一致");
    }
}
