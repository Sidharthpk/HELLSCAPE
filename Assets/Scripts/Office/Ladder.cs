using System.Collections;
using UnityEngine;

// Press F at the hatch: the player is turned to face the rungs, lowered through the hatch and climbs down
// hand over hand along `path` (the rigidbody controller can't climb, so the ladder does it for them).
// Vertical legs go one rung at a time: a dip, a sway of the head and a clank per rung.
public class Ladder : MonoBehaviour
{
    public Transform player;
    public Transform[] path;                // player-root positions, top to bottom
    public Vector3 faceDirection = Vector3.forward;   // world direction the player looks while on the rungs
    public float speed = 3.2f;              // m/s on the non-vertical legs (getting on / off)
    public float rungSpacing = 0.35f;
    public float rungTime = 0.18f;          // seconds per rung
    public float headSway = 4f;             // degrees of roll per rung
    public AudioSource sfx;
    public AudioClip rungClip;              // procedural clank if empty
    public UnityEngine.Events.UnityEvent onClimbed = new UnityEngine.Events.UnityEvent();

    private bool busy;
    public static bool Climbing { get; private set; }   // the killer can't land a hit on someone stuck on the rungs

    // cut off mid-climb (a reload, a checkpoint): don't leave the flag stuck on
    void OnDisable() { if (busy) { Climbing = false; busy = false; ShowFists(true); } }

    // your fists are out of the picture on the rungs (held stiff in their guard they looked wrong: user, 2026-10-02)
    Renderer[] fists;
    void ShowFists(bool on)
    {
        if (!on)
        {
            var rig = player != null ? player.GetComponentInChildren<PunchController>() : null;
            fists = rig != null ? System.Array.FindAll(rig.GetComponentsInChildren<Renderer>(), r => r.enabled) : null;
        }
        if (fists != null) foreach (var r in fists) if (r != null) r.enabled = on;
    }

    public void Climb()
    {
        if (!busy) StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        busy = true;
        Climbing = true;
        var rb = player.GetComponent<Rigidbody>();
        var move = player.GetComponent<FirstPersonController>();
        var cam = player.GetComponentInChildren<Camera>();
        Transform head = move != null && move.joint != null ? move.joint : (cam != null ? cam.transform : null);
        Vector3 headRest = head != null ? head.localPosition : Vector3.zero;
        Quaternion headRot = head != null ? head.localRotation : Quaternion.identity;
        if (move != null) move.enabled = false;
        rb.isKinematic = true;
        ShowFists(false);
        if (sfx != null) rungClip = Sounds.Clip("Silo Complex/Ladder rung", rungClip != null ? rungClip : ProceduralAudio.Click());

        // a recording of the whole climb replaces the per-rung clanks; it's cut off when you reach the bottom
        var climbClip = sfx != null ? Sounds.Clip("Silo Complex/Ladder climb", null) : null;
        AudioSource climbSrc = null;
        if (climbClip != null)
        {
            climbSrc = gameObject.AddComponent<AudioSource>();
            climbSrc.spatialBlend = 0f;
            climbSrc.clip = climbClip;
            climbSrc.volume = Sounds.Volume("Silo Complex/Ladder climb");
            climbSrc.Play();
        }

        // turn to the rungs
        Vector3 face = faceDirection; face.y = 0f;
        Quaternion from = player.rotation, to = Quaternion.LookRotation(face.normalized);
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            player.rotation = Quaternion.Slerp(from, to, t / 0.35f);
            yield return null;
        }
        player.rotation = to;

        int rung = 0;
        foreach (var p in path)
        {
            Vector3 start = player.position, delta = p.position - start;
            bool vertical = Mathf.Abs(delta.y) > 1f && new Vector2(delta.x, delta.z).magnitude < 0.5f;
            if (!vertical)
            {
                while ((player.position - p.position).sqrMagnitude > 0.0004f)
                {
                    player.position = Vector3.MoveTowards(player.position, p.position, speed * Time.deltaTime);
                    yield return null;
                }
                continue;
            }

            int steps = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(delta.y) / rungSpacing));
            for (int i = 1; i <= steps; i++, rung++)
            {
                Vector3 a = player.position, b = start + delta * i / steps;
                float side = rung % 2 == 0 ? 1f : -1f;   // left hand, right hand
                if (sfx != null && climbSrc == null) { sfx.pitch = Random.Range(0.8f, 1.1f); sfx.PlayOneShot(rungClip, 0.6f * Sounds.Volume("Silo Complex/Ladder rung")); }
                for (float t = 0f; t < rungTime; t += Time.deltaTime)
                {
                    float k = t / rungTime;
                    player.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, k));
                    if (head != null)
                    {
                        float arc = Mathf.Sin(k * Mathf.PI);   // reach, pull, settle
                        head.localPosition = headRest + new Vector3(side * 0.04f * arc, -0.05f * arc, 0f);
                        head.localRotation = headRot * Quaternion.Euler(6f * arc, 0f, side * headSway * arc);
                    }
                    yield return null;
                }
                player.position = b;
            }
        }

        if (climbSrc != null) { climbSrc.Stop(); Destroy(climbSrc); }
        if (head != null) { head.localPosition = headRest; head.localRotation = headRot; }
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        if (move != null) move.enabled = true;
        ShowFists(true);
        busy = false;
        Climbing = false;
        onClimbed.Invoke();
    }
}
