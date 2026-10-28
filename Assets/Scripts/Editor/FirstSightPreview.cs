using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// HellScape > Cutscenes > First Sight Frames (MainGameScene open, not playing): renders each shot of the first-sight
// cutscene at its start, middle and end into Temp/FirstSightFrames (shot_k.png), with the stand-in actors in place.
public static class FirstSightPreview
{
    [MenuItem("HellScape/Cutscenes/First Sight Frames")]
    public static void Render() => RenderTo(Path.Combine(Directory.GetCurrentDirectory(), "Temp", "FirstSightFrames"), new[] { 0f, .5f, 1f });

    public static void RenderTo(string dir, float[] ks)
    {
        Directory.CreateDirectory(dir);
        var cam = Camera.main;
        var rf = Object.FindFirstObjectByType<RedFlat>(FindObjectsInactive.Include);
        var door = rf.exit.destination;
        var stage = FirstSightCutscene.Stage(door.position, Object.FindFirstObjectByType<CityWalkers>(FindObjectsInactive.Include));
        stage.gameObject.SetActive(true);
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView, fog0 = RenderSettings.fogDensity;
        var arms = cam.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        foreach (var r in arms) r.enabled = false;
        var shots = FirstSightCutscene.Plan(door.position, door.forward);
        var grade = new FirstSightCutscene.Grade();
        for (int s = 0; s < shots.Length; s++)
            foreach (float k in ks)
            {
                var c = shots[s].at(k);
                cam.transform.SetPositionAndRotation(c.pos, Quaternion.LookRotation(c.look - c.pos)); cam.fieldOfView = c.fov;
                float e = Mathf.SmoothStep(0f, 1f, k);
                RenderSettings.fogDensity = s == 3 ? Mathf.Lerp(fog0, FirstSightCutscene.AerialFog, e) : fog0;
                grade.Lift(s == 3 ? FirstSightCutscene.AerialLift * e : 0f);
                var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; RenderTexture.active = rt;
                var t = new Texture2D(1280, 720, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); t.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, "shot" + s + "_" + Mathf.RoundToInt(k * 100) + ".png"), t.EncodeToPNG());
                Object.DestroyImmediate(rt); Object.DestroyImmediate(t);
            }
        grade.Restore();
        foreach (var r in arms) r.enabled = true;
        cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; RenderSettings.fogDensity = fog0;
        Object.DestroyImmediate(stage.gameObject);
        Debug.Log("First sight frames: " + dir);
    }
}
