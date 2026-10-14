using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// The prologue (its own scene: the apartment block, the night Derek dies), start to finish:
//   home from work -> "Derek? I'm home!" -> nobody answers: the sofa, the news (no door opens before it)
//   -> noises in the master bedroom -> you ease the door open: the masked man, killing him
//   -> he looks round: YOU ARE NOT GETTING AWAY WITH THIS AS WELL
//   -> the chase: out of the flat, up the stairwell, onto the roof, him behind you. Nowhere left to run
//   -> you shove him off the edge; he grabs your wrist and takes you with him
//   -> the fall: the night turns red -> black -> MainGameScene, waking in the hell city.
public class PrologueDirector : MonoBehaviour
{
    [Header("Player")]
    public FirstPersonController fpc;
    public Camera playerCam;
    public Behaviour[] lockDuring;          // movement, interacting
    public ScreenFader fader;

    [Header("UI")]
    public TextMeshProUGUI objective;
    public TextMeshProUGUI titleCard;       // the time, over black at the start
    public RectTransform barTop, barBottom; // letterbox
    public float barHeight = 90f;
    public CanvasGroup rage;                // red edges pulsing on the roof
    public TextMeshProUGUI pushPrompt;

    [Header("Zones")]
    public Bounds hallway;                  // the 2nd floor landing outside the flat
    public Bounds flat;                     // just inside the front door

    [Header("Derek")]
    public Animator derekRig;               // the machete stab: Derek and the killer in one FBX
    public string stabState = "Stab";
    public string eliasStabState = "StabOver";   // the same stab on Elias's own animator (PrologueKiller.controller)
    public GameObject[] killerInRig;        // the killer's half of it: only ever seen on the tape
    public Transform body;                  // where Derek lies (chest height)
    public GameObject bloodPool;            // not there yet on the tape
    public Interactable pc;                 // plays the tape

    [Header("CCTV")]
    public Camera cctvCam;
    public GameObject cctvOverlay;
    public TextMeshProUGUI cctvClock;
    public Volume cctvLook;                 // grey, grainy
    public float tapeLength = 10f;
    public int tapeStartSeconds = 6 * 3600 + 52 * 60;

    [Header("Killer")]
    public FleeingKiller killer;
    public PrologueDoor closet;             // where he hid the whole time
    public float spotAngle = 26f;
    public float sneakTimeout = 16f;
    public LightFlicker[] stairLights;      // start stuttering when the chase does

    [Header("Roof")]
    public Camera cine;                     // the push and the fall
    public Light cineLight;                 // on the cutscene camera: picks him out against the night
    public float confrontDistance = 4.2f;
    public float groundY = 0f;
    public Volume hellLook;                 // red grade: weight 0 -> 1 during the fall
    public Light moon;
    public Color hellFog = new Color(0.35f, 0.05f, 0.04f);
    public Color hellSky = new Color(0.9f, 0.08f, 0.04f);
    public Color hellLight = new Color(0.8f, 0.12f, 0.08f);
    public string nextScene = "MainGameScene";
    public DeliveryRun deliveries;          // optional: the van deliveries before the walk home
    public Transform streetAnchor;          // a street piece MainGameScene has too: you wake up where you land
    public float fallDrift = 4.5f;          // how far out from the wall you land

    [Header("Home (shown once the van's parked, so nobody gets lost)")]
    public Vector3 homeCamAt = new Vector3(34f, 6f, 1f);        // where the camera ends up, down the street
    public Vector3 homeLookAt = new Vector3(4f, 7f, -3.25f);    // the flats, above the lobby door
    public float homeFov = 50f;
    public float homeFlight = 4.5f;         // seconds from your eyes to the shot (0 = no cutscene)
    [TextArea] public string homeLines = "There it is. The brick block at the end of the street - Flat 2B.";

    [Header("The fall (animations)")]
    public string killerFallState = "Fall";     // "Falling Into Pool" on the killer's own animator, as he goes over
    public float killerFallTilt = 0f;           // extra lean back while falling, on top of what the clip does (deg)
    public float killerLyingAt = 0.75f;         // the frame of his clip he's left lying in on the street (limbs flattest)
    public Animator playerBody;                 // you, seen from the street for the last second ("Falling Flat Impact")
    public string playerFallState = "FallFlat";
    public float bodyPoseAt = 0.1f;             // the clip's frame held while the body drops through the air (flat, arms out)
    public float impactAt = 0.38f;              // seconds into that clip where the body hits the ground
    public float bodyDrop = 6f;                 // metres above the street the body is when the shot cuts in
    public float preFall = 0.7f;                // seconds of it dropping before the clip takes over for the impact
    [Range(0f, 1f)] public float cutToStreetAt = 0.82f;   // how far down the fall the camera cuts to the street
    public float holdAfterImpact = 1.4f;

    [Header("Sound")]
    public AudioSource ambience;            // the city at night (2D)
    public AudioSource tv;                  // murmuring in the living room (3D)
    public AudioSource music;               // heartbeat, stings (2D)
    public AudioSource sfx;                 // 2D one-shots
    public AudioSource wind;                // roof and fall (2D)
    public AudioClip heartbeat, sting, stabs, whisper;

    [Header("Objectives")]
    public string goHome = "Go home - Flat 2B, second floor";
    public string findDerek = "Find Derek";
    public string checkTape = "Check the camera footage - the PC in the living room";
    public string chase = "Chase him";
    public string sitObjective = "Sit down on the sofa";
    public string tvObjective = "Put the news on";
    public string letterObjective = "Take the letter from Derek's hand";
    public string runObjective = "RUN - the roof!";

    [Header("The flat: the news, the letter, the knife (there is no tape)")]
    [TextArea] public string settleLines = "Nothing. He must be in the shower.|I'll sit down and put the news on. Breakfast can wait.";
    [TextArea] public string newsLines = "News: ...and in local news, a body was found at the Kessler & Vane depot overnight.|News: He has been named as Tomas Rourke, a night cleaner there for six years.|News: Police say there are no suspicious circumstances. The company has declined to comment.";
    [TextArea] public string suspicionLines = "Tomas. The cleaner. He said goodnight to me on Friday.|...Wait.|Derek said he'd make breakfast. It's gone eight.|The kitchen's empty. No pan. No plates. Not even a mug.|Derek? ...DEREK?";
    public string letterPrompt = "Take the letter";
    [TextArea] public string letterBefore = "There's something in his hand. Paper, curled up tight. Like he wouldn't let it go.";
    [TextArea] public string letterBody = "To whoever finds this,\n\nThe Harlow boxes are not\nspare parts. They are\n<color=#6a0000>######## ## #####</color>.\n\nTomas saw it. Now he is\n<color=#6a0000>#######</color>. The ledger,\npages 41 to <color=#6a0000>##</color>, has\nit all. Take it to\n<color=#6a0000>######## ## ####</color>.\n\nTell my brother I'm s";
    [TextArea] public string letterAfter = "Most of it is soaked through. Blood.|Kessler and Vane... the Harlow collection... a ledger.|Tomas saw something. And so did Derek. That's why--|...";
    [TextArea] public string stalkLines = "";   // face to face, before the knife
    [TextArea] public string stabLine = "WHY?!";
    [TextArea] public string woundedLines = "I'm bleeding... I can't stop it.|The stairs. The roof. RUN!";
    public string adrenalinePrompt = "ADRENALINE  -  [F]  PULL HIM IN";
    [TextArea] public string roofLines = "I can't feel my legs. He's in no hurry.|I'm not getting down those stairs. I know that.|...Come on, then. Closer.";

