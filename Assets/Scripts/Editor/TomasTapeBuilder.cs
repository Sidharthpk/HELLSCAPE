using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// HellScape > Office > Tomas Tape (V5.1: Mercer shoots Tomas, as in the sofa shooting): the CCTV tape of Tomas's death rebuilt with the Meshy cast on a corner landing of
// the silo stairs (the open shaft past its railing), framed the way the user framed it. Replaces the knife rig of
// "Cutscene - Derek kills Tomas" (the StabCutscene keeps its camera, tape look and after-lines); TomasTape runs it.
public static class TomasTapeBuilder
{
    const string Dir = "Assets/Perishables/Avatar/";
    const string TomasIdle = Dir + "Meshy_AI_Tomas_Rourke_PS2_T_Po_biped/Tomas Idle/Meshy_AI_Tomas_Rourke_PS2_T_Po_biped_Animation_Idle_02_withSkin.fbx";
    const string TomasDeath = Dir + "Meshy_AI_Tomas_Rourke_PS2_T_Po_biped (1)/Tomas Death/Meshy_AI_Tomas_Rourke_PS2_T_Po_biped_Animation_Shot_and_Slow_Fall_Backward_withSkin.fbx";
    const string DerekIdle = Dir + "Meshy_AI_Derek_PS2_Rigged_biped (4)/Derek Idle Stance/Meshy_AI_Derek_PS2_Rigged_biped_Animation_Idle_02_withSkin.fbx";
    const string DerekShoot = Dir + "Meshy_AI_Derek_PS2_Rigged_biped (3)/Derek Draw and Shoot/Meshy_AI_Derek_PS2_Rigged_biped_Animation_Draw_and_Shoot_Left_withSkin.fbx";
    const string Cast = "Tomas tape cast";

    // the corner landing at the foot of a flight (y 44.5, x 1003.5..1005, z 1095.5..1098), the open shaft east of it;
    // the user's framing (their Scene view): Derek on the steps with his back to the camera, Tomas on the landing
    public static Vector3 TomasAt = new Vector3(1003.6f, 0f, 1096.6f);   // his back to the shaft: he falls 1.5 m back, to the edge
    public static Vector3 DerekAt = new Vector3(1001.6f, 0f, 1096.25f);  // two steps up the flight
    public static Vector3[] ExitAt = { new Vector3(999.6f, 0f, 1096.25f), new Vector3(997f, 0f, 1096.25f) };   // back up the stairs, out of shot
    public static Vector3 CamAt = new Vector3(998.94f, 48.51f, 1099.98f), CamRot = new Vector3(21.41f, 135.26f, 0f);
    public const float TomasHeight = 1.8f, ShotAt = 1.75f;   // the end of his aim, the frame before the recoil

    [MenuItem("HellScape/Office/Tomas Tape (silo stairs, Meshy cast)")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var cut = Object.FindObjectsByType<StabCutscene>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name.Contains("Derek kills Tomas"));
        if (cut == null) return "No 'Cutscene - Derek kills Tomas' in the open scene (MainGameScene).";

        // out with the knife rig and any earlier cast
        if (cut.actors != null && cut.actors.name != Cast) Object.DestroyImmediate(cut.actors);
        var old = cut.transform.Find(Cast); if (old != null) Object.DestroyImmediate(old.gameObject);
        var cast = new GameObject(Cast).transform;
        cast.SetParent(cut.transform, false);
        cast.position = Floor(TomasAt);

        MeshyCastBuilder.Loop(TomasIdle, true); MeshyCastBuilder.Loop(TomasDeath, false);
        MeshyCastBuilder.Loop(MeshyCastBuilder.MercerWalk, true); MeshyCastBuilder.Loop(MeshyCastBuilder.MercerAim, false);
        AnimationClip C(string f) => MeshyCastBuilder.Clip(f);
        string Tex(string f) => System.IO.Path.GetDirectoryName(f).Replace('\\', '/') + "/" + System.IO.Path.GetFileName(f).Split(new[] { "_Animation_" }, System.StringSplitOptions.None)[0] + "_texture_0.png";

        var tomas = MeshyCastBuilder.Man(TomasIdle, "Tomas", cast, TomasHeight, MeshyCastBuilder.Mat("Tomas", Tex(TomasIdle)),
            MeshyCastBuilder.Controller("TomasTape", ("Idle", C(TomasIdle)), ("Death", C(TomasDeath))));
        tomas.transform.SetPositionAndRotation(Floor(TomasAt), Quaternion.LookRotation(Vector3.left));   // facing Mercer, his back to the drop

