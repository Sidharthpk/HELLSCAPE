using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds the revised story beats to the existing levels without replacing their puzzles or encounter wiring.
public class RevisedPlotDirector : MonoBehaviour, ISceneProgress
{
    public static RevisedPlotDirector Instance { get; private set; }
    public bool Echo { get; private set; }
    public bool InSequence { get; private set; }
    public bool VaneReleased { get; private set; }
    GhostNPC[] ghosts;
    GhostNPC vane, tomas;
    KesslerConfrontation confrontation;
    Transform player, office;
    EchoOfficeMemory officeMemory;
    OfficeRealmPuzzles puzzles;
    OfficeHorrorDirector horror;
    EchoVisit echoVisit;
    GameObject rifleSetup;
    readonly List<Behaviour> locked = new List<Behaviour>();
    readonly List<Material> materials = new List<Material>();
    readonly List<AudioClip> clips = new List<AudioClip>();
    bool echoVisited, phoneSeen, introDone;
    int journalPage;

    public object CaptureProgress() => (echoVisited, phoneSeen, introDone);
    public void RestoreProgress(object state) => (echoVisited, phoneSeen, introDone) = ((bool, bool, bool))state;

    void Awake() => Instance = this;
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var material in materials) if (material != null) Destroy(material);
        foreach (var clip in clips) if (clip != null) Destroy(clip);
    }

    void Start()
    {
        var scene = gameObject.scene;
        var fpc = RevisedPlot.All<FirstPersonController>(scene).FirstOrDefault();
        player = fpc != null ? fpc.transform : null;
        if (scene.name == "Prologue")
        {
            EvidenceJournal.Reset();
            SetupPrologue();
            return;
        }
        officeMemory = gameObject.AddComponent<EchoOfficeMemory>();
        puzzles = gameObject.AddComponent<OfficeRealmPuzzles>();
        puzzles.Initialize();
        gameObject.AddComponent<OfficeEvidenceLayout>();
        ghosts = RevisedPlot.All<GhostNPC>(scene);
        vane = ghosts.FirstOrDefault(g => g.ghostName == "Vane");
        tomas = ghosts.FirstOrDefault(g => g.ghostName == "Tomas");
        confrontation = RevisedPlot.All<KesslerConfrontation>(scene).FirstOrDefault();
        // Story V4: nobody else came to the silo. Maya and Sam (and their rescue in the flood) are gone; the bridge
        // boss is Vane alone (no Tomas fusion), so DemonBoss plays his own change from the damned body
        foreach (var sv in RevisedPlot.All<Survivor>(scene)) Destroy(sv.gameObject);
        // OfficeAct/Office is at the world origin; the authored rooms are offset by ~1000m.
        // Use the office resident as the spatial anchor so the Echo key works inside the actual rooms.
        office = vane != null ? vane.transform : null;
        if (tomas != null) tomas.gameObject.AddComponent<SoulConviction>();
        if (vane != null) vane.gameObject.SetActive(false);
        foreach (var chase in RevisedPlot.All<KesslerChase>(scene))
            if (chase.tomas != null && chase.tomas.GetComponent<SoulConviction>() == null) chase.tomas.gameObject.AddComponent<SoulConviction>().rage = .75f;
        horror = gameObject.AddComponent<OfficeHorrorDirector>();
        horror.Init(this);
        OfficeV52Sequence.Instance?.Initialize();
        gameObject.AddComponent<StoryObjectives>();
        echoVisit = gameObject.AddComponent<EchoVisit>(); echoVisit.Initialize(this, player, ghosts, confrontation);
        foreach (var g in ghosts)
            if (g.GetComponent<WitnessLook>() != null) g.gameObject.AddComponent<OfficeWitness>().Initialize(g);
        foreach (var item in RevisedPlot.All<Interactable>(scene))
        {
            string id = item.name == "IncidentReport" ? EvidenceJournal.Statement : item.name == "KesslerLedger" ? EvidenceJournal.Ledger : null;
            if (id != null) AddEvidence(item, id);
        }
        if (confrontation != null && vane != null)
        {
            Vector3 at = vane.transform.position;   // the manifest stays in Vane's office when Tomas moves
            var container = Box("KEEP COLD - collection", at + new Vector3(1.5f, .35f, 0f), new Vector3(1.1f, .65f, .7f), new Color(.45f, .57f, .59f));
            Label(container.transform, "KEEP COLD\nHARLOW / V-0500", new Vector3(0f, .08f, -.505f), .065f);
            var item = container.AddComponent<Interactable>();
            item.prompt = "Read the Harlow manifest"; item.once = false;
            var note = container.AddComponent<ReadableNote>();
            note.title = "HARLOW COLLECTION MANIFEST";
            note.body = "HARLOW CLINIC\nKEEP COLD\n\nReturn sealed to Kessler & Vane depot.\nApproval: V-0500\nLinked disposal account: T. ROURKE\n05:00 collection instruction.";
            note.afterLine = HS.S("305-306") + "|*Evidence recorded. [J] to review.*";
            AddEvidence(item, EvidenceJournal.Invoice);
            Vector3 phoneAt = vane.transform.position + vane.transform.right * 1.3f - vane.transform.forward * 2.4f + Vector3.up * .85f;   // along the wall, clear of Kessler's doorway
            var phone = Box("Vane's Echo telephone", phoneAt, new Vector3(.42f, .16f, .28f), Color.black);
            Label(phone.transform, "05:00 / REPLAY", new Vector3(0f, .05f, -.505f), .02f);
            var use = phone.AddComponent<Interactable>(); use.once = false; use.prompt = "Replay Vane's call in the Echo";
            use.onUse.AddListener(() => { if (Echo && !phoneSeen) StartCoroutine(PhoneReplay()); else if (!Echo) Say("A phone that isn't really here. Press [V] to enter the Echo."); else Say(EvidenceJournal.Account(EvidenceJournal.Phone)); });

        }
        var wake = RevisedPlot.All<SleepSequence>(scene).FirstOrDefault();
        if (wake != null) wake.onWake.AddListener(() => { if (!introDone) StartCoroutine(OpeningMark()); });
    }

    public void RifleThrown() { if (rifleSetup != null) rifleSetup.SetActive(false); }

    void SetupPrologue()
    {
        var run = RevisedPlot.All<DeliveryRun>(gameObject.scene).FirstOrDefault();
        if (run == null) return;
        var bay = run.depotBay;
        if (bay != null)
        {
            Vector3 p = bay.position + bay.right * 5f;
            // Vane's car is one of the street's own parked cars (the PSX estate), left in the open behind the van's bay
            // (the way the van faces), in the first clear spot: the pavement beside the garage door was a hedge
            var parked = RevisedPlot.All<BoxCollider>(gameObject.scene).FirstOrDefault(c => c.name == "Parked car 3");
            if (parked != null)
            {
                Vector3 kerb = bay.position - bay.forward * 12f;
                for (float back = 12f; back <= 24f; back += 2f)
                    foreach (float side in new[] { 0f, 3f, -3f })
                    {
                        Vector3 at = bay.position - bay.forward * back + bay.right * side;
                        if (!Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out var g, 8f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        // clear of anything standing at car height (hedges, bollards, walls): a box the size of the car
                        Vector3 half = new Vector3(1.2f, .7f, 2.4f);   // (the car's long side runs along its z)
                        if (Physics.CheckBox(g.point + Vector3.up * (.35f + half.y), half, bay.rotation, ~0, QueryTriggerInteraction.Ignore)) continue;
                        kerb = g.point; back = 99f; break;
                    }
                var car = Instantiate(parked.gameObject, kerb, bay.rotation);
                car.name = "Vane's idling car"; car.transform.localScale = parked.transform.lossyScale; car.transform.SetParent(transform, true);
                Loop(car, ProceduralAudio.ScooterEngine(), .12f, 20f);
            }
            var deskPhone = Box("Depot ringing desk phone", p + new Vector3(-2f, .9f, 1f), new Vector3(.4f, .15f, .3f), Color.black);
            Loop(deskPhone, Ring(), .16f, 14f);
            var bucket = GameObject.CreatePrimitive(PrimitiveType.Cylinder); bucket.name = "Cleaner bucket"; bucket.transform.SetParent(transform);
            bucket.transform.position = p + new Vector3(-2f, .23f, -1f); bucket.transform.localScale = new Vector3(.45f, .23f, .45f); bucket.GetComponent<Renderer>().sharedMaterial = Material(new Color(.35f, .4f, .42f));
            Box("Pink mop water", bucket.transform.position + Vector3.up * .225f, new Vector3(.38f, .015f, .38f), new Color(.46f, .16f, .2f));
        }
        // V4: you DELIVER the sealed box to Harlow (it's Tomas, and you never open it): it appears at the man's feet
        // once it's handed over, and its label only ever says to keep the seal
        if (run.stops.Length > 0 && run.stops[0].customer != null)
        {
            Vector3 at = run.stops[0].customer.transform.position + run.stops[0].customer.transform.right * .9f + Vector3.up * .35f;
            var box = Box("Harlow KEEP COLD box (delivered)", at, new Vector3(.9f, .6f, .65f), new Color(.55f, .65f, .67f));
            Label(box.transform, "KEEP COLD\nSEAL MUST STAY INTACT", new Vector3(0f, .51f, -.51f), .07f);
            var item = box.AddComponent<Interactable>(); item.prompt = "Look at the box"; item.once = false;
            item.line = HS.S("24");
            box.SetActive(false);
            run.stops[0].customer.onUse.AddListener(() => box.SetActive(true));
        }
        var killer = RevisedPlot.All<FleeingKiller>(gameObject.scene).FirstOrDefault();
        // Mercer, company security, waiting in the depot to ask about the box: the same man who shoots you on the sofa
        // (the director's Meshy Mercer), stood still at the point of his walk where his feet are together
        var meshyMercer = RevisedPlot.All<PrologueDirector>(gameObject.scene).FirstOrDefault()?.mercerAims;
        if (meshyMercer != null && bay != null)
        {
            var mercer = Instantiate(meshyMercer.gameObject, bay.position - bay.right * 3f, Quaternion.LookRotation(bay.right));
            mercer.name = "Mercer (at the depot)";
            mercer.transform.localScale = meshyMercer.transform.lossyScale;
            mercer.transform.SetParent(transform, true);
            if (Physics.Raycast(mercer.transform.position + Vector3.up * 2f, Vector3.down, out var ground, 6f, ~0, QueryTriggerInteraction.Ignore))
                mercer.transform.position = ground.point;
            foreach (var t in mercer.GetComponentsInChildren<Transform>(true))
                if (t.name == "Pistol") t.gameObject.SetActive(false);
            foreach (var mb in mercer.GetComponentsInChildren<MonoBehaviour>()) Destroy(mb);
            foreach (var c in mercer.GetComponentsInChildren<Collider>()) Destroy(c);
            mercer.SetActive(true);
            var anim = mercer.GetComponent<Animator>();
            if (anim != null)
            {
                anim.applyRootMotion = false;
                Transform lf = null, rf = null;
                foreach (var t in anim.GetComponentsInChildren<Transform>()) { if (t.name.EndsWith("LeftFoot")) lf = t; else if (t.name.EndsWith("RightFoot")) rf = t; }
                float bestAt = 0f, bestGap = float.MaxValue;
                for (float n = 0f; lf != null && rf != null && n < 1f; n += .05f)
                {
                    anim.Play("Walk", 0, n); anim.Update(0f);
                    float gap = Vector3.ProjectOnPlane(lf.position - rf.position, Vector3.up).magnitude;
                    if (gap < bestGap) { bestGap = gap; bestAt = n; }
                }
                anim.Play("Walk", 0, bestAt); anim.Update(0f);
                anim.speed = 0f;
            }
        }
        if (killer != null)
        {
            foreach (var t in killer.GetComponentsInChildren<Transform>(true))
                if (t != killer.transform && (t.name.IndexOf("rifle", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("AK47", StringComparison.OrdinalIgnoreCase) >= 0)) t.gameObject.SetActive(false);
            Transform hand = killer.animator != null && killer.animator.isHuman ? killer.animator.GetBoneTransform(HumanBodyBones.RightHand) : killer.transform;
            if (hand != null)
            {
                GameObject knife;
                if (killer.knifeModel != null)
                {
                    // the user's knife, 30 cm from pommel to tip, its middle on the holder's pivot
                    // (measured again once it's scaled: the model's own pivot is nowhere near its middle)
                    knife = new GameObject("Mercer's knife");
                    var body = Instantiate(killer.knifeModel, knife.transform);
                    foreach (var c in body.GetComponentsInChildren<Collider>()) Destroy(c);
                    var rs = body.GetComponentsInChildren<Renderer>();
                    if (killer.knifeMaterial != null) foreach (var r in rs) r.sharedMaterial = killer.knifeMaterial;
                    Bounds Extent() { Bounds e = rs[0].bounds; foreach (var r in rs) e.Encapsulate(r.bounds); return e; }
                    Bounds b = Extent();
                    body.transform.localScale *= .3f / Mathf.Max(.0001f, Mathf.Max(b.size.x, b.size.y, b.size.z));
                    body.transform.position += knife.transform.position - Extent().center;
                }
                else knife = Box("Mercer's knife", hand.position, new Vector3(.035f, .26f, .055f), new Color(.55f, .57f, .59f));
                knife.transform.SetParent(hand, false);
                // held in the fist, blade out past the thumb: the way he faces, square to the forearm (the hand's y runs down the fingers)
                // (and a little outwards: with his arm hanging the hand turns in, and a blade dead ahead goes through his thigh)
                Vector3 blade = hand.InverseTransformDirection(killer.transform.forward + killer.transform.right * .45f); blade.y = 0f; blade.Normalize();
                float bone = Mathf.Max(.001f, hand.lossyScale.x);   // (his rig's bones are scaled)
                knife.transform.localScale /= bone;
                knife.transform.localRotation = Quaternion.FromToRotation(Vector3.up, blade);
                knife.transform.localPosition = (Vector3.up * .08f + blade * .095f) / bone;   // the handle (its back 11 cm) in the fist
                Destroy(knife.GetComponent<Collider>());
            }
        }
    }

    public void MarkHands() { if (!introDone) StartCoroutine(OpeningMark()); }

    IEnumerator OpeningMark()
    {
        introDone = true;
        yield return new WaitForSeconds(.4f);
        var ending = RevisedPlot.All<EndingSequence>(gameObject.scene).FirstOrDefault();
        if (ending != null && ending.markedSkin != null && player != null)
            foreach (var r in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name.IndexOf("arm", StringComparison.OrdinalIgnoreCase) >= 0) r.material.mainTexture = ending.markedSkin;
        // V4: no guide on the street any more. You go home first (RedFlat: the objective says so), and the flat's
        // voicemail sends you to the office.
    }

    bool journalOpen;
    readonly Dictionary<GhostNPC, bool> echoStates = new Dictionary<GhostNPC, bool>();

    void Update()
    {
        if ((horror != null && horror.Switching) || player == null || (gameObject.scene.name != "MainGameScene" && gameObject.scene.name != "OfficeSilo") || TitleScreen.Showing || PauseMenu.Paused || GameOverScreen.Open || EndingSequence.Playing || InSequence || NoteViewer.Blocking || GhostNPC.Conversing) return;
        bool nearOffice = office != null && OfficeHorrorDirector.In(player.position);
        if (Echo && !nearOffice) SetEcho(false);
        if (journalOpen && Input.GetKeyDown(KeyCode.J)) { ChoiceDialogue.Instance.Close(); journalOpen = false; return; }
        if (ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open || DialogueBox.Instance != null && DialogueBox.Instance.Busy) return;
        if (Input.GetKeyDown(KeyCode.J)) ShowJournal();
        if (Input.GetKeyDown(KeyCode.V) && nearOffice && !VaultDial.Active
            && !(OfficeV52Sequence.Instance != null && OfficeV52Sequence.Instance.BlocksRealmSwitch)
            && !(confrontation != null && confrontation.Ambushing) && !VaneReleased && (!Echo || puzzles == null || puzzles.CanSwitch(player)))
        {
            bool next = !Echo;
            if (horror != null) horror.RequestSwitch(next, () => SetEcho(next)); else SetEcho(next);
        }
    }

    public void SetEcho(bool on)
    {
        if (Echo == on) return;
        Echo = on;
        echoVisit?.RealmChanged(on);
        foreach (var d in RevisedPlot.All<InteriorDarkness>(gameObject.scene)) d.SetEcho(on);
        if (!on) journalOpen = false;
        if (officeMemory != null) officeMemory.SetEcho(on);
        if (puzzles != null) puzzles.SetEcho(on);
        if (horror != null) horror.OnEcho(on);
        foreach (var g in ghosts ?? new GhostNPC[0])
            if (g != null && g != vane)
            {
                if (g.TryGetComponent<WitnessLook>(out var look)) { look.Echo = on; continue; }
                if (on) { echoStates[g] = g.gameObject.activeSelf; g.gameObject.SetActive(false); }
                else if (echoStates.TryGetValue(g, out bool active)) g.gameObject.SetActive(active);
            }
        if (!on) echoStates.Clear();
        if (vane != null && !VaneReleased && !(confrontation != null && confrontation.Armed)) vane.gameObject.SetActive(on && OfficeV52Sequence.Instance == null);
        if (on && !echoVisited)
        {
            echoVisited = true;
            Say("*The Echo. Six seconds, then it pulls you back.*");
        }
    }

    public void ReleaseVane()
    {
        SetEcho(false);
        VaneReleased = true;
        foreach (var door in RevisedPlot.All<KesslerDoor>(gameObject.scene)) door.ReleaseForEscape();
        if (vane != null) vane.gameObject.SetActive(true);
    }

    IEnumerator PhoneReplay()
    {
        phoneSeen = true;
        EvidenceJournal.Collect(EvidenceJournal.Phone);
        // ([disconnect], HS_0302, is a sound the project doesn't have yet: the narration stands in for it)
        yield return Speak(HS.S("297-301") + "|*The line goes dead.*|" + HS.S("303-304,314"));
        if (tomas != null) tomas.GetComponent<SoulConviction>().Exert(.6f);
    }

    void ShowJournal()
    {
        var choice = ChoiceDialogue.Instance;
        if (choice == null) return;
        var entries = EvidenceJournal.Found;
        if (entries.Length == 0) { Say("*No evidence yet. Read the report and the ledger. Watch the security tape.*"); return; }
        if (journalPage * 3 >= entries.Length) journalPage = 0;
        var page = entries.Skip(journalPage * 3).Take(3).ToArray();
        var labels = page.Concat(new[] { entries.Length > 3 ? "Next page / close with [J]" : "Close journal" }).ToArray();
        journalOpen = true;
        choice.Show("Evidence", "What do I know?", labels, i =>
        {
            journalOpen = false;
            if (i < page.Length) Say(EvidenceJournal.Account(page[i]));
            else if (entries.Length > 3) { journalPage++; ShowJournal(); }
        });
    }

    // V5.1 bridge: the King calls Vane to answer, Vane admits what he approved, the King judges him, Tomas is named back.
    // (The three heart exchanges are DemonBoss.heartsLines = A07_heart_prompt; this is what follows the last heart.)
    public IEnumerator AmalgamationAccounts(DemonBoss boss)
    {
        if (EvidenceJournal.VaneConfessed) yield break;
        InSequence = true;
        yield return WaitForDialogue();
        yield return Speak(HS5.S("A07_judgement_call"));
        EvidenceJournal.VaneConfessed = true;
        Truth.Set("VaneConfessed");
        // His monstrous shell will sink in the existing finale; the confessed soul separates first.
        GameObject soul = vane != null ? Visual(vane.body != null ? vane.body : vane.transform, "Vane confessed", boss.transform.position + boss.transform.forward * 3f) : null;
        if (soul != null) soul.AddComponent<SoulConviction>().Confess();
        yield return Speak(HS5.S("A07_tomas_freed"));
        if (soul != null) Destroy(soul);
        EvidenceJournal.Record("Vane, judged", "On the bridge Vane stopped lying. He said it: he approved the shipments and removed their names, knowing they were people. The King judged him by name. Tomas heard his own name said back to him.");
        InSequence = false;
    }

    // V5.1: the Driver gives no account of himself and there is no branch (A08_judgement carries his answer)
    public IEnumerator PlayerConfession() { EvidenceJournal.PlayerConfessed = true; yield break; }

    // Script 8.5, after the end card: a black screen and a telephone call, nothing to see. The second voice's name plate
    // is whatever its CSV row says (UNKNOWN), so it cannot read DEREK before Kessler has said the name (CallReveal).
    // [Enter] skips the rest once the name is out. Timings are on EndingSequence, in the Inspector.
    // V5.1: Kessler and Mercer, nobody else (HS5 A08_post_call). Enter skips from the first line.
    public static System.Collections.Generic.List<HS5.Row> CallLines() => HS5.Rows.FindAll(r => r.trigger == "A08_post_call" && r.kind != "sfx");
    public const string CallVoice = "Ending/Post-credits voice ";   // + the row number: a recording replaces the mumble
    bool callPlayed;

    public IEnumerator PostCredits(float ringSeconds, float linePause, float revealPause, float endSilence)
    {
        var box = DialogueBox.Instance;
        if (callPlayed || box == null) yield break;
        callPlayed = true;
        yield return WaitForDialogue();
        MusicManager.Stop(2f);
        var phone = gameObject.AddComponent<AudioSource>();
        phone.playOnAwake = false; phone.spatialBlend = 0f;
        AudioClip ring = Ring(), click = ProceduralAudio.Click();
        clips.Add(ring); clips.Add(click);
        bool revealed = false, skip = false;
        IEnumerator Hold(float seconds)
        {
            for (float t = 0f; t < seconds && !skip; t += Time.deltaTime) { skip = revealed && SkipCall; yield return null; }
        }

        Step("ring");
        var recording = Sounds.Clip("Ending/Phone ring", null);
        if (recording != null)   // a recording has the ringing and the pick-up in one: all of it, once
        {
            Sounds.OneShot(phone, "Ending/Phone ring", null);
            yield return Hold(recording.length);
            Step("pick up");
        }
        else
        {
            phone.clip = ring; phone.volume = .5f; phone.loop = true;
            phone.Play();
            yield return Hold(ringSeconds);
            phone.Stop(); phone.volume = 1f;
            Step("pick up");
            Sounds.OneShot(phone, "Ending/Phone pick up", click);
        }
        // the open line hums under the whole conversation
        phone.clip = Sounds.Clip("Ending/Phone line", null); phone.volume = Sounds.Volume("Ending/Phone line"); phone.loop = true;
        if (phone.clip != null) phone.Play();

        revealed = true;   // (no name to hide any more: Enter can skip at once)
        foreach (var line in CallLines())
        {
            if (skip) break;
            yield return Hold(linePause);
            if (skip) break;
            string id = line.id.Substring(4);
            Step("line " + id);
            if (box.blip != null) box.blip.mute = Sounds.Has(CallVoice + id);
            Sounds.OneShot(phone, CallVoice + id, null);
            box.Say(Capitalised(line.speaker) + ": " + line.text);
            yield return null;
            while (box.Busy && !skip) { skip = revealed && SkipCall; yield return null; }
        }

        if (skip) { box.Clear(); phone.Stop(); Step("skipped"); yield return null; }   // (a frame: the same Enter must not also leave the end card)
        else
        {
            Step("disconnect");
            phone.Stop(); phone.volume = 1f;
            var hangUp = Sounds.Clip("Ending/Phone disconnect", click);
            phone.PlayOneShot(hangUp, Sounds.Volume("Ending/Phone disconnect"));
            yield return Hold(hangUp.length + endSilence);
        }
        if (box.blip != null) box.blip.mute = false;
        Destroy(phone);
        Step("end");
    }
    static string Capitalised(string speaker) => speaker.Substring(0, 1) + speaker.Substring(1).ToLowerInvariant();
    static bool SkipCall => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    static void Step(string what) => Debug.Log("[post-credits] " + what);   // read by HellScape > Tests > Post-Credits Call

    public static IEnumerator Speak(string lines)
    {
        yield return WaitForDialogue();
        Say(lines);
        yield return null;
        yield return WaitForDialogue();
    }
    public static IEnumerator WaitForDialogue()
    {
        // capped so a stuck dialogue box can't soft-lock a cutscene (deltaTime: the pause menu doesn't burn it)
        for (float t = 0f; t < 120f && DialogueBox.Instance != null && DialogueBox.Instance.Busy; t += Time.deltaTime) yield return null;
    }
    static void Say(string line) { if (DialogueBox.Instance != null) DialogueBox.Instance.Say(line); }
    static IEnumerator Choose(string who, string question, string[] options, Action<int> pick)
    {
        yield return WaitForDialogue();
        if (ChoiceDialogue.Instance == null) { Debug.LogError("Revised plot needs ChoiceDialogue to present confession choices."); yield break; }
        bool done = false;
        ChoiceDialogue.Instance.Show(who, question, options, i => { pick(i); done = true; });
        while (!done) yield return null;
    }

    static void AddEvidence(Interactable item, string id)
    {
        var clue = item.GetComponent<EvidenceClue>();
        if (clue == null) clue = item.gameObject.AddComponent<EvidenceClue>();
        clue.evidenceId = id;
    }
    GameObject Visual(Transform source, string name, Vector3 at)
    {
        var clone = Instantiate(source.gameObject, at, source.rotation, transform);
        clone.name = name;
        foreach (var c in clone.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var b in clone.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
        clone.SetActive(true);
        return clone;
    }
    Material Material(Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color = color; materials.Add(mat); return mat;
    }
    GameObject Box(string name, Vector3 at, Vector3 size, Color color, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent != null ? parent : transform, true); go.transform.position = at; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = Material(color); return go;
    }
    void Label(Transform parent, string value, Vector3 local, float size)
    {
        var go = new GameObject("Label"); go.transform.SetParent(parent, false); go.transform.localPosition = local;
        var text = go.AddComponent<TextMesh>(); text.text = value; text.characterSize = size; text.fontSize = 42; text.transform.localScale = Vector3.one * .15f; text.anchor = TextAnchor.MiddleCenter; text.color = Color.black;
        if (GameFont.Ttf != null) { text.font = GameFont.Ttf; go.GetComponent<MeshRenderer>().sharedMaterial = GameFont.Ttf.material; }
    }
    void Loop(GameObject owner, AudioClip clip, float volume, float range)
    {
        clips.Add(clip); var audio = owner.AddComponent<AudioSource>(); audio.clip = clip; audio.spatialBlend = 1f; audio.loop = true; audio.volume = volume; audio.rolloffMode = AudioRolloffMode.Linear; audio.maxDistance = range; audio.Play();
    }
    AudioClip Ring()
    {
        const int rate = 22050; var data = new float[rate * 4];
        for (int i = 0; i < data.Length; i++) { float t = i / (float)rate; data[i] = t < .5f || t > .7f && t < 1.2f ? Mathf.Sin(t * 440f * Mathf.PI * 2f) * .15f : 0f; }
        var clip = AudioClip.Create("Desk phone ring", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
    }
}
