using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// The office act, start to finish (2026-11-03 rewrite, the user's script). One beat after another:
//  1 dark office, find a power source            5 Vane rushes you, chokes you; tear free, flash him away
//  2 your hands burn and light comes out (Q)     6 the Lost walk the office now (Murder / Traitor / Selfish); find the
//  3 cold eyes, a blink: the Echo (V)              fuses while Vane hunts, put them in during the Echo: power
//  4 fuse box: CCTV still here, fuses gone;      7 the CCTV wants Vane's card: crowbar (cleaner's closet) on his door
//    in the Echo you see your colleagues' souls  8 tapes: Mercer kills Tomas / Mercer at Kessler's keypad (1304) /
//                                                  Kessler + Mercer on the silo stairs / Derek takes the file
//  9 Vane behind you: his confession, he turns  10 1304 opens Kessler's office, crowbar the armory, middle locker
//    (Cry Wolf form) and hunts you in a rage       (dial = the stairs tape's timestamp), pistol slows him, silo keys
// 11 tell every Lost the truth: they walk to the bridge. Then the silo door opens.
// Progress lives in Truth ("Story.*") so checkpoint retries resume at the right beat.
public sealed class OfficeV52Sequence : MonoBehaviour, ISceneProgress
{
    public static OfficeV52Sequence Instance { get; private set; }
    [Header("Existing systems")]
    public CctvDesk cctv;
    public KesslerConfrontation legacyEncounter;   // its damned body is Vane's rage form
    public KesslerDoor officeDoor;                  // Kessler's office door (keypad 1304)
    public KesslerDoor vaneDoor;                    // Vane's office door (crowbar)
    public Keypad keypad;
    public Interactable concealedDoor, pistol, keys;   // armory door (crowbar), locker contents
    public VaultDial locker;
    public Teleporter silo;
    public Checkpoint armoryCheckpoint;
    public Transform player;
    public ScreenFader fader;
    public GameObject lockerContents, lockerLid;
    [Header("Where the Lost stand (cleaner's closet, reception, boardroom x2)")]
    public Vector3 omarAt = new Vector3(1007.6f, 3.4f, 906.2f);
    public Vector3 priyaAt = new Vector3(983.5f, 3.4f, 879.5f);
    public Vector3 nadiaAt = new Vector3(956.2f, 3.4f, 894f);
    public Vector3 benAt = new Vector3(957.8f, 3.4f, 891f);
    [Header("Beats")]
    public float awakenAfter = 14f;                 // seconds in the dark (or 10 m walked) before the hands burn
    public float prySeconds = 2.2f;
    [Header("Office bullet reactions (no damage)")]
    public float staggerDuration = 1.6f;
    public float staggerCooldown = 3.5f;
    public bool palmFlashUsable = true;
    public UnityEvent onTransformation = new UnityEvent();

    public bool InSequence { get; private set; }
    public bool AwaitingFlash { get; private set; }
    public bool ShowingHands { get; private set; }
    public static bool Has(string beat) => Truth.Has("Story." + beat);
    static void Set(string beat) => Truth.Set("Story." + beat);
    public bool HandsAwake => Has("Hands");
    public bool EchoAwake => Has("Echo");
    public bool HuntActive => Has("Choked") && !Completed;
    public bool LostAwake => Has("Choked");
    public bool CanFree => Has("Keys") && Has("Pistol");
    public bool Powered => Has("Power");
    public bool Completed => Has("Done");
    public bool BlocksRealmSwitch => InSequence || !EchoAwake;
    public bool TimerRunning => false;
    public bool PursueDuringPuzzle => Has("Transformed") && !Completed && !InSequence && !PauseMenu.Paused && !GameOverScreen.Open
        && (VaultDial.Active || NoteViewer.Blocking || (ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open));
    public bool ClosePuzzleForThreat()
    {
        bool open = VaultDial.Active || NoteViewer.Blocking || (ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open);
        if (!open) return false;
        locker?.Cancel();
        if (NoteViewer.IsOpen) NoteViewer.Close();
        ChoiceDialogue.Instance?.Close();
        return true;
    }

