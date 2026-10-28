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
        public float exitSpeed = 1.3f;         // how fast they walk back to their door afterwards (an old lady: slower)
        public Transform entrance;             // a building's own entrance (forward = out to the street): they come out of it to where the customer stands and go back in, no door built
        public bool carryBox;                  // a small cardboard box appears in their arms and goes with them, no door built: they just walk off along the pavement
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
            van.stopAt = s.bay; van.stopRadius = s.bayRadius;

            // parked in the bay, and out of the van
            yield return new WaitUntil(() => !van.InVan && Near(van.transform, s.bay, s.bayRadius));
            if (!string.IsNullOrEmpty(s.arriveLine)) Say(s.arriveLine);
            if (s.entrance != null && s.customer != null) yield return Emerge(s);
            Beacon(s.customer != null ? s.customer.transform : null, true);

            bool handed = false;
            if (s.customer != null)
            {
                s.customer.enabled = true;
                s.customer.onUse.AddListener(() => handed = true);
                yield return new WaitUntil(() => handed);
                s.customer.enabled = false;
                if (s.customerAnimator != null) s.customerAnimator.SetFloat("Speed", 0f);
            }
            Beacon(null);
            yield return Conversation(s);
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
    IEnumerator Emerge(Stop s)
    {
        var npc = s.customer.transform;
        Vector3 meet = npc.position; Quaternion face = npc.rotation;
        Beacon(s.entrance, true);
        yield return new WaitUntil(() => Flat(van.player.transform.position - s.entrance.position).magnitude < 30f);
        var col = npc.GetComponent<Collider>(); if (col != null) col.enabled = false;
        npc.SetPositionAndRotation(s.entrance.position, s.entrance.rotation);
        s.customer.gameObject.SetActive(true);
        yield return WalkTo(npc, s.customerAnimator, meet, 1.4f, null);
        npc.rotation = face;
        if (col != null) col.enabled = true;
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
        if (s.carryBox) GiveBox(s.customer.transform);
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
        Quaternion camRot = Quaternion.LookRotation(head - camPos);
        Quaternion faceRot = Quaternion.LookRotation(toPlayer);
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
        yield return new WaitForSeconds(0.4f);
        StartCoroutine(Leave(s, ct, cam, npc));   // they walk home on their own while the camera eases back to you (it doesn't follow)

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
            yield return Walk(s.entrance.position, v);
            yield return Walk(s.entrance.position - s.entrance.forward * 3.5f, v * 0.8f);   // (its forward faces the street)
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
    static void GiveBox(Transform npc)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Parcel box";
        Destroy(box.GetComponent<Collider>());
        box.transform.SetPositionAndRotation(npc.position + npc.forward * 0.34f + Vector3.up * 1.05f, npc.rotation);
        box.transform.localScale = new Vector3(0.34f, 0.2f, 0.26f);
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.66f, 0.5f, 0.28f) };
        m.SetFloat("_Smoothness", 0.1f);
        box.GetComponent<Renderer>().sharedMaterial = m;
        box.transform.SetParent(npc, true);
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

    void Beacon(Transform at, bool slim = false)
    {
        DeliveryHud.Target = at;
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
