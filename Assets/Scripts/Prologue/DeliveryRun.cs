using System.Collections;
using TMPro;
using UnityEngine;

// The morning's last two drops, before the prologue proper (PrologueDirector runs this first).
// You start in the Kessler & Vane van. For each stop: drive to the beacon, park, get out, hand the parcel to the
// customer waiting on the pavement (their Interactable), hear them out. Then back to the depot bay, park, and the
// keys go through the office's mail slot. A time card, and you're standing outside the office at 7:12, where the
// walk home begins.
public class DeliveryRun : MonoBehaviour
{
    [System.Serializable]
    public class Stop
    {
        public string objective = "Deliver the parcel";
        public Transform bay;                  // park near here (the beacon stands on it)
        public float bayRadius = 12f;
        public Interactable customer;          // "Hand over the parcel"
        public Animator customerAnimator;      // turns to face you, waves (optional)
        [TextArea] public string arriveLine = "";
        [TextArea] public string handOverLines = "";
        [TextArea] public string afterLines = "";   // said once the customer has walked away (a phone call, say)
        public bool onFoot;                    // no parking: the van can't be used, you walk to the bay
        public float exitSpeed = 1.3f;         // how fast they walk back to their door afterwards (an old lady: slower)
        public Transform entrance;             // a building's own entrance (forward = out to the street): they come out of it to where the customer stands and go back in, no door built
        public bool carryBox;                  // a small cardboard box appears in their arms and goes with them, no door built: they just walk off along the pavement
        public bool lateRunner;                // nobody's there; as you head back to the van they shout, run out from `entrance`, slip and fall (no hand-over: the parcel goes on the ground)
        public string waitLine = "";           // what they shout
        public Transform fallAt;               // (lateRunner) they run from `entrance` to here, fall flat, and stay down
        public Transform via;                  // a gap they walk through between `entrance` and where they stand (so not through a fence)
        public Collider emergeTrigger;         // (entrance stops) they start walking out when the van or you enter this box, not once you've parked
        [System.NonSerialized] public bool left;    // the customer has walked off
        [System.NonSerialized] public StopDoor door;  // a door in the building behind them, built when the run starts
    }

    public PrologueVan van;
    public Stop[] stops;
    public Transform depotBay;
    public float depotRadius = 10f;
    public string depotObjective = "Return the van to the depot - Kessler & Vane";
    public Transform afterSpawn;               // where you stand after clocking out (the office front)
    public GameObject beacon;                  // a column of light, moved from stop to stop

    [Header("UI (the director's)")]
    public TextMeshProUGUI objective;
    public TextMeshProUGUI titleCard;
    public ScreenFader fader;

    [Header("Lines")]
    [TextArea] public string startLines = "Two drops left on the sheet. Then the depot, then bed.|Twelve hours on the road for Kessler & Vane. My back's killing me.";
    [TextArea] public string depotLines = "Van's in the bay.|Office lights are off. Door's locked. At this hour? Kessler's never late.|Keys through the mail slot. They land in the tray on my desk, like every Saturday.";
    public string clockOutCard = "7:12 AM";

    public bool Done { get; private set; }
    public bool Started { get; private set; }   // Run() has begun: with Done still false you are mid-delivery
    public bool Talking { get; private set; }   // in a hand-over cutscene (the HUD steps aside)
    GameObject hud;

    // before the opening fade: you're already behind the wheel when the picture comes up
    public void Prepare()
    {
        van.Enter();
        // each customer's own door, in the wall behind them (see Leave), unless they come out of a building's entrance
        foreach (var s in stops)
        {
            if (s.customer == null) continue;
            if (s.entrance != null) s.customer.gameObject.SetActive(false);   // (inside the clinic until you come up the steps)
            else if (s.door == null && !s.carryBox) s.door = StopDoor.Build(s.customer.transform, s.bay.position, s.handOverLines.Contains("Okafor"));
        }
        if (hud == null)
        {
            hud = new GameObject("Delivery HUD"); hud.transform.SetParent(transform, false);
            var h = hud.AddComponent<DeliveryHud>();
            h.Me = () => van != null && van.player != null ? (van.InVan ? van.transform : van.player.transform) : null;
            h.Driving = () => van.InVan;
            h.Cam = () => van.InVan ? van.vanCamera : van.playerCamera;
            h.Show = () => !Done && !Talking && !CutsceneHUD.Hidden && !TitleScreen.Showing && !GameOverScreen.Open && !PauseMenu.Paused;
        }
    }

