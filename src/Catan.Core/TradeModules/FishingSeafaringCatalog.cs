using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Catan.Core.M3;

namespace Catan.Core.TradeModules
{
    public static partial class FishingScenarioCatalog
    {
        public const string SeafaringVersion = "seafaring-fishing-map-v001";
        public static bool SupportsSeafaring(string id) => id == "heading-for-new-shores" || id == "four-islands" || id == "fog-islands" || id == "through-the-desert";
        internal static bool IsInterior(Tile tile, Tile[] tiles) => tiles.Count(t => t.Id != tile.Id && t.Vertices.Intersect(tile.Vertices).Count() == 2 && Land(t)) == 6;
        private static bool Land(Tile tile) => tile.Resource != "sea" && tile.Resource != "fog";
        private static Edge[] Coast(Tile[] tiles, Edge[] edges) => edges.Where(e =>
        {
            var adjacent=tiles.Where(t=>e.Vertices.All(t.Vertices.Contains)).ToArray();
            return adjacent.Count(Land)==1 && (adjacent.Length==1 || adjacent.Any(t=>t.Resource=="sea"));
        }).ToArray();

        // 2026-10-29 用户确认的 2025 地图适配；详见 F4 规则说明。
        internal static void PrepareSeafaring(SeafarersScenario map)
        {
            if(map.Id=="four-islands")return;
            if(map.Id=="fog-islands")
            {
                int wheat=Array.IndexOf(map.HiddenResources,"wheat");
                if(wheat<0||map.HiddenNumbers.Length==0)throw new InvalidDataException("迷雾池缺少捕鱼适配组件。");
                map.HiddenResources[wheat]="lake";
                // 普通12号一枚退出，湖泊使用独立的2/3/11/12号码集。
                int number=Array.IndexOf(map.HiddenNumbers,12);
                if(number<0)throw new InvalidDataException("迷雾池缺少12号组件。");
                map.HiddenNumbers=map.HiddenNumbers.Where((_,i)=>i!=number).ToArray();return;
            }
            bool desert=map.Id=="heading-for-new-shores"&&map.Tiles.Any(t=>t.Resource=="desert");
            var sources=map.Tiles.Where(t=>map.StartingTileIds.Contains(t.Id)&&t.Resource==(desert?"desert":"wheat")).OrderBy(t=>t.Id,StringComparer.Ordinal).ToArray();
            var destinations=map.Tiles.Where(t=>map.StartingTileIds.Contains(t.Id)&&IsInterior(t,map.Tiles)&&
                (map.Id!="through-the-desert"||t.Resource!="desert")).OrderBy(t=>t.Id,StringComparer.Ordinal).ToArray();
            var original=map.Tiles.Select(t=>(tile:t,resource:t.Resource,number:t.Number)).ToArray();
            foreach(var source in sources) foreach(var destination in destinations.OrderBy(t=>t.Id==source.Id?0:1))
            {
                foreach(var item in original){item.tile.Resource=item.resource;item.tile.Number=item.number;}
                var numberTile=map.Id=="through-the-desert" ? map.Tiles.SingleOrDefault(t=>t.Number==2) : source;
                if(numberTile==null)throw new InvalidDataException("穿越沙漠缺少2号组件。");
                // 地形与普通号码是独立实体：只移除指定田地/沙漠及指定号码，其余红点尽量留原位。
                var resource=destination.Resource;var number=destination.Number;
                if(source!=destination)source.Resource=resource;
                if(numberTile!=destination)numberTile.Number=number;
                destination.Resource="lake";destination.Number=null;
                // 资源与数字交换不得破坏正常布局的红点分离。
                var red=map.Tiles.Where(t=>t.Number==6||t.Number==8).ToArray();
                if(red.Any(t=>red.Any(other=>t!=other&&t.Vertices.Intersect(other.Vertices).Count()==2)))continue;
                return;
            }
            foreach(var item in original){item.tile.Resource=item.resource;item.tile.Number=item.number;}
            throw new InvalidDataException("无法在有限候选内放置内陆湖泊并保持地图约束。");
        }

