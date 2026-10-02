using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// The prologue (its own scene: the apartment block, the night Derek dies), start to finish:
//   home from work -> the flat door is open -> "Derek? I'm home!" -> his body on the living-room floor
//   -> the living-room camera's tape: the masked man killing him, twenty minutes ago
//   -> the closet creaks: he never left. He's creeping out behind you -> you turn: YOU!
//   -> the chase: out of the flat, up the stairwell, onto the roof. Nowhere left to run
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
    [TextArea] public string upLines = "He's going up!|Get back here!";
    [TextArea] public string topLines = "The roof... there's nothing up there.|He's trapped.";
    [TextArea] public string confrontLines = "Nowhere left to run.|Why him? WHY DEREK?!|\"...ask your boss about Tomas.\"|Tomas...? What the hell does that--|No. I don't care.|You're going to pay for this.";
    [TextArea] public string grabLines = "He grabs my wrist--|NO--!";

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
        killer.gameObject.SetActive(true);
        killer.transform.position = edge;
        killer.Stop();
        Vector3 back = Flat(prev - edge).normalized;
        var rb = fpc.GetComponent<Rigidbody>();
        Player.SetPositionAndRotation(edge + back * 2.6f + Vector3.up * 0.9f, Quaternion.LookRotation(-back));
        if (rb != null) rb.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        StartCoroutine(Confront());
    }
    private float nextLineY = 9f;

    Transform Player => fpc.transform;

    void Start()
    {
        if (derekRig != null) { derekRig.Play(stabState, 0, 1f); derekRig.Update(0f); }   // he's already down when you get home
        foreach (var g in killerInRig) if (g != null) g.SetActive(false);
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
        if (pc != null) { pc.enabled = false; pc.onUse.AddListener(() => StartCoroutine(WatchTape())); }
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
        if (tv != null) { if (tv.clip == null) tv.clip = ProceduralAudio.TvMurmur(); tv.loop = true; tv.Play(); }
        SetBars(0f);
        Objective(null);
        StartCoroutine(Story());
    }

    // ---------------------------------------------------------------- the night

    IEnumerator Story()
    {
        // over black: the time, the street
        if (deliveries != null) deliveries.Prepare();   // in the van before the picture comes up
        Lock(true);
        fader.fade.alpha = 1f;
        yield return null;
        fader.fade.alpha = 1f;   // the fader's own Start clears it
        if (ambience != null) { ambience.volume = 0f; ambience.Play(); StartCoroutine(Volume(ambience, 0.5f * Sounds.Volume("Prologue/Street ambience"), 4f)); }
        if (titleCard != null)
        {
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

        // the second floor: the flat door stands open
        yield return new WaitUntil(() => hallway.Contains(Player.position));
        Say(doorOpenLines);
        if (music != null && heartbeat != null) { music.clip = heartbeat; music.loop = true; music.volume = Hb(0.25f); music.Play(); }

        // inside
        yield return new WaitUntil(() => flat.Contains(Player.position));
        DialogueBox.Instance.SayNow(callOut);
        Objective(findDerek);
        if (ambience != null) StartCoroutine(Volume(ambience, 0.15f * Sounds.Volume("Prologue/Street ambience"), 3f));
        StartCoroutine(NoAnswer());

        yield return new WaitUntil(() => Sees(body.position, 32f, 8f, body));
        yield return FindBody();

        yield return new WaitUntil(() => tapeDone);
        yield return TheKillerIsStillHere();

        // the chase: up and up
        while (!(killer.Cornered && Vector3.Distance(Player.position, killer.transform.position) < confrontDistance))
        {
            if (Player.position.y > nextLineY)
            {
                Say(nextLineY < 15f ? upLines : topLines);
                nextLineY = nextLineY < 15f ? 16.8f : float.MaxValue;   // 16.8: standing on the top landing (roof 16.1)
                if (nextLineY == float.MaxValue && wind != null) { wind.volume = 0f; wind.Play(); StartCoroutine(Volume(wind, 0.45f * Sounds.Volume("Prologue/Wind (roof and fall)"), 2f)); }
            }
            yield return null;
        }
        yield return Confront();
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
        // a clean picture for this one shot: no grade, no PSX pixelation/dither (the roof wants them back)
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

    IEnumerator FindBody()
    {
        found = true;
        Lock(true);
        if (tv != null) StartCoroutine(Volume(tv, 0.05f * Sounds.Volume("Prologue/TV murmur"), 1.5f));
        if (sting != null) sfx.PlayOneShot(sting, 0.8f * Sounds.Volume("Prologue/Sting (body, spotted)"));
        if (music != null) { music.volume = Hb(0.8f); music.pitch = 1.15f; }
        yield return LookAt(body.position, 1.1f);
        DialogueBox.Instance.SayNow(bodyLines);
        // walk up to him, slowly, whatever you do
        Vector3 from = Player.position;
        Vector3 to = body.position + Flat(from - body.position).normalized * 1.3f;
        to.y = from.y;
        for (float t = 0f; t < 2.2f; t += Time.deltaTime)
        {
            Player.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 2.2f));
            Aim(body.position);
            yield return null;
        }
        // kneel
        Vector3 cam0 = playerCam.transform.localPosition;
        yield return Tween(1f, k => { playerCam.transform.localPosition = cam0 + Vector3.down * 0.55f * k; Aim(body.position); });
        while (DialogueBox.Instance.Busy) yield return null;
        yield return new WaitForSeconds(0.6f);
        yield return Tween(0.8f, k => playerCam.transform.localPosition = cam0 + Vector3.down * 0.55f * (1f - k));
        playerCam.transform.localPosition = cam0;
        if (music != null) { music.volume = Hb(0.3f); music.pitch = 1f; }

        Say(cameraIdea);
        Objective(checkTape);
        if (pc != null) pc.enabled = true;
        Lock(false);
    }

    // ---------------------------------------------------------------- the tape

    IEnumerator WatchTape()
    {
        cutscene = true;
        Lock(true);
        Objective(null);
        Say(tapeStart);
        yield return fader.FadeTo(1f, 0.6f);

        foreach (var g in killerInRig) if (g != null) g.SetActive(true);
        if (bloodPool != null) bloodPool.SetActive(false);
        derekRig.Play(stabState, 0, 0f);
        cctvCam.enabled = true;
        cctvOverlay.SetActive(true);
        if (cctvLook != null) cctvLook.weight = 1f;
        if (music != null) music.volume = Hb(0.1f);
        if (stabs != null) StartCoroutine(PlayLater(stabs, 1.6f, 0.6f * Sounds.Volume("Prologue/Stabbing on the tape")));
        StartCoroutine(fader.FadeTo(0f, 0.5f));

        var overlayRt = (RectTransform)cctvOverlay.transform;
        Vector2 home = overlayRt.anchoredPosition;
        for (float t = 0f; t < tapeLength; t += Time.deltaTime)
        {
            int s = tapeStartSeconds + Mathf.FloorToInt(t);
            cctvClock.text = $"<color=#ff2a2a>● REC</color>   CAM 01 - LIVING ROOM   {s / 3600 % 24:00}:{s / 60 % 60:00}:{s % 60:00}";
            bool glitch = Random.value < 0.04f;   // tracking noise
            overlayRt.anchoredPosition = home + (glitch ? new Vector2(Random.Range(-10f, 10f), 0f) : Vector2.zero);
            cctvCam.transform.localRotation = Quaternion.Euler(glitch ? Random.Range(-0.4f, 0.4f) : 0f, 0f, 0f) * Quaternion.identity;
            yield return null;
        }
        overlayRt.anchoredPosition = home;

        yield return fader.FadeTo(1f, 0.4f);
        foreach (var g in killerInRig) if (g != null) g.SetActive(false);
        if (bloodPool != null) bloodPool.SetActive(true);
        cctvCam.enabled = false;
        cctvOverlay.SetActive(false);
        if (cctvLook != null) cctvLook.weight = 0f;
        yield return fader.FadeTo(0f, 0.8f);
        Lock(false);
        cutscene = false;
        tapeDone = true;
    }

    // ---------------------------------------------------------------- he never left

    IEnumerator TheKillerIsStillHere()
    {
        Say(afterTape);
        if (music != null) music.volume = Hb(0.5f);
        while (DialogueBox.Instance.Busy) yield return null;
        yield return new WaitForSeconds(1.8f);

        // the closet behind you creaks open; soft steps across the floor
        if (music != null) music.volume = Hb(0f);
        if (closet != null) closet.Open();
        yield return new WaitForSeconds(1.2f);
        killer.Sneak();
        yield return new WaitForSeconds(1.4f);
        Say(footstepLines);

        bool spotted = false;
        for (float t = 0f; t < sneakTimeout && !killer.AtFlatDoor && !spotted; t += Time.deltaTime)
        {
            spotted = Sees(killer.Chest, spotAngle, 14f, killer.transform);
            yield return null;
        }

        if (spotted)
        {
            // eye contact: the world stops for a moment
            killer.Stop();
            killer.transform.rotation = Quaternion.LookRotation(Flat(Player.position - killer.transform.position));
            if (sting != null) sfx.PlayOneShot(sting, 1f * Sounds.Volume("Prologue/Sting (body, spotted)"));
            if (music != null) { music.volume = Hb(0.9f); music.pitch = 1.3f; }
            Time.timeScale = 0.3f;
            yield return new WaitForSecondsRealtime(0.9f);
            Time.timeScale = 1f;
            DialogueBox.Instance.SayNow(spottedLine);
            yield return new WaitForSeconds(0.35f);
        }
        else
        {
            DialogueBox.Instance.SayNow(escapedLines);
            if (music != null) { music.volume = Hb(0.9f); music.pitch = 1.3f; }
        }

        killer.Flee();
        MusicManager.Area("Prologue Chase");
        Objective(chase);
        fpc.unlimitedSprint = true;
        foreach (var l in stairLights) if (l != null) l.enabled = true;
    }

    // ---------------------------------------------------------------- the roof

    IEnumerator Confront()
    {
        cutscene = true;
        Lock(true);
        Objective(null);
        killer.Stop();
        Vector3 k = killer.transform.position;
        Vector3 face = k + Vector3.up * 1.65f;

        // hand over to the cutscene camera from exactly where you're looking
        cine.transform.SetPositionAndRotation(playerCam.transform.position, playerCam.transform.rotation);
        cine.fieldOfView = playerCam.fieldOfView;
        cine.enabled = true;
        if (cineLight != null) cineLight.enabled = true;
        StartCoroutine(Bars(1f));
        Vector3 out0 = Flat(k - Player.position).normalized;           // off the roof, past him
        killer.transform.rotation = Quaternion.LookRotation(-out0);
        Vector3 standAt = k - out0 * 2.4f;
        standAt.y = playerCam.transform.position.y;
        Vector3 c0 = cine.transform.position;
        Quaternion r0 = cine.transform.rotation;
        yield return Tween(1.2f, t =>
        {
            cine.transform.position = Vector3.Lerp(c0, standAt, t);
            cine.transform.rotation = Quaternion.Slerp(r0, Quaternion.LookRotation(face - cine.transform.position), t);
        });

        if (music != null) { music.volume = Hb(1f); music.pitch = 1.2f; }
        Say(confrontLines);
        while (DialogueBox.Instance.Busy)
        {
            Breathe(face);
            yield return null;
        }

        // rage
        pushPrompt.gameObject.SetActive(true);
        pushNow = false;
        while (!Input.GetKeyDown(KeyCode.F) && !Input.GetMouseButtonDown(0) && !pushNow)
        {
            float p = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
            rage.alpha = 0.35f + 0.45f * p;
            pushPrompt.alpha = 0.6f + 0.4f * p;
            Breathe(face);
            yield return null;
        }
        pushPrompt.gameObject.SetActive(false);
        yield return Push(out0, face);
    }

    // the shove, the grab, the fall
    IEnumerator Push(Vector3 outward, Vector3 face)
    {
        Transform kt = killer.transform;
        Vector3 k0 = kt.position;
        Quaternion kr = kt.rotation;
        Vector3 c0 = cine.transform.position;

        // lunge
        MusicManager.Stop(0.6f);
        Sounds.OneShot(sfx, "Prologue/Push", ProceduralAudio.Thud(), 1f);
        rage.alpha = 1f;
        yield return Tween(0.16f, t => cine.transform.position = Vector3.Lerp(c0, c0 + outward * 1.2f, t));
        Vector3 c1 = cine.transform.position;

        // he goes back over the edge, arms out
        DialogueBox.Instance.SayNow(grabLines);
        if (whisper != null) sfx.PlayOneShot(whisper, 0.7f * Sounds.Volume("Prologue/Whisper (the fall)"));
        bool fallClip = killer.animator != null && HasState(killer.animator, killerFallState);
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
        Vector3 over = k0 + outward * 1.6f + Vector3.up * (c1.y - k0.y);
        Quaternion lookDown = Quaternion.LookRotation(outward * 0.35f + Vector3.down);
        Quaternion rs = cine.transform.rotation;
        yield return Tween(0.45f, t =>
        {
            cine.transform.position = Vector3.Lerp(c1, over, t * t);
            cine.transform.rotation = Quaternion.Slerp(rs, lookDown, t);
        });
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

    void Objective(string text)
    {
        if (objective == null) return;
        objective.gameObject.SetActive(!string.IsNullOrEmpty(text));
        objective.text = "> " + text;   // the pixel font has no ▸
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