    public IEnumerator Run()
    {
        Started = true;
        van.Enter();
        Say(startLines);
        for (int i = 0; i < stops.Length; i++)
        {
            var s = stops[i];
            if (s.customer != null) s.customer.enabled = false;
            Objective(s.objective);
            Beacon(s.bay);
            s.left = false;
            if (s.onFoot) SetVanUsable(false);   // (no road to the door: you walk)
            else { van.stopAt = s.bay; van.stopRadius = s.bayRadius; }
            Coroutine early = s.emergeTrigger != null && s.entrance != null && !s.lateRunner && s.customer != null ? StartCoroutine(Emerge(s, true)) : null;

            // parked in the bay, and out of the van (or, on foot, standing there)
            yield return new WaitUntil(() => !van.InVan && Near(s.onFoot ? van.player.transform : van.transform, s.bay, s.bayRadius));
            if (s.onFoot) SetVanUsable(true);
            if (!string.IsNullOrEmpty(s.arriveLine)) Say(s.arriveLine);
            if (s.lateRunner && s.customer != null) yield return LateRunner(s);
            else if (early != null) yield return early;   // (already walking out, or done)
            else if (s.entrance != null && s.customer != null) yield return Emerge(s, false);
            if (!s.lateRunner) Beacon(s.customer != null ? s.customer.transform : null, true);

            bool handed = s.lateRunner;
            if (s.customer != null && !handed)
            {
                s.customer.enabled = true;
                s.customer.onUse.AddListener(() => handed = true);
                yield return new WaitUntil(() => handed);
                s.customer.enabled = false;
                if (s.customerAnimator != null) s.customerAnimator.SetFloat("Speed", 0f);
            }
            Beacon(null);
            yield return Conversation(s);
            if (!string.IsNullOrEmpty(s.afterLines))
            {
                yield return new WaitUntil(() => s.left);
                yield return new WaitForSeconds(0.6f);
                Say(s.afterLines);
                yield return WaitForLines();
            }
        }

        Objective(depotObjective);
        Beacon(depotBay);
        van.stopAt = depotBay; van.stopRadius = depotRadius;
        yield return new WaitUntil(() => !van.InVan && Near(van.transform, depotBay, depotRadius));
        Beacon(null);
        van.Lock();
        Objective(null);
        Say(depotLines);
        yield return WaitForLines();

        // clocked out: a moment of black, the time, and you're outside the office doors
        if (fader != null) yield return fader.FadeTo(1f, 1f);
        if (afterSpawn != null)
        {
            var p = van.player.transform;
            var rb = p.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Vector3.zero;
            p.SetPositionAndRotation(afterSpawn.position, afterSpawn.rotation);
            Physics.SyncTransforms();
        }
        if (titleCard != null)
        {
            var cardFont = GameFont.Main;
            if (cardFont != null) { titleCard.font = cardFont; titleCard.fontSharedMaterial = cardFont.material; }
            titleCard.text = clockOutCard;
            for (float t = 0f; t < 1f; t += Time.deltaTime) { titleCard.alpha = t; yield return null; }
            titleCard.alpha = 1f;
            yield return new WaitForSeconds(1.5f);
            for (float t = 0f; t < 1f; t += Time.deltaTime) { titleCard.alpha = 1f - t; yield return null; }
            titleCard.alpha = 0f;
        }
        if (fader != null) yield return fader.FadeTo(0f, 1.5f);
        Done = true;
        if (hud != null) Destroy(hud);
    }