    [Header("The bedroom door (Elias, caught in the act)")]
    public string bedroomDoorFrame = "DoorFrame_Apt_01 (3)";   // the master bedroom's door: its frame's name
    public string bedroomObjective = "Check the noise - the master bedroom";
    [TextArea] public string noiseLines = "...What was that?|The master bedroom.";
    [TextArea] public string witnessLine = "DEREK--!";
    [TextArea] public string caughtLine = "Elias: YOU ARE NOT GETTING AWAY WITH THIS AS WELL.";
    [Range(.05f, 1f)] public float ajar = .22f;   // how far the door comes open (of its whole swing)
    public float witnessTime = 7f;                // seconds of it you watch before he hears you (by then Derek is down and he stands over him)

    [Header("Lines ('|' splits, \"quotes\" = someone else talking)")]
    public string timeCard = "SATURDAY  -  7:12 AM";
    [TextArea] public string arriveLines = "Van's back. Clocked out. Twelve hours on the road.|Left my car keys in my desk again... forget it, I'll walk.|Derek said he'd have breakfast waiting. Like old times.";
    [TextArea] public string doorOpenLines = "...the door's open.|Derek never leaves it open.";
    [TextArea] public string callOut = "Derek? I'm home!";
    [TextArea] public string noAnswer = "...Derek?";
    [TextArea] public string bodyLines = "Derek...?|No. No, no, no--|DEREK! Come on, wake up! WAKE UP!|...he's cold.|Who did this to you...?";
    [TextArea] public string cameraIdea = "The camera. He put one in the bedroom last month.|Whoever did this... it saw them.";
    [TextArea] public string tapeStart = "Playback. This morning, 06:52.";
    [TextArea] public string afterTape = "That face...|He was HERE. In our home.|...wait. 06:52.|That was twenty minutes ago.";
    [TextArea] public string footstepLines = "...footsteps.|Right behind me.";
    [TextArea] public string spottedLine = "YOU!";
    [TextArea] public string escapedLines = "The front door--!|Someone just ran out!";
    [TextArea] public string upLines = "Up. Keep going. UP.|Don't stop--";
    [TextArea] public string topLines = "The roof door--! GO!";
    [TextArea] public string confrontLines = "I can't feel my legs. He's still coming.|I'm not getting down those stairs. I know that.|...Neither are you.";
    [TextArea] public string grabLines = "GOT YOU--!|If I'm going, you're coming with me!";

    public bool playFlashback = true;       // the office the night before, ahead of the morning

    private bool cutscene, found, tapeDone, pushNow;

    public void Push() => pushNow = true;   // the roof prompt, from code (tests, a gamepad binding)

    // testing: straight to the roof with him cornered at the edge (skips the flat, the tape and the chase)
    [ContextMenu("Skip To Roof")]
    public void SkipToRoof()
    {
        StopAllCoroutines();
        if (killer.route == null || killer.route.Length == 0) return;
        Vector3 edge = killer.route[killer.route.Length - 1];
        Vector3 prev = killer.route[killer.route.Length - 2];
        Vector3 back = Flat(prev - edge).normalized;
        var rb = fpc.GetComponent<Rigidbody>();
        Player.SetPositionAndRotation(edge + back * 3.4f + Vector3.up * 0.9f, Quaternion.LookRotation(-back));
        if (rb != null) rb.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        pursuer = killer.GetOrAdd<Pursuer>();
        pursuer.flee = killer; pursuer.animator = killer.animator; pursuer.target = Player;
        pursuer.Place(edge + back * 6.4f, -back);   // three metres behind you
        StartCoroutine(Confront(edge));
    }
    private float nextLineY = 9f;

    Transform Player => fpc.transform;

    void Start()
    {
        // slimmer and a bit shorter, so the stairwell's low ceilings/undersides don't snag you
        if (fpc != null && fpc.TryGetComponent<CapsuleCollider>(out var bodyCap)) { bodyCap.radius = 0.38f; fpc.transform.localScale = Vector3.one * 0.78f; }
        // Derek and his killer wait behind the bedroom door on the first frame of it, until you look in (Witness)
        if (derekRig != null) { derekRig.Play(stabState, 0, 0f); derekRig.Update(0f); derekRig.speed = 0f; }
        if (bloodPool != null) bloodPool.SetActive(false);
        if (killer != null) killer.gameObject.SetActive(false);
        if (playerBody != null) playerBody.gameObject.SetActive(false);   // only for the last second of the fall
        // the controller turns the player by setting its rotation each frame; an interpolating rigidbody fights that
        // (jittery turning) - the hell city's player has it off, so this one does too
        var playerRb = fpc != null ? fpc.GetComponent<Rigidbody>() : null;
        if (playerRb != null) playerRb.interpolation = RigidbodyInterpolation.None;
        foreach (var l in stairLights) if (l != null) l.enabled = false;
        foreach (var c in new[] { cctvCam, cine }) if (c != null) c.enabled = false;
        if (cineLight != null) cineLight.enabled = false;
        if (cctvOverlay != null) cctvOverlay.SetActive(false);
        if (cctvLook != null) cctvLook.weight = 0f;
        if (hellLook != null) hellLook.weight = 0f;
        if (rage != null) rage.alpha = 0f;
        if (pushPrompt != null) pushPrompt.gameObject.SetActive(false);
        if (pc != null) pc.enabled = false;   // the camera's tape is gone: the letter is the evidence now
        // the Sound Library (HellScape > Sound Manager) can swap any of these
        heartbeat = Sounds.Clip("Prologue/Heartbeat", heartbeat);
        sting = Sounds.Clip("Prologue/Sting (body, spotted)", sting);
        stabs = Sounds.Clip("Prologue/Stabbing on the tape", stabs);
        whisper = Sounds.Clip("Prologue/Whisper (the fall)", whisper != null ? whisper : ProceduralAudio.Whisper(2.5f));
        if (ambience != null) ambience.clip = Sounds.Clip("Prologue/Street ambience", ambience.clip);
        if (wind != null) wind.clip = Sounds.Clip("Prologue/Wind (roof and fall)", wind.clip);
        if (tv != null) tv.clip = Sounds.Clip("Prologue/TV murmur", tv.clip);
        MusicManager.Area("Prologue");
        if (wind != null && wind.clip == null) { wind.clip = ProceduralAudio.Wind(); wind.loop = true; }
        if (tv != null) { if (tv.clip == null) tv.clip = ProceduralAudio.TvMurmur(); tv.loop = true; }   // (off until you put the news on)
        SetBars(0f);
        Objective(null);
        StartCoroutine(Story());
    }

