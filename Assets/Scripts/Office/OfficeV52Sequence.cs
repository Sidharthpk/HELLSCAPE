using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Coordinates authored office story beats. Patrol, realm switching, inventory and combat remain owned by
// their original components. Truth is the run's existing persistent store (including checkpoint retries).
public sealed class OfficeV52Sequence : MonoBehaviour, ISceneProgress
{
    public static OfficeV52Sequence Instance { get; private set; }
    [Header("Existing systems")]
    public CctvDesk cctv;
    public KesslerConfrontation legacyEncounter;
    public KesslerDoor officeDoor;
    public Interactable concealedDoor, pistol, keys;
    public VaultDial locker;
    public Teleporter silo;
    public Checkpoint armoryCheckpoint;
    public Transform player;
    public ScreenFader fader;

    [Header("Authored encounter positions (feet on clear floor)")]
    public Transform redPose, transformationPose, breachPose;
    public BoxCollider alternateExit, confrontationArea, officeInterior;
    public Collider blockedExit;
    public Light redBacklight;
    public GameObject lockerContents;
    public GameObject lockerLid;
    [Header("Reuse existing actions; optional asset hooks")]
    public Animator pryAnimator;
    public string pryState = "Pry";
    public float prySeconds = 2f;
    public UnityEvent onPryStarted = new UnityEvent();
    public UnityEvent onPryCompleted = new UnityEvent();
    public AnimationClip transformationClip;
    public float transformationSeconds = 2.5f;
    public UnityEvent onTransformation = new UnityEvent();
    public UnityEvent onImpact = new UnityEvent();
    public UnityEvent onFrameDamage = new UnityEvent();
    public UnityEvent onPartialBreach = new UnityEvent();
    public UnityEvent onFinalWarning = new UnityEvent();
    public UnityEvent onBreach = new UnityEvent();
    [Header("Door breach")]
    public float breachSeconds = 60f;
    public AudioSource doorAudio;
    public AudioClip impactClip;
    [Header("Office bullet reactions (no permanent damage)")]
    public float staggerDuration = 1.6f;
    public float staggerCooldown = 3.5f;
    public bool palmFlashUsable = true;

    public OfficeOpening Opening { get; private set; }
    public bool InSequence { get; private set; }
    public bool TimerRunning { get; private set; }
    public bool Breached { get; private set; }
    public float Remaining { get; private set; }
    public bool Completed => Truth.Has("OfficeV52.Completed");
    public bool BlocksRealmSwitch => InSequence || TimerRunning || (Opening != null && Opening.Busy);
    public bool PursueDuringPuzzle => !Completed && !InSequence && !TimerRunning
        && (Breached || Truth.Has("OfficeV52.ArmorySuccess")) && !PauseMenu.Paused && !GameOverScreen.Open
        && player != null && OfficeHorrorDirector.In(player.position)
        && !(RevisedPlotDirector.Instance != null && RevisedPlotDirector.Instance.Echo)
        && (VaultDial.Active || NoteViewer.Blocking || (ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open));

    // When Vane reaches a player using the locker/journal, release that UI's own input locks before the
    // existing choke acquires them. Office pursuit must not be paused by reopening a puzzle after the breach.
    public bool ClosePuzzleForThreat()
    {
        bool open = VaultDial.Active || NoteViewer.Blocking || (ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open);
        if (!open) return false;
        locker?.Cancel();
        if (NoteViewer.IsOpen) NoteViewer.Close();
        ChoiceDialogue.Instance?.Close();
        return true;
    }
    public string Objective => Completed ? null
        : Opening != null && !Opening.FusesDiscovered ? "RESTORE THE ELECTRICITY - ELECTRICAL ROOM / MAINTENANCE"
        : Opening != null && !Opening.Cleared ? "FIND THE THREE MISSING FUSES"
        : Opening != null && !Opening.Powered ? "FIND AND INSTALL THE THREE FUSES — ELECTRICAL ROOM"
        : !Truth.Has("OfficeV52.CCTV") ? "WATCH THE SECURITY RECORDINGS"
        : !Truth.Has("OfficeV52.RedEncounter") ? "FIND ANOTHER WAY OUT"
        : !Truth.AllFreed ? "TELL THE WORKERS WHAT THE RECORDINGS REVEALED"
        : !Truth.Has("OfficeV52.Transformed") ? "HEAD BACK THROUGH THE OFFICE"
        : !Inventory.Has("Crowbar") && !Truth.Has("OfficeV52.ArmoryOpened") ? "GET KESSLER'S PISTOL — FIND A CROWBAR IN THE CLEANER'S CLOSET"
        : !Truth.Has("OfficeV52.OfficeAccessed") ? "GET KESSLER'S PISTOL — REACH HIS OFFICE"
        : !Truth.Has("OfficeV52.ArmoryOpened") ? "FIND AND PRY OPEN THE CONCEALED ARMORY"
        : !Truth.Has("OfficeV52.Pistol") || !Inventory.Has("SiloKey") ? "RETRIEVE KESSLER'S PISTOL AND SILO TWO KEYS"
        : "ESCAPE TO SILO TWO";