        // V5.1: Mercer is the shooter (the same Meshy Mercer as the sofa shooting). He has no idle clip: "Idle" is the
        // first frame of his right-turn aim held still, "Shoot" plays the turn into the aim and holds it
        var aim = C(MeshyCastBuilder.MercerAim);
        var mercer = MeshyCastBuilder.Man(MeshyCastBuilder.MercerWalk, "Mercer", cast, MeshyCastBuilder.MercerHeight,
            MeshyCastBuilder.Mat("Mercer", MeshyCastBuilder.MercerDir + "Meshy_AI_Mercer_PS2_T_Pose_35K_biped_texture_0.png"),
            MeshyCastBuilder.Controller("MercerTape", ("Idle", aim), ("Shoot", aim)));
        mercer.transform.position = Floor(DerekAt);
        // the aim ends a quarter right of his root: turned so that the barrel points at Tomas's chest when the shot goes
        Vector3 to = Floor(TomasAt) - Floor(DerekAt); to.y = 0f;
        mercer.transform.rotation = Quaternion.LookRotation(to) * Quaternion.Euler(0f, -90f, 0f);
        var muzzle = MeshyCastBuilder.HandGun(mercer, "RightHand", aim, aim.length, () => mercer.transform.right);
        aim.SampleAnimation(mercer, 0f);
        float shotAt = aim.length - .05f;

        var walker = MeshyCastBuilder.Man(MeshyCastBuilder.MercerWalk, "Mercer (walks out)", cast, MeshyCastBuilder.MercerHeight,
            MeshyCastBuilder.Mat("Mercer", MeshyCastBuilder.MercerDir + "Meshy_AI_Mercer_PS2_T_Pose_35K_biped_texture_0.png"),
            MeshyCastBuilder.Controller("MercerTapeWalk", ("Walk", C(MeshyCastBuilder.MercerWalk))));
        walker.transform.SetPositionAndRotation(mercer.transform.position, Quaternion.LookRotation(Vector3.left));
        walker.SetActive(false);

        var exits = ExitAt.Select((p, i) =>
        {
            var e = new GameObject("Exit " + (i + 1)).transform; e.SetParent(cast, false); e.position = Floor(p); return e;
        }).ToArray();

        var flash = new GameObject("Muzzle flash").AddComponent<Light>();
        flash.transform.SetParent(cast, false); flash.transform.position = muzzle.position;
        flash.type = LightType.Point; flash.range = 5f; flash.intensity = 8f; flash.color = new Color(1f, .8f, .5f); flash.shadows = LightShadows.None; flash.enabled = false;
        var lamp = new GameObject("Tape night light").AddComponent<Light>();
        // between them, under the flight overhead (above it, that flight shadowed the landing black)
        lamp.transform.SetParent(cast, false); lamp.transform.position = (Floor(TomasAt) + Floor(DerekAt)) * .5f + Vector3.up * 2.1f;
        lamp.type = LightType.Point; lamp.range = 9f; lamp.intensity = 6f; lamp.color = new Color(.75f, .9f, 1f); lamp.shadows = LightShadows.None;

        var tape = cut.GetComponent<TomasTape>(); if (tape == null) tape = cut.gameObject.AddComponent<TomasTape>();
        tape.derek = mercer.GetComponent<Animator>(); tape.frozenIdle = true; tape.tomas = tomas.GetComponent<Animator>(); tape.derekWalker = walker;
        tape.exit = exits; tape.muzzle = muzzle; tape.flash = flash; tape.fallDir = Vector3.right; tape.shotAt = shotAt;
        EditorUtility.SetDirty(tape);

        cut.actors = cast.gameObject; cut.animator = null;
        cut.soundSlot = "Office/CCTV silo tape"; cut.sound = null;   // (no stabbing under it now: an empty slot plays nothing)
        cast.gameObject.SetActive(false);
        EditorUtility.SetDirty(cut);

        // the security camera: where the user framed it, above the flight looking down onto the landing
        if (cut.cam != null)
        {
            cut.cam.transform.SetPositionAndRotation(CamAt, Quaternion.Euler(CamRot));
            cut.cam.fieldOfView = 60f;
            EditorUtility.SetDirty(cut.cam.transform);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(cut.gameObject.scene);
        return $"Tomas tape: Mercer at {mercer.transform.position} facing {mercer.transform.eulerAngles.y:0}, Tomas at {tomas.transform.position}, muzzle {muzzle.position}, shot at {shotAt:0.00}s";
    }

    // the steps or grating under p (searched from just under the flight above)
    static Vector3 Floor(Vector3 p) =>
        Physics.Raycast(new Vector3(p.x, 47.6f, p.z), Vector3.down, out var h, 6f, ~0, QueryTriggerInteraction.Ignore) ? h.point : new Vector3(p.x, 44.5f, p.z);
}
