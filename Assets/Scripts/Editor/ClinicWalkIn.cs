using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Prologue: Clinic walk-in + parking P (Prologue open). Safe to run again.
//   - the demo city's collision has a boundary wall at x 259.7 between the street and Harlow Clinic: a 16 m gap is cut
//     in the Prologue's own copy of that mesh (MainGameScene keeps the shared one), so the walk from the van to the
//     clinic has nothing in it; the two ends stay walled, and a closed-doors collider stops you walking into the lobby
//   - the clinic's customer comes out of the clinic's entrance and goes back in (DeliveryRun.Stop.entrance); no door
//   - a blue plate with a white P painted in the parking bay, the speedometer is gone (DeliveryHud draws the map)
public static class ClinicWalkIn
{
    const string MeshPath = "Assets/Generated/DemoCity/PrologueCityCollision.asset";
    const float WallX = 259.7f, GapFrom = -91f, GapTo = -75f, DoorZ = -83.3f;

    [MenuItem("HellScape/Prologue: Clinic walk-in + parking P")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        var pro = SceneManager.GetSceneByName("Prologue");
        if (!pro.isLoaded) return "Open the Prologue first.";
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.gameObject.scene == pro).ToArray();
        Transform Named(string n) => all.FirstOrDefault(t => t != null && t.name == n);   // (some get destroyed below)
        var root = Named("Deliveries");
        var run = root.GetComponent<DeliveryRun>();
        var log = new System.Text.StringBuilder();

