using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// HellScape > Tests > Post-Credits Call (in Play, any scene with the dialogue box): plays the telephone call with short
// pauses and checks what the player gets, in order: ring, pick up, each line under the name plate its row gives it
// (UNKNOWN until Kessler has said "Derek"), disconnect; and that asking for it a second time plays nothing.
// Result in Library/PostCreditsTest.result.
public static class PostCreditsTest
{
    [MenuItem("HellScape/Tests/Post-Credits Call")]
    public static string Run()
    {
        var d = RevisedPlotDirector.Instance; var box = DialogueBox.Instance;
        if (!EditorApplication.isPlaying || d == null || box == null || box.nameText == null) return Done("FAIL: press Play first (needs the dialogue box and the revised plot director)");
        // at the real ending nothing else is talking: stop whatever story this scene was in the middle of
        foreach (var m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) if (m != box && !(m is MusicManager)) m.StopAllCoroutines();
        box.Clear();
        d.StartCoroutine(Watch(d, box));
        return "running";
    }

    static IEnumerator Watch(RevisedPlotDirector d, DialogueBox box)
    {
        var seen = new List<string>();
        Application.LogCallback log = (m, s, t) => { if (m.StartsWith("[post-credits] ")) seen.Add(m.Substring(15)); };
        Application.logMessageReceived += log;
        DialogueBox.AutoAdvanceAfter = 0.3f;
        string last = "";
        var call = d.PostCredits(0.5f, 0.1f, 0.3f, 0.2f);
        while (call.MoveNext())
        {
            yield return call.Current;
            string now = box.group.alpha > 0f ? box.nameText.text + ": " + box.text.text : "";
            if (now != "" && now != last) seen.Add(now);
            last = now;
        }
        int played = seen.Count;
        yield return d.PostCredits(0.5f, 0.1f, 0.3f, 0.2f);
        DialogueBox.AutoAdvanceAfter = 0f;
        Application.logMessageReceived -= log;

        var want = new List<string> { "ring", "pick up" };
        var problems = new List<string>();
        foreach (var row in RevisedPlotDirector.CallLines())
        {
            if (row.speaker != "KESSLER" && row.speaker != "MERCER") problems.Add(row.id + " is spoken as " + row.speaker + " (V5.1: Kessler and Mercer only)");
            want.Add("line " + row.id.Substring(4)); want.Add(row.speaker + ": " + row.text);   // (the name plate shows the speaker in capitals)
        }
        if (RevisedPlotDirector.CallLines().Exists(r => r.text.Contains("Derek"))) problems.Add("the call mentions Derek");
        want.Add("disconnect"); want.Add("end");
        if (string.Join("\n", seen) != string.Join("\n", want)) problems.Add("order differs from the script");
        if (seen.Count != played) problems.Add("it played a second time");
        Done((problems.Count == 0 ? "PASS" : "FAIL: " + string.Join("; ", problems)) + "\n" + string.Join("\n", seen));
    }

    static string Done(string result)
    {
        File.WriteAllText("Library/PostCreditsTest.result", result);
        if (result.StartsWith("PASS")) Debug.Log("Post-credits call " + result); else Debug.LogError("Post-credits call " + result);
        return result;
    }
}
