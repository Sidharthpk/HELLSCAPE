using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Editor-only, Play mode: film the live game camera to a JPG sequence for the trailer.
// Renders the camera into a RenderTexture, so overlay canvases (HUD, dialogue, objectives) are never in the picture.
// Time.captureFramerate locks game time to 'fps' so a slow editor still gives smooth footage.
public static class TrailerShoot
{
    public const string Root = @"C:\Users\sidha\Desktop\Self Projects\Trailer\shots";
    public static int Frames;
    public static string Dir;
    public static Camera Override;          // film this camera instead of the live base camera (a cinematic rig)
    static bool on;
    static int last = -1;
    static RenderTexture rt;
    static Texture2D tex;
    static int w, h;

    public static void Start(string name, int fps = 30, int width = 1920, int height = 1080)
    {
        Stop();
        Dir = Path.Combine(Root, name);
        if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
        Directory.CreateDirectory(Dir);
        w = width; h = height; Frames = 0; last = -1;
        rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        Time.captureFramerate = fps;
        on = true;
        EditorApplication.update += Tick;
    }

    public static void Stop()
    {
        if (!on) return;
        on = false;
        EditorApplication.update -= Tick;
        Time.captureFramerate = 0;
        if (rt != null) Object.DestroyImmediate(rt);
        if (tex != null) Object.DestroyImmediate(tex);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Stop(); return; }
        if (!on || rt == null || tex == null) return;
        if (Time.frameCount == last) return;
        last = Time.frameCount;
        Camera cam = Override != null && Override.gameObject.activeInHierarchy ? Override : null;
        if (cam == null)
            foreach (var c in Camera.allCameras)
                if (c.targetTexture == null && c.enabled && c.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Base
                    && (cam == null || c.depth > cam.depth)) cam = c;
        if (cam == null) return;
        var prev = cam.targetTexture;
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(Dir, $"f{++Frames:00000}.jpg"), tex.EncodeToJPG(92));
    }
}
