using System;
using System.IO;
using Catan.Core;
using UnityEngine;

// Local authority boundary. Presentation receives only a projected PlayerView.
public sealed class LocalGameHost
{
    private GameSession session;
    private readonly string scenario;
    public LocalGameHost() { scenario = Resources.Load<TextAsset>("m1-scenario").text; NewGame(); }
    public void NewGame() { session = GameSession.Create(scenario); }
    public PlayerView View(string seat) => session.GetPlayerView(seat);
    public CommandResult Submit(Command command) => session.Execute(command);
    public CommandResult Preview(Command command) => session.Preview(command);
    public string SavePath => Path.Combine(Application.persistentDataPath, "m1-authority-v1.json");
    public void Save() => WriteSave(SavePath);
    public void Load() => ReadSave(SavePath);
    public void WriteSave(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var temp = path + ".tmp";
        File.WriteAllText(temp, session.Save());
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }
    public void ReadSave(string path) { var restored = GameSession.Load(scenario, File.ReadAllText(path)); session = restored; }
}
