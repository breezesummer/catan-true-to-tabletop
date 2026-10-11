using System;
using UnityEngine;

// Display-only UV crops. The photographed symbols never determine a price or an entitlement.
// Source provenance and top-left crop bounds: art/recipes/ui-operation-v001/sources.json.
public static class OperationIconAssets
{
    // Keep this order identical to EntityCardAssets.Good and the PlayerView goods arrays.
    static readonly string[] Names = { "木", "砖", "羊", "麦", "矿", "布", "币", "纸" };
    static readonly Rect[] CropsFromTopLeft = {
        new Rect(.155f, .310f, .675f, .350f),
        new Rect(.195f, .365f, .605f, .250f),
        new Rect(.220f, .305f, .555f, .340f),
        new Rect(.225f, .295f, .565f, .440f),
        new Rect(.190f, .360f, .600f, .270f),
        new Rect(.108f, .590f, .295f, .295f),
        new Rect(.075f, .625f, .300f, .295f),
        new Rect(.078f, .625f, .300f, .295f)
    };
    static GUIStyle fallbackStyle;

    public static Texture2D GoodTexture(int good)
    {
        ValidateGood(good);
        // A single commodity symbol on each level-one cost panel is sharper than the
        // same symbol on the photographed composite city-improvement guidance board.
        return good < 5 ? EntityCardAssets.Texture(EntityCardAssets.Good(good).Front)
            : EntityCardAssets.ImprovementFront(good - 5, 1);
    }

    public static Rect GoodUv(int good)
    {
        ValidateGood(good);
        var crop = CropsFromTopLeft[good];
        return new Rect(crop.x, 1f - crop.yMax, crop.width, crop.height);
    }

    // Use on a square or near-square GUI rect, at least 24 logical pixels wide.
    // Existing goods retain full colour. Missing goods are dimmed with a red border
    // and a clear minus marker, so the distinction does not depend only on colour.
    public static void DrawGood(Rect rect, int good, bool missing = false)
    {
        ValidateGood(good);
        var oldColor = GUI.color;
        var border = missing ? new Color(.91f, .35f, .28f, 1f) : new Color(.69f, .61f, .39f, 1f);
        GUI.color = oldColor * border;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        var inner = new Rect(rect.x + 1.5f, rect.y + 1.5f, Mathf.Max(0, rect.width - 3), Mathf.Max(0, rect.height - 3));
        GUI.color = oldColor * new Color(.13f, .17f, .17f, 1f);
        GUI.DrawTexture(inner, Texture2D.whiteTexture);

        var texture = GoodTexture(good);
        if (texture != null && inner.width > 0 && inner.height > 0)
        {
            var uv = GoodUv(good);
            float aspect = texture.width * uv.width / (texture.height * uv.height);
            float width = Mathf.Min(inner.width, inner.height * aspect);
            float height = width / aspect;
            var content = new Rect(inner.center.x - width / 2f, inner.center.y - height / 2f, width, height);
            GUI.color = oldColor * (missing ? new Color(.48f, .48f, .48f, .8f) : Color.white);
            GUI.DrawTextureWithTexCoords(content, texture, uv, true);
        }
        else
        {
            if (fallbackStyle == null)
                fallbackStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.color = oldColor;
            GUI.Label(inner, Names[good], fallbackStyle);
        }

        if (missing)
        {
            float size = Mathf.Clamp(rect.width * .35f, 9f, 15f);
            var badge = new Rect(rect.xMax - size, rect.yMax - size, size, size);
            GUI.color = oldColor * border;
            GUI.DrawTexture(badge, Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.DrawTexture(new Rect(badge.x + size * .22f, badge.center.y - 1f, size * .56f, 2f), Texture2D.whiteTexture);
        }
        GUI.color = oldColor;
    }

    static void ValidateGood(int good)
    {
        if (good < 0 || good >= CropsFromTopLeft.Length) throw new ArgumentOutOfRangeException(nameof(good));
    }
}
