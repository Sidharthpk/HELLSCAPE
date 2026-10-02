using System.Collections;
using UnityEngine;

// The silo confrontation, beat by beat:
//  Leap()            StoryTrigger on the last landing of the tower stairs: nowhere left to run, you jump into the
//                    silo; the fall takes half your health  (Push(): the old version, he shoves you off a ledge)
//  (auto)            he drops down after you (Derek's already dead: the prologue); Maya, up on the stairs, throws
//                    you the rifle (a cutscene: KillerCutscenes.GunThrow)
//  OnGunTaken()      the rifle's Interactable: equip it, the fight starts
//  OnKillerDefeated  KillerBoss.onDefeated: choose to end him (shoot) or show mercy (Q)
//    end him:        the badge, the photo. OnKeysTaken() (the car keys' Interactable): Maya's half-confession,
//                    then the silo floods with blood. Her real secret stays hidden, and she can come with you.
//    mercy:          Elias starts to tell you who Tomas trusted - and Maya, who gave you the gun so you'd shut him
//                    up, loses it and SAYS it: she made the call to Kessler. Saying it turns her (the Damned). She
//                    drops into the silo and comes for you; Elias gets up, takes her instead, throws you the keys
//                    ("JUST GO! LEAVE!") and the blood comes up over the two of them.
public class SiloEncounter : MonoBehaviour
{
    [Header("Player")]
    public Transform player;
    public MonoBehaviour movement;
    public PlayerHealth health;
    public GunController gun;
    public ScreenFader fader;

    [Header("Silo")]
    public Transform siloCenter;            // on the floor, middle of the silo
    public float throwDistance = 2.8f;      // how far past the ledge he throws you
    public float throwLift = 1.5f;          // enough to clear a knee-high lip or railing
    public KillerBoss killer;
    public Transform killerLandPoint;
    public StabCutscene derekDeath;         // optional stab animation where he lands (unused: Derek dies in the prologue)
    public AudioSource sfx;                 // thuds
    public AudioClip thudClip;

    [Header("The rifle")]
    public Transform gunProp;               // world pickup (Interactable -> OnGunTaken), disabled at first
    public Transform gunThrowFrom;          // up by the colleagues on the top landing
    public float bossAutoStart = 8f;        // he comes for you whether you grab it or not

    [Header("After the fight")]
    public GameObject choicePanel;          // "[LMB] End it   [Q] Show mercy"
    public GameObject keysPickup;           // car keys (Interactable -> OnKeysTaken), disabled at first
    public BloodFlood flood;

    [Header("Lines ('|' splits)")]
    [TextArea] public string pushLine = "...footsteps. Right behind me.";
    [TextArea] public string leapLine = "The stairs just... stop.|No. No no no-- JUMP!";
    [TextArea] public string landLine = "Agh... my leg... I can't feel my leg...";
    [TextArea] public string derekDeathLine = "Elias: Kessler gave the order. Your brother held the knife.|Elias: I took Derek. Kessler's already paid. Now you.";
    [TextArea] public string throwLine = "Maya: HEY! Down there! Security's rifle-- CATCH!";
    [TextArea] public string gunLine = "Security's rifle. Okay. Okay.";
    [TextArea] public string defeatedLine = "He drops to his knees, blood running down his face.|Elias: Go on. Finish it. You people are good at that.";
    [TextArea] public string killLine = "...|There's a visitor badge in his coat. ELIAS ROURKE.|Clipped to it, a photo of the night cleaner. TOMAS ROURKE. 'My little brother.'";
    [TextArea] public string spareLine = "Elias: ...you'd let me live? After everything?|Elias: My brother Tomas cleaned your floors for six years. Nobody knew his name.|Elias: One night he opened one of the trucks Kessler ran through Silo 2. That's all. He looked.|Elias: He was scared out of his mind. So he told one person. Someone he trusted.";

    [Header("Mercy: Maya says it out loud")]
    [TextArea] public string mayaSnapLine = "Maya: What are you DOING?! SHOOT him!|Maya: I threw you that rifle so you'd FINISH it. Not so you'd LISTEN to him!|Maya: He KNOWS. Don't you get it? Tomas came to ME that night!|Maya: He was shaking. He told me what was in that truck. And I picked up the phone and I called Kessler.|Maya: I told him his cleaner had seen everything. I told him to DEAL with it. I KNEW what that meant!";
    [TextArea] public string mayaRealiseLine = "Maya: ...no. No, no-- I didn't mean to say it out LOUD--";
    [TextArea] public string mayaTurnedLine = "She said it. She SAID it-- and it took her.|...she's coming DOWN!";
    [TextArea] public string eliasSaveLine = "Elias: JUST GO! LEAVE!";
    [TextArea] public string eliasKeysLine = "Elias: Your keys-- TAKE them! She made the call. She's MINE!|My keys. He's had them the whole time.";
    [TextArea] public string twistFloodLine = "The grates-- that's BLOOD. It's coming up fast!";
    [TextArea] public string keysLine = "My car keys. He had them the whole time.";
    [TextArea] public string confessionLine = "Maya: It's done. Good. He can't tell anyone anything now.|Maya: The morgue trucks, what Kessler sold... we all took the bonuses. We all signed his report.|Maya: I'm sorry about Derek. He hated himself for it, every day after.|...I signed it without even reading it.|That's why he came for us. For Derek. For me.";
    [TextArea] public string floodLine = "The grates... that's blood. It's filling up.|SWIM!";

