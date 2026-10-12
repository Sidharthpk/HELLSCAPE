using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Debug: robot walk through the office as a player would meet it. HellScape > Debug > Test Office Walkthrough.
// Checks: colleagues hidden at first; each one's scare (run-up or turn) and the pan to their face; talking to one sets
// the Eye roaming; shadow figures turning up; no Damned about. Log: Logs/office/walkthrough.txt, frames beside it.
public static class OfficeWalkthroughTest
{
    const string Key = "HellScape.OfficeWalkthroughTest";
    static string dir => Path.Combine(Application.dataPath, "../Logs/office");
    static StringBuilder log = new StringBuilder();
    static int shots, fails;
    static Camera cam; static Transform player;

    [MenuItem("HellScape/Debug/Test Office Walkthrough")]
    static void Menu() { SessionState.SetString(Key, "1"); EditorApplication.isPlaying = true; }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += st =>
        {
            if (st != PlayModeStateChange.EnteredPlayMode || SessionState.GetString(Key, "") == "") return;
            SleepSequence.WakeInHell = true;
            SceneManager.sceneLoaded += OnLoaded;
            SceneManager.LoadScene("MainGameScene");
        };
    }

    static void OnLoaded(Scene s, LoadSceneMode m)
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SessionState.EraseString(Key);
        var e = Object.FindFirstObjectByType<EndingSequence>();
        if (e != null) e.StartCoroutine(Run(e));
    }

    static void L(string s) { log.AppendLine("[" + Time.time.ToString("0.0") + "] " + s); Debug.Log("[walk] " + s); }
    static void Check(bool ok, string what) { if (!ok) fails++; L((ok ? "PASS " : "FAIL ") + what); }

    static void Shot(string name)
    {
        var rt = new RenderTexture(640, 360, 24);
        var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
        RenderTexture.active = rt;
        var tex = new Texture2D(640, 360); tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(dir, (shots++).ToString("00") + "_" + name + ".png"), tex.EncodeToPNG());
        Object.Destroy(rt); Object.Destroy(tex);
    }

    static void Teleport(Vector3 p, float yaw)
    {
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.position = p; }
        player.position = p;
        player.rotation = Quaternion.Euler(0f, yaw, 0f);
        Physics.SyncTransforms();
    }

    static IEnumerator Wait(float s)
    {
        for (float t = 0f; t < s; t += Time.deltaTime) { DialogueBox.AutoAdvanceAfter = 1f; yield return null; }
    }

    static string EyeState(OfficeEye eye) => eye == null ? "none" : eye.GetType().GetField("state", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(eye).ToString();

    // a floor point 4-6 m from the colleague's home, in the office, reachable on the navmesh
    static Vector3 Near(Vector3 home)
    {
        for (int i = 0; i < 40; i++)
        {
            Vector3 p = home + Quaternion.Euler(0f, i * 31f, 0f) * Vector3.forward * (4f + (i % 3));
            if (OfficeHorrorDirector.In(p) && NavMesh.SamplePosition(p, out var h, 1.5f, NavMesh.AllAreas)) return h.position;
        }
        return home + Vector3.forward * 4f;
    }

    static IEnumerator Run(EndingSequence e)
    {
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
        log.Clear(); shots = 0; fails = 0;
        yield return new WaitForSeconds(9f);
        cam = e.playerCamera; player = cam.transform.root;
        var fpc = player.GetComponentInChildren<FirstPersonController>();
        fpc.cameraCanMove = false;   // (or the user's mouse turns the robot)
        var horror = Object.FindFirstObjectByType<OfficeHorrorDirector>();
        if (horror == null) { L("no director"); yield break; }
        var eye = horror.Eye;
        var names = new[] { "Priya", "Ben", "Nadia", "Omar" };
        var ghosts = Object.FindObjectsByType<GhostNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var colleagues = names.Select(n => ghosts.FirstOrDefault(g => g.ghostName == n)).ToArray();
        Check(colleagues.All(c => c != null), "all four colleagues exist");
        Check(colleagues.All(c => !c.gameObject.activeSelf), "colleagues are hidden at first");
        Check(Object.FindObjectsByType<Zombie>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(z => OfficeHorrorDirector.In(z.transform.position)).Count() == 0, "no Damned in the office");
        L("eye: " + (eye != null) + ", state " + EyeState(eye));

        // walk in at the first colleague's desk and wait for the scare: turn away once the footsteps have started, then back
        var talked = false;
        for (int k = 0; k < colleagues.Length; k++)
        {
            var g = colleagues[k];
            Vector3 home = g.transform.position;
            Teleport(Near(home), 0f);
            L("at " + g.ghostName + "'s desk, in office=" + OfficeHorrorDirector.In(player.position));
            float t0 = Time.time;
            while (!g.gameObject.activeSelf && Time.time - t0 < 90f)
            {
                DialogueBox.AutoAdvanceAfter = 1f;
                // a player who looks about: the look-back that the footsteps wait for comes within a second whenever they start
                Vector3 d = home - player.position; d.y = 0f;
                bool away = Mathf.FloorToInt((Time.time - t0) / 1f) % 2 == 0;
                player.rotation = Quaternion.LookRotation(away ? -d : d);
                yield return null;
            }
            bool appeared = g.gameObject.activeSelf;
            Check(appeared, g.ghostName + " appears after " + (Time.time - t0).ToString("0.0") + " s near their desk");
            if (!appeared) continue;
            yield return Wait(2.2f);   // the pan runs about 0.9 s
            Vector3 head = g.transform.position + Vector3.up * 1.55f;
            float off = Vector3.Angle(cam.transform.forward, head - cam.transform.position);
            float dist = Vector3.Distance(player.position, g.transform.position);
            L(g.ghostName + ": " + dist.ToString("0.0") + " m away, view " + off.ToString("0") + " deg off their face");
            Check(off < 15f, g.ghostName + " is in the middle of the view after the pan");
            Shot("scare_" + g.ghostName);

            if (!talked)
            {
                talked = true;
                L("eye before talk: " + EyeState(eye));
                g.Talk();
                // answer the way a player does (the first answer each time) until the talk is over
                var select = typeof(ChoiceDialogue).GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Instance);
                for (int i = 0; i < 40 && GhostNPC.Conversing; i++)
                {
                    yield return Wait(.6f);
                    L("  talking, eye " + EyeState(eye) + ", conversing " + GhostNPC.Conversing + ", choices open " + ChoiceDialogue.Instance.Open);
                    if (ChoiceDialogue.Instance.Open) select.Invoke(ChoiceDialogue.Instance, new object[] { 0 });
                }
                L("talk over, conversing " + GhostNPC.Conversing);
                float t1 = Time.time; bool roam = false;
                while (Time.time - t1 < 25f && !roam)
                {
                    yield return Wait(.5f);
                    string s = EyeState(eye);
                    roam = s == "Patrol" || s == "Chase" || s == "Search";
                }
                L("eye after talk: " + EyeState(eye) + " (" + (Time.time - t1).ToString("0.0") + " s after the talk)");
                Check(roam, "the Eye is roaming after you speak to one of them");
                Shot("eye_roaming");
            }
        }

        // shadow figures: stand in the open for a minute and count what turns up
        Teleport(Near(colleagues[0].transform.position), 90f);
        int seen = 0, most = 0; float watch = 75f;
        for (float t = 0f; t < watch; t += Time.deltaTime)
        {
            DialogueBox.AutoAdvanceAfter = 1f;
            int now = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(x => x.name.StartsWith("Shadow figure") || x.name.StartsWith("Shadow runner"));
            if (now > most) { most = now; if (now > 1 && shots < 30) Shot("shadows_" + now); }
            if (now > 0) seen++;
            yield return null;
        }
        L("shadow figures: most at once " + most + ", frames with any " + seen + " of " + (int)(watch * 60));
        Check(most > 0, "shadow figures turn up while you wait");
        L("done, " + fails + " failed; eye " + EyeState(eye) + ", hp " + player.GetComponentInChildren<PlayerHealth>().currentHealth);
        File.WriteAllText(Path.Combine(dir, "walkthrough.txt"), log.ToString());
        Debug.Log("[walk] DONE fails=" + fails);
    }
}
