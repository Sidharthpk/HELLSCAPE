using UnityEngine;
using UnityEngine.UI;

// A resolution-independent panel with clipped corners. No texture or borrowed game artwork required.
[AddComponentMenu("")]
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class DialoguePanelGraphic : MaskableGraphic
{
    public Color edge = new Color(0.66f, 0.53f, 0.32f, 0.65f);
    public float corner = 12f;
    public float thickness = 1.5f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        Polygon(vh, r, edge, corner);
        float inset = Mathf.Min(thickness, Mathf.Min(r.width, r.height) * 0.25f);
        r = new Rect(r.x + inset, r.y + inset, r.width - inset * 2f, r.height - inset * 2f);
        Polygon(vh, r, color, Mathf.Max(0f, corner - inset));
    }

    static void Polygon(VertexHelper vh, Rect r, Color tint, float cut)
    {
        cut = Mathf.Min(cut, Mathf.Min(r.width, r.height) * 0.3f);
        int first = vh.currentVertCount;
        var points = new[] {
            new Vector2(r.xMin + cut, r.yMin), new Vector2(r.xMax, r.yMin),
            new Vector2(r.xMax, r.yMax - cut), new Vector2(r.xMax - cut, r.yMax),
            new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin + cut)
        };
        foreach (var p in points) vh.AddVert(p, tint, Vector2.zero);
        for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(first, first + i + 1, first + i);
    }
}