    // the customer steps out of the building's entrance once you are on your way up to it, and waits where they were
    // placed (the box lands at their feet)
    // (early: on the stop's emergeTrigger while you're still driving up, the beacon stays on the bay)
    IEnumerator Emerge(Stop s, bool early)
    {
        var npc = s.customer.transform;
        Vector3 meet = npc.position; Quaternion face = npc.rotation;
        if (early)
        {
            var box = s.emergeTrigger.bounds;
            yield return new WaitUntil(() => box.Contains(van.transform.position) || box.Contains(van.player.transform.position)
                || (!van.InVan && Near(van.transform, s.bay, s.bayRadius)));   // (missed the box: as before, once parked)
        }
        else
        {
            Beacon(s.entrance, true);
            yield return new WaitUntil(() => Flat(van.player.transform.position - s.entrance.position).magnitude < 30f);
        }
        var col = npc.GetComponent<Collider>(); if (col != null) col.enabled = false;
        npc.SetPositionAndRotation(s.entrance.position, s.entrance.rotation);
        s.customer.gameObject.SetActive(true);
        if (s.via != null) yield return WalkTo(npc, s.customerAnimator, s.via.position, 1.4f, null);
        yield return WalkTo(npc, s.customerAnimator, meet, 1.4f, null);
        npc.rotation = face;
        if (col != null) col.enabled = true;
    }

    // nobody home. Head back to the van and someone shouts: you turn to see them run out (from `entrance` to
    // `fallAt`), fall flat on their face and stay there (the hand-over cutscene follows, them still on the ground)
    IEnumerator LateRunner(Stop s)
    {
        Beacon(null);
        yield return WaitForLines();
        SetVanUsable(false);   // (they catch you before you can drive off)
        var p = van.player.transform;
        float t0 = Time.time;
        yield return new WaitUntil(() => Flat(p.position - van.transform.position).magnitude < 7f || Time.time - t0 > 15f);
        Say(s.waitLine);

        var fpc = van.player.GetComponent<FirstPersonController>();
        var cam = van.playerCamera != null ? van.playerCamera.transform : null;
        if (fpc != null) { fpc.playerCanMove = false; fpc.cameraCanMove = false; }
        var rb = van.player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        var npc = s.customer.transform; var an = s.customerAnimator;
        var col = npc.GetComponent<Collider>(); if (col != null) col.enabled = false;
        if (s.entrance != null) npc.SetPositionAndRotation(s.entrance.position, s.entrance.rotation);
        s.customer.gameObject.SetActive(true);
        if (an != null) { an.speed = 1f; an.SetFloat("Speed", 0f); an.SetBool("Run", true); }

        void Look()
        {
            // the body turns to them, the camera tilts to their head (to the bone once they're down)
            var hb = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 head = hb != null ? hb.position : npc.position + Vector3.up * 1.3f;
            Vector3 flat = Flat(head - p.position);
            if (flat.sqrMagnitude > 0.01f) p.rotation = Quaternion.Slerp(p.rotation, Quaternion.LookRotation(flat), 4f * Time.deltaTime);
            if (cam == null) return;
            float pitch = -Mathf.Atan2(head.y - cam.position.y, flat.magnitude) * Mathf.Rad2Deg;
            cam.localRotation = Quaternion.Slerp(cam.localRotation, Quaternion.Euler(pitch, 0f, 0f), 4f * Time.deltaTime);
        }

        while (true)
        {
            Vector3 to = Flat((s.fallAt != null ? s.fallAt.position : p.position) - npc.position);
            if (to.magnitude < (s.fallAt != null ? 0.15f : 3.4f)) break;
            npc.position += to.normalized * Mathf.Min(4.6f * Time.deltaTime, to.magnitude);
            if (Physics.Raycast(npc.position + Vector3.up * 2f, Vector3.down, out var gh, 4f, ~0, QueryTriggerInteraction.Ignore) && gh.point.y < npc.position.y + 0.6f)
                npc.position = new Vector3(npc.position.x, gh.point.y, npc.position.z);
            npc.rotation = Quaternion.Slerp(npc.rotation, Quaternion.LookRotation(to), 10f * Time.deltaTime);
            Look();
            yield return null;
        }
        // the fall: her clip if the controller has one (and she stays down), else a backwards tip on her feet and up again
        bool clip = SetBool(an, "Fall", true);
        if (an != null) an.SetBool("Run", false);
        if (clip)
            for (float t = 0f; t < 2.6f; t += Time.deltaTime) { Look(); yield return null; }
        else
        {
            Quaternion upright = npc.rotation; Vector3 fwd = npc.forward;
            yield return Blend(0.45f, k =>
            {
                npc.position += fwd * 2.6f * (1f - k) * Time.deltaTime;
                npc.rotation = upright * Quaternion.Euler(-84f * k, 0f, 0f);
                Look();
            });
            for (float t = 0f; t < 1.6f; t += Time.deltaTime) { Look(); yield return null; }
            yield return Blend(1.1f, k => { npc.rotation = upright * Quaternion.Euler(-84f * (1f - k), 0f, 0f); Look(); });
            npc.rotation = upright;
            if (col != null) col.enabled = true;
        }
        if (fpc != null && cam != null) fpc.SetPitch(cam.localEulerAngles.x);
        SetVanUsable(true);
    }

