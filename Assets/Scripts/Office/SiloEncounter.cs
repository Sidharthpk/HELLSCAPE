using System.Collections;
using UnityEngine;

// The silo confrontation (story V4: Vane, in his second form, the Cry Wolf body), beat by beat:
//  Leap()            StoryTrigger on the last landing of the tower stairs: nowhere left to run, you jump into the
//                    silo; the fall takes half your health  (Push(): the old version, he shoves you off a ledge)
//  (auto)            the pistol from Kessler's armory is in your hand (or, if you never opened the armory, one is
//                    lying on the floor in front of you); Vane appears on the edge you jumped from,
//                    "NOWHERE TO RUN, DRIVER!", and jumps down after you
//  OnGunTaken()      the floor gun's Interactable: equip it (all the ammo you want), the fight is on
//  OnKillerDefeated  KillerBoss.onDefeated: he goes down, your keys come off his belt
//  OnKeysTaken()     the keys' Interactable: "It's over" - and the King: breaking his body doesn't end him. The silo
//                    floods with blood; you climb.
public class SiloEncounter : MonoBehaviour
{
    const string NL = "\n";   // (line break inside the notes below)
    [Header("Player")]
    public Transform player;
    public MonoBehaviour movement;
    public PlayerHealth health;
    public GunController gun;
    public ScreenFader fader;

    [Header("Silo")]
    public Transform siloCenter;            // on the floor, middle of the silo
    public float throwDistance = 2.8f;      // how far past the ledge you go over
    public float throwLift = 1.5f;          // enough to clear a knee-high lip or railing
    public KillerBoss killer;               // Vane (the object is still called Killer)
    public Transform killerLandPoint;
    public AudioSource sfx;                 // thuds
    public AudioClip thudClip;

    [Header("The rifle")]
    public Transform gunProp;               // world pickup (Interactable -> OnGunTaken), disabled at first
    public Transform gunThrowFrom;          // (old: where Maya threw it from; unused)
    public float bossAutoStart = 4f;        // he comes for you whether you've got the rifle or not

    [Header("After the fight")]
    public GameObject choicePanel;          // (old kill/mercy panel: kept hidden)
    public GameObject keysPickup;           // car keys (Interactable -> OnKeysTaken), disabled at first
    public BloodFlood flood;

    [Header("Vane's jump")]
    public float jumpTime = 1.3f;           // from up in the dark to the floor
    public float dropHeight = 28f;          // how far up he comes from

    [Header("Lines ('|' splits)")]
    [TextArea] public string pushLine = "...footsteps. Right behind me.";
    [TextArea] public string leapLine = "The stairs just... stop.|No. No no no-- JUMP!";
    [TextArea] public string landLine = "Agh... my leg... I can't feel my leg...";
    [TextArea] public string rifleLine = "A rifle. Security's.";
    [TextArea] public string jumpLine = "Vane: NOWHERE TO RUN, DRIVER!";
    [TextArea] public string fightLine = "";
    [TextArea] public string gunLine = "Okay. Okay. Aim.";
    [TextArea] public string defeatedLine = "";
    [TextArea] public string keysLine = "My keys.";
    [TextArea] public string kingLine = "";
    [TextArea] public string floodLine = "The grates... that's blood. It's filling up.|SWIM!";

    private bool pushed, gunTaken, beaten, keysTaken, jumping;
    private Vector3 ledge;                  // where you went over: where he comes from
    private bool hasLedge;

    public UnityEngine.Events.UnityEvent onLanded = new UnityEngine.Events.UnityEvent();   // e.g. the silo floor checkpoint

    // (the old kill/mercy choice: there is no choice any more)
    public void Choose(bool kill) { }

    void Start()
    {
        // The staircase leap replaces this old shove trigger. The separate floor fallback remains.
        foreach (var trigger in RevisedPlot.All<StoryTrigger>(gameObject.scene))
            if (trigger.name == "PushTrigger")
                for (int i = 0; i < trigger.onEnter.GetPersistentEventCount(); i++)
                    if (trigger.onEnter.GetPersistentTarget(i) == this && trigger.onEnter.GetPersistentMethodName(i) == "Push")
                        trigger.gameObject.SetActive(false);

        thudClip = Sounds.Clip("Silo Fight/Push and fall thud", thudClip != null ? thudClip : ProceduralAudio.Thud());
        if (gunProp != null) gunProp.gameObject.SetActive(false);
        if (keysPickup != null) keysPickup.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(false);
        if (killer != null) killer.gameObject.SetActive(false);
        SpawnNames();
    }

