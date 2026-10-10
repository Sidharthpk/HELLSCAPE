using System.Collections.Generic;
using UnityEngine;

// Only the shadow-casting lamps nearest the camera cast shadows. The prologue's flats have 14 point lights and 5
// spots with shadows on: 89 shadow maps a frame, which URP squeezes into its atlas by cutting their resolution (the
// "Reduced additional punctual light shadows resolution" warning, hundreds of times a session) and which costs
// frames. A point light is 6 maps, a spot 1; the furthest ones over the budget have their shadows switched off, and
// get them back when you come near. 16 maps is what the 4096 atlas holds at full size (1024 each): no more
// squeezing, no more warning. Looks after every scene by itself: nothing to add to a scene.
public class ShadowBudget : MonoBehaviour
{
    public static int Maps = 16;            // two point lights and four spots (raise it and URP halves every shadow map again)

    readonly Dictionary<Light, LightShadows> casters = new Dictionary<Light, LightShadows>();
    readonly List<Light> order = new List<Light>();
    float scan, sort;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Make() { DontDestroyOnLoad(new GameObject("ShadowBudget", typeof(ShadowBudget))); }

    void Update()
    {
        scan -= Time.unscaledDeltaTime; sort -= Time.unscaledDeltaTime;
        if (scan <= 0f)
        {
            // (lamps made at runtime, and a new scene's: every lamp that casts a shadow, with the mode it was given)
            scan = 3f;
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type != LightType.Directional && l.shadows != LightShadows.None) casters[l] = l.shadows;
        }
        if (sort > 0f) return;
        sort = .4f;
        var cam = Camera.main;
        if (cam == null) foreach (var c in Camera.allCameras) if (c.isActiveAndEnabled) { cam = c; break; }
        if (cam == null) return;
        Vector3 eye = cam.transform.position;
        order.Clear();
        foreach (var kv in casters) if (kv.Key != null) order.Add(kv.Key);
        if (order.Count < casters.Count) { casters.Clear(); scan = 0f; return; }   // a scene went: start again
        order.Sort((a, b) => (a.transform.position - eye).sqrMagnitude.CompareTo((b.transform.position - eye).sqrMagnitude));
        int left = Maps;
        foreach (var l in order)
        {
            bool lit = l.isActiveAndEnabled;
            int cost = l.type == LightType.Point ? 6 : 1;
            bool cast = lit && cost <= left;
            if (cast) left -= cost;
            var want = cast || !lit ? casters[l] : LightShadows.None;   // (one that is switched off keeps its own setting)
            if (l.shadows != want) l.shadows = want;
        }
    }
}
