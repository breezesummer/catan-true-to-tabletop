using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

// 显式验收参数才启用；通过实际 M6 新局/读档入口检查公开地图和三维刷新，不公开权威数据。
public sealed class CombinedMapVerification : MonoBehaviour
{
    [Serializable] sealed class Result
    {
        public string status, unity;
        public int configurations, newGames, saveRoundtrips, legacyLoads;
    }
    M6App app;
    string directory, legacyDirectory;
    readonly Result result = new Result();
    void Start()
    {
        var args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, "--combined-map-verify");
        if (flag < 0) return;
        directory = args[flag + 1]; Directory.CreateDirectory(directory);
        int legacy = Array.IndexOf(args, "--combined-map-legacy");
        if (legacy >= 0) legacyDirectory = args[legacy + 1];
        app = GetComponent<M6App>(); StartCoroutine(Run());
    }
    static void Require(bool value) { if (!value) throw new InvalidOperationException("组合地图验收失败。"); }
    IEnumerator Run()
    {
        var work = Verify();
        while (true)
        {
            object current;
            try { if (!work.MoveNext()) break; current = work.Current; }
            catch (Exception ex)
            {
                Debug.LogError("CATAN_COMBINED_MAP_FAILED: " + ex.GetType().Name);
                Application.Quit(1); yield break;
            }
            yield return current;
        }
        result.status = "passed"; result.unity = Application.unityVersion;
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonUtility.ToJson(result, true));
        Debug.Log("CATAN_COMBINED_MAP_OK: actual M6 new games; terrain/number/port variation; one renderer; exact save roundtrips; legacy maps");
        Application.Quit(0);
    }
    string PublicMap()
    {
        var game = GetComponent<M5App>(); var view = game.M6View; var board = game.VerificationBoard;
        Require(FindObjectsByType<M5BoardRenderer>(FindObjectsSortMode.None).Length == 1);
        foreach (var tile in view.Board.Tiles)
        {
            Require(board.TerrainResource(tile.Id) == tile.Resource);
            Require(board.transform.Find(tile.Id) != null);
        }
        var red = view.Board.Tiles.Where(t => t.Number == 6 || t.Number == 8).ToArray();
        foreach (var tile in red) Require(!red.Any(t => t.Id != tile.Id && t.Vertices.Intersect(tile.Vertices).Count() == 2));
        return string.Join("|", view.Board.Tiles.Select(t => t.Id + ":" + t.Resource + ":" + t.Number)) +
            string.Join("|", view.Board.Ports.Select(p => p.Id + ":" + p.Resource));
    }
    IEnumerator Verify()
    {
        foreach (var scenario in Catan.Core.M5.CombinedScenarios.Ids)
        foreach (int players in new[] { 3, 4 })
        {
            string previous = null;
            for (int repetition = 0; repetition < 3; repetition++)
            {
                app.StartGame("combined", scenario, players, "P1", true);
                yield return null; yield return null;
                Require(app.Ready && app.Paused && app.Fault == "");
                string current = PublicMap(); Require(current != previous); previous = current; result.newGames++;
            }
            string path = Path.Combine(directory, scenario + "-" + players + ".json");
            string envelope = app.ExportEnvelope(); app.WriteSave(path); app.ReadSave(path);
            yield return null; yield return null;
            Require(app.ExportEnvelope() == envelope && PublicMap() == previous); result.saveRoundtrips++;
            if (legacyDirectory != null)
            {
                string payload = File.ReadAllText(Path.Combine(legacyDirectory, scenario + "-" + players + ".json"));
                app.StartGame("combined", scenario, players, "P1", true, payload);
                yield return null; yield return null;
                Require(GetComponent<M5App>().VerificationHost.ExportSave() == payload);
                PublicMap(); result.legacyLoads++;
            }
            result.configurations++;
        }
    }
}
