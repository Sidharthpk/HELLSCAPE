using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Your four dead colleagues do not walk the office. Each has a desk (no name tags: you only feel that someone is
// there; a "Desk sign - <name>" in OfficeAct/Office still moves the desk if one is put back), and nobody at it.
// Come near one and a line says you feel a presence; after a moment either they run at you down the room and stop a pace away, or footsteps walk up behind you and, when you
// look back, their burnt body is standing in front of you. Either way the view pans to their face (no more effects
// than that). From then on they stay where they appeared, to be questioned. In the Echo every one of them
// is there as a soul, scare or no scare. A player who never looks round gets them standing at their desk instead.
public class OfficeColleagues : MonoBehaviour
{
    public float vicinity = 7f;             // how near their desk the footsteps can start

    static readonly string[] feel =
    {
        "I feel something weird in here. A presence.",
        "I'm not alone at this desk. I can feel it.",
        "Something is here. Something that used to sit here.",
        "The air is wrong. Someone is watching me.",
    };
    int felt;

    class C { public GhostNPC g; public WitnessLook look; public Vector3 desk, home; public bool shown; public int tries; }
    readonly List<C> all = new List<C>();
    Transform player; Camera cam; OfficeStalker finder; Func<bool> canAct;
    AudioSource sfx; AudioClip stomp, sting;
    bool busy;
    float timer, grace = 12f;

    public void Init(List<GhostNPC> colleagues, Transform body, Camera camera, OfficeStalker stalker, Func<bool> act)
    {
        player = body; cam = camera; finder = stalker; canAct = act;
        foreach (var g in colleagues)
        {
            var sign = g.transform.parent != null ? g.transform.parent.Find("Desk sign - " + g.ghostName) : null;
            // their place is in front of their own name (the label is on the sign's back face), if there is floor there
            if (sign != null && Physics.Raycast(sign.position - Flat(sign.forward).normalized * .9f, Vector3.down, out var h, 2f, ~0, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(h.point.y - g.transform.position.y) < .2f)
                g.transform.SetPositionAndRotation(h.point, Quaternion.LookRotation(Flat(sign.forward)));
            all.Add(new C { g = g, look = g.GetComponent<WitnessLook>(), home = g.transform.position, desk = sign != null ? sign.position : g.transform.position });
            g.gameObject.SetActive(false);
        }
        sfx = new GameObject("Colleague sfx").AddComponent<AudioSource>();
        sfx.transform.SetParent(transform, false); sfx.spatialBlend = 1f; sfx.rolloffMode = AudioRolloffMode.Linear; sfx.maxDistance = 25f; sfx.playOnAwake = false;
        stomp = ProceduralAudio.Stomp(); sting = ProceduralAudio.Rumble(1.2f);
    }

    void OnDestroy() { if (stomp != null) Destroy(stomp); if (sting != null) Destroy(sting); }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    bool Near(C c) => Flat(player.position - c.desk).magnitude < vicinity;
    bool InView(Vector3 p) => Vector3.Angle(cam.transform.forward, p + Vector3.up * 1.2f - cam.transform.position) < 65f;

    bool echo;
    // (from OfficeHorrorDirector.OnEcho: the souls are there the moment the realm changes, not a frame later)
    public void SetEcho(bool on) { echo = on; Show(); }

    void Show()
    {
        foreach (var c in all)
        {
            if (c.g == null) continue;
            bool want = !Truth.Has("Freed" + c.g.ghostName) && (c.shown || echo || c.g.IsTalking);
            if (c.g.gameObject.activeSelf != want) c.g.gameObject.SetActive(want);
        }
    }

    void Update()
    {
        if (player == null) return;
        Show();
        if (busy || echo || !canAct()) return;
        grace -= Time.deltaTime; timer -= Time.deltaTime;
        if (grace > 0f || timer > 0f) return;
        timer = 1f;
        foreach (var c in all)
        {
            if (c.shown || c.g == null) continue;
            if (c.g.fate != GhostNPC.Fate.Undecided) { c.shown = true; continue; }   // judged in the Echo already: no surprise left in them
            if (Near(c)) { StartCoroutine(Scare(c)); break; }
        }
    }

    IEnumerator Scare(C c)
    {
        busy = true;
        yield return new WaitForSeconds(UnityEngine.Random.Range(1.5f, 5f));
        if (!canAct() || !Near(c)) { busy = false; yield break; }

        // no name plate to tell you whose desk it is: only the feeling (and wait for the line before anything happens)
        if (DialogueBox.Instance != null)
        {
            DialogueBox.Instance.Say(feel[felt++ % feel.Length]);
            yield return null;
            while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;
            yield return new WaitForSeconds(.6f);
            if (!canAct() || !Near(c)) { busy = false; yield break; }
        }

        // half the time they run at you down the room instead: no warning, one pace from you, then the view goes to their face
        if (UnityEngine.Random.value < .5f)
        {
            yield return RunAt(c);
            if (c.shown) { timer = UnityEngine.Random.Range(10f, 20f); busy = false; yield break; }
        }

        // footsteps, coming up behind you
        Vector3 fwd = Flat(cam.transform.forward).normalized;
        for (int i = 0; i < 5; i++)
        {
            sfx.transform.position = player.position - fwd * Mathf.Lerp(7f, 1.6f, i / 4f);
            sfx.pitch = UnityEngine.Random.Range(.85f, 1.05f);
            sfx.PlayOneShot(stomp, Mathf.Lerp(.5f, 1f, i / 4f));
            yield return new WaitForSeconds(.45f);
        }

        // you look back: nobody. That is when the body is stood where you were looking
        bool placed = false;
        for (float t = 0f; t < 5f && canAct() && !placed; t += Time.deltaTime)
        {
            if (Vector3.Angle(fwd, Flat(cam.transform.forward)) > 110f && Spot(fwd, out var p)) { Place(c, p); placed = true; }
            yield return null;
        }
        if (!placed)
        {
            if (++c.tries >= 3 && !InView(c.home)) Place(c, c.home);   // you never turn round: they are simply at their desk
            timer = UnityEngine.Random.Range(8f, 15f); busy = false;
            yield break;
        }

        // ...and there they are, in front of you: the view goes to their face
        Sting(c);
        yield return PanTo(c.g.transform.position + Vector3.up * FaceHeight);
        timer = UnityEngine.Random.Range(10f, 20f); busy = false;
    }

    const float FaceHeight = 1.55f;

    void Sting(C c)
    {
        sfx.transform.position = c.g.transform.position;
        sfx.pitch = 1.3f; sfx.PlayOneShot(Sounds.Clip("Office/Auditor sting", null) ?? sting, 1f);
        if (c.look != null) c.look.Flash();
    }

    // from the far end of the room in front of you, running, to a pace away
    IEnumerator RunAt(C c)
    {
        Vector3 from = default; bool found = false;
        for (int i = 0; i < 40 && !found && canAct(); i++)
        {
            found = finder.TryFind(UnityEngine.Random.Range(9f, 13f), false, out from)
                && Vector3.Angle(cam.transform.forward, from - cam.transform.position) < 45f
                && finder.Clear(cam.transform.position, from + Vector3.up * 1.4f);
            if (!found) yield return null;
        }
        if (!found) yield break;
        var t = c.g.transform; var anim = c.g.animator;
        Place(c, from);
        Sting(c);
        for (float time = 0f; time < 6f && canAct(); time += Time.deltaTime)
        {
            Vector3 to = Flat(player.position - t.position); float d = to.magnitude;
            if (d < 1.8f) break;
            Vector3 dir = to / d; float len = 7f * Time.deltaTime;
            if (Physics.SphereCast(t.position + Vector3.up * .9f, .3f, dir, out var h, len + .3f, ~0, QueryTriggerInteraction.Ignore)
                && !h.transform.IsChildOf(player.root) && !h.transform.IsChildOf(t)) break;
            t.SetPositionAndRotation(t.position + dir * len, Quaternion.LookRotation(dir));
            if (anim != null) { anim.SetFloat("Speed", 1.3f); anim.speed = 2.6f; }   // (after GhostNPC's own Update has set it to idle)
            yield return null;
        }
        if (anim != null) { anim.SetFloat("Speed", 0f); anim.speed = 1f; }
        yield return PanTo(t.position + Vector3.up * FaceHeight);
    }

    // the look turns to a point over about a second (yaw on the body, pitch on the camera, as the controller keeps them)
    IEnumerator PanTo(Vector3 target)
    {
        var fpc = player.GetComponent<FirstPersonController>();
        if (fpc == null || !fpc.enabled || GhostNPC.Conversing) yield break;
        var camT = cam.transform;
        Vector3 to = target - camT.position;
        float yaw1 = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        float pitch1 = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -fpc.maxLookAngle, fpc.maxLookAngle);
        float yaw0 = player.eulerAngles.y, pitch0 = Mathf.DeltaAngle(0f, camT.localEulerAngles.x);
        fpc.cameraCanMove = false;
        const float seconds = .9f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            player.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(yaw0, yaw1, k), 0f);
            camT.localEulerAngles = new Vector3(Mathf.Lerp(pitch0, pitch1, k), 0f, 0f);
            yield return null;
        }
        player.rotation = Quaternion.Euler(0f, yaw1, 0f);
        camT.localEulerAngles = new Vector3(pitch1, 0f, 0f);
        fpc.SetPitch(pitch1);
        yield return new WaitForSeconds(.6f);   // a moment on the face before the look is yours again
        fpc.cameraCanMove = true;
    }

    // free floor a step or two in front of where you were facing, with nothing between it and you
    bool Spot(Vector3 fwd, out Vector3 spot)
    {
        spot = default;
        if (!Physics.Raycast(player.position, Vector3.down, out var floor, 3f, ~0, QueryTriggerInteraction.Ignore)) return false;
        foreach (float d in new[] { 1.7f, 2.1f, 1.4f, 2.6f })
            foreach (float yaw in new[] { 0f, 18f, -18f })
            {
                Vector3 q = player.position + Quaternion.Euler(0f, yaw, 0f) * fwd * d;
                if (!Physics.Raycast(q + Vector3.up * .3f, Vector3.down, out var h, 3.5f, ~0, QueryTriggerInteraction.Ignore) || h.normal.y < .9f) continue;
                if (Mathf.Abs(h.point.y - floor.point.y) > .2f) continue;   // the floor, not a desk top
                if (Physics.CheckCapsule(h.point + Vector3.up * .45f, h.point + Vector3.up * 1.7f, .3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!finder.Clear(cam.transform.position, h.point + Vector3.up * 1.4f)) continue;
                spot = h.point; return true;
            }
        return false;
    }

    void Place(C c, Vector3 at)
    {
        Vector3 d = Flat(player.position - at);
        c.g.transform.SetPositionAndRotation(at, d.sqrMagnitude > .01f ? Quaternion.LookRotation(d) : c.g.transform.rotation);
        c.shown = true;
        c.g.gameObject.SetActive(true);
    }
}
