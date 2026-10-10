using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// The prologue's opening flashback: Friday evening, the night before. You walk out through the office to start the
// night run: it opens on Maya, Vane's assistant, at her computer across his desk, then you go past Omar on his
// rounds in the hall outside Vane's door, Ben having it out with the kitchen's vending machine, Nadia typing on the
// open-plan floor and Priya at the reception desk, and out of the front door. Then the van rolls out of the garage, and the
// real prologue starts: Saturday morning, the end of the run.
// It is the same office you come back to in the hell city ("Office Ground Floor", unburnt, on the same spot of this
// scene), with everyone where you later find them. Everything is found by name, so the scene needs no wiring.
public class OfficeFlashback : MonoBehaviour
{
    public string timeCard = "FRIDAY  -  10:45 PM";
    public static bool Playing { get; private set; }   // the radio briefing waits for it
    public float eye = 1.6f;                // above the office floor
    public float walkSpeed = 3.2f;          // (a big floor: 140 m from Vane's door to the front door)

    // Maya is in Vane's office, behind his shut door: one held shot of her from across the desk, then a cut
    static readonly Vector3 MayaShot = new Vector3(1047.4f, 0f, 905.7f);

    // the walk, leg by leg: corners of the office's own walk map (NewOfficeFit bakes it in MainGameScene), eased off the walls.
    // Each leg ends where you stand to talk: beside Ben (his profile, the machine in front of him), across the desk
    // from Nadia and across the counter from Priya. The people are where the scene has them: move one and her leg's
    // last point wants moving. (ToPriya goes straight through the open-plan floor's south door: in the hell city that
    // door is bolted and cut out of the walk map.)
    static readonly Vector3[] ToOmar = { new Vector3(1039.00f, 0f, 905.80f), new Vector3(1033.50f, 0f, 905.60f) };
    static readonly Vector3[] ToBen = { new Vector3(1033.50f, 0f, 905.60f), new Vector3(1012.83f, 0f, 906.67f), new Vector3(1012.50f, 0f, 907.00f), new Vector3(1009.33f, 0f, 915.67f), new Vector3(1009.00f, 0f, 916.00f), new Vector3(999.00f, 0f, 918.17f), new Vector3(993.20f, 0f, 918.50f) };
    static readonly Vector3[] ToNadia = { new Vector3(993.20f, 0f, 918.50f), new Vector3(999.00f, 0f, 918.17f), new Vector3(999.33f, 0f, 917.83f), new Vector3(999.33f, 0f, 914.83f), new Vector3(998.83f, 0f, 913.00f), new Vector3(998.50f, 0f, 912.67f), new Vector3(1000.00f, 0f, 910.90f) };   // (off to one side of her desk: from straight across it her own monitor hides her)
    static readonly Vector3[] ToPriya = { new Vector3(1000.00f, 0f, 910.90f), new Vector3(995.80f, 0f, 910.90f), new Vector3(990.85f, 0f, 905.00f), new Vector3(990.85f, 0f, 898.60f), new Vector3(989.50f, 0f, 898.17f), new Vector3(986.80f, 0f, 897.90f), new Vector3(986.80f, 0f, 895.10f), new Vector3(986.60f, 0f, 886.60f), new Vector3(983.67f, 0f, 883.67f), new Vector3(983.33f, 0f, 883.33f), new Vector3(977.67f, 0f, 882.17f), new Vector3(976.50f, 0f, 882.17f), new Vector3(976.17f, 0f, 882.50f), new Vector3(975.20f, 0f, 889.50f), new Vector3(975.20f, 0f, 895.20f), new Vector3(972.60f, 0f, 895.60f) };   // (up the aisle east of the waiting seats, round their north end to the counter)
    static readonly Vector3[] ToDoor = { new Vector3(972.60f, 0f, 895.60f), new Vector3(975.20f, 0f, 895.20f), new Vector3(975.20f, 0f, 889.50f), new Vector3(976.17f, 0f, 882.50f), new Vector3(982.50f, 0f, 872.20f) };