    // ---------------------------------------------------------------- the night

    IEnumerator Story()
    {
        // over black: the time, the street
        Lock(true);
        fader.fade.alpha = 1f;
        yield return null;
        fader.fade.alpha = 1f;   // the fader's own Start clears it
        if (ambience != null) { ambience.volume = 0f; ambience.Play(); StartCoroutine(Volume(ambience, 0.5f * Sounds.Volume("Prologue/Street ambience"), 4f)); }
        // the night before: the office, then the van leaving the garage (OfficeFlashback), and only then this morning
        if (playFlashback) yield return this.GetOrAdd<OfficeFlashback>().Run(this);
        if (deliveries != null) deliveries.Prepare();   // in the van before the picture comes up
        fader.fade.alpha = 1f;
        if (titleCard != null)
        {
            var cardFont = GameFont.Main;
            if (cardFont != null) { titleCard.font = cardFont; titleCard.fontSharedMaterial = cardFont.material; }
            titleCard.text = timeCard;
            titleCard.alpha = 0f;
            yield return FadeText(titleCard, 1f, 1.2f);
            yield return new WaitForSeconds(2f);
            yield return FadeText(titleCard, 0f, 1f);
        }
        yield return fader.FadeTo(0f, 2.5f);
        Lock(false);
        // the last two drops in the van first (DeliveryRun), then the walk home from the office
        if (deliveries != null) yield return deliveries.Run();
        yield return ShowHome();
        Objective(goHome);

        yield return FlatStory();
    }

    // testing: straight to the front door of the flat, as if you'd just walked home
    [ContextMenu("Skip To Flat")]
    public void SkipToFlat()
    {
        StopAllCoroutines();
        Lock(false);
        if (fader != null) fader.fade.alpha = 0f;
        var rb = fpc.GetComponent<Rigidbody>();
        Player.SetPositionAndRotation(hallway.center + Vector3.up * 0.5f, Quaternion.LookRotation(Vector3.forward));
        if (rb != null) rb.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        StartCoroutine(FlatStory());
    }

    IEnumerator FlatStory()
    {
        // the second floor: the flat door is shut, the block is quiet. An ordinary Saturday, nothing wrong at all.
        // inside, nobody answers: Derek must be in the shower. You put the news on.
        yield return new WaitUntil(() => flat.Contains(Player.position));
        var doors = FindObjectsByType<PrologueDoor>(FindObjectsSortMode.None);
        foreach (var d in doors) d.GetComponent<Interactable>().enabled = false;   // home: no door opens until the news has been on
        DialogueBox.Instance.SayNow(callOut);
        if (ambience != null)
        {
            // indoors: the street is a dull, quiet rumble through the walls
            // (a filter muffles every source on its object: the heartbeat, the stings and the wind share this one)
            if (ambience.GetComponents<AudioSource>().Length == 1) ambience.GetOrAdd<AudioLowPassFilter>().cutoffFrequency = 700f;
            StartCoroutine(Volume(ambience, 0.1f * Sounds.Volume("Prologue/Street ambience"), 3f));
        }
        yield return RevisedPlotDirector.WaitForDialogue();
        yield return SitAndWatchTheNews();

        // breakfast never came, and something is going on in the master bedroom
        yield return Witness(doors);

        // the run: out of the flat, up the stairs and onto the roof
        yield return RunToTheRoof();
    }

    // clocked out: the camera lifts off your shoulders and flies down the street to the flats while you talk
    // yourself into walking, holds on them, then you're back in your own eyes, facing that way
    IEnumerator ShowHome()
    {
        Say(arriveLines);
        if (cine == null || homeFlight <= 0f) yield break;
        Say(homeLines);
        cutscene = true;
        Lock(true);
        CutsceneHUD.Hide(true);
        Transform ct = cine.transform;
        Vector3 c0 = playerCam.transform.position;
        Quaternion r0 = playerCam.transform.rotation, r1 = Quaternion.LookRotation(homeLookAt - homeCamAt);
        float fov0 = playerCam.fieldOfView;
        ct.SetPositionAndRotation(c0, r0);
        cine.fieldOfView = fov0;
        // a clean picture for this one shot: no grade, no pixelation/dither (the roof wants them back)
        var cineData = cine.GetUniversalAdditionalCameraData();
        bool post0 = cineData.renderPostProcessing;
        cineData.renderPostProcessing = false;
        cine.enabled = true;
        StartCoroutine(Bars(1f));
        yield return Tween(homeFlight, t =>
        {
            ct.SetPositionAndRotation(Vector3.Lerp(c0, homeCamAt, t), Quaternion.Slerp(r0, r1, t));
            cine.fieldOfView = Mathf.Lerp(fov0, homeFov, t);
        });
        // a slow push in while the lines run out (a few metres at most, however long E takes)
        for (float pushed = 0f; DialogueBox.Instance != null && DialogueBox.Instance.Busy; )
        {
            float step = pushed < 5f ? 0.6f * Time.deltaTime : 0f;
            pushed += step;
            ct.position += ct.forward * step;
            yield return null;
        }
        yield return new WaitForSeconds(0.8f);

        yield return fader.FadeTo(1f, 0.4f);
        cine.enabled = false;
        cine.fieldOfView = fov0;
        cineData.renderPostProcessing = post0;
        SetBars(0f);
        CutsceneHUD.Hide(false);
        Aim(new Vector3(homeLookAt.x, playerCam.transform.position.y, homeLookAt.z));   // looking down the street at it
        Lock(false);
        cutscene = false;
        yield return fader.FadeTo(0f, 0.6f);
    }

    IEnumerator NoAnswer()
    {
        yield return new WaitForSeconds(3.5f);
        while (DialogueBox.Instance.Busy) yield return null;
        yield return new WaitForSeconds(1.2f);
        if (!found) Say(noAnswer);
    }

    // ---------------------------------------------------------------- the news

    Pursuer pursuer;
    Vector3 checkpointPos, killerStart, doorway; Quaternion checkpointRot;

