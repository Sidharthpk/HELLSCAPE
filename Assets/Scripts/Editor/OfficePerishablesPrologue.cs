using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The Prologue's office (still the Kessler & Vane model; the user chose to keep it and its flashback, 2026-10-21):
// the furniture's box colliders become mesh colliders like the main scene's, a Perishables pendant hangs over each
// lamp the flashback lights (OfficeFlashback.LedSpots), some computers stay on for the night ("Office night lights",
// off by day, switched on by the flashback), and a few pictures and clocks go on the walls along the walk. Nothing
// stands on the floor: the flashback's walk and camera stops stay as they were.
public static partial class NewOfficeFit
{
    [MenuItem("HellScape/Office/Perishables/Prologue Office (lamps, colliders, decor)")]
    public static void DressPrologue()
    {
        var m = GameObject.Find(ModelName);
        if (EditorApplication.isPlaying || m == null || !IsKv(m.transform)) { Debug.LogError("Needs the Kessler & Vane '" + ModelName + "' (the Prologue) open, out of Play."); return; }
        model = m.transform; office = null;
        log.Clear(); pieces.Clear(); pieceMats.Clear(); keepOut.Clear();
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        try
        {
            Solidify();
            foreach (var n in new[] { "Perishables", "Office night lights" }) { var old = model.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject); }
            dressing = new GameObject("Perishables").transform;
            dressing.SetParent(model, false);
            dressing.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(dressing.gameObject, "Perishables");

            var fit = PieceSize("ceiling lamp");
            int lamps = 0;
            foreach (var p in OfficeFlashback.LedSpots)
            {
                if (!Physics.Raycast(new Vector3(p.x, Floor + 1.5f, p.z), Vector3.up, out var h, 3f)) { log.AppendLine("  no ceiling over " + p); continue; }
                Stand("LAMPS", "ceiling lamp", new Vector3(p.x, h.point.y - fit.size.y, p.z), 0f, false).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lamps++;
            }

            // every third monitor left on: a pale screen facing its keyboard, and its glow
            var night = new GameObject("Office night lights").transform;
            night.SetParent(model, false);
            var supplies = model.Find("Supplies");
            var monitors = supplies.Cast<Transform>().Where(t => t.name.StartsWith("Monitor") && t.gameObject.activeInHierarchy).ToList();
            var keyboards = supplies.Cast<Transform>().Where(t => t.name.StartsWith("Keyboard") && t.gameObject.activeInHierarchy)
                                    .Select(t => t.GetComponentInChildren<Renderer>()).Where(r => r != null).Select(r => r.bounds.center).ToList();
            int on = 0;
            for (int i = 0; i < monitors.Count; i += 3)
            {
                var r = monitors[i].GetComponentInChildren<Renderer>();
                if (r == null || keyboards.Count == 0) continue;
                var b = r.bounds;
                var kb = keyboards.OrderBy(k => (k - b.center).sqrMagnitude).First();
                var dir = kb - b.center; dir.y = 0f;
                if (dir.sqrMagnitude < .01f || dir.sqrMagnitude > 1f) continue;
                dir = Mathf.Abs(dir.x) > Mathf.Abs(dir.z) ? new Vector3(Mathf.Sign(dir.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(dir.z));
                float wide = Mathf.Abs(dir.x) > 0f ? b.size.z : b.size.x, deep = Mathf.Abs(dir.x) > 0f ? b.size.x : b.size.z;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = "Screen on";
                q.transform.SetParent(night, false);
                q.transform.SetPositionAndRotation(b.center + Vector3.up * b.size.y * .12f + dir * (deep * .5f + .008f), Quaternion.LookRotation(-dir));
                q.transform.localScale = new Vector3(wide * .82f, b.size.y * .5f, 1f);
                q.GetComponent<Renderer>().sharedMaterial = GlowMat("Screen glow", new Color(.16f, .27f, .38f));
                q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var l = new GameObject("Screen light").AddComponent<Light>();
                l.transform.SetParent(night, false);
                l.transform.position = b.center + dir * .5f;
                l.type = LightType.Point; l.color = new Color(.5f, .72f, 1f); l.intensity = .45f; l.range = 2.2f; l.shadows = LightShadows.None;
                on++;
            }
            night.gameObject.SetActive(false);

            // pictures and clocks along the walk (plan coordinates, as the main scene's)
            const string R = "PROLOGUE";
            W(R, "landscape painting", 4.6f, 16.6f, 90f, 1.45f); W(R, "portrait painting", 4.8f, 25f, 270f, 1.45f);
            W(R, "landscape painting", 30f, 36f, 0f, 1.45f); W(R, "portrait painting", 39f, 36f, 180f, 1.45f);
            W(R, "wall clock", 20f, 47.8f, 0f, 2.25f); W(R, "wall clock", -8f, 17.5f, 270f, 2.25f);
            W(R, "landscape painting", -8f, 22f, 270f, 1.45f); W(R, "pinboard", 12f, 47.8f, 0f, 1.3f);
            W(R, "wall clock", 66f, 36f, 0f, 2.25f); W(R, "map wall", 70f, 33f, 90f, .9f);
            log.AppendLine("prologue: " + lamps + " pendants, " + on + " computers left on, " + dressing.Cast<Transform>().Count(t => t.name.StartsWith(R)) + " pictures");
        }
        finally { Physics.queriesHitBackfaces = backfaces; }
        EditorSceneManager.MarkSceneDirty(m.scene);
        Report = log.ToString();
        Debug.Log("Prologue office dressed.\n" + Report);
    }
}
