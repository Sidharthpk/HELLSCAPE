using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// HellScape > Prologue > Build Cafe Stop: V5.1 P03, Derek's coffee. A placeholder cafe (a plain lit box with a sign, an
// awning and a door; "Cafe model (replace me)") on the free lot west of the street, 30 m up the road from Harlow Clinic,
// plus a third delivery stop in front of it: the van parks in the bay, a worker steps out of the door ("Collect Derek's
// order"), hands it over and goes back in. Swap the box for the real model (keep the "Cafe entrance" object at its door,
// its forward facing the street). The lines are HS5 P03_cafe_pickup (RevisedPlot.ApplyText).
public static class CafeBuilder
{
    const float AlongRoad = 30f, LotOffset = 8.5f;   // from the clinic bay: north along the street, then west to the front face

    [MenuItem("HellScape/Prologue/Build Cafe Stop")]
    public static void Build()
    {
        var sc = EditorSceneManager.GetSceneByName("Prologue");
        if (!sc.isLoaded) sc = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive);
        var run = sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DeliveryRun>(true)).FirstOrDefault();
        if (run == null) { Debug.LogError("Cafe: no DeliveryRun in Prologue"); return; }
        if (run.stops.Any(s => s.bay != null && s.bay.name.Contains("Cafe"))) { Debug.Log("Cafe: already built"); return; }

        var clinic = run.stops[0];
        float groundY = clinic.bay.position.y;
        Vector3 Ground(Vector3 p)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * 30f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.gameObject.scene == sc && h.normal.y > .85f && Mathf.Abs(h.point.y - groundY) < 3f).OrderByDescending(h => h.point.y).ToArray();
            return hits.Length > 0 ? hits[0].point : new Vector3(p.x, groundY, p.z);
        }

        Vector3 bayAt = Ground(clinic.bay.position + Vector3.forward * AlongRoad);
        float frontX = bayAt.x - LotOffset;   // the street-facing wall of the cafe
        Vector3 doorAt = Ground(new Vector3(frontX, 0f, bayAt.z));
        var lot = Physics.OverlapBox(doorAt + new Vector3(-3.5f, 3.2f, 0f), new Vector3(3.5f, 3f, 4f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
            .Where(c => c.gameObject.scene == sc && c.name != "City ground (visible pavement collision)" && !c.name.StartsWith("city_part")).ToArray();
        if (lot.Length > 0) { Debug.LogError("Cafe: the lot is not free: " + string.Join(", ", lot.Select(c => c.name))); return; }

        var parent = sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Deliveries");
        var root = new GameObject("Cafe (placeholder)").transform;
        if (parent != null) root.SetParent(parent, false); else EditorSceneManager.MoveGameObjectToScene(root.gameObject, sc);

        // the building: a box with the door side facing the street (east)
        var shellMat = Mat(new Color(.28f, .17f, .13f));
        var model = new GameObject("Cafe model (replace me)").transform;
        model.SetParent(root, false);
        Box(model, "Walls", doorAt + new Vector3(-2.5f, 2.25f, 0f), new Vector3(5f, 4.5f, 7f), shellMat, true);
        Box(model, "Awning", doorAt + new Vector3(.8f, 3.3f, 0f), new Vector3(1.8f, .15f, 6f), Mat(new Color(.55f, .08f, .06f)), false);
        Box(model, "Door", doorAt + new Vector3(.02f, 1.1f, -1.6f), new Vector3(.08f, 2.2f, 1.1f), Mat(new Color(.07f, .05f, .04f)), false);
        var glow = Mat(new Color(1f, .75f, .4f), true);
        Box(model, "Window", doorAt + new Vector3(.03f, 1.5f, 1.2f), new Vector3(.05f, 1.2f, 2.2f), glow, false);
        Box(model, "Window (2)", doorAt + new Vector3(.03f, 1.5f, 2.9f), new Vector3(.05f, 1.2f, 1.1f), glow, false);
        var sign = new GameObject("Sign").AddComponent<TextMesh>();
        sign.transform.SetParent(model, false);
        sign.transform.SetPositionAndRotation(doorAt + new Vector3(.06f, 3.85f, 0f), Quaternion.Euler(0f, -90f, 0f));   // (reads from the street, looking west)
        sign.text = "CAFE"; sign.anchor = TextAnchor.MiddleCenter; sign.alignment = TextAlignment.Center;
        sign.fontSize = 120; sign.characterSize = .1f; sign.color = new Color(1f, .85f, .55f);
        var lamp = new GameObject("Cafe light").AddComponent<Light>();
        lamp.transform.SetParent(model, false);
        lamp.transform.position = doorAt + new Vector3(2f, 2.6f, 0f);
        lamp.type = LightType.Point; lamp.color = new Color(1f, .78f, .5f); lamp.intensity = 6f; lamp.range = 9f;

        // where the worker comes out (forward = out to the street, as DeliveryRun.Emerge/Leave expect)
        var entrance = new GameObject("Cafe entrance").transform;
        entrance.SetParent(root, false);
        entrance.SetPositionAndRotation(doorAt + new Vector3(0f, 0f, -1.6f), Quaternion.LookRotation(Vector3.right));

        // the bay in the road, and the worker who waits at the kerb
        var bay = new GameObject("Stop 2 - Cafe (bay)").transform;
        bay.SetParent(root, false);
        bay.SetPositionAndRotation(bayAt, Quaternion.LookRotation(Vector3.forward));
        Vector3 meet = Ground(entrance.position + Vector3.right * 3.5f);
        var src = sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PavementWalker>(true)).Select(w => w.gameObject).Skip(1).FirstOrDefault() ?? clinic.customer.gameObject;
        var worker = Object.Instantiate(src, root);
        worker.name = "Cafe worker";
        foreach (var w in worker.GetComponentsInChildren<PavementWalker>(true)) Object.DestroyImmediate(w);
        worker.transform.SetPositionAndRotation(meet, Quaternion.LookRotation(Vector3.right));
        var use = worker.GetComponent<Interactable>() ?? worker.AddComponent<Interactable>();
        use.prompt = "Collect Derek's order"; use.once = true; use.lockedLine = "";
        if (worker.GetComponent<Collider>() == null)
        {
            var cap = worker.AddComponent<CapsuleCollider>();
            cap.height = 1.8f; cap.radius = .4f; cap.center = new Vector3(0f, .9f, 0f);
        }

        run.stops = run.stops.Concat(new[]
        {
            new DeliveryRun.Stop
            {
                objective = "Pick up Derek's coffee - the cafe", bay = bay, bayRadius = 13f, customer = use,
                customerAnimator = worker.GetComponentInChildren<Animator>(), entrance = entrance, arriveLine = "", handOverLines = "",
            }
        }).ToArray();
        EditorUtility.SetDirty(run);
        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        Debug.Log("Cafe stop built at " + doorAt + " (bay " + bayAt + "), Prologue saved");
    }

    static void Box(Transform parent, string name, Vector3 centre, Vector3 size, Material mat, bool collider)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = name;
        b.transform.SetParent(parent, false);
        b.transform.position = centre;
        b.transform.localScale = size;
        b.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(b.GetComponent<Collider>());
    }

    static Material Mat(Color c, bool emissive = false)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = c };
        if (emissive) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 1.6f); }
        return m;
    }
}
