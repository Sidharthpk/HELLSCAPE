using UnityEngine;
using UnityEngine.UI;

// A timer drawn as a smear of blood instead of a number or a rectangle: thick and wet at the left, dragged out to a
// ragged point at the right. It drains from the tail, so what is left is always the heavy end. One generated sprite,
// shared; Make() gives a dark track with the red fill over it (set fillAmount 0..1 on what it returns).
public static class BloodBar
{
    public static readonly Color Red = new Color(.78f, .05f, .04f, 1f);
    static Sprite sprite;

    public static Sprite Sprite
    {
        get
        {
            if (sprite != null) return sprite;
            const int w = 256, h = 40;
            var px = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);
                // half-thickness along the stroke: a blob at the start, a long taper, a wavering edge
                float body = Mathf.Lerp(.42f, .05f, Mathf.Pow(u, 1.6f));
                float blob = .1f * Mathf.Exp(-u * 26f);
                float top = body + blob + .035f * Mathf.Sin(u * 31f) * (1f - u) + .02f * Mathf.Sin(u * 83f + 1.3f);
                float bottom = body + blob * 1.6f + .03f * Mathf.Sin(u * 23f + 2f) * (1f - u) + .025f * Mathf.Sin(u * 67f);
                // a couple of drips hanging off the underside
                bottom += .16f * Mathf.Exp(-Mathf.Pow((u - .13f) * 46f, 2f)) + .1f * Mathf.Exp(-Mathf.Pow((u - .41f) * 60f, 2f));
                // a round, wet head at the left (not a cut edge); the tail just thins away
                float head = u < .07f ? Mathf.Sqrt(1f - Mathf.Pow(1f - u / .07f, 2f)) : 1f;
                float cap = Mathf.Clamp01((1f - u) * 30f);
                for (int y = 0; y < h; y++)
                {
                    float v = (y + .5f) / h - .58f;   // (the stroke sits a little high: room for the drips)
                    float edge = (v > 0f ? top : bottom) * head;
                    float a = Mathf.Clamp01((edge - Mathf.Abs(v)) * h * .9f) * cap;
                    float shade = 1f - .28f * Mathf.Clamp01(Mathf.Abs(v) / Mathf.Max(edge, .01f)) + .12f * Mathf.Clamp01(v * 6f);   // darker rim, a wet highlight on top
                    byte c = (byte)(Mathf.Clamp01(shade) * 255f);
                    px[y * w + x] = new Color32(c, c, c, (byte)(a * 255f));
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px); tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, .5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }

    public static Image Make(Transform parent, string name, Vector2 size)
    {
        var track = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        track.transform.SetParent(parent, false);
        track.sprite = Sprite; track.color = new Color(.09f, 0f, 0f, .6f); track.raycastTarget = false;
        track.rectTransform.sizeDelta = size;
        var fill = new GameObject("Fill", typeof(RectTransform)).AddComponent<Image>();
        fill.transform.SetParent(track.transform, false);
        fill.sprite = Sprite; fill.color = Red; fill.raycastTarget = false;
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        var rt = fill.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return fill;
    }
}
