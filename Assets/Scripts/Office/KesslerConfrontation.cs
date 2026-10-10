using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

// After the tape: Vane defends his crimes. Justification, not admission alone, turns him Damned.
//   Arm()        the CCTV flashback is over: his talk becomes the confrontation (confrontNodes)
//   (talk ends)  he admits it -> "YOU SHOULD BE THANKING ME" -> the blue ghost starts to burn red, shaking itself
//                apart, and the silo key falls out of his coat (user, 2026-10-02: there is no fight here any more).
//                You get a couple of seconds of it, then you're free: grab the key and get out through the silo
//                door while he's still turning. KesslerChase takes it from there (the tunnel).
//   too slow     stay for the whole of it (turnTime) and the damned thing lunges out and hunts you through the
//                rooms (line of sight, else your trail) until you leave. Fists don't put it down.
//   Leave()      you went through the silo door: the office is done with
public class KesslerConfrontation : MonoBehaviour
{
    public GhostNPC ghost;
    public GhostNode[] confrontNodes;
    public Zombie damned;                   // a zombie with its NavMesh AI stripped (no navmesh in the office): walked from here
    public Animator damnedAnim;             // "Speed" float, "Attack" trigger

    [Header("Player")]
    public Transform player;
    public Camera playerCam;
    public FirstPersonController fpc;
    public Behaviour[] lockDuring;
    public ScreenFader fader;
    public AudioSource sfx;

    [Header("The damned")]
    public float speed = 2.6f;
    public float runClipSpeed = 3.6f;       // the run clip's own pace (ZombieAI's)
    public float attackRange = 1.7f;
    public float attackCooldown = 1.6f;
    public int attackDamage = 10;
    public int health = 99999;              // (it isn't beaten here: you run)
    public float noticeRange = 14f;

    [Header("The turn: you run while it happens")]
    public Transform keyPickup;             // the silo key (an Interactable), switched off until it falls out of his coat
    public float watchTime = 2.6f;          // you're held looking at him this long, then you can move
    public float turnTime = 18f;            // how long the whole change takes: your time to grab the key and go

    [Header("Lines ('|' splits)")]
    [TextArea] public string turnLine = "Kessler: Everything I did, I did for you. For you and your brother. To make us RICH.|Kessler: YOU SHOULD BE THANKING ME!";
    [TextArea] public string keyLine = "He said it out loud-- and the city's TAKING him.|Something fell out of his coat. Keys. The SILO key.|Grab it and GO, before he's finished turning!";
    [TextArea] public string afterTurnLine = "Too slow-- he's TURNED. The silo door. NOW!";
    [TextArea] public string defeatedLine = "";
    public UnityEvent onDefeated = new UnityEvent();

    [Header("Vane's ambush: right after the tape, not waiting in the hall")]
    public bool ambush = true;
    public float creepSpeed = 1.15f;        // m/s, closing in from behind you
    public float creepStart = 9f;           // how far behind you he starts
    public float scareDistance = 2.3f;      // this close and he goes for you
    public float creepTimeout = 40f;        // he does not wait for ever
    [TextArea] public string scareLine = "Vane: That tape was WIPED! Nobody was supposed to see that!";
    public string scareShout = "WHAT ARE YOU DOING?!";
    public bool Ambushing { get; private set; }
    public bool PreparingAmbush { get; private set; }

    private bool armed, turned, hunting, done;
    readonly List<Behaviour> heldControls = new List<Behaviour>();
    Vector3 originalBodyScale, originalGhostPosition, originalCameraPosition;
    Quaternion originalGhostRotation;
    float originalGlowIntensity;
    Color originalGlowColor, originalFaderColor;
    bool ambushSnapshot;
    readonly Dictionary<Renderer, Material[]> ambushMaterials = new Dictionary<Renderer, Material[]>();
    GameObject shoutCanvas;
    public bool ScareVisible { get; private set; }

