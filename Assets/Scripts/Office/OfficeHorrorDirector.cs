using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The office act as a horror state that gets worse every time you get something right.
//   stage 0  creepy but stable: hum, burnt bodies that do not move, a PA with normal announcements
//   stage 1  fuse 1   lights stutter; for a second, everyone is at their desk, facing away
//   stage 2  fuse 2   phones ring, a printer prints; one body is gone
//   stage 3  fuse 3   the Auditor starts watching from the ends of corridors
//   stage 4  keypad   the Echo breaks down: footsteps, people where nobody should be, a burnt figure inside it
//   stage 5  CCTV     the cameras lie, the lights trip to red, every phone rings, the Auditor hunts you
// until Kessler's turn takes over (the Auditor withdraws) and you are out through the silo door.
// Most of it only happens in the red city: the Echo stays the safe, clean memory, until stage 3-4 corrupt it.
public class OfficeHorrorDirector : MonoBehaviour, ISceneProgress
{
    public int Stage { get; private set; }
    public int Corruption => Mathf.Max(0, Stage - 2);   // 0 safe .. 3 the Echo is hunted
    public OfficeStalker Auditor => stalker;
    OfficeColleagues colleaguesAct;

    RevisedPlotDirector plot;
    Transform player; Camera cam; FirstPersonController fpc; PlayerHealth health;
    OfficeStalker stalker, visitor;
    OfficeBodies bodies;
    SoulConviction tomas;   // the one ghost that is not burnt: it is the first thing that is wrong
    KesslerConfrontation conf;
    SequencePuzzle[] tapes;
    AudioSource bed, pa, sfx;
    AudioClip humRed, humEcho, crackle, ring, beat, stepClip;
    Transform photo; GameObject photoBrother;
    Image overlay;
    bool echo, ended, switching, longSwitchDone, breakdown, busyEvent, withdrawn;
    int evidenceReactions;
    OfficeCctvMemory cctv;
    RealmBlink blink;
    public bool Switching => switching;
    public object CaptureProgress() => Stage;
    public void RestoreProgress(object state) => Advance((int)state);
    int pendingShifts, paIndex, answers, prints;
    float bedBase = .22f, hush, paTimer = 55f, scareTimer = 40f, footTimer = 30f, boxTimer;
    readonly Queue<Func<IEnumerator>> redEvents = new Queue<Func<IEnumerator>>();
    readonly List<DeskPhone> phones = new List<DeskPhone>();
    readonly List<Transform> mugs = new List<Transform>();
    Interactable printer;
    TextMeshPro scratch, clock;
    Transform box, deskNote;
    AudioSource heart;
    GameObject spill;

    // office lights, driven only while an effect is on
    Light[] lights; float[] baseI; Color[] baseC;
    float lightMul = 1f, redMix; bool lightsDirty;

    class DeskPhone { public Transform t; public AudioSource ring; public Interactable use; public bool ringing; }

    // the office = the zone of its InteriorDarkness (OfficeAct/Office), so it follows the model wherever it stands
    static Bounds zone = new Bounds(new Vector3(1015.5f, 5f, 899.4f), new Vector3(106f, 18f, 77f));
    public static bool In(Vector3 p) => zone.Contains(p);
    public static Bounds Zone => zone;
    bool InOffice => player != null && In(player.position);
    bool Safe => fpc != null && fpc.enabled && !GhostNPC.Conversing && !PauseMenu.Paused && !TitleScreen.Showing && !GameOverScreen.Open
        && !NoteViewer.Blocking && !(conf != null && conf.Ambushing) && (plot == null || !plot.InSequence)
        && (DialogueBox.Instance == null || (!DialogueBox.Instance.CardOpen && (!DialogueBox.Instance.Busy || DialogueBox.Instance.ActionSpeech))) && (ChoiceDialogue.Instance == null || !ChoiceDialogue.Instance.Open) && !EndingSequence.Playing;

    // ---------------------------------------------------------------- setup

