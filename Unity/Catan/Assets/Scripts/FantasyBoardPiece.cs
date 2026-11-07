using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 棋盘世界坐标承担落点和缩放。偏离原画观察角度时显示同一根节点的实体棋子。
public sealed class FantasyBoardPiece : MonoBehaviour
{
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static event Action<bool> viewChanged;
    static bool paintedView = true;
    Renderer[] solid;
    MeshRenderer painting;
    MeshFilter filter;
    string kind;
    bool damaged;
    public int Frame {get; private set;}

    public static Color Pigment(string colour)
    {
        if (colour=="red") return new Color(.93f,.18f,.16f);
        if (colour=="orange") return new Color(1,.39f,.055f);
        return new Color(1,.96f,.82f);
    }

    public static void SetView(bool painted)
    {
        if (painted == paintedView) return;
        paintedView = painted;
        viewChanged?.Invoke(painted);
    }

    public static GameObject Attach(GameObject root, string kind, string colour, int frame=0)
    {
        if (root == null || colour == null) return root;
        var key = kind + "-" + colour;
        if (!materials.TryGetValue(key, out var material))
        {
            var texture = Resources.Load<Texture2D>((kind=="road"?"FantasyPieces/":"FantasyPieces/Views/") + kind);
            var shader = Resources.Load<Shader>("FantasyPieces/FantasyInk");
            if (texture == null || shader == null) return root;
            material = new Material(shader) { name = "幻想棋子-" + key, mainTexture = texture };
            material.SetFloat("_Swap", colour == "blue" || colour=="neutral" ? 0 : 1);
            material.SetFloat("_Atlas",FantasyPieceFrames.IsBuilding(kind)||kind=="ship"||kind=="pirate"||kind=="knights"?1:0);
            material.SetColor("_Pigment", Pigment(colour));
            // 建筑覆盖路段端部，船覆盖其尾浪。
            material.renderQueue = kind == "road" ? 3006 : 3010;
            materials.Add(key, material);
        }
        var piece = root.AddComponent<FantasyBoardPiece>();
        piece.kind = kind;
        piece.Frame = frame;
        piece.solid = root.GetComponentsInChildren<Renderer>();
        var plane = new GameObject("幻想棋子画面");
        plane.layer = root.layer;
        plane.transform.position = root.transform.position + Vector3.up * (FantasyPieceFrames.IsBuilding(kind) ? .0022f : .0012f);
        plane.transform.SetParent(root.transform, true);
        piece.filter = plane.AddComponent<MeshFilter>();
        piece.painting = plane.AddComponent<MeshRenderer>();
        piece.painting.sharedMaterial = material;
        piece.painting.shadowCastingMode = ShadowCastingMode.Off;
        piece.painting.receiveShadows = false;
        piece.Align(root.transform.forward);
        piece.Select(paintedView);
        return root;
    }

    public void Align(Vector3 forward)
    {
        bool directional=FantasyPieceFrames.IsBuilding(kind)||kind=="ship"||kind=="pirate"||kind=="road";
        if(directional)Frame=FantasyPieceFrames.Heading(forward);
        painting.transform.rotation = kind=="road"
            ? Quaternion.LookRotation(forward)*Quaternion.Euler(0,-90,0) : Quaternion.identity;
        filter.sharedMesh=FantasyPieceFrames.Get(kind,Frame,painting.sharedMaterial.mainTexture);
    }

    internal void SetDamaged(bool value)
    {
        damaged=value;
        Select(paintedView);
    }

    void Select(bool painted)
    {
        if (painting == null) return;
        painted &= !damaged;
        painting.enabled = painted;
        foreach (var part in solid) part.forceRenderingOff = painted;
    }
    void OnEnable() { viewChanged += Select; Select(paintedView); }
    void OnDisable() { viewChanged -= Select; }
}