    private bool pushed, gunTaken, choiceMade, keysTaken;
    private int pendingChoice = -1;         // from Choose(): 1 = end it, 0 = mercy

    // the kill/mercy choice without the mouse or Q (gamepad, UI, tests)
    public void Choose(bool kill) => pendingChoice = kill ? 1 : 0;

    private KillerCutscenes cuts;
    private Survivor maya;

    // Maya and Sam have been up on the stairs the whole time: from the moment she shouts down, you can see them
    void ShowColleagues()
    {
        if (cuts == null) cuts = FindFirstObjectByType<KillerCutscenes>();
        foreach (var sv in FindObjectsByType<Survivor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (sv.Turned || sv.Drowned || Flat(sv.transform.position - siloCenter.position).magnitude > 12f) continue;
            sv.gameObject.SetActive(true);
            if (sv.displayName == "Maya") maya = sv;
        }
    }

    static IEnumerator Talk(float max)
    {
        yield return null;
        for (float t = 0f; t < max && DialogueBox.Instance != null && DialogueBox.Instance.Busy; t += Time.deltaTime) yield return null;
    }

    void Start()
    {
        thudClip = Sounds.Clip("Silo Fight/Push and fall thud", thudClip != null ? thudClip : ProceduralAudio.Thud());
        if (gunProp != null) gunProp.gameObject.SetActive(false);
        if (keysPickup != null) keysPickup.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(false);
        if (killer != null) killer.gameObject.SetActive(false);
    }

    public UnityEngine.Events.UnityEvent onLanded = new UnityEngine.Events.UnityEvent();   // e.g. the silo floor checkpoint

