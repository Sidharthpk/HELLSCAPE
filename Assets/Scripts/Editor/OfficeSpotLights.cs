using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Office: Spot Lights + Shafts
// The office's ceiling lamps (OfficeAct/Office: EmergencyLights, PowerLights) were point lights. Each is a spotlight
// now, aimed at the floor, with a faint additive cone under it (LightShaft) for some volume in the air. The same for the
// silo tunnels' lamps, the lamps down the silo shaft and the light over the blood pool. Small glows (pickups, keypads,
// ghosts) stay as they are. Safe to run again.
public static class OfficeSpotLights
{
    const string Out = "Assets/Generated/LightShaft/";
    static readonly string[] Groups = { "EmergencyLights", "PowerLights", "RedTubeLights", "CorridorLamps", "Lamps", "Silo", "BloodSurface" };   // the office, the silo tunnels (SiloComplex, KesslerTunnel), the silo shaft and its blood pool

    [MenuItem("HellScape/Office: Spot Lights + Shafts")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var scene = SceneManager.GetSceneByName("OfficeSilo");
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/OfficeSilo.unity", OpenSceneMode.Additive);
        if (!AssetDatabase.IsValidFolder("Assets/Generated/LightShaft")) AssetDatabase.CreateFolder("Assets/Generated", "LightShaft");
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Out + "Cone.asset");
        if (mesh == null) { mesh = Cone(); AssetDatabase.CreateAsset(mesh, Out + "Cone.asset"); }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Out + "LightShaft.mat");
        if (mat == null) { mat = new Material(Shader.Find("HorrorShot/LightShaft")); AssetDatabase.CreateAsset(mat, Out + "LightShaft.mat"); }
        mat.renderQueue = 3000;

        int n = 0;
        var lamps = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true))
            .Where(l => l.transform.parent != null && Groups.Contains(l.transform.parent.name)).ToList();
        foreach (var l in lamps)
        {
            l.type = LightType.Spot;
            l.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // straight down
            l.spotAngle = 120f; l.innerSpotAngle = 50f;
            Physics.queriesHitTriggers = false;
            float h = Physics.Raycast(l.transform.position, Vector3.down, out var hit, 8f) ? hit.distance : 2.7f;
            l.range = Mathf.Max(l.range, h * 2.2f);   // (a spot throws along its axis: it has to reach the floor with room to spare)

            var old = l.transform.Find("Shaft");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("Shaft");
            go.transform.SetParent(l.transform, false);
            go.transform.rotation = Quaternion.identity;
            go.transform.position = l.transform.position;
            float sh = Mathf.Min(h, 4f);   // (a lamp high over a drop gets a beam no longer than this)
            float radius = Mathf.Tan(55f * Mathf.Deg2Rad) * sh * .7f;
            var k = go.transform.parent.lossyScale;
            go.transform.localScale = new Vector3(radius / k.x, sh / k.y, radius / k.z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var s = go.AddComponent<LightShaft>();
            s.lamp = l; s.strength = .25f;
            EditorUtility.SetDirty(l);
            n++;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Lamps: " + n + " now spotlights with a light shaft each.";
    }

    // apex at the origin, the open base one unit down and one unit out; alpha 1 at the apex, 0 at the base
    static Mesh Cone()
    {
        const int seg = 24;
        var v = new Vector3[seg + 1]; var c = new Color[seg + 1]; var t = new int[seg * 3];
        v[0] = Vector3.zero; c[0] = new Color(1, 1, 1, 1);
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            v[i + 1] = new Vector3(Mathf.Cos(a), -1f, Mathf.Sin(a)); c[i + 1] = new Color(1, 1, 1, 0);
            t[i * 3] = 0; t[i * 3 + 1] = i + 1; t[i * 3 + 2] = (i + 1) % seg + 1;
        }
        var m = new Mesh { name = "LightShaftCone", vertices = v, colors = c, triangles = t };
        m.RecalculateBounds();
        return m;
    }
}
