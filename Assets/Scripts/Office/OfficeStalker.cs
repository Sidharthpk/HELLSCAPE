using System;
using UnityEngine;

// "The Auditor": one burnt figure that is never fought. For most of the act it only watches: at the far end of a
// corridor, in a doorway, behind you in the reflection. It goes if you look at it for long or come near, and it
// comes back a little closer each time an objective is done (progress 0..1). Once `hunting`, it walks at you
// (slower while you look at it); it hurts, but never finishes you off. No navmesh in the office: it walks straight
// and slides round furniture.
public class OfficeStalker : MonoBehaviour
{
    public Transform player;
    public Camera cam;
    public PlayerHealth health;
    public float progress;                  // 0 far and rare .. 1 close and often
    public bool hunting;
    public Func<bool> canAct = () => true;  // false in cutscenes, conversations and pauses
    public Action onSeen, onAttack;
    public Vector2 xRange = new Vector2(993f, 1022f), zRange = new Vector2(984f, 1034f);   // the office

    GameObject model;
    Animator anim;
    Light glow;
    AudioSource sfx;
    AudioClip stepClip;
    void OnDestroy() { if (stepClip != null) Destroy(stepClip); }
    float timer, lookTime, shownFor, scripted, attackCooldown = 2f, stepTimer;
    enum S { Hidden, Watching, Hunting }
    S state = S.Hidden;
    public float TimerLeft => timer;
    public bool Active => canAct();
    public bool Present => model != null && model.activeSelf;
    public Vector3 Position => model != null ? model.transform.position : Vector3.zero;

