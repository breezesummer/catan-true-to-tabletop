using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.M3
{
    public sealed class ScenarioRegion { public string Id { get; set; } public string[] TileIds { get; set; } }
    public sealed class VillageDefinition { public string Id { get; set; } public string VertexId { get; set; } public int Number { get; set; } }
    public sealed class FortressDefinition
    {
        public string PlayerId { get; set; } public string VertexId { get; set; } public string BeachheadVertexId { get; set; }
        public string StartingVertexId { get; set; } public string StartingShipEdgeId { get; set; }
    }
    public sealed class WonderDefinition
    {
        public string Id { get; set; } public ResourceBag Cost { get; set; } public string Requirement { get; set; }
        public string[] VertexIds { get; set; } = Array.Empty<string>();
    }
    /// <summary>Public scenario definition. Fog pools are unordered composition, never authoritative draw order.</summary>
    public sealed class SeafarersScenario
    {
        public string Id { get; set; } public string Name { get; set; } public string Version { get; set; } = "official-2025-v001";
        public string Description { get; set; } public int PlayerCount { get; set; } public int SourcePage { get; set; }
        public Tile[] Tiles { get; set; } public Vertex[] Vertices { get; set; } public Edge[] Edges { get; set; }
        public Port[] Ports { get; set; } = Array.Empty<Port>();
        public string InitialRobberTileId { get; set; } public string InitialPirateTileId { get; set; }
        public string[] StartingTileIds { get; set; } = Array.Empty<string>();
        public ScenarioRegion[] Regions { get; set; } = Array.Empty<ScenarioRegion>();
        public int IslandBonusPoints { get; set; } public int TargetVictoryPoints { get; set; }
        public int SetupRounds { get; set; } = 2; public bool UseLongestRoute { get; set; } = true; public bool UseLargestArmy { get; set; } = true;
        public string[] HiddenResources { get; set; } = Array.Empty<string>(); public int[] HiddenNumbers { get; set; } = Array.Empty<int>();
        public string[] BonusEdgeIds { get; set; } = Array.Empty<string>(); public string[] GiftCardEdgeIds { get; set; } = Array.Empty<string>();
        public Port[] GiftPorts { get; set; } = Array.Empty<Port>(); public VillageDefinition[] Villages { get; set; } = Array.Empty<VillageDefinition>();
        public FortressDefinition[] Fortresses { get; set; } = Array.Empty<FortressDefinition>(); public string[] PiratePath { get; set; } = Array.Empty<string>();
        public WonderDefinition[] Wonders { get; set; } = Array.Empty<WonderDefinition>();
        public string[] BannedSetupVertexIds { get; set; } = Array.Empty<string>();
        public string[] ForbiddenSettlementTileIds { get; set; } = Array.Empty<string>();
        public string[] ForbiddenRobberTileIds { get; set; } = Array.Empty<string>();
    }

    public static class SeafarersScenarios
    {
        public static string[] Ids { get { return new[] { "heading-for-new-shores", "four-islands", "fog-islands", "through-the-desert", "forgotten-tribe", "cloth-for-catan", "pirate-islands", "wonders-of-catan", "new-world" }; } }
        // Fixed layouts below are transcribed from the 2025 rulebook, not generated island approximations.
        public static SeafarersScenario Create(string id, int playerCount)
        {
            if (playerCount != 3 && playerCount != 4) throw new ArgumentOutOfRangeException(nameof(playerCount));
            if (!Ids.Contains(id)) throw new ArgumentException("Unknown Seafarers scenario.", nameof(id));
            var s = new SeafarersScenario { Id = id, PlayerCount = playerCount };
            bool three = playerCount == 3;
            switch (id)
            {
                case "heading-for-new-shores":
                    s.Name = "驶向新海岸"; s.SourcePage = three ? 4 : 5; s.TargetVictoryPoints = 14; s.IslandBonusPoints = 2;
                    s.Description = "主岛开局；每个新小岛的第一处定居点额外 2 分；14 分获胜。";
                    Build(s, three ? new[] { "b12 g5 . .", ". . . s4 o9", ". h4 s6 . h3 .", "s2 o5 w10 . g4", "b8 s10 s9 w8 . .", "h11 o3 b11 . o8", "h6 w5 . b10" }
                        : new[] { "o8 s11 . g4 .", ". . . . b5 o2", ". s5 w6 o4 . w9 .", "h12 b11 h3 s9 . g10", "b6 w10 d h11 w5 . .", "o3 s4 b9 s8 . b3", "h8 w2 o10 . h6" });
                    Regions(s); s.StartingTileIds = s.Regions.OrderByDescending(x => x.TileIds.Length).First().TileIds;
                    AddPorts(s, three ? new[] { "2,1,4,h", "2,2,1,*", "3,0,4,o", "4,0,3,*", "4,3,0,s", "6,0,4,b", "6,1,3,w", "6,1,1,*" }
                        : new[] { "2,1,4,*", "2,3,5,o", "3,0,4,s", "3,3,0,*", "4,0,3,b", "4,4,2,h", "6,0,4,*", "6,1,3,w", "6,2,1,*" });
                    break;
                case "four-islands":
                    s.Name = "四座岛屿"; s.SourcePage = three ? 6 : 7; s.TargetVictoryPoints = 13; s.IslandBonusPoints = 2;
                    s.Description = "任意岛开局；非起始岛的第一处定居点额外 2 分；13 分获胜。";
                    Build(s, three ? new[] { ". . h4 s3", "o4 w9 . h9 b5", "s6 o10 . w8 b11 .", ". . . . .", "h11 o8 w3 . b10 b6", "w5 s9 . o2 h5", "s12 . . ." }
                        : new[] { "s8 . w9 w11", "b10 . o3 h12 s5", "h5 w3 . b5 o10 .", ". . w6 . .", "b4 s9 . . w9 s11", "h6 o4 b2 . o8", "s10 h11 . h4" });
                    Regions(s); s.StartingTileIds = Land(s).Select(t => t.Id).ToArray();
                    AddPorts(s, three ? new[] { "0,3,0,*", "1,0,4,*", "2,1,1,o", "2,4,3,b", "4,0,0,w", "4,0,3,*", "5,3,5,*", "4,5,2,h", "6,0,1,s" }
                        : new[] { "1,0,4,h", "2,0,2,*", "2,4,1,w", "3,2,5,*", "4,0,3,b", "5,1,0,*", "4,5,5,o", "4,5,2,*", "6,0,4,s" });
                    break;
                case "fog-islands":
                    s.Name = "迷雾岛屿"; s.SourcePage = three ? 8 : 9; s.TargetVictoryPoints = 12;
                    s.Description = "道路或船触及迷雾顶点即探索；新陆地奖励资源；12 分获胜。";
                    Build(s, three ? new[] { "? ? . b6 w11", "? ? ? . w5 h3", ". . . ? . s8 s9", "w6 s5 . ? . o4", ". b11 w9 . ? . .", ". o8 h10 . ? ?", ". s12 . ? ?" }
                        : new[] { "? . b4 h10 o3", "? ? . s9 w6 b12", ". . ? . . s10 o8", "o3 . ? ? . h11", "h6 w4 . ? ? . w5", "b9 s8 . ? ? .", "s2 w5 . ? ?" });
                    s.HiddenResources = new[] { "sea", "sea", "gold", "gold", "brick", "brick", "wood", "wool", "wheat", "wheat", "ore", "ore" };
                    s.HiddenNumbers = three ? new[] { 3,3,4,5,6,8,9,10,11,12 } : new[] { 3,4,5,6,8,9,10,11,11,12 };
                    Regions(s); s.StartingTileIds = Land(s).Select(t => t.Id).ToArray();
                    AddPorts(s, three ? new[] { "0,3,0,*", "0,4,1,s", "1,5,1,h", "3,5,1,*", "3,0,4,o", "4,1,3,w", "6,1,4,*", "5,2,2,b" }
                        : new[] { "0,2,0,s", "0,4,5,h", "0,4,1,*", "1,5,1,b", "3,5,1,*", "3,0,4,w", "4,0,3,o", "6,0,4,*", "6,0,2,*" });
                    break;
                case "through-the-desert":
                    s.Name = "穿越沙漠"; s.SourcePage = three ? 10 : 11; s.TargetVictoryPoints = 14; s.IslandBonusPoints = 2;
                    s.Description = "主岛开局；跨沙漠地区与每个小岛首次定居额外 2 分；14 分获胜。";
                    Build(s, three ? new[] { "g4 d . o8", "w3 d w4 . s12", "h6 d b5 s6 . .", ". o3 w10 . g5", "w11 b6 h2 b9 . .", "o10 h9 w8 . h9", "s8 s4 . o5" }
                        : new[] { "g10 d w5 . o9", "o11 d b3 s6 . h4", "h8 d o8 h10 w4 . b2", ". w10 b11 s9 . .", "b12 b6 h5 w8 . g5 s3", "s3 s11 o4 . . .", ". w9 . o6 h12" });
                    Regions(s, true); s.StartingTileIds = s.Regions.OrderByDescending(x => x.TileIds.Length).First().TileIds; s.InitialRobberTileId = At(s,1,1).Id;
                    AddPorts(s, three ? new[] { "1,2,0,w", "3,1,4,h", "3,2,1,*", "4,0,3,s", "4,3,2,*", "6,0,4,o", "6,0,2,b", "6,1,1,*" }
                        : new[] { "0,2,1,b", "1,3,1,*", "2,4,2,*", "3,1,4,s", "4,3,2,*", "5,0,4,h", "5,0,2,o", "6,1,1,w", "6,1,3,*" });
                    break;
                default: Advanced(s); break;
            }
            if (s.InitialRobberTileId == null && id != "pirate-islands") s.InitialRobberTileId = s.Tiles.FirstOrDefault(t => t.Resource == "desert")?.Id ?? s.Tiles.FirstOrDefault(t => t.Number == 12)?.Id;
            if (s.InitialPirateTileId == null && id != "wonders-of-catan") s.InitialPirateTileId = "frame";
            return s;
        }

        private static IEnumerable<Tile> Land(SeafarersScenario s) { return s.Tiles.Where(t => t.Resource != "sea" && t.Resource != "fog"); }
        private static string ResourceName(char c)
        {
            switch (c) { case 'b': return "brick"; case 'w': return "wood"; case 's': return "wool"; case 'h': return "wheat"; case 'o': return "ore"; case 'g': return "gold"; case 'd': return "desert"; case '?': return "fog"; default: return "sea"; }
        }
        private static void Build(SeafarersScenario s, string[] rows)
        {
            var vertices = new Dictionary<string, Vertex>(); var edges = new Dictionary<string, Edge>(); var tiles = new List<Tile>();
            int[] starts = { 0,-1,-2,-2,-3,-3,-3 }; int[] dx = {0,1,1,0,-1,-1}; int[] dy = {-2,-1,1,2,1,-1};
            for (int r = 0; r < rows.Length; r++)
            {
                var tokens = rows[r].Split(' ');
                for (int c = 0; c < tokens.Length; c++)
                {
                    int q = starts[r] + c; string token = tokens[c];
                    var t = new Tile { Id = "T" + q + ":" + r, Q = q, R = r, Resource = ResourceName(token[0]), Number = token.Length > 1 ? (int?)int.Parse(token.Substring(1)) : null, Vertices = new string[6] };
                    for (int k = 0; k < 6; k++)
                    {
                        int x = 2*q+r+dx[k], y = 3*r+dy[k]; string vid = "V" + x + ":" + y;
                        if (!vertices.ContainsKey(vid)) vertices.Add(vid, new Vertex { Id = vid, X = x, Y = y });
                        t.Vertices[k] = vid;
                    }
                    for (int k = 0; k < 6; k++)
                    {
                        var ends = new[] { t.Vertices[k], t.Vertices[(k+1)%6] }.OrderBy(x=>x,StringComparer.Ordinal).ToArray(); string eid = "E" + ends[0] + "|" + ends[1];
                        if (!edges.ContainsKey(eid)) edges.Add(eid,new Edge { Id = eid, Vertices = ends });
                    }
                    tiles.Add(t);
                }
            }
            s.Tiles = tiles.ToArray(); s.Vertices = vertices.Values.OrderBy(v=>v.Id,StringComparer.Ordinal).ToArray(); s.Edges = edges.Values.OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray();
        }
        private static Tile At(SeafarersScenario s, int r, int c) { return s.Tiles.Where(t=>t.R==r).OrderBy(t=>t.Q).ElementAt(c); }
        private static string EdgeAt(SeafarersScenario s, int r, int c, int side)
        {
            var tile = At(s,r,c); string a = tile.Vertices[side], b = tile.Vertices[(side+1)%6];
            return s.Edges.Single(e=>e.Vertices.Contains(a)&&e.Vertices.Contains(b)).Id;
        }
        private static void AddPorts(SeafarersScenario s, string[] entries, bool gifts = false)
        {
            var ports = entries.Select((entry,i) => { var a=entry.Split(','); string edge=EdgeAt(s,int.Parse(a[0]),int.Parse(a[1]),int.Parse(a[2]));
                string name=ResourceName(a[3][0]); Resource resource; return new Port { Id = (gifts ? "G" : "P")+(i+1), EdgeId=edge, Vertices=s.Edges.Single(e=>e.Id==edge).Vertices.ToArray(), Resource=Enum.TryParse<Resource>(name,true,out resource)? (Resource?)resource : null }; }).ToArray();
            if (gifts) s.GiftPorts=ports; else s.Ports=ports;
        }
        private static void Regions(SeafarersScenario s, bool desertSeparates = false)
        {
            var unseen = new HashSet<string>(Land(s).Where(t=>!desertSeparates||t.Resource!="desert").Select(t=>t.Id)); var result = new List<ScenarioRegion>();
            while(unseen.Count>0)
            {
                var found=new List<string>(); var queue=new Queue<string>(); queue.Enqueue(unseen.OrderBy(x=>x,StringComparer.Ordinal).First());
                while(queue.Count>0) { string id=queue.Dequeue(); if(!unseen.Remove(id)) continue; found.Add(id); var t=s.Tiles.Single(x=>x.Id==id);
                    foreach(var n in s.Tiles.Where(x=>unseen.Contains(x.Id)&&x.Vertices.Intersect(t.Vertices).Count()==2)) queue.Enqueue(n.Id); }
                result.Add(new ScenarioRegion {Id="region-"+(result.Count+1), TileIds=found.ToArray()});
            }
            s.Regions=result.ToArray();
        }

        private static void Advanced(SeafarersScenario s)
        {
            switch(s.Id)
            {
                case "forgotten-tribe":
                    s.Name="被遗忘的部落"; s.SourcePage=12; s.TargetVictoryPoints=13;
                    s.Description="只在有数字的主岛定居；海上拾取分数、发展卡和可迁移港口；13 分获胜。";
                    Build(s,new[] { "g o . d o h", ". . . . . . .", "h6 w9 o11 b5 w6 o4 . s", "s10 b8 s4 h12 w5 s2 .", "w11 h9 o3 s8 b10 h3 . w", ". . . . . . .", "d b . b d g" });
                    Regions(s); s.StartingTileIds=s.Tiles.Where(t=>t.Number.HasValue).Select(t=>t.Id).ToArray();
                    s.ForbiddenSettlementTileIds=Land(s).Where(t=>!t.Number.HasValue).Select(t=>t.Id).ToArray();
                    s.ForbiddenRobberTileIds=s.ForbiddenSettlementTileIds.ToArray();
                    s.InitialRobberTileId=At(s,6,4).Id; s.InitialPirateTileId=At(s,0,2).Id;
                    s.BonusEdgeIds=new[] { EdgeAt(s,0,0,0),EdgeAt(s,0,3,0),EdgeAt(s,0,5,0),EdgeAt(s,2,7,1),EdgeAt(s,4,7,1),EdgeAt(s,6,0,2),EdgeAt(s,6,3,3),EdgeAt(s,6,5,2) };
                    s.GiftCardEdgeIds=new[] { EdgeAt(s,0,0,4),EdgeAt(s,0,5,1),EdgeAt(s,6,0,4),EdgeAt(s,6,5,1) };
                    AddPorts(s,new[] {"0,0,5,b","0,3,5,w","2,7,0,s","4,7,2,h","6,0,3,o","6,3,2,*"},true);
                    break;
                case "cloth-for-catan":
                    s.Name="卡坦的布匹"; s.SourcePage=14; s.TargetVictoryPoints=14; s.SetupRounds=3; s.UseLongestRoute=false;
                    s.Description="开局放三处定居点；连接村庄取布，每两布 1 分；14 分或五村耗尽时结算。";
                    Build(s,new[] { "w4 s6 b5 s11 h8", "h3 w12 . . w3 o9", "h12 . . g . . .", ". d . . d .", ". . . g . . o2", "b9 h2 . . s11 o4", "s10 w6 o5 h10 b8" });
                    Regions(s); s.StartingTileIds=s.Tiles.Where(t=>t.Number.HasValue).Select(t=>t.Id).ToArray();
                    s.ForbiddenSettlementTileIds=Land(s).Where(t=>!t.Number.HasValue).Select(t=>t.Id).ToArray(); s.ForbiddenRobberTileIds=s.ForbiddenSettlementTileIds.ToArray();
                    s.InitialRobberTileId=At(s,2,0).Id;
                    s.Villages=new[] { Village(s,3,1,0,10),Village(s,3,1,3,9),Village(s,2,3,0,11),Village(s,2,3,3,8),Village(s,4,3,0,6),Village(s,4,3,3,3),Village(s,3,4,0,4),Village(s,3,4,3,5) };
                    AddPorts(s,new[] {"0,0,0,b","0,3,5,w","1,0,4,s","1,5,1,h","4,6,1,o","5,0,4,*","6,1,3,*","6,3,2,*","5,5,2,*"});
                    break;
                case "pirate-islands":
                    s.Name="海盗群岛"; s.SourcePage=16; s.TargetVictoryPoints=10; s.UseLongestRoute=false; s.UseLargestArmy=false;
                    s.Description="固定舰队穿越海域；骑士转战舰，最短航线经滩头抵达本色堡垒；夺堡且 10 分获胜。";
                    Build(s,new[] { "g11 o6 . . h4 b5", "b . . d . o9 w10", "h4 . . d . w3 s8 w5", ". o8 . . h6 b9 s12", "h10 . . d . s11 w8 s9", "b . . s . o5 w2", "g3 o6 . . h10 b4" });
                    Regions(s); s.StartingTileIds=s.Regions.OrderByDescending(x=>x.TileIds.Length).First().TileIds;
                    s.ForbiddenSettlementTileIds=Land(s).Where(t=>!s.StartingTileIds.Contains(t.Id)).Select(t=>t.Id).ToArray();
                    var forts=new[] {
                        Fortress(s,"P1",0,0,0,0,1,1,0,4,3,3),
                        Fortress(s,"P2",4,0,2,3,1,3,3,4,3,3),
                        Fortress(s,"P3",6,0,3,6,1,2,6,4,0,5),
                        Fortress(s,"P4",2,0,1,3,1,0,3,4,0,5) };
                    s.Fortresses=forts.Take(s.PlayerCount).ToArray();
                    s.PiratePath=new[] { At(s,6,3).Id,At(s,6,2).Id,At(s,5,2).Id,At(s,4,2).Id,At(s,3,2).Id,At(s,2,2).Id,At(s,1,2).Id,At(s,0,2).Id,At(s,0,3).Id,At(s,1,4).Id,At(s,2,4).Id,At(s,3,3).Id,At(s,4,4).Id,At(s,5,4).Id };
                    s.InitialPirateTileId=s.PiratePath[0];
                    AddPorts(s,new[] {"0,4,5,b","0,5,5,w","0,5,1,s","1,6,1,h","3,6,1,o","4,7,2,*","5,6,2,*","6,4,2,*"});
                    break;
                case "wonders-of-catan":
                    s.Name="卡坦奇观"; s.SourcePage=18; s.TargetVictoryPoints=10; s.IslandBonusPoints=1;
                    s.Description="小岛首次定居额外 1 分；先完成四级奇观获胜，或 10 分且奇观等级独占领先。";
                    Build(s,new[] { "g8 b2 . o10 . .", ". . . w11 . d .", "o12 h6 b11 h10 s3 w9 d .", "o3 h4 o6 s5 b4 d .", "b8 s9 . . b10 . . g6", ". w3 . w8 h9 . w4", "o5 . s11 s2 . h5" });
                    Regions(s); s.StartingTileIds=s.Regions.OrderByDescending(x=>x.TileIds.Length).First().TileIds.Where(id=>s.Tiles.Single(t=>t.Id==id).Resource!="desert").ToArray();
                    var wall=new[] { At(s,2,5).Vertices[0],At(s,2,5).Vertices[1],At(s,2,5).Vertices[2],At(s,2,5).Vertices[3],At(s,3,4).Vertices[2] };
                    var bridge=new[] { At(s,5,1).Vertices[2],At(s,6,2).Vertices[5] };
                    s.BannedSetupVertexIds=wall.Concat(bridge).Concat(new[] { At(s,5,1).Vertices[1],At(s,5,1).Vertices[3],At(s,6,2).Vertices[4],At(s,6,2).Vertices[0] }).Distinct().ToArray();
                    s.Wonders=new[] {
                        new WonderDefinition { Id="great-wall", Requirement="marker-building", VertexIds=wall, Cost=new ResourceBag {Brick=3,Wood=1,Wheat=1} },
                        new WonderDefinition { Id="great-bridge", Requirement="marker-building", VertexIds=bridge, Cost=new ResourceBag {Wood=3,Wool=1,Wheat=1} },
                        new WonderDefinition { Id="grand-monument", Requirement="port-city-route-five", Cost=new ResourceBag {Wheat=3,Ore=2} },
                        new WonderDefinition { Id="grand-theater", Requirement="two-cities", Cost=new ResourceBag {Brick=1,Wood=1,Wool=3} },
                        new WonderDefinition { Id="grand-castle", Requirement="city-six-vp", Cost=new ResourceBag {Brick=1,Wheat=1,Ore=3} } };
                    s.InitialRobberTileId=At(s,2,6).Id;
                    AddPorts(s,new[] {"2,0,0,b","3,0,4,w","5,1,4,s","2,3,5,h","2,4,0,o","3,1,2,*","4,4,1,*","5,4,2,*","6,2,2,*"});
                    break;
                case "new-world":
                    s.Name="新世界"; s.SourcePage=20; s.TargetVictoryPoints=12; s.IslandBonusPoints=1;
                    s.Description="按官方自由地图程序生成；非起始岛首次定居额外 1 分；12 分获胜。";
                    NewWorld(s,2025u); break;
            }
        }
        private static VillageDefinition Village(SeafarersScenario s,int r,int c,int corner,int number)
        { return new VillageDefinition { Id="village-"+number, VertexId=At(s,r,c).Vertices[corner], Number=number }; }
        private static FortressDefinition Fortress(SeafarersScenario s,string player,int fr,int fc,int fv,int br,int bc,int bv,int sr,int sc,int sv,int side)
        { return new FortressDefinition { PlayerId=player,VertexId=At(s,fr,fc).Vertices[fv],BeachheadVertexId=At(s,br,bc).Vertices[bv],StartingVertexId=At(s,sr,sc).Vertices[sv],StartingShipEdgeId=EdgeAt(s,sr,sc,side) }; }
        public static SeafarersScenario CreateSeeded(string id,int playerCount,uint seed)
        {
            var s=Create(id,playerCount);
            if(id=="new-world") NewWorld(s,seed);
            if(id=="forgotten-tribe"||id=="cloth-for-catan"||id=="pirate-islands"||id=="wonders-of-catan")
            {
                var ports=id=="forgotten-tribe"?s.GiftPorts:s.Ports; var resources=ports.Select(p=>p.Resource).ToArray(); Shuffle(resources,ref seed);
                for(int i=0;i<ports.Length;i++) ports[i].Resource=resources[i];
            }
            return s;
        }
        private static uint Next(ref uint seed) { if(seed==0)seed=0x9E3779B9; seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return seed; }
        private static void Shuffle<T>(T[] values,ref uint seed) { for(int i=values.Length-1;i>0;i--) { int j=(int)(Next(ref seed)%(uint)(i+1)); T temp=values[i];values[i]=values[j];values[j]=temp; } }
        private static void NewWorld(SeafarersScenario s,uint seed)
        {
            Build(s,new[] { ". . . . .", ". . . . . .", ". . . . . . .", ". . . . . .", ". . . . . . .", ". . . . . .", ". . . . ." });
            var pool=Enumerable.Repeat("sea",19).Concat(Enumerable.Repeat("brick",4)).Concat(Enumerable.Repeat("wood",5)).Concat(Enumerable.Repeat("wool",5)).Concat(Enumerable.Repeat("wheat",5)).Concat(Enumerable.Repeat("ore",4)).ToArray(); Shuffle(pool,ref seed);
            for(int i=0;i<pool.Length;i++) s.Tiles[i].Resource=pool[i];
            var land=Land(s).ToArray(); Shuffle(land,ref seed); var red=new List<Tile>();
            foreach(var tile in land) if(red.All(t=>t.Vertices.Intersect(tile.Vertices).Count()<2)) { red.Add(tile); if(red.Count==4)break; }
            if(red.Count!=4) throw new InvalidOperationException("Unable to separate New World red numbers.");
            int[] redNumbers={6,6,8,8}; Shuffle(redNumbers,ref seed); for(int i=0;i<4;i++)red[i].Number=redNumbers[i];
            int[] black={2,3,3,3,4,4,4,5,5,5,9,9,9,10,10,10,11,11,12}; Shuffle(black,ref seed); int n=0;
            foreach(var tile in land.Except(red))tile.Number=black[n++];
            Regions(s);s.StartingTileIds=land.Select(t=>t.Id).ToArray();s.InitialRobberTileId="frame";
            var coast=s.Edges.Where(e=> {var adjacent=s.Tiles.Where(t=>e.Vertices.All(v=>t.Vertices.Contains(v))).ToArray();return adjacent.Count(t=>t.Resource!="sea")==1&&(adjacent.Length==1||adjacent.Any(t=>t.Resource=="sea"));}).ToArray();
            Shuffle(coast,ref seed); var selected=new List<Edge>();
            foreach(var edge in coast) if(selected.All(e=>!e.Vertices.Intersect(edge.Vertices).Any())) {selected.Add(edge); if(selected.Count==10)break;}
            if(selected.Count!=10) throw new InvalidOperationException("Insufficient separated New World port locations.");
            Resource?[] resources={Resource.Wood,Resource.Brick,Resource.Wool,Resource.Wheat,Resource.Ore,null,null,null,null,null}; Shuffle(resources,ref seed);
            s.Ports=selected.Select((e,i)=>new Port {Id="P"+(i+1),EdgeId=e.Id,Vertices=e.Vertices.ToArray(),Resource=resources[i]}).ToArray();
        }
    }
}
