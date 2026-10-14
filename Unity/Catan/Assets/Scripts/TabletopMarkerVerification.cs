using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Catan.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 只在显式图形批处理参数启用。所有夹具是公开几何与公开计数；报告不写手牌或权威存档。
public sealed class TabletopMarkerVerification : MonoBehaviour
{
    const string AssetRoot = "EntityAssets/v005/tabletop-markers/";
    const int DetailLayer = 29;
    static readonly string[] AssetIds = { "port-generic", "port-wood", "port-brick", "port-wool", "port-wheat", "port-ore", "catan-victory-marker", "number-token-back" };
    M6App app;
    string mode, output;
    int checks, sourceInstances, cameraImages, detailImages;
    int specialGiftPorts, specialBonusMarkers;
    readonly List<ModeResult> modes = new List<ModeResult>();

    [Serializable] sealed class ModeResult
    {
        public string mode;
        public int ports, gifts, unclaimedVictoryMarkers, awardedVictoryMarkers, numberTokens;
    }
    [Serializable] sealed class Result
    {
        public string status, unity, note;
        public int checks, checkedSourceInstances, cameraImages, detailImages;
        public int specialGiftPorts, specialBonusMarkers;
        public bool publicDataOnly, visualsHaveNoColliders, stableIds, outsideCoast, hiddenNumbersSuppressed, rulesSupplyNumberFaces, photographedNumberBack, fullUiPixelsVerified;
        public bool publicSpecialScenario, publicFogScenario;
        public string[] assets;
        public ModeResult[] modes;
    }

