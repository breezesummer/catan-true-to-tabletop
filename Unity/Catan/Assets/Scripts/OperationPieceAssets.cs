using System.Collections.Generic;
using UnityEngine;

// Transparent thumbnails rendered from the retained physical-piece meshes.
// Runtime never creates a camera, changes layers, or reads hidden game state.
public static class OperationPieceAssets
{
    const string Root = "OperationUI/v001/pieces/";
    static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

    public static Texture2D Texture(string icon, string seat, int level = 1)
    {
        var colour = PlayerComponentAssets.ColourKey(seat);
        if (colour == null || string.IsNullOrEmpty(icon)) return null;
        icon = icon.ToLowerInvariant();
        string kind;
        if (icon.Contains("road")) kind = "road";
        else if (icon.Contains("ship")) kind = "ship";
        else if (icon.Contains("knight")) kind = "knight-" + Mathf.Clamp(level, 1, 3);
        else if (icon.Contains("wall")) kind = "wall";
        else if (icon.Contains("city")) kind = "city";
        else if (icon.Contains("settlement") || icon.Contains("village")) kind = "settlement";
        else return null;
        string path = Root + kind + "-" + colour;
        if (!Textures.TryGetValue(path, out var texture))
        {
            texture = Resources.Load<Texture2D>(path);
            Textures[path] = texture;
        }
        return texture;
    }
}