    // V5.1 A05: the manifest (Marcus Obi), Tomas's badge and the Driver's own tag lie on the silo floor, readable before
    // the fight and after it. Reading sets KnowsObi / KnowsTomas / FoundOwnTag (Truth).
    void SpawnNames()
    {
        if (siloCenter == null) return;
        Vector3 c = siloCenter.position, side = siloCenter.right * 2.2f, fwd = siloCenter.forward * 1.2f;
        EvidenceProp.Spawn(c + side - fwd, "Silo manifest", "SILO TWO MANIFEST", "214 ENTRIES" + NL + "Names: none" + NL + "COLD ROOM C: OBI M." + NL + "DISPOSAL: ALL OTHERS", "Silo Two kept 214 entries and not one full name. One tag in cold room C reads OBI M.: Marcus Obi.", HS5.S("A05_ledger"), () => Truth.Set("KnowsObi"));
        EvidenceProp.Spawn(c + fwd * 1.5f, "Tomas's badge", "T. ROURKE - STAFF BADGE", "KESSLER & VANE" + NL + "T. ROURKE" + NL + "NIGHT CLEANER", "Tomas Rourke's staff badge, found in Silo Two.", HS5.Id(173), () => Truth.Set("KnowsTomas"));
        EvidenceProp.Spawn(c - side - fwd, "Driver's tag", "DRIVER - DISPOSAL TAG", "DRIVER" + NL + "Authorised: A.K." + NL + "Dated before the shift ended", "Kessler authorised a tag with my name on it before I was dead.", HS5.S("A05_own_tag") + "|" + HS5.S("A05_own_tag_tape"), () => Truth.Set("FoundOwnTag"));
    }

    // a checkpoint respawn on the floor mid-fight: he stands up at full strength and comes again
    public void RespawnFight()
    {
        if (beaten) return;   // he was already down: nothing to redo
        StopAllCoroutines();
        jumping = false;
        Vector3 at = killerLandPoint.position;
        killer.ResetBoss(at, player.position - at);
        if (gunProp != null && !gunTaken) gunProp.gameObject.SetActive(true);
        if (movement != null) movement.enabled = true;
        StartCoroutine(RefightSoon());
    }

    IEnumerator RefightSoon()
    {
        yield return new WaitForSeconds(gunTaken ? 2.5f : bossAutoStart);
        killer.Activate();
    }

    public void Push()
    {
        if (pushed) return;
        pushed = true;
        StartCoroutine(PushRoutine());
    }

    // the end of the chase: the stairs run out, he's two steps behind, and you go over the rail yourself
    public void Leap()
    {
        if (pushed) return;
        pushed = true;
        StartCoroutine(LeapRoutine());
    }

