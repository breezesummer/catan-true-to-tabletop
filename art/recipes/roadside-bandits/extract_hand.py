"""从 MakeHuman HM08 的 CC0 基网和配套骨骼提取一只手。配方可直接读取随附的裁片。"""
from collections import Counter
from pathlib import Path
import json
import numpy as np


def extract_hand(raw, dest):
    raw = Path(raw)
    vertices, body = [], []
    group = ""
    for line in (raw / "base.obj").read_text(encoding="utf-8").splitlines():
        word = line.split()
        if not word:
            continue
        if word[0] == "v":
            vertices.append(tuple(map(float, word[1:4])))
        elif word[0] == "g":
            group = word[1]
        elif word[0] == "f" and group == "body":
            body.append([int(item.split("/")[0])-1 for item in word[1:]])
    xyz = np.asarray(vertices)
    rig = json.loads((raw / "default.mhskel").read_text(encoding="utf-8"))
    weight_file = json.loads((raw / "default_weights.mhw").read_text(encoding="utf-8"))
    assert rig["license"] == weight_file["license"] == "CC0"
    names = ["wrist.R"] + [f"metacarpal{i}.R" for i in range(1, 5)]
    names += [f"finger{i}-{j}.R" for i in range(1, 6) for j in range(1, 4)]
    bones = []
    parents = []
    for name in names:
        bone = rig["bones"][name]
        bones.append([xyz[rig["joints"][bone[end]]].mean(axis=0) for end in ("head", "tail")])
        parents.append(names.index(bone["parent"]) if bone["parent"] in names else -1)
    bones = np.asarray(bones)
    wrist = bones[0, 0]
    longitudinal = bones[names.index("finger3-1.R"), 0] - wrist
    longitudinal /= np.linalg.norm(longitudinal)
    # 两个前臂环之间截取，保留天然的二十边腕环和整片指蹼、甲床。
    finger_side = ((xyz-wrist) @ longitudinal > -.093) & (xyz[:, 0] < -3.37)
    finger_side &= np.linalg.norm(xyz-wrist, axis=1) < 2.55
    faces = np.asarray(body)
    faces = faces[finger_side[faces].all(axis=1)]
    indices, inverse = np.unique(faces, return_inverse=True)
    faces = inverse.reshape(faces.shape)
    edges = Counter(tuple(sorted((int(a), int(b)))) for face in faces for a,b in zip(face, np.roll(face,-1)))
    boundary = [(a,b) for (a,b), count in edges.items() if count == 1]
    rim = [min(min(edge) for edge in boundary)]
    while len(rim) < len(boundary):
        adjacent = [b if a == rim[-1] else a for a,b in boundary if rim[-1] in (a,b)]
        rim.append(next(point for point in adjacent if point not in rim))
    assert len(rim) == 20 and max(edges.values()) == 2
    weights = np.zeros((len(xyz), len(names)))
    for j, name in enumerate(names):
        for vertex, strength in weight_file["weights"][name]:
            weights[vertex, j] = strength
    weights = weights[indices]
    weights[:, 0] += np.maximum(0, 1 - weights.sum(axis=1))
    weights /= weights.sum(axis=1)[:,None]
    np.savez_compressed(dest, vertices=xyz[indices].astype("f4"), faces=faces.astype("i2"),
                        bones=bones.astype("f4"), parents=np.array(parents,"i1"), names=np.array(names),
                        weights=weights.astype("f4"), rim=np.array(rim,"i2"))
    return {"手部顶点":len(indices), "四边面":len(faces), "腕环":len(rim)}
