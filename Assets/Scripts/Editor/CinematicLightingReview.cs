using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Editor-only, opt-in review. Never runs in a player build.
[InitializeOnLoad]
public static class CinematicLightingReview
{
    public const string Output = "docs/CinematicAtmosphere";
    const string Key = "HellScape.CinematicReview";
    public const string BackgroundKey = "HellScape.LightingBackgroundReview";
    public static readonly Vector3 Street = new Vector3(-10f, 2.8f, 28f);
    public static readonly Vector3 Target = new Vector3(12f, 5f, 85f);
    static double readyAt;
    static string stage;
    static CinematicLightingReview()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) SessionState.EraseBool(BackgroundKey);
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            stage = SessionState.GetString(Key, "");
            if (stage == "") return;
            readyAt = EditorApplication.timeSinceStartup + 24;
            EditorApplication.update -= Ready;
            EditorApplication.update += Ready;
        };
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void SkipTitleForReview()
    {
        if (SessionState.GetBool(BackgroundKey, false)) Application.runInBackground = true;
        if (SessionState.GetString(Key, "") != "") SleepSequence.WakeInHell = true;
    }
    public static void Start(string name)
    {
        Directory.CreateDirectory(Output);
        SessionState.SetString(Key, name);
        SessionState.SetBool(BackgroundKey, true);
        EditorApplication.isPlaying = true;
    }
    static void Ready()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Ready; return; }
        if (EditorApplication.timeSinceStartup < readyAt || Time.timeSinceLevelLoad < 12f || SleepSequence.Waking) return;
        EditorApplication.update -= Ready;
        SessionState.EraseString(Key);
        try { Position(); Capture(stage + "-street"); Debug.Log("[CinematicReview] " + stage + " ready"); }
        catch (Exception e) { Debug.LogException(e); }
    }
    public static Camera PlayerCamera => Resources.FindObjectsOfTypeAll<SleepSequence>()
        .First(s => s.gameObject.scene.isLoaded).playerRoot.GetComponentInChildren<Camera>(true);
    public static void Position()
    {
        var sleep = UnityEngine.Object.FindFirstObjectByType<SleepSequence>();
        sleep.SendMessage("ApplyLook", true);
        var cam = PlayerCamera;
        var root = sleep.playerRoot.transform;
        // Keep a static photo review from dying to live enemies. This is Play Mode only.
        if (root.TryGetComponent<PlayerHealth>(out var health)) health.Revive(1000000f);
        var fpc = root.GetComponent<FirstPersonController>();
        if (fpc != null) fpc.enabled = false;
        if (root.TryGetComponent<Rigidbody>(out var body)) { if (!body.isKinematic) body.linearVelocity = Vector3.zero; body.isKinematic = true; }
        root.position += Street - cam.transform.position;
        cam.transform.rotation = Quaternion.LookRotation(Target - Street);
        cam.enabled = true;
        foreach (var c in Camera.allCameras) if (c != cam) c.enabled = false;
        Physics.SyncTransforms();
    }
    public static void Capture(string name, Camera camera = null)
    {
        camera = camera != null ? camera : PlayerCamera;
        Directory.CreateDirectory(Output);
        // Export the display-referred sRGB result, rather than storing linear HDR readback as a dark PNG.
        var rt = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var old = camera.targetTexture; var active = RenderTexture.active;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", tex.EncodeToPNG());
            File.WriteAllText(Output + "/" + name + "-camera.txt", $"Camera={camera.name}\nPosition={camera.transform.position:F4}\nRotation={camera.transform.eulerAngles:F4}\nFOV={camera.fieldOfView}\nResolution=1600x900\nFog={RenderSettings.fogMode} {RenderSettings.fogDensity} {RenderSettings.fogColor}\n");
        }
        finally { camera.targetTexture = old; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(tex); }
    }
}