    VaneHunt Hunt => VaneHunt.Instance;
    readonly List<Behaviour> held = new List<Behaviour>();
    bool initialized, redPending, redStanding, prying;
    int warnings;
    Vector3 doorHome;
    Quaternion doorRotation;
    float impactAt;
    Vector3 frameHome;
    float impactShake;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; UnlockControls(); }
    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        doorHome = concealedDoor.transform.localPosition;
        doorRotation = concealedDoor.transform.localRotation;
        if (officeDoor != null) frameHome = officeDoor.transform.localPosition;
        locker.onSolved.AddListener(UnlockLocker);
        pistol.onUse.AddListener(PistolCollected);
        keys.onUse.AddListener(KeysCollected);
        silo.onArrive.AddListener(Complete);
        if (armoryCheckpoint != null) armoryCheckpoint.onRespawn.AddListener(Retry);
        foreach (var cp in RevisedPlot.All<Checkpoint>(gameObject.scene))
            if (cp.name == "Checkpoint - Office entrance") cp.onRespawn.AddListener(Retry);
        ApplyPersistentState();
        Opening = gameObject.GetOrAdd<OfficeOpening>(); Opening.Initialize(this);
    }

    public object CaptureProgress() => (Remaining, TimerRunning, Breached, warnings);
    public void RestoreProgress(object state)
    {
        (Remaining, TimerRunning, Breached, warnings) = ((float, bool, bool, int))state;
        ApplyPersistentState();
        if (TimerRunning) HoldAtBreach();
    }

    void ApplyPersistentState()
    {
        if (Completed) Hunt?.EndOfficeSequence();
        else if (Truth.Has("OfficeV52.Transformed"))
            Hunt?.UseOfficeForm(legacyEncounter != null ? legacyEncounter.damned : null);
        if (blockedExit != null) blockedExit.enabled = false;
        if (redBacklight != null) redBacklight.enabled = false;
        if (Truth.Has("OfficeV52.CCTV") && !Truth.Has("OfficeV52.RedEncounter")) redPending = true;
        SetArmoryOpen(Truth.Has("OfficeV52.ArmoryOpened"));
        bool unlocked = Truth.Has("OfficeV52.LockerUnlocked");
        locker.SetSolved(unlocked);
        locker.gameObject.SetActive(!unlocked);
        if (lockerContents != null) lockerContents.SetActive(unlocked);
        if (lockerLid != null) lockerLid.SetActive(!unlocked);
        if (pistol != null && Truth.Has("OfficeV52.Pistol")) pistol.gameObject.SetActive(false);
        if (keys != null) keys.gameObject.SetActive(unlocked && !Inventory.Has("SiloKey"));
    }

    public void RecordingFinished()
    {
        if (Completed || Truth.Has("OfficeV52.CCTV")) return;
        Truth.Set("SawFireTape");
        Truth.Set("OfficeV52.CCTV");
        EvidenceJournal.Record("Mercer's fire order",
            "Kessler ordered the fire. Mercer shut down security, locked the exits and killed Vane with Kessler's personal Beretta. Kessler told him to return it to his office. The same pistol killed Derek and me.");
        redPending = true;
    }

    bool FreeToSequence => !(Opening != null && Opening.Busy) && !PauseMenu.Paused && !GameOverScreen.Open && !TitleScreen.Showing
        && !NoteViewer.Blocking && !GhostNPC.Conversing && !VaultDial.Active
        && (DialogueBox.Instance == null || !DialogueBox.Instance.Busy)
        && (ChoiceDialogue.Instance == null || !ChoiceDialogue.Instance.Open);
    public static bool Contains(BoxCollider area, Vector3 point)
    {
        if (area == null) return false;
        var p = area.transform.InverseTransformPoint(point) - area.center;
        var h = area.size * .5f;
        return Mathf.Abs(p.x) < h.x && Mathf.Abs(p.y) < h.y && Mathf.Abs(p.z) < h.z;
    }
    void Update()
    {
        if (!initialized || Completed || player == null || Hunt == null) return;
        if (GameOverScreen.Open) return;
        if (!Truth.Has("OfficeV52.ArmoryOpened") && concealedDoor != null)
            concealedDoor.prompt = Inventory.Has("Crowbar") ? "Pry open the concealed armory" : "Crowbar required";
        if (redPending && !InSequence && FreeToSequence) StartCoroutine(RedEncounter());
        if (redStanding && Contains(alternateExit, player.position) && FreeToSequence)
        {
            redStanding = false;
            Truth.Set("OfficeV52.RedEncounter");
            if (blockedExit != null) blockedExit.enabled = false;
            if (redBacklight != null) redBacklight.enabled = false;
            Hunt.ReleaseSequence(false);
        }
        if (Truth.Has("OfficeV52.RedEncounter") && Truth.AllFreed && !Truth.Has("OfficeV52.Transformed")
            && !InSequence && Contains(confrontationArea, player.position) && FreeToSequence && Hunt.CanStageAt(transformationPose))
            StartCoroutine(TransformVane());
        if (Truth.Has("OfficeV52.Transformed") && !Truth.Has("OfficeV52.OfficeAccessed")
            && Contains(officeInterior, player.position))
        {
            Truth.Set("OfficeV52.OfficeAccessed");
            EvidenceJournal.Record("Kessler's office", "The main door works in the Echo. His concealed storage is behind the office.");
        }
        if (TimerRunning && !PauseMenu.Paused && !TitleScreen.Showing && Time.timeScale > 0f
            && !(fader != null && fader.fade != null && fader.fade.alpha >= .99f))
            AdvanceBreach(Time.deltaTime);
    }

    IEnumerator PlaceHidden(Transform marker)
    {
        InSequence = true;
        LockControls();
        if (fader != null) yield return fader.FadeTo(1f, .25f);
        RevisedPlotDirector.Instance?.SetEcho(false);
        bool placed = Hunt != null && Hunt.HoldForSequence(marker);
        if (!placed)
        {
            Debug.LogWarning("[Office V5.2] Encounter marker obstructed; Vane retained at his current clear position.");
            Hunt?.HoldForSequence(null);
        }
        if (fader != null) yield return fader.FadeTo(0f, .4f);
    }
    IEnumerator Speak(string id)
    {
        if (DialogueBox.Instance == null) yield break;
        DialogueBox.Instance.Say(HS5.S(id));
        yield return null;
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;
    }
    IEnumerator RedEncounter()
    {
        redPending = false;
        yield return PlaceHidden(redPose);
        redStanding = true;
        if (blockedExit != null) blockedExit.enabled = true;
        if (redBacklight != null) redBacklight.enabled = true;
        yield return Speak("A3V52_red_encounter");
        UnlockControls(); InSequence = false;
    }
    IEnumerator TransformVane()
    {
        yield return PlaceHidden(transformationPose);
        yield return Speak("A3V52_transformation");
        onTransformation.Invoke();
        if (transformationClip != null) Hunt.PlaySequenceClip(transformationClip);
        yield return new WaitForSeconds(Mathf.Max(.1f, transformationClip != null ? transformationClip.length : transformationSeconds));
        // The existing Cry Wolf form replaces the human while the existing fader conceals the missing morph.
        if (fader != null) yield return fader.FadeTo(1f, .2f);
        Hunt.UseOfficeForm(legacyEncounter != null ? legacyEncounter.damned : null);
        Truth.Set("OfficeV52.Transformed");
        EvidenceJournal.Record("A way to stop Vane", "Kessler kept the pistol from the recordings in his office. A crowbar from the cleaner's closet could open the concealed storage.");
        if (fader != null) yield return fader.FadeTo(0f, .4f);
        Hunt.ReleaseSequence(true, 3f);
        UnlockControls(); InSequence = false;
    }

    public void Pry()
    {
        if (prying || Truth.Has("OfficeV52.ArmoryOpened") || !Inventory.Has("Crowbar")) return;
        StartCoroutine(PryDoor());
    }
    IEnumerator PryDoor()
    {
        prying = InSequence = true;
        LockControls();
        GunController.Holster(this, true);
        onPryStarted.Invoke();
        Opening?.BeginPry();
        if (pryAnimator != null && pryAnimator.HasState(0, Animator.StringToHash(pryState))) pryAnimator.Play(pryState, 0, 0f);
        float length = Mathf.Max(.1f, prySeconds);
        for (float t = 0f; t < length; t += Time.deltaTime)
        {
            if(GameOverScreen.Open)
            {
                Opening?.EndPry();GunController.Holster(this,false);
                prying=InSequence=false;UnlockControls();yield break;
            }
            Opening?.UpdatePry(t / length);
            concealedDoor.transform.localPosition = doorHome + Vector3.right * Mathf.Sin(t * 28f) * .012f;
            yield return null;
        }
        Opening?.EndPry();
        SetArmoryOpen(true);
        Truth.Set("OfficeV52.ArmoryOpened");
        onPryCompleted.Invoke();
        GunController.Holster(this, false);
        prying = InSequence = false; UnlockControls();
        // Only now is the entrance accessible; this is the start of the timed encounter.
        StartBreach();
    }
    void SetArmoryOpen(bool open)
    {
        if (concealedDoor == null) return;
        concealedDoor.transform.localPosition = doorHome;
        concealedDoor.transform.localRotation = doorRotation * (open ? Quaternion.Euler(0f, -85f, 0f) : Quaternion.identity);
        foreach (var c in concealedDoor.GetComponents<Collider>()) c.enabled = !open;
        concealedDoor.enabled = !open;
    }
    void HoldAtBreach()
    {
        Hunt?.HoldForSequence(breachPose);
    }
    public void StartBreach()
    {
        if (TimerRunning || !Truth.Has("OfficeV52.ArmoryOpened") || Completed) return;
        if (Inventory.Has("SiloKey") && Truth.Has("OfficeV52.Pistol")) return;
        RevisedPlotDirector.Instance?.SetEcho(false);
        TimerRunning = true; Breached = false; warnings = 0;
        Remaining = Mathf.Max(1f, breachSeconds);
        Truth.Set("OfficeV52.TimerStarted");
        // Keep the main door closed in both realms until success or the breach.
        if (officeDoor != null)
        {
            officeDoor.echoOnly = true;
            officeDoor.ApplyRealm(false);
            officeDoor.RestoreProgress(false);
        }
        HoldAtBreach();
        impactAt = 0f;
        if (armoryCheckpoint != null) armoryCheckpoint.Activate();
        Impact();
    }
    public void AdvanceBreach(float delta)
    {
        if (!TimerRunning || PauseMenu.Paused || GameOverScreen.Open) return;
        Remaining = Mathf.Max(0f, Remaining - Mathf.Max(0f, delta));
        float fraction = Remaining / Mathf.Max(1f, breachSeconds);
        if (fraction <= .75f && (warnings & 1) == 0) { warnings |= 1; onFrameDamage.Invoke(); Impact(); }
        if (fraction <= .5f && (warnings & 2) == 0) { warnings |= 2; onPartialBreach.Invoke(); Impact(); }
        if (fraction <= .25f && (warnings & 4) == 0) { warnings |= 4; onFinalWarning.Invoke(); Impact(); }
        impactAt -= Mathf.Max(0f, delta);
        if (impactAt <= 0f) { impactAt = fraction < .25f ? 2f : 5f; Impact(); }
        if (Remaining > 0f) return;
        TimerRunning = false; Breached = true;
        ClosePuzzleForThreat();
        Truth.Set("OfficeV52.Breached");
        onBreach.Invoke();
        ReleaseBreach();
    }
    void Impact()
    {
        impactShake = .35f;
        onImpact.Invoke();
        if (doorAudio != null)
            doorAudio.PlayOneShot(impactClip != null ? impactClip : impactClip = ProceduralAudio.Thud(), .8f);
    }
    void ReleaseBreach()
    {
        impactShake = 0f;
        if (officeDoor != null) officeDoor.transform.localPosition = frameHome;
        officeDoor?.ReleaseForEscape();
        Hunt?.RefreshOfficeRoute();
        Hunt?.ReleaseSequence(true, 2f);
    }
    public void UnlockLocker()
    {
        Truth.Set("OfficeV52.LockerUnlocked");
        locker.gameObject.SetActive(false);
        if (lockerLid != null) lockerLid.SetActive(false);
        if (lockerContents != null) lockerContents.SetActive(true);
        if (keys != null && !Inventory.Has("SiloKey")) keys.gameObject.SetActive(true);
    }
    public void DiscoverCombination()
    {
        Truth.Set("OfficeV52.CombinationClue");
        EvidenceJournal.Record("Executive storage combination", "Executive storage. Locker 03. Rotary mechanism serviced. Sequence L18, R07, L31.");
    }
    void PistolCollected()
    {
        Truth.Set("OfficeV52.Pistol");
        EvidenceJournal.Record("Kessler's Beretta", "Kessler's personal pistol. Mercer used it to kill Vane, Derek and me.");
        CheckSuccess();
    }
    void KeysCollected()
    {
        Truth.Set("OfficeV52.Keys");
        EvidenceJournal.Record("Silo Two access keys", "Kessler's Silo Two keys. The service entrance leads into the existing silo complex.");
        CheckSuccess();
    }
    void CheckSuccess()
    {
        if (!Truth.Has("OfficeV52.Pistol") || !Inventory.Has("SiloKey")) return;
        Truth.Set("OfficeV52.ArmorySuccess");
        TimerRunning = false;
        ReleaseBreach();
    }
    public void Retry()
    {
        Opening?.Retry();
        StopAllCoroutines(); InSequence = prying = redStanding = false;
        TimerRunning = false;
        UnlockControls(); GunController.Holster(this, false);
        Hunt?.ReleaseSequence(false, 3f);
        ApplyPersistentState();
        if (Truth.Has("OfficeV52.Transformed")) Hunt?.UseOfficeForm(legacyEncounter != null ? legacyEncounter.damned : null);
        if (Truth.Has("OfficeV52.ArmoryOpened"))
        {
            if (Truth.Has("OfficeV52.ArmorySuccess")) ReleaseBreach();
            else StartBreach();
        }
    }
    public void Complete()
    {
        if (!Inventory.Has("SiloKey") || !Truth.Has("OfficeV52.Pistol")) return;
        Truth.Set("OfficeV52.SiloUnlocked"); Truth.Set("OfficeV52.Completed");
        TimerRunning = false; InSequence = false;
        Hunt?.EndOfficeSequence();
        if (blockedExit != null) blockedExit.enabled = false;
        if (redBacklight != null) redBacklight.enabled = false;
        UnlockControls();
    }
    void LockControls()
    {
        foreach (var b in player.GetComponentsInChildren<Behaviour>(true))
            if ((b is FirstPersonController || b is PlayerInteractor || b is PunchController || b is GunController) && b.enabled)
            { b.enabled = false; held.Add(b); }
        if (player.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic) rb.linearVelocity = Vector3.zero;
    }
    void UnlockControls()
    {
        foreach (var b in held) if (b != null) b.enabled = true;
        held.Clear();
    }
    void OnDisable()
    {
        StopAllCoroutines(); Opening?.EndPry(); UnlockControls(); GunController.Holster(this, false);
        Hunt?.ReleaseSequence(false);
        if (blockedExit != null) blockedExit.enabled = false;
        if (redBacklight != null) redBacklight.enabled = false;
        if (initialized && officeDoor != null) officeDoor.transform.localPosition = frameHome;
    }
    void LateUpdate()
    {
        if (!initialized || officeDoor == null || !TimerRunning || PauseMenu.Paused) return;
        impactShake = Mathf.Max(0f, impactShake - Time.deltaTime);
        officeDoor.transform.localPosition = frameHome + Vector3.right * Mathf.Sin(Time.time * 70f) * .04f * impactShake;
    }
    void OnGUI()
    {
        if (!TimerRunning || PauseMenu.Paused || GameOverScreen.Open) return;
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 28, font = GameFont.Ttf };
        style.normal.textColor = Remaining <= 15f ? Color.red : Color.white;
        GUI.Label(new Rect(0, 24, Screen.width, 50), "DOOR BREACH  " + Mathf.CeilToInt(Remaining).ToString("00"), style);
    }
}
