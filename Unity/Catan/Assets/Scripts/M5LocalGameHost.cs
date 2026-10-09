using System;
using System.IO;
using Catan.Core.M5;
using UnityEngine;

// Local authority boundary. The presentation receives only its seat's PlayerView.
public sealed class M5LocalGameHost
{
    private CombinedGameSession session;
    public M5LocalGameHost(int players = 4)
    {
        NewGame("heading-for-new-shores", players);
    }
    public void NewGame(string scenarioId, int players = 4) => NewGame(scenarioId, players, unchecked((uint)Guid.NewGuid().GetHashCode()));
    public void NewGame(string scenarioId, int players, uint seed) { session = CombinedGameSession.Create(scenarioId, players, seed); }
    public PlayerView View(string seat) => session.GetPlayerView(seat);
    public CommandResult Submit(Command command) => session.Execute(command);
    public CommandResult Preview(Command command) => session.Preview(command);
    public string ExportSave() => session.Save();
    public void ImportSave(string json) { session = CombinedGameSession.Load(json); }
    public string SavePath => Path.Combine(Application.persistentDataPath, "m5-authority-v5.json");
    public void Save() => WriteSave(SavePath);
    public void Load() => ReadSave(SavePath);
    public void WriteSave(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, session.Save());
        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
        else File.Move(temporary, path);
    }
    public void ReadSave(string path)
    {
        // Parsing and validation finish before replacing the running game.
        var restored = CombinedGameSession.Load(File.ReadAllText(path));
        session = restored;
    }
}

