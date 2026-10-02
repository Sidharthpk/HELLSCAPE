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
    public void Prepare() => van.Enter();

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

        yield return Blend(1f, k =>
        {
            ct.SetPositionAndRotation(Vector3.Lerp(camPos, pos0, k), Quaternion.Slerp(camRot, rot0, k));
            cam.fieldOfView = Mathf.Lerp(38f, fov0, k);
            Bars(dir, 1f - k);
        });
        ct.SetPositionAndRotation(pos0, rot0);
        cam.fieldOfView = fov0;
        Bars(dir, 0f);
        if (fpc != null) { fpc.playerCanMove = true; fpc.cameraCanMove = true; }
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
        if (objective == null) return;
        objective.gameObject.SetActive(!string.IsNullOrEmpty(text));
        objective.text = "> " + text;   // (as the director writes them)
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