    IEnumerator LeapRoutine()
    {
        var rb = player.GetComponent<Rigidbody>();
        var cam = player.GetComponentInChildren<Camera>();
        var chase = FindFirstObjectByType<ChaseKiller>();
        bool hunted = chase != null && chase.Chasing && killer.gameObject.activeInHierarchy
                      && Vector3.Distance(killer.transform.position, player.position) < 14f;
        if (chase != null) chase.Stop(false);
        movement.enabled = false;
        rb.linearVelocity = Vector3.zero;
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(leapLine);

        if (hunted)
        {
            // one look back: he's on the last steps, coming at you
            Vector3 Face() => killer.transform.position + Vector3.up * 1.62f * killer.transform.lossyScale.y;
            FaceFlat(killer.transform, player.position);
            if (killer.animator != null) killer.animator.SetFloat("Speed", 0f);
            Quaternion fromYaw = player.rotation, toYaw = Quaternion.LookRotation(Flat(killer.transform.position - player.position));
            Quaternion fromPitch = cam != null ? cam.transform.localRotation : Quaternion.identity;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.25f);
                player.rotation = Quaternion.Slerp(fromYaw, toYaw, k);
                if (cam != null) cam.transform.localRotation = Quaternion.Slerp(fromPitch, PitchTo(cam.transform, Face()), k);
                yield return null;
            }
            Play(thudClip, 1f);
            KillerBoss.Attack(killer.animator);
            yield return Jolt(cam, 0.45f, 0.05f, Face);
        }
        else yield return new WaitForSeconds(0.5f);

        // turn to the drop... and go
        Vector3 start = player.position;
        ledge = start; hasLedge = true;
        Vector3 into = Flat(siloCenter.position - start).normalized;
        Quaternion y0 = player.rotation, y1 = Quaternion.LookRotation(into);
        Quaternion p0 = cam != null ? cam.transform.localRotation : Quaternion.identity, down = Quaternion.Euler(55f, 0f, 0f);
        for (float t = 0f; t < 0.28f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.28f);
            player.rotation = Quaternion.Slerp(y0, y1, k);
            if (cam != null) cam.transform.localRotation = Quaternion.Slerp(p0, down, k);
            yield return null;
        }
        Play(thudClip, 0.6f);
        yield return Fall(rb, cam, start, into, Quaternion.Euler(78f, 0f, 0f));
    }

    IEnumerator PushRoutine()
    {
        var rb = player.GetComponent<Rigidbody>();
        var cam = player.GetComponentInChildren<Camera>();
        var chase = FindFirstObjectByType<ChaseKiller>();
        if (chase != null) chase.Stop(false);
        movement.enabled = false;
        rb.linearVelocity = Vector3.zero;

        // he's right behind you, feet on the ledge
        var body = player.GetComponent<Collider>();
        float feetY = body != null ? body.bounds.min.y : player.position.y - player.lossyScale.y;
        Vector3 behind = player.position - Flat(player.forward).normalized * 1.25f;
        behind.y = feetY;
        if (Physics.Raycast(behind + Vector3.up * 1f, Vector3.down, out RaycastHit ground, 2.5f, ~0, QueryTriggerInteraction.Ignore)
            && ground.collider.transform.root != player.root)
            behind.y = ground.point.y;
        killer.transform.position = behind;
        FaceFlat(killer.transform, player.position);
        killer.gameObject.SetActive(true);
        if (killer.animator != null) killer.animator.SetFloat("Speed", 0f);
        Play(thudClip, 0.35f);
        yield return new WaitForSeconds(0.35f);
        Play(thudClip, 0.45f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(pushLine);
        yield return new WaitForSeconds(0.7f);

        Vector3 Face() => killer.transform.position + Vector3.up * 1.62f * killer.transform.lossyScale.y;
        Quaternion fromYaw = player.rotation, toYaw = Quaternion.LookRotation(Flat(killer.transform.position - player.position));
        Quaternion fromPitch = cam != null ? cam.transform.localRotation : Quaternion.identity;
        for (float t = 0f; t < 0.28f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.28f);
            player.rotation = Quaternion.Slerp(fromYaw, toYaw, k);
            if (cam != null) cam.transform.localRotation = Quaternion.Slerp(fromPitch, PitchTo(cam.transform, Face()), k);
            yield return null;
        }
        Play(thudClip, 1f);
        KillerBoss.Attack(killer.animator);
        yield return Jolt(cam, 0.42f, 0.05f, Face);
        Play(thudClip, 0.9f);
        ledge = player.position; hasLedge = true;
        yield return Fall(rb, cam, player.position, Flat(siloCenter.position - player.position).normalized, PitchTo(cam != null ? cam.transform : player, Face()));
    }

    // over the edge, steered so you land in the middle of the silo floor
    IEnumerator Fall(Rigidbody rb, Camera cam, Vector3 start, Vector3 into, Quaternion look)
    {
        Vector3 end = start + into * throwDistance + Vector3.up * throwLift;
        rb.isKinematic = true;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            float k = t / 0.4f;
            player.position = Vector3.Lerp(start, end, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.5f;
            yield return null;
        }
        player.position = end;
        rb.isKinematic = false;
        float drop = Mathf.Max(0.5f, player.position.y - 1f - siloCenter.position.y);
        float fallTime = Mathf.Sqrt(2f * drop / -Physics.gravity.y);
        Vector3 across = Flat(siloCenter.position - player.position) * 0.85f;
        rb.linearVelocity = new Vector3(across.x / fallTime, rb.linearVelocity.y, across.z / fallTime);
        for (float t = 0f; t < 10f; t += Time.deltaTime)
        {
            if (cam != null) cam.transform.localRotation = Quaternion.Slerp(cam.transform.localRotation, look, 4f * Time.deltaTime);
            if (t > 0.3f && player.position.y < siloCenter.position.y + 2.5f && rb.linearVelocity.y > -0.5f) break;
            yield return null;
        }
        yield return Landed(cam);
    }

    // the silo floor: the fall doesn't kill you, but it takes half of what you had left
    IEnumerator Landed(Camera cam)
    {
        Play(thudClip, 1f);
        health.TakeDamage(Mathf.Min(health.currentHealth * 0.5f, health.currentHealth - 1f));
        if (fader != null) yield return fader.FadeTo(1f, 0.1f);
        killer.gameObject.SetActive(false);   // off the stairs while it's black: he's coming down after you
        if (cam != null) cam.transform.localRotation = Quaternion.identity;
        // the pistol from Kessler's armory is already in your hand; if you never opened the armory, a gun is lying on
        // the floor in front of you (it came down with the bodies)
        bool armed = GunController.Equipped;
        if (armed) { gunTaken = true; if (killer != null) killer.stunTime = 2.2f; }
        else if (gunProp != null)
        {
            Vector3 at = player.position + Flat(player.forward).normalized * 1.6f;
            at.y = siloCenter.position.y + 0.15f;
            gunProp.SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 90f));
            gunProp.gameObject.SetActive(true);
        }
        if (fader != null) { yield return new WaitForSeconds(1.2f); StartCoroutine(fader.FadeTo(0f, 1.5f)); }
        Say(landLine);
        MusicManager.Area("Silo Fight");
        onLanded.Invoke();
        yield return new WaitForSeconds(1f);
        movement.enabled = true;
        yield return RevisedPlotDirector.WaitForDialogue();
        if (!armed) { Say(rifleLine); yield return new WaitForSeconds(2.5f); }
        yield return VaneJumpsIn();
    }

    // he's up on the edge you went over, roaring down at you, and then he jumps
    IEnumerator VaneJumpsIn()
    {
        jumping = true;
        var cam = player.GetComponentInChildren<Camera>();
        // he comes over the side you went over, high above in the dark (the edge itself is too far up to see him)
        Vector3 land = killerLandPoint.position;
        Vector3 side = hasLedge ? Flat(ledge - land).normalized : Vector3.forward;
        Vector3 top = land + side * 2f + Vector3.up * dropHeight;
        killer.transform.position = top;
        FaceFlat(killer.transform, player.position);
        killer.gameObject.SetActive(true);
        if (killer.animator != null) killer.animator.SetFloat("Speed", 0f);
        Play(thudClip, 0.7f);

        // a roar from up there: you look up, into the dark
        Vector3 Face() => killer.transform.position + Vector3.up * 1.62f * killer.transform.lossyScale.y;
        movement.enabled = false;
        Quaternion fromYaw = player.rotation, fromPitch = cam != null ? cam.transform.localRotation : Quaternion.identity;
        Quaternion toYaw = Quaternion.LookRotation(Flat(top - player.position));
        Vector3 upThere = land + side * 2f + Vector3.up * 9f;
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.6f);
            player.rotation = Quaternion.Slerp(fromYaw, toYaw, k);
            if (cam != null) cam.transform.localRotation = Quaternion.Slerp(fromPitch, PitchTo(cam.transform, upThere), k);
            yield return null;
        }
        killer.PlayState("Throw");
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(jumpLine);
        yield return new WaitForSeconds(1.4f);

        // the drop: he falls into view and hits the floor in front of you
        Vector3 from = killer.transform.position;
        killer.PlayState("Charge");
        for (float t = 0f; t < jumpTime; t += Time.deltaTime)
        {
            float k = t / jumpTime;
            killer.transform.position = Vector3.Lerp(from, land, k * k);   // (falling: slow off the top, fast at the bottom)
            FaceFlat(killer.transform, player.position);
            if (cam != null)
            {
                player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(Flat(killer.transform.position - player.position)), 8f * Time.deltaTime);
                cam.transform.localRotation = Quaternion.Slerp(cam.transform.localRotation, PitchTo(cam.transform, Face()), 8f * Time.deltaTime);
            }
            yield return null;
        }
        killer.transform.position = land;
        killer.PlayState("Locomotion");
        Play(thudClip, 1f);
        BloodFx.Spray(land + Vector3.up * 0.2f, Vector3.up, 16, 3f);
        yield return Jolt(cam, 0.5f, 0.08f, Face);
        if (cam != null) { cam.transform.localRotation = PitchTo(cam.transform, Face()); var fpc = player.GetComponent<FirstPersonController>(); if (fpc != null) fpc.SetPitch(cam.transform.localEulerAngles.x); }
        movement.enabled = true;
        jumping = false;

        yield return new WaitForSeconds(gunTaken ? 0.8f : bossAutoStart);
        Say(fightLine);
        killer.Activate();
    }

    public void OnGunTaken()
    {
        if (gunTaken) return;
        gunTaken = true;
        if (killer != null) killer.stunTime = 2.2f;
        gun.Equip();
        gun.SetLimited(false);   // nobody throws you magazines any more: the gun has what you need
        Say(gunLine);
        if (!jumping && killer != null && killer.gameObject.activeInHierarchy && !killer.Defeated) StartCoroutine(StartFightSoon());
    }

    IEnumerator StartFightSoon()
    {
        yield return new WaitForSeconds(1f);
        killer.Activate();
    }

    public void OnKillerDefeated()
    {
        if (beaten) return;
        beaten = true;
        Truth.Set("VaneForm2Defeated");
        StartCoroutine(Fallen());
    }

    // down, and still: your keys come off his belt
    IEnumerator Fallen()
    {
        Judgement.KilledKiller = false;
        Say(defeatedLine);
        yield return RevisedPlotDirector.WaitForDialogue();
        yield return new WaitForSeconds(0.8f);
        keysPickup.transform.position = killer.transform.position + Flat(player.position - killer.transform.position).normalized * 1.2f + Vector3.up * 0.1f;
        keysPickup.SetActive(true);
        Play(thudClip, 0.3f);
    }

    public void OnKeysTaken()
    {
        if (keysTaken || !beaten) return;
        keysTaken = true;
        if (!Inventory.Has("CarKeys")) Inventory.Add("CarKeys");
        StartCoroutine(Flood());
    }

    // it's over - and then the blood comes up through the grates, and the King: breaking him wasn't enough
    IEnumerator Flood()
    {
        Say(keysLine);
        yield return RevisedPlotDirector.WaitForDialogue();
        yield return new WaitForSeconds(1f);
        Say(kingLine);
        yield return RevisedPlotDirector.WaitForDialogue();
        Say(floodLine);
        flood.Begin();
    }

    // camera-local rotation (pitch only, the body carries the yaw) that looks at a point
    static Quaternion PitchTo(Transform cam, Vector3 at)
    {
        Vector3 d = at - cam.position;
        float pitch = -Mathf.Atan2(d.y, Flat(d).magnitude) * Mathf.Rad2Deg;
        return Quaternion.Euler(Mathf.Clamp(pitch, -85f, 85f), 0f, 0f);
    }

    // short head-jolt on the player camera while keeping it on a target
    static IEnumerator Jolt(Camera cam, float time, float amount, System.Func<Vector3> lookAt)
    {
        if (cam == null) { yield return new WaitForSeconds(time); yield break; }
        Vector3 p0 = cam.transform.localPosition;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            cam.transform.localPosition = p0 + Random.insideUnitSphere * amount * (1f - t / time);
            cam.transform.localRotation = PitchTo(cam.transform, lookAt());
            yield return null;
        }
        cam.transform.localPosition = p0;
    }

    void Play(AudioClip c, float vol) { if (sfx != null && c != null) sfx.PlayOneShot(c, vol); }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    static void FaceFlat(Transform t, Vector3 target) { t.rotation = Quaternion.LookRotation(Flat(target - t.position)); }
    static void Say(string s) { if (!string.IsNullOrEmpty(s) && DialogueBox.Instance != null) DialogueBox.Instance.Say(s); }
}