    public void AbortAmbush()
    {
        if (!Ambushing && !PreparingAmbush) return;
        StopAllCoroutines();
        RestoreAmbush();
        Ambushing = PreparingAmbush = false;
        if (ghost != null) { ghost.transform.SetPositionAndRotation(originalGhostPosition, originalGhostRotation); ghost.gameObject.SetActive(true); }
    }
    void RestoreAmbush()
    {
        ScareVisible = false;
        if (shoutCanvas != null) Destroy(shoutCanvas);
        if (ambushSnapshot && ghost != null)
        {
            var body = ghost.body != null ? ghost.body : ghost.transform;
            body.localScale = originalBodyScale;
            foreach (var r in body.GetComponentsInChildren<Renderer>(true)) r.SetPropertyBlock(null);
            var light = ghost.GetComponentInChildren<Light>();
            if (light != null) { light.intensity = originalGlowIntensity; light.color = originalGlowColor; }
            if (playerCam != null) playerCam.transform.localPosition = originalCameraPosition;
            if (fader != null && fader.fade != null) { fader.fade.alpha = 0f; var img = fader.GetComponent<UnityEngine.UI.Image>(); if (img != null) img.color = originalFaderColor; }
            ghost.enabled = true;
            foreach (var it in ghost.GetComponents<Interactable>()) it.enabled = true;
            foreach (var c in ghost.GetComponents<Collider>()) c.enabled = true;
        }
        foreach (var pair in ambushMaterials) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        ambushMaterials.Clear();
        ambushSnapshot = false;
        Lock(false);
    }
    void OnDisable() { AbortAmbush(); Lock(false); }

    public bool Armed => armed;
    public bool Done => done;
    private Vector3 turnSpot;
    private Quaternion turnRot;
    private float attackTimer, trailTimer;
    AudioClip stepClip;
    void OnDestroy() { if (stepClip != null) Destroy(stepClip); }
    private readonly List<Vector3> trail = new List<Vector3>();

    void Start()
    {
        if (damned != null) damned.gameObject.SetActive(false);
    }

    public void Arm()
    {
        if (armed || done || ghost == null) return;
        armed = true;
        EvidenceJournal.Collect(EvidenceJournal.Tape);
        ghost.Replace(confrontNodes);
        ghost.onFinished.AddListener(() => { if (!turned) StartCoroutine(Turn()); });
        if (ambush && ghost != null && playerCam != null)
        { originalGhostPosition = ghost.transform.position; originalGhostRotation = ghost.transform.rotation; ghost.gameObject.SetActive(false); StartCoroutine(Ambush()); }
    }

    // ---------------------------------------------------------------- the ambush