    static Bounds BoundsOf(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // a normal Saturday: sit on the sofa, pick up the remote, put the news on... and then the penny drops
    IEnumerator SitAndWatchTheNews()
    {
        var sofa = GameObject.Find("Sofa_Apt_02");
        var tvObj = GameObject.Find("TV_Apt_01");
        if (sofa == null || tvObj == null) yield break;   // (a flat without them: straight to looking for him)

        Say(settleLines);
        Objective(sitObjective);
        var box = new GameObject("Sofa (sit down)").AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.transform.position = sofa.transform.position + Vector3.up * .6f;
        box.size = new Vector3(1.4f, 1.2f, 2.8f);
        bool sat = false;
        var sit = box.gameObject.AddComponent<Interactable>();
        sit.prompt = "Sit down"; sit.once = true; sit.onUse.AddListener(() => sat = true);
        yield return new WaitUntil(() => sat);
        Destroy(box.gameObject);

        // down onto the cushion, facing the TV
        var rb = Player.GetComponent<Rigidbody>();
        fpc.playerCanMove = false;
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
        Vector3 from = Player.position, seat = new Vector3(sofa.transform.position.x - .1f, from.y - .1f, sofa.transform.position.z);
        Vector3 cam0 = playerCam.transform.localPosition;
        Vector3 tvAt = BoundsOf(tvObj).center;
        yield return Tween(1.3f, k => { Player.position = Vector3.Lerp(from, seat, k); playerCam.transform.localPosition = cam0 + Vector3.down * .35f * k; Aim(tvAt); });

        // the TV: a prompt on screen, any of E / F / click (no aiming at a remote you can't see from the sofa)
        Objective(tvObjective);
        Color promptColor = pushPrompt.color; float promptSize = pushPrompt.fontSize;   // (the roof's red adrenaline text: put back after)
        pushPrompt.text = "[E]  Turn on the TV";
        pushPrompt.color = new Color(.92f, .9f, .85f);
        pushPrompt.fontSize = promptSize * .55f;
        pushPrompt.gameObject.SetActive(true);
        pushNow = false;
        while (!Input.GetKeyDown(KeyCode.E) && !Input.GetKeyDown(KeyCode.F) && !Input.GetMouseButtonDown(0) && !pushNow)
        {
            pushPrompt.alpha = .55f + .45f * Mathf.Sin(Time.time * 4f) * .5f + .225f;
            Aim(tvAt);
            yield return null;
        }
        pushPrompt.gameObject.SetActive(false);
        pushPrompt.color = promptColor; pushPrompt.fontSize = promptSize;

        TvOn(tvObj);
        Objective(null);
        Say(newsLines);
        yield return RevisedPlotDirector.WaitForDialogue();
        yield return new WaitForSeconds(.6f);
        Say(suspicionLines);
        yield return RevisedPlotDirector.WaitForDialogue();

        // up off the sofa
        Vector3 here = Player.position;
        yield return Tween(1f, k => { Player.position = Vector3.Lerp(here, from, k); playerCam.transform.localPosition = cam0 + Vector3.down * .35f * (1f - k); });
        playerCam.transform.localPosition = cam0;
        if (rb != null) rb.isKinematic = false;
        fpc.playerCanMove = true;
    }

    // the TV comes on: a lit screen, a cold glow over the room, the murmur of a news reader
    void TvOn(GameObject tvObj)
    {
        var b = BoundsOf(tvObj);
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "TV screen (news)";
        Destroy(q.GetComponent<Collider>());
        q.transform.SetPositionAndRotation(new Vector3(b.max.x + .02f, b.center.y, b.center.z), Quaternion.Euler(0f, -90f, 0f));   // (the face towards the sofa)
        q.transform.localScale = new Vector3(b.size.z * .86f, b.size.y * .72f, 1f);
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        q.GetComponent<Renderer>().sharedMaterial = m;
        var l = new GameObject("TV glow").AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(.55f, .7f, 1f); l.range = 8f; l.intensity = 1.6f; l.shadows = LightShadows.None;
        l.transform.position = q.transform.position + Vector3.right * 1f;
        StartCoroutine(TvFlicker(m, l));
        if (tv != null) { tv.volume = .5f * Sounds.Volume("Prologue/TV murmur"); tv.Play(); }
    }

    // The picture: the BREAKING NEWS sting twice round, then the anchor, looping. Both are flipbook sheets made from
    // the GIFs in Resources by Tools/tv/make_tv_sheets.py (frame 0 top-left). The glow in the room flickers with it.
    IEnumerator TvFlicker(Material m, Light l)
    {
        Texture2D breaking = Resources.Load<Texture2D>("TvBreaking"), anchor = Resources.Load<Texture2D>("TvAnchor");
        const float breakingTime = 3.8f;   // two loops of its 19 frames at 10 a second
        float on = Time.time;
        while (m != null && l != null)
        {
            float t = Time.time - on, n = Mathf.PerlinNoise(Time.time * 3f, 1.3f);
            bool sting = t < breakingTime && breaking != null;
            Texture2D sheet = sting ? breaking : anchor;
            if (sheet != null)
            {
                int cols = sting ? 5 : 10, rows = sting ? 4 : 9, frames = sting ? 19 : 86;
                int f = (int)((sting ? t * 10f : (t - breakingTime) * 7.5f) % frames);
                m.SetTexture("_BaseMap", sheet);
                m.SetTextureScale("_BaseMap", new Vector2(1f / cols, 1f / rows));
                m.SetTextureOffset("_BaseMap", new Vector2(f % cols / (float)cols, 1f - (f / cols + 1f) / rows));
                m.SetColor("_BaseColor", Color.Lerp(new Color(.85f, .88f, .95f), Color.white, n));
            }
            else m.SetColor("_BaseColor", Color.Lerp(new Color(.32f, .42f, .66f), new Color(.82f, .9f, 1f), n));
            l.intensity = 1.1f + n;
            yield return null;
        }
    }

    // ---------------------------------------------------------------- the bedroom door

    // Noises from the master bedroom. You ease the door open a hand's width: Elias, over Derek, the machete going in.
    // He hears you, looks round, and comes for you.
    IEnumerator Witness(PrologueDoor[] doors)
    {
        // the bedroom door (the kit names the frames the same on every floor: the one on his)
        PrologueDoor door = null;
        foreach (var d in doors)
            if (d.transform.parent != null && d.transform.parent.name == bedroomDoorFrame && Mathf.Abs(d.transform.position.y - body.position.y) < 2.5f) door = d;

        var noise = new GameObject("Bedroom noises").AddComponent<AudioSource>();
        noise.transform.position = body.position + Vector3.up;
        noise.clip = stabs; noise.loop = true; noise.spatialBlend = 1f; noise.rolloffMode = AudioRolloffMode.Linear; noise.minDistance = 2f; noise.maxDistance = 24f;
        noise.volume = .6f * Sounds.Volume("Prologue/Stabbing on the tape");
        noise.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 900f;   // through a shut door
        if (stabs != null) noise.Play();
        Say(noiseLines);
        Objective(bedroomObjective);

        bool peek = false;
        foreach (var d in doors) d.GetComponent<Interactable>().enabled = true;
        var handle = door.GetComponent<Interactable>();
        handle.onUse.RemoveListener(door.Toggle);   // this one doesn't swing open: you ease it
        handle.once = true;
        handle.onUse.AddListener(() => peek = true);
        yield return new WaitUntil(() => peek);

        // the door, a hand's width; then your eye to the gap
        cutscene = true;
        Lock(true);
        Objective(null);
        CutsceneHUD.Hide(true);
        Vector3 eye = playerCam.transform.position, look = body.position + Vector3.up * 1.1f;
        // the gap: at the door's free edge (its pivot is the hinge), a step into the room
        Bounds leaf = door.GetComponentInChildren<Collider>().bounds;
        Vector3 inward = leaf.extents.x < leaf.extents.z ? Vector3.right : Vector3.forward;   // the wall's normal: the leaf's thin side
        if (Vector3.Dot(inward, body.position - leaf.center) < 0f) inward = -inward;
        Vector3 hinge = door.transform.position - leaf.center; hinge.y = 0f;
        Vector3 gap = leaf.center - hinge * .6f + inward * .4f;
        gap.y = eye.y;
        doorway = leaf.center;
        Quaternion r0 = playerCam.transform.rotation, r1 = Quaternion.LookRotation(look - gap);
        float fov0 = playerCam.fieldOfView;
        cine.transform.SetPositionAndRotation(eye, r0);
        cine.fieldOfView = fov0;
        cine.enabled = true;
        StartCoroutine(Bars(1f));
        if (music != null && heartbeat != null) { music.clip = heartbeat; music.loop = true; music.pitch = 1.2f; music.volume = Hb(.8f); if (!music.isPlaying) music.Play(); }
        if (door.sfx != null && door.openClip != null) door.sfx.PlayOneShot(door.openClip, .25f);
        door.anim.speed = 0f;
        yield return Tween(1.1f, k => door.anim.Play(door.openState, 0, k * ajar));
        Destroy(noise.gameObject);
        if (stabs != null) sfx.PlayOneShot(stabs, Sounds.Volume("Prologue/Stabbing on the tape"));
        derekRig.speed = 1f;
        derekRig.Play(stabState, 0, 0f);
        // Elias himself does it: his own body on the rig's origin, playing the rig killer's half of the same clip
        // (HellScape > People: Elias Stabs Derek Himself). The rig's killer is a different-looking man: hidden.
        pursuer = killer.GetOrAdd<Pursuer>();
        pursuer.flee = killer; pursuer.animator = killer.animator;
        pursuer.Place(derekRig.transform.position, derekRig.transform.forward);
        pursuer.Hold();
        bool ownStab = killer.animator != null && killer.animator.HasState(0, Animator.StringToHash(eliasStabState));
        if (ownStab) { foreach (var g in killerInRig) if (g != null) g.SetActive(false); killer.animator.Play(eliasStabState, 0, 0f); }
        else killer.gameObject.SetActive(false);
        yield return Tween(1.2f, k =>
        {
            cine.transform.SetPositionAndRotation(Vector3.Lerp(eye, gap, k), Quaternion.Slerp(r0, r1, k));
            cine.fieldOfView = Mathf.Lerp(fov0, 42f, k);
        });
        yield return new WaitForSeconds(witnessTime * .5f);
        DialogueBox.Instance.SayNow(witnessLine);
        yield return new WaitForSeconds(witnessTime * .5f);

        // he heard that. The rig's killer can't turn round, so Elias himself takes his place on a cut in to the mask
        Vector3 feet = killerInRig.Length > 0 && killerInRig[0] != null && killerInRig[0].TryGetComponent(out Renderer mask) ? mask.bounds.center : derekRig.transform.position;
        if (ownStab) { feet = killer.animator.GetBoneTransform(HumanBodyBones.Hips).position; killer.animator.Play("Locomotion", 0, 0f); }   // where the clip left him
        feet.y = derekRig.transform.position.y;
        foreach (var g in killerInRig) if (g != null) g.SetActive(false);
        if (bloodPool != null) bloodPool.SetActive(true);
        killerStart = feet;
        pursuer = killer.GetOrAdd<Pursuer>();
        pursuer.flee = killer; pursuer.animator = killer.animator;
        pursuer.Place(feet, body.position - feet);   // still bent over him
        pursuer.Hold();
        cine.fieldOfView = 24f;
        cine.transform.rotation = Quaternion.LookRotation(pursuer.Chest + Vector3.up * .2f - gap);
        if (sting != null) sfx.PlayOneShot(sting, Sounds.Volume("Prologue/Sting (body, spotted)"));
        yield return new WaitForSeconds(.5f);
        Quaternion k0 = killer.transform.rotation, k1 = Quaternion.LookRotation(Flat(gap - feet));
        yield return Tween(.7f, k => killer.transform.rotation = Quaternion.Slerp(k0, k1, k));
        yield return RevisedPlotDirector.Speak(caughtLine);

        // and he comes: through the door, after you
        cine.enabled = false;
        cine.fieldOfView = fov0;
        SetBars(0f);
        CutsceneHUD.Hide(false);
        door.Burst();
        fpc.walkSpeed = 3.6f; fpc.sprintSpeed = 5.3f; fpc.unlimitedSprint = true; fpc.enableJump = false;   // (the chase is tuned to these: he is quicker than your walk, slower than your run)
        checkpointPos = Player.position; checkpointRot = Player.rotation;
        cutscene = false;
        Lock(false);
        Objective(runObjective);
        foreach (var l in stairLights) if (l != null) l.enabled = true;
        yield return new WaitForSeconds(.9f);
        pursuer.Locomotion();
        pursuer.Chase(Player, doorway);
    }

    // out of the flat, down the hall, up the stairwell and onto the roof, with him on your heels
    IEnumerator RunToTheRoof()
    {
        Vector3 edge = killer.route[killer.route.Length - 1];
        float roofTime = 0f;
        while (true)
        {
            if (Player.position.y > nextLineY)
            {
                Say(nextLineY < 15f ? upLines : topLines);
                nextLineY = nextLineY < 15f ? 16.8f : float.MaxValue;   // 16.8: standing on the top landing (roof 16.1)
            }
            foreach (var d in killer.doors)   // you burst through the doors in your way
                if (d != null && !d.IsOpen && Vector3.Distance(d.transform.position, Player.position + Vector3.up) < 1.8f) d.Burst();
            if (pursuer != null && pursuer.Caught) yield return Retry();
            if (Player.position.y > 15.2f)
            {
                roofTime += Time.deltaTime;
                if (Flat(edge - Player.position).magnitude < 4.5f || roofTime > 9f) break;
            }
            yield return null;
        }
        yield return Confront(edge);
    }

    // he caught you: back to the bedroom door, him in the bedroom again
    IEnumerator Retry()
    {
        Lock(true);
        pursuer.Hold(); pursuer.StabAnim();
        yield return fader.FadeTo(1f, .5f);
        var rb = Player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;
        Player.SetPositionAndRotation(checkpointPos, checkpointRot);
        Physics.SyncTransforms();
        pursuer.Place(killerStart, Player.position - killerStart);
        pursuer.Locomotion();
        yield return fader.FadeTo(0f, .6f);
        Lock(false);
        pursuer.Chase(Player, doorway);
    }

    Vector3 FloorBelow(Vector3 p)
    {
        return Physics.Raycast(p + Vector3.up * .5f, Vector3.down, out var h, 3f, ~0, QueryTriggerInteraction.Ignore) && !h.collider.transform.IsChildOf(Player) ? h.point : p - Vector3.up * .9f;
    }

    // ---------------------------------------------------------------- the roof

    // You run out of roof and stop at the edge, your back to the drop. He comes for you, slowly, as if there were no
    // hurry. The adrenaline spikes: you let him come, take hold of him... and pull. You both go over.
    IEnumerator Confront(Vector3 edge)
    {
        cutscene = true;
        Lock(true);
        Objective(null);
        pursuer.Hold();
        if (wind != null) { wind.volume = 0f; wind.Play(); StartCoroutine(Volume(wind, .45f * Sounds.Volume("Prologue/Wind (roof and fall)"), 2f)); }
        rage.alpha = .6f;

        Vector3 out0 = Flat(edge - Player.position);
        if (out0.sqrMagnitude < .25f) out0 = Flat(Player.forward);
        out0.Normalize();

        // he's on the roof with you, a few metres back (he took the last flight while you ran for the edge)
        Vector3 behind = FloorBelow(Player.position - out0 * 3.4f);
        if (Mathf.Abs(killer.transform.position.y - behind.y) > 1.2f || Flat(killer.transform.position - Player.position).magnitude > 6f)
        {
            killer.transform.position = behind;
            killer.transform.rotation = Quaternion.LookRotation(Flat(Player.position - behind));
        }

        // hand over to the cutscene camera from exactly where you're looking, then step to the very edge and turn round
        cine.transform.SetPositionAndRotation(playerCam.transform.position, playerCam.transform.rotation);
        cine.fieldOfView = playerCam.fieldOfView;
        cine.enabled = true;
        if (cineLight != null) cineLight.enabled = true;
        StartCoroutine(Bars(1f));
        if (music != null) { music.volume = Hb(1f); music.pitch = 1.2f; }
        Vector3 c0 = cine.transform.position;
        Quaternion r0 = cine.transform.rotation;
        Vector3 standAt = edge - out0 * .9f;
        standAt.y = c0.y;
        yield return Tween(1f, t =>
        {
            cine.transform.position = Vector3.Lerp(c0, standAt, t);
            cine.transform.rotation = Quaternion.Slerp(r0, Quaternion.LookRotation(killer.transform.position + Vector3.up * 1.6f - cine.transform.position), t);
        });

        // he walks up to you, slowly, knife low; you can't go anywhere
        Say(roofLines);
        Transform kt = killer.transform;
        for (float wait = 0f; ; wait += Time.deltaTime)
        {
            Vector3 toCam = Flat(standAt - kt.position);
            bool close = toCam.magnitude <= 1.6f;
            if (!close)
            {
                kt.position += toCam.normalized * 1.15f * Time.deltaTime;
                if (Physics.Raycast(kt.position + Vector3.up * 1.2f, Vector3.down, out var gh, 2.6f, ~0, QueryTriggerInteraction.Ignore) && !gh.collider.transform.IsChildOf(Player))
                    kt.position = new Vector3(kt.position.x, gh.point.y, kt.position.z);
            }
            kt.rotation = Quaternion.Slerp(kt.rotation, Quaternion.LookRotation(toCam.sqrMagnitude > .01f ? toCam : -out0), 6f * Time.deltaTime);
            if (killer.animator != null) killer.animator.SetFloat("Speed", close ? 0f : 1.3f);
            Breathe(kt.position + Vector3.up * 1.6f);
            if (close && !DialogueBox.Instance.Busy) break;
            if (wait > 40f) break;
            yield return null;
        }
        Vector3 face = kt.position + Vector3.up * 1.6f;

        // adrenaline
        pushPrompt.text = adrenalinePrompt;
        pushPrompt.gameObject.SetActive(true);
        pushNow = false;
        while (!Input.GetKeyDown(KeyCode.F) && !Input.GetMouseButtonDown(0) && !pushNow)
        {
            float p = .5f + .5f * Mathf.Sin(Time.time * 7f);
            rage.alpha = .35f + .45f * p;
            pushPrompt.alpha = .6f + .4f * p;
            Breathe(face);
            yield return null;
        }
        pushPrompt.gameObject.SetActive(false);
        yield return Push(out0, face, true);
    }

    // the shove, the grab, the fall
    IEnumerator Push(Vector3 outward, Vector3 face, bool pulled = false)
    {
        Transform kt = killer.transform;
        Vector3 k0 = kt.position;
        Quaternion kr = kt.rotation;
        Vector3 c0 = cine.transform.position;

        MusicManager.Stop(0.6f);
        Sounds.OneShot(sfx, "Prologue/Push", ProceduralAudio.Thud(), 1f);
        rage.alpha = 1f;
        bool fallClip = killer.animator != null && HasState(killer.animator, killerFallState);
        Vector3 c1, over;
        if (pulled)
        {
            // both hands in his coat: you throw yourself at him...
            Vector3 toward = -outward;
            yield return Tween(0.22f, t =>
            {
                cine.transform.position = Vector3.Lerp(c0, c0 + toward * 0.55f, t);
                cine.transform.rotation = Quaternion.LookRotation(kt.position + Vector3.up * 1.5f - cine.transform.position);
            });
            c1 = cine.transform.position;
            DialogueBox.Instance.SayNow(grabLines);
            if (whisper != null) sfx.PlayOneShot(whisper, 0.7f * Sounds.Volume("Prologue/Whisper (the fall)"));
            if (fallClip) { killer.enabled = false; killer.animator.speed = 1f; killer.animator.CrossFadeInFixedTime(killerFallState, 0.15f); }
            // ...and let your weight go backwards, over the edge, with him in your arms
            over = c1 + outward * 2.6f + Vector3.down * 0.5f;
            Quaternion rs0 = cine.transform.rotation;
            yield return Tween(0.8f, t =>
            {
                cine.transform.position = Vector3.Lerp(c1, over, t * t);
                kt.position = cine.transform.position + toward * 0.85f + Vector3.down * 0.2f * t;
                cine.transform.rotation = Quaternion.Slerp(rs0, Quaternion.LookRotation(kt.position + Vector3.up * 0.3f - cine.transform.position), t);
                rage.alpha = 1f - 0.6f * t;
            });
        }
        else
        {
            // lunge
            yield return Tween(0.16f, t => cine.transform.position = Vector3.Lerp(c0, c0 + outward * 1.2f, t));
            c1 = cine.transform.position;

            // he goes back over the edge, arms out
            DialogueBox.Instance.SayNow(grabLines);
            if (whisper != null) sfx.PlayOneShot(whisper, 0.7f * Sounds.Volume("Prologue/Whisper (the fall)"));
            if (fallClip) { killer.enabled = false; killer.animator.speed = 1f; killer.animator.CrossFadeInFixedTime(killerFallState, 0.15f); }
            Vector3 axis = Vector3.Cross(Vector3.up, outward);
            yield return Tween(0.6f, t =>
            {
                kt.position = k0 + outward * 0.9f * t;
                kt.rotation = Quaternion.AngleAxis((fallClip ? killerFallTilt : 80f) * t * t, axis) * kr;   // the clip throws him back itself
                cine.transform.rotation = Quaternion.LookRotation(kt.position + kt.up * 1.4f - cine.transform.position);
                rage.alpha = 1f - 0.6f * t;
            });

            // yanked after him, over the edge
            over = k0 + outward * 1.6f + Vector3.up * (c1.y - k0.y);
            Quaternion lookDown = Quaternion.LookRotation(outward * 0.35f + Vector3.down);
            Quaternion rs = cine.transform.rotation;
            yield return Tween(0.45f, t =>
            {
                cine.transform.position = Vector3.Lerp(c1, over, t * t);
                cine.transform.rotation = Quaternion.Slerp(rs, lookDown, t);
            });
        }
        rage.alpha = 0f;
        StartCoroutine(Bars(0f));

        // falling. Halfway down the night turns red and time drags.
        if (ambience != null) ambience.Stop();
        if (tv != null) tv.Stop();
        if (wind != null) { if (!wind.isPlaying) wind.Play(); wind.volume = 0.8f; }
        if (music != null) music.volume = Hb(0f);
        var sky = RenderSettings.skybox != null ? new Material(RenderSettings.skybox) : null;
        if (sky != null) RenderSettings.skybox = sky;
        Color fog0 = RenderSettings.fogColor, sky0 = sky != null && sky.HasProperty("_SkyTint") ? sky.GetColor("_SkyTint") : Color.black;
        Color moon0 = moon != null ? moon.color : Color.white;
        float y0 = over.y, y1 = groundY + 1.2f;
        bool whispered = false;
        Quaternion kFall = kt.rotation;
        float end = playerBody != null ? cutToStreetAt : 1f;   // with a body to show, cut to the street before the end
        for (float u = 0f; u < end;)
        {
            bool slow = u > 0.3f && u < 0.62f;
            u += Time.deltaTime / (slow ? 5.5f : 2.2f);
            float y = Mathf.Lerp(y0, y1, u * u);
            Vector3 p = new Vector3(over.x, y, over.z) + outward * fallDrift * u;
            cine.transform.position = p;
            // he's just below you, still holding on: flailing (his clip), or on his back
            kt.position = p + Vector3.down * 1.9f + outward * 0.25f;
            kt.rotation = fallClip ? kFall : Quaternion.LookRotation(Vector3.up, outward);
            cine.transform.rotation = Quaternion.Slerp(cine.transform.rotation,
                Quaternion.LookRotation(kt.position + outward * 0.9f - p, outward), 4f * Time.deltaTime);

            float red = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.6f, u));
            if (hellLook != null) hellLook.weight = red;
            RenderSettings.fogColor = Color.Lerp(fog0, hellFog, red);
            if (sky != null && sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", Color.Lerp(sky0, hellSky, red));
            if (moon != null) moon.color = Color.Lerp(moon0, hellLight, red);
            if (wind != null) wind.pitch = slow ? 0.55f : 1f + u * 0.6f;
            if (slow && !whispered && whisper != null) { whispered = true; sfx.PlayOneShot(whisper, 1f * Sounds.Volume("Prologue/Whisper (the fall)")); }
            yield return null;
        }

        // impact: seen from the street (you, falling flat onto it) or straight to black
        if (playerBody != null)
        {
            Vector3 landed = new Vector3(over.x, groundY, over.z) + outward * fallDrift;
            yield return StreetShot(landed, outward, kt, fallClip);
        }
        fader.fade.alpha = 1f;
        if (wind != null) wind.Stop();
        if (playerBody == null) Sounds.OneShot(sfx, "Prologue/Impact (hitting the street)", ProceduralAudio.Thud(), 1f);
        yield return new WaitForSeconds(playerBody != null ? 2f : 3f);
        SleepSequence.WakeInHell = true;
        if (streetAnchor != null)
        {
            Vector3 landed = new Vector3(over.x, groundY, over.z) + outward * fallDrift;
            SleepSequence.FallSpot = streetAnchor.InverseTransformPoint(landed);
            SleepSequence.FallFacing = streetAnchor.InverseTransformDirection(outward);    // out at the city, not the wall
        }
        SceneManager.LoadScene(nextScene);
    }

