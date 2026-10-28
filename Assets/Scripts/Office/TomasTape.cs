using System.Collections;
using UnityEngine;

// The CCTV tape of Tomas's death (StabCutscene "Cutscene - Derek kills Tomas" runs it in place of its timer), on a
// corner landing of the silo stairs, Derek's back to the camera. Both idle while they talk; on "I'm sorry" Derek draws
// (left hand), holds his aim until "Don't." is out, fires; Tomas is shot, falls back to the floor at the edge and goes
// over into the silo. Derek talks on with Vane, who is behind the camera, turns and walks back up the stairs out of
// shot while the talk carries on.
// Built by HellScape > Office > Tomas Tape (TomasTapeBuilder); the lines are rows of HellscapeDialogue.csv.
public class TomasTape : MonoBehaviour
{
    public Animator derek, tomas;           // Derek: Idle / Shoot / Idle after. Tomas: Idle / Death
    public GameObject derekWalker;          // the walking Derek (his walk is on another rig): swapped in to leave
    public Transform[] exit;                // the way out: the tower door, then on through it
    public Transform muzzle;
    public Light flash;
    public Vector3 fallDir = Vector3.right;   // over the edge, into the silo
    public float shotAt = 1.75f;            // seconds into Derek's draw: the shot (the recoil follows)
    public float hitAt = .35f;              // seconds into Tomas's death: where the hit lands
    public float downAt = 2.9f;             // ...and where he is on the floor
    public float walkSpeed = 1.3f;
    public bool frozenIdle;                 // V5.1 Mercer: no idle clip, so "Idle" is the first frame of his aim held still
    public bool ShotFired { get; private set; }
    public bool ExitCompleted { get; private set; }
    public float StairHeightGain { get; private set; }

    [Header("Lines (HellscapeDialogue.csv ids)")]
    public string talk = "325-331", shot = "332-333", after = "663-665", leaving = "335", end = "334";

    Vector3[] at; Quaternion[] rot; Transform[] cast;
    AudioSource bang;

    void Awake()
    {
        GetComponent<StabCutscene>().sequence = Beats;
        cast = new[] { derek.transform, tomas.transform, derekWalker.transform };
        at = new Vector3[cast.Length]; rot = new Quaternion[cast.Length];
        for (int i = 0; i < cast.Length; i++) { at[i] = cast[i].position; rot[i] = cast[i].rotation; }
        bang = gameObject.AddComponent<AudioSource>(); bang.playOnAwake = false; bang.spatialBlend = 0f;   // the tape's microphone
    }

    IEnumerator Beats()
    {
        ShotFired = ExitCompleted = false; StairHeightGain = 0f;
        // (a replay starts where the first one did)
        for (int i = 0; i < cast.Length; i++) cast[i].SetPositionAndRotation(at[i], rot[i]);
        derek.gameObject.SetActive(true); tomas.gameObject.SetActive(true); derekWalker.SetActive(false);
        derek.speed = tomas.speed = 1f;
        derek.Play("Idle", 0, 0f); tomas.Play("Idle", 0, .4f);
        if (frozenIdle) derek.speed = 0f;
        if (flash != null) flash.enabled = false;

        yield return Talk(talk);

        // "I'm sorry." - he draws; "Don't." - he holds the aim until it's said, then fires
        Say(shot);
        derek.speed = 1f;
        derek.Play("Shoot", 0, 0f);
        yield return null;   // (the clip info is the new state's from the next frame)
        var draw = derek.GetCurrentAnimatorClipInfo(0);
        float len = draw.Length > 0 ? draw[0].clip.length : 5.3f;
        while (Seconds(derek, "Shoot", len) < shotAt - .12f) yield return null;
        if (Busy) { derek.speed = 0f; while (Busy) yield return null; yield return new WaitForSeconds(.4f); derek.speed = 1f; }
        while (Seconds(derek, "Shoot", len) < shotAt) yield return null;
        Fire();
        tomas.Play("Death", 0, 0f);
        yield return null;
        var death = tomas.GetCurrentAnimatorClipInfo(0);
        float dLen = death.Length > 0 ? death[0].clip.length : 4.4f;
        tomas.Play("Death", 0, hitAt / dLen);
        yield return new WaitForSeconds(downAt - hitAt + .6f);
        yield return Fall();

        // Derek talks on with Vane; he leaves on his last word, the talk carries on
        yield return Talk(after);
        var walk = StartCoroutine(Walk());
        Say(leaving);
        while (Busy || derekWalker.activeSelf) yield return null;
        StopCoroutine(walk);
        yield return Talk(end);
        yield return new WaitForSeconds(.6f);
    }

