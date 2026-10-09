using System;
using System.IO;
using Catan.Core.M4;
using UnityEngine;

// Local authority boundary. The presentation receives only its seat's PlayerView.
public sealed class M4LocalGameHost
{
    private CitiesKnightsGameSession session;
    private readonly string topology;
    public M4LocalGameHost(int players = 4)
    {
        topology = Resources.Load<TextAsset>("m1-scenario").text;
        NewGame(players);
    }
    public void NewGame(int players = 4) => NewGame(players, unchecked((uint)Guid.NewGuid().GetHashCode()));
    public void NewGame(int players, uint seed) { session = CitiesKnightsGameSession.Create(topology, players, seed); }
    public PlayerView View(string seat) => session.GetPlayerView(seat);
    public CommandResult Submit(Command command) => session.Execute(command);
    public CommandResult Preview(Command command) => session.Preview(command);
    public string SavePath => Path.Combine(Application.persistentDataPath, "m4-authority-v4.json");
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
        var restored = CitiesKnightsGameSession.Load(topology, File.ReadAllText(path));
        session = restored;
    }
}

