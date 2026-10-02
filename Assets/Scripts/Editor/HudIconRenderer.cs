using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > UI > Render HUD Icons: the heart and the van on the HUD are not drawings, they're photographs of the
// game's own models, taken here on an empty stage with their own lights and saved as smooth sprites:
//   Heart_healthy / hurt / critical.png   the anatomical heart (HeartModel), lit warm, then cold, then from below
//   VanIcon_0..4.png                      the Kessler & Vane van in each of its five paint states (VanDamageLook)
// Needs a scene with the van open (either one). The sprites keep their GUIDs, so the HUD picks them up as they are.
public static class HudIconRenderer
{
    const string Out = "Assets/UI/Modern/";
    const string VanTex = "Assets/New Folder/stylized-pixel-fiat-ducato-gen-i-jacex/textures/";
    static Scene stage;
    static Camera cam;
    static readonly List<Object> temp = new List<Object>();

    [MenuItem("HellScape/UI/Render HUD Icons (heart, van)")]
    public static void RenderAll()
    {
        string heart = Heart();
        string van = Van();
        AssetDatabase.Refresh();
        foreach (var f in Directory.GetFiles(Out, "Heart_*.png").Concat(Directory.GetFiles(Out, "VanIcon_*.png")))
            ModernUIBuilder.Picture(Path.GetFileName(f), false);
        Debug.Log("HUD icons: " + heart + "; " + van);
    }

    // ---------------------------------------------------------------- the stage

    static void Open()
    {
        stage = EditorSceneManager.NewPreviewScene();
        var go = new GameObject("Icon camera");
        SceneManager.MoveGameObjectToScene(go, stage);
        cam = go.AddComponent<Camera>();
        cam.scene = stage;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f;
        cam.allowHDR = false;
        cam.enabled = false;
    }

    static void Close()
    {
        EditorSceneManager.ClosePreviewScene(stage);
        foreach (var o in temp) if (o != null) Object.DestroyImmediate(o);
        temp.Clear();
    }

    static GameObject Put(GameObject g) { SceneManager.MoveGameObjectToScene(g, stage); return g; }

    static Light Lamp(string name, LightType type, Vector3 at, Vector3 lookAt, Color colour, float intensity, float range = 30f)
    {
        var l = Put(new GameObject(name)).AddComponent<Light>();
        l.type = type; l.color = colour; l.intensity = intensity; l.range = range;
        l.shadows = LightShadows.None;
        l.transform.position = at;
        l.transform.rotation = Quaternion.LookRotation(lookAt - at);
        return l;
    }

