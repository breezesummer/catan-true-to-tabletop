using System;
using System.IO;
using Catan.Core.M3;
using UnityEngine;

// The local host owns authority. Presentation and verification actions use PlayerView.
public sealed class M3LocalGameHost
{
    private SeafarersGameSession session;
    public M3LocalGameHost(int players = 4) { NewGame(players); }
    public void NewGame(int players = 4) => NewGame(players, unchecked((uint)Guid.NewGuid().GetHashCode()));
    public void NewGame(int players, uint seed) => NewGame("heading-for-new-shores", players, seed);
    public void NewGame(string scenario, int players) => NewGame(scenario, players, unchecked((uint)Guid.NewGuid().GetHashCode()));
    public void NewGame(string scenario, int players, uint seed) { session = SeafarersGameSession.Create(scenario, players, seed); }
    public PlayerView View(string seat) => session.GetPlayerView(seat);
    public CommandResult Submit(Command command) => session.Execute(command);
    public CommandResult Preview(Command command) => session.Preview(command);
    public string SavePath => Path.Combine(Application.persistentDataPath, "m3-authority-v3.json");
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
        // Complete parsing and validation before replacing the current game.
        var restored = SeafarersGameSession.Load(File.ReadAllText(path));
        session = restored;
    }
}
