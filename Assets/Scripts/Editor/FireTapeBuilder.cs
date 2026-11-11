using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// HellScape > Office > Fire Tape (Mercer, CCTV desk): V5.1 A3C, the office fire as a CCTV recording. A small stage far
// from the level (a wall, a locked door with the fire glowing round it, Mercer in front of it) and a StabCutscene +
// FireTape on it that the CctvDesk plays before the Tomas tape. Mercer is the same Meshy model as the sofa shooting.
// Needs the office scene open (OfficeSilo.unity), with the Tomas tape already built.
public static class FireTapeBuilder
{
    const string Name = "Cutscene - Mercer fire order";
    static readonly Vector3 Stage = new Vector3(3000f, -300f, 3000f);   // far from everything: only the tape camera sees it

    [MenuItem("HellScape/Office/Fire Tape (Mercer, CCTV desk)")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var tomas = Object.FindObjectsByType<StabCutscene>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name.Contains("Derek kills Tomas"));
        var desk = Object.FindFirstObjectByType<CctvDesk>(FindObjectsInactive.Include);
        if (tomas == null || desk == null) return "Open OfficeSilo.unity (needs the Tomas tape cutscene and the CCTV desk).";
        foreach (var old in Object.FindObjectsByType<StabCutscene>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.name == Name).ToArray()) Object.DestroyImmediate(old.gameObject);

        var go = new GameObject(Name);
        EditorSceneManager.MoveGameObjectToScene(go, tomas.gameObject.scene);
        go.transform.SetParent(tomas.transform.parent, false);
        var stage = new GameObject("Fire tape cast").transform;
        stage.SetParent(go.transform, false);
        stage.position = Stage;

        // the stage: a wall, a door with the fire glowing round its edges, a floor
        var dark = Mat(new Color(.18f, .17f, .17f));
        Box(stage, "Floor", new Vector3(0f, -.1f, 1f), new Vector3(14f, .2f, 12f), dark);
        Box(stage, "Wall", new Vector3(0f, 2f, 3.15f), new Vector3(10f, 4f, .3f), dark);
        Box(stage, "Fire behind the door", new Vector3(0f, 1.35f, 3.02f), new Vector3(1.55f, 2.7f, .05f), Mat(new Color(1f, .45f, .1f), true));
        var door = Box(stage, "Door", new Vector3(0f, 1.25f, 2.93f), new Vector3(1.3f, 2.5f, .12f), Mat(new Color(.12f, .08f, .06f)));
        var glow = new[] { Lamp(stage, "Fire glow", new Vector3(0f, 1.6f, 2.3f), new Color(1f, .45f, .15f), 6f, 8f), Lamp(stage, "Fire glow (2)", new Vector3(1.4f, 2.2f, 2.6f), new Color(1f, .35f, .1f), 5f, 6f) };
        Lamp(stage, "Tape light", new Vector3(3f, 3f, 0f), new Color(.7f, .85f, 1f), 4f, 12f);

        // Mercer, facing the door; his turn into the aim ends a quarter right of that, at the camera (east)
        MeshyCastBuilder.Loop(MeshyCastBuilder.MercerWalk, true); MeshyCastBuilder.Loop(MeshyCastBuilder.MercerAim, false);
        var aim = MeshyCastBuilder.Clip(MeshyCastBuilder.MercerAim);
        var mercer = MeshyCastBuilder.Man(MeshyCastBuilder.MercerWalk, "Mercer", stage, MeshyCastBuilder.MercerHeight,
            MeshyCastBuilder.Mat("Mercer", MeshyCastBuilder.MercerDir + "Meshy_AI_Mercer_PS2_T_Pose_35K_biped_texture_0.png"),
            MeshyCastBuilder.Controller("MercerFireTape", ("Idle", aim), ("Shoot", aim)));
        mercer.transform.SetPositionAndRotation(Stage + new Vector3(.2f, 0f, 1.2f), Quaternion.identity);
        var muzzle = MeshyCastBuilder.HandGun(mercer, "RightHand", aim, aim.length, () => mercer.transform.right);
        aim.SampleAnimation(mercer, 0f);
        var flash = Lamp(stage, "Muzzle flash", muzzle.position - Stage, new Color(1f, .8f, .5f), 8f, 5f);
        flash.enabled = false;

        // the camera: high in the east corner, Mercer left of the door, the lens on his aim
        var cam = new GameObject("CctvCamera (fire)").AddComponent<Camera>();
        cam.transform.SetParent(go.transform, false);
        cam.transform.position = Stage + new Vector3(4.6f, 2.2f, 1.0f);
        cam.transform.rotation = Quaternion.LookRotation((Stage + new Vector3(.8f, 1.25f, 2.0f)) - cam.transform.position);
        cam.fieldOfView = 60f; cam.enabled = false;
        var ears = cam.GetComponent<AudioListener>(); if (ears != null) Object.DestroyImmediate(ears);

        var cut = go.AddComponent<StabCutscene>();
        cut.actors = stage.gameObject; cut.cam = cam; cut.overlay = tomas.overlay; cut.player = tomas.player; cut.lockDuring = tomas.lockDuring; cut.fader = tomas.fader;
        cut.soundSlot = "Office/CCTV silo tape"; cut.sound = null; cut.hideActorsAfter = true;
        var tape = go.AddComponent<FireTape>();
        tape.mercer = mercer.GetComponent<Animator>(); tape.door = door; tape.glow = glow; tape.flash = flash; tape.shotAt = aim.length - .05f;
        stage.gameObject.SetActive(false);
        desk.fireTape = cut;
        EditorUtility.SetDirty(desk); EditorUtility.SetDirty(cut); EditorUtility.SetDirty(tape);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(go.scene);
        return "Fire tape built at " + Stage + "; CctvDesk.fireTape set. Save the scene.";
    }

    static Transform Box(Transform parent, string name, Vector3 local, Vector3 size, Material mat)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = name;
        b.transform.SetParent(parent, false);
        b.transform.localPosition = local; b.transform.localScale = size;
        b.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(b.GetComponent<Collider>());
        return b.transform;
    }

    static Light Lamp(Transform parent, string name, Vector3 local, Color colour, float intensity, float range)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.SetParent(parent, false);
        l.transform.localPosition = local;
        l.type = LightType.Point; l.color = colour; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        return l;
    }

    static Material Mat(Color c, bool emissive = false)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = c };
        if (emissive) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 2.5f); }
        return m;
    }
}
