using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// HellScape > Finale > Place Escape Cameras: the driving cameras, few and readable.
//   - at most 3 before the tunnel and 5 after it (Before / After below)
//   - each stands behind the start of its stretch, looking the way you drive, placed (side, height) where it sees the
//     most road ahead; a stretch ends where its camera can no longer see the road, and the next one takes over
//   - the tunnel and the boss arena use the chase camera
// HellScape > Map > Centre Road Points: the escape road's points moved to the middle of the street (they used to
// run along the walls; the horde, Kessler and these cameras all follow them).
public static class EscapeCameras
{
    const int Before = 3, After = 5;
    const float Step = 4f;          // a look at the road every this many metres
    const float MaxStretch = 135f;

    [MenuItem("HellScape/Map/Centre Road Points")]
    public static void CentreMenu() => Debug.Log(CentreRoad());

    public static string CentreRoad()
    {
        var horde = Object.FindFirstObjectByType<HordeChase>(FindObjectsInactive.Include);
        if (horde == null || horde.road == null) return "no HordeChase road";
        var scene = horde.gameObject.scene;
        var road = horde.road;
        Physics.SyncTransforms();
        var sb = new StringBuilder();
        var moved = new Vector3[road.Length];
        for (int i = 0; i < road.Length; i++)
        {
            Vector3 p = road[i].position;
            Vector3 dir = Flat(road[Mathf.Min(i + 1, road.Length - 1)].position - road[Mathf.Max(i - 1, 0)].position).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            moved[i] = p;
            if (!RoadCentre(scene, p, right, out float shift) || Mathf.Abs(shift) < 0.4f) continue;
            moved[i] = p + right * shift;
            sb.Append($"{road[i].name.Replace("RoadPoint ", "")}:{shift:+0.0;-0.0} ");
        }
        for (int i = 0; i < road.Length; i++)
        {
            Undo.RecordObject(road[i], "Centre road");
            road[i].position = moved[i];
        }
        EditorSceneManager.MarkSceneDirty(scene);
        return "road points moved sideways (m): " + sb;
    }