    static void Frame(Bounds b, Vector3 from, float fov)
    {
        cam.fieldOfView = fov;
        float dist = b.extents.magnitude / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad);
        cam.transform.position = b.center + from.normalized * dist;
        cam.transform.rotation = Quaternion.LookRotation(b.center - cam.transform.position);
    }

    static Color32[] Shot(Color background, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        cam.backgroundColor = background;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        var px = tex.GetPixels32();
        Object.DestroyImmediate(tex); Object.DestroyImmediate(rt);
        return px;
    }

    // the subject cut out of its background: shot once on black and once on white, the difference is the alpha
    // (works whatever the pipeline does to the camera's own alpha)
    static Color32[] Matte(int w, int h)
    {
        var b = Shot(Color.black, w, h);
        var wt = Shot(Color.white, w, h);
        var o = new Color32[b.Length];
        for (int i = 0; i < b.Length; i++)
        {
            float a = 1f - ((wt[i].r - b[i].r) + (wt[i].g - b[i].g) + (wt[i].b - b[i].b)) / (3f * 255f);
            a = Mathf.Clamp01(a);
            float k = a > 0.004f ? 1f / a : 0f;
            o[i] = new Color32((byte)Mathf.Min(255f, b[i].r * k), (byte)Mathf.Min(255f, b[i].g * k), (byte)Mathf.Min(255f, b[i].b * k), (byte)(a * 255f));
        }
        return o;
    }

    static void Grade(Color32[] px, float saturation, Color multiply)
    {
        for (int i = 0; i < px.Length; i++)
        {
            float r = px[i].r, g = px[i].g, b = px[i].b, grey = r * 0.3f + g * 0.59f + b * 0.11f;
            r = (grey + (r - grey) * saturation) * multiply.r;
            g = (grey + (g - grey) * saturation) * multiply.g;
            b = (grey + (b - grey) * saturation) * multiply.b;
            px[i] = new Color32((byte)Mathf.Clamp(r, 0f, 255f), (byte)Mathf.Clamp(g, 0f, 255f), (byte)Mathf.Clamp(b, 0f, 255f), px[i].a);
        }
    }

    // trimmed to the subject (plus a margin) and written as a PNG
    static void Save(string file, Color32[] px, int w, int h, int margin = 10)
    {
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 6) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
        if (x1 < 0) { x0 = y0 = 0; x1 = w - 1; y1 = h - 1; }
        x0 = Mathf.Max(0, x0 - margin); y0 = Mathf.Max(0, y0 - margin); x1 = Mathf.Min(w - 1, x1 + margin); y1 = Mathf.Min(h - 1, y1 + margin);
        int cw = x1 - x0 + 1, ch = y1 - y0 + 1;
        var crop = new Color32[cw * ch];
        for (int y = 0; y < ch; y++) System.Array.Copy(px, (y0 + y) * w + x0, crop, y * cw, cw);
        var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
        tex.SetPixels32(crop);
        File.WriteAllBytes(Out + file, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    // ---------------------------------------------------------------- the heart

    static string Heart()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(HeartModel.Fbx);
        if (model == null) return "no heart model";
        Open();
        try
        {
            var g = Put(Object.Instantiate(model));
            var smr = g.GetComponentInChildren<SkinnedMeshRenderer>();
            var mat = HeartModel.Material(null, Color.black); temp.Add(mat);
            smr.sharedMaterial = mat;
            Bounds b = smr.bounds;
            Vector3 c = b.center, front = g.transform.rotation * HeartModel.FrontAxis;
            Vector3 side = Vector3.Cross(Vector3.up, front);
            Frame(b, front + Vector3.up * 0.08f, 20f);

            var key = Lamp("Key", LightType.Directional, c + front * 6f + Vector3.up * 5f - side * 4f, c, new Color(1f, 0.95f, 0.9f), 1.5f);
            var fill = Lamp("Fill", LightType.Directional, c + front * 4f - Vector3.up * 2f + side * 5f, c, new Color(0.5f, 0.56f, 0.7f), 0.5f);
            var rim = Lamp("Rim", LightType.Directional, c - front * 5f + Vector3.up * 3f + side * 4f, c, new Color(1f, 0.35f, 0.3f), 1.1f);

            const int S = 512;
            var px = Matte(S, S);
            Save("Heart_healthy.png", px, S, S);

            // hurt: the warmth going out of it
            key.intensity = 1.15f; key.color = new Color(0.9f, 0.9f, 1f);
            px = Matte(S, S);
            Grade(px, 0.62f, new Color(0.86f, 0.74f, 0.8f));
            Save("Heart_hurt.png", px, S, S);

            // critical: grey meat, lit from underneath
            key.transform.position = c + front * 5f - Vector3.up * 6f + side * 2f;
            key.transform.rotation = Quaternion.LookRotation(c - key.transform.position);
            key.intensity = 1.25f; key.color = new Color(0.78f, 0.92f, 0.86f);
            fill.intensity = 0.15f; rim.intensity = 0.5f; rim.color = new Color(0.6f, 0.7f, 0.8f);
            px = Matte(S, S);
            Grade(px, 0.2f, new Color(0.7f, 0.74f, 0.72f));
            Save("Heart_critical.png", px, S, S);
            return "heart x3";
        }
        finally { Close(); }
    }

    // ---------------------------------------------------------------- the van

    static string Van()
    {
        Transform van = Object.FindObjectsByType<PrometeoCarController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(c => c.transform).FirstOrDefault();
        var clean = AssetDatabase.LoadAssetAtPath<Texture2D>(VanTex + "Kesller.png");
        if (van == null || clean == null) return "no van in the open scenes, van icons not rendered";
        var stages = new List<Texture2D> { clean };
        for (int i = 1; i <= 4; i++) stages.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(VanTex + "Kesller_damage" + i + ".png"));

        Open();
        try
        {
            // a bare copy of the van's meshes on the stage, facing +z
            var root = Put(new GameObject("Van copy")).transform;
            var painted = new List<(MeshRenderer r, int slot, Material original)>();
            Bounds b = new Bounds(); bool first = true;
            foreach (var mr in van.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (!mr.enabled || mf == null || mf.sharedMesh == null) continue;
                var copy = new GameObject(mr.name);
                copy.transform.SetParent(root, false);
                copy.transform.localPosition = van.InverseTransformPoint(mr.transform.position);
                copy.transform.localRotation = Quaternion.Inverse(van.rotation) * mr.transform.rotation;
                copy.transform.localScale = mr.transform.lossyScale;
                copy.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var cr = copy.AddComponent<MeshRenderer>();
                cr.sharedMaterials = mr.sharedMaterials;
                for (int s = 0; s < cr.sharedMaterials.Length; s++)
                    if (cr.sharedMaterials[s] != null && cr.sharedMaterials[s].mainTexture == clean) painted.Add((cr, s, cr.sharedMaterials[s]));
                if (first) { b = cr.bounds; first = false; } else b.Encapsulate(cr.bounds);
            }
            if (first) return "the van has no meshes, van icons not rendered";

            Vector3 c = b.center;
            Frame(b, new Vector3(-0.85f, 0.36f, 1f), 16f);
            var key = Lamp("Key", LightType.Directional, c + new Vector3(-5f, 7f, 6f), c, new Color(0.95f, 0.97f, 1f), 1.35f);
            Lamp("Fill", LightType.Directional, c + new Vector3(6f, 2f, 4f), c, new Color(0.45f, 0.5f, 0.62f), 0.45f);
            var under = Lamp("Under", LightType.Point, c + new Vector3(0f, -b.extents.y - 0.4f, b.extents.z + 0.6f), c, new Color(1f, 0.16f, 0.1f), 0f, 9f);

            const int W = 640, H = 480;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] == null) continue;
                foreach (var (r, slot, original) in painted)
                {
                    var m = new Material(original); temp.Add(m);
                    m.mainTexture = stages[i];
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", stages[i]);
                    var mats = r.sharedMaterials; mats[slot] = m; r.sharedMaterials = mats;
                }
                // the worse it is, the less daylight and the more of the red from underneath
                float k = i / 4f;
                key.intensity = Mathf.Lerp(1.35f, 0.8f, k);
                under.intensity = Mathf.Lerp(0f, 14f, k * k);
                var px = Matte(W, H);
                Grade(px, Mathf.Lerp(0.92f, 0.6f, k), Color.Lerp(Color.white, new Color(0.82f, 0.8f, 0.84f), k));
                Save("VanIcon_" + i + ".png", px, W, H);
            }
            return "van x" + stages.Count(s => s != null) + " (from " + van.name + ")";
        }
        finally { Close(); }
    }
}
