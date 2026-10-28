#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Test aid (editor only): records every LoopDriver each 0.25 s of Play, so the driving can be read back as numbers.
// Add it to any object in Play; DriverRecorder.Report(name) gives the track of one car with its yaw rate and the
// sideways acceleration it felt.
public class DriverRecorder : MonoBehaviour
{
    struct Sample { public float t; public Vector3 p; public float yaw, v; }
    static readonly Dictionary<string, List<Sample>> tracks = new Dictionary<string, List<Sample>>();
    float next;

    // with the EditorPrefs flag "drv.rec" set, every Play starts a recorder that survives scene loads
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!UnityEditor.EditorPrefs.GetBool("drv.rec", false) || FindFirstObjectByType<DriverRecorder>() != null) return;
        var go = new GameObject("Driver recorder");
        DontDestroyOnLoad(go);
        go.AddComponent<DriverRecorder>();
    }

    void Update()
    {
        if (Time.time < next) return;
        next = Time.time + 0.25f;
        foreach (var d in FindObjectsByType<LoopDriver>(FindObjectsSortMode.None))
        {
            string key = d.name + "@" + d.transform.GetSiblingIndex() + d.GetInstanceID() % 1000;
            if (!tracks.TryGetValue(key, out var l)) tracks[key] = l = new List<Sample>();
            l.Add(new Sample { t = Time.time, p = d.transform.position, yaw = d.transform.eulerAngles.y, v = d.Speed });
        }
    }

    // one line per sample: where, speed, yaw rate (deg/s) and lateral acceleration (m/s^2) from v * yaw rate; only samples with any turning
    public static string Report(float minLat)
    {
        var sb = new StringBuilder();
        foreach (var kv in tracks)
        {
            var l = kv.Value; float worst = 0f, maxV = 0f; int hard = 0;
            for (int i = 1; i < l.Count; i++)
            {
                float dt = l[i].t - l[i - 1].t; if (dt <= 0f) continue;
                float dyaw = Mathf.DeltaAngle(l[i - 1].yaw, l[i].yaw) * Mathf.Deg2Rad / dt;
                float lat = Mathf.Abs(dyaw) * l[i].v;
                if (Vector3.Distance(l[i].p, l[i - 1].p) > 20f) continue;   // the jump back to the start
                worst = Mathf.Max(worst, lat); maxV = Mathf.Max(maxV, l[i].v);
                if (lat > minLat) hard++;
            }
            sb.AppendLine(kv.Key + ": samples " + l.Count + " maxV " + maxV.ToString("F1") + " worstLat " + worst.ToString("F2") + " samples>" + minLat + " " + hard);
        }
        return sb.ToString();
    }

    public static string Track(string keyStart, float minLat)
    {
        var sb = new StringBuilder();
        foreach (var kv in tracks)
        {
            if (!kv.Key.StartsWith(keyStart)) continue;
            var l = kv.Value;
            for (int i = 1; i < l.Count; i++)
            {
                float dt = l[i].t - l[i - 1].t; if (dt <= 0f) continue;
                float dyaw = Mathf.DeltaAngle(l[i - 1].yaw, l[i].yaw) * Mathf.Deg2Rad / dt;
                if (Mathf.Abs(dyaw) * l[i].v < minLat && l[i].v > 1f) continue;
                sb.AppendLine(l[i].t.ToString("F1") + " pos " + l[i].p.x.ToString("F0") + "," + l[i].p.z.ToString("F0") + " v " + l[i].v.ToString("F1") + " yawrate " + (dyaw * Mathf.Rad2Deg).ToString("F0") + " lat " + (Mathf.Abs(dyaw) * l[i].v).ToString("F2"));
            }
        }
        return sb.ToString();
    }

    public static void Clear() => tracks.Clear();
}
#endif
