using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace Catan.Core
{
    internal static class Json
    {
        public static string Write<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        public static T Read<T>(string text)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
        public static T Copy<T>(T value) { return Read<T>(Write(value)); }
        public static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }

    [DataContract]
    internal sealed class Scenario
    {
        [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
        [DataMember(Name = "scenarioId")] public string Id { get; set; }
        [DataMember(Name = "scenarioVersion")] public string Version { get; set; }
        [DataMember(Name = "rulesBaselineId")] public string RulesBaselineId { get; set; }
        [DataMember(Name = "rulesVersion")] public string RulesVersion { get; set; }
        [DataMember(Name = "players")] public string[] Players { get; set; }
        [DataMember(Name = "initialBank")] public ResourceBag InitialBank { get; set; }
        [DataMember(Name = "initialPieces")] public PieceSupply InitialPieces { get; set; }
        [DataMember(Name = "roadCost")] public ResourceBag RoadCost { get; set; }
        [DataMember(Name = "robberTile")] public string RobberTile { get; set; }
        [DataMember(Name = "topology")] public Topology Topology { get; set; }
        [DataMember(Name = "controlledRandom")] public ControlledRandom ControlledRandom { get; set; }
    }

    [DataContract]
    internal sealed class Topology
    {
        [DataMember(Name = "coordinateScheme")] public string CoordinateScheme { get; set; }
        [DataMember(Name = "tiles")] public Tile[] Tiles { get; set; }
        [DataMember(Name = "vertices")] public Vertex[] Vertices { get; set; }
        [DataMember(Name = "edges")] public Edge[] Edges { get; set; }
    }

    [DataContract]
    internal sealed class ControlledRandom
    {
        [DataMember(Name = "algorithmId")] public string AlgorithmId { get; set; }
        [DataMember(Name = "dice")] public int[][] Dice { get; set; }
        [DataMember(Name = "initialCursor")] public int InitialCursor { get; set; }
        [DataMember(Name = "authorityOnly")] public bool AuthorityOnly { get; set; }
    }
}
