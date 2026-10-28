using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// The office floor plan: a round minimap at the top right with you as a red arrow (pointing the way you look) in its
// middle, and M for the whole floor. The name of the room you are in sits at the left of the screen. The plan is the office navmesh (the floor you can walk on) drawn once from above, so it follows the model:
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
        ("ENTRANCE\nLOBBY", -.2f, -19.4f), ("RECEPTION", 0f, -9.6f), ("LOUNGE", -25.4f, -11.2f), ("ARMORY", 25.4f, -12.2f),
        ("KESSLER'S\nOFFICE", 25.4f, -5.1f), ("VANE'S\nOFFICE", 25.4f, 2.3f), ("MAINTENANCE", 25.7f, 12.8f), ("BOARDROOM", -25.4f, .5f),
        ("SECURITY\nROOM", 17.7f, 12f), ("ARCHIVE", 11.3f, 10.1f), ("PANTRY", -4.9f, 10.6f), ("TO SILO", -4.5f, 17.7f),
        ("RESTROOMS", -23.2f, 11.2f), ("OPEN OFFICE", .1f, .3f), ("MAIN HALL", -12.2f, -12.3f),
    };

    // the rooms of the model (the v3 office, NewOfficeFit's plan): world x0, x1, z0, z1 -> the name shown at the left
    static readonly (float x0, float x1, float z0, float z1, string name)[] Floors =
    {
        (974.1f, 990f, 870.2f, 875.6f, "ENTRANCE LOBBY"), (974.4f, 989.8f, 880.2f, 885.2f, "RECEPTION"), (952.4f, 961.2f, 877f, 885.2f, "LOUNGE"),
        (1003.3f, 1011.8f, 877.1f, 883.1f, "ARMORY"), (1003.3f, 1011.8f, 883.4f, 891f, "KESSLER'S OFFICE"), (1003.3f, 1011.8f, 891.3f, 897.9f, "VANE'S OFFICE"),
        (1003.9f, 1011.8f, 901.8f, 908.4f, "MAINTENANCE"), (952.4f, 961.2f, 885.3f, 900.3f, "BOARDROOM"), (996f, 1003.7f, 900.1f, 908.4f, "SECURITY ROOM"),
        (991.3f, 995.7f, 900.1f, 904.6f, "ARCHIVE"), (969.4f, 985.2f, 900.1f, 905.6f, "PANTRY"), (969.4f, 985.9f, 905.9f, 914.2f, "LOADING / TO SILO"),
        (985.9f, 996.4f, 908.4f, 911.4f, "LOADING / TO SILO"), (952.4f, 965.5f, 900.5f, 906.5f, "RESTROOMS"), (961.2f, 1003.3f, 885.2f, 900f, "OPEN OFFICE"),
        (961.2f, 1003.3f, 875.6f, 885.2f, "MAIN HALL"), (1003.3f, 1011.8f, 898f, 901.8f, "WEST CORRIDOR"),
    };

    public static string RoomAt(Vector3 p)
    {
        foreach (var f in Floors)
            if (p.x >= f.x0 && p.x <= f.x1 && p.z >= f.z0 && p.z <= f.z1) return f.name;
        // (doorways and wall thickness between floors: the nearest one)
        string best = null; float bd = 4f;
        foreach (var f in Floors)
        {
            float dx = Mathf.Max(f.x0 - p.x, 0f, p.x - f.x1), dz = Mathf.Max(f.z0 - p.z, 0f, p.z - f.z1);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < bd) { bd = d; best = f.name; }
        }
        return best;
    }

    Transform player; Bounds zone;
    GameObject mini, full; RectTransform miniMap, fullDot, miniArrow;
    TextMeshProUGUI roomText; string room; float roomShown;
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
        rim.rectTransform.anchoredPosition = new Vector2(-30f, -110f);   // under the Echo label
        mini = rim.gameObject;
        var face = Make<Image>("Face", mini.transform, new Vector2(Size, Size)); face.sprite = disc; face.color = Back;
        face.gameObject.AddComponent<Mask>();
        var map = Make<RawImage>("Plan", face.transform, zone.size.x * Size / (2f * Range) * new Vector2(1f, aspect)); map.texture = plan; map.color = Floor;
        miniMap = map.rectTransform;
        var arrowSprite = Arrow();
        var dot = Make<Image>("You", mini.transform, new Vector2(30f, 30f)); dot.sprite = arrowSprite; dot.color = You;
        miniArrow = dot.rectTransform;

        // the room you are in, at the left
        var rt = Make<TextMeshProUGUI>("Room", canvas.transform, new Vector2(520f, 60f));
        rt.rectTransform.anchorMin = rt.rectTransform.anchorMax = rt.rectTransform.pivot = new Vector2(0f, .5f);
        rt.rectTransform.anchoredPosition = new Vector2(40f, 120f);
        if (GameFont.Main != null) rt.font = GameFont.Main;
        rt.fontSize = 30f; rt.alignment = TextAlignmentOptions.Left; rt.color = Ink; rt.textWrappingMode = TextWrappingModes.NoWrap;
        roomText = rt;

        var dim = Make<Image>("Full map", canvas.transform, Vector2.zero); dim.color = Back;
        dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
        full = dim.gameObject;
        var paper = Make<RawImage>("Paper", full.transform, FullWidth * new Vector2(1f, aspect) + new Vector2(170f, 150f)); paper.texture = Paper();
        paper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -1.5f);   // lying a little crooked, like the notes
        var whole = Make<RawImage>("Plan", paper.transform, FullWidth * new Vector2(1f, aspect)); whole.texture = plan; whole.color = Drawn;
        var font = GameFont.Main;
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
        var you = Make<Image>("You", whole.transform, new Vector2(34f, 34f)); you.sprite = arrowSprite; you.color = You;
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
        if (!show) { roomText.gameObject.SetActive(false); return; }

        // the arrow points the way you look (north is up on the plan; the body turns with the look)
        var facing = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
        miniArrow.localRotation = facing; fullDot.localRotation = facing;

        string now = RoomAt(player.position);
        if (now != null && now != room) { room = now; roomShown = Time.unscaledTime; roomText.text = room; }
        // full for 3 seconds on entering a room, then fades out over 1.5s
        var ink = Ink; ink.a = Mathf.Clamp01(1f - (Time.unscaledTime - roomShown - 3f) / 1.5f);
        roomText.color = ink;
        roomText.gameObject.SetActive(!open && room != null);

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

    // an arrowhead pointing up (turned to the way you look)
    internal static Sprite Arrow()
    {
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Arrow", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        Vector2 tip = new Vector2(32f, 62f), l = new Vector2(8f, 6f), notch = new Vector2(32f, 20f), r = new Vector2(56f, 6f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(x + .5f, y + .5f);
                px[y * n + x] = In(p, tip, l, notch) || In(p, tip, notch, r) ? Color.white : Color.clear;
            }
        t.SetPixels(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f));
    }

    static bool In(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
    }

    internal static Sprite Disc()
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
