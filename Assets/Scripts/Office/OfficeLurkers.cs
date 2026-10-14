using System;
using System.Collections;
using UnityEngine;

// Random scares in the office: black shadow figures that appear a few metres away (look at them and they are gone;
// look away too long and they run at you, grab you for a little damage; a palm flash makes them vanish), one that
// RUNS at you and vanishes, and footsteps lurking about. Only while the office act is calm (canAct), never during dialogue or cutscenes.
public class OfficeLurkers : MonoBehaviour
{
    OfficeHorrorDirector dir; OfficeStalker finder; Transform player; Camera cam; PlayerHealth health; Func<bool> canAct;
    GameObject shade, runner; Animator anim, runAnim; Material black; AudioSource sfx; AudioClip stomp;
    bool busy;
    float timer = 20f;

    public void Init(OfficeHorrorDirector director, OfficeStalker stalker, Transform body, Camera camera, PlayerHealth hp, Transform template, GameObject runTemplate, Func<bool> act)
    {
        dir = director; finder = stalker; player = body; cam = camera; health = hp; canAct = act;
        if (template == null) { enabled = false; return; }
        black = new Material(Shader.Find("Universal Render Pipeline/Unlit")); black.color = Color.black;
        shade = OfficeBodies.Clone(template);
        shade.name = "Shadow figure"; shade.transform.SetParent(transform, false);
        shade.transform.localScale = template.lossyScale * 1.08f;
        foreach (var r in shade.GetComponentsInChildren<Renderer>(true))
        {
            var m = r.sharedMaterials; for (int i = 0; i < m.Length; i++) m[i] = black;
            r.sharedMaterials = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        anim = shade.GetComponentInChildren<Animator>();
        sfx = new GameObject("Lurker sfx").AddComponent<AudioSource>(); sfx.transform.SetParent(transform, false); sfx.spatialBlend = 1f; sfx.maxDistance = 25f; sfx.rolloffMode = AudioRolloffMode.Linear;
        Backlight(shade.transform);
        shade.SetActive(false);
        // the runner is Kessler's own model with his RUNKESSLER "Run" clip, stripped of ragdoll and scripts (the original is inactive, so the copy is too)
        // (the "Run" state can only be asked for on the copy once it is switched on: an inactive animator has no states)
        if (runTemplate != null && runTemplate.GetComponentInChildren<Animator>(true) != null)
        {
            runner = Instantiate(runTemplate);
            runner.name = "Shadow runner"; runner.transform.SetParent(transform, false);
            runner.transform.localPosition = Vector3.zero; runner.transform.localRotation = Quaternion.identity;
            foreach (var c in runner.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(c);   // (scripts first: they may require the rigidbody)
            foreach (var c in runner.GetComponentsInChildren<CharacterJoint>(true)) DestroyImmediate(c);
            foreach (var c in runner.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(c);
            foreach (var c in runner.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var c in runner.GetComponentsInChildren<Light>(true)) DestroyImmediate(c);
            foreach (var r in runner.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterials; for (int i = 0; i < m.Length; i++) m[i] = black;
                r.sharedMaterials = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            runAnim = runner.GetComponentInChildren<Animator>(true); runAnim.applyRootMotion = false;
            runner.SetActive(true);
            if (runAnim.HasState(0, Animator.StringToHash("Run"))) { Backlight(runner.transform); runner.SetActive(false); }
            else { Destroy(runner); runner = null; runAnim = null; }
        }
        // the body bags are gone from the office: shadows stand where they lay
        foreach (var b in RevisedPlot.All<BodyBagScare>(gameObject.scene)) Destroy(b.gameObject);
    }

    void OnDestroy() { if (black != null) Destroy(black); if (stomp != null) Destroy(stomp); }

    // a dull red light at his back (he always faces you): in the blacked-out office a black figure is nothing
    // without something to stand out against
    static void Backlight(Transform figure)
    {
        var l = new GameObject("Shadow backlight").AddComponent<Light>();
        l.transform.SetParent(figure, false);
        l.transform.position = figure.position + Vector3.up * 1.3f - figure.forward * 1.2f;
        l.type = LightType.Point; l.color = new Color(1f, .1f, .05f); l.range = 5f; l.intensity = 1.6f; l.shadows = LightShadows.None;
    }

    void Update()
    {
        if (player == null) return;
        if (!canAct()) { timer = Mathf.Max(timer, 10f); return; }   // only once things have gone quiet again
        if (busy) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = UnityEngine.Random.Range(9f, 20f);   // (the Damned are gone from the office: it is all shadows now)
        float r = UnityEngine.Random.value;
        if (r < .15f) dir.LurkSteps();
        else if (r < .55f) StartCoroutine(Lurk());
        else StartCoroutine(Rush());
    }

    void Show(Vector3 at, float speed, float animSpeed)
    {
        shade.SetActive(true); shade.transform.position = sfx.transform.position = at;
        if (anim != null) { anim.SetFloat("Speed", speed); anim.speed = animSpeed; }
    }

    void Hide() { shade.SetActive(false); if (anim != null) anim.speed = 1f; busy = false; }

    void Face()
    {
        Vector3 d = player.position - shade.transform.position; d.y = 0f;
        if (d.sqrMagnitude > .01f) shade.transform.rotation = Quaternion.LookRotation(d);
    }

    // one to three of them stand there, all facing you; all gone once you look at one of them or come close
    IEnumerator Lurk()
    {
        var figures = new System.Collections.Generic.List<Transform>();
        int want = UnityEngine.Random.value < .4f ? 1 : UnityEngine.Random.Range(2, 4);
        for (int i = 0; i < want; i++)
        {
            if (!finder.TryFind(UnityEngine.Random.Range(11f, 14f), true, out var spot)) continue;
            var f = i == 0 ? shade : Instantiate(shade, transform);   // (a copy of the one prepared: its backlight too)
            f.SetActive(true); f.transform.position = spot;
            var a = f.GetComponentInChildren<Animator>();
            if (a != null) { a.SetFloat("Speed", 0f); a.speed = 1f; }
            figures.Add(f.transform);
        }
        if (figures.Count == 0) yield break;
        busy = true;
        sfx.transform.position = figures[0].position;
        sfx.pitch = UnityEngine.Random.Range(.6f, .8f); sfx.PlayOneShot(ProceduralAudio.Rumble(1.2f), .6f);
        // three chances: they stand far off, then (unseen) stand closer, then closer still; the third time unseen they run
        float seen = 0f; bool charge = false;
        for (int stage = 0; stage < 3 && !charge; stage++)
        {
            if (stage > 0)
            {
                foreach (var f in figures)
                    if (finder.TryFind(stage == 1 ? UnityEngine.Random.Range(7f, 9f) : UnityEngine.Random.Range(4.5f, 6f), true, out var near)) f.position = near;
                sfx.transform.position = figures[0].position;
                sfx.pitch = UnityEngine.Random.Range(.5f, .7f); sfx.PlayOneShot(ProceduralAudio.Rumble(1f), .7f);
                seen = 0f;
            }
            for (float t = 0f; t < 4f && canAct() && !charge; t += Time.deltaTime)
            {
                bool looked = false;
                foreach (var f in figures)
                {
                    Vector3 d = player.position - f.position; d.y = 0f;
                    if (d.sqrMagnitude > .01f) f.rotation = Quaternion.LookRotation(d);
                    looked |= Vector3.Angle(cam.transform.forward, f.position + Vector3.up * 1.2f - cam.transform.position) < 28f;
                    charge |= d.magnitude < 3f;
                }
                seen = looked ? seen + Time.deltaTime : Mathf.Max(0f, seen - Time.deltaTime);
                if (seen > .8f) { Cleanup(figures); yield break; }   // you looked: they are gone
                yield return null;
            }
            if (!canAct()) break;
        }
        // you never looked (or they were on you): they run at you, grab you, and are gone. A flash makes them vanish.
        float since = Time.time;
        foreach (var f in figures) { var a = f.GetComponentInChildren<Animator>(); if (a != null) { a.SetFloat("Speed", 1.3f); a.speed = 2.6f; } }
        sfx.pitch = .6f; sfx.PlayOneShot(ProceduralAudio.Rumble(1f), .8f);
        float step = 0f;
        for (float t = 0f; t < 5f && figures.Count > 0 && canAct() && PalmLight.FlashedAt < since; t += Time.deltaTime)
        {
            step -= Time.deltaTime;
            if (step <= 0f) { step = .24f; sfx.pitch = UnityEngine.Random.Range(.9f, 1.1f); sfx.PlayOneShot(stomp ??= ProceduralAudio.Stomp(), .8f); }
            for (int i = figures.Count - 1; i >= 0; i--)
            {
                var f = figures[i];
                Vector3 to = player.position - f.position; to.y = 0f;
                if (to.magnitude < 1.2f)
                {
                    foreach (var o in figures) if (o != f && o != shade.transform) Destroy(o.gameObject);
                    var keep = f; figures.Clear(); figures.Add(keep);
                    yield return Grab(keep);
                    Cleanup(figures);
                    yield break;
                }
                f.position += to.normalized * 7.5f * Time.deltaTime;
                f.rotation = Quaternion.LookRotation(to);
            }
            yield return null;
        }
        Cleanup(figures);
    }

    // it has you: your head is wrenched round onto it, held, shaking, then it lets go (and some of your health goes with it)
    IEnumerator Grab(Transform f)
    {
        var fpc = player.GetComponentInChildren<FirstPersonController>();
        bool was = fpc != null && fpc.enabled; if (was) fpc.enabled = false;
        Transform c = cam.transform; Quaternion q0 = c.localRotation;
        float dmg = health != null && !health.IsDead ? Mathf.Min(6f, health.currentHealth - 12f) : 0f;
        if (dmg > 0f) health.TakeDamage(dmg);
        sfx.transform.position = f.position; sfx.pitch = .8f; sfx.PlayOneShot(ProceduralAudio.Thud(), 1f);
        Vector3 away = f.position - player.position; away.y = 0f;
        f.position = player.position + away.normalized * .9f;
        const float hold = 1.1f;
        for (float t = 0f; t < hold; t += Time.deltaTime)
        {
            Quaternion look = Quaternion.LookRotation(f.position + Vector3.up * 1.3f - c.position);
            float k = 3.5f * (1f - t / hold) + .5f;
            c.rotation = Quaternion.Slerp(c.rotation, look, 12f * Time.deltaTime) * Quaternion.Euler(UnityEngine.Random.Range(-k, k), UnityEngine.Random.Range(-k, k), UnityEngine.Random.Range(-k, k));
            yield return null;
        }
        if (was && fpc != null) fpc.enabled = true;   // (its look code takes the camera back from here)
        else c.localRotation = q0;
    }

    void Cleanup(System.Collections.Generic.List<Transform> figures)
    {
        foreach (var f in figures) if (f != null && f != shade.transform) Destroy(f.gameObject);
        Hide();
    }

    // the shadow man: footsteps behind you, down the room. Look round and he is running at you, and gone before he
    // touches you. Until you look he only comes on slowly, and stops short at your back.
    bool rushSaid;
    IEnumerator Rush()
    {
        // most spots behind you are behind a wall too: one a frame until there is one you will see when you turn
        busy = true;
        Vector3 at = default; bool found = false;
        for (int i = 0; i < 60 && !found && canAct(); i++)
        {
            found = finder.TryFind(UnityEngine.Random.Range(12f, 17f), false, out at) && finder.Clear(cam.transform.position, at + Vector3.up * 1.2f);
            if (!found) yield return null;
        }
        if (found && !rushSaid && DialogueBox.Instance != null) { rushSaid = true; DialogueBox.Instance.Say(HS.S("385-386")); }   // the first time only
        if (!found) { busy = false; yield break; }
        var swap = shade; var swapAnim = anim;
        if (runner != null) { shade = runner; anim = runAnim; Show(at, 0f, 1f); anim.Play("Run", 0, UnityEngine.Random.value); }
        else Show(at, 1.3f, 2.6f);   // no run clip: the civilian walk cycle at 2.6x
        if (stomp == null) stomp = ProceduralAudio.Stomp();
        sfx.pitch = UnityEngine.Random.Range(.55f, .7f); sfx.PlayOneShot(ProceduralAudio.Rumble(1f), .6f);
        float step = 0f; bool seen = false;
        for (float t = 0f; t < 8f && canAct(); t += Time.deltaTime)
        {
            Face();
            Vector3 pos = shade.transform.position, to = player.position - pos; to.y = 0f;
            float d = to.magnitude;
            seen |= Vector3.Angle(cam.transform.forward, pos + Vector3.up * 1.2f - cam.transform.position) < 40f;
            if (d < (seen ? 2.2f : 4f)) break;
            Vector3 fwd = to / d; float len = (seen ? 7.5f : 2.2f) * Time.deltaTime;
            if (Physics.SphereCast(pos + Vector3.up * .9f, .3f, fwd, out var h, len + .3f, ~0, QueryTriggerInteraction.Ignore) && !h.transform.IsChildOf(player.root)) break;
            shade.transform.position = sfx.transform.position = pos + fwd * len;
            step -= Time.deltaTime;
            if (step <= 0f) { step = seen ? .24f : .5f; sfx.pitch = UnityEngine.Random.Range(.9f, 1.1f); sfx.PlayOneShot(stomp, .8f); }
            yield return null;
        }
        sfx.pitch = .7f; sfx.PlayOneShot(stomp, 1f);
        Hide();
        shade = swap; anim = swapAnim;
    }
}