        // ---------------------------------------------------------------- the wall: a gap in front of the clinic
        var city = Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(m => m.gameObject.scene == pro && m.name == "city_part_collider");
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (mesh == null || city.sharedMesh != mesh)
        {
            var copy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Generated/DemoCity/CityCollisionWithEntrances.asset"));
            copy.name = "PrologueCityCollision";
            var v = copy.vertices; int cut = 0;
            for (int sub = 0; sub < copy.subMeshCount; sub++)
            {
                var tr = copy.GetTriangles(sub); var keep = new System.Collections.Generic.List<int>();
                for (int i = 0; i < tr.Length; i += 3)
                {
                    Vector3 a = city.transform.TransformPoint(v[tr[i]]), b = city.transform.TransformPoint(v[tr[i + 1]]), c = city.transform.TransformPoint(v[tr[i + 2]]);
                    var bd = new Bounds(a, Vector3.zero); bd.Encapsulate(b); bd.Encapsulate(c);
                    bool wall = Mathf.Abs(Vector3.Cross(b - a, c - a).normalized.y) < .35f && bd.min.x > WallX - 3f && bd.max.x < WallX + 3f && bd.max.z > -105.5f && bd.min.z < -58.5f;
                    if (wall) { cut++; continue; }
                    keep.Add(tr[i]); keep.Add(tr[i + 1]); keep.Add(tr[i + 2]);
                }
                copy.SetTriangles(keep, sub);
            }
            if (mesh == null) { AssetDatabase.CreateAsset(copy, MeshPath); mesh = copy; }
            else { EditorUtility.CopySerialized(copy, mesh); Object.DestroyImmediate(copy); EditorUtility.SetDirty(mesh); }
            city.sharedMesh = mesh;
            EditorUtility.SetDirty(city);
            log.AppendLine("wall triangles removed: " + cut);
        }
        var old = Named("Clinic approach");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var approach = new GameObject("Clinic approach").transform;
        approach.SetParent(root, false);
        Transform Box(string name, Vector3 centre, Vector3 size, float tilt = 0f) { var b = new GameObject(name).AddComponent<BoxCollider>(); b.transform.SetParent(approach, false); b.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, 0f, tilt)); b.size = size; return b.transform; }
        Box("Boundary wall (north of the gap)", new Vector3(WallX, 22f, (GapTo - 59f) / 2f), new Vector3(0.4f, 35f, -59f - GapTo));
        Box("Boundary wall (south of the gap)", new Vector3(WallX, 22f, (-105f + GapFrom) / 2f), new Vector3(0.4f, 35f, GapFrom + 105f));

        // the clinic stands on a 4 m plinth (basement_big, no collision of its own): its stairs are steps of 0.3 m that
        // the player body can't take, so a smooth ramp lies along their noses, then the landing up to the doors. Rails
        // along the stair sides and a closed-doors wall keep you on it (and out of the lobby, which has no floor of its own)
        const float PlinthY = 8.8f, StairFrom = 266.8f, StairTo = 270.8f, EdgeN = -69.5f, EdgeS = -97.5f, Width = EdgeN - EdgeS, MidZ = (EdgeN + EdgeS) / 2f;
        float rise = PlinthY - 4.8f, run45 = StairTo - StairFrom, len = Mathf.Sqrt(rise * rise + run45 * run45), ang = Mathf.Atan2(rise, run45) * Mathf.Rad2Deg;
        var dir = new Vector3(run45, rise, 0f) / len; var up = new Vector3(-dir.y, dir.x, 0f);
        Box("Clinic stairs (ramp)", new Vector3((StairFrom + StairTo) / 2f, (4.8f + PlinthY) / 2f, MidZ) - up * 0.2f - dir * 0.3f, new Vector3(len + 0.6f, 0.4f, Width), ang);   // (a lip below the street, flush with the landing at the top)
        Box("Clinic landing", new Vector3(278.4f, PlinthY - 0.25f, MidZ), new Vector3(15.2f, 0.5f, Width));          // x 270.8 - 286
        Box("Clinic stairs rail (north)", new Vector3(274f, 9.5f, EdgeN + 0.2f), new Vector3(17f, 10f, 0.4f));      // x 265.5 - 282.5
        Box("Clinic stairs rail (south)", new Vector3(274f, 9.5f, EdgeS - 0.2f), new Vector3(17f, 10f, 0.4f));
        Box("Clinic doors (closed)", new Vector3(283.2f, 11f, MidZ), new Vector3(1.6f, 6f, Width));

        // ---------------------------------------------------------------- the customer comes out of the clinic
        var stop = run.stops.First(s => s.customer != null && s.customer.name.Contains("Harlow"));
        var entrance = Named("Clinic entrance") ?? new GameObject("Clinic entrance").transform;
        entrance.SetParent(root, false);
        entrance.SetPositionAndRotation(new Vector3(281.6f, PlinthY, DoorZ), Quaternion.Euler(0f, 270f, 0f));   // forward = out to the street
        stop.entrance = entrance;
        stop.customer.transform.SetPositionAndRotation(new Vector3(274f, PlinthY, DoorZ), Quaternion.Euler(0f, 270f, 0f));
        EditorUtility.SetDirty(run);

        // ---------------------------------------------------------------- the P
        var bay = run.depotBay;
        var oldP = bay.Find("Parking P");
        if (oldP != null) Object.DestroyImmediate(oldP.gameObject);
        var flat = Quaternion.LookRotation(Vector3.down, bay.forward);   // lies on the ground, its top towards the way the van faces
        var p = new GameObject("Parking P").transform;
        p.SetParent(bay, false);
        p.position = bay.position;
        var plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plate.name = "Plate";
        Object.DestroyImmediate(plate.GetComponent<Collider>());
        plate.transform.SetParent(p, false);
        plate.transform.SetPositionAndRotation(bay.position + Vector3.up * 0.04f, flat);
        plate.transform.localScale = new Vector3(2.7f, 2.7f, 1f);
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Prologue_ParkingP.mat");
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, "Assets/Materials/Prologue_ParkingP.mat"); }
        mat.SetColor("_BaseColor", new Color(.06f, .17f, .42f)); EditorUtility.SetDirty(mat);
        var pr = plate.GetComponent<Renderer>(); pr.sharedMaterial = mat; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var letter = new GameObject("P").AddComponent<TextMeshPro>();
        letter.transform.SetParent(p, false);
        letter.transform.SetPositionAndRotation(bay.position + Vector3.up * 0.06f, flat);
        letter.rectTransform.sizeDelta = new Vector2(2.7f, 2.7f);
        letter.font = GameFont.Main; letter.text = "P"; letter.fontSize = 26f; letter.fontStyle = FontStyles.Bold;
        letter.alignment = TextAlignmentOptions.Center; letter.color = Color.white; letter.textWrappingMode = TextWrappingModes.NoWrap;
        letter.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // ---------------------------------------------------------------- the speedometer makes way for the map
        var speedo = Named("Speedometer");
        if (speedo != null) Object.DestroyImmediate(speedo.gameObject);

        EditorSceneManager.MarkSceneDirty(pro);
        EditorSceneManager.SaveScene(pro);
        AssetDatabase.SaveAssets();
        log.AppendLine($"clinic entrance {entrance.position}, customer waits at {stop.customer.transform.position}; P at {bay.position}");
        return log.ToString();
    }
}
