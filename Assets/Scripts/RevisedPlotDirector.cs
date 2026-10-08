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
    Light echoLight;
    EchoOfficeMemory officeMemory;
    OfficeRealmPuzzles puzzles;
    OfficeHorrorDirector horror;
    EchoVisit echoVisit;
    GameObject hallwayFigure, rifleSetup;
    readonly List<Behaviour> locked = new List<Behaviour>();
    readonly List<Material> materials = new List<Material>();
    readonly List<AudioClip> clips = new List<AudioClip>();
    Color fog;
    float density;
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
            note.body = EvidenceJournal.Account(EvidenceJournal.Invoice) + "\n\n05:00 dispatch instruction. Return sealed container to depot collection.\nKEEP COLD. Approval V-0500.";
            note.afterLine = "The same box I brought back at seven. Here it's frosted with blood.|Vane approved the clinic's work. Evidence recorded - [J] to review.";
            AddEvidence(item, EvidenceJournal.Invoice);
            Vector3 phoneAt = vane.transform.position + vane.transform.right * 1.3f + Vector3.up * .85f;
            var phone = Box("Vane's Echo telephone", phoneAt, new Vector3(.42f, .16f, .28f), Color.black);
            Label(phone.transform, "05:00 / REPLAY", new Vector3(0f, .05f, -.505f), .02f);
            var use = phone.AddComponent<Interactable>(); use.once = false; use.prompt = "Replay Vane's call in the Echo";
            use.onUse.AddListener(() => { if (Echo && !phoneSeen) StartCoroutine(PhoneReplay()); else if (!Echo) Say("An impression of a phone. Cross into the Echo with [V]."); else Say(EvidenceJournal.Account(EvidenceJournal.Phone)); });
            var lamp = new GameObject("Echo light"); lamp.transform.SetParent(transform); lamp.transform.position = phoneAt + Vector3.up * 2f;
            echoLight = lamp.AddComponent<Light>(); echoLight.color = new Color(.64f, .8f, 1f); echoLight.range = 45f; echoLight.intensity = 3f; echoLight.enabled = false;
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
            var car = Box("Vane's idling car", p + Vector3.up * .55f, new Vector3(1.8f, .8f, 3.9f), new Color(.13f, .15f, .18f));
            Box("Cab", p + Vector3.up * 1.13f, new Vector3(1.55f, .65f, 2f), new Color(.07f, .11f, .14f), car.transform);
            foreach (float x in new[] { -.87f, .87f }) foreach (float z in new[] { -1.25f, 1.25f })
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder); wheel.name = "Wheel"; wheel.transform.SetParent(transform);
                wheel.transform.position = p + new Vector3(x, .35f, z); wheel.transform.rotation = Quaternion.Euler(0f, 0f, 90f); wheel.transform.localScale = new Vector3(.65f, .14f, .65f);
                wheel.GetComponent<Renderer>().sharedMaterial = Material(Color.black);
            }
            Loop(car, ProceduralAudio.ScooterEngine(), .12f, 12f);
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
            item.line = "KEEP COLD. Harlow Clinic. Approved V-0500. Vane's code. I'm carrying it anyway.";
            var clue = box.AddComponent<EvidenceClue>(); clue.prologueLabel = true;
            run.stops[1].customer.onUse.AddListener(() => box.SetActive(false));
        }
        var killer = RevisedPlot.All<FleeingKiller>(gameObject.scene).FirstOrDefault();
        if (killer != null)
        {
            foreach (var t in killer.GetComponentsInChildren<Transform>(true))
                if (t != killer.transform && (t.name.IndexOf("rifle", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("AK47", StringComparison.OrdinalIgnoreCase) >= 0)) t.gameObject.SetActive(false);
            Transform hand = killer.animator != null && killer.animator.isHuman ? killer.animator.GetBoneTransform(HumanBodyBones.RightHand) : killer.transform;
            if (hand != null)
            {
                var knife = Box("Elias's knife", hand.position, new Vector3(.035f, .26f, .055f), new Color(.55f, .57f, .59f));
                knife.transform.SetParent(hand, false); knife.transform.localPosition = new Vector3(0f, -.1f, .03f);
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
        yield return Speak("Maya: [Dispatch, 23:04] Okafor on Dunmore first, then the Harlow collection. Sam's loaded you.|Sam: [Recording] Bring it back in one piece this time.|Maya: [Dispatch, 23:04] And if Vane asks you to take a box with no manifest, just--");
        audio.Stop();
        yield return Speak("*6:40 AM. The recording cuts out mid-sentence.*|Maya? Dispatch?|...Nothing. Just hiss.");
    }

    IEnumerator OpeningMark()
    {
        introDone = true;
        yield return new WaitForSeconds(.4f);
        var ending = RevisedPlot.All<EndingSequence>(gameObject.scene).FirstOrDefault();
        if (ending != null && ending.markedSkin != null && player != null)
            foreach (var r in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name.IndexOf("arm", StringComparison.OrdinalIgnoreCase) >= 0) r.material.mainTexture = ending.markedSkin;
        yield return WaitForDialogue();
        if (player == null) yield break;
        GameObject guide = tomas != null ? Visual(tomas.body != null ? tomas.body : tomas.transform, "Tomas finds the marked driver", player.position + player.forward * 3f - Vector3.up) : null;
        if (guide != null) { guide.transform.LookAt(new Vector3(player.position.x, guide.transform.position.y, player.position.z)); guide.AddComponent<SoulConviction>().Exert(.3f); }
        yield return Speak("Tomas: That beating isn't your heart. It's the mark. The Damned can hear it.|Tomas: I need you to take me to Vane. He's hiding in the Echo. The dead can't follow him there without a mark like yours.|Vane's dead and he's still hiding.|Tomas: In the office, press [V] to cross into the Echo. Press [J] to read your evidence.|Tomas: And be careful with excuses. They sound a lot like confessions.");
        if (guide != null) Destroy(guide);
    }

    bool journalOpen;
    readonly Dictionary<GhostNPC, bool> echoStates = new Dictionary<GhostNPC, bool>();

    void Update()
    {
        if ((horror != null && horror.Switching) || player == null || gameObject.scene.name != "MainGameScene" || TitleScreen.Showing || PauseMenu.Paused || GameOverScreen.Open || EndingSequence.Playing || InSequence || NoteViewer.Blocking || GhostNPC.Conversing) return;
        bool nearOffice = office != null && Mathf.Abs(player.position.y - office.position.y) < 20f && Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(office.position.x, office.position.z)) < 65f;
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
        if (on) { fog = RenderSettings.fogColor; density = RenderSettings.fogDensity; RenderSettings.fogColor = new Color(.48f, .55f, .62f); RenderSettings.fogDensity = .007f; }
        else { RenderSettings.fogColor = fog; RenderSettings.fogDensity = density; }
        if (echoLight != null) echoLight.enabled = on;
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
            Say("*The mark opens the Echo. Same collection box. Same office, before the blood. Six seconds, then the mark pulls me back. Vane can hold it while we talk.*|*The Echo shows their souls. Question them before it fades. Denial loses them; excuses damn them; accepting their own actions calms them.*|*The paper evidence will remain even if a witness is lost.*");
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
        yield return Speak("Vane: [05:00 replay] Arthur. The Rourke brother's on his way.|Kessler: Then you'd better not be there.|*The line goes dead.*|Tomas: You signed it. You SIGNED it.|That voice on the phone...|*The silhouette at the end of the hall is flickering red.*");
        if (tomas != null) tomas.GetComponent<SoulConviction>().Exert(.6f);
        if (hallwayFigure != null) hallwayFigure.SetActive(false);
    }

    void ShowJournal()
    {
        var choice = ChoiceDialogue.Instance;
        if (choice == null) return;
        var entries = EvidenceJournal.Found;
        if (entries.Length == 0) { Say("*No evidence recorded yet. Inspect the collection manifest, shift statement and ledger. Watch the CCTV. Replay Vane's phone in the Echo.*"); return; }
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
            if (fact == EvidenceJournal.Statement) yield return Speak("I signed your statement. You filed it as a fall.|Vane: A false report. I made every night worker sign it.");
            else if (fact == EvidenceJournal.Tape) yield return Speak("I saw the tape. You threatened us both.|Vane: I ordered Derek to kill Tomas. He was protecting his brother. I used that.");
            else yield return Speak(EvidenceJournal.Has(fact) && selected == 0 ? "Your code is on the Harlow invoice. What did you pay for?|Vane: The clinic. To process the bodies. To make Tomas disappear." : "I collected that box on your order. What was in it?|Vane: What remained of Tomas. I sent you to carry it.");
        }
        yield return Speak("Vane: Kessler told me--|I'm not asking about Kessler.|Vane: ...I chose it. I ordered the murder. I sold the bodies. No excuse.|*Vane's voice separates from the others. The flesh at his neck loosens.*");
        EvidenceJournal.VaneConfessed = true;
        // His monstrous shell will sink in the existing finale; the confessed soul separates first.
        GameObject soul = vane != null ? Visual(vane.body != null ? vane.body : vane.transform, "Vane confessed", boss.transform.position + boss.transform.forward * 3f) : null;
        if (soul != null) soul.AddComponent<SoulConviction>().Confess();
        yield return Speak("Maya: Tomas trusted me. I told Vane where he was. I knew.|Tomas: Don't listen. They're mine.|You gave Elias the names. Then you took these two for yourself.|Tomas: They deserved it.|The Demon King: Their sins are their own, Tomas. The chains are yours. Loose them.");
        EvidenceJournal.TomasDamned = true;
        if (soul != null) Destroy(soul);
        EvidenceJournal.Record("Tomas's amalgamation", "Tomas used Elias to kill, then bound Vane and Maya's ruined Damned bodies to himself. He wanted the mark to reach Kessler. Breaking the joined body freed three separate souls for judgement; it did not absolve any of them.");
        yield return Speak("Tomas: They won't leave me again.|The Demon King: The scales are not yours to hold.|*The mark burns. The seams split. Three souls, no longer one.*");
        InSequence = false;
    }

    public IEnumerator PlayerConfession()
    {
        InSequence = true;
        yield return Speak("The Demon King: I did not send thee to make Vane speak. I sent thee to hear thyself in him.|The Demon King: Thy brother's knife. Elias's rifle. And thy hands, driver. What did they hold?");
        var answers = new List<string> { "I signed a statement I knew I hadn't witnessed.", "I pushed Elias off the roof. I wanted him dead." };
        if (EvidenceJournal.ContainerInspected) answers.Add("I read the container label and carried it anyway.");
        answers.Add("None of this was my fault.");
        int picked = -1;
        yield return Choose("Your account", "Speak for yourself.", answers.ToArray(), i => picked = i);
        EvidenceJournal.PlayerConfessed = picked >= 0 && picked < answers.Count - 1;
        yield return Speak(EvidenceJournal.PlayerConfessed
            ? "I signed for an accident I never saw. I carried a box I should have questioned.|I pushed Elias because I wanted him dead. Nobody made me."
            : "They made me. Derek. Vane. Elias. I had no choice.|The Demon King: Even now, another's name sits where thine should be.");
        InSequence = false;
    }

    public IEnumerator PostCredits()
    {
        yield return Speak("*Somewhere in the living city.*|Unseen Voice: Vane talked.|Kessler: About himself.|Unseen Voice: The marked driver can reach the Echo.|Kessler: Then we leave.");
        // The unseen witness is Leon Vale, Kessler's first murdered bookkeeper; intentionally unnamed on screen.
    }

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
