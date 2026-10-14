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
        [System.NonSerialized] public StopDoor door;   // a door in the building behind them, built when the run starts
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

    // before the opening fade: you're already behind the wheel when the picture comes up
    public void Prepare()
    {
        van.Enter();
        // each customer's own door, in the wall behind them (see Leave)
        foreach (var s in stops)
            if (s.customer != null && s.door == null) s.door = StopDoor.Build(s.customer.transform, s.bay.position, s.handOverLines.Contains("Okafor"));
    }

    public IEnumerator Run()
    {
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
    }

    // the hand-over as a cutscene: you're held still, the customer turns to you, the camera pushes in on them
    // behind letterbox bars while they talk, then it eases back and you're free again
    IEnumerator Conversation(Stop s)
    {
        var cam = van.playerCamera;
        var fpc = van.player.GetComponent<FirstPersonController>();
        var dir = FindFirstObjectByType<PrologueDirector>();
        if (cam == null || s.customer == null) { Say(s.handOverLines); yield return WaitForLines(); yield break; }

        if (fpc != null) { fpc.playerCanMove = false; fpc.cameraCanMove = false; }
        var rb = van.player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        Transform npc = s.customer.transform, ct = cam.transform;
        Vector3 pos0 = ct.position; Quaternion rot0 = ct.rotation; float fov0 = cam.fieldOfView;
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
        CutsceneSkip.Extra++;   // (hold SPACE to skip the walk home)
        yield return Leave(s, ct, cam, npc);   // they walk back to their door and go in
        CutsceneSkip.Extra--;

        Vector3 camNow = ct.position; Quaternion rotNow = ct.rotation; float fovNow = cam.fieldOfView;
        yield return Blend(1f, k =>
        {
            ct.SetPositionAndRotation(Vector3.Lerp(camNow, pos0, k), Quaternion.Slerp(rotNow, rot0, k));
            cam.fieldOfView = Mathf.Lerp(fovNow, fov0, k);
            Bars(dir, 1f - k);
        });
        ct.SetPositionAndRotation(pos0, rot0);
        cam.fieldOfView = fov0;
        Bars(dir, 0f);
        if (fpc != null) { fpc.playerCanMove = true; fpc.cameraCanMove = true; }
    }

    // the customer walks off to the door in the building behind them (the camera drifts after them), waits at its
    // side while it opens, goes through into the black, and it shuts behind them
    IEnumerator Leave(Stop s, Transform ct, Camera cam, Transform npc)
    {
        var door = s.door;
        if (door == null) { npc.gameObject.SetActive(false); yield break; }   // no wall to put one on: they just go
        var col = npc.GetComponent<Collider>(); if (col != null) col.enabled = false;
        var an = s.customerAnimator;
        Vector3 cam0 = ct.position, npc0 = npc.position;
        float fov0 = cam.fieldOfView;

        IEnumerator Walk(Vector3 to, float speed)
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
                Follow();
                yield return null;
            }
            if (an != null) { an.SetFloat("Speed", 0f); an.speed = 1f; }
        }
        void Follow()
        {
            Vector3 want = cam0 + Flat(npc.position - npc0) * 0.7f;
            ct.position = Vector3.Lerp(ct.position, want, 2f * Time.deltaTime);
            Vector3 head = npc.position + Vector3.up * 1.4f;
            ct.rotation = Quaternion.Slerp(ct.rotation, Quaternion.LookRotation(head - ct.position), 3.5f * Time.deltaTime);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 50f, 1.5f * Time.deltaTime);
        }

        float v = Mathf.Max(0.5f, s.exitSpeed);
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
        if (beacon == null) return;
        beacon.SetActive(at != null);
        if (at == null) return;
        beacon.transform.position = at.position;
        if (beaconWidth <= 0f) beaconWidth = beacon.transform.localScale.x;
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
