using System.Collections.Generic;
using UnityEngine;

// Presentation-only resources. No GameState, rule mutations or private-opponent data.
public static class PhotoSampleAssets
{
    const string Root = "PhotoSamples/v001/";
    static readonly Dictionary<string, GameObject> Models = new Dictionary<string, GameObject>();
    public static GameObject Model(string id)
    {
        if (!Models.TryGetValue(id, out var model) || model == null)
        {
            model = Resources.Load<GameObject>(Root + id);
            if (model != null) Models[id] = model;
        }
        return model;
    }
    public static Texture2D Front => Resources.Load<Texture2D>(Root + "wood-front");
    public static Texture2D Back => Resources.Load<Texture2D>(Root + "resource-back");
    public static GameObject Forest(Vector3 position, Transform parent)
    {
        return Object.Instantiate(Model("forest"), position, Quaternion.identity, parent);
    }
    public static GameObject Piece(bool road, string label, Vector3 bottom, Quaternion rotation, Color color, Transform parent)
    {
        var model = Model(road ? "road" : "village");
        if (model == null) return null;
        var instance = Object.Instantiate(model, bottom, rotation, parent);
        instance.name = label;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(block);
        return instance;
    }
    public static bool WoodBadge(Rect rect, int count, GUIStyle style)
    {
        var face = Front;
        if (face == null) return false;
        GUI.DrawTexture(rect, face, ScaleMode.ScaleToFit);
        var strip = new Rect(rect.x, rect.yMax - 23, rect.width, 23);
        var old = GUI.color;
        GUI.color = new Color(.025f, .06f, .07f, .95f);
        GUI.DrawTexture(strip, Texture2D.whiteTexture);
        GUI.color = old;
        GUI.Label(strip, "木 " + count, style);
        return true;
    }
}
