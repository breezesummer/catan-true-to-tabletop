"""Check the authored M1 fixture, not a game implementation or Unity build.

Python standard library only. The fixture is deliberately narrow: topology,
setup legality, resource arithmetic and documented checkpoint consistency.
"""

import argparse
import copy
import json
import sys
from collections import Counter
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def indexed(items, prefix, count):
    result = {item["id"]: item for item in items}
    require(len(items) == len(result) == count, f"{prefix}: count/duplicate ID")
    require(list(result) == [f"{prefix}{i:02d}" for i in range(1, count + 1)],
            f"{prefix}: IDs/order changed; version the fixture explicitly")
    return result


def verify(data):
    resources = ["wood", "brick", "wool", "wheat", "ore"]
    players = ["P1", "P2", "P3", "P4"]
    require(data["schemaVersion"] == 1, "Unsupported fixture schema")
    require(data["scenarioId"] == "m1-fixed-four-seats-v001", "Scenario ID")
    require(data["scenarioVersion"] == "1.0.0", "Scenario version")
    require(data["rulesBaselineId"] == "catan-base-2025-en-v1", "Rules baseline")
    require(data["rulesVersion"] == "catan-m1-slice-v1", "Rules slice version")
    require(data["resourceOrder"] == resources and data["players"] == players,
            "Resource/player order")
    require(data["initialBank"] == dict.fromkeys(resources, 19), "Initial bank")
    require(data["initialPieces"] == {"roads": 15, "settlements": 5, "cities": 4},
            "Initial pieces")
    require(data["roadCost"] == dict(zip(resources, [1, 1, 0, 0, 0])), "Road cost")
    require(data["ports"] == [], "Ports are outside this slice")

    topology = data["topology"]
    require(topology["coordinateScheme"] == "pointy-axial-integer-corners-v1",
            "Coordinate scheme")
    tiles = indexed(topology["tiles"], "T", 19)
    vertices = indexed(topology["vertices"], "V", 54)
    edges = indexed(topology["edges"], "E", 72)
    coords = [(q, r) for r in range(-2, 3) for q in range(-2, 3) if abs(q + r) <= 2]
    corners = [(0, -2), (1, -1), (1, 1), (0, 2), (-1, 1), (-1, -1)]
    require([(t["q"], t["r"]) for t in tiles.values()] == coords, "Hex radius/order")
    points = sorted({(2*q+r+x, 3*r+y) for q, r in coords for x, y in corners},
                    key=lambda point: (point[1], point[0]))
    require([(v["x"], v["y"]) for v in vertices.values()] == points,
            "Vertex integer coordinates")
    point_ids = {point: f"V{i+1:02d}" for i, point in enumerate(points)}
    all_pairs = Counter()
    for tile in tiles.values():
        q, r = tile["q"], tile["r"]
        expected = [point_ids[(2*q+r+x, 3*r+y)] for x, y in corners]
        require(tile["vertices"] == expected, f"{tile['id']}: vertex winding")
        all_pairs.update(tuple(sorted((expected[i], expected[(i+1) % 6])))
                         for i in range(6))
    require([tuple(e["vertices"]) for e in edges.values()] == sorted(all_pairs),
            "Edge endpoints or stable order")
    require(Counter(all_pairs.values()) == {1: 30, 2: 42}, "Boundary/shared edges")
    require(54 - 72 + 19 == 1, "Planar topology invariant")
    neighbours = {vertex: set() for vertex in vertices}
    for edge in edges.values():
        a, b = edge["vertices"]
        neighbours[a].add(b)
        neighbours[b].add(a)
    seen, stack = set(), ["V01"]
    while stack:
        vertex = stack.pop()
        if vertex not in seen:
            seen.add(vertex)
            stack.extend(neighbours[vertex] - seen)
    require(len(seen) == 54, "Disconnected board")
    require(Counter(t["resource"] for t in tiles.values()) ==
            {"wood": 4, "brick": 3, "wool": 4, "wheat": 4, "ore": 3, None: 1},
            "Terrain quantities")
    require(Counter(t["number"] for t in tiles.values()) ==
            {2: 1, 3: 2, 4: 2, 5: 2, 6: 2, 8: 2, 9: 2, 10: 2, 11: 2, 12: 1, None: 1},
            "Number disc quantities")
    require(data["robberTile"] == "T10" and tiles["T10"]["resource"] is None
            and tiles["T10"]["number"] is None, "Desert/robber")
    red_tiles = [t for t in tiles.values() if t["number"] in (6, 8)]
    for i, tile in enumerate(red_tiles):
        for other in red_tiles[i+1:]:
            require(len(set(tile["vertices"]) & set(other["vertices"])) < 2,
                    "Adjacent red numbers in fixed fixture")

    setup = data["setup"]
    require([s["player"] for s in setup] == ["P1", "P2", "P3", "P4", "P4", "P3", "P2", "P1"],
            "Snake setup order")
    settlements, placed_roads = {}, {}
    hands = {p: dict.fromkeys(resources, 0) for p in players}
    bank = data["initialBank"].copy()
    pieces = {p: data["initialPieces"].copy() for p in players}
    for i, placement in enumerate(setup):
        player, vertex, edge = (placement[k] for k in ("player", "vertex", "edge"))
        require(placement["settlementCommandId"] == f"C{2*i+1:02d}" and
                placement["roadCommandId"] == f"C{2*i+2:02d}", "Setup command IDs")
        require(vertex in vertices and vertex not in settlements, "Setup occupied vertex")
        require(not (neighbours[vertex] & settlements.keys()), "Setup distance rule")
        require(edge not in placed_roads and vertex in edges[edge]["vertices"],
                "Setup road must be empty and incident to its own new settlement")
        settlements[vertex] = player
        pieces[player]["settlements"] -= 1
        if i == 3:
            pending = data["pendingSave"]
            require(pending["afterCommandId"] == "C07" and
                    pending["activePlayer"] == "P4" and pending["phase"] == "SetupRoad",
                    "Pending checkpoint phase")
            require(pending["pendingDecision"] == {"kind": "SetupRoad", "playerId": "P4",
                    "anchorVertexId": vertex, "placementNumber": 4}, "Pending anchor")
            require(pending["remainingPieces"] == pieces, "Pending piece supply")
            require(pending["hands"] == hands and pending["bank"] == bank and
                    pending["rngCursor"] == 0, "Pending resources/random cursor")
            for p in players:
                require(pending["settlements"][p] == [v for v, owner in settlements.items() if owner == p]
                        and pending["roads"][p] == [e for e, owner in placed_roads.items() if owner == p],
                        "Pending board occupancy")
        placed_roads[edge] = player
        pieces[player]["roads"] -= 1
        reward = {r: sum(t["resource"] == r and vertex in t["vertices"] for t in tiles.values())
                  if i >= 4 else 0 for r in resources}
        require(placement["initialResources"] == reward, f"Setup {i+1}: second-settlement reward")
    # The fixture awards all starting resources at the end of setup (C16).
    for placement in setup:
        for resource, amount in placement["initialResources"].items():
            hands[placement["player"]][resource] += amount
            bank[resource] -= amount

    def checkpoint(name, active, turn, phase, cursor):
        expected = data["expected"][name]
        require(expected == dict(hands=hands, bank=bank, remainingPieces=pieces,
                activePlayer=active, turn=turn, phase=phase, pendingDecision=None,
                rngCursor=cursor, publicVictoryPoints=dict.fromkeys(players, 2)),
                f"Checkpoint mismatch: {name}")
        for resource in resources:
            values = [bank[resource]] + [hands[p][resource] for p in players]
            require(all(type(n) is int and n >= 0 for n in values) and sum(values) == 19,
                    f"Resource conservation: {name}/{resource}")
        for player in players:
            require(pieces[player]["settlements"] + list(settlements.values()).count(player) == 5
                    and pieces[player]["roads"] + list(placed_roads.values()).count(player) == 15
                    and pieces[player]["cities"] == 4, f"Piece conservation: {name}/{player}")

    checkpoint("afterSetup", "P1", 1, "ProductionAwaitRoll", 0)
    random = data["controlledRandom"]
    require(random == dict(algorithmId="fixture-dice-queue-v1", dice=[[2, 3], [1, 1]],
                           initialCursor=0, authorityOnly=True), "Controlled dice contract")
    actions = data["actions"]
    require([a["commandId"] for a in actions] == ["C17", "C18", "C19", "C20"], "Action IDs")
    require([a["kind"] for a in actions] == ["RollDice", "BankTrade", "BuildRoad", "EndTurn"]
            and all(a["player"] == "P1" for a in actions), "Action sequence")
    require([a["checkpoint"] for a in actions] == ["afterRoll", "afterTrade", "afterBuild", "afterEnd"],
            "Action checkpoint order")

    def add_production(dice):
        gains = {p: dict.fromkeys(resources, 0) for p in players}
        for tile in tiles.values():
            if tile["number"] == sum(dice) and tile["id"] != data["robberTile"]:
                for vertex in tile["vertices"]:
                    if vertex in settlements:
                        gains[settlements[vertex]][tile["resource"]] += 1
        for player in players:
            for resource, amount in gains[player].items():
                hands[player][resource] += amount
                bank[resource] -= amount
        return gains

    require(actions[0]["expectedDice"] == random["dice"][0], "First roll")
    gains = add_production(random["dice"][0])
    require(gains["P1"]["wood"] == 2 and sum(sum(h.values()) for h in gains.values()) == 2,
            "First roll should yield only two wood to P1")
    checkpoint("afterRoll", "P1", 1, "Action", 1)
    require(actions[1]["give"] == {"wood": 4} and actions[1]["receive"] == {"brick": 1}, "4:1 trade")
    hands["P1"]["wood"] -= 4
    bank["wood"] += 4
    hands["P1"]["brick"] += 1
    bank["brick"] -= 1
    checkpoint("afterTrade", "P1", 1, "Action", 1)
    edge = actions[2]["edge"]
    require(edge == "E65" and edge not in placed_roads, "Paid road target")
    connected = {v for v, owner in settlements.items() if owner == "P1"}
    connected.update(v for e, owner in placed_roads.items() if owner == "P1"
                     for v in edges[e]["vertices"] if settlements.get(v, "P1") == "P1")
    require(connected & set(edges[edge]["vertices"]), "Paid road connectivity")
    for resource, amount in data["roadCost"].items():
        hands["P1"][resource] -= amount
        bank[resource] += amount
    placed_roads[edge] = "P1"
    pieces["P1"]["roads"] -= 1
    checkpoint("afterBuild", "P1", 1, "Action", 1)
    checkpoint("afterEnd", "P2", 2, "ProductionAwaitRoll", 1)
    require(data["restoreProbe"] == dict(commandId="C21", player="P2", kind="RollDice",
            expectedDice=[1, 1], checkpoint="afterRestoreProbe"), "Restore probe contract")
    add_production(random["dice"][1])
    checkpoint("afterRestoreProbe", "P2", 2, "Action", 2)

    # These checks establish authored negative-case preconditions only. They do
    # not send commands, test rejection atomicity, test views, or perform saves.
    negatives = data["negativeCases"]
    require([n["id"] for n in negatives] == [f"N{i:02d}" for i in range(1, 11)],
            "Negative case IDs")
    require(negatives[0]["vertex"] in neighbours[setup[0]["vertex"]], "N01 distance precondition")
    require(setup[3]["vertex"] not in edges[negatives[1]["edge"]]["vertices"], "N02 anchor precondition")
    require(not connected & set(edges[negatives[5]["edge"]]["vertices"]), "N06 disconnected target")
    require(negatives[6]["edge"] in {s["edge"] for s in setup}, "N07 occupied target")
    require(negatives[7]["edge"] not in placed_roads and
            set(edges[negatives[7]["edge"]]["vertices"]) & set(edges[edge]["vertices"]),
            "N08 target should connect but lack resources")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("fixture", nargs="?", type=Path,
                        default=Path(__file__).resolve().parents[2] / "docs/acceptance/v0.1/scenario.json")
    args = parser.parse_args()
    try:
        data = json.loads(args.fixture.read_text(encoding="utf-8"))
        verify(copy.deepcopy(data))
    except (OSError, ValueError, KeyError, TypeError, IndexError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1
    print("PASS: M1 authored fixture: 19 tiles / 54 vertices / 72 edges; "
          "8 setup placements; 6 resource/piece checkpoints; pending-save data consistent.")
    print("NOT RUN: game commands, rejection/retry behavior, PlayerView, save/load, Unity, or user playtest.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