        // FSF.pdf 第1—2页：渔场附着固定空间；排序、洗牌、有限回溯，不能无限重抽。
        internal static FishingState CreateSeafaring(SeafarersScenario map, string[] players, Action<int[]> shuffle)
        {
            var fish=new FishingState {MapVersion=SeafaringVersion+":"+map.Id,LakeTileId=map.Tiles.SingleOrDefault(t=>t.Resource=="lake")?.Id,Holdings=players.Select(p=>new FishHolding {PlayerId=p}).ToArray()};
            var coast=Coast(map.Tiles,map.Edges);
            var candidates=new List<string[]>();
            foreach(var middle in coast.SelectMany(e=>e.Vertices).Distinct().OrderBy(v=>v,StringComparer.Ordinal))
            {
                var neighbors=coast.Where(e=>e.Vertices.Contains(middle)).SelectMany(e=>e.Vertices).Where(v=>v!=middle).Distinct().OrderBy(v=>v,StringComparer.Ordinal).ToArray();
                for(int a=0;a<neighbors.Length;a++) for(int b=a+1;b<neighbors.Length;b++) candidates.Add(new[]{neighbors[a],middle,neighbors[b]});
            }
            var order=Enumerable.Range(0,candidates.Count).ToArray();shuffle(order);
            var shuffled=order.Select(i=>candidates[i]).ToArray();
            var numbers=new[]{4,5,6,8,9,10};shuffle(numbers);
            var islands=map.Regions.OrderBy(r=>r.TileIds.Length).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray();
            bool Allowed(string[] vertices,int number)
            {
                var ids=map.Id=="four-islands" ? islands[number==4||number==8?0:number==6||number==10?1:number==5?2:3].TileIds : map.Id=="fog-islands"?map.StartingTileIds:null;
                return ids==null || vertices.All(v=>map.Tiles.Any(t=>ids.Contains(t.Id)&&Land(t)&&t.Vertices.Contains(v)));
            }
            var selected=new List<FishingGround>();int budget=200000;
            bool Place(int index)
            {
                if(index==6)return true;
                if(--budget<0)return false;
                foreach(var vertices in shuffled.Where(v=>Allowed(v,numbers[index])&&!selected.Any(g=>g.VertexIds.Intersect(v).Any())))
                {
                    selected.Add(new FishingGround {Id="FG"+(index+1),Number=numbers[index],VertexIds=vertices});
                    if(Place(index+1))return true;
                    selected.RemoveAt(selected.Count-1);
                }
                return false;
            }
            if(map.Id=="four-islands"&&islands.Length!=4 || !Place(0))throw new InvalidDataException("无法在有限搜索内放置六处合规渔场。");
            fish.Grounds=selected.ToArray();ValidateSeafaring(fish,map.Tiles,map.Edges);return fish;
        }
        private static void ValidateSeafaring(FishingState fish,Tile[] tiles,Edge[] edges)
        {
            string id=fish.MapVersion.Substring((SeafaringVersion+":").Length);
            bool lakeRequired=id=="heading-for-new-shores"||id=="through-the-desert";
            var lakes=tiles.Where(t=>t.Resource=="lake").ToArray();
            if(!SupportsSeafaring(id)||lakes.Length>1||lakeRequired&&lakes.Length!=1||id=="four-islands"&&lakes.Length!=0||
                fish.LakeTileId!=lakes.SingleOrDefault()?.Id||lakes.Any(t=>t.Number!=null||lakeRequired&&!IsInterior(t,tiles))||
                fish.Grounds==null||fish.Grounds.Length!=6||!fish.Grounds.Select(g=>g.Number).OrderBy(n=>n).SequenceEqual(new[]{4,5,6,8,9,10}))
                throw new InvalidDataException("航海捕鱼地图身份或湖泊无效。");
            var coast=Coast(tiles,edges);
            if(fish.Grounds.SelectMany(g=>g.VertexIds).Distinct().Count()!=18)throw new InvalidDataException("渔场顶点不得重叠。");
            for(int i=0;i<6;i++)
            {
                var g=fish.Grounds[i];
                if(g.Id!="FG"+(i+1)||g.VertexIds.Length!=3||!coast.Any(e=>e.Vertices.Contains(g.VertexIds[0])&&e.Vertices.Contains(g.VertexIds[1]))||!coast.Any(e=>e.Vertices.Contains(g.VertexIds[1])&&e.Vertices.Contains(g.VertexIds[2])))
                    throw new InvalidDataException("渔场必须附着连续海岸。");
            }
        }
    }
}
