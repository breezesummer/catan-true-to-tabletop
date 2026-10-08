"""Render the authored fixture as an SVG locator, not game art or a game view."""
import json
import math
from html import escape
from pathlib import Path


def main():
    root = Path(__file__).resolve().parents[2]
    directory = root / "docs/acceptance/v0.1"
    data = json.loads((directory / "scenario.json").read_text(encoding="utf-8"))
    vertices = {v["id"]: v for v in data["topology"]["vertices"]}
    edges = {e["id"]: e for e in data["topology"]["edges"]}
    colors = {"wood": "#d1e5ce", "brick": "#efc9b7", "wool": "#e4edc4",
              "wheat": "#f4e3a5", "ore": "#d4dce6", None: "#eadfc9"}
    player_colors = {"P1": "#a23f2a", "P2": "#1a60a6", "P3": "#754eaa", "P4": "#167a67"}
    out = ['<svg xmlns="http://www.w3.org/2000/svg" width="1400" height="1190" viewBox="0 0 1400 1190">',
           '<rect width="1400" height="1190" fill="#faf9f6"/>',
           '<style>text{font-family:Arial,sans-serif;text-anchor:middle;fill:#233344} '
           '.label{paint-order:stroke;stroke:#faf9f6;stroke-width:4px;stroke-linejoin:round}</style>']

    def text(x, y, value, size=16, extra=""):
        out.append(f'<text x="{x:.2f}" y="{y:.2f}" font-size="{size}" {extra}>{escape(str(value))}</text>')

    def point(vertex):
        v = vertices[vertex]
        return (700 + v["x"] * 110 * math.sqrt(3) / 2, 580 + v["y"] * 55)

    def line(edge, stroke, width, dash=""):
        a, b = (point(v) for v in edges[edge]["vertices"])
        out.append(f'<line x1="{a[0]:.2f}" y1="{a[1]:.2f}" x2="{b[0]:.2f}" y2="{b[1]:.2f}" '
                   f'stroke="{stroke}" stroke-width="{width}" stroke-linecap="round" {dash}/>')

    text(700, 36, "M1 FIXED ACCEPTANCE BOARD", 26, 'font-weight="bold"')
    text(700, 64, "Authored test fixture | after setup C16 | 19 tiles / 54 vertices / 72 edges", 16)
    text(700, 89, "T = tile     V = vertex     E = edge     Colored circles/roads = initial placements", 15)
    for tile in data["topology"]["tiles"]:
        points = [point(v) for v in tile["vertices"]]
        coords = " ".join(f"{x:.2f},{y:.2f}" for x, y in points)
        out.append(f'<polygon points="{coords}" fill="{colors[tile["resource"]]}"/>')
        x, y = (sum(p[axis] for p in points) / 6 for axis in (0, 1))
        text(x, y - 26, tile["id"], 16)
        text(x, y - 4, tile["resource"] or "desert", 15)
        text(x, y + 27, tile["number"] if tile["number"] else "ROBBER", 23, 'font-weight="bold"')
    for edge in edges:
        line(edge, "#8e979b", 1.6)
    for placement in data["setup"]:
        line(placement["edge"], player_colors[placement["player"]], 7)
    line("E65", "#a23f2a", 6, 'stroke-dasharray="9 7"')
    for edge in edges.values():
        a, b = (point(v) for v in edge["vertices"])
        # Offset the label perpendicular to its edge so the geometry stays visible.
        dx, dy = b[0] - a[0], b[1] - a[1]
        length = math.hypot(dx, dy)
        x, y = (a[0] + b[0]) / 2 - dy / length * 12, (a[1] + b[1]) / 2 + dx / length * 12
        text(x, y + 4, edge["id"], 12, 'class="label"')
    occupied = {s["vertex"]: (s["player"], i + 1) for i, s in enumerate(data["setup"])}
    for vertex in vertices:
        x, y = point(vertex)
        if vertex in occupied:
            player, order = occupied[vertex]
            out.append(f'<circle cx="{x:.2f}" cy="{y:.2f}" r="14" fill="{player_colors[player]}"/>')
            text(x, y + 5, order, 14, 'style="fill:white" font-weight="bold"')
        else:
            out.append(f'<circle cx="{x:.2f}" cy="{y:.2f}" r="3.5" fill="#344652"/>')
        text(x, y - 19, vertex, 13, 'class="label"')
    text(700, 1077, "Circle numbers: setup order 1-8 (P1, P2, P3, P4, P4, P3, P2, P1)", 16)
    text(700, 1104, "Dashed E65: paid road built by P1 at C19; absent from the C16 position.", 16)
    text(700, 1131, "Test-only map without ports. This locator is not a mesh, a game screenshot, or the official beginner layout.", 15)
    text(700, 1156, f'{data["scenarioId"]} / {data["scenarioVersion"]}', 13)
    out.append("</svg>")
    destination = directory / "board.svg"
    destination.write_text("\n".join(out) + "\n", encoding="utf-8")
    print(f"Wrote {destination}")


if __name__ == "__main__":
    main()
