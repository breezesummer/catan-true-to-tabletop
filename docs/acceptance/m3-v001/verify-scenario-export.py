"""Compare an exported SeafarersScenario[] JSON with independently transcribed PDF component tables."""
import collections
import json
import pathlib
import sys

baseline = json.loads(pathlib.Path(__file__).with_name("scenario-source-counts.json").read_text(encoding="utf-8"))
scenarios = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding="utf-8"))
checks = 0
for case in baseline["cases"]:
    players = case["players"] if isinstance(case["players"], list) else [case["players"]]
    for count in players:
        s = next(x for x in scenarios if x["Id"] == case["id"] and x["PlayerCount"] == count)
        label = f'{case["id"]}/{count}'
        resources = collections.Counter(t["Resource"] for t in s["Tiles"])
        assert [resources[r] for r in baseline["resourceOrder"]] == case["resources"], (label,"resources",resources)
        numbers = collections.Counter(t["Number"] for t in s["Tiles"] if t["Number"] is not None)
        numbers.update(v["Number"] for v in s["Villages"])
        assert [numbers[n] for n in baseline["numberOrder"]] == case["numbers"], (label,"numbers",numbers)
        checks += 2
        assert s["InitialRobberTileId"] == case["robber"], (label,"robber",s["InitialRobberTileId"])
        assert s["InitialPirateTileId"] == case["pirate"], (label,"pirate",s["InitialPirateTileId"])
        checks += 2
        for key, prop in {"ports":"Ports","regions":"Regions","giftPorts":"GiftPorts","bonusEdges":"BonusEdgeIds","giftCardEdges":"GiftCardEdgeIds","villages":"Villages","piratePath":"PiratePath","wonders":"Wonders"}.items():
            if key in case:
                assert len(s[prop]) == case[key], (label,key,len(s[prop]))
                checks += 1
        vertices = {v["Id"] for v in s["Vertices"]}
        edges = {e["Id"]:set(e["Vertices"]) for e in s["Edges"]}
        tiles = {t["Id"]:t for t in s["Tiles"]}
        assert len(tiles) == len(s["Tiles"]) and len(edges) == len(s["Edges"]) and len(vertices) == len(s["Vertices"])
        assert all(len(t["Vertices"]) == 6 and set(t["Vertices"]) <= vertices for t in s["Tiles"])
        assert all(len(e) == 2 and e <= vertices for e in edges.values())
        for port in s["Ports"]+s["GiftPorts"]:
            assert set(port["Vertices"]) == edges[port["EdgeId"]], (label,"port endpoints")
            adjacent = [t for t in s["Tiles"] if edges[port["EdgeId"]] <= set(t["Vertices"])]
            assert len([t for t in adjacent if t["Resource"] != "sea"]) == 1, (label,"coastal port",port)
        for i,p in enumerate(s["Ports"]):
            assert all(not set(p["Vertices"]) & set(other["Vertices"]) for other in s["Ports"][:i]), (label,"port separation")
        for v in s["Villages"]:
            assert v["VertexId"] in vertices
        for f in s["Fortresses"]:
            assert {f["VertexId"],f["BeachheadVertexId"],f["StartingVertexId"]} <= vertices
            assert f["StartingVertexId"] in edges[f["StartingShipEdgeId"]]
        if s["PiratePath"]:
            assert len(set(s["PiratePath"])) == len(s["PiratePath"])
            path = [tiles[t] for t in s["PiratePath"]]
            assert all(t["Resource"] == "sea" for t in path)
            assert all(len(set(t["Vertices"]) & set(path[(i+1)%len(path)]["Vertices"])) == 2 for i,t in enumerate(path)), (label,"pirate path continuity")
        checks += 7
        print("PASS", label)
print(f"Passed {checks} source-count and topology assertions for 18 scenarios.")
