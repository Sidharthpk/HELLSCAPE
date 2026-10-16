using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds the revised story beats to the existing levels without replacing their puzzles or encounter wiring.
public class RevisedPlotDirector : MonoBehaviour
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
    GameObject hallwayFigure, rifleSetup;
    readonly List<Behaviour> locked = new List<Behaviour>();
    readonly List<Material> materials = new List<Material>();
    readonly List<AudioClip> clips = new List<AudioClip>();
    bool echoVisited, phoneSeen, introDone;
    int journalPage;

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
        EvidenceJournal.ResetHell();
        ghosts = RevisedPlot.All<GhostNPC>(scene);
        vane = ghosts.FirstOrDefault(g => g.ghostName == "Vane");
        tomas = ghosts.FirstOrDefault(g => g.ghostName == "Tomas");
        confrontation = RevisedPlot.All<KesslerConfrontation>(scene).FirstOrDefault();
        foreach (var boss in RevisedPlot.All<DemonBoss>(scene))
        {
            var fusion = boss.GetComponent<TomasAmalgamation>();
            if (fusion == null) fusion = boss.gameObject.AddComponent<TomasAmalgamation>();
            fusion.Prepare(boss);
        }
        // OfficeAct/Office is at the world origin; the authored rooms are offset by ~1000m.
        // Use the office resident as the spatial anchor so the Echo key works inside the actual rooms.
        office = vane != null ? vane.transform : null;
        if (tomas != null) tomas.gameObject.AddComponent<SoulConviction>();
        if (vane != null) vane.gameObject.SetActive(false);
        foreach (var chase in RevisedPlot.All<KesslerChase>(scene))
            if (chase.tomas != null && chase.tomas.GetComponent<SoulConviction>() == null) chase.tomas.gameObject.AddComponent<SoulConviction>().rage = .75f;
        horror = gameObject.AddComponent<OfficeHorrorDirector>();
        horror.Init(this);
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
            Vector3 at = tomas != null ? tomas.transform.position : confrontation.transform.position;
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
            if (tomas != null)
            {
                hallwayFigure = Visual(tomas.body != null ? tomas.body : tomas.transform, "Tomas at the end of the Echo", at + new Vector3(0f, 0f, 5f));
                hallwayFigure.AddComponent<SoulConviction>().rage = .8f;
                hallwayFigure.SetActive(false);
            }
        }
        var silo = RevisedPlot.All<SiloEncounter>(scene).FirstOrDefault();
        if (silo != null && silo.gunProp != null && silo.gunThrowFrom != null)
        {
            rifleSetup = Visual(silo.gunProp, "Elias's abandoned rifle", silo.gunThrowFrom.position + Vector3.down * .8f);
            rifleSetup.transform.rotation = Quaternion.Euler(0f, 20f, 90f);
        }
        var wake = RevisedPlot.All<SleepSequence>(scene).FirstOrDefault();
        if (wake != null) wake.onWake.AddListener(() => { if (!introDone) StartCoroutine(OpeningMark()); });
    }

    public void RifleThrown() { if (rifleSetup != null) rifleSetup.SetActive(false); }

    void SetupPrologue()
    {
        var run = RevisedPlot.All<DeliveryRun>(gameObject.scene).FirstOrDefault();
        if (run == null) return;
        // Opening dispatch is explicitly a stored briefing: Maya died before the game begins.
        StartCoroutine(RadioBriefing());
        var bay = run.depotBay;
        if (bay != null)
        {
            Vector3 p = bay.position + bay.right * 5f;
            // Vane's car is one of the street's own parked cars (the PSX estate), left on the pavement beside the
            // garage door, where you see it driving in (there's no room for it inside: the side wall is 5 m from the bay)
            var parked = RevisedPlot.All<BoxCollider>(gameObject.scene).FirstOrDefault(c => c.name == "Parked car 3");
            if (parked != null)
            {
                Vector3 kerb = bay.position + bay.right * 7.5f - bay.forward * 10.9f;
                if (Physics.Raycast(kerb + Vector3.up * 2f, Vector3.down, out var ground, 6f, ~0, QueryTriggerInteraction.Ignore)) kerb = ground.point;
                var car = Instantiate(parked.gameObject, kerb, bay.rotation * Quaternion.Euler(0f, 90f, 0f));
                car.name = "Vane's idling car"; car.transform.localScale = parked.transform.lossyScale; car.transform.SetParent(transform, true);
                Loop(car, ProceduralAudio.ScooterEngine(), .12f, 20f);
            }
            var deskPhone = Box("Depot ringing desk phone", p + new Vector3(-2f, .9f, 1f), new Vector3(.4f, .15f, .3f), Color.black);
            Loop(deskPhone, Ring(), .16f, 14f);
            var bucket = GameObject.CreatePrimitive(PrimitiveType.Cylinder); bucket.name = "Cleaner bucket"; bucket.transform.SetParent(transform);
            bucket.transform.position = p + new Vector3(-2f, .23f, -1f); bucket.transform.localScale = new Vector3(.45f, .23f, .45f); bucket.GetComponent<Renderer>().sharedMaterial = Material(new Color(.35f, .4f, .42f));
            Box("Pink mop water", bucket.transform.position + Vector3.up * .225f, new Vector3(.38f, .015f, .38f), new Color(.46f, .16f, .2f));
        }
        if (run.stops.Length > 1 && run.stops[1].customer != null)
        {
            Vector3 at = run.stops[1].customer.transform.position + run.stops[1].customer.transform.right * .9f + Vector3.up * .35f;
            var box = Box("Clinic KEEP COLD container", at, new Vector3(.9f, .6f, .65f), new Color(.55f, .65f, .67f));
            Label(box.transform, "KEEP COLD\nVANE / V-0500", new Vector3(0f, .51f, -.51f), .08f);
            var item = box.AddComponent<Interactable>(); item.prompt = "Inspect the container label"; item.once = false;
            item.line = HS.S("54-57");
            var clue = box.AddComponent<EvidenceClue>(); clue.prologueLabel = true;
            var stop = run.stops[1];
            stop.customer.onUse.AddListener(() =>
            {
                box.SetActive(false);
                if (!EvidenceJournal.ContainerInspected) stop.handOverLines += "|" + HS.S("58-59");   // took it without reading the label
            });
        }
        var killer = RevisedPlot.All<FleeingKiller>(gameObject.scene).FirstOrDefault();
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
                    knife = new GameObject("Elias's knife");
                    var body = Instantiate(killer.knifeModel, knife.transform);
                    foreach (var c in body.GetComponentsInChildren<Collider>()) Destroy(c);
                    var rs = body.GetComponentsInChildren<Renderer>();
                    if (killer.knifeMaterial != null) foreach (var r in rs) r.sharedMaterial = killer.knifeMaterial;
                    Bounds Extent() { Bounds e = rs[0].bounds; foreach (var r in rs) e.Encapsulate(r.bounds); return e; }
                    Bounds b = Extent();
                    body.transform.localScale *= .3f / Mathf.Max(.0001f, Mathf.Max(b.size.x, b.size.y, b.size.z));
                    body.transform.position += knife.transform.position - Extent().center;
                }
                else knife = Box("Elias's knife", hand.position, new Vector3(.035f, .26f, .055f), new Color(.55f, .57f, .59f));
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

    IEnumerator RadioBriefing()
    {
        yield return new WaitForSeconds(2f);
        while (OfficeFlashback.Playing) yield return null;   // the night before comes first
        yield return new WaitForSeconds(2f);
        yield return WaitForDialogue();
        var audio = gameObject.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0f;
        var noise = ProceduralAudio.Whisper(2f); clips.Add(noise); audio.PlayOneShot(noise, .09f);
        yield return Speak(HS.S("21-27"));
        audio.Stop();
        yield return Speak(HS.S("28-31"));
    }

    IEnumerator OpeningMark()
    {
        introDone = true;
        yield return new WaitForSeconds(.4f);
        var ending = RevisedPlot.All<EndingSequence>(gameObject.scene).FirstOrDefault();
        if (ending != null && ending.markedSkin != null && player != null)
            foreach (var r in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name.IndexOf("arm", StringComparison.OrdinalIgnoreCase) >= 0) r.material.mainTexture = ending.markedSkin;
        if (!SleepSequence.FromStory) yield break;   // a debug jump (Start In Office, Play > ...): don't spoil his cutscene
        yield return WaitForDialogue();
        if (player == null) yield break;
        GameObject guide = tomas != null ? Visual(tomas.body != null ? tomas.body : tomas.transform, "Tomas finds the marked driver", player.position + player.forward * 3f - Vector3.up) : null;
        if (guide != null) { guide.transform.LookAt(new Vector3(player.position.x, guide.transform.position.y, player.position.z)); guide.AddComponent<SoulConviction>().Exert(.3f); }
        const string hint = "*In the office: [V] opens the Echo. [J] is your evidence.*";
        var cuts = RevisedPlot.All<KillerCutscenes>(gameObject.scene).FirstOrDefault(k => k.isActiveAndEnabled && k.cam != null && !k.Playing);
        if (guide == null || cuts == null) { yield return Speak(HS.S("129-147") + "|" + hint); if (guide != null) Destroy(guide); yield break; }

        // A cutscene on the silo's cutscene camera (its letterbox, player lock, hold-SPACE skip; a checkpoint retry
        // aborts it, and every shot below stops when it has):
        //   129      his voice, off screen, while you stare at the nearest Lost Soul: "Don't stare at them."
        //   130-145  round to Tomas, a slow push in on him
        //   146-147  the depot's door down the street, and the objective comes up over it
        Transform ct = cuts.cam.transform, head = cuts.playerHead != null ? cuts.playerHead : player;
        Vector3 eye = head.position;
        Bounds body = new Bounds(guide.transform.position + Vector3.up, Vector3.one);
        foreach (var r in guide.GetComponentsInChildren<Renderer>()) body.Encapsulate(r.bounds);
        Vector3 face = new Vector3(guide.transform.position.x, body.max.y - .2f, guide.transform.position.z);
        var soul = RevisedPlot.All<LostSoul>(gameObject.scene).Where(s => s.isActiveAndEnabled && (s.transform.position - eye).magnitude < 60f)
            .OrderBy(s => (s.transform.position - eye).magnitude).FirstOrDefault();
        var fader = RevisedPlot.All<SleepSequence>(gameObject.scene).Select(s => s.fader).FirstOrDefault(f => f != null);
        var door = RevisedPlot.All<Transform>(gameObject.scene).FirstOrDefault(t => t.name == "OfficeEntrance (City)");

        cuts.Begin();
        CutsceneHUD.Hide(true);
        ct.SetPositionAndRotation(eye, head.rotation);
        // (each shot runs until its lines are read; `move` gets the seconds it has been up)
        IEnumerator Shot(string lines, Action<float> move)
        {
            Say(lines);
            yield return null;
            for (float t = 0f; cuts.Playing && DialogueBox.Instance != null && DialogueBox.Instance.Busy; t += Time.deltaTime) { move(t); yield return null; }
        }
        void Turn(Vector3 to, float speed) => ct.rotation = Quaternion.Slerp(ct.rotation, Quaternion.LookRotation(to - ct.position), 1f - Mathf.Exp(-speed * Time.deltaTime));

        if (soul != null) yield return Shot(HS.S("129"), t => Turn(soul.transform.position + Vector3.up * 1.2f, 1.5f));
        else yield return Shot(HS.S("129"), t => { });
        Vector3 side = Vector3.Cross(Vector3.up, face - eye).normalized * .45f;
        if (cuts.Playing) yield return Shot(HS.S("130-145"), t =>
        {
            ct.position = Vector3.Lerp(eye, Vector3.Lerp(eye, face, .45f) + side, 1f - Mathf.Exp(-t * .2f));
            Turn(face, 6f);
        });

        if (cuts.Playing && door != null)
        {
            if (fader != null) yield return fader.FadeTo(1f, .4f);
            // from out in the street, above head height, looking back at the door and its sign (never from inside a wall)
            Vector3 sign = door.TransformPoint(0f, 4.5f, 0f), from = door.TransformPoint(5f, 5.5f, 20f);
            if (Physics.Linecast(sign + door.forward * .6f, from, out RaycastHit wall, ~0, QueryTriggerInteraction.Ignore)) from = wall.point - (from - sign).normalized * .5f;
            ct.SetPositionAndRotation(from, Quaternion.LookRotation(sign - from));
            if (fader != null) StartCoroutine(fader.FadeTo(0f, .5f));
            Vector3 push = (sign - from).normalized * .3f;
            yield return Shot(HS.S("146-147"), t => ct.position = from + push * Mathf.Min(t, 6f));
            ObjectiveUI.OverCutscene = true;   // "Go to the depot" is announced over the picture of it
            for (float t = 0f; t < 3.6f && cuts.Playing; t += Time.deltaTime) { ct.position += push * Time.deltaTime; yield return null; }
            if (cuts.Playing && fader != null) yield return fader.FadeTo(1f, .4f);
        }
        else if (cuts.Playing) yield return Shot(HS.S("146-147"), t => Turn(face, 6f));

        bool finished = cuts.Playing;   // (false: a retry took the camera back already and put you at the checkpoint)
        ObjectiveUI.OverCutscene = false;
        CutsceneHUD.Hide(false);
        Destroy(guide);
        if (!finished) yield break;
        if (door != null)
        {
            // back in your own eyes, facing the way to the depot
            Vector3 way = door.position - player.position; way.y = 0f;
            if (way.sqrMagnitude > 1f) player.rotation = Quaternion.LookRotation(way);
            var fpc = player.GetComponent<FirstPersonController>();
            if (fpc != null) fpc.SetPitch(0f);
        }
        cuts.End();
        if (fader != null) StartCoroutine(fader.FadeTo(0f, .6f));
        Say(hint);
    }

    bool journalOpen;
    readonly Dictionary<GhostNPC, bool> echoStates = new Dictionary<GhostNPC, bool>();

    void Update()
    {
        if ((horror != null && horror.Switching) || player == null || gameObject.scene.name != "MainGameScene" || TitleScreen.Showing || PauseMenu.Paused || GameOverScreen.Open || EndingSequence.Playing || InSequence || NoteViewer.Blocking || GhostNPC.Conversing) return;
        bool nearOffice = office != null && OfficeHorrorDirector.In(player.position);
        if (Echo && !nearOffice) SetEcho(false);
        if (journalOpen && Input.GetKeyDown(KeyCode.J)) { ChoiceDialogue.Instance.Close(); journalOpen = false; return; }
        if (ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open || DialogueBox.Instance != null && DialogueBox.Instance.Busy) return;
        if (Input.GetKeyDown(KeyCode.J)) ShowJournal();
        if (Input.GetKeyDown(KeyCode.V) && nearOffice && !(confrontation != null && confrontation.Ambushing) && !VaneReleased && (!Echo || puzzles == null || puzzles.CanSwitch(player)))
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
        if (vane != null && !VaneReleased && !(confrontation != null && confrontation.Armed)) vane.gameObject.SetActive(on);
        if (on && !echoVisited)
        {
            echoVisited = true;
            Say(HS.S("286-287") + "|*The Echo. Six seconds, then it pulls you back.*");
        }
    }

    public void ReleaseVane()
    {
        SetEcho(false);
        VaneReleased = true;
        foreach (var door in RevisedPlot.All<KesslerDoor>(gameObject.scene)) door.ReleaseForEscape();
        if (vane != null) vane.gameObject.SetActive(true);
        if (hallwayFigure != null) hallwayFigure.SetActive(false);
    }

    IEnumerator PhoneReplay()
    {
        phoneSeen = true;
        EvidenceJournal.Collect(EvidenceJournal.Phone);
        if (hallwayFigure != null) { hallwayFigure.SetActive(true); hallwayFigure.GetComponent<SoulConviction>().Exert(); }
        // ([disconnect], HS_0302, is a sound the project doesn't have yet: the narration stands in for it)
        yield return Speak(HS.S("297-301") + "|*The line goes dead.*|" + HS.S("303-304,314"));
        if (tomas != null) tomas.GetComponent<SoulConviction>().Exert(.6f);
        if (hallwayFigure != null) hallwayFigure.SetActive(false);
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

    public IEnumerator AmalgamationAccounts(DemonBoss boss)
    {
        if (EvidenceJournal.VaneConfessed) yield break;
        InSequence = true;
        yield return WaitForDialogue();
        var required = new[] { EvidenceJournal.Statement, EvidenceJournal.Tape, EvidenceJournal.Invoice };
        // These are critical-path facts. Never invent possession of a skipped clue.
        foreach (string fact in required)
        {
            string[] options = EvidenceJournal.Has(fact) ? new[] { "Present " + fact, "Ask about what I witnessed" } : new[] { "Ask about what I witnessed" };
            int selected = 0;
            yield return Choose("Vane", fact == EvidenceJournal.Invoice ? "That clinic was just another supplier." : "I did what the business needed.", options, i => selected = i);
            bool shown = EvidenceJournal.Has(fact) && selected == 0;
            if (fact == EvidenceJournal.Statement) yield return Speak(shown ? HS.S("536-539") : HS.S("540-542"));
            else if (fact == EvidenceJournal.Tape) yield return Speak(shown ? HS.S("543-546") : HS.S("547-549"));
            else yield return Speak(shown ? HS.S("550-553") : HS.S("554-557"));
        }
        yield return Speak(HS.S("558-563"));
        EvidenceJournal.VaneConfessed = true;
        // His monstrous shell will sink in the existing finale; the confessed soul separates first.
        GameObject soul = vane != null ? Visual(vane.body != null ? vane.body : vane.transform, "Vane confessed", boss.transform.position + boss.transform.forward * 3f) : null;
        if (soul != null) soul.AddComponent<SoulConviction>().Confess();
        yield return Speak(HS.S("564-569"));
        EvidenceJournal.TomasDamned = true;
        if (soul != null) Destroy(soul);
        EvidenceJournal.Record("Tomas's amalgamation", "Tomas used Elias to kill, then bound Vane and Maya's ruined Damned bodies to himself. He wanted the mark to reach Kessler. Breaking the joined body freed three separate souls for judgement; it did not absolve any of them.");
        yield return Speak(HS.S("570-576"));
        InSequence = false;
    }

    public IEnumerator PlayerConfession()
    {
        InSequence = true;
        yield return Speak(HS.S("582-588"));
        var answers = new List<string> { "I signed a statement I knew I had not witnessed.", "I pushed Elias off the roof. I wanted him dead." };
        var accounts = new List<string> { HS.S("590-594"), HS.S("595-598") };
        if (EvidenceJournal.ContainerInspected) { answers.Add("I read the label and still carried the box."); accounts.Add(HS.S("599-602")); }
        answers.Add("None of this was my fault."); accounts.Add(HS.S("603-607"));
        int picked = -1;
        yield return Choose("Your account", HS.T(589), answers.ToArray(), i => picked = i);
        EvidenceJournal.PlayerConfessed = picked >= 0 && picked < answers.Count - 1;
        yield return Speak(accounts[Mathf.Clamp(picked, 0, accounts.Count - 1)]);
        InSequence = false;
    }

    // Script 8.5, after the end card: a black screen and a telephone call, nothing to see. The second voice's name plate
    // is whatever its CSV row says (UNKNOWN), so it cannot read DEREK before Kessler has said the name (CallReveal).
    // [Enter] skips the rest once the name is out. Timings are on EndingSequence, in the Inspector.
    // HS-rows: 642-650,652
    public static readonly int[] CallRows = { 642, 643, 644, 645, 646, 647, 648, 649, 650, 652 };   // (652: Derek's "What?" came last)
    public const int CallReveal = 648;
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

        foreach (int id in CallRows)
        {
            if (skip) break;
            yield return Hold(id == CallReveal ? revealPause : linePause);
            if (skip) break;
            Step("line " + id);
            if (box.blip != null) box.blip.mute = Sounds.Has(CallVoice + id);
            Sounds.OneShot(phone, CallVoice + id, null);
            box.Say(HS.S(id.ToString()));
            yield return null;
            while (box.Busy && !skip) { skip = revealed && SkipCall; yield return null; }
            revealed |= id == CallReveal;
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