    public void Init(RevisedPlotDirector director)
    {
        plot = director;
        var scene = gameObject.scene;
        fpc = RevisedPlot.All<FirstPersonController>(scene).FirstOrDefault2();
        player = fpc != null ? fpc.transform : null;
        cam = player != null ? player.GetComponentInChildren<Camera>() : null;
        health = player != null ? player.GetComponentInParent<PlayerHealth>() ?? player.GetComponentInChildren<PlayerHealth>() : null;
        if (player == null || cam == null) { enabled = false; return; }
        foreach (var d in RevisedPlot.All<InteriorDarkness>(scene)) if (d.name == "Office") zone = d.zone;
        gameObject.AddComponent<OfficeMinimap>().Init(player, zone);

        GhostNPC male = null, female = null;
        var colleagues = new List<GhostNPC>();
        foreach (var g in RevisedPlot.All<GhostNPC>(scene))
        {
            if (g.ghostName == "Ben") male = g;
            if (g.ghostName == "Priya") female = g;
            if (g.ghostName == "Tomas")
            {
                tomas = g.GetOrAdd<SoulConviction>();
                tomas.Solidify(WitnessLook.Load("IntactMale"), new Color(.92f, .92f, .95f, 1f));
            }
            // burnt look for the witnesses (Tomas keeps his own, nearly intact; Vane is Kessler's stand-in)
            if ((g.ghostName == "Priya" || g.ghostName == "Ben" || g.ghostName == "Nadia" || g.ghostName == "Omar") && g.GetComponent<WitnessLook>() == null)
            {
                g.gameObject.AddComponent<WitnessLook>();
                if (g.GetComponent<PainWalk>() == null) g.gameObject.AddComponent<PainWalk>().roam = 0f;   // burnt, not judged yet, in pain: standing, not wandering (OfficeColleagues)
                colleagues.Add(g);
            }
        }
        // (the burnt bodies and the Auditor are copies of the old humanoid models, kept inactive on Ben and Priya as "Body template")
        Transform Tpl(GhostNPC g) => g == null ? null : g.transform.Find("Body template") ?? g.body ?? g.transform;
        Transform mt = Tpl(male), ft = female != null ? Tpl(female) : mt;

        bodies = gameObject.AddComponent<OfficeBodies>();
        bodies.Build(player, cam, mt, ft);
        stalker = MakeStalker("Auditor", mt, "AuditorSkin");
        visitor = MakeStalker("Echo visitor", mt, "IntactMale");
        visitor.canAct = () => Safe && InOffice && echo && Corruption >= 1;

        humRed = Gen("OfficeHum", 2f, (t, n) => Mathf.Sin(t * 628.3f) * .14f + Mathf.Sin(t * 942f) * .05f + n * .03f);
        float lp = 0f;
        humEcho = Gen("OfficeAir", 2f, (t, n) => { lp += (n - lp) * .06f; return lp * .55f + Mathf.Sin(t * 754f) * .03f; });
        crackle = Gen("PACrackle", .7f, (t, n) => n * Mathf.Exp(-t * 4f) * (Mathf.Sin(t * 60f) > -.3f ? 1f : .15f) * .5f);
        ring = Gen("PhoneRing", 3f, (t, n) => (t % 1.5f) < .9f ? Mathf.Sin(t * 2765f) * Mathf.Sin(t * 120f) * Mathf.Sin(t * 2985f + 1f) * .35f : 0f);
        beat = Gen("BoxHeart", 1.1f, (t, n) => (Mathf.Exp(-t * 14f) + (t > .24f ? Mathf.Exp(-(t - .24f) * 14f) * .7f : 0f)) * Mathf.Sin(t * 345f) * .8f);

        bed = Source("Office hum", false); bed.clip = humRed; bed.loop = true; bed.volume = bedBase; bed.Play();
        pa = Source("Office PA", false); sfx = Source("Office sfx", true);
        heart = Source("Box heart", true); heart.clip = beat; heart.loop = true; heart.volume = 0f; heart.maxDistance = 9f;

        var canvas = new GameObject("Office horror overlay").AddComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 400;
        overlay = new GameObject("flash").AddComponent<Image>();
        overlay.transform.SetParent(canvas.transform, false);
        var rt = overlay.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        overlay.color = Color.clear; overlay.raycastTarget = false;

        var all = new List<Light>();
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (l.type != LightType.Directional && In(l.transform.position) && l.transform.position.y > 3f && !l.transform.IsChildOf(player.root)) all.Add(l);
        lights = all.ToArray(); baseI = new float[lights.Length]; baseC = new Color[lights.Length];
        for (int i = 0; i < lights.Length; i++) { baseI[i] = lights[i].intensity; baseC[i] = lights[i].color; }

        foreach (var f in RevisedPlot.All<FuseBox>(scene)) if (f.counter != null) f.counter.onProgress += n => Advance(Mathf.Min(n, 3));
        foreach (var k in RevisedPlot.All<Keypad>(scene)) k.onSolved.AddListener(() => Advance(4));
        tapes = RevisedPlot.All<SequencePuzzle>(scene);
        foreach (var t in tapes) { t.onStep += OnTapeStep; t.onSolved.AddListener(OnTapeSolved); }
        conf = RevisedPlot.All<KesslerConfrontation>(scene).FirstOrDefault2();

        foreach (var i in RevisedPlot.All<Interactable>(scene)) if (i.name == "YourDesk_Note") deskNote = i.transform;
        BuildProps(scene);
        gameObject.AddComponent<VaneHunt>().Init(this, player, cam, fpc, health, mt, conf != null && conf.damned != null ? conf.damned.gameObject : null,
            () => Safe && InOffice && !echo && !withdrawn && !ended, () => InOffice && !echo && !withdrawn && !ended);
        colleaguesAct = gameObject.AddComponent<OfficeColleagues>(); colleaguesAct.Init(colleagues, player, cam, stalker, () => Safe && InOffice && !echo && !withdrawn && !ended);
        blink = gameObject.AddComponent<RealmBlink>(); blink.Initialize(player);
        cctv = gameObject.AddComponent<OfficeCctvMemory>(); cctv.Initialize(tapes, player, cam, mt);
        EvidenceJournal.Collected += OnEvidence;
    }

