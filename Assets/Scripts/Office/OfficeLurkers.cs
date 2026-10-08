using System;
using System.Collections;
using UnityEngine;

// Random scares in the office: a black shadow figure that appears a few metres away and is gone when you look at it,
// one that RUNS at you and vanishes, footsteps lurking about, and one of the body bags dropping from the ceiling onto
// you (once). Only while the office act is calm (canAct), never during dialogue or cutscenes.
public class OfficeLurkers : MonoBehaviour
{
    OfficeHorrorDirector dir; OfficeStalker finder; Transform player; Camera cam; PlayerHealth health; Func<bool> canAct;
    GameObject shade, runner; Animator anim, runAnim; Material black; AudioSource sfx; AudioClip stomp;
    BodyBagScare bag; bool dropped, busy;
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
        shade.SetActive(false);
        // the runner is Kessler's own model with his RUNKESSLER "Run" clip, stripped of ragdoll and scripts (the original is inactive, so the copy is too)
        if (runTemplate != null && runTemplate.GetComponentInChildren<Animator>(true) is Animator src && src.HasState(0, Animator.StringToHash("Run")))
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
            runner.SetActive(false);
        }
        // the bag that falls: the westmost one, so the other keeps its own jump scare
        foreach (var b in RevisedPlot.All<BodyBagScare>(gameObject.scene))
            if (bag == null || b.transform.position.x < bag.transform.position.x) bag = b;
        if (bag != null) bag.enabled = false;
    }

    void OnDestroy() { if (black != null) Destroy(black); if (stomp != null) Destroy(stomp); }

    void Update()
    {
        if (player == null || !canAct()) return;
        if (!dropped && bag != null && !busy)
        {
            Vector3 d = player.position - bag.transform.position; d.y = 0f;
            if (d.magnitude < 4.5f) { dropped = true; StartCoroutine(DropBag()); return; }
        }
        if (busy) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = UnityEngine.Random.Range(18f, 38f);
        float r = UnityEngine.Random.value;
        if (r < .35f) dir.LurkSteps();
        else if (r < .7f) StartCoroutine(Lurk());
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

    // stands there; gone once you look at it or come close
    IEnumerator Lurk()
    {
        if (!finder.TryFind(UnityEngine.Random.Range(6f, 9f), true, out var at)) yield break;
        busy = true; Show(at, 0f, 1f); Face();
        sfx.pitch = UnityEngine.Random.Range(.6f, .8f); sfx.PlayOneShot(ProceduralAudio.Rumble(1.2f), .6f);
        float seen = 0f;
        for (float t = 0f; t < 7f && canAct(); t += Time.deltaTime)
        {
            Face();
            bool looked = Vector3.Angle(cam.transform.forward, at + Vector3.up * 1.2f - cam.transform.position) < 28f;
            seen = looked ? seen + Time.deltaTime : Mathf.Max(0f, seen - Time.deltaTime);
            Vector3 d = player.position - at; d.y = 0f;
            if (seen > .8f || d.magnitude < 3.5f) break;
            yield return null;
        }
        Hide();
    }

    // the fast run: straight at you, and gone before it touches you
    IEnumerator Rush()
    {
        if (!finder.TryFind(UnityEngine.Random.Range(9f, 12f), true, out var at)) yield break;
        busy = true;
        var swap = shade; var swapAnim = anim;
        if (runner != null) { shade = runner; anim = runAnim; Show(at, 0f, 1f); anim.Play("Run", 0, UnityEngine.Random.value); }
        else Show(at, 1.3f, 2.6f);   // no run clip: the civilian walk cycle at 2.6x
        if (stomp == null) stomp = ProceduralAudio.Stomp();
        sfx.pitch = UnityEngine.Random.Range(.55f, .7f); sfx.PlayOneShot(ProceduralAudio.Rumble(1f), .6f);
        float step = 0f;
        for (float t = 0f; t < 3f && canAct(); t += Time.deltaTime)
        {
            Face();
            Vector3 pos = shade.transform.position, to = player.position - pos; to.y = 0f;
            float d = to.magnitude;
            if (d < 1.8f) break;
            Vector3 fwd = to / d; float len = 7.5f * Time.deltaTime;
            if (Physics.SphereCast(pos + Vector3.up * .9f, .3f, fwd, out var h, len + .3f, ~0, QueryTriggerInteraction.Ignore) && !h.transform.IsChildOf(player.root)) break;
            shade.transform.position = sfx.transform.position = pos + fwd * len;
            step -= Time.deltaTime;
            if (step <= 0f) { step = .24f; sfx.pitch = UnityEngine.Random.Range(.9f, 1.1f); sfx.PlayOneShot(stomp, .8f); }
            yield return null;
        }
        sfx.pitch = .7f; sfx.PlayOneShot(stomp, 1f);
        Hide();
        shade = swap; anim = swapAnim;
    }

    // one body bag comes down off the ceiling right where you're standing
    IEnumerator DropBag()
    {
        busy = true;
        var t = bag.transform;
        var cols = t.GetComponentsInChildren<Collider>(); foreach (var c in cols) c.enabled = false;
        Quaternion rest = t.rotation; float floorY = t.position.y;
        float ceil = player.position.y + 3f;
        if (Physics.Raycast(cam.transform.position, Vector3.up, out var up, 6f, ~0, QueryTriggerInteraction.Ignore) && !up.transform.IsChildOf(player.root)) ceil = up.point.y - .4f;
        Vector3 fwd = cam.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > .01f ? fwd.normalized : Vector3.forward;
        Vector3 xz = player.position + fwd * .4f;
        t.position = new Vector3(xz.x, ceil, xz.z);
        var spin = UnityEngine.Random.onUnitSphere * 140f;
        float v = 0f;
        while (t.position.y > floorY)
        {
            v += 22f * Time.deltaTime;
            Vector3 p = t.position; p.y -= v * Time.deltaTime;
            Vector3 target = player.position + fwd * .4f;
            p.x = Mathf.MoveTowards(p.x, target.x, 6f * Time.deltaTime); p.z = Mathf.MoveTowards(p.z, target.z, 6f * Time.deltaTime);
            t.position = p; t.Rotate(spin * Time.deltaTime, Space.World);
            yield return null;
        }
        t.position = new Vector3(t.position.x, floorY, t.position.z);
        t.rotation = rest * Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        foreach (var c in cols) if (c != null) c.enabled = true;
        sfx.transform.position = t.position; sfx.pitch = .8f; sfx.PlayOneShot(ProceduralAudio.Thud(), 1f);
        Vector3 off = player.position - t.position; off.y = 0f;
        if (off.magnitude < 1.5f && health != null && !health.IsDead)
        {
            float dmg = Mathf.Min(10f, health.currentHealth - 12f);
            if (dmg > 0f) health.TakeDamage(dmg);
        }
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow("AH-- a body bag. It fell right off the ceiling.");
        busy = false;
    }
}