    // The flashback ends; the office is quiet. He is not in the hall any more: he is somewhere behind you, coming
    // closer while you are not looking (he stops dead when you turn). Then the jump, and he talks.
    IEnumerator Ambush()
    {
        PreparingAmbush = true;
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;
        while (fpc != null && (!fpc.enabled || PauseMenu.Paused || (ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open))) yield return null;
        // Let the player step away from the terminal before Vane closes in.
        Vector3 terminal = player.position;
        for (float wait = 0f; wait < 6f && Vector3.Distance(player.position, terminal) < 2.5f;)
        { if (!PauseMenu.Paused && fpc.enabled) wait += Time.deltaTime; yield return null; }
        yield return new WaitForSeconds(1.5f);

        PreparingAmbush = false; Ambushing = true;
        ghost.CancelConversation();
        ghost.enabled = false;   // his own wandering would fight this
        foreach (var it in ghost.GetComponents<Interactable>()) it.enabled = false;
        foreach (var c in ghost.GetComponents<Collider>()) c.enabled = false;
        Transform body = ghost.body != null ? ghost.body : ghost.transform;
        Vector3 scale0 = body.localScale;
        originalBodyScale = scale0; originalGhostPosition = ghost.transform.position; originalGhostRotation = ghost.transform.rotation;
        originalCameraPosition = playerCam.transform.localPosition;
        ambushSnapshot = true;
        ghost.gameObject.SetActive(true);
        var rends = body.GetComponentsInChildren<Renderer>(true);
        var intact = WitnessLook.Load("IntactMale");
        if (intact != null) foreach (var r in rends) if (r is SkinnedMeshRenderer)
        { ambushMaterials[r] = r.sharedMaterials; var mats = r.sharedMaterials; for (int i = 0; i < mats.Length; i++) mats[i] = intact; r.sharedMaterials = mats; }
        var block = new MaterialPropertyBlock();
        var glow = ghost.GetComponentInChildren<Light>();
        float glow0 = glow != null ? glow.intensity : 0f;
        originalGlowIntensity = glow0; originalGlowColor = glow != null ? glow.color : Color.white;
        var originalImage = fader != null ? fader.GetComponent<UnityEngine.UI.Image>() : null;
        originalFaderColor = originalImage != null ? originalImage.color : Color.black;
        PlaceBehind(creepStart);

        float t = 0f, watched = 0f, stepTimer = 0f;
        while (t < creepTimeout)
        {
            if (!fpc.enabled || PauseMenu.Paused || (DialogueBox.Instance != null && DialogueBox.Instance.Busy)) { yield return null; continue; }   // not while you are in something else
            t += Time.deltaTime;
            Vector3 pos = ghost.transform.position;
            Vector3 to = Flat(player.position - pos);
            float dist = to.magnitude;
            Vector3 chest = pos + Vector3.up * 1.2f;
            bool seen = Vector3.Angle(playerCam.transform.forward, chest - playerCam.transform.position) < 42f && ClearTo(playerCam.transform.position, chest);
            Tint(rends, block, new Color(.06f, .0f, .0f, seen ? .95f : .8f), 0f);
            ghost.transform.rotation = Quaternion.LookRotation(to);
            if (seen)
            {
                // frozen the moment you look; look for long and he is somewhere behind you again, no nearer than before
                watched += Time.deltaTime;
                if (ghost.animator != null) ghost.animator.SetFloat("Speed", 0f, .1f, Time.deltaTime);
                if (watched > 2.4f) { watched = 0f; PlaceBehind(Mathf.Max(dist, 6f)); }
            }
            else
            {
                watched = 0f;
                Vector3 dir = to / Mathf.Max(dist, .01f);
                if (FreeFor(chest, dir)) ghost.transform.position += dir * creepSpeed * Time.deltaTime;
                if (ghost.animator != null) { ghost.animator.SetFloat("Speed", ghost.walkClipSpeed, .2f, Time.deltaTime); ghost.animator.speed = Mathf.Clamp(creepSpeed / ghost.walkClipSpeed, .3f, 1f); }
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f) { stepTimer = .75f; Play(stepClip != null ? stepClip : stepClip = ProceduralAudio.Stomp(), Mathf.Lerp(.15f, .6f, 1f - Mathf.Clamp01(dist / creepStart))); }
            }
            if (dist <= scareDistance) break;
            yield return null;
        }

