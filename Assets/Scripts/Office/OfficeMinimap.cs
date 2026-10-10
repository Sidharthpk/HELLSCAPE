using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// The office floor plan: a round minimap at the top right with you as the red dot in its middle, and M for the whole
// floor. The plan is the office navmesh (the floor you can walk on) drawn once from above, so it follows the model:
// nothing to redraw by hand when the rooms change. Added by OfficeHorrorDirector, so only the office scene has it.
public class OfficeMinimap : MonoBehaviour
{
    public static KeyCode Key = KeyCode.M;
    const float Size = 220f, Range = 15f;   // the circle (pixels of a 1080 screen) and the metres from its middle to its rim
    const float FullWidth = 1300f;

    static readonly Color Floor = new Color(.78f, .7f, .66f, .9f), Rim = new Color(.35f, .03f, .03f, 1f), Back = new Color(.03f, .01f, .01f, .85f), You = new Color(1f, .08f, .08f, 1f);

    static readonly Color Ink = new Color(.9f, .84f, .78f, 1f), Drawn = new Color(.22f, .13f, .09f, .9f);   // the names; the rooms inked on the paper map

    // the names on the whole-floor map: metres east and north of the middle of the office (the room map in NewOfficeFit)
    public static readonly (string name, float x, float z)[] Rooms =   // (InteriorDarkness hangs an emergency lamp over each)
    {
        ("ENTRANCE\nLOBBY", -33.5f, -22.9f), ("RECEPTION", -44f, -7.4f), ("STORAGE", -10f, -8.9f), ("OPEN OFFICE", -20.5f, 6.6f),
        ("KITCHEN /\nBREAK ROOM", -29.5f, 21.6f), ("CONFERENCE\nROOM", -6.5f, 29.6f), ("RECORDS\nARCHIVE", 17f, 17.1f), ("SECURITY\nROOM", 8f, -4.9f),
        ("VANE'S\nOFFICE", 35f, 5.1f), ("SERVICE\nCORRIDOR", 24.5f, -11.1f), ("TO SILO", 43f, -10.7f),
    };

    Transform player; Bounds zone;
    GameObject mini, full; RectTransform miniMap, fullDot;
    bool open;

    public void Init(Transform body, Bounds office) { player = body; zone = office; }

    // (Start, not Init: the navmesh of the scene is loaded by then)
    void Start()
    {
        var plan = Plan(zone);
        var disc = Disc();
        float aspect = zone.size.z / zone.size.x;

        var canvas = new GameObject("Office minimap", typeof(Canvas), typeof(CanvasScaler));
        canvas.transform.SetParent(transform, false);
        var c = canvas.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 150;   // over the HUD (100), under the story canvas (200)
        var s = canvas.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920f, 1080f); s.matchWidthOrHeight = 1f;

        var rim = Make<Image>("Minimap", canvas.transform, new Vector2(Size + 10f, Size + 10f)); rim.sprite = disc; rim.color = Rim;
        rim.rectTransform.anchorMin = rim.rectTransform.anchorMax = rim.rectTransform.pivot = Vector2.one;
        rim.rectTransform.anchoredPosition = new Vector2(-30f, -110f);   // under the objective strip (90)
        mini = rim.gameObject;
        var face = Make<Image>("Face", mini.transform, new Vector2(Size, Size)); face.sprite = disc; face.color = Back;
        face.gameObject.AddComponent<Mask>();
        var map = Make<RawImage>("Plan", face.transform, zone.size.x * Size / (2f * Range) * new Vector2(1f, aspect)); map.texture = plan; map.color = Floor;
        miniMap = map.rectTransform;
        var dot = Make<Image>("You", mini.transform, new Vector2(12f, 12f)); dot.sprite = disc; dot.color = You;