    OfficeStalker MakeStalker(string name, Transform template, string material)
    {
        var s = new GameObject(name).AddComponent<OfficeStalker>();
        s.transform.SetParent(transform, false);
        s.player = player; s.cam = cam; s.health = health;
        s.xRange = new Vector2(zone.min.x, zone.max.x); s.zRange = new Vector2(zone.min.z, zone.max.z);
        s.canAct = () => Safe && InOffice && !echo;
        s.Build(template, material);
        s.enabled = false;
        return s;
    }

    AudioSource Source(string name, bool spatial)
    {
        var a = new GameObject(name).AddComponent<AudioSource>();
        a.transform.SetParent(spatial ? transform : player, false);
        a.playOnAwake = false; a.spatialBlend = spatial ? 1f : 0f; a.rolloffMode = AudioRolloffMode.Linear; a.maxDistance = 25f;
        return a;
    }

    static AudioClip Gen(string name, float seconds, Func<float, float, float> f)
    {
        int rate = 22050, n = (int)(rate * seconds);
        var data = new float[n];
        var rng = new System.Random(name.GetHashCode());
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)rate, (float)(rng.NextDouble() * 2 - 1)), -1f, 1f);
        var c = AudioClip.Create(name, n, 1, rate, false);
        c.SetData(data, 0);
        return c;
    }

    // ---------------------------------------------------------------- props: phones, printer, desk, clock, mugs

    bool Surface(Vector3 near, out Vector3 at)
    {
        at = near;
        if (!Physics.Raycast(near + Vector3.up * .6f, Vector3.down, out var h, 1.4f, ~0, QueryTriggerInteraction.Ignore) || h.normal.y < .9f) return false;
        at = h.point; return true;
    }

    Transform Prop(string name, Vector3 at, Vector3 size, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(transform, true);
        go.transform.position = at + Vector3.up * size.y * .5f; go.transform.localScale = size;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = c };
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go.transform;
    }

    void BuildProps(UnityEngine.SceneManagement.Scene scene)
    {
        Transform ledger = null, report = null;
        foreach (var i in RevisedPlot.All<Interactable>(scene)) { if (i.name == "KesslerLedger") ledger = i.transform; if (i.name == "IncidentReport") report = i.transform; }
        var spots = new List<Vector3>();
        if (deskNote != null) spots.Add(deskNote.position + new Vector3(.6f, 0f, .15f));
        if (ledger != null) spots.Add(ledger.position + new Vector3(-.9f, 0f, 0f));
        if (deskNote != null) spots.Add(deskNote.position + new Vector3(-.7f, 0f, -.2f));
        foreach (var s in spots)
            if (Surface(s, out var at))
            {
                var t = Prop("Office phone", at, new Vector3(.22f, .09f, .2f), new Color(.08f, .08f, .09f));
                var a = t.gameObject.AddComponent<AudioSource>();
                a.clip = ring; a.loop = true; a.spatialBlend = 1f; a.maxDistance = 22f; a.rolloffMode = AudioRolloffMode.Linear; a.volume = .8f;
                var use = t.gameObject.AddComponent<Interactable>();
                use.prompt = "Answer the phone"; use.once = false; use.enabled = false;
                var p = new DeskPhone { t = t, ring = a, use = use };
                use.onUse.AddListener(() => Answer(p));
                phones.Add(p);
            }
        if (report != null && Surface(report.position + new Vector3(-1.2f, 0f, 0f), out var pat))
        {
            var t = Prop("Office printer", pat, new Vector3(.5f, .32f, .42f), new Color(.2f, .2f, .22f));
            printer = t.gameObject.AddComponent<Interactable>();
            printer.prompt = "Check the printer"; printer.once = false;
            printer.onUse.AddListener(PrinterRead);
        }
        if (deskNote != null)
        {
            if (Surface(deskNote.position + new Vector3(.35f, 0f, 0f), out var photoAt))
            {
                photo = Prop("Derek and me - desk photograph", photoAt, new Vector3(.28f, .2f, .035f), new Color(.18f, .14f, .1f));
                var note = photo.gameObject.AddComponent<ReadableNote>(); note.title = "DEREK AND ME";
                note.body = "Two brothers beside the delivery van. Before the signatures. Before the fall.";
                var use = photo.gameObject.AddComponent<Interactable>(); use.once = false; use.prompt = "Look at the photograph";
                for (int person = 0; person < 2; person++) {
                    var silhouette = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(silhouette.GetComponent<Collider>());
                    silhouette.name = person == 0 ? "Derek in the photograph" : "Your scratched-out portrait";
                    silhouette.transform.SetParent(photo, false); silhouette.transform.localPosition = new Vector3(person == 0 ? -.22f : .22f, 0f, -.52f);
                    silhouette.transform.localScale = new Vector3(.28f, .7f, .1f);
                    silhouette.GetComponent<Renderer>().sharedMaterial = WitnessLook.Load("IntactMale");
                    if (person == 1) photoBrother = silhouette;
                }
                if (photoBrother != null) photoBrother.SetActive(false);
            }
            if (Surface(deskNote.position + new Vector3(0f, 0f, -.5f), out var cat))
            {
                var t = Prop("Desk clock", cat, new Vector3(.2f, .12f, .06f), new Color(.1f, .1f, .1f));
                clock = Text(t, new Vector3(0f, .0f, -.034f), .1f, "06:15", new Color(.6f, .9f, 1f));
                clock.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                t.gameObject.SetActive(false);
            }
            if (Surface(deskNote.position + new Vector3(.1f, 0f, .45f), out var sat))
            {
                var holder = new GameObject("Desk scratch").transform;
                holder.SetParent(transform, true); holder.position = sat + Vector3.up * .01f; holder.rotation = Quaternion.Euler(90f, 0f, 0f);
                scratch = Text(holder, Vector3.zero, .07f, "", new Color(.45f, .02f, .02f));
                scratch.transform.localRotation = Quaternion.identity;
            }
            for (int i = 0; i < 4; i++)
            {
                var m = Prop("Echo mug", deskNote.position + Vector3.up * -5f, new Vector3(.08f, .1f, .08f), new Color(.9f, .9f, .85f));
                m.gameObject.SetActive(false); mugs.Add(m);
            }
        }
    }

    TextMeshPro Text(Transform parent, Vector3 local, float size, string text, Color c)
    {
        var go = new GameObject("text");
        go.transform.SetParent(parent, false); go.transform.localPosition = local;
        var t = go.AddComponent<TextMeshPro>();
        var pixel = GameFont.Main;
        if (pixel != null) t.font = pixel;
        t.text = text; t.fontSize = size * 10f; t.color = c; t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = new Vector2(2f, .4f);
        return t;
    }

    // ---------------------------------------------------------------- progression

    public void Advance(int s)
    {
        if (s <= Stage || ended) return;
        Stage = s;
        foreach (var g in RevisedPlot.All<GhostNPC>(gameObject.scene)) g.working = false;   // the first fuse wakes them: they leave their desks
        stalker.progress = Mathf.Clamp01(Stage / 5f);
        pendingShifts++;
        hush = 1f;   // the room holds its breath for a moment
        if (tomas != null) { tomas.soot = Stage / 5f; if (Stage >= 3) tomas.Exert(.25f); }   // soot at his edges; a red flicker when he is angry
        switch (s)
        {
            case 1: redEvents.Enqueue(DesksEvent); break;
            case 2: redEvents.Enqueue(PhonesEvent); break;
            case 3: stalker.enabled = true; visitor.enabled = true; Say(HS.S("268")); redEvents.Enqueue(AuditorIntro); break;
        }
        if (s == 4) { visitor.progress = .5f; FlashOverlay(new Color(.4f, .55f, .8f), .5f, .6f); Say(HS.S("284-285")); }
        RefreshDesk();
    }

    public void OnTapeStep(int step)
    {
        if (ended || withdrawn) return;
        cctv?.ShowStep(step);
        var tape = tapes.Length > 0 ? tapes[0] : null;
        if (step < 0) { hush = 1f; Sting(); return; }
        if (step == 1)
        {
            if (tape != null && tape.display != null) tape.display.text = "CAM 1  " + tape.order[0].ToUpperInvariant() + "\n1 PERSON STANDING";
            Say(HS.S("321"));
            Sting();
        }
        else if (step == 2)
        {
            if (tape != null && tape.display != null) tape.display.text = "CAM 2  " + tape.order[1].ToUpperInvariant() + "\nYOU";
            if (stalker.enabled == false) stalker.enabled = true;
            if (stalker.TryFind(3.5f, false, out var behind)) stalker.Apparition(behind, .9f);
            Say(HS.S("322"));
            FlashOverlay(Color.black, .7f, .15f);
        }
        else if (step == 3)
        {
            if (tape != null && tape.display != null) tape.display.text = "CAM 1 / 2 / 3 / 4\nTHE SAME CORRIDOR";
            Say(HS.S("323-324"));
            Sting();
        }
    }

    public void OnTapeSolved()
    {
        if (ended || breakdown) return;
        Advance(5);
        Say(HS.S("320"));   // the terminal; what the tape's microphone caught is said in time with the tape (TomasTape)
        StartCoroutine(Breakdown());
    }

    IEnumerator Breakdown()
    {
        breakdown = true;
        // The murder footage owns the camera first. Escalate once the player has control again.
        while (!Safe) { if (ended || withdrawn) yield break; yield return null; }
        yield return LightStutter(1.4f, 0f);
        redMix = 1f; lightMul = .55f; lightsDirty = true;
        stalker.enabled = true; stalker.Hunt();
        foreach (var p in phones) Ring(p, true);
        Sting();
        yield return new WaitForSeconds(2f);
        for (int i = 0; i < 4; i++) { pendingShifts++; yield return new WaitForSeconds(1.2f); }
        Pa(HS.S("338-339"));
    }

    // ---------------------------------------------------------------- red-city events

    IEnumerator DesksEvent()
    {
        yield return LightStutter(2.2f, .05f);
        var figs = new List<GameObject>();
        for (int i = 0; i < 6; i++)
            if (stalker.TryFind(UnityEngine.Random.Range(5f, 11f), true, out var p))
            {
                var g = bodies.Standing(p, cam.transform.eulerAngles.y + UnityEngine.Random.Range(-15f, 15f), i % 3 == 1);   // backs to you
                if (g != null) figs.Add(g);
            }
        lightMul = 1f; lightsDirty = true;
        Say(HS.S("269-271"));
        yield return new WaitForSeconds(3.2f);
        yield return LightStutter(.9f, 0f);
        foreach (var g in figs) if (g != null) Destroy(g);
        yield return new WaitForSeconds(.4f);
        Say(HS.S("272"));
    }

    IEnumerator PhonesEvent()
    {
        Sting();
        if (phones.Count > 0) Ring(phones[0], true);
        PrintPage();
        yield return null;   // (the ring is the line)
    }

    IEnumerator AuditorIntro()
    {
        yield return new WaitForSeconds(2f);
        Pa(HS.S("273-274"));
    }

    void Ring(DeskPhone p, bool on)
    {
        if (p == null || p.ring == null || (on && (withdrawn || ended))) return;
        p.ringing = on; p.use.enabled = on;
        if (on) p.ring.Play(); else p.ring.Stop();
    }

    void Answer(DeskPhone p)
    {
        Ring(p, false);
        answers++;
        Say(HS.S(answers == 1 ? "367" : answers == 2 ? "368" : "369"));
        if (answers < 4 && phones.Count > 0 && Stage >= 2) StartCoroutine(RingLater(phones[UnityEngine.Random.Range(0, phones.Count)], UnityEngine.Random.Range(25f, 45f)));
    }

    IEnumerator RingLater(DeskPhone p, float after) { yield return new WaitForSeconds(after); if (!ended && !withdrawn && !breakdown) Ring(p, true); }

    void PrinterRead()
    {
        prints++;
        Say(HS.S(Stage < 3 ? "375" : "376-377"));
    }

    void RefreshDesk()
    {
        if (scratch != null) scratch.text = Stage >= 4 ? "I DIDN'T READ IT" : Stage >= 3 ? "DEREK" : "";
    }

    // ---------------------------------------------------------------- realm switching

    public void OnEcho(bool on)
    {
        echo = on;
        if (photoBrother != null) photoBrother.SetActive(on);
        if (photo != null) photo.GetComponent<ReadableNote>().body = on
            ? "Derek and me beside the delivery van. Two faces. I remember taking this."
            : "Derek is still in the photograph. My face has been scraped away. The frame is warm.";
        bodies.SetEcho(on);
        if (colleaguesAct != null) colleaguesAct.SetEcho(on);
        bed.clip = on ? humEcho : humRed; bed.Play();
        foreach (var m in mugs) if (m != null) m.gameObject.SetActive(on && Stage >= 2);
        if (on) ShuffleMugs();
        lightsDirty = true;
        if (clock != null) { clock.transform.parent.gameObject.SetActive(on); UpdateClock(); }
        if (scratch != null) scratch.gameObject.SetActive(!on);
        if (!on && visitor.Present) visitor.Vanish();
        if (on && stalker.Present && Corruption < 2) stalker.Vanish();
        // the burnt thing only follows you in when the Echo has gone bad
        stalker.canAct = () => Safe && InOffice && (!echo || Corruption >= 2);
    }

    void UpdateClock()
    {
        if (clock == null) return;
        clock.text = Corruption >= 3 ? "13:04" : Corruption >= 2 ? "07:12" : Corruption >= 1 ? "06:52" : "06:15";
    }

    void ShuffleMugs()
    {
        if (deskNote == null || Stage < 2) return;
        foreach (var m in mugs)
            if (m != null && UnityEngine.Random.value < .6f)
            {
                Vector3 o = deskNote.position + new Vector3(UnityEngine.Random.Range(-1.4f, 1.4f), 0f, UnityEngine.Random.Range(-1.2f, 1.2f));
                if (Surface(o, out var at)) m.position = at + Vector3.up * .05f;
            }
    }

    public void RequestSwitch(bool on, Action apply, bool forcedReturn = false)
    {
        if (switching || VaneHunt.Seizing) return;   // (his hands are on you)
        StartCoroutine(SwitchRoutine(on, apply, forcedReturn));
    }

    IEnumerator SwitchRoutine(bool on, Action apply, bool forcedReturn)
    {
        switching = true;
        int level = Corruption;
        blink.Begin();
        yield return blink.Animate(1f, .24f);
        float hold = .12f;
        if (level >= 1) { sfx.pitch = .55f; sfx.PlayOneShot(crackle, .35f); }
        if (level >= 2) hold = UnityEngine.Random.Range(.35f, .7f);
        if (level >= 3 && on && !longSwitchDone)
        {
            longSwitchDone = true; hold = 2.4f;
            StartCoroutine(Footsteps(7f, 4, .55f));
        }
        yield return new WaitForSeconds(hold);
        var realmPuzzles = plot.GetComponent<OfficeRealmPuzzles>();
        if (!GameOverScreen.Open && ((!ended && !withdrawn && InOffice && !(conf != null && conf.Armed)
            && (on || realmPuzzles == null || realmPuzzles.CanSwitch(player))) || (forcedReturn && !on))) apply();
        // Let the atmosphere start blending behind closed lids before revealing the new realm.
        yield return new WaitForSeconds(.12f);
        yield return blink.Animate(0f, .42f);
        blink.Cancel(); switching = false;
    }

    public void CancelSwitch()
    {
        if (!switching) return;
        StopAllCoroutines();
        switching = busyEvent = false;
        if (blink != null) blink.Cancel();
        if (overlay != null) overlay.color = Color.clear;
        lightMul = 1f; lightsDirty = true;
    }

    // ---------------------------------------------------------------- update

    void Update()
    {
        if (ended || player == null) return;
        if (conf != null && conf.Ambushing && !withdrawn) Withdraw();
        if (conf != null && conf.Done) { End(); return; }
        if (withdrawn) { bed.volume = 0f; return; }

        // the sound bed dies when it is near: silence is the warning
        float target = InOffice ? bedBase * (echo ? .6f : 1f) : 0f;
        float near = stalker.Present && !echo ? Mathf.InverseLerp(14f, 5f, Vector3.Distance(stalker.Position, player.position)) : 0f;
        hush = Mathf.MoveTowards(hush, 0f, Time.deltaTime * .5f);
        bed.volume = Mathf.Lerp(bed.volume, target * (1f - Mathf.Max(hush, near)), 4f * Time.deltaTime);

        if (!InOffice || !Safe) return;
        if (!echo && redEvents.Count > 0 && !busyEvent) StartCoroutine(RunEvent(redEvents.Dequeue()));
        if (pendingShifts > 0 && !echo && bodies.Shift()) pendingShifts--;

        paTimer -= Time.deltaTime; scareTimer -= Time.deltaTime; footTimer -= Time.deltaTime; boxTimer -= Time.deltaTime;
        if (paTimer <= 0f) { paTimer = Mathf.Lerp(150f, 75f, Stage / 5f) * UnityEngine.Random.Range(.8f, 1.3f); NextAnnouncement(); }
        if (scareTimer <= 0f) { scareTimer = Mathf.Lerp(45f, 16f, Stage / 5f) * UnityEngine.Random.Range(.7f, 1.3f); StartCoroutine(FalseScare()); }
        if (echo && Corruption >= 1 && footTimer <= 0f) { footTimer = UnityEngine.Random.Range(22f, 40f); StartCoroutine(Footsteps(8f, 5, .6f)); }
        if (boxTimer <= 0f) { boxTimer = 1f; TendBox(); }
        if (Stage >= 4 && deskNote != null && !echo) DeskGhost();
    }

    void LateUpdate()
    {
        if (lights == null || !lightsDirty) return;
        bool neutral = echo || (lightMul >= .999f && redMix <= .001f);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
            if (neutral) { lights[i].intensity = baseI[i]; lights[i].color = baseC[i]; }
            else { lights[i].intensity = baseI[i] * lightMul; lights[i].color = Color.Lerp(baseC[i], new Color(1f, .06f, .04f), redMix); }
        }
        if (neutral) lightsDirty = false;
    }

    IEnumerator RunEvent(Func<IEnumerator> e) { busyEvent = true; yield return e(); busyEvent = false; }

    IEnumerator LightStutter(float seconds, float minMul)
    {
        sfx.pitch = 1f; sfx.PlayOneShot(ProceduralAudio.Click(), .8f);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            lightMul = UnityEngine.Random.value < .5f ? minMul : UnityEngine.Random.Range(.5f, 1.1f); lightsDirty = true;
            yield return new WaitForSeconds(UnityEngine.Random.Range(.04f, .16f));
            t += .1f;
        }
        lightMul = 1f; lightsDirty = true;
    }

    // ---------------------------------------------------------------- PA, scares, box, desk

    static readonly string[][] Announcements =
    {
        new[] { "154-156", "370" },
        new[] { "371" },
        new[] { "372", "373" },
        new[] { "373", "374" },
        new[] { "338", "339" },
    };   // rows of the script, by stage (3.1, 3.12 and the 3.14 pool)

    void NextAnnouncement()
    {
        if (breakdown || (DialogueBox.Instance != null && DialogueBox.Instance.Busy)) return;
        var set = Announcements[Mathf.Clamp(Stage < 2 ? 0 : Stage - 1, 0, Announcements.Length - 1)];
        Pa(HS.S(set[paIndex++ % set.Length]));
    }

    void Pa(string line) { StartCoroutine(PaRoutine(line)); }

    IEnumerator PaRoutine(string line)
    {
        pa.PlayOneShot(crackle, .8f);
        hush = .7f;
        yield return new WaitForSeconds(.6f);
        Say(line);
    }

    IEnumerator FalseScare()
    {
        if (Stage < 1) yield break;
        hush = 1f;   // the room goes quiet first
        yield return new WaitForSeconds(2.2f);
        if (!Safe || !InOffice || withdrawn || ended || (echo && Corruption == 0)) yield break;
        switch (UnityEngine.Random.Range(0, 4))
        {
            case 0: yield return Footsteps(9f, 6, .55f); break;   // closer, closer, nothing
            case 1: At(ProceduralAudio.Creak(1.4f), 5.5f, .8f, .7f); break;   // a door handle moves
            case 2: At(ProceduralAudio.Thud(), 4f, .9f, 1.1f); yield return new WaitForSeconds(.5f); At(ProceduralAudio.Thud(), 4f, .9f, 1f); break;   // knocking on glass
            default: if (printer != null) At(ProceduralAudio.Rumble(1.2f), 5f, .9f, 1.4f); break;   // the printer behind you
        }
    }

    void At(AudioClip clip, float dist, float volume, float pitch)
    {
        var go = new GameObject("scare sound");
        go.transform.position = player.position + Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward * dist;
        var a = go.AddComponent<AudioSource>();
        a.spatialBlend = 1f; a.pitch = pitch; a.volume = volume; a.maxDistance = 25f; a.rolloffMode = AudioRolloffMode.Linear; a.clip = clip; a.Play();
        Destroy(go, clip.length / pitch + .3f);
    }

    public void LurkSteps() => StartCoroutine(Footsteps(UnityEngine.Random.Range(8f, 12f), UnityEngine.Random.Range(4, 8), UnityEngine.Random.Range(.5f, .75f)));

    // footsteps that come up to you from `dist` away and stop beside you. Nobody is there.
    IEnumerator Footsteps(float dist, int count, float interval)
    {
        var go = new GameObject("footsteps");
        var a = go.AddComponent<AudioSource>();
        a.spatialBlend = 1f; a.maxDistance = 20f; a.rolloffMode = AudioRolloffMode.Linear;
        Vector3 dir = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward;
        for (int i = 0; i < count; i++)
        {
            float d = Mathf.Lerp(dist, 1.8f, i / (float)Mathf.Max(1, count - 1));
            go.transform.position = player.position + dir * d;
            a.pitch = UnityEngine.Random.Range(.85f, 1.05f);
            a.PlayOneShot(stepClip != null ? stepClip : stepClip = ProceduralAudio.Stomp(), Mathf.Lerp(.4f, .9f, i / (float)count));
            yield return new WaitForSeconds(interval);
        }
        Destroy(go);
    }

    void TendBox()
    {
        if (box == null) { var go = GameObject.Find("KEEP COLD - collection"); if (go == null) return; box = go.transform; BuildSpill(); }
        bool on = !echo && Stage >= 1;
        if (spill != null) spill.SetActive(!echo);
        heart.transform.position = box.position;
        float want = on ? Mathf.Lerp(.25f, .8f, Stage / 5f) : 0f;
        heart.volume = Mathf.MoveTowards(heart.volume, want, .3f);
        if (on && !heart.isPlaying) heart.Play();
        if (!on && heart.volume <= 0f && heart.isPlaying) heart.Stop();
        heart.pitch = 1f + Stage * .06f;
    }

    void BuildSpill()
    {
        RaycastHit h = default; float nearest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(box.position + box.right * .4f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(box) && hit.normal.y > .9f && hit.distance < nearest) { h = hit; nearest = hit.distance; }
        if (float.IsInfinity(nearest)) return;
        spill = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        spill.name = "Box leak"; Destroy(spill.GetComponent<Collider>());
        spill.transform.SetParent(transform, true);
        spill.transform.position = h.point + Vector3.up * .008f;
        spill.transform.localScale = new Vector3(1.1f, .004f, .8f);
        spill.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.06f, .005f, .005f) };
    }

    // at your own desk the burnt shape of you is sitting in your chair, for a moment, once
    bool deskSeen;
    void DeskGhost()
    {
        if (deskSeen || Vector3.Distance(player.position, deskNote.position) > 7f || stalker.Present) return;
        Vector3 chair = deskNote.position + new Vector3(0f, -1.0f, .9f);
        if (!Physics.Raycast(chair + Vector3.up * 1.5f, Vector3.down, out var h, 3f, ~0, QueryTriggerInteraction.Ignore)) return;
        Vector3 to = h.point + Vector3.up * .1f - cam.transform.position;
        if (Vector3.Angle(cam.transform.forward, to) > 28f) return;
        deskSeen = true;
        stalker.Apparition(h.point, 1.4f);
        Say(HS.S("378"));

    }

    // ---------------------------------------------------------------- the end of it

    void Withdraw()
    {
        withdrawn = true;
        if (blink != null) blink.Cancel();
        StopAllCoroutines(); redEvents.Clear(); switching = busyEvent = false;
        if (overlay != null) overlay.color = Color.clear;
        if (pa != null) pa.Stop(); if (sfx != null) sfx.Stop(); if (heart != null) heart.Stop();
        lightMul = .65f; redMix = 1f; lightsDirty = true;
        stalker.hunting = false; stalker.Vanish(); stalker.enabled = false;
        visitor.Vanish(); visitor.enabled = false;
        foreach (var p in phones) Ring(p, false);
        breakdown = false;
    }

    void End()
    {
        ended = true;
        Withdraw();
        bed.Stop();
        lightMul = 1f; redMix = 0f; lightsDirty = true;
        foreach (var p in phones) if (p.t != null) p.t.gameObject.SetActive(false);
    }

    public void WitnessResolved(GhostNPC.Fate fate)
    {
        if (ended || withdrawn) return;
        hush = 1f;
        if (fate == GhostNPC.Fate.Damned) { pendingShifts++; Sting(); FlashOverlay(new Color(.35f, .02f, .01f), .2f, .45f); }
    }
    void OnEvidence(string id)
    {
        if (ended || withdrawn || !InOffice || id == EvidenceJournal.Tape) return;
        evidenceReactions++; pendingShifts++; hush = 1f;
        if (evidenceReactions <= 3) redEvents.Enqueue(EvidenceEvent);
    }
    IEnumerator EvidenceEvent()
    {
        yield return Footsteps(7f, 3, .75f);
        if (printer != null) { PrinterRead(); PrintPage(); }
    }
    void PrintPage()
    {
        if (printer == null || prints >= 6) return;
        var page = Prop("Corrupted incident report", printer.transform.position + printer.transform.forward * .26f + Vector3.up * (.18f + prints * .006f),
            new Vector3(.22f, .004f, .3f), new Color(.8f, .76f, .65f));
        var note = page.gameObject.AddComponent<ReadableNote>();
        note.title = "INCIDENT REPORT - DUPLICATE";
        note.body = Stage >= 4 ? "I DIDN'T READ IT.\nI DIDN'T READ IT." : Stage >= 3 ? "SIGNATURE: YOURS\nSIGNATURE: YOURS\nSIGNATURE: YOURS" : "T. ROURKE FELL.\nFELL.\nFELL.\nREPORT INCOMPLETE.";
        var use = page.gameObject.AddComponent<Interactable>(); use.once = false; use.prompt = "Read the printed report";
    }
    void OnDestroy() { EvidenceJournal.Collected -= OnEvidence; foreach (var clip in new[] { humRed, humEcho, crackle, ring, beat, stepClip }) if (clip != null) Destroy(clip); }

    // ---------------------------------------------------------------- small helpers

    void Say(string lines) { if (DialogueBox.Instance != null) DialogueBox.Instance.Say(lines); }
    void Sting() { sfx.pitch = UnityEngine.Random.Range(.6f, .8f); sfx.PlayOneShot(ProceduralAudio.Rumble(1.4f), .8f); }

    void FlashOverlay(Color c, float peak, float time) { StartCoroutine(Flash(c, peak, time)); }

    IEnumerator Flash(Color c, float peak, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime) { overlay.color = new Color(c.r, c.g, c.b, peak * Mathf.Sin(t / time * Mathf.PI)); yield return null; }
        overlay.color = Color.clear;
    }
}

static class ArrayExt
{
    public static T FirstOrDefault2<T>(this T[] a) where T : class => a != null && a.Length > 0 ? a[0] : null;
}
