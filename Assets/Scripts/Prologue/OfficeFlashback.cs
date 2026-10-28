using System.Collections;
using System.Linq;
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
        var opening = GetComponent<OfficeOpeningSequence>();
        if (opening != null && opening.room != null)
        {
            yield return opening.Run(d);
            yield return DriveOut(d, d.cine.transform, d.cine);
            CutsceneHUD.Hide(false);
        }
        else yield return Play(d);
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
        var cardFont = GameFont.Main;
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
        Say(HS5.S("P01_p01_start"));   // V5.1: One more drop. Then bed.
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
        if (van == null || bay == null) yield break;

        Rigidbody rb = van.body;
        Vector3 home = van.transform.position;
        Quaternion homeRot = van.transform.rotation;
        bool kin = rb != null && rb.isKinematic;
        if (rb != null) rb.isKinematic = true;

        Vector3 from = bay.transform.position, to = drive != null ? drive.transform.position : from + bay.transform.forward * 9f;
        Vector3 dir = (to - from); dir.y = 0f; dir.Normalize();
        van.transform.SetPositionAndRotation(from, Quaternion.LookRotation(dir));
        var opening = GetComponent<OfficeOpeningSequence>();
        var depotMercer = GameObject.Find("Mercer (at the depot)");
        var walker = opening != null ? opening.mercerWalk : null;
        var mercer = walker != null ? null : opening != null && opening.mercer != null ? opening.mercer.gameObject : depotMercer;   // (the walking Mercer replaces the one standing at the bay)
        Vector3 mercerHome = mercer != null ? mercer.transform.position : default;
        Quaternion mercerRotation = mercer != null ? mercer.transform.rotation : default;
        if (GetComponent<OfficeOpeningSequence>() != null && mercer != null)
        {
            if (depotMercer != null && depotMercer != mercer) depotMercer.SetActive(false);
            mercer.SetActive(true);
            Vector3 feet = from - van.transform.right * 2.1f + van.transform.forward * 1.6f;
            if(Physics.Raycast(feet+Vector3.up*2f,Vector3.down,out var ground,6f,~0,QueryTriggerInteraction.Ignore)) feet=ground.point;
            mercer.transform.SetPositionAndRotation(feet + Vector3.up*(opening.mercer != null ? mercerHome.y : 0f), Quaternion.LookRotation(-van.transform.right));
            if (opening.mercer != null) opening.mercer.Play("Idle",0,0f);
            ct.position = mercer.transform.position - van.transform.right * 2.8f + van.transform.forward * .4f + Vector3.up * 1.6f;
            ct.LookAt(mercer.transform.position + Vector3.up * 1.45f);
        }
        else ct.position = StreetCam;
        if (GetComponent<OfficeOpeningSequence>() == null || mercer == null) ct.rotation = Quaternion.LookRotation(Vector3.Lerp(from, to, 0.5f) + Vector3.up * 1.2f - ct.position);
        cam.fieldOfView = 55f;
        var night = Night(cam);
        GameObject faceLight = null;
        if(opening != null)
        {
            faceLight = new GameObject("Depot conversation light");
            faceLight.transform.position = ct.position+Vector3.up*.4f;
            var lamp=faceLight.AddComponent<Light>(); lamp.type=LightType.Point; lamp.range=8f; lamp.intensity=3f; lamp.color=new Color(1f,.86f,.7f);
        }
        cam.enabled = true;
        d.StartCoroutine(d.fader.FadeTo(0f, 0.8f));

        if (walker != null)
        {
            if (depotMercer != null) depotMercer.SetActive(false);
            if (faceLight != null) Destroy(faceLight);
            yield return WalkWithMercer(d, ct, cam, walker, from, dir);
            yield return d.fader.FadeTo(1f, .35f);   // (you get in: the engine is next)
            walker.gameObject.SetActive(false);
            ct.position = from - van.transform.right*7f + dir*4f + Vector3.up*2.5f;
            ct.LookAt(Vector3.Lerp(from, to, .5f) + Vector3.up * 1.2f);
            yield return d.fader.FadeTo(0f, .5f);
        }

        if (GetComponent<OfficeOpeningSequence>() != null && mercer != null)
        {
            yield return OfficeOpeningSequence.Speak(HS5.S("P02_dock_enter") + "|" + HS5.S("P02_manifest_pickup") + "|" + HS5.S("P02_dock_leave"));
            yield return d.fader.FadeTo(1f, .35f);
            ct.position = from - van.transform.right*7f + dir*4f + Vector3.up*2.5f;
            ct.LookAt(Vector3.Lerp(from, to, .5f) + Vector3.up * 1.2f);
            mercer.transform.SetPositionAndRotation(mercerHome, mercerRotation);
            if(opening.mercer != null) mercer.SetActive(false);
            if(depotMercer != null && depotMercer != mercer) depotMercer.SetActive(true);
            if(faceLight != null) Destroy(faceLight);
            yield return d.fader.FadeTo(0f, .5f);
        }

        if (van.engine != null) { van.engine.loop = true; van.engine.volume = 0.25f; van.engine.Play(); }
        yield return new WaitForSeconds(1.5f);   // the engine turning over
        // out of the bay, then a left turn onto the street (straight on is the house across it)
        Vector3 corner = from + dir * 9f, turnTo = corner - Vector3.Cross(Vector3.up, dir) * 8f;
        const float seconds = 2.4f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / seconds); k *= k;   // from rest, picking up
            Vector3 p = Vector3.Lerp(Vector3.Lerp(from, corner, k), Vector3.Lerp(corner, turnTo, k), k);
            Vector3 ahead = Vector3.Lerp(corner - from, turnTo - corner, k);
            van.transform.SetPositionAndRotation(p, Quaternion.LookRotation(ahead));
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

    // from the office front to the van in the garage (the street's own coordinates; the last point is worked out from the bay)
    // (the user's own path of "WAypoint1" cubes in the Prologue scene: out of the door, west along the deck, down to the street and
    // along it to the garage. Wide all the way, so Mercer is beside you throughout: GapAt 0 = no single-file stretch)
    static readonly Vector3[] ToVan = { new Vector3(54.8f, 0f, 78.1f), new Vector3(51.22f, 0f, 77.23f), new Vector3(45.78f, 0f, 77.32f), new Vector3(35.40f, 0f, 76.30f), new Vector3(24.95f, 0f, 76.47f), new Vector3(24.78f, 0f, 66.25f), new Vector3(16.30f, 0f, 57.94f) };
    const int GapAt = 0;

    // You step out of the office; "Wait up." and Mercer comes after you, falls in beside you and says the whole depot
    // talk as the two of you walk to the van. The pace follows the talk: it slows near the van rather than arrive before
    // he has finished, and you stop at the van only when the last line is done.
    IEnumerator WalkWithMercer(PrologueDirector d, Transform ct, Camera cam, Animator mw, Vector3 vanAt, Vector3 vanDir)
    {
        var pts = new System.Collections.Generic.List<Vector3>(ToVan) { new Vector3(vanAt.x + .6f, 0f, vanAt.z + 3.9f) };
        float total = 0f;
        for (int i = 1; i < pts.Count; i++) total += Vector3.Distance(pts[i - 1], pts[i]);
        Vector3 Along(float m)
        {
            for (int i = 1; i < pts.Count; i++)
            {
                float seg = Vector3.Distance(pts[i - 1], pts[i]);
                if (m <= seg || i == pts.Count - 1) return Vector3.Lerp(pts[i - 1], pts[i], Mathf.Clamp01(m / Mathf.Max(seg, .01f)));
                m -= seg;
            }
            return pts[pts.Count - 1];
        }
        float gapDist = 0f;
        for (int i = 1; i <= GapAt; i++) gapDist += Vector3.Distance(pts[i - 1], pts[i]);
        float floor = 2.59f;   // the deck outside the door; followed down the slope as we go (the player's and the van's own colliders are skipped)
        float Ground(Vector3 p)
        {
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, floor + 1.5f, p.z), Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (h.collider.transform.IsChildOf(d.fpc.transform) || h.collider.GetComponentInParent<PrologueVan>() != null) continue;
                floor = h.point.y; break;
            }
            return floor;
        }

        Vector3 first = Along(0f), ahead0 = Flat(Along(2f) - first).normalized;
        float dist = 1.2f, bob = 0f, clock = 0f, spoken = -1f;
        Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        float eyeY = Ground(Along(dist)) + 1.65f;
        ct.position = new Vector3(Along(dist).x, eyeY, Along(dist).z);
        ct.rotation = Quaternion.LookRotation(ahead0);
        cam.fieldOfView = 60f;
        var lamp = new GameObject("Depot conversation light");
        lamp.transform.SetParent(ct, false); lamp.transform.localPosition = new Vector3(0f, .3f, 0f);
        var lit = lamp.AddComponent<Light>(); lit.type = LightType.Point; lit.range = 14f; lit.intensity = 6.5f; lit.color = new Color(1f, .86f, .7f);
        float ambBefore = RenderSettings.ambientIntensity;
        RenderSettings.ambientIntensity = Mathf.Max(ambBefore, 1.2f);   // (the night grade leaves the street dull; lifted for the walk, put back after)

        var m = mw.transform;
        mw.gameObject.SetActive(true);
        Vector3 start = Along(0f) - ahead0 * 3.2f;   // still in the doorway, behind you
        m.SetPositionAndRotation(new Vector3(start.x, Ground(start), start.z), Quaternion.LookRotation(ahead0));
        mw.Play("Walk", 0, 0f);
        string lines = "Mercer: Wait up.|" + HS5.S("P02_dock_enter") + "|" + HS5.S("P02_manifest_pickup") + "|" + HS5.S("P02_dock_leave");

        while (true)
        {
            clock += Time.deltaTime;
            if (spoken < 0f && clock > .8f && DialogueBox.Instance != null) { DialogueBox.Instance.Say(lines); spoken = clock; }
            bool talking = spoken < 0f || (DialogueBox.Instance != null && DialogueBox.Instance.Busy);
            float remaining = total - dist;
            if (!talking && remaining < .15f) break;
            float v = 1.9f;
            if (talking && remaining < 8f) v = Mathf.Lerp(.3f, 1.9f, Mathf.Clamp01(remaining / 8f));   // never at the van before the last line
            if (clock < 1.2f) v = .6f;                                                                     // a beat to hear "Wait up." first
            float step = talking ? Mathf.Min(v * Time.deltaTime, Mathf.Max(0f, remaining - .1f)) : Mathf.Min(1.9f * Time.deltaTime, remaining);
            dist += step;
            bob += step;

            Vector3 p = Along(dist), fwd = Flat(Along(dist + 2.5f) - p);
            if (fwd.sqrMagnitude < .01f) fwd = Flat(Along(total) - Along(total - 1f));
            fwd.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            eyeY = Mathf.Lerp(eyeY, Ground(p) + 1.65f, 1f - Mathf.Exp(-8f * Time.deltaTime));   // (eased: the ramp and the steps)
            ct.position = new Vector3(p.x, eyeY + Mathf.Sin(bob * 4.2f) * .03f, p.z);

            // Mercer: catching up first, then in step on your right a pace ahead (in the picture); ahead of you in single file for the gap in the rail
            float wide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Abs(dist - gapDist) / 3.5f - .3f));
            Vector3 want = new Vector3(p.x, 0f, p.z) + side * 1.05f * wide + fwd * Mathf.Lerp(2.4f, 1.7f, wide);
            Vector3 mp = m.position; Vector3 to = Flat(want - mp);
            float gap = to.magnitude;
            float camV = step / Mathf.Max(Time.deltaTime, .0001f);
            float mv = gap < .05f ? camV : camV + Mathf.Clamp(gap * .9f, 0f, 2f);   // (faster while he is catching up)
            Vector3 np = gap < .001f ? mp : mp + to.normalized * Mathf.Min(gap, mv * Time.deltaTime);
            m.position = new Vector3(np.x, Ground(np), np.z);
            floor = Ground(p);   // (the camera's floor is the one we follow)
            Vector3 face = gap > .35f ? to : fwd;
            m.rotation = Quaternion.Slerp(m.rotation, Quaternion.LookRotation(face), 6f * Time.deltaTime);
            mw.speed = Mathf.Clamp(mv / 1.4f, 0f, 1.6f);

            // the view looks where you walk, a little towards him
            Vector3 toHim = m.position + Vector3.up * 1.5f - ct.position;
            Quaternion look = Quaternion.LookRotation(Vector3.Slerp(fwd + Vector3.down * .04f, toHim.normalized, .2f));
            ct.rotation = Quaternion.Slerp(ct.rotation, look, 1f - Mathf.Exp(-4f * Time.deltaTime));
            yield return null;
        }
        mw.speed = 0f;
        yield return new WaitForSeconds(.6f);
        Destroy(lamp);
        RenderSettings.ambientIntensity = ambBefore;
    }

    // the lamps lit along the way at night (NewOfficeFit.DressPrologue hangs a pendant over each one)
    public static readonly Vector3[] LedSpots = {
        new Vector3(1039f, 0f, 906f), new Vector3(1030f, 0f, 906f), new Vector3(1021f, 0f, 906f), new Vector3(1012f, 0f, 907f),   // the hall outside Vane's door
        new Vector3(1011f, 0f, 913f), new Vector3(1009f, 0f, 918f), new Vector3(1002f, 0f, 917.8f),   // up its east side, the north corridor
        new Vector3(991f, 0f, 920f), new Vector3(993.5f, 0f, 922.5f),   // kitchen
        new Vector3(999f, 0f, 911f), new Vector3(993f, 0f, 905f), new Vector3(988f, 0f, 900f),   // open-plan floor
        new Vector3(986.5f, 0f, 891f), new Vector3(983f, 0f, 885f), new Vector3(979f, 0f, 879f), new Vector3(982.5f, 0f, 873f),   // corridor, lobby
        new Vector3(974f, 0f, 887.5f), new Vector3(972f, 0f, 895f),   // reception
        new Vector3(1048f, 0f, 906f), new Vector3(1053f, 0f, 903f) };   // Vane's office

    // the office at night: no sun through the windows, a dim ambient, warm pendant lamps along the way and the
    // computers left on (the scene's "Office night lights", off by day)
    static System.Action Interior(float floor, Camera cam)
    {
        var flags = cam.clearFlags; var bg = cam.backgroundColor;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.03f, 0.07f);   // (10:45 PM out of the windows)
        var suns = new System.Collections.Generic.List<(Light l, float i)>();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { suns.Add((l, l.intensity)); l.intensity = 0f; }
        float amb = RenderSettings.ambientIntensity;
        RenderSettings.ambientIntensity = amb * 0.42f;
        var root = new GameObject("Office LEDs");
        foreach (var p in LedSpots)
        {
            var go = new GameObject("LED");
            go.transform.SetParent(root.transform);
            go.transform.position = new Vector3(p.x, floor + 2.35f, p.z);   // (under its pendant)
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.88f, 0.74f); l.intensity = 3.4f; l.range = 10f; l.shadows = LightShadows.Soft;
        }
        var glow = GameObject.Find("Office Ground Floor")?.transform.Find("Office night lights");
        if (glow != null) glow.gameObject.SetActive(true);
        return () =>
        {
            foreach (var (l, i) in suns) if (l != null) l.intensity = i;
            RenderSettings.ambientIntensity = amb;
            cam.clearFlags = flags; cam.backgroundColor = bg;
            if (root != null) Destroy(root);
            if (glow != null) glow.gameObject.SetActive(false);
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
