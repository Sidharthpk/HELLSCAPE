using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// The prologue's opening flashback: Friday evening, the night before. You walk across the office to clock out for
// the night run, say hi to Priya and Nadia (typing away at their desks), wave goodbye to the room, then the van
// rolls out of the garage. Then the real prologue starts: Saturday morning, the end of the run.
// Everything is found by name, so the scene needs no wiring (Priya and Nadia get Typing.controller here).
public class OfficeFlashback : MonoBehaviour
{
    public string timeCard = "FRIDAY  -  10:45 PM";
    public static bool Playing { get; private set; }   // the radio briefing waits for it
    public float eye = 1.6f;                // above the office floor
    public float walkSpeed = 1.9f;

    // the office (it sits far off from the street, in the same scene): in through the east door, west along the aisle
    static readonly Vector3 Door = new Vector3(993.4f, 0f, 982.2f);
    static readonly Vector3 HelloPriya = new Vector3(987.6f, 0f, 981.8f);
    static readonly Vector3 HelloNadia = new Vector3(990.9f, 0f, 981.8f);   // (walking west: Nadia's desk comes up first)
    static readonly Vector3 Leave = new Vector3(982.4f, 0f, 980.0f);

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
        foreach (var g in new[] { priya, nadia })
        {
            var a = g.GetComponent<Animator>();
            if (a == null) continue;
            if (typing != null) a.runtimeAnimatorController = typing;
            a.applyRootMotion = false;
            g.transform.rotation = Quaternion.Euler(0f, g == priya ? 150f : 90f, 0f);   // turned to her keyboard (they were facing away)
            a.Play("Typing", 0, Random.value);   // not in step with each other
        }

        float floor = priya.transform.position.y;
        var daylight = Interior(floor);
        Vector3 At(Vector3 p) { p.y = floor + eye; return p; }
        var cam = d.cine;
        var ct = cam.transform;
        var data = cam.GetUniversalAdditionalCameraData();
        bool post0 = data.renderPostProcessing;
        data.renderPostProcessing = false;   // the cutscene camera is plain: no post-processing

        // a title over black, then the picture comes up on the office door
        d.titleCard.text = timeCard;
        d.titleCard.alpha = 0f;
        yield return Fade(d, 1f, 1.2f);
        yield return new WaitForSeconds(1.8f);
        yield return Fade(d, 0f, 1f);

        // Omar paces the east hallway, Ben fights the printer in the south-west room (both placed in the scene)
        var omar = GameObject.Find("Omar");
        var ben = GameObject.Find("Character_05");
        Vector3 omarHome = omar != null ? omar.transform.position : default;
        Quaternion omarRot = omar != null ? omar.transform.rotation : default;
        var omarAnim = omar != null ? omar.GetComponent<Animator>() : null;
        var omarCtrl = omarAnim != null ? omarAnim.runtimeAnimatorController : null;
        bool hall = omar != null && omarAnim != null;
        if (hall)
        {
            var walk = Resources.Load<RuntimeAnimatorController>("OfficeWalk");
            if (walk != null) omarAnim.runtimeAnimatorController = walk;
            omarAnim.applyRootMotion = false;
            omarAnim.speed = 0.7f;
            omar.transform.position = new Vector3(HallX, omarHome.y, 973f);
            paceOn = true;
            d.StartCoroutine(Pace(omar.transform));
        }

        cam.fieldOfView = 70f;
        cam.enabled = true;
        CutsceneHUD.Hide(true);
        d.StartCoroutine(d.fader.FadeTo(0f, 1.5f));
        d.StartCoroutine(Track(ct));
        Vector3 Hall(float z) => At(new Vector3(HallCamX, 0f, z));
        if (hall) ct.SetPositionAndRotation(Hall(964f), Quaternion.LookRotation(Vector3.forward));
        else ct.SetPositionAndRotation(At(Door), Quaternion.LookRotation(Vector3.left));
        yield return new WaitForSeconds(0.6f);
        Say("Night run again.");

        if (hall)
        {
            // up the hallway to Omar, who is walking his rounds
            yield return Path(ct, new[] { Hall(964f), Hall(968.8f) }, walkSpeed);
            lookAt = omar.transform; lookHeight = 1.35f;
            Say("Night, Omar.|Omar: Night. Mind the road.");
            yield return Wait();
            lookAt = null;
            // on up the hallway and in through the door to the main office
            yield return Path(ct, new[] { Hall(968.8f), Hall(978.5f), At(new Vector3(1000.75f, 0f, 979.8f)), At(new Vector3(1000.75f, 0f, 982.8f)), At(new Vector3(996.0f, 0f, 983.4f)), At(new Vector3(993.8f, 0f, 983.3f)) }, walkSpeed);
            paceOn = false;
        }
        else yield return new WaitForSeconds(0.4f);
        Vector3 inside = hall ? At(new Vector3(993.8f, 0f, 983.3f)) : At(Door);
        yield return Walk(ct, inside, At(HelloNadia), Face(HelloNadia, nadia, floor), walkSpeed);