    // a checkpoint respawn on the floor mid-fight: he stands up at full strength and comes again
    public void RespawnFight()
    {
        if (choiceMade) return;   // he was already beaten: nothing to redo
        StopAllCoroutines();
        if (choicePanel != null) choicePanel.SetActive(false);
        Vector3 at = killerLandPoint.position;
        killer.ResetBoss(at, player.position - at);
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
            // one look back: he's on the last steps, arm coming up
            Vector3 Mask() => killer.transform.position + Vector3.up * 1.62f * killer.transform.lossyScale.y;
            FaceFlat(killer.transform, player.position);
            if (killer.animator != null) killer.animator.SetFloat("Speed", 0f);
            Quaternion fromYaw = player.rotation, toYaw = Quaternion.LookRotation(Flat(killer.transform.position - player.position));
            Quaternion fromPitch = cam != null ? cam.transform.localRotation : Quaternion.identity;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.25f);
                player.rotation = Quaternion.Slerp(fromYaw, toYaw, k);
                if (cam != null) cam.transform.localRotation = Quaternion.Slerp(fromPitch, PitchTo(cam.transform, Mask()), k);
                yield return null;
            }
            Play(thudClip, 1f);
            KillerBoss.Attack(killer.animator);
            yield return Jolt(cam, 0.45f, 0.05f, Mask);
        }
        else yield return new WaitForSeconds(0.5f);

        // turn to the drop... and go
        Vector3 start = player.position;
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

        // steer the fall so you land in the middle of the silo floor, watching it come up
        float drop = Mathf.Max(0.5f, player.position.y - 1f - siloCenter.position.y);
        float fallTime = Mathf.Sqrt(2f * drop / -Physics.gravity.y);
        Vector3 across = Flat(siloCenter.position - player.position) * 0.85f;
        rb.linearVelocity = new Vector3(across.x / fallTime, rb.linearVelocity.y, across.z / fallTime);
        Quaternion fall = Quaternion.Euler(78f, 0f, 0f);
        for (float t = 0f; t < 10f; t += Time.deltaTime)
        {
            if (cam != null) cam.transform.localRotation = Quaternion.Slerp(cam.transform.localRotation, fall, 4f * Time.deltaTime);
            if (t > 0.3f && player.position.y < siloCenter.position.y + 2.5f && rb.linearVelocity.y > -0.5f) break;
            yield return null;
        }
        yield return Landed(cam);
    }

    IEnumerator PushRoutine()
    {
        var rb = player.GetComponent<Rigidbody>();
        var cam = player.GetComponentInChildren<Camera>();
        var chase = FindFirstObjectByType<ChaseKiller>();
        if (chase != null) chase.Stop(false);
        movement.enabled = false;
        rb.linearVelocity = Vector3.zero;

        // he's right behind you, feet on the ledge (not sunk into it)
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

        // heavy footsteps closing in, then your own voice
        Play(thudClip, 0.35f);
        yield return new WaitForSeconds(0.35f);
        Play(thudClip, 0.45f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(pushLine);
        yield return new WaitForSeconds(0.7f);

        // whip around - body AND head - straight into the mask
        Vector3 Mask() => killer.transform.position + Vector3.up * 1.62f * killer.transform.lossyScale.y;
        Quaternion fromYaw = player.rotation;
        Quaternion toYaw = Quaternion.LookRotation(Flat(killer.transform.position - player.position));
        Quaternion fromPitch = cam != null ? cam.transform.localRotation : Quaternion.identity;
        for (float t = 0f; t < 0.28f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.28f);
            player.rotation = Quaternion.Slerp(fromYaw, toYaw, k);
            if (cam != null) cam.transform.localRotation = Quaternion.Slerp(fromPitch, PitchTo(cam.transform, Mask()), k);
            yield return null;
        }
        player.rotation = toYaw;

        // stinger + jolt: he's already swinging
        Play(thudClip, 1f);
        KillerBoss.Attack(killer.animator);
        yield return Jolt(cam, 0.42f, 0.05f, Mask);

        // the shove lands: thrown backwards off the ledge, still staring up at him
        Play(thudClip, 0.9f);
        Vector3 start = player.position;
        Vector3 end = start + Flat(siloCenter.position - start).normalized * throwDistance + Vector3.up * throwLift;
        rb.isKinematic = true;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            float k = t / 0.4f;
            player.position = Vector3.Lerp(start, end, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.5f;
            if (cam != null) cam.transform.localRotation = PitchTo(cam.transform, Mask());
            yield return null;
        }
        player.position = end;
        rb.isKinematic = false;

        // however high the ledge, steer the fall so you land in the middle of the silo floor
        float drop = Mathf.Max(0.5f, player.position.y - 1f - siloCenter.position.y);
        float fallTime = Mathf.Sqrt(2f * drop / -Physics.gravity.y);
        Vector3 across = Flat(siloCenter.position - player.position) * 0.85f;
        rb.linearVelocity = new Vector3(across.x / fallTime, rb.linearVelocity.y, across.z / fallTime);

        // falling: he stands at the edge looking down at you, shrinking away
        FaceFlat(killer.transform, siloCenter.position);
        for (float t = 0f; t < 10f; t += Time.deltaTime)
        {
            if (cam != null) cam.transform.localRotation = Quaternion.Slerp(cam.transform.localRotation, PitchTo(cam.transform, Mask()), 10f * Time.deltaTime);
            if (t > 0.3f && player.position.y < siloCenter.position.y + 2.5f && rb.linearVelocity.y > -0.5f) break;
            yield return null;
        }

        yield return Landed(cam);
    }

    // the silo floor: the fall doesn't kill you, but it takes half of what you had left; then he comes down after you
    IEnumerator Landed(Camera cam)
    {
        Play(thudClip, 1f);
        health.TakeDamage(Mathf.Min(health.currentHealth * 0.5f, health.currentHealth - 1f));
        if (fader != null) yield return fader.FadeTo(1f, 0.1f);
        killer.gameObject.SetActive(false);   // off the ledge while it's black: he's coming down after you
        if (cam != null) cam.transform.localRotation = Quaternion.identity;
        if (fader != null) { yield return new WaitForSeconds(1.2f); StartCoroutine(fader.FadeTo(0f, 1.5f)); }
        Say(landLine);
        MusicManager.Area("Silo Fight");
        onLanded.Invoke();
        yield return new WaitForSeconds(1f);
        movement.enabled = true;

        yield return new WaitForSeconds(2f);
        StartCoroutine(KillerDropsIn());
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

    IEnumerator KillerDropsIn()
    {
        Play(thudClip, 1f);
        if (derekDeath != null)
        {
            // he lands on Derek. The rig's killer hands over to the real boss while the screen is black.
            movement.enabled = false;
            yield return derekDeath.Run(() =>
            {
                killer.transform.position = new Vector3(derekDeath.HiddenSpot.x, siloCenter.position.y, derekDeath.HiddenSpot.z);
                FaceFlat(killer.transform, player.position);
                killer.gameObject.SetActive(true);
            });
            movement.enabled = true;
            Say(derekDeathLine);
            yield return new WaitForSeconds(4.5f);
        }
        else
        {
            killer.transform.position = killerLandPoint.position;
            FaceFlat(killer.transform, player.position);
            killer.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.8f);
            Say(derekDeathLine);
            yield return new WaitForSeconds(4f);
        }

        // the rifle comes down from the stairs and lands in front of you
        ShowColleagues();
        Vector3 start = gunThrowFrom.position;
        Vector3 end = player.position + Flat(player.forward).normalized * 1.6f;
        end.y = siloCenter.position.y + 0.15f;
        if (maya != null && cuts != null && !cuts.Playing)
            yield return cuts.StartCoroutine(cuts.GunThrow(maya.transform, maya.animator, gunProp, end, siloCenter, throwLine));
        else
        {
            Say(throwLine);
            yield return new WaitForSeconds(1.5f);
            gunProp.gameObject.SetActive(true);
            const float flight = 1.4f;
            for (float t = 0f; t < flight; t += Time.deltaTime)
            {
                float k = t / flight;
                gunProp.position = Vector3.Lerp(start, end, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 4f;
                gunProp.Rotate(0f, 0f, 540f * Time.deltaTime, Space.Self);
                yield return null;
            }
        }
        gunProp.gameObject.SetActive(true);
        gunProp.SetPositionAndRotation(end, Quaternion.Euler(0f, Random.Range(0f, 360f), 90f));
        Play(thudClip, 0.4f);

        yield return new WaitForSeconds(bossAutoStart);
        killer.Activate();
    }

    public void OnGunTaken()
    {
        if (gunTaken) return;
        gunTaken = true;
        gun.Equip();
        Say(gunLine);
        StartCoroutine(StartFightSoon());
    }

    IEnumerator StartFightSoon()
    {
        yield return new WaitForSeconds(1f);
        killer.Activate();
    }

    public void OnKillerDefeated() => StartCoroutine(Choice());

    IEnumerator Choice()
    {
        yield return new WaitForSeconds(1.2f);
        Say(defeatedLine);
        yield return new WaitForSeconds(2.5f);
        var cuts = FindFirstObjectByType<KillerCutscenes>();
        while (cuts != null && cuts.Playing) yield return null;   // the orbit round him on his knees
        pendingChoice = -1;                                        // nothing clicked during it counts
        if (choicePanel != null) choicePanel.SetActive(true);

        bool kill = false;
        while (true)
        {
            if (Input.GetMouseButtonDown(0) || pendingChoice == 1) { kill = true; break; }   // the rifle fires by itself
            if (Input.GetKeyDown(KeyCode.Q) || pendingChoice == 0) break;
            yield return null;
        }
        choiceMade = true;
        if (choicePanel != null) choicePanel.SetActive(false);

        if (kill)
        {
            Judgement.KilledKiller = true;
            killer.FallDead();
            yield return new WaitForSeconds(1f);
            Say(killLine);
        }
        else
        {
            Judgement.SparedKiller = true;
            Say(spareLine);
            ShowColleagues();
            if (maya != null && cuts != null && flood != null && flood.risenDamned != null)
            {
                yield return Talk(40f);
                yield return MercyTwist();
                yield break;
            }
        }

        yield return new WaitForSeconds(4f);
        keysPickup.transform.position = killer.transform.position + Flat(player.position - killer.transform.position).normalized * 1f + Vector3.up * 0.1f;
        keysPickup.SetActive(true);
    }

    // he never gets to say her name: she says it for him
    IEnumerator MercyTwist()
    {
        while (cuts.Playing) yield return null;
        yield return cuts.StartCoroutine(cuts.MayaTurns(maya, flood.risenDamned, killer, siloCenter,
            mayaSnapLine, mayaRealiseLine, mayaTurnedLine, eliasSaveLine, () => flood.SurfaceY));
        yield return Talk(6f);

        // the keys, thrown over her shoulder
        keysTaken = true;
        Inventory.Add("CarKeys");
        Play(thudClip, 0.3f);
        Say(eliasKeysLine);
        yield return Talk(12f);

        // and the silo starts to fill, with the two of them still locked together on the floor
        Say(twistFloodLine);
        yield return new WaitForSeconds(2.4f);
        flood.Begin();
    }

    public void OnKeysTaken()
    {
        if (keysTaken || !choiceMade) return;
        keysTaken = true;
        StartCoroutine(Flood());
    }

    IEnumerator Flood()
    {
        Say(keysLine + "|" + confessionLine);
        yield return new WaitForSeconds(14f);
        Say(floodLine);   // queued: it must not cut off the confession
        flood.Begin();
    }

    void Play(AudioClip c, float vol) { if (sfx != null && c != null) sfx.PlayOneShot(c, vol); }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    static void FaceFlat(Transform t, Vector3 target) { t.rotation = Quaternion.LookRotation(Flat(target - t.position)); }
    static void Say(string s) { if (!string.IsNullOrEmpty(s) && DialogueBox.Instance != null) DialogueBox.Instance.Say(s); }
}
