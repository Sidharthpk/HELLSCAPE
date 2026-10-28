using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Debug: jump straight to the bridge judgement with a forced verdict, to watch either ending (the Demon King
// binding you = "to be continued", or the damned one).
public static class EndingPreview
{
    const string Key = "HellScape.EndingPreview";   // "bound" / "demon" while a preview is starting

    [MenuItem("HellScape/Debug/Play Bound Ending (to be continued)")] static void Bound() => Begin("bound");
    [MenuItem("HellScape/Debug/Play Demon Ending")] static void Demon() => Begin("demon");
    [MenuItem("HellScape/Debug/Play Death Vision (wake after the fall)")] static void Vision() => Begin("vision");
    [MenuItem("HellScape/Debug/Walk The Dream (you play it)")] static void Dream() => Begin("dream");

    static void Begin(string which)
    {
        SessionState.SetString(Key, which);
        if (EditorApplication.isPlaying) Load();
        else
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { SessionState.EraseString(Key); return; }
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MainGameScene.unity");
            EditorApplication.isPlaying = true;
        }
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(Key, "") != "") Load();
        };
    }

    // skip the title screen: arrive in the hell city the way the prologue does, then run the ending
    static void Load()
    {
        SleepSequence.WakeInHell = true;
        if (SessionState.GetString(Key, "") == "vision" || SessionState.GetString(Key, "") == "dream") { SleepSequence.FallSpot = Vector3.zero; SleepSequence.FallFacing = Vector3.forward; }
        SceneManager.sceneLoaded += OnLoaded;
        SceneManager.LoadScene("MainGameScene");
    }

    static void OnLoaded(Scene s, LoadSceneMode m)
    {
        SceneManager.sceneLoaded -= OnLoaded;
        if (SessionState.GetString(Key, "") == "dream") { SessionState.EraseString(Key); return; }   // (the wake-up starts it by itself; no robot)
        if (SessionState.GetString(Key, "") == "vision")
        {
            SessionState.EraseString(Key);
            var ending = Object.FindFirstObjectByType<EndingSequence>(FindObjectsInactive.Include);
            if (ending != null) ending.StartCoroutine(ShootVision(ending));
            return;
        }
        var e = Object.FindFirstObjectByType<EndingSequence>();
        if (e != null) e.StartCoroutine(Run(e, SessionState.GetString(Key, "bound") == "demon"));
        SessionState.EraseString(Key);
    }

    // robot: frames of the death vision (Logs/vision/), the dialogue auto-advancing; a frame every 0.5 s while it runs
    static IEnumerator ShootVision(EndingSequence e)
    {
        string dir = System.IO.Path.Combine(Application.dataPath, "../Logs/vision");
        System.IO.Directory.CreateDirectory(dir);
        var auto = typeof(DialogueBox).GetField("AutoAdvanceAfter");
        e.soulWalkTime = 30f;   // (a shorter walk for the robot: the real one is longer)
        e.StartCoroutine(WalkDream(e));
        for (int i = 0, shots = 0; i < 700 && shots < 260; i++)
        {
            if (auto != null) auto.SetValue(null, 1.2f);
            yield return new WaitForSeconds(0.5f);
            var cam = e.playerCamera;
            if (cam == null || cam.transform.position.y > -2500f) { if (shots > 0) break; continue; }
            var rt = new RenderTexture(640, 360, 24);
            var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
            RenderTexture.active = rt;
            var tex = new Texture2D(640, 360); tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "f" + shots.ToString("00") + ".png"), tex.EncodeToPNG());
            shots++;
        }
        Debug.Log("[robot] vision frames written to Logs/vision");
    }

    // robot legs for the dream hall (no keyboard here): walk straight ahead whenever the dream lets you
    static IEnumerator WalkDream(EndingSequence e)
    {
        var you = e.playerMovementScript.transform;
        var body = you.GetComponent<Rigidbody>();
        float since = -1f;
        while (true)
        {
            yield return null;
            if (!e.playerMovementScript.enabled || you.position.y > -2500f || GameObject.Find("Dream hall") == null) continue;
            if (since < 0f) since = Time.time;
            float t = Time.time - since;
            if ((t > 13f && t < 15f) || (t > 19f && t < 21f) || (t > 25f && t < 26.5f)) { you.rotation = Quaternion.Euler(0f, 180f, 0f); continue; }   // glance back at the stalkers
            you.rotation = Quaternion.identity;
            Vector3 p = you.position + Vector3.forward * 3.4f * Time.deltaTime;
            if (body != null && !body.isKinematic) body.position = p; else you.position = p;
        }
    }

    static IEnumerator Run(EndingSequence e, bool damned)
    {
        yield return new WaitForSeconds(9f);   // let the wake-up on the street finish (it moves the player and camera)

        // (Judgement resets on scene load, so the verdict is forced here)
        Judgement.KilledKiller = damned; Judgement.SparedKiller = !damned; Judgement.Karma = damned ? -3 : 3;
        var rescued = typeof(Survivor).GetProperty("Rescued");
        foreach (var sv in Survivor.All) if (sv != null) rescued.SetValue(sv, !damned);

        // stand on the bridge, 14 m short of where the being lands, facing it
        var being = e.demon;
        Transform player = e.playerCamera.transform.root;
        Vector3 land = being.endPoint.position;
        Vector3 away = player.position - land; away.y = 0f;
        Vector3 pos = land + away.normalized * 14f;
        if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out var hit, 200f)) pos = hit.point + Vector3.up * 1.1f;
        var cc = player.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
        player.SetPositionAndRotation(pos, Quaternion.LookRotation(-away.normalized));
        if (cc) cc.enabled = true;

        e.PlayEnding();
    }
}