    public void Build(Transform template, string material = "AuditorSkin")
    {
        if (template == null) { enabled = false; return; }
        model = new GameObject("The Auditor");
        model.transform.SetParent(transform, false);
        var body = OfficeBodies.Clone(template);
        body.transform.SetParent(model.transform, false);
        body.name = "Auditor body";
        body.transform.localPosition = Vector3.zero; body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = template.lossyScale * 1.12f;
        var mat = WitnessLook.Load(material);
        foreach (var r in body.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
        anim = body.GetComponentInChildren<Animator>();
        glow = new GameObject("Auditor glow").AddComponent<Light>();
        glow.transform.SetParent(model.transform, false); glow.transform.localPosition = Vector3.up * 1.2f;
        glow.type = LightType.Point; glow.color = new Color(1f, .12f, .05f); glow.range = 6f; glow.intensity = 1.2f; glow.shadows = LightShadows.None;
        sfx = model.AddComponent<AudioSource>();
        sfx.spatialBlend = 1f; sfx.maxDistance = 30f; sfx.rolloffMode = AudioRolloffMode.Linear;
        model.SetActive(false);
        timer = 18f;
    }

    // from now on it comes for you, soon
    public void Hunt()
    {
        hunting = true; progress = 1f;
        if (state == S.Hidden) timer = Mathf.Min(timer, 2.5f);
    }

    public void Vanish()
    {
        if (model != null) model.SetActive(false);
        state = S.Hidden;
        lookTime = shownFor = scripted = 0f;
        timer = Mathf.Lerp(34f, 9f, progress) * UnityEngine.Random.Range(.7f, 1.3f);
    }

    // a scripted showing (a silhouette in a doorway, behind you in the monitor): it is gone after `seconds`
    public void Apparition(Vector3 at, float seconds)
    {
        if (model == null) return;
        Place(at, true);
        state = S.Watching;
        scripted = seconds;
        lookTime = 0f;
    }

    void Place(Vector3 at, bool face)
    {
        model.SetActive(true);
        model.transform.position = at;
        if (face) FaceYou();
        if (anim != null) anim.SetFloat("Speed", 0f);
    }

    void FaceYou()
    {
        Vector3 d = player.position - model.transform.position; d.y = 0f;
        if (d.sqrMagnitude > .01f) model.transform.rotation = Quaternion.LookRotation(d);
    }

    bool InOffice(Vector3 p) => p.x > xRange.x && p.x < xRange.y && p.z > zRange.x && p.z < zRange.y;

    // nothing solid between your eyes and `b` (you and the figure itself do not count)
    bool Clear(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a; float len = d.magnitude;
        if (len < .01f) return true;
        foreach (var h in Physics.RaycastAll(a, d / len, len, ~0, QueryTriggerInteraction.Ignore))
            if (!h.transform.IsChildOf(player.root) && (model == null || !h.transform.IsChildOf(model.transform))) return false;
        return true;
    }

    bool Sees(Vector3 p, float cone = 38f)
    {
        Vector3 to = p + Vector3.up * 1.2f - cam.transform.position;
        if (Vector3.Angle(cam.transform.forward, to) > cone) return false;
        return Clear(cam.transform.position, p + Vector3.up * 1.2f);
    }

    // a free standing spot on the floor `dist` away, in view (or out of it)
    public bool TryFind(float dist, bool inView, out Vector3 spot) => Find(dist, inView, out spot);

    bool Find(float dist, bool inView, out Vector3 spot)
    {
        spot = default;
        for (int i = 0; i < 48; i++)
        {
            // the office is full of walls: if the far end is blocked, take a nearer one in the same direction
            float scale = i < 16 ? 1f : i < 32 ? .65f : .4f;
            float yaw = inView ? UnityEngine.Random.Range(-36f, 36f) : UnityEngine.Random.Range(110f, 250f);
            Vector3 dir = Quaternion.Euler(0f, cam.transform.eulerAngles.y + yaw, 0f) * Vector3.forward;
            Vector3 p = player.position + dir * (Mathf.Max(5.5f, dist * scale) * UnityEngine.Random.Range(.8f, 1.15f));
            if (!InOffice(p)) continue;
            if (!Physics.Raycast(new Vector3(p.x, player.position.y + .3f, p.z), Vector3.down, out var h, 3.5f, ~0, QueryTriggerInteraction.Ignore) || h.normal.y < .9f) continue;
            if (Mathf.Abs(h.point.y - (player.position.y - 1f)) > 1.2f) continue;   // the same floor
            p = h.point;
            if (Physics.CheckCapsule(p + Vector3.up * .45f, p + Vector3.up * 1.7f, .32f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (Sees(p) != inView) continue;
            spot = p; return true;
        }
        return false;
    }

    void Update()
    {
        if (model == null || player == null || cam == null) return;
        bool act = canAct();
        if (glow != null && model.activeSelf) glow.intensity = 1f + .5f * Mathf.PerlinNoise(Time.time * 9f, 0f) + (state == S.Hunting ? .8f : 0f);

        if (state == S.Hidden)
        {
            if (!act) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            if (hunting)
            {
                if (Find(Mathf.Lerp(16f, 11f, progress), false, out var behind)) { Place(behind, true); state = S.Hunting; Sting(); }
                else timer = 1f;
            }
            else if (Find(Mathf.Lerp(26f, 9f, progress), true, out var spot))
            {
                Place(spot, true); state = S.Watching; shownFor = 0f; lookTime = 0f; Sting(); onSeen?.Invoke();
            }
            else timer = 1.5f;
            return;
        }

        if (state == S.Watching && scripted > 0f)
        {
            scripted -= Time.deltaTime; FaceYou();
            if (scripted <= 0f) Vanish();
            return;
        }
        if (!act) return;   // frozen mid-cutscene
        shownFor += Time.deltaTime;
        Vector3 to = player.position - model.transform.position; to.y = 0f;
        float dist = to.magnitude;
        bool watched = Sees(model.transform.position, 30f);

        if (state == S.Watching)
        {
            FaceYou();
            if (scripted > 0f) { scripted -= Time.deltaTime; if (scripted <= 0f) Vanish(); return; }
            if (watched) lookTime += Time.deltaTime; else lookTime = Mathf.Max(0f, lookTime - Time.deltaTime);
            // it was there, and then you looked: gone
            if (lookTime > Mathf.Lerp(2.2f, 1.1f, progress) || dist < 5f || shownFor > 14f) Vanish();
            else if (hunting && shownFor > 3f) { state = S.Hunting; Sting(); }
            return;
        }

        // hunting
        attackCooldown -= Time.deltaTime;
        float speed = watched ? 1.3f : Mathf.Lerp(2.3f, 3.1f, progress);
        FaceYou();
        if (dist > 1.5f) Step(to / Mathf.Max(dist, .01f), speed * Time.deltaTime);
        if (anim != null) anim.SetFloat("Speed", dist > 1.5f ? 1.3f : 0f, .1f, Time.deltaTime);
        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f && dist > 1.5f) { stepTimer = watched ? .75f : .5f; Thud(Mathf.Clamp01(1f - dist / 18f)); }
        if (dist < 1.7f && attackCooldown <= 0f && Clear(model.transform.position + Vector3.up, player.position)) Attack();
    }

    void Step(Vector3 dir, float len)
    {
        Vector3 o = model.transform.position + Vector3.up * .9f;
        foreach (float a in new[] { 0f, 40f, -40f, 80f, -80f })
        {
            Vector3 d = Quaternion.Euler(0f, a, 0f) * dir;
            if (Physics.SphereCast(o, .3f, d, out var h, len + .3f, ~0, QueryTriggerInteraction.Ignore) && !h.transform.IsChildOf(player.root)) continue;
            Vector3 p = model.transform.position + d * len;
            if (!InOffice(p)) continue;
            if (!Physics.Raycast(p + Vector3.up * .4f, Vector3.down, out var g, .8f, ~0, QueryTriggerInteraction.Ignore) || g.normal.y < .9f || Mathf.Abs(g.point.y - model.transform.position.y) > .3f) continue;
            p.y = g.point.y;
            model.transform.position = p;
            return;
        }
    }

    void Attack()
    {
        attackCooldown = 3f;
        if (health != null && !health.IsDead)
        {
            float dmg = Mathf.Min(14f, health.currentHealth - 12f);   // it hurts. It does not finish you
            if (dmg > 0f) health.TakeDamage(dmg);
        }
        Thud(1f);
        onAttack?.Invoke();
        Vanish();
        timer = Mathf.Lerp(9f, 5f, progress);   // and it's back
    }

    void Sting()
    {
        if (sfx == null) return;
        sfx.pitch = UnityEngine.Random.Range(.6f, .8f);
        sfx.PlayOneShot(Sounds.Clip("Office/Auditor sting", null) ?? ProceduralAudio.Rumble(1.6f), .7f);
    }

    void Thud(float v) { if (sfx != null && v > .05f) { sfx.pitch = UnityEngine.Random.Range(.8f, 1f); sfx.PlayOneShot(stepClip != null ? stepClip : stepClip = ProceduralAudio.Stomp(), v * .6f); } }
}