    public IEnumerator Run(PrologueDirector d)
    {
        Playing = true;
        // the office's own background noise replaces the street loop while the flashback plays
        var amb = d.ambience; var street = amb != null ? amb.clip : null;
        var office = Sounds.Clip("Prologue/Office ambience", null);
        if (amb != null && office != null) { amb.Stop(); amb.clip = office; amb.loop = true; amb.Play(); }
        yield return Play(d);
        if (amb != null && office != null) { amb.Stop(); amb.clip = street; amb.Play(); }
        Playing = false;
    }

    IEnumerator Play(PrologueDirector d)
    {
        var priya = GameObject.Find("Priya");
        var nadia = GameObject.Find("Nadia");
        if (priya == null || nadia == null || d.cine == null) yield break;   // an older scene: just skip it

        var typing = Resources.Load<RuntimeAnimatorController>("Typing");
        var maya = GameObject.Find("Maya");
        foreach (var g in new[] { priya, nadia, maya })
        {
            var a = g != null ? g.GetComponent<Animator>() : null;
            if (a == null) continue;
            if (typing != null) a.runtimeAnimatorController = typing;
            a.applyRootMotion = false;           // (sat on her chair, as placed in the scene)
            a.Play("Typing", 0, Random.value);   // not in step with each other
        }

        const float floor = 3.4f;   // the office's (the seated ones are on their chairs, a little higher)
        var daylight = Interior(floor, d.cine);
        Vector3 At(Vector3 p) { p.y = floor + eye; return p; }
        var cam = d.cine;
        var ct = cam.transform;
        var data = cam.GetUniversalAdditionalCameraData();
        bool post0 = data.renderPostProcessing;
        data.renderPostProcessing = false;   // the cutscene camera is plain: no post-processing

        // a title over black, then the picture comes up on the office door
        var cardFont = Resources.Load<TMPro.TMP_FontAsset>("SourceSans3-Semibold SDF");
        if (cardFont != null) { d.titleCard.font = cardFont; d.titleCard.fontSharedMaterial = cardFont.material; }
        d.titleCard.text = timeCard;
        d.titleCard.alpha = 0f;
        yield return Fade(d, 1f, 1.2f);
        yield return new WaitForSeconds(1.8f);
        yield return Fade(d, 0f, 1f);

        // Omar paces the hall outside Vane's door, Ben is at the vending machine in the kitchen (both placed in the scene)
        var omar = GameObject.Find("Omar");
        var ben = GameObject.Find("Ben");
        Vector3 omarHome = omar != null ? omar.transform.position : default;
        Quaternion omarRot = omar != null ? omar.transform.rotation : default;
        var omarAnim = omar != null ? omar.GetComponent<Animator>() : null;
        var benAnim = ben != null ? ben.GetComponent<Animator>() : null;
        var angry = Resources.Load<RuntimeAnimatorController>("AngryPoint");   // (the user's Mixamo "Angry Point", looped)
        if (benAnim != null && angry != null) { benAnim.runtimeAnimatorController = angry; benAnim.applyRootMotion = false; }
        var omarCtrl = omarAnim != null ? omarAnim.runtimeAnimatorController : null;
        bool hall = omar != null && omarAnim != null;
        if (hall)
        {
            var walk = Resources.Load<RuntimeAnimatorController>("OfficeWalk");
            if (walk != null) omarAnim.runtimeAnimatorController = walk;
            omarAnim.applyRootMotion = false;
            omarAnim.speed = 0.7f;
            omar.transform.position = new Vector3(PaceEnds[0].x, omarHome.y, PaceEnds[0].z);
            paceOn = true;
            d.StartCoroutine(Pace(omar.transform));
        }

        cam.fieldOfView = 70f;
        cam.enabled = true;
        CutsceneHUD.Hide(true);
        d.StartCoroutine(d.fader.FadeTo(0f, 1.5f));
        d.StartCoroutine(Track(ct));
        Vector3[] Leg(Vector3[] pts) { var r = new Vector3[pts.Length]; for (int i = 0; i < pts.Length; i++) r[i] = At(pts[i]); return r; }
        IEnumerator Talk(Transform who, float height, string lines)
        {
            lookAt = who; lookHeight = height;
            Say(lines);
            yield return Wait();
            lookAt = null;
        }
        if (maya != null)
        {
            // Vane's assistant, typing at the computer on his desk: a slow push in on her, then cut to the walk
            Vector3 from = At(MayaShot), look = maya.transform.position + Vector3.up * 1.05f, push = (look - from).normalized * 0.7f;
            ct.SetPositionAndRotation(from, Quaternion.LookRotation(look - from));
            for (float t = 0f; t < 4f; t += Time.deltaTime) { ct.position = from + push * Mathf.SmoothStep(0f, 1f, t / 4f); yield return null; }
            yield return d.fader.FadeTo(1f, 0.5f);
            ct.SetPositionAndRotation(At(ToOmar[0]), Quaternion.LookRotation(Vector3.left));
            d.StartCoroutine(d.fader.FadeTo(0f, 0.8f));
        }
        ct.SetPositionAndRotation(At(ToOmar[0]), Quaternion.LookRotation(Vector3.left));
        yield return new WaitForSeconds(0.6f);

        // the hall outside Vane's door: Omar, walking his rounds
        yield return Path(ct, Leg(ToOmar), walkSpeed);
        if (hall) yield return Talk(omar.transform, 1.55f, HS.S("1-4"));

        // past him, round the open-plan floor by the corridors and into the kitchen: Ben, telling the vending machine what he thinks of it
        yield return Path(ct, Leg(ToBen), walkSpeed);
        paceOn = false;
        if (ben != null) yield return Talk(ben.transform, 1.5f, HS.S("14-16"));

        // back out and onto the open-plan floor: Nadia at her desk. Then its south door, the corridor and reception: Priya at the counter
        yield return Path(ct, Leg(ToNadia), walkSpeed);
        yield return Talk(nadia.transform, 1.1f, HS.S("5-10"));
        yield return Path(ct, Leg(ToPriya), walkSpeed);
        yield return Talk(priya.transform, 1.1f, HS.S("11-13"));

        if (hall)
        {
            omar.transform.SetPositionAndRotation(omarHome, omarRot);
            omarAnim.runtimeAnimatorController = omarCtrl;
            omarAnim.speed = 1f;
        }

        // out through the lobby to the front door, and the garage (Maya on the radio as you go)
        Say(HS.S("17-19"));
        yield return Path(ct, Leg(ToDoor), walkSpeed * 0.8f);
        yield return Wait();
        yield return d.fader.FadeTo(1f, 0.8f);

        daylight();
        cam.enabled = false;
        yield return DriveOut(d, ct, cam);

        data.renderPostProcessing = post0;
        CutsceneHUD.Hide(false);
        // (the van's back where the deliveries expect it; the Saturday card and the run are the director's)
    }

