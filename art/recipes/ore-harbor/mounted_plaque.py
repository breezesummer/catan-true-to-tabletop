"""把原矿港的切角漆牌、铜边与铸字装到各自屋坡，浮雕保留原港的图集。"""
from mathutils import Vector
from exposure import bake
from roof_sign import SIGN, SIGN_W, X, S, N, lettering, plaque_body
from surfaces_ore import surface


def trade_plaque(scene, mount, width, emblem=None):
    material = surface()
    body = plaque_body().object(scene, material)
    letters = lettering(scene, material, "2:1" if emblem else "3:1")
    scale = width / SIGN_W
    for obj in (body, letters):
        # 普通港收窄牌身，铜字保留矿石港字模的实际尺寸。
        plane_scale = scale if obj == body else 1.0
        for vertex in obj.data.vertices:
            delta = vertex.co * 1000 - SIGN
            vertex.co = mount(delta.dot(X)*plane_scale, delta.dot(S)*plane_scale, delta.dot(N)*scale) * .001
        obj.data.update()

    parts = [body, letters]
    if emblem:
        center = mount(0, 0, 0)
        axes = [mount(1, 0, 0)-center, mount(0, 1, 0)-center, mount(0, 0, 1)-center]
        coords = [tuple((vertex.co*1000-center).dot(axis) for axis in axes)
                  for vertex in emblem.data.vertices]
        (xmin, xmax), (ymin, ymax), (base, _) = [(min(values), max(values)) for values in zip(*coords)]
        fit = min(4.26/(xmax-xmin), 2.92/(ymax-ymin))
        for vertex, (x, y, relief) in zip(emblem.data.vertices, coords):
            u = 3.42 + (x-(xmin+xmax)/2)*fit
            v = (y-(ymin+ymax)/2)*fit
            h = .105 + (relief-base)*fit
            vertex.co = mount(u*scale, v*scale, h*scale) * .001
        emblem.data.uv_layers[0].name = "HarborUV"
        emblem.data.update()
        parts.insert(0, emblem)

    bake(parts)
    return parts

def bind_preview(obj, house, ore):
    """摄影回读时将 FBX 的每个材质槽重新绑定到母版内嵌图集。"""
    plaque = obj.name == "TradePlaque"
    assert len(obj.data.uv_layers) == (2 if plaque else 1)
    materials = {m.name: m for m in (house, ore)}
    names = [slot.name.split(".")[0] for slot in obj.data.materials]
    expected = {ore.name} if plaque else {house.name}
    if plaque and house.name != "CommonHarbor":
        expected.add(house.name)
    assert set(names) == expected, (obj.name, names)
    for index, name in enumerate(names):
        obj.data.materials[index] = materials[name]
