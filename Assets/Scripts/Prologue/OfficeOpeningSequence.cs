using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Authored against the furnished v3 room. All actor and camera positions are scene references.
// The camera is the player: it walks from person to person (no fades between them), stops about talkDistance from the
// face, and while somebody talks the background is blurred. A vignette and a lifted grade sit over it all (no film grain).
public class OfficeOpeningSequence : MonoBehaviour
{
    public Transform room;
    public Animator omar, ben, nadia, priya;
    public Animator mercer;
    public Animator mercerWalk;              // Mercer in his casual walk: he catches you up outside the office and walks you to the van (OfficeFlashback.DriveOut)
    public Transform omarShot, benShot, nadiaShot, priyaShot;
    public Transform serverChairShot, serverExitShot;   // (the old start in the server room chair: no longer used)
    public Transform entranceShot;           // where it starts: the server room entrance, looking towards Omar's closet
    public Transform priyaStart, priyaSeat;
    public Transform bucket, bucketGrip;
    public Transform bucketSpot;             // where the bucket waits, next to the cabinets: Omar walks there to fetch it (empty = he stands by it)
    public Vector3 bucketWaypoint;           // a point on his way round the furniture (only x and z count)
    public float omarWalkSpeed = 1.4f;
    public float omarActionSeconds, benActionSeconds, nadiaActionSeconds, priyaSitSeconds;
    public float priyaWalkSpeed = 1.25f;
    public float walkSpeed = 1.8f;           // the player's walk between the people
    public float talkDistance = .95f;        // eye to face while somebody talks: a close-up, never uncomfortably so
    public float talkFov = 52f;              // tighter lens while somebody talks
    public float walkFov = 63f;
    Camera lens;
    bool priyaStarted;
    public Color officeAmbient = new Color(.6f, .58f, .53f);   // the even fill in the office (it has no sun)
    public float priyaSide = -1f;            // which side of her path you stand on (1 = her right, -1 = her left)
    public float cameraLight = .6f;       // a soft light on the view so faces are never black
    public float exposure = 1.1f;            // the room is lifted in the grade, not in the scene
    public string Beat { get; private set; }
    public bool PriyaSeated { get; private set; }
    Coroutine priyaRoutine, focusRoutine;
    Volume grade;
    DepthOfField dof;