    // the last second of the fall from down on the street: a low camera looking up as you come down and hit it
    // flat ("Falling Flat Impact"), him already down beside you. Ends on the impact, a moment of stillness.
    IEnumerator StreetShot(Vector3 landed, Vector3 outward, Transform kt, bool killerClip)
    {
        Vector3 side = Vector3.Cross(Vector3.up, outward);
        // low by the building's wall, looking out over the body at the street and the city
        Vector3 camAt = landed - outward * 3.2f + side * 1.6f + Vector3.up * 0.55f;
        cine.transform.position = camAt;
        cine.transform.rotation = Quaternion.LookRotation(landed + Vector3.up * (bodyDrop * 0.45f) - camAt);
        cine.fieldOfView = 55f;
        StartCoroutine(Bars(1f));
        // the street is dark by now: a streetlamp-ish key over the spot so the bodies read
        var key = new GameObject("FallKeyLight").AddComponent<Light>();
        key.type = LightType.Point;
        key.transform.position = landed + Vector3.up * 3.2f + side * 2f + outward * 1.5f;
        key.color = new Color(1f, 0.55f, 0.45f);
        key.intensity = 8f;
        key.range = 14f;
        key.shadows = LightShadows.None;
        if (cineLight != null) { cineLight.enabled = true; cineLight.intensity = Mathf.Max(cineLight.intensity, 2.5f); cineLight.range = Mathf.Max(cineLight.range, 10f); }

        // he hit first, a little further out, and lies still on the street
        if (killerClip)
        {
            // down on the street just behind the camera, out of the shot (his clip has a leg up in every frame, which
            // reads badly on the ground): the shot is yours
            var ka = killer.animator;
            ka.speed = 0f;
            kt.SetPositionAndRotation(landed - outward * 3.6f + side * 4.5f, Quaternion.LookRotation(-outward));
            var hipsBone = ka.isHuman ? ka.GetBoneTransform(HumanBodyBones.Hips) : null;
            float bestAt = killerLyingAt / 0.9f;
            if (hipsBone != null)
            {
                var limbs = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
                float best = float.MaxValue;
                for (float n = 0f; n < 1f; n += 0.05f)
                {
                    ka.Play(killerFallState, 0, n);
                    ka.Update(0f);
                    float up = 0f;
                    foreach (var b in limbs) { var t = ka.GetBoneTransform(b); if (t != null) up = Mathf.Max(up, t.position.y - hipsBone.position.y); }
                    if (up < best) { best = up; bestAt = n; }
                }
            }
            ka.Play(killerFallState, 0, bestAt);
            ka.Update(0f);
            if (hipsBone != null) kt.position += Vector3.up * (groundY + 0.18f - hipsBone.position.y);   // the clip holds him up at hip height
        }
        else
        {
            kt.position = landed + outward * 1.4f + side * 1.1f;
            kt.rotation = Quaternion.LookRotation(Vector3.up, outward);
        }

        // you: dropping out of the sky held flat (the clip's early frame), then the clip lands you at impactAt
        var body = playerBody.transform;
        playerBody.gameObject.SetActive(true);
        body.SetPositionAndRotation(landed + Vector3.up * bodyDrop, Quaternion.LookRotation(-outward));
        var clips = playerBody.runtimeAnimatorController != null ? playerBody.runtimeAnimatorController.animationClips : null;
        float len = clips != null && clips.Length > 0 ? clips[0].length : 1.57f;
        playerBody.speed = 0f;
        playerBody.Play(playerFallState, 0, bodyPoseAt / len);
        playerBody.Update(0f);

        bool hit = false, landing = false;
        float impactTime = preFall + (impactAt - bodyPoseAt);
        for (float t = 0f; t < impactTime + holdAfterImpact; t += Time.deltaTime)
        {
            if (t < preFall)
                body.position = landed + Vector3.up * bodyDrop * (1f - Mathf.Pow(t / preFall, 1.3f));
            else if (!landing)
            {
                landing = true;
                body.position = landed;
                playerBody.speed = 1f;   // from the held frame on: the clip's own last drop and the impact
            }
            if (!hit && t >= impactTime)
            {
                hit = true;
                Sounds.OneShot(sfx, "Prologue/Impact (hitting the street)", ProceduralAudio.Thud(), 1f);
                if (wind != null) wind.Stop();
                StartCoroutine(Shake(cine.transform, 0.45f, 0.12f));
            }
            // the camera follows him down, settling on the body
            Vector3 look = Vector3.Lerp(landed + Vector3.up * (bodyDrop * 0.45f), body.position + Vector3.up * 0.8f, Mathf.Clamp01(t / impactTime));
            if (hit) look = landed + Vector3.up * 0.4f;
            cine.transform.rotation = Quaternion.Slerp(cine.transform.rotation, Quaternion.LookRotation(look - cine.transform.position), 10f * Time.deltaTime);
            yield return null;
        }
    }