        // the jump
        Lock(true);
        // he is suddenly right there: on the free side of you nearest to where he was creeping (a wall can't have him)
        if (!JumpSpot(Flat(ghost.transform.position - player.position).normalized, out Vector3 spot))
        {
            RestoreAmbush(); Ambushing = false;
            ghost.transform.SetPositionAndRotation(originalGhostPosition, originalGhostRotation);
            DialogueBox.Instance?.Say(HS.S("340"));
            yield break;
        }
        ScareVisible = true;
        ghost.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(Flat(player.position - spot)));
        yield return LookAt(spot + Vector3.up * 1.5f, .1f);
        body.localScale = scale0 * 1.18f;
        Tint(rends, block, new Color(1f, .05f, .03f, 1f), 3f);
        if (glow != null) { glow.color = Color.red; glow.intensity = glow0 + 6f; }
        Aim(ghost.transform.position + Vector3.up * 1.6f);
        var img = fader != null ? fader.GetComponent<UnityEngine.UI.Image>() : null;
        Color black = img != null ? img.color : Color.black;
        if (img != null && fader.fade != null) { img.color = new Color(.35f, .015f, .01f, black.a); fader.fade.alpha = .32f; }
        Play(ProceduralAudio.Thud(), .85f);
        Play(ProceduralAudio.Rumble(1.6f), 1f);
        Play(Sounds.Clip("Office/Vane jumpscare", null) ?? ProceduralAudio.Whisper(1.4f), 1f);
        StartCoroutine(Shake(.6f, .035f));
        StartCoroutine(Shout(scareShout, 1.3f));
        for (float f = .32f; f > 0f; f -= Time.deltaTime * 1.6f) { if (fader != null && fader.fade != null) fader.fade.alpha = f; Aim(ghost.transform.position + Vector3.up * 1.6f); yield return null; }
        if (fader != null && fader.fade != null) fader.fade.alpha = 0f;
        if (img != null) img.color = black;
        yield return new WaitForSeconds(.5f);

        // he stays up in your face, and talks
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(scareLine)) DialogueBox.Instance.SayNow(scareLine);
        yield return null;
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) { Aim(ghost.transform.position + Vector3.up * 1.6f); yield return null; }

        // back to normal for the talk: the answers are yours to pick
        RestoreAmbush();
        Tint(rends, block, default, -1f);
        body.localScale = scale0;
        if (glow != null) { glow.intensity = glow0; glow.color = new Color(.5f, .85f, 1f); }
        ghost.enabled = true;
        foreach (var it in ghost.GetComponents<Interactable>()) it.enabled = true;
        foreach (var c in ghost.GetComponents<Collider>()) c.enabled = true;
        Ambushing = false;
        Lock(false);
        ghost.Talk();
    }

    // the three lines the jump needs: a spot out of sight behind you, who can see what, and the shout on screen
    void PlaceBehind(float dist)
    {
        for (int i = 0; i < 40; i++)
        {
            float yaw = playerCam.transform.eulerAngles.y + 180f + Random.Range(-45f, 45f);
            float d = i < 20 ? dist : dist * .6f;
            Vector3 p = player.position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * d;
            if (!Physics.Raycast(new Vector3(p.x, player.position.y + .3f, p.z), Vector3.down, out RaycastHit h, 3.5f, ~0, QueryTriggerInteraction.Ignore) || h.normal.y < .9f) continue;
            if (Mathf.Abs(h.point.y - (player.position.y - 1f)) > 1.2f) continue;
            p = h.point;
            if (Physics.CheckCapsule(p + Vector3.up * .45f, p + Vector3.up * 1.7f, .32f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (Vector3.Angle(playerCam.transform.forward, p + Vector3.up * 1.2f - playerCam.transform.position) < 60f) continue;   // never in view
            ghost.transform.position = p;
            return;
        }
        // Retain the last valid position if no free floor can be found. Never spawn inside a wall.
    }

    bool JumpSpot(Vector3 prefer, out Vector3 spot)
    {
        foreach (float d in new[] { 1.15f, .85f })
            for (int i = 0; i < 12; i++)
            {
                float a = (i % 2 == 0 ? 1 : -1) * (i / 2) * 30f;
                Vector3 p = player.position + Quaternion.Euler(0f, a, 0f) * prefer * d;
                if (!Physics.Raycast(new Vector3(p.x, player.position.y + .3f, p.z), Vector3.down, out RaycastHit h, 3.5f, ~0, QueryTriggerInteraction.Ignore) || h.normal.y < .9f) continue;
                if (Mathf.Abs(h.point.y - (player.position.y - 1f)) > 1.2f) continue;
                p = h.point;
                if (Physics.CheckCapsule(p + Vector3.up * .45f, p + Vector3.up * 1.7f, .3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!ClearTo(playerCam.transform.position, p + Vector3.up * 1.2f)) continue;
                spot = p; return true;
            }
        spot = default; return false;
    }

    bool ClearTo(Vector3 from, Vector3 to)
    {
        foreach (var h in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(ghost.transform) && !h.collider.transform.IsChildOf(player.root)) return false;
        return true;
    }

    bool FreeFor(Vector3 from, Vector3 dir)
    {
        foreach (var h in Physics.RaycastAll(from, dir, .5f, ~0, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(ghost.transform) && !h.collider.transform.IsChildOf(player.root)) return false;
        return true;
    }

    // paint the body (strength < 0: put it back)
    static void Tint(Renderer[] rends, MaterialPropertyBlock block, Color c, float emission)
    {
        foreach (var r in rends)
        {
            if (r == null) continue;
            if (emission < 0f) { r.SetPropertyBlock(null); continue; }
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_EmissionColor", new Color(c.r, c.g * .2f, c.b * .2f) * emission);
            r.SetPropertyBlock(block);
        }
    }

    // big pixel-font text in the middle of the screen
    IEnumerator Shout(string text, float seconds)
    {
        var canvas = new GameObject("Vane shout").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 450;
        shoutCanvas = canvas.gameObject;
        var t = new GameObject("text").AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(canvas.transform, false);
        t.font = Resources.Load<TMP_FontAsset>("NotePixelFont") ?? t.font;
        t.raycastTarget = false; t.text = text; t.fontSize = 64f; t.alignment = TextAlignmentOptions.Center; t.color = new Color(.95f, .05f, .03f);
        t.enableAutoSizing = true; t.fontSizeMax = 110f; t.fontSizeMin = 40f;
        var rt = t.rectTransform; rt.anchorMin = new Vector2(.08f, .12f); rt.anchorMax = new Vector2(.92f, .29f); rt.offsetMin = rt.offsetMax = Vector2.zero;
        for (float s = 0f; s < seconds && rt != null && t != null; s += Time.deltaTime)
        {
            rt.anchoredPosition = Random.insideUnitCircle * 14f * (1f - s / seconds);
            t.alpha = s > seconds - .3f ? (seconds - s) / .3f : 1f;
            yield return null;
        }
        if (canvas != null) Destroy(canvas.gameObject);
    }

    // ---------------------------------------------------------------- the turn

    IEnumerator Turn()
    {
        turned = true;
        turnSpot = ghost.transform.position; turnRot = ghost.transform.rotation;
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;   // let his last answer land
        if (RevisedPlotDirector.Instance != null) RevisedPlotDirector.Instance.ReleaseVane();
        Lock(true);
        ghost.enabled = false;   // its own flicker would fight ours
        foreach (var it in ghost.GetComponents<Interactable>()) it.enabled = false;
        foreach (var c in ghost.GetComponents<Collider>()) c.enabled = false;
        Transform body = ghost.body != null ? ghost.body : ghost.transform;
        Vector3 head = ghost.transform.position + Vector3.up * 1.6f;
        yield return LookAt(head, 0.6f);

        var renderers = body.GetComponentsInChildren<Renderer>();
        var block = new MaterialPropertyBlock();
        var glow = ghost.GetComponentInChildren<Light>();
        var whisper = ghost.GetComponent<AudioSource>();
        Vector3 body0 = body.localPosition;
        Color blue = renderers.Length > 0 ? renderers[0].sharedMaterial.GetColor("_BaseColor") : Color.cyan;

        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(turnLine);
        Play(Sounds.Clip("Office/Kessler turning (whisper)", null) ?? ProceduralAudio.Whisper(3f), 0.8f * Sounds.Volume("Office/Kessler turning (whisper)"));
        bool free = false;
        for (float t = 0f; t < turnTime; t += Time.deltaTime)
        {
            // blue -> blood red, ever more solid, shaking itself apart
            float k = Mathf.Clamp01(t / turnTime);
            Color c = Color.Lerp(blue, new Color(0.8f, 0.03f, 0.02f), k);
            c.a = Mathf.Lerp(0.4f, 0.95f, k) * (Random.value < 0.08f ? 0.2f : 1f);
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c);
                r.SetPropertyBlock(block);
            }
            body.localPosition = body0 + Random.insideUnitSphere * (0.02f + 0.07f * k);
            if (glow != null) { glow.color = Color.Lerp(new Color(0.5f, 0.85f, 1f), Color.red, k); glow.intensity = 0.8f + 4f * k; }
            if (whisper != null) whisper.pitch = Mathf.Lerp(0.9f, 0.45f, k);
            if (!free && t >= watchTime)
            {
                // you've seen enough: the key drops, and you can move
                free = true;
                DropKey();
                Lock(false);
                if (DialogueBox.Instance != null) DialogueBox.Instance.Say(keyLine);
            }
            if (!free) Aim(head);
            yield return null;
        }

        // still here: the lost becomes the damned, and it comes for you
        Play(Sounds.Clip("Office/Kessler becomes the damned", null) ?? ProceduralAudio.Thud(), Sounds.Volume("Office/Kessler becomes the damned"));
        if (fader != null) yield return fader.FadeTo(1f, 0.08f);
        turnSpot = ghost.transform.position;
        turnRot = Quaternion.LookRotation(Flat(player.position - turnSpot));
        ghost.gameObject.SetActive(false);
        damned.transform.SetPositionAndRotation(turnSpot, turnRot);
        damned.health = health;
        damned.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.25f);
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.2f));
        if (damnedAnim != null) damnedAnim.SetTrigger("Attack");
        Play(Sounds.Clip("Office/Kessler lunges", null) ?? ProceduralAudio.Thud(), Sounds.Volume("Office/Kessler lunges"));
        yield return Shake(0.5f, 0.06f);

        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(afterTurnLine);
        MusicManager.Area("Kessler Fight");
        Lock(false);
        hunting = true;
    }

    // the silo key, on the floor between him and you, lit so you can't miss it
    void DropKey()
    {
        if (keyPickup == null || Inventory.Has("SiloKey")) return;
        Vector3 at = ghost.transform.position + Flat(player.position - ghost.transform.position).normalized * 0.9f;
        if (Physics.Raycast(at + Vector3.up, Vector3.down, out RaycastHit floor, 3f, ~0, QueryTriggerInteraction.Ignore)) at.y = floor.point.y;
        keyPickup.SetParent(transform, true);   // (out of the safe it used to sit in)
        keyPickup.position = at + Vector3.up * 0.05f;
        keyPickup.gameObject.SetActive(true);
        Play(ProceduralAudio.Click(), 0.8f);
    }

    // through the silo door (KesslerChase.Arrive): whatever state he was in here, it's over
    public void Leave()
    {
        AbortAmbush();
        StopAllCoroutines();
        Lock(false);
        hunting = false;
        done = true;
        if (ghost != null) ghost.gameObject.SetActive(false);
        if (damned != null) damned.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- too slow: it hunts you

    void Update()
    {
        if (!hunting || done || damned == null || player == null || PauseMenu.Paused || GameOverScreen.Open || (fpc != null && !fpc.enabled)) return;
        if (damned.IsDead) { StartCoroutine(Defeated()); return; }

        Transform z = damned.transform;
        Vector3 eye = z.position + Vector3.up * 1.5f;
        Vector3 toPlayer = player.position - z.position;
        bool sees = toPlayer.magnitude < noticeRange && Clear(eye, player.position + Vector3.up * 0.5f);

        // keep your trail while it can't see you: it follows you through doorways
        trailTimer -= Time.deltaTime;
        if (sees) trail.Clear();
        else if (trailTimer <= 0f) { trailTimer = 0.3f; trail.Add(player.position); if (trail.Count > 60) trail.RemoveAt(0); }

        attackTimer -= Time.deltaTime;
        if (sees && Flat(toPlayer).magnitude <= attackRange)
        {
            Speed(0f);
            z.rotation = Quaternion.Slerp(z.rotation, Quaternion.LookRotation(Flat(toPlayer)), 10f * Time.deltaTime);
            if (attackTimer <= 0f)
            {
                attackTimer = attackCooldown;
                if (damnedAnim != null) damnedAnim.SetTrigger("Attack");
                StartCoroutine(HitSoon());
            }
            return;
        }

        Vector3 target;
        if (sees) target = player.position;
        else if (trail.Count > 0)
        {
            target = trail[0];
            if (Flat(target - z.position).magnitude < 0.5f) { trail.RemoveAt(0); return; }
        }
        else { Speed(0f); return; }   // lost you completely: wait for you to come back into view

        Vector3 dir = Flat(target - z.position).normalized;
        Vector3 step = dir * speed * Time.deltaTime;
        if (Free(z.position + Vector3.up * 0.8f, dir)) z.position += step;
        z.rotation = Quaternion.Slerp(z.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        Speed(speed);
    }

    // nothing in the way but its own body or the player (who it's allowed to walk into)
    bool Free(Vector3 from, Vector3 dir)
    {
        foreach (var h in Physics.RaycastAll(from, dir, 0.5f, ~0, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(damned.transform) && !h.collider.transform.IsChildOf(player)) return false;
        return true;
    }

    bool Clear(Vector3 from, Vector3 to)
    {
        foreach (var h in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(damned.transform) && !h.collider.transform.IsChildOf(player)) return false;
        return true;
    }

    IEnumerator HitSoon()
    {
        yield return new WaitForSeconds(0.4f);
        if (damned == null || damned.IsDead || done || !hunting || PauseMenu.Paused || GameOverScreen.Open) yield break;
        if (Flat(player.position - damned.transform.position).magnitude > attackRange + 0.4f || !Clear(damned.transform.position + Vector3.up, player.position)) yield break;   // you stepped back in time
        var ph = player.GetComponentInParent<PlayerHealth>();
        if (ph != null) ph.TakeDamage(attackDamage);
    }

    IEnumerator Defeated()
    {
        done = true;
        hunting = false;
        yield return new WaitForSeconds(1.5f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.Say(defeatedLine);
        MusicManager.Area("Office");
        onDefeated.Invoke();
    }

    // a checkpoint respawn mid-fight: it's back where it turned, whole again, waiting
    public void ResetFight()
    {
        AbortAmbush();
        if (!turned || done || damned == null) return;
        StopAllCoroutines();
        Lock(false);
        trail.Clear();
        damned.transform.SetPositionAndRotation(turnSpot, turnRot);
        damned.health = health;
        damned.gameObject.SetActive(true); if (ghost != null) ghost.gameObject.SetActive(false);
        DropKey();
        Speed(0f);
        hunting = true;
    }

    // ---------------------------------------------------------------- helpers

    void Lock(bool on)
    {
        if (on)
        {
            foreach (var b in lockDuring ?? new Behaviour[0])
                if (b != null && b.enabled) { heldControls.Add(b); b.enabled = false; }
        }
        else { foreach (var b in heldControls) if (b != null) b.enabled = true; heldControls.Clear(); }
        if (on && player.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    IEnumerator LookAt(Vector3 point, float time)
    {
        Quaternion y0 = player.rotation, p0 = playerCam.transform.localRotation;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            Vector3 d = point - playerCam.transform.position;
            player.rotation = Quaternion.Slerp(y0, Quaternion.LookRotation(Flat(d)), k);
            playerCam.transform.localRotation = Quaternion.Slerp(p0, Pitch(d), k);
            yield return null;
        }
        Aim(point);
    }

    void Aim(Vector3 point)
    {
        Vector3 d = point - playerCam.transform.position;
        player.rotation = Quaternion.LookRotation(Flat(d));
        playerCam.transform.localRotation = Pitch(d);
        if (fpc != null) fpc.SetPitch(playerCam.transform.localEulerAngles.x);
    }

    static Quaternion Pitch(Vector3 d) => Quaternion.Euler(Mathf.Clamp(-Mathf.Atan2(d.y, Flat(d).magnitude) * Mathf.Rad2Deg, -80f, 80f), 0f, 0f);

    IEnumerator Shake(float time, float amount)
    {
        Vector3 p0 = playerCam.transform.localPosition;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            playerCam.transform.localPosition = p0 + Random.insideUnitSphere * amount * (1f - t / time);
            yield return null;
        }
        playerCam.transform.localPosition = p0;
    }

    void Speed(float v)
    {
        if (damnedAnim == null) return;
        damnedAnim.SetFloat("Speed", v, 0.15f, Time.deltaTime);
        damnedAnim.speed = v < 0.05f ? 1f : Mathf.Clamp(v / runClipSpeed, 0.8f, 2.2f);
    }

    void Play(AudioClip c, float v) { if (sfx != null && c != null) sfx.PlayOneShot(c, v); }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v : Vector3.forward; }
}