    public IEnumerator Run(PrologueDirector d)
    {
        var cam = d.cine;
        bool post = cam.GetUniversalAdditionalCameraData().renderPostProcessing;
        float fov = cam.fieldOfView;
        CutsceneHUD.Hide(true);
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        MakeGrade();
        cam.fieldOfView = walkFov;
        lens = cam;
        priyaStarted = false;
        cam.enabled = true;
        room.gameObject.SetActive(true);
        // the office is lit by its own ceiling lights only: the city's sun and fill would throw shadows from nowhere in here
        var skyLights = new List<Light>();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional && l.enabled) { l.enabled = false; skyLights.Add(l); }
        // and an even fill, so the rooms between the ceiling lights are not black (the night sky gave none)
        var ambMode = RenderSettings.ambientMode; var ambLight = RenderSettings.ambientLight; var ambIntensity = RenderSettings.ambientIntensity;
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = officeAmbient; RenderSettings.ambientIntensity = 1f;
        var fillGo = new GameObject("Opening camera light");
        fillGo.transform.SetParent(cam.transform, false); fillGo.transform.localPosition = new Vector3(0f, .2f, .15f);
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Point; fill.range = 7f; fill.intensity = cameraLight; fill.color = new Color(1f, .95f, .88f); fill.shadows = LightShadows.None;
        omar.Play("Idle"); ben.Play("Idle"); nadia.Play("Seated"); priya.Play("Idle");
        priya.transform.SetPositionAndRotation(priyaStart.position, priyaStart.rotation);
        priya.gameObject.SetActive(false);   // she walks in from off the scene while Ben talks
        PriyaSeated = false;
        try
        {
            Beat = "Friday";
            d.titleCard.text = "FRIDAY  -  10:45 PM";
            d.titleCard.alpha = 1f;
            yield return new WaitForSeconds(1.8f);
            d.titleCard.alpha = 0f;
            // seated in the server room chair, looking through the doorway at Omar's room; with no bucket spot, from the entrance
            SetShot(cam, bucketSpot != null && serverChairShot != null ? serverChairShot : entranceShot);
            yield return d.fader.FadeTo(0f, .8f);
            Beat = "Server room entrance";
            yield return new WaitForSeconds(.6f);
            // Omar is seen walking to the bucket (next to the cabinets), picking it up and carrying it back to his place;
            // with no bucket spot he just stands by it as he always did
            Vector3 home = omar.transform.position;
            Vector3 stand = home;
            if (bucketSpot != null)
            {
                Beat = "Omar walks to the bucket";
                bucket.position = bucketSpot.position;
                Vector3 side = home - bucketSpot.position; side.y = 0f;
                stand = bucketSpot.position + side.normalized * .48f; stand.y = home.y;
                Vector3 round = new Vector3(bucketWaypoint.x, home.y, bucketWaypoint.z);   // clear of the maintenance cabinet
                yield return WalkOmar(cam, round, stand);
                omar.transform.rotation = Quaternion.LookRotation(Flat(bucketSpot.position - stand));
            }
            else
            {
                Beat = "Walking to Omar";
                yield return Approach(d, omar, omarShot);
            }
            Beat = "Omar picks up bucket";
            omar.Play("Action", 0, 0f);
            float duration = Mathf.Max(.5f, omarActionSeconds);
            Vector3 start = bucket.position;
            // you get up from the chair and walk over to him while he picks it up
            Coroutine toOmar = bucketSpot != null ? StartCoroutine(Approach(d, omar, omarShot)) : null;
            // The supplied carry clip begins with the hands low. Raise the bucket into its carry grip.
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t / duration - .1f) / .55f));
                bucket.position = Vector3.Lerp(start, bucketGrip.position, k);
                yield return null;
            }
            bucket.SetParent(bucketGrip, true);
            omar.Play("Idle", 0, 0f);
            if (toOmar != null) yield return toOmar;
            Face(omar, cam.transform.position);
            yield return Aim(cam, omar, .6f);
            Beat = "Omar conversation";
            yield return Speak(HS5.S("P01_omar_talk"));
            Truth.Set("MetOmar");   // V5.1: optional intro callbacks only
            // Priya crosses the office on her way back to her chair: you stop her and talk right there, then watch her
            // go back and sit down while you turn to Ben and Nadia
            Beat = "Priya walking";
            yield return StopPriya(d, cam);
            Beat = "Priya conversation";
            yield return Speak(HS5.S("P01_priya_talk"));
            Truth.Set("MetPriya");   // V5.1: optional intro callbacks only
            Beat = "Priya goes back to her chair";
            // the talk is the end of the scene: she is simply back in her chair, no watching her walk off
            if (priyaRoutine != null) StopCoroutine(priyaRoutine);
            priya.transform.SetPositionAndRotation(priyaSeat.position, priyaSeat.rotation);
            priya.Play("Seated", 0, 0f);
            PriyaSeated = true;
            Beat = "Walking to Ben";
            yield return Approach(d, ben, benShot, true);
            Beat = "Ben hits desktop";
            ben.Play("Action", 0, 0f);
            yield return new WaitForSeconds(Mathf.Max(.4f, benActionSeconds));
            ben.Play("Idle", 0, 0f);
            Face(ben, cam.transform.position);
            yield return Aim(cam, ben, .6f);
            Beat = "Ben conversation";
            yield return Speak(HS5.S("P01_ben_talk"));
            Truth.Set("MetBen");   // V5.1: optional intro callbacks only
            Beat = "Walking to Nadia";
            yield return Approach(d, nadia, nadiaShot);
            Beat = "Nadia stands";
            nadia.Play("Action", 0, 0f);
            yield return new WaitForSeconds(Mathf.Max(.4f, nadiaActionSeconds));
            nadia.Play("Idle", 0, 0f);
            Face(nadia, cam.transform.position);
            yield return Aim(cam, nadia, .6f);
            Beat = "Nadia conversation";
            yield return Speak(HS5.S("P01_nadia_talk"));
            Truth.Set("MetNadia");   // V5.1: optional intro callbacks only
            yield return Speak(HS5.S("P01_office_leave"));   // V5.1: See you tomorrow.
            yield return d.fader.FadeTo(1f, .65f);
            Beat = "Leaving office";
        }
        finally
        {
            if (priyaRoutine != null) StopCoroutine(priyaRoutine);
            if (focusRoutine != null) StopCoroutine(focusRoutine);
            if (grade != null) { Destroy(grade.sharedProfile); Destroy(grade.gameObject); grade = null; }
            priya.gameObject.SetActive(true);
            room.gameObject.SetActive(false);
            if (fillGo != null) Destroy(fillGo);
            foreach (var l in skyLights) if (l != null) l.enabled = true;
            RenderSettings.ambientMode = ambMode; RenderSettings.ambientLight = ambLight; RenderSettings.ambientIntensity = ambIntensity;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = post;
            cam.fieldOfView = fov;
        }
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // the view follows somebody (a gentle turn, no cut)
    void Track(Camera cam, Animator who)
    {
        Quaternion want = Quaternion.LookRotation(Head(who) - cam.transform.position);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, want, 1f - Mathf.Exp(-4f * Time.deltaTime));
    }

    // Omar's own steps (the "Walk" state) along the points, turning to where he goes; the view stays on him
    IEnumerator WalkOmar(Camera cam, params Vector3[] points)
    {
        omar.Play("Walk", 0, 0f);
        omar.speed = omarWalkSpeed / 1.4f;     // (the clip is a 1.4 m/s walk)
        var t = omar.transform;
        foreach (var p in points)
            while (Vector3.Distance(Flat(t.position), Flat(p)) > .03f)
            {
                Vector3 delta = Flat(p - t.position);
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(delta), 7f * Time.deltaTime);
                Vector3 pos = Vector3.MoveTowards(t.position, new Vector3(p.x, t.position.y, p.z), omarWalkSpeed * Time.deltaTime);
                t.position = pos;
                Track(cam, omar);
                yield return null;
            }
        omar.speed = 1f;
    }

    static float SpeechSeconds(string lines)
    {
        float t = 0f;
        foreach (var l in lines.Split('|')) if (l.Trim().Length > 0) t += .9f + .05f * l.Length;
        return t;
    }

    IEnumerator PriyaEnters(float delay)
    {
        yield return new WaitForSeconds(delay);
        priyaStarted = true;
        priya.gameObject.SetActive(true);
        yield return SeatPriya();
    }

    // turn to watch her walk up to reception and sit (the camera stays where it is)
    IEnumerator WatchPriya(Camera cam)
    {
        Focus(false, 0f);
        if (!priyaStarted)
        {
            if (priyaRoutine != null) StopCoroutine(priyaRoutine);
            priyaRoutine = StartCoroutine(PriyaEnters(0f));
            yield return null;
        }
        float give = 25f;
        while (!PriyaSeated && give > 0f)
        {
            give -= Time.deltaTime;
            Vector3 at = priya.transform.position + Vector3.up * 1.1f;
            Quaternion want = Quaternion.LookRotation(at - cam.transform.position);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, want, 1f - Mathf.Exp(-3f * Time.deltaTime));
            yield return null;
        }
        yield return new WaitForSeconds(.8f);
    }

    bool priyaHold, priyaStopped;
    Vector3 priyaStopAt, priyaFace;

    // Priya crossing the office to her chair: cut to just ahead of her on her path, she walks up and stops in front of you
    IEnumerator StopPriya(PrologueDirector d, Camera cam)
    {
        Focus(false, 0f);
        Vector3 start = priyaStart.position, chair = priyaSeat.position - priyaSeat.forward * .65f;
        Vector3 dir = Flat(chair - start).normalized;
        priyaStopAt = start + dir * Mathf.Min(2.8f, Flat(chair - start).magnitude * .55f);
        // you stand at the side of her path (she walks past, stops by you and turns), never in her way
        Vector3 camAt = priyaStopAt + new Vector3(dir.z, 0f, -dir.x) * priyaSide * talkDistance * 1.15f; camAt.y = priyaShot.position.y;
        yield return d.fader.FadeTo(1f, .5f);
        priya.transform.SetPositionAndRotation(start, Quaternion.LookRotation(dir));
        priya.gameObject.SetActive(true);
        priyaHold = true; priyaStopped = false; priyaFace = camAt; priyaStarted = true;
        cam.transform.SetPositionAndRotation(camAt, Quaternion.LookRotation(new Vector3(start.x, camAt.y - .05f, start.z) - camAt));
        PriyaSeated = false;
        priyaRoutine = StartCoroutine(SeatPriya());
        yield return d.fader.FadeTo(0f, .8f);
        float give = 12f;
        while (!priyaStopped && give > 0f) { give -= Time.deltaTime; Track(cam, priya); yield return null; }
        yield return Aim(cam, priya, .5f);
    }

    IEnumerator SeatPriya()
    {
        priya.Play("Walk", 0, 0f);
        var t = priya.transform;
        Vector3 end = priyaSeat.position;
        Vector3 approach = end - priyaSeat.forward * .65f;
        while (true)
        {
            // held where you stopped her (priyaHold), then on to the chair
            Vector3 goal = priyaHold ? priyaStopAt : approach;
            Vector3 delta = goal - t.position; delta.y = 0;
            if (delta.magnitude <= .025f)
            {
                if (!priyaHold) break;
                if (!priyaStopped) { priyaStopped = true; priya.Play("Idle", 0, 0f); }
                Vector3 toYou = priyaFace - t.position; toYou.y = 0;
                if (toYou.sqrMagnitude > .01f) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(toYou), 5f * Time.deltaTime);
                yield return null;
                continue;
            }
            if (priyaStopped) { priyaStopped = false; priya.Play("Walk", 0, 0f); }
            t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(delta), 8f * Time.deltaTime);
            t.position = Vector3.MoveTowards(t.position, new Vector3(goal.x, t.position.y, goal.z), priyaWalkSpeed * Time.deltaTime);
            yield return null;
        }
        t.rotation = priyaSeat.rotation;
        // the sit clip opens with ~5 s of standing/walking in place before she drops into the chair: skip that part
        float skip = Mathf.Min(4.6f, priyaSitSeconds - 1f);
        priya.Play("Action", 0, skip / priyaSitSeconds);
        float duration = Mathf.Max(.5f, priyaSitSeconds - skip);
        for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
        {
            t.position = Vector3.Lerp(approach, end, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        t.SetPositionAndRotation(end, priyaSeat.rotation);
        priya.Play("Seated", 0, 0f);
        PriyaSeated = true;
    }

    // a vignette, a lifted exposure and (while somebody talks) a blurred background
    void MakeGrade()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var grain = profile.Add<FilmGrain>(true);
        grain.intensity.Override(0f);   // (the Prologue's own volume has some: none here)
        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(.3f); vig.smoothness.Override(.6f); vig.rounded.Override(false);
        var ca = profile.Add<ColorAdjustments>(true);
        ca.postExposure.Override(Mathf.Max(exposure, 1.7f));   // (only the office's own lights now: lifted to still read bright) ca.contrast.Override(8f); ca.saturation.Override(-4f); ca.colorFilter.Override(Color.white);
        dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(3f); dof.gaussianEnd.Override(9f); dof.gaussianMaxRadius.Override(0f); dof.highQualitySampling.Override(true);
        dof.active = false;
        var go = new GameObject("Office opening grade");
        grade = go.AddComponent<Volume>();
        grade.isGlobal = true; grade.priority = 50f; grade.sharedProfile = profile; grade.weight = 1f;
    }

    // blur the background (focus on the face at `distance`) or let it sharpen again
    void Focus(bool on, float distance)
    {
        if (dof == null) return;
        if (focusRoutine != null) StopCoroutine(focusRoutine);
        focusRoutine = StartCoroutine(FocusTo(on, distance));
    }

    IEnumerator FocusTo(bool on, float distance)
    {
        // only what is well behind the face blurs (it starts a metre and a half past it): never the face itself
        if (on) { dof.gaussianStart.Override(distance + 1.5f); dof.gaussianEnd.Override(distance + 6f); dof.active = true; }
        float from = dof.gaussianMaxRadius.value, to = on ? 1.4f : 0f;
        float fov0 = lens != null ? lens.fieldOfView : 0f, fov1 = on ? talkFov : walkFov;
        for (float t = 0f; t < .5f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / .5f);
            dof.gaussianMaxRadius.Override(Mathf.Lerp(from, to, k));
            if (lens != null) lens.fieldOfView = Mathf.Lerp(fov0, fov1, k);
            yield return null;
        }
        if (lens != null) lens.fieldOfView = fov1;
        dof.gaussianMaxRadius.Override(to);
        if (!on) dof.active = false;
    }

    static float HeadHeight(Animator a)
    {
        var rs = a.GetComponentsInChildren<Renderer>(false);
        if (rs.Length == 0) return a.transform.position.y + 1.55f;
        float top = rs[0].bounds.max.y;
        foreach (var r in rs) top = Mathf.Max(top, r.bounds.max.y);
        return top - .24f;   // a little below the eyes: head and shoulders framed, eyes in the upper third
    }

    static Vector3 Head(Animator a) => new Vector3(a.transform.position.x, HeadHeight(a), a.transform.position.z);

    static void Face(Animator a, Vector3 toward)
    {
        Vector3 f = toward - a.transform.position; f.y = 0f;
        if (f.sqrMagnitude > .01f) a.transform.rotation = Quaternion.LookRotation(f);
    }

    // settle the view on the face (they may have stood up or turned since we arrived)
    IEnumerator Aim(Camera cam, Animator who, float seconds)
    {
        Quaternion from = cam.transform.rotation;
        Quaternion to = Quaternion.LookRotation(Head(who) - cam.transform.position);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            cam.transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        cam.transform.rotation = to;
        Focus(true, Vector3.Distance(cam.transform.position, Head(who)));
    }

    // walk there instead of cutting: the player's own steps (with a little head bob) to a spot about talkDistance from the
    // face, looking where they are going and turning to the face as they arrive. A shot placed in the scene at a good
    // distance is used as it is; a farther one is replaced by a spot on the way in.
    IEnumerator Approach(PrologueDirector d, Animator who, Transform shot, bool fadeWalk = false)
    {
        var cam = d.cine;
        Focus(false, 0f);
        if (fadeWalk) yield return d.fader.FadeTo(1f, .5f);   // out; the long walk is not shown, it fades back in as the person is reached
        bool fadedBackIn = !fadeWalk;
        Vector3 head = Head(who);
        float eye = shot.position.y;
        Vector3 from = cam.transform.position;
        Vector3 spot = shot.position;
        float away = Vector2.Distance(new Vector2(spot.x, spot.z), new Vector2(who.transform.position.x, who.transform.position.z));
        if (away < .8f || away > 1.3f)
        {
            Vector3 dir = from - who.transform.position; dir.y = 0f;
            if (dir.sqrMagnitude < .01f) dir = -who.transform.forward;
            spot = who.transform.position + dir.normalized * talkDistance;
        }
        spot.y = eye;
        // never a spot inside a wall or a desk: the nearest place on the walk map
        float floorY = eye - 1.6f;
        if (NavMesh.SamplePosition(new Vector3(spot.x, floorY, spot.z), out var onMap, 2.5f, NavMesh.AllAreas)) spot = new Vector3(onMap.position.x, eye, onMap.position.z);

        // the way there: round the furniture if the office has a navmesh, else straight
        var corners = new List<Vector3>();
        var path = new NavMeshPath();
        float floor = eye - 1.6f;
        if (NavMesh.SamplePosition(new Vector3(from.x, floor, from.z), out var a, 2f, NavMesh.AllAreas) &&
            NavMesh.SamplePosition(new Vector3(spot.x, floor, spot.z), out var b, 2f, NavMesh.AllAreas) &&
            NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
            foreach (var c in path.corners) corners.Add(new Vector3(c.x, eye, c.z));
        if (corners.Count < 2) { corners.Clear(); corners.Add(new Vector3(from.x, eye, from.z)); }
        corners[0] = new Vector3(from.x, from.y, from.z);
        corners.Add(spot);

        float total = 0f;
        for (int i = 1; i < corners.Count; i++) total += Vector3.Distance(corners[i - 1], corners[i]);
        float walked = 0f, bob = 0f;
        Vector3 pos = corners[0];
        while (walked < total - .02f)
        {
            float toGo = total - walked;
            if (!fadedBackIn && toGo < 4.5f) { fadedBackIn = true; StartCoroutine(d.fader.FadeTo(0f, 1f)); }
            float step = walkSpeed * (fadeWalk && !fadedBackIn ? 4f : 1f) * Time.deltaTime;
            walked = Mathf.Min(total, walked + step);
            // the point `walked` along the path
            float left = walked; Vector3 p0 = corners[0], p1 = corners[1];
            for (int i = 1; i < corners.Count; i++)
            {
                p0 = corners[i - 1]; p1 = corners[i];
                float len = Vector3.Distance(p0, p1);
                if (left <= len || i == corners.Count - 1) { pos = Vector3.Lerp(p0, p1, len > 0f ? Mathf.Clamp01(left / len) : 1f); break; }
                left -= len;
            }
            bob += step;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - (total - walked) / 3f));      // turn to the face over the last 3 m
            Vector3 ahead = p1 - p0; ahead.y = 0f;
            Quaternion travel = ahead.sqrMagnitude > .001f ? Quaternion.LookRotation(ahead.normalized + Vector3.down * .05f) : cam.transform.rotation;
            Quaternion toFace = Quaternion.LookRotation(head - pos);
            cam.transform.position = pos + Vector3.up * Mathf.Sin(bob * 4.2f) * .03f * (1f - k * .6f);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.Slerp(travel, toFace, k), 1f - Mathf.Exp(-6f * Time.deltaTime));
            yield return null;
        }
        Vector3 end = cam.transform.position; end.y = eye;
        Quaternion endRot = Quaternion.LookRotation(Head(who) - end);
        Quaternion r0 = cam.transform.rotation;
        for (float t = 0f; t < .5f; t += Time.deltaTime)
        {
            cam.transform.position = Vector3.Lerp(cam.transform.position, end, .2f);
            cam.transform.rotation = Quaternion.Slerp(r0, endRot, Mathf.SmoothStep(0f, 1f, t / .5f));
            yield return null;
        }
        cam.transform.SetPositionAndRotation(end, endRot);
        Focus(true, Vector3.Distance(end, Head(who)));
    }

    static void SetShot(Camera cam, Transform shot) => cam.transform.SetPositionAndRotation(shot.position, shot.rotation);
    public static IEnumerator Speak(string line)
    {
        if (DialogueBox.Instance == null) yield break;
        DialogueBox.Instance.Say(line);
        yield return new WaitForSeconds(.3f);
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;
        yield return new WaitForSeconds(.35f);
    }
}
