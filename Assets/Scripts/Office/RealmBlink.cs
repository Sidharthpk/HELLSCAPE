using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Owns the blink and only the controls it disabled. Realm swaps happen at full closure.
public class RealmBlink : MonoBehaviour
{
    public float Closed { get; private set; }
    public bool Active { get; private set; }
    RealmEyelids lids;
    Transform player;
    readonly List<Behaviour> held = new List<Behaviour>();
    public void Initialize(Transform who)
    {
        player = who;
        var canvas = new GameObject("Realm eyelids", typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 420;
        lids = new GameObject("Upper and lower eyelids", typeof(RectTransform)).AddComponent<RealmEyelids>();
        lids.transform.SetParent(canvas.transform, false);
        var rect = lids.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero; lids.raycastTarget = false;
        lids.gameObject.SetActive(false);
    }
    public void Begin()
    {
        if (Active) return;
        Active = true; lids.gameObject.SetActive(true);
        foreach (var b in player.GetComponentsInChildren<Behaviour>(true))
            if ((b is FirstPersonController || b is PunchController || b is GunController || b is PlayerInteractor) && b.enabled)
            { held.Add(b); b.enabled = false; }
        if (player.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }
    public IEnumerator Animate(float target, float duration)
    {
        float from = Closed;
        for (float elapsed = 0f; elapsed < duration; elapsed += Mathf.Min(Time.deltaTime, 1f / 30f))
        {
            Set(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
            yield return null;
        }
        Set(target);
    }
    void Set(float value) { Closed = value; if (lids != null) { lids.closure = value; lids.SetVerticesDirty(); } }
    void LateUpdate() { if (lids != null) lids.enabled = !PauseMenu.Paused; }
    public void Cancel()
    {
        StopAllCoroutines(); Set(0f); Active = false;
        if (lids != null) lids.gameObject.SetActive(false);
        foreach (var b in held) if (b != null) b.enabled = true;
        held.Clear();
    }
    void OnDisable() => Cancel();
}

// Curved lids with a soft inside edge, rather than a rectangular fade-to-black.
[RequireComponent(typeof(CanvasRenderer))]
class RealmEyelids : MaskableGraphic
{
    public float closure;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (closure <= 0f) return;
        Rect rect = rectTransform.rect;
        const int segments = 48;
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < segments; i++)
            {
                float x0 = i / (float)segments, x1 = (i + 1f) / segments;
                float Edge(float x) => side * ((1f - closure) + .1f * (1f - Mathf.Pow(x * 2f - 1f, 2f)) * Mathf.Sin(closure * Mathf.PI));
                float y0 = Edge(x0), y1 = Edge(x1);
                Add(mesh, rect, x0, side, x1, side, x1, y1, x0, y0, 1f, 1f);
                float feather = .015f * Mathf.Sin(closure * Mathf.PI);
                Add(mesh, rect, x0, y0, x1, y1, x1, y1 - side * feather, x0, y0 - side * feather, 1f, 0f);
            }
    }
    static void Add(VertexHelper mesh, Rect rect, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, float outer, float inner)
    {
        int start = mesh.currentVertCount;
        void Vertex(float x, float y, float alpha) => mesh.AddVert(new Vector3(rect.xMin + x * rect.width, rect.center.y + y * rect.height * .5f), new Color(.006f, .002f, .002f, alpha), Vector2.zero);
        Vertex(x0, y0, outer); Vertex(x1, y1, outer); Vertex(x2, y2, inner); Vertex(x3, y3, inner);
        mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
    }
}