    public int FreedCount { get { int n = 0; foreach (var c in Truth.Colleagues) if (Truth.Has("Freed" + c)) n++; return n; } }
    public string Objective => Completed ? null
        : !Has("FuseBox") ? "Find a power source to light up the office"
        : !Has("Power") ? "Find the three missing fuses - install them in the Echo"
        : !Inventory.Has("SecurityPass") ? (Inventory.Has("Crowbar") || Has("VaneDoor") ? "Get the security card from Vane's office" : "Find something to pry open Vane's office - the cleaner's closet")
        : !Has("Tapes") ? "Watch the CCTV footage - security room"
        : !Has("KesslerCode") ? "Get into Kessler's office - code 1304"
        : !Has("Armory") ? "Pry open the armory"
        : !CanFree ? "Open the middle locker"
        : !Truth.AllFreed ? "Tell the Lost what really happened (" + FreedCount + "/4)"
        : "Escape to Silo Two";

    VaneHunt Hunt => VaneHunt.Instance;
    FirstPersonController fpc;
    PalmLight palm;
    readonly List<Behaviour> held = new List<Behaviour>();
    bool initialized, bulletLine, freedLine;
    float darkTime; Vector3 darkFrom;
    FuseBox breaker; Transform breakerUse;
    Interactable vaneDoorUse;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; Unlock(); }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        fpc = player.GetComponent<FirstPersonController>();
        palm = player.GetComponentInChildren<PalmLight>(true);
        player.gameObject.GetOrAdd<Stamina>();
        breaker = FindFirstObjectByType<FuseBox>(FindObjectsInactive.Include);
        if (breaker != null)
        {
            foreach (var use in breaker.GetComponentsInChildren<Interactable>(true)) if (use.requiredItem == "Fuse") breakerUse = use.transform;
            if (breakerUse == null) foreach (var use in RevisedPlot.All<Interactable>(gameObject.scene)) if (use.requiredItem == "Fuse") breakerUse = use.transform;
            if (breaker.counter != null) breaker.counter.onProgress += n => { if (n >= breaker.counter.required) PowerOn(); };
        }
        if (locker != null) locker.onSolved.AddListener(UnlockLocker);
        if (pistol != null) pistol.onUse.AddListener(() => { Set("Pistol"); EvidenceJournal.Record("Kessler's Beretta", "Kessler's personal pistol. Mercer used it on Vane, Derek and me."); AfterLocker(); });
        if (keys != null) keys.onUse.AddListener(() => { Set("Keys"); AfterLocker(); });
        if (silo != null) silo.onArrive.AddListener(Complete);
        if (keypad != null) keypad.onSolved.AddListener(() => { Set("KesslerCode"); if (officeDoor != null) officeDoor.Unlock(); });
        if (vaneDoor != null && vaneDoor.lockedPrompt != null)
        {
            vaneDoorUse = vaneDoor.lockedPrompt;
            vaneDoorUse.requiredItem = ""; vaneDoorUse.line = ""; vaneDoorUse.once = false;
            vaneDoorUse.onUse.AddListener(UseVaneDoor);
        }
        foreach (var cp in RevisedPlot.All<Checkpoint>(gameObject.scene))
            if (cp.name == "Checkpoint - Office entrance" || cp == armoryCheckpoint) cp.onRespawn.AddListener(Retry);
        // OfficeRealmPuzzles makes every pad and door Echo-only: these three work in the burnt office
        if (keypad != null) { keypad.echoOnly = false; keypad.ApplyRealm(false); }
        foreach (var d in new[] { officeDoor, vaneDoor }) if (d != null) { d.echoOnly = false; d.ApplyRealm(false); }
        PlaceLost();
        ApplyPersistentState();
        darkFrom = player.position;
    }

    public object CaptureProgress() => null;
    public void RestoreProgress(object state) => ApplyPersistentState();

    void ApplyPersistentState()
    {
        if (Completed) Hunt?.EndOfficeSequence();
        else if (Has("Transformed")) Hunt?.UseOfficeForm(legacyEncounter != null ? legacyEncounter.damned : null);
        SetArmoryOpen(Has("Armory"));
        bool unlocked = Has("Locker");
        if (locker != null) { locker.SetSolved(unlocked); locker.gameObject.SetActive(!unlocked); }
        if (lockerContents != null) lockerContents.SetActive(unlocked);
        if (lockerLid != null) lockerLid.SetActive(!unlocked);
        if (pistol != null && Has("Pistol")) pistol.gameObject.SetActive(false);
        if (keys != null) keys.gameObject.SetActive(unlocked && !Inventory.Has("SiloKey"));
        if (Has("Power") && keypad != null) keypad.SetPowered(true);
        if (Has("VaneDoor") && vaneDoor != null) vaneDoor.ReleaseForEscape();
        if (Has("KesslerCode") && officeDoor != null) officeDoor.ReleaseForEscape();
        if (CanFree) ReleaseEchoDoors();
    }

    // the four souls where the user wants them: Omar in the cleaner's closet, Priya by reception, Nadia and Ben in the boardroom
    void PlaceLost()
    {
        foreach (var g in RevisedPlot.All<GhostNPC>(gameObject.scene))
        {
            Vector3 at;
            switch (g.ghostName) { case "Omar": at = omarAt; break; case "Priya": at = priyaAt; break; case "Nadia": at = nadiaAt; break; case "Ben": at = benAt; break; default: continue; }
            at = ClearFloor(at);
            Vector3 face = Flat(new Vector3(982f, at.y, 885f) - at);
            g.transform.SetPositionAndRotation(at, face.sqrMagnitude > .01f ? Quaternion.LookRotation(face) : g.transform.rotation);
            g.working = false; g.driftRadius = 0f;
            var walk = g.GetComponent<PainWalk>(); if (walk != null) walk.roam = 0f;
        }
    }

    static Vector3 ClearFloor(Vector3 want)
    {
        for (int ring = 0; ring <= 8; ring++)
            for (int i = 0; i < (ring == 0 ? 1 : 12); i++)
            {
                Vector3 p = want + Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward * (ring * .35f);
                if (!Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out var floor, 2.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(floor.point.y - want.y) > .25f) continue;   // a desk top, not the floor
                p.y = floor.point.y;
                if (Physics.CheckCapsule(p + Vector3.up * .4f, p + Vector3.up * 1.6f, .3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                return p;
            }
        return want;
    }

    // ---------------------------------------------------------------- per frame

    bool InOffice => player != null && OfficeHorrorDirector.In(player.position);
    bool Free => !InSequence && !PauseMenu.Paused && !GameOverScreen.Open && !TitleScreen.Showing && !NoteViewer.Blocking
        && !GhostNPC.Conversing && !VaultDial.Active && !VaneHunt.Seizing && !(cctv != null && cctv.Rolling)
        && (DialogueBox.Instance == null || (!DialogueBox.Instance.Busy && !DialogueBox.Instance.CardOpen))
        && (ChoiceDialogue.Instance == null || !ChoiceDialogue.Instance.Open);
    bool Echo => RevisedPlotDirector.Instance != null && RevisedPlotDirector.Instance.Echo;

    void Update()
    {
        if (!initialized || Completed || !InOffice || GameOverScreen.Open) return;
        if (!Has("Armory") && concealedDoor != null)
            concealedDoor.prompt = Inventory.Has("Crowbar") ? "Pry open the armory" : "Armory - locked";
        if (vaneDoorUse != null && !Has("VaneDoor"))
            vaneDoorUse.prompt = Inventory.Has("Crowbar") ? "Pry open Vane's office" : "Vane's office - jammed";
        if (!Free) return;

        if (!Has("Arrived")) { StartCoroutine(Arrival()); return; }
        if (!HandsAwake)
        {
            darkTime += Time.deltaTime;
            if (darkTime > awakenAfter || Flat(player.position - darkFrom).magnitude > 10f) StartCoroutine(Awakening());
            return;
        }
        if (!EchoAwake) { StartCoroutine(Eyes()); return; }
        if (!Has("FuseBox") && breakerUse != null && Vector3.Distance(player.position, breakerUse.position) < 4.5f) { FuseBoxSeen(); return; }
        if (Has("FuseBox") && !Has("Presence") && Echo) { StartCoroutine(Presence()); return; }
        if (Has("Presence") && !Has("Choked") && !Echo && Hunt != null && Hunt.OpeningRouteReady && Time.time > rushRetry) { StartCoroutine(VaneRush()); return; }
        if (Has("Tapes") && !Has("Transformed") && !Echo) { StartCoroutine(VaneBehind()); return; }
        if (CanFree && Truth.AllFreed && !freedLine) { freedLine = true; Say("A3V6_all_freed"); }
    }

    // ---------------------------------------------------------------- 1-3: the dark, the hands, the eyes

    IEnumerator Arrival()
    {
        Set("Arrived");
        yield return new WaitForSeconds(1.5f);
        yield return Speak("A3V6_dark");
    }

    IEnumerator Awakening()
    {
        InSequence = true; Lock();
        palm = palm != null ? palm : player.GetComponentInChildren<PalmLight>(true);
        var cam = View;
        var burn = BurnOverlay();
        var src = player.gameObject.AddComponent<AudioSource>(); src.spatialBlend = 0f;
        src.PlayOneShot(ProceduralAudio.Rumble(2.5f), .9f);
        // look down at the hands as they burn: the marks surge, the view trembles, the edges go red
        float pitch0 = cam.transform.localEulerAngles.x; if (pitch0 > 180f) pitch0 -= 360f;
        ShowingHands = true; Set("Hands");
        if (palm != null) palm.Raise();
        DialogueBox.Instance?.SayNow(HS5.S("A3V6_hands_burn"));
        for (float t = 0f; t < 3.2f; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / 1.2f);
            float pitch = Mathf.Lerp(pitch0, 38f, Mathf.SmoothStep(0f, 1f, k));
            cam.transform.localRotation = Quaternion.Euler(pitch + Random.Range(-1f, 1f) * k, Random.Range(-1f, 1f) * k, 0f);
            if (palm != null) palm.Surge = 1f;
            burn.color = new Color(.7f, .05f, .02f, .35f * k * (.7f + .3f * Mathf.Sin(t * 18f)));
            yield return null;
        }
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) { if (palm != null) palm.Surge = 1f; yield return null; }
        // ...and the light comes out of them
        src.PlayOneShot(ProceduralAudio.Thud(), 1f);
        for (float t = 0f; t < .5f; t += Time.deltaTime) { burn.color = new Color(1f, .93f, .8f, .8f * (1f - t / .5f)); yield return null; }
        burn.color = Color.clear;
        yield return Speak("A3V6_hands_light");
        for (float t = 0f; t < .8f; t += Time.deltaTime)
        { cam.transform.localRotation = Quaternion.Slerp(cam.transform.localRotation, Quaternion.Euler(5f, 0f, 0f), 4f * Time.deltaTime); yield return null; }
        fpc.SetPitch(5f);
        ShowingHands = false;
        Destroy(burn.canvas.gameObject); Destroy(src, 3f);
        Unlock(); InSequence = false;
        DialogueBox.Instance?.ShowCard("YOUR HANDS",
            "<color=#C70D0A>[Q]</color> raise / lower your hands: the light\n" +
            "<color=#C70D0A>[LEFT CLICK]</color> with them up: a flash that blinds\n" +
            "Holding the light and running both use your <color=#C70D0A>STAMINA</color>");
    }

    IEnumerator Eyes()
    {
        InSequence = true;
        yield return new WaitForSeconds(2.5f);
        yield return Speak("A3V6_eyes_cold");
        // a blink, and the Echo is there
        var black = BurnOverlay(); black.color = Color.black;
        yield return new WaitForSeconds(.25f);
        Set("Echo");
        RevisedPlotDirector.Instance?.SetEcho(true);
        for (float t = 0f; t < .6f; t += Time.deltaTime) { black.color = new Color(0f, 0f, 0f, 1f - t / .6f); yield return null; }
        Destroy(black.canvas.gameObject);
        InSequence = false;
        yield return Speak("A3V6_echo_first");
        DialogueBox.Instance?.ShowCard("THE ECHO",
            "<color=#C70D0A>[V]</color> see the office as it was, before the fire\n" +
            "Burnt things work again in the Echo. It only lasts a few seconds");
    }

    void FuseBoxSeen()
    {
        Set("FuseBox");
        EvidenceJournal.Record("Empty fuse box", "Three fuses missing from the box in the security room. The CCTV desk is still there.");
        Say("A3V6_fusebox");
    }

    IEnumerator Presence()
    {
        Set("Presence");
        yield return Speak("A3V6_presence");
        yield return Speak("A3V6_bodies");
    }

    // ---------------------------------------------------------------- 5: Vane

    IEnumerator VaneRush()
    {
        InSequence = true; Lock();
        if (!Has("Rush")) DialogueBox.Instance?.SayNow(HS5.S("A3V6_vane_rush"));
        Set("Rush");
        lockLook = true;
        yield return Hunt.OpeningApproach();
        lockLook = false;
        if (!Hunt.OpeningApproached) { rushRetry = Time.time + 3f; Hunt.ReleaseSequence(false); Unlock(); InSequence = false; yield break; }   // no clear line here: next frame tries again
        FacePoint(Hunt.Actor.position + Vector3.up * 1.55f);
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) { FacePoint(Hunt.Actor.position + Vector3.up * 1.55f); yield return null; }
        Unlock();   // (the choke takes the controls itself)
        DialogueBox.Instance?.SayNow(HS5.S("A3V6_vane_choke"));
        yield return Hunt.ScriptedChoke();
        if (!Hunt.ChokeFlashed)
        {
            // torn free: he staggers back. Now the light
            lockLook = true;
            AwaitingFlash = true;
            if (palm != null) palm.PrepareOpeningFlash();
            Hint("FLASH HIM\n[Q] raise your hand, aim at Vane, [LEFT CLICK]");
            while (AwaitingFlash) yield return null;
            lockLook = false; hint = null;
        }
        Hunt.SendAway(20f);   // blinded, he is gone; then he hunts
        Set("Choked");
        InSequence = false;
        yield return new WaitForSeconds(1f);
        yield return Speak("A3V6_after_choke");
    }
    bool lockLook;
    float rushRetry;
    public void VaneFlashed() { AwaitingFlash = false; }

    // ---------------------------------------------------------------- 6-7: power, Vane's door

    void PowerOn()
    {
        if (Has("Power")) return;
        Set("Power");
        Truth.Set("OfficeOpening.Powered");
        if (keypad != null) keypad.SetPowered(true);
        Say(HS5.S("A3V6_power_on") + "|" + HS5.S("A3V6_need_card"));
    }

    void UseVaneDoor()
    {
        if (Has("VaneDoor") || InSequence) return;
        if (!Inventory.Has("Crowbar")) { Say("A3V6_vane_door"); return; }
        StartCoroutine(Pry(vaneDoor.transform, () => { Set("VaneDoor"); vaneDoor.ReleaseForEscape(); }));
    }

    // ---------------------------------------------------------------- 8-9: the tapes, then Vane behind you

    public void RecordingFinished()
    {
        if (Has("Tapes")) return;
        Set("Tapes");
        Truth.Set("SawFireTape"); Truth.Set("SawTomasTape");
        EvidenceJournal.Record("The CCTV footage", "Mercer killed Tomas in the silo. Kessler told him to burn the office and make sure no one saw the footage. Derek saw it and took the file. That is why Mercer killed him, and me.");
    }

    IEnumerator VaneBehind()
    {
        InSequence = true; Lock();
        yield return new WaitForSeconds(1.2f);
        // he stands right behind you; you turn round into him
        var spot = new GameObject("Vane behind you").transform;
        Vector3 back = Flat(-View.transform.forward).normalized;
        spot.position = player.position + back * 1.3f;
        spot.rotation = Quaternion.LookRotation(-back);
        if (!Hunt.HoldForSequence(spot)) { spot.position = player.position + back * 2.2f; if (!Hunt.HoldForSequence(spot)) Hunt.HoldForSequence(null); }
        Destroy(spot.gameObject);
        var sting = player.gameObject.AddComponent<AudioSource>(); sting.spatialBlend = 0f;
        sting.PlayOneShot(Sounds.Clip("Office/Vane jumpscare", null) ?? ProceduralAudio.Rumble(1.2f), 1f);
        Vector3 face = Hunt.Actor.position + Vector3.up * 1.6f;
        for (float t = 0f; t < .35f; t += Time.deltaTime) { FaceToward(face, 14f); yield return null; }
        FacePoint(face);
        Destroy(sting, 3f);
        yield return Speak("A3V6_vane_confess");
        // he loses himself: the shake, a black cut, the other body
        onTransformation.Invoke();
        var black = BurnOverlay();
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            View.transform.localRotation *= Quaternion.Euler(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f), 0f);
            black.color = new Color(.35f, 0f, 0f, t / 1.2f * .8f);
            yield return null;
        }
        black.color = Color.black;
        Hunt.UseOfficeForm(legacyEncounter != null ? legacyEncounter.damned : null);
        Set("Transformed");
        yield return new WaitForSeconds(.3f);
        for (float t = 0f; t < .5f; t += Time.deltaTime) { black.color = new Color(0f, 0f, 0f, 1f - t / .5f); yield return null; }
        Destroy(black.canvas.gameObject);
        FacePoint(Hunt.Actor.position + Vector3.up * 1.8f);
        yield return Speak("A3V6_vane_rage");
        Hunt.ReleaseSequence(true, 2.5f);
        Unlock(); InSequence = false;
        yield return Speak("A3V6_keypad_hint");
    }

    // ---------------------------------------------------------------- 10: armory, locker, gun

    public void Pry()   // the armory door's Interactable
    {
        if (InSequence || Has("Armory")) return;
        if (!Has("KesslerCode")) { Say("Kessler's armory. I'd have to get through his office first."); return; }
        if (!Inventory.Has("Crowbar")) { Say("Sealed tight. I need a crowbar."); return; }
        StartCoroutine(Pry(concealedDoor.transform, () => { SetArmoryOpen(true); Set("Armory"); if (armoryCheckpoint != null) armoryCheckpoint.Activate(); Say("A3V6_armory"); }));
    }

    void SetArmoryOpen(bool open)
    {
        if (concealedDoor == null || !open) return;
        concealedDoor.gameObject.SetActive(false);
    }

    public void UnlockLocker()
    {
        Set("Locker");
        if (locker != null) locker.gameObject.SetActive(false);
        if (lockerLid != null) lockerLid.SetActive(false);
        if (lockerContents != null) lockerContents.SetActive(true);
        if (keys != null && !Inventory.Has("SiloKey")) keys.gameObject.SetActive(true);
    }
    public void DiscoverCombination() { }

    void AfterLocker()
    {
        if (!CanFree) return;
        Say(HS5.S("A3V6_gun") + "|" + HS5.S("A3V6_free_hint"));
        ReleaseEchoDoors();
    }

    void ReleaseEchoDoors()
    {
        foreach (var d in RevisedPlot.All<KesslerDoor>(gameObject.scene)) if (d.echoOnly) d.ReleaseForEscape();
    }

    // a bullet: he staggers (VaneHunt.BulletHit), never hurt
    public void BulletLanded() { if (!bulletLine) { bulletLine = true; Say("A3V6_bullets"); } }

    // ---------------------------------------------------------------- 11: the Lost

    public string Bark()
    {
        var lines = new List<string>();
        foreach (var r in HS5.Rows) if (r.trigger == "A3V6_lost_bark") lines.Add(r.text);
        return lines.Count > 0 ? lines[Random.Range(0, lines.Count)] : "Murder.";
    }

    public void Complete()
    {
        if (!Inventory.Has("SiloKey") || !Has("Pistol")) return;
        Set("Done");
        Hunt?.EndOfficeSequence();
        InSequence = false; Unlock();
    }

    public void Retry()
    {
        StopAllCoroutines(); EndPry();
        InSequence = AwaitingFlash = ShowingHands = lockLook = false; hint = null;
        Unlock(); GunController.Holster(this, false);
        Hunt?.ReleaseSequence(false, 3f);
        darkTime = 0f; darkFrom = player.position;
        ApplyPersistentState();
    }

    // the interactables run this first: the fuse box tells you what's missing before it takes anything
    public bool BeforeInteract(Interactable use)
    {
        if (InSequence) return false;
        if (breakerUse != null && use.transform == breakerUse && !Has("FuseBox")) { if (HandsAwake) FuseBoxSeen(); return false; }
        return true;
    }

    // ---------------------------------------------------------------- the crowbar

    GameObject pryTool; Light pryLight;
    IEnumerator Pry(Transform target, System.Action done)
    {
        InSequence = true; Lock();
        GunController.Holster(this, true);
        if (palm != null) palm.LowerForAction();
        pryLight = WorkLight();
        pryTool = CrowbarCopy();
        var sfx = player.gameObject.AddComponent<AudioSource>(); sfx.spatialBlend = 0f;
        Vector3 home = target.localPosition;
        Vector3 at = target.GetComponentInChildren<Collider>() != null ? target.GetComponentInChildren<Collider>().bounds.center : target.position;
        at.y = player.position.y + 1.05f;
        for (float t = 0f; t < prySeconds; t += Time.deltaTime)
        {
            if (GameOverScreen.Open) break;
            float k = t / prySeconds;
            Vector3 toward = Flat(View.transform.position - at).normalized;
            if (pryTool != null)
            {
                pryTool.transform.position = at + toward * .3f;
                pryTool.transform.rotation = Quaternion.LookRotation(toward) * Quaternion.Euler(15f + Mathf.Sin(k * Mathf.PI * 3f) * 22f, 0f, 25f);
            }
            FaceToward(at, 8f);
            target.localPosition = home + Vector3.right * Mathf.Sin(t * 28f) * .01f;
            if (Mathf.Repeat(t, .7f) < Time.deltaTime) sfx.PlayOneShot(ProceduralAudio.Creak(), .7f);
            yield return null;
        }
        target.localPosition = home;
        sfx.PlayOneShot(ProceduralAudio.Thud(), 1f);
        Destroy(sfx, 2f);
        EndPry();
        GunController.Holster(this, false);
        Unlock(); InSequence = false;
        if (!GameOverScreen.Open) done();
    }

    void EndPry()
    {
        if (pryTool != null) Destroy(pryTool);
        if (pryLight != null) Destroy(pryLight.gameObject);
    }

    GameObject CrowbarCopy()
    {
        var source = System.Array.Find(RevisedPlot.All<Interactable>(gameObject.scene), i => i.giveItem == "Crowbar");
        if (source == null) return null;
        var tool = new GameObject("Crowbar (prying)");
        var parts = new GameObject("Mesh").transform; parts.SetParent(tool.transform, false);
        Bounds b = new Bounds(); bool first = true;
        foreach (var r in source.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var v = new GameObject(r.name); v.transform.SetParent(parts, false);
            v.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation); v.transform.localScale = r.transform.lossyScale;
            v.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh; v.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        parts.position -= b.center;
        float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (size > .001f) tool.transform.localScale = Vector3.one * (.85f / size);
        return tool;
    }

    Light WorkLight()
    {
        var l = new GameObject("Crowbar work light").AddComponent<Light>();
        l.transform.SetParent(View.transform, false); l.transform.localPosition = new Vector3(0f, .12f, .8f);
        l.type = LightType.Spot; l.spotAngle = 80f; l.color = new Color(1f, .85f, .65f); l.range = 7f; l.intensity = 4f; l.shadows = LightShadows.None;
        return l;
    }

    // ---------------------------------------------------------------- helpers

    Camera View => fpc.playerCamera;
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    void FacePoint(Vector3 point)
    {
        Vector3 d = point - View.transform.position;
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        player.rotation = Quaternion.Euler(0f, yaw, 0f);
        View.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        fpc.SetPitch(pitch);
    }
    void FaceToward(Vector3 point, float rate)
    {
        var want = Quaternion.LookRotation(point - View.transform.position);
        Vector3 e = Quaternion.Slerp(View.transform.rotation, want, rate * Time.deltaTime).eulerAngles;
        player.rotation = Quaternion.Euler(0f, e.y, 0f);
        float pitch = e.x > 180f ? e.x - 360f : e.x;
        View.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        fpc.SetPitch(pitch);
    }

    void LateUpdate()
    {
        // waiting for the flash: the view is pulled towards him (he is staggering a pace away)
        if (lockLook && Hunt != null && Hunt.Actor != null && Hunt.Actor.gameObject.activeInHierarchy && !PauseMenu.Paused) FaceToward(Hunt.Actor.position + Vector3.up * 1.5f, AwaitingFlash ? 2f : 6f);
    }

    UnityEngine.UI.Image BurnOverlay()
    {
        var canvas = new GameObject("Office story overlay").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 410;
        var img = new GameObject("Overlay", typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
        img.transform.SetParent(canvas.transform, false);
        var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        img.color = Color.clear; img.raycastTarget = false;
        return img;
    }

    static void Say(string triggerOrText)
    {
        if (DialogueBox.Instance == null) return;
        DialogueBox.Instance.Say(triggerOrText.StartsWith("A3V6_") && !triggerOrText.Contains("|") ? HS5.S(triggerOrText) : triggerOrText);
    }
    static IEnumerator Speak(string trigger)
    {
        Say(trigger);
        yield return null;
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;
    }

    string hint;
    void Hint(string text) => hint = text;
    void OnGUI()
    {
        if (hint == null || PauseMenu.Paused || GameOverScreen.Open) return;
        var style = new GUIStyle(GUI.skin.box) { font = GameFont.Ttf, fontSize = 24, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        style.normal.textColor = new Color(.95f, .93f, .86f);
        float w = Mathf.Min(Screen.width - 40f, 760f);
        GUI.Box(new Rect((Screen.width - w) / 2f, Screen.height * .7f, w, 90f), hint, style);
    }

    void Lock()
    {
        foreach (var b in player.GetComponentsInChildren<Behaviour>(true))
            if ((b is FirstPersonController || b is PlayerInteractor || b is PunchController || b is GunController) && b.enabled)
            { b.enabled = false; held.Add(b); }
        if (player.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic) rb.linearVelocity = Vector3.zero;
    }
    void Unlock()
    {
        foreach (var b in held) if (b != null) b.enabled = true;
        held.Clear();
    }
    void OnDisable() { StopAllCoroutines(); EndPry(); Unlock(); GunController.Holster(this, false); }
}