    // across the street at p: the carriageway is the lowest flat strip between the kerbs (or walls, or rails).
    // shift = how far along 'right' its middle is from p
    static bool RoadCentre(Scene scene, Vector3 p, Vector3 right, out float shift)
    {
        const float Reach = 20f, Step = 0.25f;
        int n = Mathf.RoundToInt(Reach * 2f / Step) + 1;
        var h = new float[n];
        float low = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            Vector3 q = p + right * (-Reach + i * Step);
            h[i] = float.NaN;
            bool blocked = false;
            foreach (var c in Physics.OverlapSphere(q + Vector3.up * 1.4f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                if (c.gameObject.scene == scene && c.attachedRigidbody == null && !MapAudit.Skip(c.transform)) { blocked = true; break; }
            if (blocked) continue;
            foreach (var hit in Physics.RaycastAll(q + Vector3.up * 1.6f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject.scene != scene || hit.collider.attachedRigidbody != null || MapAudit.Skip(hit.collider.transform)) continue;
                if (float.IsNaN(h[i]) || hit.point.y > h[i]) h[i] = hit.point.y;
            }
            if (!float.IsNaN(h[i]) && h[i] < low) low = h[i];
        }
        // only between the walls either side of p (what's behind a building is somebody else's street)
        int mid = n / 2, c0 = mid;
        if (float.IsNaN(h[mid]))
        {
            c0 = -1;
            for (int k = 1; k <= 24 && c0 < 0; k++) { if (!float.IsNaN(h[mid - k])) c0 = mid - k; else if (!float.IsNaN(h[mid + k])) c0 = mid + k; }
            if (c0 < 0) { shift = 0f; return false; }
        }
        int lo = c0, hi = c0;
        while (lo > 0 && !float.IsNaN(h[lo - 1])) lo--;
        while (hi < n - 1 && !float.IsNaN(h[hi + 1])) hi++;
        low = float.MaxValue;
        for (int i = lo; i <= hi; i++) low = Mathf.Min(low, h[i]);
        // the carriageway: the widest strip at the lowest level
        shift = 0f; float best = 0f; bool found = false;
        int start = -1;
        for (int i = lo; i <= hi + 1; i++)
        {
            bool roadHere = i <= hi && h[i] < low + 0.1f;
            if (roadHere && start < 0) start = i;
            if (!roadHere && start >= 0)
            {
                float a = -Reach + start * Step, b = -Reach + (i - 1) * Step;
                if (b - a >= 3.5f && b - a > best) { best = b - a; shift = (a + b) * 0.5f; found = true; }
                start = -1;
            }
        }
        return found;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    [MenuItem("HellScape/Finale/Place Escape Cameras")]
    public static void PlaceMenu() => Debug.Log(Place());

    public static string Place()
    {
        var ci = Object.FindFirstObjectByType<CarInteract>(FindObjectsInactive.Include);
        var horde = Object.FindFirstObjectByType<HordeChase>(FindObjectsInactive.Include);
        if (ci == null || horde == null) return "needs the van (CarInteract) and the HordeChase road";
        var road = horde.road;
        var scene = ci.gameObject.scene;
        var garage = GarageBuilder.Find(scene);
        var sb = new StringBuilder();
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;   // from inside a building its one-sided walls must still block the view
        Physics.SyncTransforms();
        try
        {
            var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Escape Cameras");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Escape Cameras").transform;
            SceneManager.MoveGameObjectToScene(root.gameObject, scene);

            float[] along = new float[road.Length];
            for (int i = 1; i < road.Length; i++) along[i] = along[i - 1] + Flat(road[i].position - road[i - 1].position).magnitude;
            float total = along[road.Length - 1];
            Vector3 lastDir = Flat(road[road.Length - 1].position - road[road.Length - 2].position).normalized;

            Vector3 At(float a, out Vector3 dir)
            {
                if (a >= total) { dir = lastDir; return road[road.Length - 1].position + lastDir * (a - total); }
                if (a <= 0f) { dir = Flat(road[1].position - road[0].position).normalized; return road[0].position + dir * a; }
                int i = 0; while (i < road.Length - 2 && along[i + 1] < a) i++;
                float k = Mathf.InverseLerp(along[i], along[i + 1], a);
                dir = Flat(road[i + 1].position - road[i].position).normalized;
                return Vector3.Lerp(road[i].position, road[i + 1].position, k);
            }
            bool Ignore(Collider c) => c.isTrigger || c.gameObject.scene != scene || c.transform.IsChildOf(ci.transform) || c.GetComponentInParent<Zombie>() != null
                                        || c.GetComponentInParent<DemonBoss>() != null || c.GetComponentInParent<RoadObstacle>() != null
                                        || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) || MapAudit.Skip(c.transform);
            bool Clear(Vector3 a, Vector3 b) => !Physics.RaycastAll(a, b - a, Vector3.Distance(a, b), ~0, QueryTriggerInteraction.Ignore).Any(h => !Ignore(h.collider));
            float GroundY(Vector3 p)
            {
                var h = Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore).Where(x => !Ignore(x.collider)).OrderBy(x => x.distance).FirstOrDefault();
                return h.collider != null ? h.point.y : p.y;
            }
            bool Covered(Vector3 p) => Physics.RaycastAll(p + Vector3.up * 1.5f, Vector3.up, 9f, ~0, QueryTriggerInteraction.Ignore).Any(h => !Ignore(h.collider));
            Vector3 Sample(float a) { var q = At(a, out _); q.y = GroundY(q) + 1.2f; return q; }

            // where the tunnel's roof starts and stops
            float tunnelIn = -1f, tunnelOut = -1f, open = 0f;
            for (float a = 0f; a < total; a += 1f)
            {
                bool c = Covered(At(a, out _));
                if (c && tunnelIn < 0f) tunnelIn = a;
                if (tunnelIn < 0f) continue;
                if (c) { tunnelOut = a; open = 0f; }
                else if ((open += 1f) > 12f)
                {
                    if (tunnelOut - tunnelIn > 40f) break;   // that was the tunnel; whatever roofs come later (the bridge's truss) aren't
                    tunnelIn = tunnelOut = -1f; open = 0f;    // a sign or an arch, not a tunnel: keep looking
                }
            }
            if (tunnelIn < 0f || tunnelOut - tunnelIn <= 40f) { tunnelIn = total; tunnelOut = total; }
            else { tunnelIn = Mathf.Max(0f, tunnelIn - 4f); tunnelOut += 4f; }   // the chase camera picks up just before the mouth
            float end = total + 30f;   // on up the bridge towards the arena

            var spots = new List<FixedCameras.Spot>();
            // one section of open road, covered with as few cameras as can see all of it (and never more than 'budget')
            void Section(float from, float to, int budget, string label, Vector3[] alsoSee)
            {
                // even stretches: no camera is asked to watch the van further away than it has to
                float maxStretch = Mathf.Min(MaxStretch, (to - from) / budget + 10f);
                // every camera as low as still sees its whole stretch; only if that takes too many of them, go higher
                foreach (var heights in new[] { new[] { 5.5f, 7f, 9f, 11f, 14f, 18f, 24f, 30f }, new[] { 11f, 14f, 18f, 24f, 30f }, new[] { 18f, 24f, 30f } })
                {
                    var found = new List<(Vector3 pos, float s0, float s1, int seen, int of)>();
                    float s0 = from;
                    while (s0 < to - 8f && found.Count <= budget)
                    {
                        Vector3 best = Vector3.zero; float bestScore = float.MinValue, bestLen = 0f; int bestSeen = 0, bestOf = 0;
                        foreach (float back in new[] { 6f, 10f, 14f, 18f, 24f })
                        {
                            Vector3 bp = At(s0, out Vector3 d0);
                            // "behind" is back along the way the stretch starts off, not back round the previous bend
                            Vector3 ahead = Flat(At(Mathf.Min(s0 + 18f, to), out _) - bp).normalized;
                            if (ahead.sqrMagnitude < 0.5f) ahead = d0;
                            Vector3 across = Vector3.Cross(Vector3.up, ahead);
                            foreach (float side in new[] { 0f, -3f, 3f, -6f, 6f, -9f, 9f })
                                foreach (float h in heights)
                                {
                                    Vector3 p = bp - ahead * back + across * side;
                                    p.y = GroundY(bp) + h;
                                    if (Physics.OverlapSphere(p, 0.8f, ~0, QueryTriggerInteraction.Ignore).Any(c => !Ignore(c))) continue;       // inside something
                                    // under a roof = inside a building (the bridge's own truss overhead is fine)
                                    if (Physics.RaycastAll(p, Vector3.up, 40f, ~0, QueryTriggerInteraction.Ignore).Any(x => !Ignore(x.collider) && !x.collider.GetComponentsInParent<Transform>().Any(t => t.name.StartsWith("TEXTURES_FOR_SKETCH")))) continue;
                                    if (alsoSee != null && s0 == from && alsoSee.Any(q => !Clear(p, q))) continue;
                                    // how far down the road it can see without a break
                                    float len = 0f; int misses = 0, seen = 0, of = 0;
                                    for (float a = s0; a <= Mathf.Min(to, s0 + maxStretch); a += Step)
                                    {
                                        of++;
                                        if (Clear(p, Sample(a))) { seen++; misses = 0; len = a - s0; }
                                        else if (++misses >= 2) break;   // (one lamp post in the way isn't the end of the view)
                                    }
                                    if (len < 20f && s0 + len < to - Step) continue;
                                    float score = len - Mathf.Abs(h - 8f) * 0.45f - Mathf.Abs(side) * 0.4f - back * 0.15f;   // about 8 m up reads best
                                    if (score > bestScore) { bestScore = score; best = p; bestLen = len; bestSeen = seen; bestOf = of; }
                                }
                        }
                        if (bestScore == float.MinValue) { found.Clear(); break; }   // nothing sees this bit from this height: go higher
                        // hand over a little before it loses sight (unless it simply reached the length it was allowed)
                        float s1 = s0 + bestLen >= to - Step ? to : s0 + Mathf.Max(24f, bestLen >= maxStretch - Step ? bestLen : bestLen - 6f);
                        found.Add((best, s0, s1, bestSeen, bestOf));
                        s0 = s1;
                    }
                    if (found.Count == 0 || found.Count > budget || s0 < to - 8f) continue;
                    foreach (var f in found)
                    {
                        var go = new GameObject($"Cam {spots.Count(s => s.cam != null) + 1} ({label})");
                        go.transform.SetParent(root, false);
                        go.transform.SetPositionAndRotation(f.pos, Quaternion.LookRotation(Sample(Mathf.Lerp(f.s0, f.s1, 0.4f)) - f.pos));
                        spots.Add(new FixedCameras.Spot { cam = go.transform, from = f.s0, to = f.s1 });
                        sb.AppendLine($"  {go.name}: {f.s0:0}-{f.s1:0} m, {f.pos.y - GroundY(At(f.s0, out _)):0} m up at {f.pos:F1}");
                    }
                    return;
                }
                sb.AppendLine($"  ! {label}: no set of {budget} cameras sees {from:0}-{to:0}; the chase camera takes it");
                spots.Add(new FixedCameras.Spot { chase = true, from = from, to = to });
            }

            // the first camera also has to see the van pull out of the garage
            Vector3[] garageView = garage != null ? new[] { garage.TransformPoint(new Vector3(-0.25f, 1.2f, 12.5f)), GarageBuilder.DrivewayMouth(garage) + Vector3.up * 1.2f } : null;
            Section(0f, tunnelIn, Before, "before the tunnel", garageView);
            if (tunnelOut > tunnelIn)
            {
                spots.Add(new FixedCameras.Spot { chase = true, from = tunnelIn, to = tunnelOut });
                sb.AppendLine($"  tunnel {tunnelIn:0}-{tunnelOut:0} m: chase camera");
                Section(tunnelOut, end, After, "after the tunnel", null);
            }
            // the stretches run on from each other with no holes; the first starts before the road does (the garage)
            var roadSpots = spots.Where(s => !s.arena).OrderBy(s => s.from).ToList();
            roadSpots[0].from = -1000f;
            roadSpots[roadSpots.Count - 1].to = 100000f;
            spots.Add(new FixedCameras.Spot { arena = true, chase = true });
            sb.AppendLine("  arena: chase camera");

            var fc = ci.carCamera.GetComponent<FixedCameras>();
            if (fc == null) fc = ci.carCamera.gameObject.AddComponent<FixedCameras>();
            fc.car = ci;
            fc.follow = ci.carCamera.GetComponent<CameraFollow>();
            fc.road = road;
            fc.spots = roadSpots.Concat(spots.Where(s => s.arena)).ToArray();
            EditorUtility.SetDirty(fc);
            EditorSceneManager.MarkSceneDirty(scene);
            int fixedBefore = fc.spots.Count(s => s.cam != null && s.to <= tunnelIn + 1f), fixedAfter = fc.spots.Count(s => s.cam != null && s.from >= tunnelOut - 1f);
            return $"Escape cameras: {fixedBefore} before the tunnel, {fixedAfter} after.\n" + sb;
        }
        finally { Physics.queriesHitBackfaces = backfaces; }
    }
}