    // the hand-over as a cutscene: you're held still, the customer turns to you, the camera pushes in on them
    // behind letterbox bars while they talk, then it eases back and you're free again
    IEnumerator Conversation(Stop s)
    {
        var cam = van.playerCamera;
        var fpc = van.player.GetComponent<FirstPersonController>();
        var dir = FindFirstObjectByType<PrologueDirector>();
        if (cam == null || s.customer == null) { Say(s.handOverLines); yield return WaitForLines(); yield break; }

        Talking = true;
        if (fpc != null) { fpc.playerCanMove = false; fpc.cameraCanMove = false; }
        var rb = van.player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        Transform npc = s.customer.transform, ct = cam.transform;
        Vector3 pos0 = ct.position; Quaternion rot0 = ct.rotation; float fov0 = cam.fieldOfView;
        Vector3 lpos0 = ct.localPosition; Quaternion lrot0 = ct.localRotation;   // (the camera is parented to the player's head: put it back by local pose, not world, or it floats if the body moved)
        Quaternion npcRot0 = npc.rotation;

        // over the player's shoulder, a couple of metres off, on the customer's face
        Vector3 head = npc.position + Vector3.up * 1.6f;
        Vector3 toPlayer = van.player.transform.position - npc.position; toPlayer.y = 0f;
        toPlayer.Normalize();
        Vector3 camPos = npc.position + toPlayer * 2.1f + Vector3.Cross(Vector3.up, toPlayer) * 0.7f + Vector3.up * 1.55f;
        Quaternion faceRot = Quaternion.LookRotation(toPlayer);
        var headBone = s.customerAnimator != null && s.customerAnimator.isHuman ? s.customerAnimator.GetBoneTransform(HumanBodyBones.Head) : null;
        if (s.lateRunner && headBone != null)
        {
            // she's still face down on the pavement: you stand over her, looking down
            head = headBone.position;
            camPos = head + toPlayer * 1.9f + Vector3.Cross(Vector3.up, toPlayer) * 0.6f + Vector3.up * 1.4f;
            faceRot = npcRot0;
        }
        Quaternion camRot = Quaternion.LookRotation(head - camPos);
        if (s.customerAnimator != null) s.customerAnimator.SetFloat("Speed", 0f);

        yield return Blend(1.2f, k =>
        {
            ct.SetPositionAndRotation(Vector3.Lerp(pos0, camPos, k), Quaternion.Slerp(rot0, camRot, k));
            cam.fieldOfView = Mathf.Lerp(fov0, 38f, k);
            npc.rotation = Quaternion.Slerp(npcRot0, faceRot, k);
            Bars(dir, k);
        });

        Say(s.handOverLines);
        yield return WaitForLines();
        if (s.lateRunner) PutBoxDown(van.player.transform);   // "just put it down there"
        if (s.carryBox) GiveBox(s.customer.transform, SetBool(s.customerAnimator, "Carry", true), s.customerAnimator);   // (in her arms as she turns to go)
        yield return new WaitForSeconds(0.4f);
        StartCoroutine(LeaveThen(s, ct, cam, npc));   // they walk home on their own while the camera eases back to you (it doesn't follow)

        Vector3 camNow = ct.position; Quaternion rotNow = ct.rotation; float fovNow = cam.fieldOfView;
        yield return Blend(1f, k =>
        {
            ct.SetPositionAndRotation(Vector3.Lerp(camNow, ct.parent.TransformPoint(lpos0), k), Quaternion.Slerp(rotNow, ct.parent.rotation * lrot0, k));
            cam.fieldOfView = Mathf.Lerp(fovNow, fov0, k);
            Bars(dir, 1f - k);
        });
        ct.localPosition = lpos0; ct.localRotation = lrot0;
        cam.fieldOfView = fov0;
        Bars(dir, 0f);
        if (fpc != null) { fpc.playerCanMove = true; fpc.cameraCanMove = true; }
        Talking = false;
    }