    static bool Busy => DialogueBox.Instance != null && DialogueBox.Instance.Busy;
    static void Say(string ids) { if (!string.IsNullOrEmpty(ids) && DialogueBox.Instance != null) DialogueBox.Instance.Say(ids.Contains("_") ? HS5.S(ids) : HS.S(ids)); }   // (a V5 trigger like A05_tomas_tape, or old row numbers)

    static IEnumerator Talk(string ids)
    {
        while (Busy) yield return null;   // (whatever was already being said: the terminal's line)
        Say(ids);
        yield return null;
        while (Busy) yield return null;
    }

    static float Seconds(Animator a, string state, float len)
    {
        var s = a.GetCurrentAnimatorStateInfo(0);
        return s.IsName(state) ? s.normalizedTime * len : 0f;
    }

    void Fire()
    {
        ShotFired = true;
        bang.PlayOneShot(Sounds.Clip("Silo Fight/Rifle shot", ProceduralAudio.Gunshot()), Sounds.Volume("Silo Fight/Rifle shot"));
        if (flash != null) StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        flash.transform.position = muzzle != null ? muzzle.position : flash.transform.position;
        flash.enabled = true;
        yield return new WaitForSeconds(.07f);
        flash.enabled = false;
    }

    // from where he lies, over the edge (head first: he fell on his back towards it) and down the silo
    IEnumerator Fall()
    {
        var t = tomas.transform;
        Vector3 axis = Vector3.Cross(Vector3.up, fallDir);
        for (float s = 0f; s < .45f; s += Time.deltaTime)
        {
            t.position += fallDir * 1.8f * Time.deltaTime;
            t.RotateAround(t.position, axis, 60f * Time.deltaTime);
            yield return null;
        }
        float vy = 0f;
        for (float s = 0f; s < 2.4f; s += Time.deltaTime)
        {
            vy -= 9.81f * Time.deltaTime;
            t.position += (fallDir * .8f + Vector3.up * vy) * Time.deltaTime;
            t.RotateAround(t.position, axis, 110f * Time.deltaTime);
            yield return null;
        }
        tomas.gameObject.SetActive(false);
    }

    // the walking Derek in his place, then through the door and out of shot
    IEnumerator Walk()
    {
        var w = derekWalker.transform;
        w.position = derek.transform.position;
        derek.gameObject.SetActive(false);
        derekWalker.SetActive(true);
        var walking = derekWalker.GetComponent<Animator>();
        if (walking != null) { walking.speed = 1f; walking.Play("Walk", 0, 0f); }
        float startY = w.position.y;
        foreach (var p in exit)
        {
            for (Vector3 d = p.position - w.position; Flat(d).sqrMagnitude > .01f; d = p.position - w.position)
            {
                var step = Flat(d);
                w.rotation = Quaternion.Slerp(w.rotation, Quaternion.LookRotation(step), 8f * Time.deltaTime);
                w.position = Vector3.MoveTowards(w.position, new Vector3(p.position.x, w.position.y, p.position.z), walkSpeed * Time.deltaTime);
                // up the steps: feet on whatever is under him
                if (Physics.Raycast(w.position + Vector3.up * 1.2f, Vector3.down, out var hit, 2.4f, ~0, QueryTriggerInteraction.Ignore))
                    w.position = new Vector3(w.position.x, Mathf.MoveTowards(w.position.y, hit.point.y, 2f * Time.deltaTime), w.position.z);
                StairHeightGain = Mathf.Max(StairHeightGain, w.position.y - startY);
                yield return null;
            }
        }
        derekWalker.SetActive(false);
        ExitCompleted = true;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