    static readonly Vector3 StreetCam = new Vector3(82f, 1.8f, -18f);   // in the road, across from the garage mouth

    // the engine starts, the van rolls out of the bay for two seconds, and that's the shot
    IEnumerator DriveOut(PrologueDirector d, Transform ct, Camera cam)
    {
        var van = FindFirstObjectByType<PrologueVan>(FindObjectsInactive.Include);
        var bay = GameObject.Find("Garage parking bay");
        var drive = GameObject.Find("Garage driveway");
        if (van == null || bay == null || drive == null) yield break;

        Rigidbody rb = van.body;
        Vector3 home = van.transform.position;
        Quaternion homeRot = van.transform.rotation;
        bool kin = rb != null && rb.isKinematic;
        if (rb != null) rb.isKinematic = true;

        Vector3 from = bay.transform.position, to = drive.transform.position;
        Vector3 dir = (to - from); dir.y = 0f; dir.Normalize();
        van.transform.SetPositionAndRotation(from, Quaternion.LookRotation(dir));
        ct.position = StreetCam;
        ct.rotation = Quaternion.LookRotation(Vector3.Lerp(from, to, 0.5f) + Vector3.up * 1.2f - StreetCam);
        cam.fieldOfView = 55f;
        var night = Night(cam);
        cam.enabled = true;
        d.StartCoroutine(d.fader.FadeTo(0f, 0.8f));

        if (van.engine != null) { van.engine.loop = true; van.engine.volume = 0.25f; van.engine.Play(); }
        yield return new WaitForSeconds(1.5f);   // the engine turning over
        const float seconds = 2f;
        float total = Vector3.Distance(from, to);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / seconds);
            Vector3 p = Vector3.Lerp(from, to, k * k);   // from rest, picking up
            van.transform.position = p;                  // (never leaves the line from the bay to the drive: no walls, no footpath)
            ct.rotation = Quaternion.Slerp(ct.rotation, Quaternion.LookRotation(p + Vector3.up * 1.2f - ct.position), 3f * Time.deltaTime);
            if (t > seconds - 0.6f) d.fader.fade.alpha = Mathf.InverseLerp(seconds - 0.6f, seconds, t);
            yield return null;
        }
        yield return d.fader.FadeTo(1f, 0.1f);
        if (van.engine != null) van.engine.Stop();
        cam.enabled = false;
        night();
        van.transform.SetPositionAndRotation(home, homeRot);
        if (rb != null) { rb.isKinematic = kin; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        Physics.SyncTransforms();
        yield return new WaitForSeconds(0.8f);
    }

    // the office at night: no sun through the windows, flat cool ambient and a few LED ceiling panels along the way
    static System.Action Interior(float floor, Camera cam)
    {
        var flags = cam.clearFlags; var bg = cam.backgroundColor;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.03f, 0.07f);   // (10:45 PM out of the windows)
        var suns = new System.Collections.Generic.List<(Light l, float i)>();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { suns.Add((l, l.intensity)); l.intensity = 0f; }
        float amb = RenderSettings.ambientIntensity;
        RenderSettings.ambientIntensity = amb * 0.55f;
        var root = new GameObject("Office LEDs");
        Vector3[] spots = {
            new Vector3(1039f, 0f, 906f), new Vector3(1030f, 0f, 906f), new Vector3(1021f, 0f, 906f), new Vector3(1012f, 0f, 907f),   // the hall outside Vane's door
            new Vector3(1011f, 0f, 913f), new Vector3(1009f, 0f, 918f), new Vector3(1002f, 0f, 917.8f),   // up its east side, the north corridor
            new Vector3(991f, 0f, 920f), new Vector3(993.5f, 0f, 922.5f),   // kitchen
            new Vector3(999f, 0f, 911f), new Vector3(993f, 0f, 905f), new Vector3(988f, 0f, 900f),   // open-plan floor
            new Vector3(986.5f, 0f, 891f), new Vector3(983f, 0f, 885f), new Vector3(979f, 0f, 879f), new Vector3(982.5f, 0f, 873f),   // corridor, lobby
            new Vector3(974f, 0f, 887.5f), new Vector3(972f, 0f, 895f),   // reception
            new Vector3(1048f, 0f, 906f), new Vector3(1053f, 0f, 903f) };   // Vane's office
        foreach (var p in spots)
        {
            var go = new GameObject("LED");
            go.transform.SetParent(root.transform);
            go.transform.position = new Vector3(p.x, floor + 2.6f, p.z);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.82f, 0.93f, 1f); l.intensity = 3.2f; l.range = 11f; l.shadows = LightShadows.None;
        }
        return () =>
        {
            foreach (var (l, i) in suns) if (l != null) l.intensity = i;
            RenderSettings.ambientIntensity = amb;
            cam.clearFlags = flags; cam.backgroundColor = bg;
            if (root != null) Destroy(root);
        };
    }

    // dims the day (suns, ambient, sky, fog) and returns the call that puts it all back
    static System.Action Night(Camera cam)
    {
        var suns = new System.Collections.Generic.List<(Light l, float i)>();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { suns.Add((l, l.intensity)); l.intensity *= 0.1f; }
        float amb = RenderSettings.ambientIntensity; Color fog = RenderSettings.fogColor; var flags = cam.clearFlags; var bg = cam.backgroundColor;
        RenderSettings.ambientIntensity = amb * 0.25f;
        RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.08f);
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.03f, 0.07f);
        return () =>
        {
            foreach (var (l, i) in suns) if (l != null) l.intensity = i;
            RenderSettings.ambientIntensity = amb; RenderSettings.fogColor = fog; cam.clearFlags = flags; cam.backgroundColor = bg;
        };
    }

    // eased walk with a footstep bob; the camera turns to look at `look` as it goes
    static IEnumerator Walk(Transform ct, Vector3 from, Vector3 to, Vector3 look, float speed)
    {
        float dur = Mathf.Max(0.5f, Vector3.Distance(from, to) / speed);
        Quaternion r0 = ct.rotation;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            Vector3 p = Vector3.Lerp(from, to, k);
            p.y += Mathf.Sin(t * 8f) * 0.025f * Mathf.Sin(k * Mathf.PI);
            ct.position = p;
            ct.rotation = Quaternion.Slerp(r0, Quaternion.LookRotation(look - p), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (dur * 0.7f))));
            yield return null;
        }
        ct.position = to;
        ct.rotation = Quaternion.LookRotation(look - to);
    }

    // Omar's beat: the hall outside Vane's door, along its south wall (you pass on the north side)
    static readonly Vector3[] PaceEnds = { new Vector3(1026f, 0f, 904.7f), new Vector3(1031.5f, 0f, 904.7f) };
    bool paceOn;
    Transform lookAt;
    float lookHeight = 1.3f;

    // up and down it, turning at each end
    IEnumerator Pace(Transform omar)
    {
        int to = 1;
        while (paceOn)
        {
            Vector3 p = omar.position;
            Vector3 goal = new Vector3(PaceEnds[to].x, p.y, PaceEnds[to].z);
            Vector3 dir = goal - p; dir.y = 0f;
            if (dir.magnitude < 0.2f) { to = 1 - to; continue; }
            omar.position = p + dir.normalized * 0.95f * Time.deltaTime;
            omar.rotation = Quaternion.Slerp(omar.rotation, Quaternion.LookRotation(dir), 5f * Time.deltaTime);
            yield return null;
        }
    }

    // the camera keeps its eyes on whoever you're talking to
    IEnumerator Track(Transform ct)
    {
        while (Playing)
        {
            if (lookAt != null)
            {
                Vector3 to = lookAt.position + Vector3.up * lookHeight - ct.position;
                ct.rotation = Quaternion.Slerp(ct.rotation, Quaternion.LookRotation(to), 4f * Time.deltaTime);
            }
            yield return null;
        }
    }

    // a continuous walk along several points, with a footstep bob; it looks where it's going
    static IEnumerator Path(Transform ct, Vector3[] pts, float speed)
    {
        float total = 0f;
        for (int i = 1; i < pts.Length; i++) total += Vector3.Distance(pts[i - 1], pts[i]);
        Vector3 Along(float m)
        {
            for (int i = 1; i < pts.Length; i++)
            {
                float seg = Vector3.Distance(pts[i - 1], pts[i]);
                if (m <= seg || i == pts.Length - 1) return Vector3.Lerp(pts[i - 1], pts[i], Mathf.Clamp01(m / Mathf.Max(seg, 0.01f)));
                m -= seg;
            }
            return pts[pts.Length - 1];
        }
        float dist = 0f, clock = 0f;
        Vector3 start = ct.position;
        while (dist < total)
        {
            float edge = Mathf.Min(dist, total - dist);
            dist += speed * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(edge / 1.2f)) * Time.deltaTime;
            clock += Time.deltaTime;
            Vector3 p = Along(Mathf.Min(dist, total));
            Vector3 ahead = Along(Mathf.Min(dist + 2.5f, total));
            p.y += Mathf.Sin(clock * 8f) * 0.025f;
            ct.position = p;
            Vector3 look = ahead - p; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) ct.rotation = Quaternion.Slerp(ct.rotation, Quaternion.LookRotation(look), 3f * Time.deltaTime);
            yield return null;
        }
        ct.position = pts[pts.Length - 1];
    }

    // chest height of whoever you're looking at
    static Vector3 Face(Vector3 from, GameObject who, float floor) => who.transform.position + Vector3.up * 1.0f;

    static void Say(string lines) { if (DialogueBox.Instance != null) DialogueBox.Instance.Say(lines); }

    static IEnumerator Wait()
    {
        yield return new WaitForSeconds(0.3f);
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;
        yield return new WaitForSeconds(0.4f);
    }

    static IEnumerator Fade(PrologueDirector d, float to, float time)
    {
        float from = d.titleCard.alpha;
        for (float t = 0f; t < time; t += Time.deltaTime) { d.titleCard.alpha = Mathf.Lerp(from, to, t / time); yield return null; }
        d.titleCard.alpha = to;
    }
}