        var dim = Make<Image>("Full map", canvas.transform, Vector2.zero); dim.color = Back;
        dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
        full = dim.gameObject;
        var paper = Make<RawImage>("Paper", full.transform, FullWidth * new Vector2(1f, aspect) + new Vector2(170f, 150f)); paper.texture = Paper();
        paper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -1.5f);   // lying a little crooked, like the notes
        var whole = Make<RawImage>("Plan", paper.transform, FullWidth * new Vector2(1f, aspect)); whole.texture = plan; whole.color = Drawn;
        var font = Resources.Load<TMP_FontAsset>("NotePixelFont");
        foreach (var (name, x, z) in Rooms)
        {
            // (a dark plate under each name: the plan is pale floor and black wall, no one colour reads on both)
            var plate = Make<Image>(name, whole.transform, Vector2.zero); plate.color = Back;
            plate.rectTransform.anchoredPosition = new Vector2(x, z) * (FullWidth / zone.size.x);
            var label = Make<TextMeshProUGUI>("Name", plate.transform, new Vector2(240f, 60f));
            if (font != null) label.font = font;
            label.text = name; label.fontSize = 18f; label.alignment = TextAlignmentOptions.Center; label.color = Ink;
            plate.rectTransform.sizeDelta = label.GetPreferredValues() + new Vector2(12f, 6f);
        }
        var you = Make<Image>("You", whole.transform, new Vector2(16f, 16f)); you.sprite = disc; you.color = You;
        fullDot = you.rectTransform;

        mini.SetActive(false); full.SetActive(false);
    }

    void Update()
    {
        if (mini == null) return;
        bool show = player != null && player.gameObject.activeInHierarchy && OfficeHorrorDirector.In(player.position)
            && !CutsceneHUD.Hidden && !TitleScreen.Showing && !GameOverScreen.Open && !EndingSequence.Playing && !NoteViewer.Blocking;
        if (!show) open = false;
        else if (!PauseMenu.Paused && Input.GetKeyDown(Key)) open = !open;
        if (mini.activeSelf != (show && !open)) mini.SetActive(show && !open);
        if (full.activeSelf != (show && open)) full.SetActive(show && open);
        if (!show) return;

        var at = new Vector2(player.position.x - zone.center.x, player.position.z - zone.center.z);
        miniMap.anchoredPosition = -at * (Size / (2f * Range));   // the plan slides under the dot
        fullDot.anchoredPosition = at * (FullWidth / zone.size.x);
    }

    static T Make<T>(string name, Transform parent, Vector2 size) where T : Graphic
    {
        var g = new GameObject(name, typeof(RectTransform)).AddComponent<T>();
        g.transform.SetParent(parent, false);
        g.rectTransform.sizeDelta = size;
        g.raycastTarget = false;
        return g;
    }

    // the walkable floor inside the zone, seen from above (north up), white on clear
    static Texture Plan(Bounds zone)
    {
        var nav = NavMesh.CalculateTriangulation();
        var verts = new Vector3[nav.vertices.Length];
        for (int i = 0; i < verts.Length; i++)
            verts[i] = new Vector3((nav.vertices[i].x - zone.min.x) / zone.size.x, (nav.vertices[i].z - zone.min.z) / zone.size.z, 0f);
        var tris = new List<int>();
        for (int i = 0; i < nav.indices.Length; i += 3)   // (the scene's other navmeshes, the roads and the silo, are left out)
            if (zone.Contains((nav.vertices[nav.indices[i]] + nav.vertices[nav.indices[i + 1]] + nav.vertices[nav.indices[i + 2]]) / 3f))
            { tris.Add(nav.indices[i]); tris.Add(nav.indices[i + 1]); tris.Add(nav.indices[i + 2]); }
        var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = verts };
        mesh.SetTriangles(tris, 0);

        // ponytail: a RenderTexture loses its picture if the graphics device is lost (rare); copy to a Texture2D if that ever shows
        var plan = new RenderTexture(1024, Mathf.RoundToInt(1024f * zone.size.z / zone.size.x), 0) { name = "Office plan" };
        var ink = new Material(Shader.Find("Sprites/Default"));
        var before = RenderTexture.active;
        RenderTexture.active = plan;
        GL.Clear(true, true, Color.clear);
        GL.PushMatrix(); GL.LoadOrtho();
        ink.SetPass(0);
        Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
        GL.PopMatrix();
        RenderTexture.active = before;
        Destroy(ink); Destroy(mesh);
        return plan;
    }

    // the sheet the whole-floor map is drawn on: stained parchment folded in six, scorched ragged edges, one blot of
    // blood (in chunky pixels, like the rest of the UI)
    static Texture Paper()
    {
        const int w = 368, h = 244;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Worn map", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = Color.Lerp(new Color(.8f, .7f, .5f), new Color(.6f, .48f, .31f), Mathf.PerlinNoise(x * .021f + 7f, y * .021f + 3f));
                c *= .94f + Mathf.PerlinNoise(x * .6f, y * .6f) * .1f;   // grain
                c = Color.Lerp(c, new Color(.42f, .31f, .18f), Mathf.InverseLerp(.58f, .74f, Mathf.PerlinNoise(x * .045f + 40f, y * .045f + 90f)) * .55f);   // water marks
                float blot = Vector2.Distance(new Vector2(x, y), new Vector2(w * .84f, h * .2f)) + Mathf.PerlinNoise(x * .12f, y * .12f) * 18f;
                c = Color.Lerp(c, new Color(.33f, .03f, .03f), Mathf.InverseLerp(34f, 20f, blot) * .85f);
                if (x == w / 3 || x == 2 * w / 3 || y == h / 2) c *= .72f;   // the folds: a dark crease with a worn, pale side
                else if (x == w / 3 + 1 || x == 2 * w / 3 + 1 || y == h / 2 - 1) c *= 1.1f;
                float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                float torn = 2f + Mathf.PerlinNoise(x * .13f + 11f, y * .13f + 5f) * 9f;
                c *= Mathf.Lerp(.35f, 1f, Mathf.Clamp01((edge - torn) / 16f));   // scorched towards the edge
                c.a = edge < torn ? 0f : 1f;
                px[y * w + x] = c;
            }
        t.SetPixels(px); t.Apply();
        return t;
    }

    static Sprite Disc()
    {
        const int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Disc" };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(n / 2f - Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(n / 2f, n / 2f))));
        t.SetPixels(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f));
    }
}