        // Nadia's desk first, then Priya's
        Say("Evening, Nadia.|Nadia: Sign the sheet before you go. Vane's been checking.");
        yield return Wait();
        yield return Walk(ct, At(HelloNadia), At(HelloPriya), Face(HelloPriya, priya, floor), walkSpeed * 0.8f);
        Say("Priya.|Priya: Sleep when you're back. You look terrible.");
        yield return Wait();
        Say("Will do. Night, you two.|Nadia: Drive safe.");

        if (ben != null)
        {
            // through to the print room: Ben, glaring at the printer
            Vector3 benStand = At(new Vector3(981.3f, 0f, 975.2f));
            yield return Path(ct, new[] { At(HelloPriya), At(new Vector3(983.5f, 0f, 981.4f)), At(new Vector3(981.9f, 0f, 979.2f)), At(new Vector3(981.75f, 0f, 977.3f)), benStand }, walkSpeed);
            benOn = true;
            d.StartCoroutine(Fidget(ben.transform));
            lookAt = ben.transform; lookHeight = 1.25f;
            Say("Ben: Jammed. Again. Third time tonight.|Ben: Sixty jobs depend on a printer older than I am.|Night, Ben.|Ben: Yeah. Go. ...Come ON, you--");
            yield return Wait();
            lookAt = null;
            benOn = false;
            // out through the south door
            yield return Path(ct, new[] { benStand, At(new Vector3(980.6f, 0f, 972.0f)), At(new Vector3(979.75f, 0f, 969.4f)), At(new Vector3(979.75f, 0f, 965.8f)) }, walkSpeed);
        }
        else
        {
            yield return Walk(ct, At(HelloPriya), At(Leave), At(Leave) + Vector3.left * 5f + Vector3.down * 0.2f, walkSpeed);
            yield return Wait();
        }

        if (hall)
        {
            omar.transform.SetPositionAndRotation(omarHome, omarRot);
            omarAnim.runtimeAnimatorController = omarCtrl;
            omarAnim.speed = 1f;
        }

        // out through the office, to the garage
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
    static System.Action Interior(float floor)
    {
        var suns = new System.Collections.Generic.List<(Light l, float i)>();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { suns.Add((l, l.intensity)); l.intensity = 0f; }
        float amb = RenderSettings.ambientIntensity;
        RenderSettings.ambientIntensity = amb * 0.55f;
        var root = new GameObject("Office LEDs");
        Vector3[] spots = {
            new Vector3(1001.0f, 0f, 966f), new Vector3(1001.0f, 0f, 972f), new Vector3(1001.0f, 0f, 978f),
            new Vector3(998f, 0f, 984f), new Vector3(992f, 0f, 981f), new Vector3(986f, 0f, 979f),
            new Vector3(981.5f, 0f, 975f), new Vector3(981.5f, 0f, 970f), new Vector3(978.7f, 0f, 967f) };
        foreach (var p in spots)
        {
            var go = new GameObject("LED");
            go.transform.SetParent(root.transform);
            go.transform.position = new Vector3(p.x, floor + 3.1f, p.z);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.82f, 0.93f, 1f); l.intensity = 2.2f; l.range = 8f; l.shadows = LightShadows.None;
        }
        return () =>
        {
            foreach (var (l, i) in suns) if (l != null) l.intensity = i;
            RenderSettings.ambientIntensity = amb;
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

    // Omar's hallway (it runs north-south along the east side of the office) and the lane the camera takes
    const float HallX = 1000.5f, HallCamX = 1001.5f;
    bool paceOn, benOn;
    Transform lookAt;
    float lookHeight = 1.3f;

    // up and down the hallway, turning at each end
    IEnumerator Pace(Transform omar)
    {
        float[] ends = { 977.5f, 970.5f };
        int to = 0;
        while (paceOn)
        {
            Vector3 p = omar.position;
            Vector3 goal = new Vector3(HallX, p.y, ends[to]);
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

    // Ben thumps the printer now and then
    IEnumerator Fidget(Transform ben)
    {
        Vector3 p0 = ben.position; Quaternion r0 = ben.rotation;
        while (benOn)
        {
            yield return new WaitForSeconds(Random.Range(1.2f, 2.2f));
            for (float t = 0f; t < 0.3f && benOn; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t / 0.3f * Mathf.PI);
                ben.position = p0 + ben.forward * 0.06f * k;
                ben.rotation = r0 * Quaternion.Euler(0f, -4f * k, 0f);
                yield return null;
            }
            ben.SetPositionAndRotation(p0, r0);
        }
        ben.SetPositionAndRotation(p0, r0);
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
