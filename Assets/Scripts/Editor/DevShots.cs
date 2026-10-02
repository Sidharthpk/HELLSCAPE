using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Editor-only: render a view of one open scene to a JPG (for checking map work without entering Play).
public static class DevShots
{
    // Play mode: a strip of frames from a camera while it's switched on (a cutscene camera), one every 'every'
    // real seconds, as <prefix>01.jpg, 02... Stops after 'count' frames or when Play ends.
    public static void Film(Camera cam, string prefix, int count, float every)
    {
        int n = 0; double next = 0;
        UnityEditor.EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            if (!UnityEditor.EditorApplication.isPlaying || cam == null || n >= count) { UnityEditor.EditorApplication.update -= tick; return; }
            if (!cam.enabled || UnityEditor.EditorApplication.timeSinceStartup < next) return;
            next = UnityEditor.EditorApplication.timeSinceStartup + every;
            var rt = new RenderTexture(960, 540, 24);
            var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes($"{prefix}{++n:00}.jpg", tex.EncodeToJPG(80));
            Object.Destroy(rt); Object.Destroy(tex);
        };
        UnityEditor.EditorApplication.update += tick;
    }

    // Play mode: what the player sees (whichever camera is live, with the overlay UI drawn in), 'fps' frames a real
    // second, as <dir>/f0001.jpg... for making GIFs. Runs until StopFilm(), 'max' frames or the end of Play.
    static bool filming;
    static int filmTake;
    public static int Filmed;
    public static void StopFilm() { filming = false; }
    public static void FilmGame(string dir, float fps = 10f, int w = 640, int h = 360, int max = 3000)
    {
        Directory.CreateDirectory(dir);
        filming = true; Filmed = 0;
        double next = 0;
        int take = ++filmTake;   // (a newer FilmGame call ends this one)
        var rt = new RenderTexture(w, h, 24);
        var uiRt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var uiTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Camera uiCam = null;
        UnityEditor.EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            if (!UnityEditor.EditorApplication.isPlaying || !filming || take != filmTake || Filmed >= max)
            {
                UnityEditor.EditorApplication.update -= tick;
                if (take == filmTake) filming = false;
                Object.Destroy(rt); Object.Destroy(uiRt); Object.Destroy(tex); Object.Destroy(uiTex);
                if (uiCam != null) Object.Destroy(uiCam.gameObject);
                return;
            }
            if (UnityEditor.EditorApplication.timeSinceStartup < next) return;
            next = UnityEditor.EditorApplication.timeSinceStartup + 1.0 / fps;
            Camera cam = null;
            foreach (var c in Camera.allCameras)
                if (c.targetTexture == null && c.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Base && (cam == null || c.depth > cam.depth)) cam = c;
            if (cam == null) return;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);

            // the overlay UI on its own camera (so the PSX pass doesn't chew it up), laid over the game picture
            if (uiCam == null)
            {
                var go = new GameObject("__filmUI") { hideFlags = HideFlags.HideAndDontSave };
                uiCam = go.AddComponent<Camera>();
                uiCam.enabled = false; uiCam.clearFlags = CameraClearFlags.SolidColor; uiCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                uiCam.transform.position = new Vector3(0f, -5000f, 0f);
                uiCam.targetTexture = uiRt;
            }
            var overlays = new System.Collections.Generic.List<Canvas>();
            int mask = 0;
            foreach (var cv in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (cv.isRootCanvas && cv.enabled && cv.renderMode == RenderMode.ScreenSpaceOverlay)
                { overlays.Add(cv); mask |= 1 << cv.gameObject.layer; cv.renderMode = RenderMode.ScreenSpaceCamera; cv.worldCamera = uiCam; cv.planeDistance = 1f; }
            uiCam.cullingMask = mask;
            try { Canvas.ForceUpdateCanvases(); uiCam.Render(); }
            finally { foreach (var cv in overlays) { cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.worldCamera = null; } }
            RenderTexture.active = uiRt;
            uiTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = null;
            var px = tex.GetPixels32(); var ui = uiTex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                int keep = 255 - ui[i].a;   // (the UI shaders write premultiplied colour)
                px[i].r = (byte)Mathf.Min(255, ui[i].r + px[i].r * keep / 255);
                px[i].g = (byte)Mathf.Min(255, ui[i].g + px[i].g * keep / 255);
                px[i].b = (byte)Mathf.Min(255, ui[i].b + px[i].b * keep / 255);
            }
            tex.SetPixels32(px); tex.Apply();
            File.WriteAllBytes($"{dir}/f{++Filmed:0000}.jpg", tex.EncodeToJPG(88));
        };
        UnityEditor.EditorApplication.update += tick;
    }

    // an overlay canvas as the player would see it, over a flat backdrop
    public static void UIShot(string file, Canvas canvas, Color backdrop)
    {
        var go = new GameObject("__uishot") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = backdrop; cam.cullingMask = 1 << canvas.gameObject.layer;
        cam.transform.position = new Vector3(0f, -5000f, 0f);
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        var mode = canvas.renderMode; var wc = canvas.worldCamera; float pd = canvas.planeDistance;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1f;
        try
        {
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(file, tex.EncodeToJPG(90));
            Object.DestroyImmediate(tex);
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = wc; canvas.planeDistance = pd;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        }
    }

    // ortho > 0: straight down, that many metres half-height; cutAbove: hide everything above this y (roofs)
    public static void Shot(string file, string sceneName, Vector3 pos, Vector3 look, float fov = 65f, float ortho = 0f, float cutAbove = float.NaN)
    {
        // only the named scene, and its hell city rather than the day one
        var toggled = new System.Collections.Generic.List<(GameObject, bool)>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            foreach (var r in s.GetRootGameObjects())
            {
                bool want = s.name == sceneName && r.name != "DayWorld" && r.name != "TitleScreen" && (r.activeSelf || r.name == "HellWorld");
                if (r.activeSelf != want) { toggled.Add((r, r.activeSelf)); r.SetActive(want); }
            }
        }
        var go = new GameObject("__shot") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.transform.position = pos;
        cam.transform.rotation = ortho > 0f ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.LookRotation(look - pos);
        cam.fieldOfView = fov; cam.orthographic = ortho > 0f; cam.orthographicSize = ortho;
        cam.nearClipPlane = ortho > 0f && !float.IsNaN(cutAbove) ? Mathf.Max(0.05f, pos.y - cutAbove) : 0.1f;
        cam.farClipPlane = 800f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.65f, 0.8f);
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        bool fog = RenderSettings.fog; RenderSettings.fog = false;
        Color amb = RenderSettings.ambientLight; var mode = RenderSettings.ambientMode;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.75f, 0.75f, 0.75f);
        try { cam.Render(); }
        finally
        {
            RenderSettings.fog = fog; RenderSettings.ambientMode = mode; RenderSettings.ambientLight = amb;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(file, tex.EncodeToJPG(85));
            cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
            foreach (var (g, was) in toggled) g.SetActive(was);
        }
    }
}