    Transform Board => mode == "base" ? GetComponent<M2App>().VerificationBoard.transform :
        mode == "seafarers" ? GetComponent<M3App>().VerificationBoard.transform :
        mode == "cities-knights" ? GetComponent<M4App>().VerificationBoard.transform : GetComponent<M5App>().VerificationBoard.transform;
    TabletopReferenceMarkers Markers => Board.GetComponentInChildren<TabletopReferenceMarkers>(true);
    TabletopScene Table => mode == "base" ? GetComponent<M2App>().VerificationBoard.TableScene :
        mode == "seafarers" ? GetComponent<M3App>().VerificationBoard.TableScene :
        mode == "cities-knights" ? GetComponent<M4App>().VerificationBoard.TableScene : GetComponent<M5App>().VerificationBoard.TableScene;
    OperationSnapshot Snapshot => mode == "base" ? GetComponent<M2App>().VerificationOperationSnapshot :
        mode == "seafarers" ? GetComponent<M3App>().VerificationOperationSnapshot :
        mode == "cities-knights" ? GetComponent<M4App>().VerificationOperationSnapshot : GetComponent<M5App>().VerificationOperationSnapshot;
    Tile[] Tiles => mode == "base" ? GetComponent<M2App>().M6View.Board.Tiles :
        mode == "seafarers" ? GetComponent<M3App>().M6View.Board.Tiles :
        mode == "cities-knights" ? GetComponent<M4App>().M6View.Board.Tiles : GetComponent<M5App>().M6View.Board.Tiles;
    Edge[] Edges => mode == "base" ? GetComponent<M2App>().M6View.Board.Edges :
        mode == "seafarers" ? GetComponent<M3App>().M6View.Board.Edges :
        mode == "cities-knights" ? GetComponent<M4App>().M6View.Board.Edges : GetComponent<M5App>().M6View.Board.Edges;
    PiecePlacement[] Buildings => mode == "base" ? GetComponent<M2App>().M6View.Board.Settlements.Concat(GetComponent<M2App>().M6View.Board.Cities).ToArray() :
        mode == "seafarers" ? GetComponent<M3App>().M6View.Board.Settlements.Concat(GetComponent<M3App>().M6View.Board.Cities).ToArray() :
        mode == "cities-knights" ? GetComponent<M4App>().M6View.Board.Settlements.Concat(GetComponent<M4App>().M6View.Board.Cities).ToArray() :
        GetComponent<M5App>().M6View.Board.Settlements.Concat(GetComponent<M5App>().M6View.Board.Cities).ToArray();
    TabletopReferenceMarkers.PortDisplay[] Ports
    {
        get
        {
            if (mode == "base") return GetComponent<M2App>().M6View.Board.Ports.Select(p => Port(p.Id, p.Vertices, p.Resource)).ToArray();
            if (mode == "seafarers") return GetComponent<M3App>().M6View.Board.Ports.Select(p => Port(p.Id, p.Vertices, p.Resource, p.EdgeId)).ToArray();
            if (mode == "cities-knights") return GetComponent<M4App>().M6View.Board.Ports.Select(p => Port(p.Id, p.Vertices, p.Resource)).ToArray();
            return GetComponent<M5App>().M6View.Board.Ports.Select(p => Port(p.Id, p.Vertices, p.Resource)).ToArray();
        }
    }
    static TabletopReferenceMarkers.PortDisplay Port(string id, string[] vertices, Resource? resource, string edge = null)
        => new TabletopReferenceMarkers.PortDisplay { Id = id, Vertices = vertices, Resource = resource, EdgeId = edge };

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs(); int flag = Array.IndexOf(args, "--tabletop-markers-verify");
        if (flag < 0) yield break;
        if (flag + 1 >= args.Length || !Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        { Debug.LogError("CATAN_TABLETOP_MARKERS_FAILED: 必须使用图形批处理并指定输出目录。"); Application.Quit(1); yield break; }
        output = Path.GetFullPath(args[flag + 1]); Directory.CreateDirectory(output); app = GetComponent<M6App>();
        var run = Verify();
        while (true)
        {
            object next;
            try { if (!run.MoveNext()) break; next = run.Current; }
            catch (Exception error)
            {
                string reason = error is InvalidOperationException ? error.Message : error.GetType().Name;
                Debug.LogError("CATAN_TABLETOP_MARKERS_FAILED: " + mode + " / " + reason); Application.Quit(1); yield break;
            }
            yield return next;
        }
        File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result {
            status = "passed", unity = Application.unityVersion, checks = checks, checkedSourceInstances = sourceInstances,
            cameraImages = cameraImages, detailImages = detailImages, assets = AssetIds, modes = modes.ToArray(),
            specialGiftPorts = specialGiftPorts, specialBonusMarkers = specialBonusMarkers, publicSpecialScenario = true, publicFogScenario = true,
            publicDataOnly = true, visualsHaveNoColliders = true, stableIds = true, outsideCoast = true,
            hiddenNumbersSuppressed = true, rulesSupplyNumberFaces = true, photographedNumberBack = true, fullUiPixelsVerified = false,
            note = "四种真实 App 正常开局后检查公开地图；补查被遗忘的部落的公开港礼/航路奖励和迷雾岛屿的隐藏数字。独立公开夹具检查迁移、增删及奖励计数。相机图不含 IMGUI；8 张细节图各含顶视、侧视、游戏角度及翻面。未把程序截图计为用户外观确认。"
        }, true));
        Debug.Log("CATAN_TABLETOP_MARKERS_OK: checks=" + checks + " cameraImages=" + cameraImages + " detailImages=" + detailImages);
        Application.Quit(0);
    }

    IEnumerator Verify()
    {
        CheckPrefabs();
        foreach (string rules in new[] { "base", "seafarers", "cities-knights", "combined" })
        {
            mode = rules;
            app.StartGame(mode, "heading-for-new-shores", 4, "P1", true, FixedAuthority());
            yield return null; yield return null;
            Require(app.Ready && Markers != null, "真实 App 未创建实体港口组件");
            int steps = 0;
            while (!Snapshot.CanTrade && steps++ < 120) { Advance(); yield return null; }
            Require(Snapshot.CanTrade && Buildings.Length == 8, "未正常推进至完成初始建筑的行动阶段");
            var publicPorts = Ports;
            Require(Markers.PlacedPortCount == publicPorts.Length, "真实 App 实体港口数量未同步公开地图");
            Require(Markers.GetComponentsInChildren<Collider>(true).Length == 0, "实体标记出现交互 Collider");
            foreach (var port in publicPorts)
            {
                var obj = Markers.PortObject(port.Id); Require(obj != null && obj.activeInHierarchy, "公开港口缺少显示对象");
                Source(obj.transform, PortAsset(port.Resource)); CheckCoast(port, obj.transform); CheckBuildings(obj.transform);
            }
            CheckNormalNumbers();
            CheckPublicCounters();
            var before = MarkerInstances(); RefreshRealView();
            Require(before.SequenceEqual(MarkerInstances()), "未改变的真实公开视图反复创建标记实例");
            foreach (bool top in new[] { false, true })
            {
                SetTop(top); yield return null; yield return null;
                CaptureBoard(mode + (top ? "-top.png" : "-game-camera.png"));
            }
            SetTop(false);
            if (mode == "base") { VerifyPublicFixture(); RefreshRealView(); yield return null; yield return null; }
        }
        mode = "seafarers";
        app.StartGame(mode, "forgotten-tribe", 4, "P1", true, Catan.Core.M3.SeafarersGameSession.Create("forgotten-tribe", 4, 20261014).Save());
        yield return null; yield return null;
        var special = GetComponent<M3App>().M6View;
        specialGiftPorts = special.Board.GiftPorts.Length; specialBonusMarkers = special.Board.BonusEdgeIds.Length;
        Require(specialGiftPorts > 0 && specialBonusMarkers > 0 && Markers.GiftPortCount == specialGiftPorts && Markers.UnclaimedVictoryMarkerCount == specialBonusMarkers,
            "真实部落剧本没有接入公开港礼和未领取奖励");
        foreach (var gift in special.Board.GiftPorts) Source(Markers.PortObject(gift.Id).transform, PortAsset(gift.Resource));
        foreach (Transform marker in Markers.transform.Find("航路奖励")) Source(marker, "catan-victory-marker");
        var specialInstances = MarkerInstances(); RefreshRealView(); Require(specialInstances.SequenceEqual(MarkerInstances()), "真实部落剧本稳定 ID 发生变化");
        app.StartGame(mode, "fog-islands", 4, "P1", true, Catan.Core.M3.SeafarersGameSession.Create("fog-islands", 4, 20261014).Save());
        yield return null; yield return null;
        Require(Tiles.Any(t => t.Resource == "fog"), "真实迷雾剧本夹具未包含隐藏占位格"); CheckNormalNumbers();
        var details = CaptureDetails(); while (details.MoveNext()) yield return details.Current;
    }

    void CheckPrefabs()
    {
        foreach (string id in AssetIds)
        {
            var source = Resources.Load<GameObject>(AssetRoot + id);
            Require(source != null && source.GetComponentsInChildren<MeshFilter>(true).Any(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 3), "实体资产缺少真实网格 " + id);
            Require(source.GetComponentsInChildren<Collider>(true).Length == 0, "实体资产源包含 Collider " + id);
            var materials = source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
            var texture = Resources.Load<Texture2D>(TextureResource(id));
            Require(texture != null && texture.width >= 256 && texture.height >= 256, "实体原照贴图没有导入 " + id);
            Require(materials.Any(m => m != null && m.mainTexture == texture), "实体资产材质未绑定对应原照贴图 " + id);
        }
    }
    static string TextureResource(string id) => AssetRoot + id + (id.StartsWith("port-", StringComparison.Ordinal) ? "-front" : "");
    static string PortAsset(Resource? resource) => resource.HasValue ? "port-" + resource.Value.ToString().ToLowerInvariant() : "port-generic";
    void Source(Transform instance, string id)
    {
        var source = Resources.Load<GameObject>(AssetRoot + id); Require(source != null && instance != null, "缺少预期实体资产 " + id);
        var meshes = instance.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh).ToArray();
        Require(source.GetComponentsInChildren<MeshFilter>(true).All(m => meshes.Contains(m.sharedMesh)), "实体标记网格来源错误 " + id);
        var instanceMaterials = instance.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
        Require(source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).All(instanceMaterials.Contains), "实体标记材质来源错误 " + id);
        Require(instanceMaterials.Any(m => m != null && m.mainTexture == Resources.Load<Texture2D>(TextureResource(id))), "实体标记纹理来源错误 " + id);
        Require(instance.localScale == Vector3.one && instance.GetComponentsInChildren<Collider>(true).Length == 0, "实体标记没有保留米制或阻挡拾取 " + id); sourceInstances++;
    }
    void CheckCoast(TabletopReferenceMarkers.PortDisplay port, Transform instance)
    {
        Require(port.Vertices != null && port.Vertices.Length == 2, "港口缺少公开海岸顶点");
        var adjacent = Tiles.Where(t => port.Vertices.All(t.Vertices.Contains) && !IsWater(t.Resource)).ToArray();
        Require(adjacent.Length > 0, "公开港口没有相邻陆地");
        var edge = (Position(port.Vertices[0]) + Position(port.Vertices[1])) * .5f;
        var land = adjacent.Select(t => Position(t.Id)).Aggregate(Vector3.zero, (sum, p) => sum + p) / adjacent.Length;
        var outward = edge - land; outward.y = 0; var displacement = instance.position - edge; displacement.y = 0;
        Require(Vector3.Dot(displacement, outward.normalized) > .002f, "实体港口没有放在海岸外侧");
    }
    void CheckBuildings(Transform port)
    {
        var portBounds = BoundsOf(port);
        var pieces = Board.Find("Committed pieces"); Require(pieces != null, "真实棋盘缺少已放棋子");
        foreach (var building in Buildings)
        {
            var obj = pieces.Cast<Transform>().FirstOrDefault(t => t.name == building.LocationId || t.name.StartsWith(building.LocationId + " [", StringComparison.Ordinal) ||
                t.name == "City " + building.LocationId || t.name.StartsWith("City " + building.LocationId + " [", StringComparison.Ordinal));
            Require(obj != null, "公开建筑没有显示对象");
            var bounds = BoundsOf(obj);
            bool overlaps = portBounds.min.x < bounds.max.x - .0001f && portBounds.max.x > bounds.min.x + .0001f &&
                portBounds.min.z < bounds.max.z - .0001f && portBounds.max.z > bounds.min.z + .0001f;
            Require(!overlaps || !FootprintIntersects(port, obj), "港口实体占地与已放建筑重合");
        }
    }
    static bool IsWater(string resource) => resource == "sea" || resource == "water" || resource == "fog" || resource == "hidden";
    static bool ShowsNumber(Tile tile) => tile.Number.HasValue && tile.Number.Value >= 2 && tile.Number.Value <= 12 && tile.Number.Value != 7 && !IsWater(tile.Resource) && tile.Resource != "desert";
    void CheckNormalNumbers()
    {
        var tokens = Board.GetComponentInChildren<TabletopNumberTokens>(true); Require(tokens != null && tokens.gameObject.activeInHierarchy, "正常游戏点数牌未显示");
        var active = tokens.transform.Cast<Transform>().Where(t => t.gameObject.activeInHierarchy).ToArray();
        Require(active.Length == Tiles.Count(ShowsNumber), "正常公开地块与实体数字数量不一致");
        foreach (var tile in Tiles.Where(ShowsNumber))
        {
            var obj = active.SingleOrDefault(t => t.name == "点数 " + tile.Number + " · " + tile.Id);
            Require(obj != null, "正常点数牌没有使用规则公开数字");
            var mesh = obj.GetComponent<MeshFilter>().sharedMesh; var materials = obj.GetComponent<MeshRenderer>().sharedMaterials;
            Require(mesh.subMeshCount == 3 && materials.Length == 3, "正常点数牌缺少独立翻面子网格");
            Require(materials[0].mainTexture == Resources.Load<Texture2D>("TabletopUI/v008/NumberTokens/number-" + tile.Number), "正常正面没有使用原规则数字贴图");
            Require(materials[2].mainTexture == Resources.Load<Texture2D>(AssetRoot + "number-token-back"), "正常背面没有使用实体照片");
            Require(obj.GetComponentsInChildren<Collider>(true).Length == 0, "点数牌阻挡地图拾取");
        }
    }
    void CheckPublicCounters()
    {
        int gifts = 0, unclaimed = 0, awarded = 0;
        if (mode == "seafarers")
        {
            var view = GetComponent<M3App>().M6View; gifts = view.Board.GiftPorts.Length; unclaimed = view.Board.BonusEdgeIds.Length; awarded = view.Players.Sum(p => p.BonusVictoryPoints);
        }
        else if (mode == "combined") awarded = GetComponent<M5App>().M6View.Players.Sum(p => p.BonusVictoryPoints);
        Require(Markers.GiftPortCount == gifts && Markers.UnclaimedVictoryMarkerCount == unclaimed && Markers.AwardedVictoryMarkerCount == awarded, "公开奖励筹码数量未按公开计数显示");
        foreach (var marker in Markers.transform.Find("航路奖励").Cast<Transform>().Concat(Markers.transform.Find("公开奖励").Cast<Transform>()).Where(t => t.gameObject.activeInHierarchy)) Source(marker, "catan-victory-marker");
        modes.Add(new ModeResult { mode = mode, ports = Markers.PlacedPortCount, gifts = gifts, unclaimedVictoryMarkers = unclaimed, awardedVictoryMarkers = awarded, numberTokens = Tiles.Count(ShowsNumber) });
    }

    void VerifyPublicFixture()
    {
        var original = Ports; Require(original.Length >= 7, "公共港口夹具缺少七个公开锚点");
        var fixture = Enumerable.Range(0, 6).Select(i => Port("public-port-" + i, original[i].Vertices, i == 0 ? (Resource?)null : (Resource)(i - 1))).ToArray();
        var noPorts = new TabletopReferenceMarkers.PortDisplay[0]; var noScores = new TabletopReferenceMarkers.ScoreDisplay[0];
        Markers.Refresh(Tiles, fixture, noPorts, new string[0], noScores, "P1", Table);
        Require(Markers.PlacedPortCount == 6, "六种公共夹具港口未生成");
        foreach (var port in fixture) Source(Markers.PortObject(port.Id).transform, PortAsset(port.Resource));
        var instances = MarkerInstances(); Markers.Refresh(Tiles, fixture, noPorts, new string[0], noScores, "P1", Table);
        Require(instances.SequenceEqual(MarkerInstances()), "不变公开 ID 重新创建了实体港口");
        var retained = Markers.PortObject(fixture[0].Id); var oldPosition = retained.transform.position;
        fixture[0] = Port(fixture[0].Id, original[6].Vertices, null);
        Markers.Refresh(Tiles, fixture, noPorts, new string[0], noScores, "P1", Table);
        Require(Markers.PortObject(fixture[0].Id) == retained && (retained.transform.position - oldPosition).magnitude > .01f, "可迁移港口没有用稳定 ID 更新公开锚点");
        fixture[0] = Port(fixture[0].Id, original[6].Vertices, Resource.Ore);
        Markers.Refresh(Tiles, fixture, noPorts, new string[0], noScores, "P1", Table);
        Require(Markers.PortObject(fixture[0].Id) != retained && !retained.activeSelf, "港口类型变化未换图或旧对象仍可见");
        Source(Markers.PortObject(fixture[0].Id).transform, "port-ore");
        var removed = Markers.PortObject(fixture[1].Id); fixture = fixture.Where(p => p.Id != "public-port-1").ToArray();
        var gift = Port("public-gift", original[1].Vertices, Resource.Wood, Edges.First(e => original[1].Vertices.All(e.Vertices.Contains)).Id);
        var bonuses = new[] { Edges[0].Id, Edges[1].Id };
        var scores = new[] { new TabletopReferenceMarkers.ScoreDisplay { PlayerId = "P1", Points = 2 }, new TabletopReferenceMarkers.ScoreDisplay { PlayerId = "P2", Points = 3 } };
        Markers.Refresh(Tiles, fixture, new[] { gift }, bonuses, scores, "P1", Table);
        Require(!Markers.HasPort("public-port-1") && !removed.activeSelf && Markers.PlacedPortCount == 5, "移除公共港口没有立即隐藏旧对象");
        Require(Markers.GiftPortCount == 1 && Markers.UnclaimedVictoryMarkerCount == 2 && Markers.AwardedVictoryMarkerCount == 5, "独立公开奖励计数错误");
        Source(Markers.PortObject(gift.Id).transform, "port-wood");
        foreach (var marker in Markers.transform.Find("航路奖励").Cast<Transform>().Concat(Markers.transform.Find("公开奖励").Cast<Transform>()).Where(t => t.gameObject.activeInHierarchy)) Source(marker, "catan-victory-marker");
        instances = MarkerInstances(); Markers.Refresh(Tiles, fixture, new[] { gift }, bonuses, scores, "P1", Table);
        Require(instances.SequenceEqual(MarkerInstances()), "公开港礼或奖励反复创建实例");
        Require(Markers.GetComponentsInChildren<Collider>(true).Length == 0, "公共夹具标记阻挡棋盘拾取");
        Markers.Refresh(Tiles, noPorts, noPorts, new string[0], noScores, "P1", Table);
        Require(Markers.PlacedPortCount == 0 && Markers.GiftPortCount == 0 && Markers.UnclaimedVictoryMarkerCount == 0 && Markers.AwardedVictoryMarkerCount == 0 &&
            Markers.GetComponentsInChildren<Renderer>().All(r => !r.gameObject.activeInHierarchy), "删除奖励后存在幽灵筹码");
        CheckHiddenNumbers();
    }
    void CheckHiddenNumbers()
    {
        var root = new GameObject("公开隐藏数字夹具");
        try
        {
            var publicTiles = new[] { new Tile { Id = "visible", Resource = "wood", Number = 6 }, new Tile { Id = "fog", Resource = "fog", Number = 8 },
                new Tile { Id = "hidden", Resource = "hidden", Number = 5 }, new Tile { Id = "desert", Resource = "desert", Number = 3 }, new Tile { Id = "sea", Resource = "sea", Number = 4 } };
            var positions = publicTiles.Select((t, i) => new { t.Id, Position = new Vector3(i * .05f, 0, 0) }).ToDictionary(t => t.Id, t => t.Position);
            var tokens = TabletopNumberTokens.Create(root.transform, publicTiles, positions); tokens.gameObject.SetActive(true);
            Require(tokens.transform.Cast<Transform>().Count(t => t.gameObject.activeSelf) == 1 && tokens.transform.Find("点数 6 · visible") != null, "未揭示或非产出地块显露数字");
            publicTiles[1].Resource = "wool"; tokens.Refresh(publicTiles);
            Require(tokens.transform.Cast<Transform>().Count(t => t.gameObject.activeSelf) == 2 && tokens.transform.Find("点数 8 · fog") != null, "公开探索后未同步数字");
        }
        finally { root.SetActive(false); Destroy(root); }
    }
    int[] MarkerInstances() => Markers.GetComponentsInChildren<MeshFilter>().OrderBy(m => m.name, StringComparer.Ordinal).Select(m => m.GetInstanceID()).ToArray();
    void RefreshRealView()
    {
        if (mode == "base") GetComponent<M2App>().VerificationBoard.RefreshReferenceMarkers(GetComponent<M2App>().M6View);
        else if (mode == "seafarers") GetComponent<M3App>().VerificationBoard.RefreshReferenceMarkers(GetComponent<M3App>().M6View);
        else if (mode == "cities-knights") GetComponent<M4App>().VerificationBoard.RefreshReferenceMarkers(GetComponent<M4App>().M6View);
        else GetComponent<M5App>().VerificationBoard.RefreshReferenceMarkers(GetComponent<M5App>().M6View);
    }
    string FixedAuthority()
    {
        const uint seed = 20261014;
        if (mode == "base") return Catan.Core.M2.BaseGameSession.Create(Resources.Load<TextAsset>("m1-scenario").text, 4, seed).Save();
        if (mode == "seafarers") return Catan.Core.M3.SeafarersGameSession.Create("heading-for-new-shores", 4, seed).Save();
        if (mode == "cities-knights") return Catan.Core.M4.CitiesKnightsGameSession.Create(Resources.Load<TextAsset>("m1-scenario").text, 4, seed).Save();
        return Catan.Core.M5.CombinedGameSession.Create("heading-for-new-shores", 4, seed).Save();
    }
    void Advance()
    {
        if (!app.IsHumanTurn) { app.SetPaused(false); bool success = app.StepAi(); app.SetPaused(true); Require(success, "正常 AI 未继续开局"); return; }
        bool accepted;
        if (mode == "base") { var a = GetComponent<M2App>(); var c = new Catan.AI.BaseGameAi().Decide(a.M6View); c.Id = Guid.NewGuid().ToString("N"); accepted = a.Submit(c)?.Success == true; }
        else if (mode == "seafarers") { var a = GetComponent<M3App>(); var c = new Catan.AI.SeafarersAi().Decide(a.M6View); c.Id = Guid.NewGuid().ToString("N"); accepted = a.Submit(c)?.Success == true; }
        else if (mode == "cities-knights") { var a = GetComponent<M4App>(); var c = new Catan.AI.CitiesKnightsAi().Decide(a.M6View); c.Id = Guid.NewGuid().ToString("N"); accepted = a.Submit(c)?.Success == true; }
        else { var a = GetComponent<M5App>(); var c = new Catan.AI.CombinedAi().Decide(a.M6View); c.Id = Guid.NewGuid().ToString("N"); accepted = a.Submit(c)?.Success == true; }
        Require(accepted, "同权限测试策略的正常开局命令被拒绝");
    }
    Vector3 Position(string id) => mode == "base" ? GetComponent<M2App>().VerificationBoard.Position(id) :
        mode == "seafarers" ? GetComponent<M3App>().VerificationBoard.Position(id) :
        mode == "cities-knights" ? GetComponent<M4App>().VerificationBoard.Position(id) : GetComponent<M5App>().VerificationBoard.Position(id);
    void SetTop(bool top)
    {
        if (mode == "base") GetComponent<M2App>().VerificationBoard.SetCamera(top);
        else if (mode == "seafarers") GetComponent<M3App>().VerificationBoard.SetCamera(top);
        else if (mode == "cities-knights") GetComponent<M4App>().VerificationBoard.SetCamera(top);
        else GetComponent<M5App>().VerificationBoard.SetCamera(top);
    }
    void CaptureBoard(string filename)
    {
        var target = new RenderTexture(1440, 900, 24); var previous = RenderTexture.active; Texture2D image = null;
        try { target.Create(); Table.RenderComposite(target); RenderTexture.active = target; image = ReadImage(1440, 900); RequireNonEmpty(image); File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG()); cameraImages++; }
        finally { RenderTexture.active = previous; target.Release(); Destroy(target); if (image != null) Destroy(image); }
    }
    IEnumerator CaptureDetails()
    {
        var folder = Path.Combine(output, "details"); Directory.CreateDirectory(folder);
        var cameraObject = new GameObject("实体细节专用相机"); var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.nearClipPlane = .001f; camera.farClipPlane = 2;
        camera.cullingMask = 1 << DetailLayer; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.21f, .19f, .16f);
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        try
        {
            foreach (string id in AssetIds)
            {
                var root = new GameObject("实体四视图 " + id); GameObject asset;
                if (id == "number-token-back")
                {
                    var tile = new Tile { Id = "public-number", Resource = "wood", Number = 6 };
                    var tokens = TabletopNumberTokens.Create(root.transform, new[] { tile }, new Dictionary<string, Vector3> { { tile.Id, Vector3.zero } });
                    tokens.gameObject.SetActive(true); asset = tokens.transform.GetChild(0).gameObject;
                }
                else { asset = Instantiate(Resources.Load<GameObject>(AssetRoot + id), root.transform); Source(asset.transform, id); }
                SetLayer(root.transform, DetailLayer); yield return null;
                Texture2D sheet = new Texture2D(1600, 1200, TextureFormat.RGB24, false);
                var target = new RenderTexture(800, 600, 24); var previous = RenderTexture.active;
                try
                {
                    target.Create();
                    for (int panel = 0; panel < 4; panel++)
                    {
                        asset.transform.localRotation = panel == 3 ? Quaternion.Euler(180, 0, 0) : Quaternion.identity;
                        var bounds = BoundsOf(asset.transform); camera.aspect = 800f / 600;
                        camera.orthographicSize = Mathf.Max(bounds.size.z, bounds.size.x / camera.aspect, .008f) * .70f;
                        camera.transform.rotation = Quaternion.Euler(panel == 0 || panel == 3 ? 90 : panel == 1 ? 4 : 40, 0, 0);
                        camera.transform.position = bounds.center - camera.transform.forward * .5f;
                        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                        Require(RenderPipeline.SupportsRenderRequest(camera, request), "URP 不支持实体细节离屏渲染");
                        RenderPipeline.SubmitRenderRequest(camera, request); RenderTexture.active = target;
                        var panelImage = ReadImage(800, 600); RequireNonEmpty(panelImage);
                        sheet.SetPixels((panel % 2) * 800, (1 - panel / 2) * 600, 800, 600, panelImage.GetPixels()); Destroy(panelImage);
                    }
                    sheet.Apply(); File.WriteAllBytes(Path.Combine(folder, id + "-four-views.png"), sheet.EncodeToPNG()); detailImages++;
                }
                finally { RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(sheet); root.SetActive(false); Destroy(root); }
                yield return null;
            }
        }
        finally { Destroy(cameraObject); }
    }
    static Texture2D ReadImage(int width, int height)
    {
        var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); return image;
    }
    void RequireNonEmpty(Texture2D image)
    {
        var pixels = image.GetPixels32(); var first = pixels[0];
        Require(pixels.Count(p => Math.Abs(p.r - first.r) + Math.Abs(p.g - first.g) + Math.Abs(p.b - first.b) > 25) > 500, "实体相机图为空或没有绘出模型");
    }
    static Bounds BoundsOf(Transform obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>(); if (renderers.Length == 0) throw new InvalidOperationException("实体缺少 Renderer");
        var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
    }
    // AABB 仅作快速筛选；最终比较真实网格的 XZ 三角形，避免旋转港口的包围框产生假阳性。
    static bool FootprintIntersects(Transform a, Transform b)
    {
        var left = FootprintTriangles(a); var right = FootprintTriangles(b);
        foreach (var first in left) foreach (var second in right)
        {
            bool separated = false;
            foreach (var triangle in new[] { first, second })
                for (int edge = 0; edge < 3; edge++)
                {
                    var direction = triangle[(edge + 1) % 3] - triangle[edge];
                    var axis = new Vector2(-direction.y, direction.x).normalized;
                    float minA = first.Min(p => Vector2.Dot(p, axis)), maxA = first.Max(p => Vector2.Dot(p, axis));
                    float minB = second.Min(p => Vector2.Dot(p, axis)), maxB = second.Max(p => Vector2.Dot(p, axis));
                    if (maxA <= minB + .0001f || maxB <= minA + .0001f) { separated = true; break; }
                }
            if (!separated) return true;
        }
        return false;
    }
    static List<Vector2[]> FootprintTriangles(Transform root)
    {
        var triangles = new List<Vector2[]>();
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = filter.sharedMesh; if (mesh == null) continue;
            var vertices = mesh.vertices.Select(v => filter.transform.TransformPoint(v)).Select(v => new Vector2(v.x, v.z)).ToArray();
            var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                var a = vertices[indices[i]]; var b = vertices[indices[i + 1]]; var c = vertices[indices[i + 2]];
                if (Mathf.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) > .0000000001f) triangles.Add(new[] { a, b, c });
            }
        }
        return triangles;
    }
    static void SetLayer(Transform obj, int layer) { obj.gameObject.layer = layer; foreach (Transform child in obj) SetLayer(child, layer); }
    void Require(bool passed, string message) { checks++; if (!passed) throw new InvalidOperationException(message); }
}