    IEnumerator Shake(Transform t, float time, float amount)
    {
        Vector3 p0 = t.position;
        for (float s = 0f; s < time; s += Time.deltaTime)
        {
            t.position = p0 + Random.insideUnitSphere * amount * (1f - s / time);
            yield return null;
        }
        t.position = p0;
    }

    static bool HasState(Animator a, string state) => a.runtimeAnimatorController != null && a.HasState(0, Animator.StringToHash(state));

    // ---------------------------------------------------------------- helpers

    void Say(string lines)
    {
        if (!string.IsNullOrEmpty(lines) && DialogueBox.Instance != null) DialogueBox.Instance.Say(lines);
    }

    // (ObjectiveUI draws it now, the same in every scene: the scene's own Objective text stays off)
    void Objective(string text)
    {
        if (objective != null) objective.gameObject.SetActive(false);
        ObjectiveUI.Set(text);
    }

    void Lock(bool on)
    {
        foreach (var b in lockDuring) if (b != null) b.enabled = !on;
        var rb = fpc.GetComponent<Rigidbody>();
        if (rb == null) return;
        if (on && !rb.isKinematic) rb.linearVelocity = Vector3.zero;
        rb.isKinematic = on;   // scripted walks and turns: physics keeps its hands off
    }

    // is this in front of the player's eyes, close enough, and not behind a wall?
    bool Sees(Vector3 point, float angle, float range, Transform owner)
    {
        Vector3 eye = playerCam.transform.position;
        Vector3 to = point - eye;
        if (to.magnitude > range || Vector3.Angle(playerCam.transform.forward, to) > angle) return false;
        foreach (var h in Physics.RaycastAll(eye, to.normalized, to.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(Player)) continue;
            if (owner != null && h.collider.transform.IsChildOf(owner)) continue;
            return false;
        }
        return true;
    }