    // the customer walks off to the door in the building behind them (the camera drifts after them), waits at its
    // side while it opens, goes through into the black, and it shuts behind them
    IEnumerator LeaveThen(Stop s, Transform ct, Camera cam, Transform npc)
    {
        if (!s.lateRunner) yield return Leave(s, ct, cam, npc);   // (the one who fell stays face down where she is)
        s.left = true;
    }

    void SetVanUsable(bool on)
    {
        van.locked = !on;
        if (van.getIn != null) van.getIn.enabled = on;
    }

    IEnumerator Leave(Stop s, Transform ct, Camera cam, Transform npc)
    {
        var door = s.door;
        if (door == null && s.entrance == null && !s.carryBox) { npc.gameObject.SetActive(false); yield break; }   // no wall to put one on: they just go
        var col = npc.GetComponent<Collider>(); if (col != null) col.enabled = false;
        var an = s.customerAnimator;
        Vector3 cam0 = ct.position, npc0 = npc.position;
        float fov0 = cam.fieldOfView;

        IEnumerator Walk(Vector3 to, float speed) => WalkTo(npc, an, to, speed, Follow);
        if (door == null && s.entrance == null)
        {
            // the box under their arm: off along the pavement, the way the road runs, and gone
            yield return Walk(npc.position + Flat(s.bay.forward).normalized * 9f, Mathf.Max(0.5f, s.exitSpeed));
            npc.gameObject.SetActive(false);
            for (float t = 0f; t < 0.4f; t += Time.deltaTime) { Follow(); yield return null; }
            yield return new WaitForSeconds(0.5f);
            yield break;
        }
        void Follow()
        {
            // (the camera no longer drifts after them: it is already easing back to you)
        }

        float v = Mathf.Max(0.5f, s.exitSpeed);
        if (s.entrance != null)
        {
            // back to the entrance and in, the camera drifting after them
            if (s.via != null) yield return Walk(s.via.position, v);
            yield return Walk(s.entrance.position, v);
            // (its forward faces the street; a closed shopfront: up to the glass, not through it)
            Vector3 e0 = s.entrance.position;
            float depth = Physics.CapsuleCast(e0 + Vector3.up * 0.6f, e0 + Vector3.up * 1.6f, 0.28f, -s.entrance.forward, out var wall, 3.5f, ~0, QueryTriggerInteraction.Ignore) ? Mathf.Max(0f, wall.distance - 0.35f) : 3.5f;
            yield return Walk(s.entrance.position - s.entrance.forward * depth, v * 0.8f);
            if (depth >= 3.5f)
            {
                // no building there, just more plaza: on she goes, and is gone once nobody is looking
                yield return Walk(s.entrance.position - s.entrance.forward * 12f, v);
                var rs = npc.GetComponentsInChildren<Renderer>();
                yield return new WaitUntil(() => !System.Array.Exists(rs, r => r.isVisible));
            }
            npc.gameObject.SetActive(false);
            for (float t = 0f; t < 0.4f; t += Time.deltaTime) { Follow(); yield return null; }
            yield return new WaitForSeconds(0.5f);
            yield break;
        }
        yield return Walk(door.Stand, v);
        // turn to the door, a beat, then it opens
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            npc.rotation = Quaternion.Slerp(npc.rotation, Quaternion.LookRotation(Flat(door.Doorway - npc.position)), 7f * Time.deltaTime);
            Follow();
            yield return null;
        }
        yield return door.Open();
        yield return Walk(door.Doorway, v * 0.8f);
        yield return Walk(door.Inside, v * 0.7f);
        npc.gameObject.SetActive(false);
        for (float t = 0f; t < 0.4f; t += Time.deltaTime) { Follow(); yield return null; }
        yield return door.Close();
        yield return new WaitForSeconds(0.5f);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // a small yellowish-brown cardboard box held at the chest (it's their child, so it goes wherever they do)
    // (`big`: the carry-heavy-object walk holds its hands ~0.68 m apart, half a metre out: a box that fills them)
    // on a Humanoid it rides the chest bone (the walk sways her hips sideways, so the root would leave it behind)
    static void GiveBox(Transform npc, bool big, Animator an)
    {
        var box = Box();
        Destroy(box.GetComponent<Collider>());
        if (big) box.transform.localScale = new Vector3(0.6f, 0.4f, 0.42f);
        var chest = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Chest) ?? an.GetBoneTransform(HumanBodyBones.Spine) : null;
        if (big && chest != null)
        {
            box.transform.SetPositionAndRotation(chest.position + npc.rotation * new Vector3(0.03f, 0f, 0.3f), npc.rotation);
            box.transform.SetParent(chest, true);
            return;
        }
        box.transform.SetPositionAndRotation(npc.position + npc.forward * (big ? 0.48f : 0.34f) + Vector3.up * (big ? 1.08f : 1.05f), npc.rotation);
        box.transform.SetParent(npc, true);
    }

    // the same box, left on the pavement a step in front of you
    static void PutBoxDown(Transform player)
    {
        var box = Box();
        Vector3 at = player.position + Flat(player.forward).normalized * 0.9f;
        if (Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out var gh, 3f, ~0, QueryTriggerInteraction.Ignore)) at = gh.point;
        box.transform.SetPositionAndRotation(at + Vector3.up * 0.1f, Quaternion.Euler(0f, player.eulerAngles.y + 17f, 0f));
    }

    static GameObject Box()
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Parcel box";
        box.transform.localScale = new Vector3(0.34f, 0.2f, 0.26f);
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.66f, 0.5f, 0.28f) };
        m.SetFloat("_Smoothness", 0.1f);
        box.GetComponent<Renderer>().sharedMaterial = m;
        return box;
    }

    // false when the controller has no such parameter
    static bool SetBool(Animator an, string name, bool on)
    {
        if (an == null) return false;
        foreach (var p in an.parameters) if (p.name == name) { an.SetBool(name, on); return true; }
        return false;
    }

    // a pedestrian's walk to a point on the ground (`each` runs every frame: the cutscene camera follows)
    static IEnumerator WalkTo(Transform npc, Animator an, Vector3 to, float speed, System.Action each)
    {
        to.y = npc.position.y;
        while (Flat(to - npc.position).magnitude > 0.08f)
        {
            Vector3 step = Flat(to - npc.position);
            npc.position += step.normalized * Mathf.Min(speed * Time.deltaTime, step.magnitude);
            if (Physics.Raycast(npc.position + Vector3.up * 2f, Vector3.down, out var gh, 4f, ~0, QueryTriggerInteraction.Ignore) && gh.point.y < npc.position.y + 0.6f)
                npc.position = new Vector3(npc.position.x, gh.point.y, npc.position.z);
            npc.rotation = Quaternion.Slerp(npc.rotation, Quaternion.LookRotation(step), 6f * Time.deltaTime);
            if (an != null) { an.SetFloat("Speed", 1.3f); an.speed = speed / 1.3f; }
            each?.Invoke();
            yield return null;
        }
        if (an != null) { an.SetFloat("Speed", 0f); an.speed = 1f; }
    }

    static IEnumerator Blend(float time, System.Action<float> apply)
    {
        for (float t = 0f; t < time; t += Time.deltaTime) { apply(Mathf.SmoothStep(0f, 1f, t / time)); yield return null; }
        apply(1f);
    }

    static void Bars(PrologueDirector d, float k)
    {
        if (d == null) return;
        foreach (var b in new[] { d.barTop, d.barBottom })
            if (b != null) b.sizeDelta = new Vector2(b.sizeDelta.x, d.barHeight * k);
    }

    static bool Near(Transform a, Transform b, float r)
    {
        if (a == null || b == null) return false;
        Vector3 d = a.position - b.position; d.y = 0f;
        return d.magnitude < r;
    }

    // a person target: the yellow beacon from afar, a green spinning arrow over their head up close
    public float arrowDistance = 10f;
    Transform person;
    GameObject arrow;

    void Update()
    {
        bool close = false;
        if (person != null && van != null && van.player != null)
        {
            Vector3 me = (van.InVan ? van.transform : van.player.transform).position;
            close = Flat(me - person.position).magnitude < arrowDistance;
        }
        if (beacon != null && person != null) beacon.SetActive(!close);
        if (close && arrow == null) arrow = BuildArrow();
        if (arrow == null) return;
        arrow.SetActive(close);
        if (!close) return;
        arrow.transform.position = person.position + Vector3.up * (2.05f + Mathf.Sin(Time.time * 3f) * 0.06f);
        arrow.transform.rotation = Quaternion.Euler(0f, Time.time * 120f, 0f);
    }

    // a flat arrow pointing down, extruded a little, both sides drawn (unlit green so it reads in the dark)
    static GameObject BuildArrow()
    {
        Vector2[] p = { new(0f, 0f), new(0.2f, 0.22f), new(0.08f, 0.22f), new(0.08f, 0.5f), new(-0.08f, 0.5f), new(-0.08f, 0.22f), new(-0.2f, 0.22f) };
        const float th = 0.04f;
        int n = p.Length;
        var v = new Vector3[n * 2];
        for (int i = 0; i < n; i++) { v[i] = new Vector3(p[i].x, p[i].y, th); v[i + n] = new Vector3(p[i].x, p[i].y, -th); }
        var t = new System.Collections.Generic.List<int> { 0, 1, 6, 2, 3, 4, 2, 4, 5, n, n + 1, n + 6, n + 2, n + 3, n + 4, n + 2, n + 4, n + 5 };
        for (int i = 0; i < n; i++) { int j = (i + 1) % n; t.AddRange(new[] { i, j, j + n, i, j + n, i + n }); }
        int c = t.Count;
        for (int k = 0; k < c; k += 3) t.AddRange(new[] { t[k], t[k + 2], t[k + 1] });   // (reversed copies: visible from every side)
        var mesh = new Mesh { vertices = v, triangles = t.ToArray() };
        mesh.RecalculateBounds();
        var go = new GameObject("Delivery Arrow");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", new Color(0.15f, 1f, 0.25f));
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    void Beacon(Transform at, bool slim = false)
    {
        DeliveryHud.Target = at;
        person = slim ? at : null;
        if (arrow != null && person == null) arrow.SetActive(false);
        if (beacon == null) return;
        beacon.SetActive(at != null);
        if (at == null) return;
        beacon.transform.position = at.position;
        if (beaconWidth <= 0f) beaconWidth = beacon.transform.localScale.x * 1.3f;   // (a little wider than the scene's 1.4)
        float w = slim ? beaconWidth * 0.3f : beaconWidth;   // over a person: a thin shaft, not a wall of light
        beacon.transform.localScale = new Vector3(w, beacon.transform.localScale.y, w);
    }
    private float beaconWidth;

    void Objective(string text)
    {
        if (objective != null) objective.gameObject.SetActive(false);   // (as the director: ObjectiveUI draws it)
        ObjectiveUI.Set(text);
    }

    static void Say(string lines)
    {
        if (!string.IsNullOrEmpty(lines) && DialogueBox.Instance != null) DialogueBox.Instance.Say(lines);
    }

    static IEnumerator WaitForLines()
    {
        yield return null;
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;
    }
}