    // turn the (locked) player to look at a point, then hand the pitch back to the controller
    IEnumerator LookAt(Vector3 point, float time)
    {
        Quaternion y0 = Player.rotation, p0 = playerCam.transform.localRotation;
        Vector3 d = point - playerCam.transform.position;
        Quaternion y1 = Quaternion.LookRotation(Flat(d));
        Quaternion p1 = Quaternion.Euler(-Mathf.Atan2(d.y, Flat(d).magnitude) * Mathf.Rad2Deg, 0f, 0f);
        yield return Tween(time, t =>
        {
            Player.rotation = Quaternion.Slerp(y0, y1, t);
            playerCam.transform.localRotation = Quaternion.Slerp(p0, p1, t);
        });
        fpc.SetPitch(playerCam.transform.localEulerAngles.x);
    }

    void Aim(Vector3 point)
    {
        Vector3 d = point - playerCam.transform.position;
        Player.rotation = Quaternion.LookRotation(Flat(d));
        playerCam.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(d.y, Flat(d).magnitude) * Mathf.Rad2Deg, 0f, 0f);
        fpc.SetPitch(playerCam.transform.localEulerAngles.x);
    }

    // heavy breathing on the roof: the shot sways
    void Breathe(Vector3 face)
    {
        float s = Mathf.Sin(Time.time * 2.4f);
        cine.transform.rotation = Quaternion.LookRotation(face - cine.transform.position) * Quaternion.Euler(s * 0.8f, 0f, s * 0.6f);
    }

    static float Hb(float v) => v * Sounds.Volume("Prologue/Heartbeat");

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude < 0.0001f ? Vector3.forward : v; }

    static IEnumerator Tween(float time, System.Action<float> step)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            step(Mathf.SmoothStep(0f, 1f, t / time));
            yield return null;
        }
        step(1f);
    }

    IEnumerator PlayLater(AudioClip c, float delay, float volume)
    {
        yield return new WaitForSeconds(delay);
        sfx.PlayOneShot(c, volume);
    }

    void SetBars(float k)
    {
        foreach (var b in new[] { barTop, barBottom })
            if (b != null) b.sizeDelta = new Vector2(b.sizeDelta.x, barHeight * k);
    }

    IEnumerator Bars(float to)
    {
        float from = barTop != null ? barTop.sizeDelta.y / barHeight : 0f;
        yield return Tween(0.5f, t => SetBars(Mathf.Lerp(from, to, t)));
    }

    static IEnumerator Volume(AudioSource a, float to, float time)
    {
        float from = a.volume;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            a.volume = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        a.volume = to;
    }

    static IEnumerator FadeText(TextMeshProUGUI txt, float to, float time)
    {
        float from = txt.alpha;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            txt.alpha = Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        txt.alpha = to;
    }
}
