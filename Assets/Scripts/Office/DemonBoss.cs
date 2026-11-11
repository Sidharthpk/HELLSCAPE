using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

// Kessler's true form: the thing that was wearing Arthur Kessler. Killing his damned body in the office only
// let it out. It waits under the bridge - the one way out of the city - and it's why the horde won't set foot
// on it. The last fight before the judgement.
//
// Begin() from a StoryTrigger partway onto the bridge: the car is stopped, it climbs up out of the river
// (letterboxed), wrecks the car and the fight starts. Three phases (100-65%, 65-30%, <30%), the same four blood
// attacks in each but done differently (DemonBossBlood.cs): blood shards, the ground smash, corrupted blood pools,
// body-part bombs. From phase 2 the drowned climb up over the rails. Below heartsBelow it floats up sealed in a
// shield of blood and body parts until the black hearts are shot, then crashes down for the last of it.
// Its glowing heart takes triple damage. Each new phase drops a health pickup. At zero it burns and falls back
// into the river, and onDefeated plays the judgement (EndingSequence.PlayEnding).
//
// The model: put anything under `model` (the user's own demon). Animation is optional: states are played by the
// names below when the Animator has them, a "Speed" float is set if there is one; otherwise it's all procedural.
public partial class DemonBoss : MonoBehaviour
{
    public static DemonBoss Active;

    [Header("Scene")]
    public Transform player;
    public PlayerHealth playerHealth;
    public Transform model;                   // the mesh root (child). Swap your model in here.
    public Animator animator;                 // optional
    public Transform heart;                   // weak point (child with a collider + glow)
    public Transform arena;                   // centre of the bridge deck, +z along the bridge toward the dead end
    public float arenaHalfLength = 29f;
    public float arenaHalfWidth = 7f;
    public Transform riseFrom;                // under the water beside the bridge
    public Transform standAt;                 // where it lands on the deck
    public GameObject[] fightWalls;           // invisible walls round the deck, on only during the fight
    public GameObject zombiePrefab;           // the drowned: PrimeZombie, AI stripped
    public GameObject healthPickup;           // disabled template
    public Checkpoint checkpoint;             // "Bridge": die in the fight and you're back at the start of it

    [Header("Car")]
    public CarInteract carInteract;
    public Rigidbody carRb;

    [Header("Cutscene")]
    public Camera cutsceneCam;
    public RectTransform barTop, barBottom;
    public float barHeight = 90f;
    public ScreenFader fader;
    public string musicArea = "Demon Fight";

    [Header("UI")]
    public GameObject healthBar;
    public RectTransform healthFill;          // anchored left, scaled on x
    public TextMeshProUGUI bossName;
    public string displayName = "KESSLER, WHAT WORE HIM";

    [Header("Stats")]
    public float maxHealth = 3200f;
    public float heartMultiplier = 3f;
    public float stunMultiplier = 2f;
    public float walkSpeed = 3.2f, enragedWalk = 5f;
    public float turnRate = 3f;

    [Header("Slam (the smash's windup, damage and recovery; the chase's blow)")]
    public float slamWindup = 1.15f;
    public float slamDamage = 32f;
    public float slamRecover = 1.1f;

    [Header("Bolts (the chase)")]
    public float boltDamage = 12f;
    public float boltFlight = 1.1f;

    [Header("Last phase: the black hearts")]
    [Range(0f, 1f)] public float heartsBelow = 0.18f;   // at this health it flies up, sealed, and the hearts come up out of the deck
    public int blackHearts = 3;
    public float blackHeartHealth = 210f;    // each (the rifle does 14 a shot)
    public float blackHeartHeight = 2.8f;
    public float hoverHeight = 17f;
    public float shieldRadius = 15f;         // the blood orb round it while it's sealed (holds the whole 24 m Vane)
    public float crashStun = 7f;            // how long it lies stunned (double damage) after the last heart goes
    public string sealedName = "KESSLER - SEALED BY THE BLACK HEARTS";
    [TextArea] public string heartsLines = "KESSLER: You can't kill what doesn't beat in me anymore.|KESSLER: Three hearts. Three sins I buried. Come and dig them up.";
    public string[] heartNameLines;           // V5.1: what is said as the 1st, 2nd, 3rd heart goes (the Driver names a soul, Vane answers)
    [TextArea] public string heartsHint = "It's tied to those black hearts. Nothing touches it until they're gone!";
    [TextArea] public string crashLines = "KESSLER: No--! My hearts--!";

    [Header("The drowned (phase 2+)")]
    public int maxDrowned = 6;
    public float drownedSpeed = 3.6f;
    public float drownedDamage = 8f;

    [Header("Animation state names (only used if the Animator has them)")]
    public string moveState = "Upright";
    public string slamState = "Attack";
    public string roarState = "Scream";
    public string throwState = "NeckBite";
    public string chargeState = "Upright";
    public string idleState = "";             // empty: the move state at speed 0
    public string stunState = "";
    public string dieState = "";
    public float moveClipSpeed = 3.67f;

    [Header("Lines")]
    [TextArea] public string riseLines = "KESSLER: Did you think that was ME, in the office? That was a coat I wore.|KESSLER: Forty years I fed this city the dead. It fed me back.|KESSLER: Nobody leaves my city. Not the damned. Not the lost. Not YOU.";
    [TextArea] public string phase2Lines = "KESSLER: Every body I sold, I kept a little. Come up, all of you. Say hello.";
    [TextArea] public string phase3Lines = "KESSLER: Your brother BEGGED me for that money!|KESSLER: I made you both. I can unmake you.";
    [TextArea] public string deathLines = "KESSLER: No... the river... it's taking me DOWN--|KESSLER: They'll judge you too, driver. You signed it. You SIGNED--";
    [TextArea] public string afterLines = "It's over. It's really over.|Now... whatever's waiting at the end of this bridge.";

    public UnityEvent onDefeated = new UnityEvent();

    [Header("The arena fight, in the van")]
    public bool fightInVan = true;           // you keep the van: drive, dodge the orbs, shoot (right mouse) and ram
    public GameObject orbModel;              // the blood orb (the old escape's BloodWave orb): the bombs - a sphere if empty
    public float chaseVanDamage = 0.5f;      // everything it throws at the van while you drive away is halved
    public float vanDamageScale = 1.3f;      // its slams and charges hurt the van this much more than they'd hurt you
    public float ramMinSpeed = 7f;           // m/s: ram it faster than this and it hurts
    public float ramDamagePerMs = 9f;
    public float ramMax = 170f;
    public bool chaseBolts = false;          // blood bolts during the chase (off: the chase is just the escape)
    public float crawlFrom = 735f;           // metres along the road (the bridge): the van gets here and he drops to the ground and crawls
    public float chaseHold = 18f;            // further behind the van than this and he keeps up with it, however fast it goes: he stays in the picture

    [Header("The escape: it bursts out of the office and hunts the van to the bridge")]
    public Transform[] chaseRoad;            // the escape road's points, in order (the horde's road)
    public Transform officeDoor;             // where it bursts out, facing the street
    public Transform fightStart;             // the city end of the arena: where it stands when the fight begins
    public Transform playerStart;            // where you're standing when it lands (empty: near the far end)
    public float chaseNear = 11f;            // m/s right behind you
    public float chaseFar = 26f;             // m/s when you've pulled away: it never falls far behind
    public float chaseFarDistance = 90f;
    public float chaseClose = 35f;           // inside this it gains at most chaseGain on a moving van
    public float chaseGain = 2.5f;
    public float catchDistance = 7.5f;
    public float catchDamage = 30f;          // to the van, per blow
    public float catchCooldown = 2.2f;
    public string chaseMusic = "Escape";
    [Range(0f, 1f)] public float chaseHealthFloor = 0.66f;   // shooting it from the van can take it this far down, no further
    public float chaseStaggerDamage = 220f;  // this much damage from the van and it staggers
    public float chaseStaggerTime = 1.4f;
    public Vector2 chaseBoltEvery = new Vector2(5f, 8f);     // it lobs blood at the road ahead of the van: swerve
    public float boltVanDamage = 16f;
    [TextArea] public string burstLines = "Something's tearing the office doors apart--|Kessler: DRIVER! Nobody leaves MY city!|DRIVE!";
    [TextArea] public string arriveLines = "The bridge just... ends.|Kessler: End of the road, driver.";
    public GameObject damnedForm;            // Kessler's damned body from the office: the cutscene opens on it
    public string damnedScreamState = "Scream";   // on its animator; zombieattack if it hasn't got one
    [TextArea] public string transformLine = "Kessler: Then LOOK at what I really am--";
    public string driveLine = "DRIVE!";
    public float burstFog = 0.008f;
    [Header("The change: he walks clear of the office first (the monster doesn't fit in a doorway)")]
    public float walkOut = 11f;              // metres out from the doors before he stops and turns
    public float walkOutSpeed = 1.7f;
    public float transformClearance = 4f;    // open space he needs around him where he changes
    [Header("Or the office comes down and the final form walks out of the blast (set brokenOffice to use it)")]
    public GameObject officeBuilding;        // the whole office in the city: hidden as it blows
    public GameObject brokenOffice;          // Building_Fractured fitted over it, off: each run blows up a fresh copy
    public GameObject smallBlast, bigBlast, dustBlast, fireBall, flames, smoke, sparks;   // Particle Pack prefabs
    public float collapsePush = 6f;          // m/s the chunks are thrown out from the blast
    public float emergeWalk = 9f;            // metres he walks out of the smoke towards the van
    public Transform kvSign;                 // the KV Logistics sign over the door: he rips it off and throws it at you
    public float signSpeed = 34f;            // m/s through the air
    public float signOvershoot = 16f;        // how far past the van it comes down
    public float signRelease = 1f;           // seconds into the throw clip it leaves his hand
    [Header("The arena: it leaps over you and lands in your way")]
    public float leapAhead = 20f;            // how far in front of you it comes down
    public float leapHeight = 13f;
    [TextArea] public string nowhereLine = "Kessler: NOWHERE LEFT TO RUN, DRIVER!";
    [Header("Arena lighting: a touch brighter than the city once the fight starts")]
    public float arenaExposure = 0.25f;      // the hell volume's post exposure (city: -0.2)
    public float arenaSun = 0.75f;           // sun intensity (city: 0.4)
    public float arenaAmbient = 1.4f;        // ambient multiplier
    public float arenaFog = 0f;              // fog density on the bridge (city: 0.03); none, the user wants it clear
    public float arenaBloom = 0.7f;          // the hell volume's bloom x this in the fight (2.1 -> ~1.5)
    public float arenaChromatic = 0.5f;      // chromatic aberration in the fight (the city has 0.25)
    [Header("Footsteps: a deep stomp every stride")]
    public float stompSpacing = 7f;           // metres per footfall
    public float stompLevel = 0.4f;
    [Header("Its death: the drowned drag it into the water")]
    public float waterY = -7.6f;             // the water's surface under the bridge
    public int dragCount = 8;                // how many of the drowned come over the rail for it
    public Behaviour[] lockDuring;           // your movement / look / weapons, off while the cutscene plays          // fog density while it bursts out of the office (a little thinner than the chase)
    [Header("The tunnel: it can't follow you in")]
    public int tunnelFrom = 7;               // chaseRoad index where the tunnel starts
    public int tunnelTo = 11;                // ...and where it comes out
    public float tunnelJumpAhead = 22f;      // (old: it landed in front of the van)
    public float tunnelJumpDelay = 3f;       // seconds after you come out of the tunnel before it drops down behind you
    public float tunnelJumpBehind = 11f;     // it lands this far behind the van
    [TextArea] public string tunnelInLine = "It stopped at the tunnel-- it's too big to fit--|...something's running across the roof.";
    [TextArea] public string tunnelOutLine = "It came off the tunnel roof-- it's RIGHT BEHIND ME!";

    // ---------------------------------------------------------------- state

    bool chasing, chaseBegun, crawling;
    public bool Crawling => crawling;
    int chaseSeg;
    float catchReady, vanSpeed, trampleAt, staggerUntil, staggerDamage, chaseBoltAt;
    Vector3 lastVan;

    float ramReady;
    readonly List<GameObject> orbs = new List<GameObject>();

    bool InVan => carInteract != null && carInteract.InCar;
    Transform Tgt => InVan && carRb != null ? carRb.transform : player;   // what its attacks go for

    int phase;
    float health, actTimer, summonTimer;
    bool fighting, busy, stunned, defeated, begun;
    bool sealedBoss, hovering, heartsDone;
    readonly List<BossHeart> hearts = new List<BossHeart>();
    BossShield shield;
    readonly List<GameObject> spawned = new List<GameObject>();
    readonly List<Drowned> drowned = new List<Drowned>();
    GameObject zombieTemplate;
    GameObject damnedCopy;                   // taken at load: the office fight kills (and deletes) the real one
    Material ringMat, boltMat;
    Vector3 homePos; Quaternion homeRot;

    class Drowned { public Zombie z; public Transform t; public Animator a; public float hitAt; public float climb; public Vector3 climbFrom, climbTo; }

    public bool Defeated => defeated;
    public bool IsChasing => chasing;   // the run from the office to the bridge (EscapeRoute marks it)
    public static bool InFight => Active != null && Active.fighting && !Active.chasing && !Active.defeated;   // the boss fight itself, not the run from it

    void Awake()
    {
        health = maxHealth;
        if (player == null) { var p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.transform; }
        if (playerHealth == null && player != null) playerHealth = player.GetComponent<PlayerHealth>();
        if (healthBar != null) healthBar.SetActive(false);
        foreach (var w in fightWalls) if (w != null) w.SetActive(false);
        if (cutsceneCam != null) cutsceneCam.enabled = false;
        homePos = standAt != null ? standAt.position : transform.position;
        homeRot = standAt != null ? standAt.rotation : transform.rotation;
        SetModelVisible(false);   // nothing on the bridge until it climbs up
        if (damnedForm != null)
        {
            bool was = damnedForm.activeSelf;
            damnedForm.SetActive(false);   // copied while off: none of its scripts wake up in the copy
            damnedCopy = Instantiate(damnedForm);
            damnedForm.SetActive(was);
            damnedCopy.name = "Kessler (damned, for the cutscene)";
            foreach (var mb in damnedCopy.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(mb);
            foreach (var j in damnedCopy.GetComponentsInChildren<Joint>(true)) DestroyImmediate(j);   // (ragdoll joints first: they hold the rigidbodies)
            foreach (var c in damnedCopy.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var rb in damnedCopy.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
        }
        ringMat = GlowMat(new Color(1f, 0.08f, 0.02f, 0.55f), new Color(3f, 0.25f, 0.05f));
        boltMat = GlowMat(new Color(0.5f, 0f, 0f, 1f), new Color(4f, 0.3f, 0.1f));
    }

    static Material GlowMat(Color baseCol, Color emission)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(sh);
        m.SetColor("_BaseColor", baseCol);
        m.SetFloat("_Surface", 1f);   // transparent
        m.SetFloat("_Blend", 0f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = 3000;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.color = baseCol + emission * 0.1f;
        return m;
    }

    // ---------------------------------------------------------------- the escape

    // CarInteract.onEnter: the first time you drive off with the keys
    public void BeginChase()
    {
        if (chaseBegun || begun) return;
        chaseBegun = true;
        Active = this;
        StartCoroutine(BurstOut());
    }

    IEnumerator BurstOut()
    {
        var carCtl = carInteract != null ? carInteract.carController : null;
        CutsceneSkip.Extra++;   // hold SPACE: skip the walk out and the change
        bool hadControl = carCtl != null && carCtl.enabled;
        if (carCtl != null) carCtl.enabled = false;
        if (carRb != null) { carRb.linearVelocity = Vector3.zero; carRb.angularVelocity = Vector3.zero; carRb.isKinematic = true; }
        MusicManager.Stop(0.8f);
        float fogBefore = RenderSettings.fogDensity;

        if (fader != null) yield return fader.FadeTo(1f, 0.3f);
        Vector3 door = officeDoor != null ? officeDoor.position : carRb.position - carRb.transform.forward * 25f;
        Vector3 outDir = officeDoor != null ? Flat(officeDoor.forward).normalized : Flat(carRb.position - door).normalized;
        Vector3 van = carRb.position;
        Vector3 toVan = Flat(van - door).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, toVan);
        SetBars(1f);
        RenderSettings.fogDensity = Mathf.Min(fogBefore, burstFog);
        if (cutsceneCam != null) cutsceneCam.enabled = true;
        var rumble = MakeSource(ProceduralAudio.Rumble(), true, 0.6f);

        if (brokenOffice != null && kvSign != null)
        {
            Vector3 stand = door + Flat(van - door).normalized * emergeWalk;
            stand.y = Ground(stand);
            PlanSign(stand, van);
            yield return DamnedApproach(van);
        }
        if (brokenOffice != null)
        {
            // the office blows and comes down; he walks out of the blast already in his final form
            yield return Collapse(door, van);
            toVan = Flat(van - transform.position).normalized;
            side = Vector3.Cross(Vector3.up, toVan);
        }
        else
        {
            // ---- 1. Kessler, still in his damned body, raging at you in close-up
            GameObject body = null;
            Animator bodyAnim = null;
            // he comes out of the doors and walks clear of the building before anything happens: what he turns into
            // is far too big to stand in a doorway
            Vector3 doorStep = door + outDir * 1.2f;
            doorStep.y = Ground(doorStep);
            Vector3 standAtDoor = door + outDir * walkOut;   // (where he stops: out in the open)
            foreach (float d in new[] { walkOut, walkOut + 3f, walkOut - 3f, walkOut + 6f, walkOut - 6f })
            {
                Vector3 p = door + outDir * Mathf.Max(4f, d);
                p.y = Ground(p);
                if (Physics.OverlapSphere(p + Vector3.up * (transformClearance + 0.6f), transformClearance, ~0, QueryTriggerInteraction.Ignore).Any(c => !CamIgnores(c))) continue;
                standAtDoor = p;
                break;
            }
            standAtDoor.y = Ground(standAtDoor);
            toVan = Flat(van - standAtDoor).normalized;
            side = Vector3.Cross(Vector3.up, toVan);
            var fusion = GetComponent<TomasAmalgamation>();
            if (fusion != null && fusion.Prepared)
            {
                body = fusion.CreateTomas(doorStep, Quaternion.LookRotation(Flat(standAtDoor - doorStep).normalized));
                fusion.Stage(doorStep, Quaternion.LookRotation(Flat(standAtDoor - doorStep).normalized));
            }
            else if (damnedCopy != null)
            {
                body = Instantiate(damnedCopy, doorStep, Quaternion.LookRotation(Flat(standAtDoor - doorStep).normalized));
                body.name = "Kessler (damned, cutscene)";
                body.SetActive(true);
                bodyAnim = body.GetComponentsInChildren<Animator>().FirstOrDefault(a => a.enabled) ?? body.GetComponentInChildren<Animator>();   // (the Cry Wolf body: its own animator; the old one is off)
            }
            int scream = Animator.StringToHash(damnedScreamState);
            bool canScream = bodyAnim != null && bodyAnim.HasState(0, scream);
            transform.SetPositionAndRotation(door - outDir * 6f + Vector3.down * 30f, Quaternion.LookRotation(toVan));   // the monster: not yet
            gameObject.SetActive(true);
            SetModelVisible(false);

            // the walk out, the camera backing away in front of him
            if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.3f));
            if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(burstLines);
            if (body != null)
            {
                Vector3 walkDir = Flat(standAtDoor - doorStep).normalized;
                Vector3 walkSide = Vector3.Cross(Vector3.up, walkDir);
                float dist = Flat(standAtDoor - doorStep).magnitude, walked = 0f;
                float headUp = Mathf.Clamp(BoundsOf(body).max.y - doorStep.y - 0.2f, 1.2f, 2.4f);
                if (bodyAnim != null) { bodyAnim.applyRootMotion = false; SetWalking(bodyAnim, true); }
                while (walked < dist)
                {
                    walked += walkOutSpeed * Time.deltaTime;
                    Vector3 p = Vector3.Lerp(doorStep, standAtDoor, Mathf.Clamp01(walked / dist));
                    p.y = Ground(p);
                    body.transform.SetPositionAndRotation(p, Quaternion.LookRotation(walkDir));
                    if (fusion != null) fusion.Follow(p, walkDir);
                    Vector3 f = p + Vector3.up * headUp;
                    Place(SafeCam(f, f + walkDir * 3.8f + walkSide * 0.9f + Vector3.down * 0.3f) + Random.insideUnitSphere * 0.01f, f);
                    yield return null;
                }
                body.transform.position = standAtDoor;
                if (bodyAnim != null) SetWalking(bodyAnim, false);
                if (fusion != null) fusion.Tomas(0f);
            }

            float headY = body != null ? BoundsOf(body).max.y - 0.2f : standAtDoor.y + 1.8f;
            Vector3 face = new Vector3(standAtDoor.x, headY, standAtDoor.z);
            Vector3 closeFrom = face + toVan * 3.2f + side * 0.6f + Vector3.down * 0.25f;
            Vector3 closeTo = face + toVan * 1.5f + side * 0.3f + Vector3.down * 0.15f;
            var redKey = new GameObject("Kessler key light").AddComponent<Light>();
            redKey.transform.position = face + toVan * 2f + Vector3.up * 0.8f;
            redKey.color = new Color(1f, 0.35f, 0.2f); redKey.intensity = 3f; redKey.range = 8f;
            yield return null;
            float spoke = 0f;
            while (spoke < 11f && (spoke < 2.5f || (DialogueBox.Instance != null && DialogueBox.Instance.Busy)))
            {
                spoke += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, spoke / 7f));
                Place(Vector3.Lerp(closeFrom, closeTo, k) + Random.insideUnitSphere * 0.015f, face);
                if (bodyAnim != null)
                {
                    // screaming at the camera, over and over
                    var info = bodyAnim.GetCurrentAnimatorStateInfo(0);
                    if (canScream) { if (info.shortNameHash != scream || info.normalizedTime > 0.95f) bodyAnim.Play(scream, 0, 0f); }
                    else if (!info.IsName("zombieattack") || info.normalizedTime > 0.92f) bodyAnim.Play("zombieattack", 0, 0f);
                }
                if (body != null) body.transform.rotation = Quaternion.Slerp(body.transform.rotation, Quaternion.LookRotation(toVan), 5f * Time.deltaTime);
                yield return null;
            }

            // ---- 2. the change: it convulses, swells, bleeds, burns red... and the thing inside tears out
            if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(transformLine);
            rumble.Play();
            Vector3 midCam = standAtDoor + toVan * 7f + side * 2.2f + Vector3.up * 1.6f;
            Vector3 s0 = body != null ? body.transform.localScale : Vector3.one;
            float fusionTime = fusion != null ? 5f : 2.6f;
            for (float t = 0f; t < fusionTime; t += Time.deltaTime)
            {
                float k = t / fusionTime;
                if (fusion != null) fusion.Absorb(k);
                Place(Vector3.Lerp(closeTo, midCam, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 2f))) + Random.insideUnitSphere * 0.06f * k, Vector3.Lerp(face, standAtDoor + Vector3.up * 1.4f, k));
                if (body != null)
                {
                    body.transform.localScale = s0 * (1f + 0.55f * k * k) + new Vector3(Random.value, Random.value, Random.value) * 0.06f * k;
                    body.transform.rotation = Quaternion.LookRotation(toVan) * Quaternion.Euler(Random.Range(-12f, 12f) * k, Random.Range(-20f, 20f) * k, Random.Range(-10f, 10f) * k);
                    if (bodyAnim != null) bodyAnim.speed = 1f + 3f * k;
                }
                redKey.intensity = Mathf.Lerp(3f, 18f, k);
                redKey.color = Color.Lerp(new Color(1f, 0.35f, 0.2f), new Color(1f, 0.05f, 0.02f), k);
                if (Random.value < 0.25f + k * 0.5f) BloodFx.Spray(standAtDoor + Vector3.up * Random.Range(0.8f, 2f), Random.onUnitSphere + Vector3.up, 14, 3f + 3f * k);
                rumble.volume = Mathf.Lerp(0.3f, 1f, k);
                yield return null;
            }

            // the flash: the body bursts, the monster is standing where it was
            var img = fader != null ? fader.GetComponent<UnityEngine.UI.Image>() : null;
            Color black = img != null ? img.color : Color.black;
            if (img != null) { img.color = new Color(1f, 0.85f, 0.8f); fader.fade.alpha = 0.95f; }
            for (int i = 0; i < 4; i++) BloodFx.Spray(standAtDoor + Vector3.up * 1.4f, Random.onUnitSphere + Vector3.up, 60, 9f);
            if (body != null) Destroy(body);
            if (fusion != null) fusion.Complete();
            Boom(standAtDoor, 1.5f);
            transform.SetPositionAndRotation(standAtDoor, Quaternion.LookRotation(toVan));
            SetModelVisible(true);
            Transform m = model != null ? model : transform;
            Vector3 full = m.localScale;
            m.localScale = full * 0.3f;
            Play(roarState);
            Roar(1.2f);
            yield return new WaitForSeconds(0.08f);
            if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.6f));

            // it unfolds to its full height, the camera pulling back and tilting up to take it in
            Vector3 wideCam = standAtDoor + toVan * 16f + side * 5f + Vector3.up * 2.2f;
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 2.2f);
                m.localScale = Vector3.Lerp(full * 0.3f, full, 1f - Mathf.Pow(1f - k, 3f));
                Place(Vector3.Lerp(midCam, wideCam, k) + Random.insideUnitSphere * 0.12f * (1f - k), Centre());
                redKey.intensity = Mathf.Lerp(18f, 0f, k);
                yield return null;
            }
            m.localScale = full;
            if (img != null) img.color = black;
            Destroy(redKey.gameObject);
        }

        // ---- 3. it turns on the van and roars: from behind its shoulder, the garage and your van beyond it
        // (the van is parked indoors now: a camera beside it only sees the garage wall)
        // (after the collapse the ruin is behind him: then from beside him on the street, the van down the road)
        Vector3 shoulder = brokenOffice != null ? Centre() + toVan * 15f + side * 7f + Vector3.up * 1f   // (behind him is the burning ruin)
                                                : SafeCam(Centre(), Centre() - toVan * 11f + side * 5f + Vector3.up * 1.2f);
        Vector3 cam = ClearCam(Centre(), shoulder,
            Centre() + side * 9f + toVan * 3f + Vector3.up * 1.5f, Centre() - side * 9f + toVan * 3f + Vector3.up * 1.5f,
            Centre() + side * 6f + toVan * 9f + Vector3.up * 4f);
        // over his shoulder the van is beyond him; from the side, keep him in the picture with the road ahead of him
        Vector3 lookAt = brokenOffice != null ? Centre() : cam == shoulder ? Vector3.Lerp(Centre(), van + Vector3.up * 1.2f, 0.4f) : Centre() + toVan * 4f;
        Place(cam, lookAt);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(driveLine);
        for (float t = 0f; t < 1.8f; t += Time.deltaTime)
        {
            FaceToward(van, 3f);
            Place(cam + Random.insideUnitSphere * 0.06f * (1f - t / 1.8f), lookAt);
            yield return null;
        }
        StartCoroutine(FadeSource(rumble, 1.5f));

        if (fader != null) yield return fader.FadeTo(1f, 0.25f);
        if (cutsceneCam != null) cutsceneCam.enabled = false;
        SetBars(0f);
        RenderSettings.fogDensity = fogBefore;
        if (carRb != null) carRb.isKinematic = false;
        if (carCtl != null && hadControl) carCtl.enabled = true;
        SleepSequence.BloodTide(2f);   // (under the fade: the city goes red behind the blast)
        MusicManager.Area(chaseMusic, 1f);
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.4f));

        chaseSeg = NearestRoad(transform.position);
        routeS = null;   // (measured again from where it stands now)
        lastVan = Flat(carRb.position);
        catchReady = Time.time + 2f;
        chaseBoltAt = Time.time + Random.Range(chaseBoltEvery.x, chaseBoltEvery.y);
        tunnelState = 0;
        if (healthBar != null) healthBar.SetActive(true);
        if (bossName != null) bossName.text = displayName;
        UpdateBar();
        CutsceneSkip.Extra--;
        VanFragility(chaseVanDamage);
        chasing = true;
    }

    // ---------------------------------------------------------------- the office comes down
    // A wide shot from the street: the office shakes, blasts pop along the front, then the big one takes the
    // ground floor out and the whole building (the fractured copy, swapped in for the whole one) crashes down
    // in dust and fire. He walks out of the smoke, already in his final form, and roars.
    GameObject rubble;                       // this run's blown-up copy (a retry gets a fresh one)
    Renderer[] officeRs; Collider[] officeCs;
    float shake;                             // cutscene camera shake, metres; decays on its own

    void ShowOffice(bool whole)
    {
        if (officeBuilding == null) return;
        officeRs ??= officeBuilding.GetComponentsInChildren<Renderer>(true);
        officeCs ??= officeBuilding.GetComponentsInChildren<Collider>(true);
        foreach (var r in officeRs) if (r != null) r.enabled = whole;
        foreach (var c in officeCs) if (c != null) c.enabled = whole;
        if (whole && rubble != null) Destroy(rubble);
    }

    // a Particle Pack effect, scaled up (the pack is made for a few metres); life <= 0 keeps it with the rubble
    GameObject Fx(GameObject prefab, Vector3 at, float scale, float life)
    {
        if (prefab == null) return null;
        var g = Instantiate(prefab, at, Quaternion.identity);
        g.transform.localScale = prefab.transform.localScale * scale;
        bool once = prefab != smoke && prefab != flames;   // (the pack's blasts loop: one bang each here)
        foreach (var ps in g.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.name == "Shockwave") { Destroy(ps.gameObject); continue; }   // its distortion sphere renders as a white bubble
            var m = ps.main; m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            if (once) m.loop = false;
        }
        if (life > 0f) Destroy(g, life);
        else if (rubble != null) g.transform.SetParent(rubble.transform, true);
        return g;
    }

    void Bang(float volume, float pitch)
    {
        var clip = Sounds.Clip("Demon Fight/Office explosion", null);
        var s = MakeSource(clip != null ? clip : ProceduralAudio.Stomp(), false, volume * (clip != null ? Sounds.Volume("Demon Fight/Office explosion") : 1f));
        s.transform.SetParent(null); s.spatialBlend = 0.2f; s.pitch = clip != null ? 1f : pitch;
        s.Play();
        Destroy(s.gameObject, 6f);
    }

    void PlaceShaking(Vector3 pos, Vector3 look)
    {
        shake = Mathf.MoveTowards(shake, 0f, shake * 1.6f * Time.deltaTime + 0.02f * Time.deltaTime);
        Place(pos + Random.insideUnitSphere * shake, look + Random.insideUnitSphere * shake * 0.6f);
        if (cutsceneCam != null) cutsceneCam.transform.Rotate(0f, 0f, Random.Range(-1f, 1f) * shake * 3f, Space.Self);
    }

    Transform signParent; Vector3 signPos; Quaternion signRot; bool signThrown;

    void PutSignBack()
    {
        if (!signThrown || kvSign == null) return;
        signThrown = false;
        kvSign.SetParent(signParent, true);
        kvSign.SetPositionAndRotation(signPos, signRot);
        if (onlookers != null) Destroy(onlookers);
    }

    GameObject onlookers;
    Light onlookerLight;
    Vector3 signPass, signLand, signDir, signOff;
    float signOver;

    // where the sign will come down: past the van on the far side, so ahead of you and off to one side
    void PlanSign(Vector3 at, Vector3 van)
    {
        var signR = kvSign.GetComponent<Renderer>();
        float signHalf = signR != null ? Mathf.Max(signR.bounds.extents.x, signR.bounds.extents.z) : 3f;
        Vector3 aimFrom = at + Vector3.up * 8f;
        signOff = -Vector3.Cross(Vector3.up, Flat(van - at).normalized);
        signPass = van + signOff * (1.2f + signHalf * 0.95f) + Vector3.up * 2f;   // (the sign's edge clears the bodywork by a hair)
        signDir = Flat(signPass - aimFrom).normalized;
        signOver = signOvershoot;
        if (Physics.SphereCast(signPass, 1f, signDir, out RaycastHit wall, signOver, ~0, QueryTriggerInteraction.Ignore) && !CamIgnores(wall.collider)) signOver = Mathf.Max(6f, wall.distance - 1f);
        signLand = signPass + signDir * signOver;
        signLand.y = Ground(signLand) + 0.6f;
    }

    // you start the engine and they notice: a few of the Damned shuffle up the street towards the van, making conversation. Then the ground goes.
    IEnumerator DamnedApproach(Vector3 van)
    {
        Vector3 toThem = Flat(signLand - van).normalized;
        const float walkIn = 6f, pace = 0.75f;
        StandOnlookers(signLand + toThem * walkIn, van);
        onlookerLight = new GameObject("Onlookers light").AddComponent<Light>();   // (a lamp's worth of amber: they're the joke, they have to read)
        onlookerLight.transform.position = signLand - toThem * 3f + Vector3.up * 5f;
        onlookerLight.color = new Color(0.83f, 0.6f, 0.27f); onlookerLight.range = 18f; onlookerLight.intensity = 40f;
        if (onlookers != null) onlookerLight.transform.SetParent(onlookers.transform, true);   // (a retry takes it away with them)
        var anims = onlookers != null ? onlookers.GetComponentsInChildren<Animator>() : new Animator[0];
        foreach (var an in anims) an.SetFloat("Speed", 1f);
        Vector3 hood = ClearCam(signLand + Vector3.up, signLand - toThem * 5f + Vector3.up * 1.5f, signLand - toThem * 8f + Vector3.up * 2f, van + toThem * 7f + Vector3.up * 2.5f);
        Vector3 lookAt = signLand + toThem * walkIn * 0.4f + Vector3.up * 1.1f;
        Vector3 from = onlookers != null ? onlookers.transform.position : Vector3.zero;
        shake = 0f;
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.4f));
        yield return null;
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(HS.S("666-668"));
        float walk = walkIn / pace;
        for (float t = 0f; t < 14f && (t < walk || (DialogueBox.Instance != null && DialogueBox.Instance.Busy)); t += Time.deltaTime)
        {
            if (onlookers != null) onlookers.transform.position = from - toThem * Mathf.Min(walkIn, t * pace);
            Place(hood, lookAt);
            yield return null;
        }
        if (onlookers != null) onlookers.transform.position = from - toThem * walkIn;
        foreach (var an in anims) an.SetFloat("Speed", 0f);
        // then something very big, a long way behind you
        var rum = MakeSource(ProceduralAudio.Rumble(), true, 0.8f);
        rum.Play();
        Bang(0.9f, 0.5f);
        shake = 1.6f;
        for (float t = 0f; t < 1.8f; t += Time.deltaTime)
        {
            shake = Mathf.Max(shake, 0.5f);
            PlaceShaking(hood, lookAt);
            yield return null;
        }
        StartCoroutine(FadeSource(rum, 1.5f));
    }

    // a knot of the city's Damned on the street, idly staring at the van. Stripped to model + animator: nothing here wakes up.
    void StandOnlookers(Vector3 at, Vector3 van)
    {
        var walkers = FindFirstObjectByType<CityWalkers>(FindObjectsInactive.Include);
        if (walkers == null || walkers.zombiePrefab == null) return;
        onlookers = new GameObject("Onlookers (cutscene)");
        onlookers.SetActive(false);
        for (int i = 0; i < 9; i++)
        {
            float r = 1.15f * Mathf.Sqrt(i), a = i * 2.4f;   // (a sunflower: close together, no two on one spot)
            Vector3 p = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            p.y = Ground(p);
            var g = Instantiate(walkers.zombiePrefab, p, Quaternion.LookRotation(Flat(van - p)) * Quaternion.Euler(0f, Random.Range(-14f, 14f), 0f), onlookers.transform);
            foreach (var mb in g.GetComponentsInChildren<MonoBehaviour>(true).Reverse()) DestroyImmediate(mb);
            foreach (var ag in g.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) DestroyImmediate(ag);
            foreach (var j in g.GetComponentsInChildren<Joint>(true)) DestroyImmediate(j);
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var rb in g.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
        }
        onlookers.SetActive(true);
        foreach (var an in onlookers.GetComponentsInChildren<Animator>())
        {
            an.applyRootMotion = false;
            an.speed = Random.Range(0.75f, 1.1f);
            an.Play("Upright", 0, Random.value);
        }
    }

    void CrushOnlookers(Vector3 at)
    {
        if (onlookers != null)
            foreach (Transform g in onlookers.transform.Cast<Transform>().Where(x => x.GetComponent<Light>() == null))
            {
                BloodFx.Spray(g.position + Vector3.up * 0.4f, Vector3.up * 0.6f + Flat(g.position - at).normalized, 90, 11f, null, 1.3f);
                foreach (var an in g.GetComponentsInChildren<Animator>()) an.enabled = false;
                Vector3 s = g.localScale;
                g.localScale = new Vector3(s.x * 1.3f, s.y * 0.06f, s.z * 1.3f);
            }
        for (int i = 0; i < 12; i++) BloodFx.Spray(at + Vector3.up * 0.4f, Vector3.up * 0.5f + Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward, 120, 15f, null, 1.6f);   // squeezed out sideways, all round
        var pool = BloodFx.Pool(new Vector3(at.x, Ground(at), at.z), onlookers != null ? onlookers.transform : null);
        StartCoroutine(SpreadPool(pool, 13f, 2.6f));
    }

    // out from under the sign, fast then slowing
    IEnumerator SpreadPool(GameObject pool, float size, float time)
    {
        for (float t = 0f; t < time && pool != null; t += Time.deltaTime)
        {
            pool.transform.localScale = Vector3.one * size * (1f - Mathf.Pow(1f - t / time, 3f));
            yield return null;
        }
    }

    // he tears the company's sign off the front and throws it at you: it skims past the van by a hair and comes down on the Damned watching you
    IEnumerator ThrowSign(Vector3 at, Vector3 van)
    {
        signParent = kvSign.parent; signPos = kvSign.position; signRot = kvSign.rotation; signThrown = true;
        Transform hand = animator != null ? animator.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.StartsWith("HandMiddle1_R")) : null;
        Vector3 Held() => hand != null ? hand.position : Hand();
        Vector3 toVan = Flat(van - at).normalized, across = Vector3.Cross(Vector3.up, toVan);
        Vector3 wide = ClearCam(Centre(),
            at + across * 15f + toVan * 7f + Vector3.up * 3f, at - across * 15f + toVan * 7f + Vector3.up * 3f,
            at + toVan * 24f + across * 4f + Vector3.up * 5f, at + toVan * 28f + Vector3.up * 6f);

        Vector3 pass = signPass, dir = signDir, off = signOff, land = signLand;
        float over = signOver;

        // meanwhile, down the street: the Damned, just standing there, taking it all in
        Vector3 toThem = Flat(land - van).normalized;
        Vector3 gawk = ClearCam(land + Vector3.up * 1.2f, land - toThem * 8f + Vector3.up * 1.5f, land - toThem * 10f + Vector3.up * 2f, van + toThem * 7f + Vector3.up * 2.5f);
        shake = 0f;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            Place(gawk, land + Vector3.up * 1f);
            yield return null;
        }

        // the grab: he turns, reaches up, and it comes away from the wall in a shower of sparks
        Play(slamState);
        bool torn = false;
        for (float t = 0f; t < 1.1f; t += Time.deltaTime)
        {
            FaceToward(signPos, 5f);
            float k = Mathf.Clamp01((t - 0.65f) / 0.35f);
            if (k > 0f && !torn)
            {
                torn = true;
                kvSign.SetParent(null, true);
                Fx(sparks, signPos, 4f, 4f);
                Bang(0.7f, 1.2f);
                shake = 0.5f;
            }
            kvSign.position = Vector3.Lerp(signPos, Held(), k * k);
            PlaceShaking(wide, Vector3.Lerp(Centre(), signPos, 0.35f));
            yield return null;
        }
        // back round to the van with it, and the throw
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            FaceToward(van, 5f);
            kvSign.position = Held();
            PlaceShaking(wide, Centre());
            yield return null;
        }
        Play(throwState);
        for (float t = 0f; t < signRelease; t += Time.deltaTime)
        {
            FaceToward(van, 5f);
            kvSign.position = Held();
            PlaceShaking(wide, Centre());
            yield return null;
        }

        // the flight, from beside the van: straight at you, past the door mirror, on down the street
        Vector3 start = kvSign.position;
        float passDist = Flat(pass - start).magnitude;
        Vector3 cam = ClearCam(van + Vector3.up * 1.5f,
            van + dir * 6f - off * 1.5f + Vector3.up * 2.2f, van + dir * 6f - off * 1.5f + Vector3.up * 3.5f, van - off * 6f + Vector3.up * 2.5f);
        Transform body = carRb != null ? carRb.transform : null;
        Quaternion bodyRot = body != null ? body.rotation : Quaternion.identity;
        float dur = (passDist + over) / Mathf.Max(1f, signSpeed), passedAt = -1f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float s = t / dur * (passDist + over);
            Vector3 p = s < passDist
                ? Vector3.Lerp(start, pass, s / passDist) + Vector3.up * 5f * Mathf.Sin(Mathf.PI * s / passDist)
                : Vector3.Lerp(pass, land, (s - passDist) / over);
            kvSign.position = p;
            kvSign.Rotate(Vector3.up, 540f * Time.deltaTime, Space.World);
            if (s >= passDist && passedAt < 0f) { passedAt = t; shake = 0.7f; Bang(0.5f, 1.8f); }
            if (passedAt >= 0f && body != null)   // the van rocks on its springs in the wind of it
            {
                float a = (t - passedAt) / 0.7f;
                if (a < 1f) body.rotation = bodyRot * Quaternion.Euler(0f, 0f, Mathf.Sin(a * 22f) * 3.5f * (1f - a));
            }
            PlaceShaking(cam, passedAt >= 0f ? Vector3.Lerp(p, land, 0.6f) : p);   // (once it's past, the eye goes ahead of it: to them)
            yield return null;
        }
        if (body != null) body.rotation = bodyRot;

        // down on top of them, nearly flat, and it stays there
        kvSign.position = land;
        kvSign.rotation = Quaternion.AngleAxis(72f, Vector3.Cross(Vector3.up, dir)) * kvSign.rotation;
        CrushOnlookers(land);
        Fx(sparks, land, 3f, 4f);
        Bang(1f, 0.6f);
        shake = 1f;
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)   // (the same shot as before: where they stood)
        {
            PlaceShaking(gawk, land + Vector3.up * 0.8f);
            yield return null;
        }
        if (onlookerLight != null) Destroy(onlookerLight.gameObject);
        shake = 0f;
    }

    IEnumerator Collapse(Vector3 door, Vector3 van)
    {
        ShowOffice(true);
        Bounds ob = officeRs != null && officeRs.Length > 0 ? officeRs[0].bounds : new Bounds(door, Vector3.one * 30f);
        if (officeRs != null) foreach (var r in officeRs) ob.Encapsulate(r.bounds);
        Vector3 toVan = Flat(van - door).normalized, side = Vector3.Cross(Vector3.up, toVan);
        float groundY = Ground(door);
        // the street end of the building, where the blasts go off
        Vector3 Front(float across, float up) => new Vector3(ob.center.x, ob.min.y + ob.size.y * up, ob.center.z) + toVan * ob.extents.magnitude * 0.62f + side * across * ob.extents.x;
        Vector3 wideFrom = SafeCam(door + Vector3.up * 3f, door + toVan * 46f + side * 9f + Vector3.up * 3f);
        Vector3 wideTo = SafeCam(door + Vector3.up * 3f, door + toVan * 38f + side * 7f + Vector3.up * 2f);
        Vector3 lookHigh = ob.center + Vector3.up * ob.extents.y * 0.15f, lookLow = new Vector3(door.x, groundY + 6f, door.z);
        var rumble = MakeSource(ProceduralAudio.Rumble(), true, 0.4f);
        rumble.Play();
        var fire = new GameObject("Office blast light").AddComponent<Light>();
        fire.color = new Color(1f, 0.45f, 0.15f); fire.range = 90f; fire.intensity = 0f;
        fire.transform.position = door + toVan * 6f + Vector3.up * 8f;
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.4f));
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(burstLines);

        // ---- 1. it shakes: windows blow out one after another along the front, sparks, dust off the walls
        float next = 0.6f;
        for (float t = 0f; t < 9f && (t < 3.5f || (DialogueBox.Instance != null && DialogueBox.Instance.Busy)); t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / 3.5f);
            shake = Mathf.Max(shake, 0.3f + 0.5f * k);
            rumble.volume = Mathf.Lerp(0.6f, 1f, k);
            if (t >= next)   // no blasts: it heaves, again and again, and nothing can be seen doing it
            {
                next = t + Random.Range(0.7f, 1.3f) * (1.2f - k * 0.5f);
                Bang(0.55f, 0.6f);
                shake = Mathf.Min(shake + 1.1f, 2.2f);
            }
            PlaceShaking(Vector3.Lerp(wideFrom, wideTo, k * 0.4f), lookHigh);
            yield return null;
        }

        // ---- 2. the big one: the ground floor goes, the whole office comes down
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(transformLine);
        ShowOffice(false);
        rubble = Instantiate(brokenOffice, brokenOffice.transform.position, brokenOffice.transform.rotation, brokenOffice.transform.parent);
        rubble.transform.localScale = brokenOffice.transform.localScale;
        rubble.name = "Office (coming down)";
        rubble.SetActive(true);
        Vector3 blast = door + toVan * 2f + Vector3.up * 3f;
        var chunks = new List<Rigidbody>();
        foreach (var mf in rubble.GetComponentsInChildren<MeshFilter>())
        {
            var b = mf.GetComponent<Renderer>().bounds;
            float h01 = Mathf.InverseLerp(ob.min.y, ob.max.y, b.center.y);
            // the ground floor is gone in the blast: nothing holds the rest up
            if (h01 < 0.12f) { Destroy(mf.gameObject); continue; }
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.convex = true;
            var rb = mf.gameObject.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(50f, b.size.x * b.size.y * b.size.z * 40f);
            rb.maxDepenetrationVelocity = 1.5f;   // the cells start touching: no popping apart
            Vector3 inward = Flat(ob.center - b.center).normalized;
            // the floors over the blast kicked out a little at the street; everything else falls in on itself
            float near = 1f - Mathf.Clamp01(Vector3.Distance(b.center, blast) / 18f);
            rb.linearVelocity = toVan * collapsePush * near + Vector3.up * collapsePush * near * 0.4f + inward * (0.5f + h01 * 2f) + Random.insideUnitSphere;
            rb.angularVelocity = Random.insideUnitSphere * (0.3f + near * 1.5f);
            chunks.Add(rb);
        }
        StartCoroutine(SettleRubble(chunks, 10f));
        Fx(bigBlast, blast, 5f, 8f);
        Fx(fireBall, blast + Vector3.up * 4f, 4f, 6f);
        Fx(dustBlast, new Vector3(door.x, groundY, door.z), 6f, 10f);
        for (int i = 0; i < 4; i++) Fx(dustBlast, new Vector3(ob.center.x, groundY, ob.center.z) + Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.forward * ob.extents.x, 6f, 10f);
        for (int i = 0; i < 3; i++) Fx(sparks, Front(Random.Range(-0.8f, 0.8f), Random.Range(0.1f, 0.4f)), 3f, 5f);
        Bang(1f, 0.6f); Bang(0.8f, 0.45f);
        var img = fader != null ? fader.GetComponent<UnityEngine.UI.Image>() : null;
        Color black = img != null ? img.color : Color.black;
        if (img != null) { img.color = new Color(1f, 0.8f, 0.6f); fader.fade.alpha = 0.85f; StartCoroutine(fader.FadeTo(0f, 0.7f)); }
        shake = 1.6f;
        fire.intensity = 60f;
        for (float t = 0f; t < 4.2f; t += Time.deltaTime)
        {
            float k = t / 4.2f;
            fire.intensity = Mathf.Lerp(60f, 14f, Mathf.Sqrt(k)) * Random.Range(0.85f, 1.15f);
            PlaceShaking(Vector3.Lerp(wideTo, wideFrom, Mathf.SmoothStep(0f, 1f, k) * 0.5f), Vector3.Lerp(lookHigh, lookLow, Mathf.SmoothStep(0f, 1f, k)));
            yield return null;
        }
        if (img != null) img.color = black;
        // what is left burns: smoke columns and flames in the rubble (they stay; a retry clears them with it)
        Fx(smoke, new Vector3(ob.center.x, groundY, ob.center.z), 6f, 0f);
        Fx(smoke, new Vector3(door.x, groundY, door.z) - toVan * 4f, 4f, 0f);
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = ob.center + side * Random.Range(-1f, 1f) * ob.extents.x + toVan * Random.Range(-0.6f, 1.1f) * ob.extents.z;
            Fx(flames, new Vector3(p.x, Ground(p + Vector3.up * 20f), p.z), Random.Range(2f, 3.5f), 0f);
        }

        // ---- 3. out of the smoke: the final form walks out of the blast towards the van
        Vector3 from = door - toVan * 2f, to = door + toVan * emergeWalk;
        from.y = Ground(from); to.y = Ground(to);
        transform.SetPositionAndRotation(from, Quaternion.LookRotation(toVan));
        if (model != null) model.localScale = modelScale0;
        SetModelVisible(true);
        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;   // (the rubble is still settling round him)
        Fx(smoke, from, 3f, 9f);
        float tall = Centre().y - from.y;
        // out on the street, up a little: the rubble that rolled out mustn't stand between us and him
        Vector3 seeHim = to + Vector3.up * tall * 0.6f;
        Vector3 lowCam = ClearCam(seeHim,
            to + toVan * 22f + side * 6f + Vector3.up * 3f, to + toVan * 22f - side * 6f + Vector3.up * 3f,
            to + toVan * 26f + side * 3f + Vector3.up * 5f, to + toVan * 30f + Vector3.up * 7f);
        float walk = Mathf.Max(2.5f, emergeWalk / Mathf.Max(1.5f, walkSpeed));
        for (float t = 0f; t < walk; t += Time.deltaTime)
        {
            Vector3 p = Vector3.Lerp(from, to, t / walk);
            p.y = Ground(p);
            transform.position = p;
            Locomote(emergeWalk / walk);
            fire.intensity = Mathf.MoveTowards(fire.intensity, 8f, 10f * Time.deltaTime) * Random.Range(0.9f, 1.1f);
            PlaceShaking(lowCam, Centre() + Vector3.up * tall * 0.2f);
            yield return null;
        }
        // he stops and roars at the camera
        Locomote(0f);
        Play(roarState);
        Roar(1.2f);
        shake = 0.9f;
        Vector3 roarCam = ClearCam(Centre(),
            to + toVan * Mathf.Max(6f, tall * 1.5f) - side * 2f + Vector3.up * tall * 0.3f,
            to + toVan * Mathf.Max(6f, tall * 1.5f) + side * 2f + Vector3.up * tall * 0.3f,
            to + toVan * 10f + Vector3.up * tall * 0.5f, lowCam);
        for (float t = 0f; t < 2.6f; t += Time.deltaTime)
        {
            shake = Mathf.Max(shake, 0.15f);
            PlaceShaking(Vector3.Lerp(lowCam, roarCam, Mathf.SmoothStep(0f, 1f, t / 1.2f)), Centre());
            yield return null;
        }
        if (kvSign != null) yield return ThrowSign(to, van);
        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = true;
        shake = 0f;
        StartCoroutine(FadeSource(rumble, 1.5f));
        fire.transform.SetParent(rubble.transform, true);   // the fire's glow stays on the ruin
    }

    // once it's down the rubble stops moving for good (and costs nothing)
    IEnumerator SettleRubble(List<Rigidbody> chunks, float after)
    {
        yield return new WaitForSeconds(after);
        foreach (var rb in chunks) if (rb != null) rb.isKinematic = true;
    }

    // the damned body's walk: its controller blends idle / walk / run on "Speed"
    static void SetWalking(Animator a, bool on)
    {
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Float && p.name == "Speed") { a.SetFloat("Speed", on ? 0.35f : 0f); return; }
    }

    static Bounds BoundsOf(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one);
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // ---------------------------------------------------------------- the tunnel: too tall to follow you in
    // It drops out of sight as you go in; something heavy runs across the tunnel roof over your head; and as you
    // come out the far end it lands on the road in front of you.
    int tunnelState;       // 0 before the tunnel, 1 you're in it (it's gone), 2 it's jumped out in front
    float nextThump;

    // true while the tunnel has it: ChaseUpdate does nothing else
    bool TunnelUpdate(Vector3 vanPos)
    {
        if (chaseRoad == null || tunnelFrom < 0 || tunnelTo <= tunnelFrom || tunnelTo >= chaseRoad.Length || tunnelState == 2) return false;
        Vector3 inPt = chaseRoad[tunnelFrom].position, outPt = chaseRoad[tunnelTo].position;
        Vector3 inDir = Flat(chaseRoad[Mathf.Min(tunnelFrom + 1, chaseRoad.Length - 1)].position - inPt).normalized;
        Vector3 outDir = Flat(chaseRoad[Mathf.Min(tunnelTo + 1, chaseRoad.Length - 1)].position - outPt).normalized;
        if (tunnelState == 0)
        {
            if (Vector3.Dot(Flat(vanPos - inPt), inDir) < 2f || Vector3.Dot(Flat(vanPos - outPt), outDir) > 0f) return false;
            // into the tunnel: it's gone
            tunnelState = 1;
            SetModelVisible(false);
            nextThump = Time.time + 1.5f;
            if (DialogueBox.Instance != null) DialogueBox.Instance.Say(tunnelInLine);
            return true;
        }
        // in the tunnel: footsteps on the roof, the tunnel shaking
        if (tunnelState == 1 && Time.time >= nextThump)
        {
            nextThump = Time.time + Random.Range(1.2f, 2.2f);
            var thud = Sounds.Clip("Silo Fight/Push and fall thud", null) ?? Sounds.Clip("Prologue/Impact (hitting the street)", null);
            if (thud != null) AudioSource.PlayClipAtPoint(thud, vanPos + Vector3.up * 5f, 1f);
            var cam = carInteract != null && carInteract.carCamera != null ? carInteract.carCamera.transform : null;
            if (cam != null) StartCoroutine(CamShake(cam, 0.35f));
        }
        if (tunnelState == 1)
        {
            if (Vector3.Dot(Flat(vanPos - outPt), outDir) < 0f) return true;
            // out of the tunnel: a few quiet seconds... then it drops down right behind you
            tunnelState = 3;
            landAt = Time.time + tunnelJumpDelay;
            return true;
        }
        if (tunnelState == 3 && Time.time >= landAt) { tunnelState = 4; StartCoroutine(JumpBehind()); }
        return tunnelState != 2;
    }

    float landAt;

    float Clearance(Vector3 from, Vector3 dir, float max)
    {
        foreach (var h in Physics.SphereCastAll(from, 0.4f, dir, max, ~0, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance))
            if (!CamIgnores(h.collider) && h.distance > 0f) return h.distance;
        return max;
    }

    // a camera spot out from the anchor that isn't inside a wall: pulled back towards the anchor if something's in the way
    Vector3 SafeCam(Vector3 anchor, Vector3 want)
    {
        Vector3 d = want - anchor;
        float nearest = d.magnitude;
        foreach (var h in Physics.SphereCastAll(anchor, 0.4f, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!CamIgnores(h.collider) && h.distance > 0f && h.distance < nearest) nearest = h.distance;
        return anchor + d.normalized * Mathf.Max(0.5f, nearest - 0.3f);
    }

    // the first of these camera spots with room round it and nothing between it and `look` (the falling office
    // counts); none clear: the last one, pulled in towards `look`
    Vector3 ClearCam(Vector3 look, params Vector3[] spots)
    {
        foreach (var s in spots)
        {
            Vector3 d = look - s;
            if (Physics.OverlapSphere(s, 0.6f, ~0, QueryTriggerInteraction.Ignore).Any(c => !CamIgnores(c))) continue;
            if (Physics.SphereCastAll(s, 0.5f, d.normalized, Mathf.Max(0f, d.magnitude - 1.5f), ~0, QueryTriggerInteraction.Ignore).Any(h => !CamIgnores(h.collider))) continue;
            return s;
        }
        return SafeCam(look, spots[spots.Length - 1]);
    }

    bool CamIgnores(Collider c) => c.isTrigger || c.transform.IsChildOf(transform) || (carRb != null && c.transform.IsChildOf(carRb.transform))
                                   || c.GetComponentInParent<Zombie>() != null
                                   || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic && !(rubble != null && c.transform.IsChildOf(rubble.transform)));   // (falling office: keep the camera out of it)

    // the cutscene: slowed down, the camera looking back past the van as it slams onto the road behind it
    IEnumerator JumpBehind()
    {
        Vector3 van = carRb.position;
        Vector3 fwd = Flat(carRb.transform.forward).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, fwd);
        // the side of the van with more room for the camera
        Vector3 v0 = van + Vector3.up * 1.5f;
        if (Clearance(v0, side, 6f) < Clearance(v0, -side, 6f)) side = -side;
        Vector3 land = van - fwd * tunnelJumpBehind;
        land.y = Ground(land);
        Vector3 from = land + Vector3.up * 16f - fwd * 6f;

        float ts0 = Time.timeScale;
        Time.timeScale = 0.35f;
        SetBars(1f);
        if (cutsceneCam != null) cutsceneCam.enabled = true;
        transform.SetPositionAndRotation(from, Quaternion.LookRotation(fwd));
        SetModelVisible(true);
        Play(roarState);
        Roar(1.2f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(tunnelOutLine);

        // it falls out of the sky behind the van
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            float k = t / 0.7f;
            transform.position = Vector3.Lerp(from, land, k * k);
            Vector3 v = carRb.position;
            Vector3 camPos = SafeCam(v + Vector3.up * 1.5f, v + Flat(carRb.transform.forward).normalized * 5f + side * 3f + Vector3.up * 2.2f);
            Place(camPos, Vector3.Lerp(v + Vector3.up * 1.2f, transform.position + Vector3.up * 3f, 0.6f));
            yield return null;
        }
        transform.position = land;
        Boom(land, 1.8f);
        Play(slamState);

        // the landing: a beat on it, crouched on the road right behind you
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            Vector3 v = carRb.position;
            Vector3 camPos = SafeCam(v + Vector3.up * 1.5f, v + Flat(carRb.transform.forward).normalized * 5f + side * 3f + Vector3.up * 2.2f);
            FaceToward(v, 6f);
            Place(camPos + Random.insideUnitSphere * 0.12f * (1f - t / 0.9f), Vector3.Lerp(v + Vector3.up * 1.2f, Centre(), 0.6f));
            yield return null;
        }

        Time.timeScale = ts0 > 0f ? ts0 : 1f;
        if (cutsceneCam != null) cutsceneCam.enabled = false;
        SetBars(0f);
        chaseSeg = NearestRoad(land);
        catchReady = Time.time + 1f;
        tunnelState = 2;
    }

    // ---- the escape road as a line with a length: where along it the van is, and where it is
    float[] routeS;                 // metres along the road at each point
    float bossS, vanS;
    float watchAt, watchS;

    void BuildRoute()
    {
        if (routeS != null && chaseRoad != null && routeS.Length == chaseRoad.Length) return;
        if (chaseRoad == null || chaseRoad.Length < 2) { routeS = null; return; }
        routeS = new float[chaseRoad.Length];
        for (int i = 1; i < chaseRoad.Length; i++) routeS[i] = routeS[i - 1] + Flat(chaseRoad[i].position - chaseRoad[i - 1].position).magnitude;
        bossS = ProjectOnRoute(transform.position, 0f, float.MaxValue);
        vanS = carRb != null ? ProjectOnRoute(carRb.position, 0f, float.MaxValue) : 0f;
        watchAt = Time.time + 1.5f; watchS = bossS;
    }

    Vector3 RoutePoint(float s, out Vector3 dir)
    {
        int last = chaseRoad.Length - 1;
        s = Mathf.Clamp(s, 0f, routeS[last]);
        int i = 0; while (i < last - 1 && routeS[i + 1] < s) i++;
        Vector3 a = chaseRoad[i].position, b = chaseRoad[i + 1].position;
        dir = Flat(b - a).normalized;
        return Vector3.Lerp(a, b, Mathf.InverseLerp(routeS[i], routeS[i + 1], s));
    }

    // the metres along the road of the nearest point on it, looking only within `window` of where it was last
    float ProjectOnRoute(Vector3 p, float near, float window)
    {
        float best = float.MaxValue, at = near;
        for (int i = 0; i < chaseRoad.Length - 1; i++)
        {
            Vector3 a = Flat(chaseRoad[i].position), ab = Flat(chaseRoad[i + 1].position) - a;
            float len = ab.magnitude; if (len < 0.01f) continue;
            float t = Mathf.Clamp01(Vector3.Dot(Flat(p) - a, ab) / (len * len));
            float s = routeS[i] + len * t;
            if (Mathf.Abs(s - near) > window) continue;
            float d = (a + ab * t - Flat(p)).sqrMagnitude;
            if (d < best) { best = d; at = s; }
        }
        return at;
    }

    // the ground under it on the road: what is within a step of the road's own height (not the roof of a tunnel or
    // a building it passes), so it can't climb onto things
    float ChaseGround(Vector3 p)
    {
        if (routeS == null) return Ground(p);
        float refY = RoutePoint(bossS, out _).y;
        float best = float.MinValue;
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, refY + 3f, p.z), Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || h.collider.GetComponentInParent<Zombie>() != null) continue;
            if (player != null && h.collider.transform.IsChildOf(player)) continue;
            if (carRb != null && h.collider.transform.IsChildOf(carRb.transform)) continue;
            if (h.point.y > refY + 1.5f) continue;
            best = Mathf.Max(best, h.point.y);
        }
        return best > float.MinValue ? best : refY;
    }

    // held up for any reason (a mesh, a prop): after a second and a half without getting anywhere it is put on the
    // road a few metres further on
    void ChaseWatchdog(float speed, float dist)
    {
        if (routeS == null || Time.time < watchAt) return;
        watchAt = Time.time + 1.5f;
        bool behind = bossS < vanS - 12f && dist > catchDistance + 4f && Time.time >= staggerUntil;
        if (behind && speed > 2f && bossS - watchS < 1f)
        {
            bossS = Mathf.Min(bossS + 12f, vanS);
            Vector3 at = RoutePoint(bossS, out Vector3 dir);
            at.y = ChaseGround(at);
            transform.SetPositionAndRotation(at, Quaternion.LookRotation(dir));
        }
        watchS = bossS;
    }

    int NearestRoad(Vector3 p)
    {
        int best = 0; float bd = float.MaxValue;
        if (chaseRoad == null) return 0;
        for (int i = 0; i < chaseRoad.Length; i++)
        {
            float d = Flat(chaseRoad[i].position - p).sqrMagnitude;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    void ChaseUpdate()
    {
        bool inVan = carInteract != null && carInteract.InCar;
        Transform target = inVan || player == null || !player.gameObject.activeInHierarchy ? carRb.transform : player;
        Vector3 vanFlat = Flat(carRb.position);
        if (Time.deltaTime > 0f) vanSpeed = Mathf.Lerp(vanSpeed, Vector3.Distance(vanFlat, lastVan) / Time.deltaTime, 5f * Time.deltaTime);
        lastVan = vanFlat;
        if (inVan && TunnelUpdate(carRb.position)) return;

        Vector3 to = Flat(target.position - transform.position);
        float dist = to.magnitude;

        // staggered by your shooting: rooted to the spot for a moment
        if (Time.time < staggerUntil) { FaceToward(target.position, 2f); return; }

        // blood lobbed at the road ahead of the van
        if (chaseBolts && Time.time >= chaseBoltAt && dist > 12f && dist < 70f)
        {
            chaseBoltAt = Time.time + Random.Range(chaseBoltEvery.x, chaseBoltEvery.y);
            StartCoroutine(ChaseBolts(target, dist > 40f ? 4 : 3));
        }

        // along the road to where the van is, then straight at it. The road is walked by distance along it (not
        // point to point), so nothing between the points - a building, a rail, a tunnel mouth - can hold it up
        Vector3 goal = target.position;
        BuildRoute();
        if (routeS != null && !crawling && ProjectOnRoute(carRb.position, 0f, float.MaxValue) >= crawlFrom) crawling = true;
        if (routeS != null && dist > 30f)
        {
            vanS = ProjectOnRoute(target.position, vanS, 150f);
            if (bossS < vanS - 12f) goal = RoutePoint(Mathf.Min(bossS + 14f, vanS), out _);
        }
        float speed = Mathf.Lerp(chaseNear, chaseFar, dist / chaseFarDistance);
        if (dist < chaseClose && target != player)
            speed = vanSpeed < 3f ? chaseNear : Mathf.Min(speed, vanSpeed + chaseGain);   // moving: it only creeps up on you
        if (dist > chaseHold && target != player) speed = Mathf.Max(speed, vanSpeed + chaseGain);
        if (dist < catchDistance * 0.8f) speed = 0f;
        Vector3 step = Flat(goal - transform.position);
        Vector3 next = transform.position + step.normalized * Mathf.Min(step.magnitude, speed * Time.deltaTime);
        next.y = Mathf.Lerp(transform.position.y, ChaseGround(next), 10f * Time.deltaTime);
        transform.position = next;
        if (step.sqrMagnitude > 0.01f) FaceToward(transform.position + step, 5f);
        busy = false;
        Locomote(speed);
        if (routeS != null) bossS = ProjectOnRoute(transform.position, bossS, 60f);
        ChaseWatchdog(speed, dist);

        // the damned in its way are trampled
        if (Time.time >= trampleAt)
        {
            trampleAt = Time.time + 0.2f;
            foreach (var c in Physics.OverlapSphere(transform.position + Vector3.up, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                var z = c.GetComponentInParent<Zombie>();
                if (z != null && !z.IsDead) z.KillInstant(c.transform.position, (Flat(c.transform.position - transform.position).normalized + Vector3.up * 0.4f) * 600f);
            }
        }

        // caught up: it brings the pitchfork down on the van (or you)
        if (dist < catchDistance && Time.time >= catchReady)
        {
            catchReady = Time.time + catchCooldown;
            StartCoroutine(ChaseBlow(target));
        }
    }

    // three or four bolts where the van is about to be, spread across the road: swerve out of the circles
    IEnumerator ChaseBolts(Transform target, int count)
    {
        Play(throwState);
        yield return new WaitForSeconds(0.45f);
        Vector3 hand = Centre() + Vector3.up * 1.5f + transform.forward * 1.5f;
        Vector3 vel = carRb != null && target != player ? Flat(carRb.linearVelocity) : Vector3.zero;
        Vector3 ahead = target.position + vel * boltFlight * 1.1f;
        Vector3 across = Vector3.Cross(Vector3.up, vel.sqrMagnitude > 1f ? vel.normalized : Flat(target.forward).normalized);
        for (int i = 0; i < count; i++)
        {
            Vector3 aim = ahead + across * (i - (count - 1) * 0.5f) * 3.4f + vel.normalized * Random.Range(-2f, 2f);
            aim.y = Ground(aim);
            StartCoroutine(Bolt(hand, aim));
            yield return new WaitForSeconds(0.1f);
        }
    }

    IEnumerator ChaseBlow(Transform target)
    {
        if (!crawling) Play(slamState);
        yield return new WaitForSeconds(0.55f);
        if (Flat(target.position - transform.position).magnitude > catchDistance + 1.5f) yield break;   // you got away in time
        Boom(target.position, 1f);
        if (target == player) { Hurt(catchDamage * 1.4f); Knock(Flat(player.position - transform.position).normalized * 8f + Vector3.up * 3f); }
        else
        {
            var ch = carRb.GetComponent<CarHealth>();
            if (ch != null) ch.TakeDamage(catchDamage);
            carRb.AddForce((Flat(carRb.position - transform.position).normalized * 4f + Vector3.up * 1.5f) * carRb.mass, ForceMode.Impulse);
        }
    }

    void VanFragility(float scale)
    {
        var hp = carRb != null ? carRb.GetComponent<CarHealth>() : null;
        if (hp != null) hp.damageScale = scale;
    }

    // the trigger at the far end of the bridge: the van dies at the dead end and it catches up
    public void ArriveAtBridgeEnd()
    {
        if (!chaseBegun) { Begin(); return; }   // no chase happened: the old rise from the river
        if (begun) return;
        begun = true;
        chasing = false;
        crawling = false;
        if (animator != null) animator.speed = 1f;
        VanFragility(1f);
        StartCoroutine(Arrive());
    }

    IEnumerator Arrive()
    {
        if (carInteract != null && carInteract.InCar)
        {
            if (carInteract.carController is PrometeoCarController pc) { pc.ThrottleOff(); pc.Brakes(); }
            carInteract.carController.enabled = false;
            Vector3 v0 = carRb.linearVelocity;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime) { carRb.linearVelocity = Vector3.Lerp(v0, Vector3.zero, t / 1.2f); yield return null; }
            carRb.linearVelocity = Vector3.zero;
        }
        MusicManager.Stop(1.5f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(arriveLines);
        yield return new WaitForSeconds(1f);

        // which way is "into the arena" (from where it would stand at the city end towards the middle)
        Vector3 into = fightStart != null && playerStart != null ? Flat(playerStart.position - fightStart.position).normalized
                     : arena != null ? Flat(arena.forward).normalized : Flat(Tgt.forward).normalized;
        bool onWheels = fightInVan && carInteract != null && carInteract.InCar;
        // inside the fight's walls already? (the van stops where the trigger caught it)
        bool inside = true;
        if (arena != null)
        {
            Vector3 l = arena.InverseTransformPoint(Tgt.position);
            inside = Mathf.Abs(l.x) < arenaHalfWidth - 7f && Mathf.Abs(l.z) < arenaHalfLength - 7f;
        }
        if (!onWheels || !inside)
        {
            if (fader != null) yield return fader.FadeTo(1f, 0.35f);
            if (onWheels && carRb != null)
            {
                // the van, well inside the arena, pointing down it
                Vector3 at = playerStart != null ? playerStart.position : arena.position;
                at.y = Ground(at) + 0.4f;
                carRb.linearVelocity = Vector3.zero; carRb.angularVelocity = Vector3.zero;
                carRb.transform.SetPositionAndRotation(at, Quaternion.LookRotation(into));
                Physics.SyncTransforms();
            }
            else
            {
                if (carInteract != null) carInteract.ForceExit(true);   // out, and the van's done for
                if (player != null && arena != null)
                {
                    Vector3 you = playerStart != null ? playerStart.position : arena.position;
                    you.y = Ground(you) + 0.1f;
                    var body = player.GetComponent<Rigidbody>();
                    if (body != null) { body.isKinematic = false; body.linearVelocity = Vector3.zero; }
                    player.SetPositionAndRotation(you, Quaternion.LookRotation(into));
                    Physics.SyncTransforms();
                }
            }
        }

        yield return LeapInFront(into);
        homePos = transform.position; homeRot = transform.rotation;
        if (onWheels) carInteract.carController.enabled = true;   // engine running: the fight is on wheels
        if (DialogueBox.Instance != null) DialogueBox.Instance.Say(riseLines);
        StartFight();
    }

    // The cutscene that opens the fight: it comes over the top of you in one leap, lands in your way with the deck
    // shaking, turns, and roars that there's nowhere left to run.
    IEnumerator LeapInFront(Vector3 into)
    {
        Transform you = Tgt;
        bool wheels = InVan && carRb != null;
        Vector3 pos = you.position;
        Vector3 fwd = Flat(you.forward).normalized;
        if (Vector3.Dot(fwd, into) < 0.2f) fwd = into;   // facing back the way you came: it still lands deeper in
        Vector3 side = Vector3.Cross(Vector3.up, fwd);
        Vector3 eye = pos + Vector3.up * 1.5f;
        if (Clearance(eye, side, 6f) < Clearance(eye, -side, 6f)) side = -side;

        Vector3 land = pos + fwd * leapAhead;
        if (arena != null)
        {
            Vector3 l = arena.InverseTransformPoint(land);
            l.x = Mathf.Clamp(l.x, -(arenaHalfWidth - 9f), arenaHalfWidth - 9f);
            l.z = Mathf.Clamp(l.z, -(arenaHalfLength - 9f), arenaHalfLength - 9f);
            land = arena.TransformPoint(l);
        }
        land.y = Ground(land);
        Vector3 from = pos - fwd * 24f;
        from.y = pos.y;

        // hold everything still while it plays
        if (wheels) { carRb.linearVelocity = Vector3.zero; carRb.angularVelocity = Vector3.zero; carRb.isKinematic = true; }
        var locked = new List<Behaviour>();
        if (lockDuring != null)
            foreach (var b in lockDuring) if (b != null && b.enabled) { b.enabled = false; locked.Add(b); }
        SetBars(1f);
        if (cutsceneCam != null) cutsceneCam.enabled = true;
        gameObject.SetActive(true);
        transform.SetPositionAndRotation(from, Quaternion.LookRotation(fwd));
        SetModelVisible(true);
        Play(roarState);
        Roar(1.1f);
        if (fader != null && fader.fade.alpha > 0.01f) StartCoroutine(fader.FadeTo(0f, 0.35f));

        // shot 1: low beside you, looking back and up: it comes over the top
        Vector3 cam1 = SafeCam(eye, pos + side * 4.5f + fwd * 3f + Vector3.up * 1.1f);
        const float flight = 1.5f;
        bool swung = false;
        for (float t = 0f; t < flight; t += Time.deltaTime)
        {
            float k = t / flight;
            Vector3 p = Vector3.Lerp(from, land, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * leapHeight;
            transform.SetPositionAndRotation(p, Quaternion.LookRotation(fwd));
            if (!swung && k > 0.7f) { swung = true; Play(slamState); }
            Place(cam1, Vector3.Lerp(pos + Vector3.up * 1.2f, p + Vector3.up * 3f, 0.75f));
            yield return null;
        }
        transform.position = land;
        Boom(land, 2.2f);

        // shot 2: from just ahead of you, low, looking up at it as it turns round
        Vector3 cam2 = SafeCam(eye, pos + fwd * 5.5f + side * 2.4f + Vector3.up * 0.9f);
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            FaceToward(pos, 8f);
            Place(cam2 + Random.insideUnitSphere * 0.14f * (1f - t / 0.8f), Centre());
            yield return null;
        }
        Play(roarState);
        Roar(1.3f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(nowhereLine);
        Vector3 push = cam2 + (Centre() - cam2).normalized * 3f;
        for (float held = 0f; held < 2.8f || (held < 7f && DialogueBox.Instance != null && DialogueBox.Instance.Busy); held += Time.deltaTime)
        {
            FaceToward(pos, 6f);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, held / 3.5f));
            Place(Vector3.Lerp(cam2, push, k) + Random.insideUnitSphere * 0.04f * (1f - k), Centre() + Vector3.up * 0.8f);
            yield return null;
        }

        if (cutsceneCam != null) cutsceneCam.enabled = false;
        SetBars(0f);
        foreach (var b in locked) if (b != null) b.enabled = true;
        if (wheels) carRb.isKinematic = false;
    }

    void PlaceVan()
    {
        if (carRb == null) return;
        Vector3 at = playerStart != null ? playerStart.position : arena.position;
        at.y = Ground(at) + 0.4f;
        Vector3 face = Flat(transform.position - at);
        carRb.linearVelocity = Vector3.zero; carRb.angularVelocity = Vector3.zero;
        carRb.position = at;
        carRb.rotation = Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face.normalized : arena.forward);
        carRb.transform.SetPositionAndRotation(carRb.position, carRb.rotation);
        Physics.SyncTransforms();
    }

    // Escape checkpoint: back at the office, before it ever came out
    public void ResetChase()
    {
        if (begun) return;
        StopAllCoroutines();
        SleepSequence.BloodTideOff();
        CutsceneSkip.Extra = 0;
        VanFragility(1f);
        chasing = false;
        chaseBegun = false;
        crawling = false;
        routeS = null;
        tunnelState = 0;
        if (brokenOffice != null) ShowOffice(true);   // the office stands again, the rubble goes
        PutSignBack();
        shake = 0f;
        if (Time.timeScale < 1f && Time.timeScale > 0f) Time.timeScale = 1f;
        if (model != null) model.localScale = modelScale0;
        health = maxHealth;
        staggerUntil = 0f; staggerDamage = 0f;
        if (healthBar != null) healthBar.SetActive(false);
        foreach (var m in marks) if (m != null) Destroy(m);
        marks.Clear();
        SetModelVisible(false);
        if (cutsceneCam != null) cutsceneCam.enabled = false;
        SetBars(0f);
        if (carRb != null) carRb.isKinematic = false;
        if (fader != null) fader.fade.alpha = 0f;
        Active = null;
    }

    // ---------------------------------------------------------------- the rise

    public void Begin()
    {
        if (begun) return;
        begun = true;
        Active = this;
        StartCoroutine(Rise());
    }

    IEnumerator Rise()
    {
        // the car gives out on the bridge
        if (carInteract != null && carInteract.InCar)
        {
            if (carInteract.carController is PrometeoCarController pc) { pc.ThrottleOff(); pc.Brakes(); }
            carInteract.carController.enabled = false;
            if (carRb != null)
            {
                Vector3 v0 = carRb.linearVelocity;
                for (float s = 0f; s < 1.4f; s += Time.deltaTime) { carRb.linearVelocity = Vector3.Lerp(v0, Vector3.zero, s / 1.4f); yield return null; }
                carRb.linearVelocity = Vector3.zero;
            }
        }
        MusicManager.Stop(1.5f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow("Come on. Come on.|Something's under the bridge.");
        yield return new WaitForSeconds(1.8f);

        if (fader != null) yield return fader.FadeTo(1f, 0.35f);
        SetBars(1f);
        Vector3 carPos = carRb != null ? carRb.position : player.position;
        Vector3 up0 = riseFrom.position, up1 = standAt.position;
        transform.SetPositionAndRotation(up0, Quaternion.LookRotation(Flat(carPos - up1)));
        gameObject.SetActive(true);
        SetModelVisible(true);
        if (cutsceneCam != null) { cutsceneCam.enabled = true; }
        Vector3 fwd = arena != null ? arena.forward : Flat(up1 - carPos).normalized;
        Vector3 camPos = carPos - fwd * 4f + Vector3.up * 1.6f + Vector3.Cross(Vector3.up, fwd) * 2f;
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.4f));

        // it climbs up the side of the bridge, the deck shaking, the river boiling red
        var rumble = MakeSource(ProceduralAudio.Rumble(), true, 1f);
        rumble.Play();
        for (float t = 0f; t < 4.2f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 4.2f);
            Vector3 p = Vector3.Lerp(up0, up1 + Vector3.up * 3f, k);
            if (t > 3.6f) p = Vector3.Lerp(up1 + Vector3.up * 3f, up1, (t - 3.6f) / 0.6f);
            transform.position = p;
            Place(camPos + Random.insideUnitSphere * 0.05f * (1f - k * 0.5f), Centre() + Vector3.up * (1f - k) * -4f);
            yield return null;
        }
        transform.position = up1;
        Play(roarState);
        Roar();
        Shake(camPos, 0.9f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(riseLines);

        // a slow push in on it while it talks
        for (float t = 0f; t < 9f; t += Time.deltaTime)
        {
            Vector3 near = Vector3.Lerp(camPos, up1, 0.6f) + Vector3.up * 0.4f;   // push in until it fills the frame
            Vector3 c = Vector3.Lerp(camPos, near, Mathf.SmoothStep(0f, 1f, t / 9f));
            Place(c, Centre() + Vector3.up * 1.5f);
            FaceToward(carPos, 2f);
            yield return null;
        }
        StartCoroutine(FadeSource(rumble, 2f));

        // it brings its fists down on the car
        Play(slamState);
        yield return new WaitForSeconds(0.5f);
        if (fader != null) yield return fader.FadeTo(1f, 0.25f);
        if (carInteract != null && carInteract.InCar) carInteract.ForceExit(true);   // wrecked: no getting back in
        if (carRb != null) { carRb.AddForce((Vector3.up * 3f - fwd * 6f) * carRb.mass, ForceMode.Impulse); carRb.AddTorque(Vector3.forward * carRb.mass * 4f, ForceMode.Impulse); }
        // you're thrown clear, back down the bridge
        if (player != null)
        {
            var body = player.GetComponent<Rigidbody>();
            if (body != null) { body.isKinematic = false; body.linearVelocity = Vector3.zero; }
            Vector3 land = carPos - fwd * 7f; land.y = Ground(land) + 0.1f;
            player.SetPositionAndRotation(land, Quaternion.LookRotation(fwd));
            Physics.SyncTransforms();
        }
        if (cutsceneCam != null) cutsceneCam.enabled = false;
        SetBars(0f);
        if (playerHealth != null) playerHealth.TakeDamage(10f);
        yield return new WaitForSeconds(0.4f);
        if (fader != null) yield return fader.FadeTo(0f, 0.6f);
        StartFight();
    }

    bool arenaLit;

    // the bridge end opens up: no fog, more light, so you can see what's coming at you
    IEnumerator LightArena(float time)
    {
        arenaLit = true;
        SleepSequence.BloodTide(time);
        var sun = RenderSettings.sun;
        float sun0 = sun != null ? sun.intensity : 0f, amb0 = RenderSettings.ambientIntensity, fog0 = RenderSettings.fogDensity;
        UnityEngine.Rendering.Universal.ColorAdjustments ca = null;
        UnityEngine.Rendering.Universal.Bloom bloom = null;
        UnityEngine.Rendering.Universal.ChromaticAberration chroma = null;
        float exp0 = 0f, bloom0 = 0f, chroma0 = 0f;
        foreach (var v in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
            if (v.name == "Hell Global Volume" && v.profile != null && v.profile.TryGet(out ca))   // (.profile: a runtime copy, the asset is untouched)
            {
                exp0 = ca.postExposure.value;
                if (v.profile.TryGet(out bloom)) bloom0 = bloom.intensity.value;
                if (!v.profile.TryGet(out chroma)) chroma = v.profile.Add<UnityEngine.Rendering.Universal.ChromaticAberration>();
                chroma0 = chroma.intensity.overrideState ? chroma.intensity.value : 0f;
                break;
            }
        for (float t = 0f; t <= time; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            if (sun != null) sun.intensity = Mathf.Lerp(sun0, Mathf.Max(sun0, arenaSun), k);
            RenderSettings.ambientIntensity = Mathf.Lerp(amb0, amb0 * arenaAmbient, k);
            RenderSettings.fogDensity = Mathf.Lerp(fog0, Mathf.Min(fog0, arenaFog), k);
            HellMist.Strength = 1f - k;   // the drifting mist banks go too: a clear bridge (user)
            if (ca != null) { ca.postExposure.overrideState = true; ca.postExposure.value = Mathf.Lerp(exp0, Mathf.Max(exp0, arenaExposure), k); }
            if (bloom != null) bloom.intensity.value = Mathf.Lerp(bloom0, bloom0 * arenaBloom, k);   // less glare off the wet blood (user)
            if (chroma != null) { chroma.intensity.overrideState = true; chroma.intensity.value = Mathf.Lerp(chroma0, Mathf.Max(chroma0, arenaChromatic), k); }
            yield return null;
        }
    }

    void StartFight()
    {
        if (checkpoint != null) checkpoint.Activate();
        foreach (var w in fightWalls) if (w != null) w.SetActive(true);
        if (healthBar != null) healthBar.SetActive(true);
        if (bossName != null) bossName.text = displayName;
        UpdateBar();
        MusicManager.Area(musicArea, 1.5f);
        if (!arenaLit) StartCoroutine(LightArena(2.5f));
        phase = 1;
        fighting = true;
        busy = false;
        actTimer = 1.5f;
        summonTimer = 4f;
        var move = player != null ? player.GetComponent<FirstPersonController>() : null;
        if (move != null) move.enabled = true;
        var punch = player != null ? player.GetComponentInChildren<PunchController>(true) : null;
        if (punch != null) punch.enabled = !GunController.Equipped;
    }

    // ---------------------------------------------------------------- the fight

    void Update()
    {
        if (chasing) { ChaseUpdate(); return; }
        UpdateDrowned();
        if (hovering) return;   // HeartPhase flies it
        if (!fighting || defeated || player == null) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        bool rage = phase >= 3;
        if (!busy)
        {
            Vector3 to = Flat(Tgt.position - transform.position);
            float dist = to.magnitude;
            FaceToward(Tgt.position, rage ? turnRate * 1.6f : turnRate);

            actTimer -= Time.deltaTime;
            summonTimer -= Time.deltaTime;

            // the blood attacks (DemonBossBlood) work the same on foot or in the van
            if (phase >= 2 && summonTimer <= 0f && drowned.Count < maxDrowned) { summonTimer = rage ? 14f : 20f; StartCoroutine(Summon(InVan ? (rage ? 4 : 5) : (rage ? 3 : 4))); }
            else if (actTimer <= 0f) StartCoroutine(Attack(PickAttack(dist)));
            else if (dist > 10f)
            {
                float spd = rage ? enragedWalk : walkSpeed;
                MoveTo(transform.position + to.normalized * spd * Time.deltaTime);
                Locomote(spd);
            }
            else Locomote(0f);
        }
    }

    IEnumerator Stun(float time)
    {
        stunned = true;
        Play(stunState);
        if (DialogueBox.Instance != null && Random.value < 0.35f) DialogueBox.Instance.Say(HS.S("525"));
        Transform m = model != null ? model : transform;
        Quaternion r0 = m.localRotation;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            m.localRotation = r0 * Quaternion.Euler(12f * Mathf.Sin(Mathf.Min(1f, t / 0.3f) * Mathf.PI * 0.5f), 0f, Mathf.Sin(t * 9f) * 2f);
            yield return null;
        }
        m.localRotation = r0;
        stunned = false;
    }

    // blood orbs: the old escape's orb, shrunk to a ball of blood this big (radius, metres)
    GameObject MakeOrb(float orbRadius)
    {
        GameObject g;
        if (orbModel != null)
        {
            g = Instantiate(orbModel);
            g.SetActive(true);
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var rs = g.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (size > 0.01f) g.transform.localScale *= orbRadius * 2f / size;
            }
        }
        else
        {
            g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial = boltMat;
            g.transform.localScale = Vector3.one * orbRadius * 2f;
        }
        g.name = "BloodOrb";
        var l = new GameObject("Glow").AddComponent<Light>();
        l.transform.SetParent(g.transform, false);
        l.color = new Color(1f, 0.12f, 0.05f); l.intensity = 5f; l.range = orbRadius * 5f;
        orbs.Add(g);
        spawned.Add(g);
        return g;
    }

    // rammed by the van: it hurts it, by how fast you hit
    void OnCollisionEnter(Collision c)
    {
        if (!fighting || defeated || carRb == null || c.rigidbody != carRb || Time.time < ramReady) return;
        float speed = c.relativeVelocity.magnitude;
        if (speed < ramMinSpeed) return;
        ramReady = Time.time + 1f;
        Vector3 point = c.contactCount > 0 ? c.GetContact(0).point : transform.position + Vector3.up;
        TakeDamage(Mathf.Min(ramMax, speed * ramDamagePerMs), point, Flat(transform.position - carRb.position).normalized);
        Roar(0.7f);
        Boom(point, 0.8f);
    }

    IEnumerator Bolt(Vector3 from, Vector3 to)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(b.GetComponent<Collider>());
        b.name = "BloodBolt";
        b.transform.localScale = Vector3.one * 0.7f;
        b.GetComponent<Renderer>().sharedMaterial = boltMat;
        var l = b.AddComponent<Light>(); l.color = new Color(1f, 0.15f, 0.05f); l.intensity = 4f; l.range = 6f;
        spawned.Add(b);
        var mark = Circle(to, 1.8f);
        SetCircle(mark, 1.8f, 0.35f);
        for (float t = 0f; t < boltFlight; t += Time.deltaTime)
        {
            float k = t / boltFlight;
            Vector3 p = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 6f;
            if (b == null) yield break;
            b.transform.position = p;
            yield return null;
        }
        Destroy(mark);
        if (b != null) Destroy(b);
        BloodFx.Spray(to + Vector3.up * 0.2f, Vector3.up, 25, 4f);
        if (carInteract != null && carInteract.InCar)
        {
            if (carRb != null && Flat(carRb.position - to).magnitude < 2.8f)
            {
                var ch = carRb.GetComponent<CarHealth>();
                if (ch != null) ch.TakeDamage(boltVanDamage);
                carRb.AddForce(Vector3.up * carRb.mass * 2f, ForceMode.Impulse);
            }
        }
        else if (Vector3.Distance(player.position, to) < 2f) Hurt(boltDamage);
    }

    IEnumerator Summon(int count)
    {
        busy = true;
        Locomote(0f);
        Play(roarState);
        Roar(0.8f);
        yield return new WaitForSeconds(1f);
        MakeZombieTemplate();
        for (int i = 0; i < count && drowned.Count < maxDrowned; i++)
        {
            // up over the rail on either side, somewhere near you
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector3 along = arena.InverseTransformPoint(Tgt.position);
            float z = Mathf.Clamp(along.z + Random.Range(-10f, 10f), -arenaHalfLength + 3f, arenaHalfLength - 3f);
            Vector3 onDeck = arena.TransformPoint(new Vector3(side * (arenaHalfWidth - 1f), 0f, z));
            onDeck.y = Ground(onDeck);
            Vector3 below = arena.TransformPoint(new Vector3(side * (arenaHalfWidth + 1.5f), 0f, z)) + Vector3.down * 3f;
            var g = Instantiate(zombieTemplate, below, Quaternion.LookRotation(Flat(onDeck - below)));
            g.SetActive(true);
            g.name = "Drowned";
            var d = new Drowned { z = g.GetComponent<Zombie>(), t = g.transform, a = g.GetComponentInChildren<Animator>(), climb = 0f, climbFrom = below, climbTo = onDeck };
            drowned.Add(d);
            spawned.Add(g);
            if (i == 0) d.z.Scream();
            yield return new WaitForSeconds(0.3f);
        }
        actTimer = 1f;
        busy = false;
    }

    void UpdateDrowned()
    {
        for (int i = drowned.Count - 1; i >= 0; i--)
        {
            var d = drowned[i];
            if (d.z == null || d.z.IsDead) { drowned.RemoveAt(i); continue; }
            if (d.climb < 1f)
            {
                d.climb += Time.deltaTime / 1.2f;
                d.t.position = Vector3.Lerp(d.climbFrom, d.climbTo, Mathf.SmoothStep(0f, 1f, d.climb));
                if (d.a != null && !d.a.GetCurrentAnimatorStateInfo(0).IsName("Crawl") && !d.a.IsInTransition(0)) ZombieAI.FadeTo(d.a, "Crawl", 0.1f, Random.value);
                continue;
            }
            if (player == null || defeated) continue;
            Vector3 to = Flat(Tgt.position - d.t.position);
            if (to.sqrMagnitude > 0.01f) d.t.rotation = Quaternion.Slerp(d.t.rotation, Quaternion.LookRotation(to), 8f * Time.deltaTime);
            if (to.magnitude > (InVan ? 3f : 1.5f))
            {
                Vector3 next = d.t.position + to.normalized * drownedSpeed * Time.deltaTime;
                if (InArena(next, 0.3f)) d.t.position = new Vector3(next.x, Ground(next), next.z);
                PlayOn(d.a, "Upright", drownedSpeed);
            }
            else
            {
                PlayOn(d.a, "Attack", 0f);
                if (Time.time >= d.hitAt) { d.hitAt = Time.time + 1.3f; if (fighting) Hurt(drownedDamage); }
            }
        }
    }

    void PlayOn(Animator a, string state, float speed)
    {
        if (a == null) return;
        var info = a.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName(state) && !a.IsInTransition(0)) ZombieAI.FadeTo(a, state, 0.15f, state == "Attack" ? 0f : Random.value);
        else if (state == "Attack" && info.normalizedTime > 0.95f) a.Play("Attack", 0, 0f);
        if (state == "Upright") { a.SetFloat("Speed", speed > 0.1f ? 3.67f : 0f); a.speed = speed > 0.1f ? Mathf.Clamp(speed / 3.67f, 0.8f, 1.6f) : 1f; }
    }

    void MakeZombieTemplate()
    {
        if (zombieTemplate != null || zombiePrefab == null) return;
        var hold = new GameObject("hold"); hold.SetActive(false);
        zombieTemplate = Instantiate(zombiePrefab, new Vector3(0f, -500f, 0f), Quaternion.identity, hold.transform);
        zombieTemplate.name = "Drowned (template)";
        zombieTemplate.SetActive(false);
        zombieTemplate.transform.SetParent(transform, true);
        Destroy(hold);
        var ai = zombieTemplate.GetComponent<ZombieAI>(); if (ai != null) DestroyImmediate(ai);
        var ag = zombieTemplate.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null) DestroyImmediate(ag);
        // soaked in the river: darker, redder
        foreach (var r in zombieTemplate.GetComponentsInChildren<Renderer>(true))
        {
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", new Color(0.55f, 0.22f, 0.2f));
            r.SetPropertyBlock(block);
        }
    }

    // ---------------------------------------------------------------- damage

    public void TakeDamage(float dmg, Vector3 point, Vector3 dir, Collider part = null)
    {
        if (defeated || (!begun && !chasing)) return;
        bool crit = heart != null && part != null && part.transform.IsChildOf(heart);
        if (crit) dmg *= heartMultiplier;
        if (stunned) dmg *= stunMultiplier;
        BloodFx.Spray(point, -dir, crit ? 30 : 12, crit ? 5f : 3f);
        if (sealedBoss) { if (shield != null) shield.Flash(); return; }   // the black hearts hold it: no damage until they are gone
        if (chasing)
        {
            // shot from the van: it hurts it, it staggers it, but the real fight is on the bridge
            health = Mathf.Max(maxHealth * chaseHealthFloor, health - dmg);
            UpdateBar();
            if (heart != null && crit) StartCoroutine(Pulse());
            staggerDamage += dmg;
            if (staggerDamage >= chaseStaggerDamage && Time.time > staggerUntil)
            {
                staggerDamage = 0f;
                staggerUntil = Time.time + chaseStaggerTime;
                Play(stunState);
                Roar(0.7f);
            }
            return;
        }
        health -= dmg;
        if (!heartsDone && phase >= 3 && health < maxHealth * heartsBelow)
        {
            health = Mathf.Max(health, 1f);   // it cannot die before the hearts have been dealt with
            UpdateBar();
            StopAllCoroutines();   // whatever it was in the middle of (a long blood pattern, a slam) is over
            ClearTransients();
            busy = true; sealedBoss = true; hovering = true; stunned = false; heartsDone = true;
            StartCoroutine(HeartPhase());
            return;
        }
        UpdateBar();
        if (heart != null && crit) StartCoroutine(Pulse());

        if (health <= 0f) { StopAllCoroutines(); busy = true; StartCoroutine(Die()); return; }
        if (phase == 1 && health < maxHealth * 0.65f) StartCoroutine(NextPhase(2, phase2Lines));
        else if (phase == 2 && health < maxHealth * 0.3f) StartCoroutine(NextPhase(3, phase3Lines));
    }

    // The last phase: it flies up over the middle of the deck, three black hearts push up out of the bridge tied to
    // it, and nothing hurts it until they're all shot. Then it crashes down, stunned, and the rest of its health is
    // yours to take.
    IEnumerator HeartPhase()
    {
        phase = 4;
        Locomote(0f);
        Play(roarState);
        Roar(1f);
        Boom(transform.position, 0.8f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(heartsLines + "|" + heartsHint);
        if (bossName != null) bossName.text = sealedName;

        shield = BossShield.Make(Centre, shieldRadius);
        shield.UseOrb(orbModel);           // he floats inside the red orb itself...
        shield.AddDebris(bodyParts, 24);   // ...with the body parts drifting in it round him
        Vector3 mid = arena != null ? arena.position : transform.position;
        Vector3 from = transform.position, top = new Vector3(mid.x, mid.y + hoverHeight, mid.z);
        for (float t = 0f; t < 2.4f; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(from, top, Mathf.SmoothStep(0f, 1f, t / 2.4f));
            FaceToward(Tgt.position, 3f);
            yield return null;
        }
        SpawnHearts();

        float next = 3.5f;
        Coroutine attack = null;
        while (hearts.Exists(h => h != null && !h.Dead))
        {
            transform.position = top + Vector3.up * Mathf.Sin(Time.time * 1.3f) * 0.8f;
            FaceToward(Tgt.position, 2f);
            next -= Time.deltaTime;
            if (next <= 0f) { next = Random.Range(5f, 7f); attack = StartCoroutine(HoverAttack()); }
            yield return null;
        }
        if (attack != null) StopCoroutine(attack);

        // the last one's gone: the bubble bursts and it falls
        if (shield != null) shield.Pop();
        Play(stunState);
        Roar(1.2f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(crashLines);
        Vector3 up = transform.position, land = new Vector3(up.x, Ground(new Vector3(up.x, mid.y, up.z)), up.z);
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(up, land, (t / 0.7f) * (t / 0.7f));
            yield return null;
        }
        transform.position = land;
        Boom(land, 2.2f);
        if (Flat(Tgt.position - land).magnitude < 7f && Grounded()) { Hurt(slamDamage); Knock(Flat(Tgt.position - land).normalized * 8f + Vector3.up * 3f); }
        foreach (var o in orbs) if (o != null) Destroy(o);
        sealedBoss = false; hovering = false;
        if (bossName != null) bossName.text = displayName;
        DropPickup();
        StartCoroutine(Stun(crashStun));
        yield return new WaitForSeconds(crashStun);
        actTimer = 1.2f;
        busy = false;
    }

    void SpawnHearts()
    {
        Transform src = heart != null ? heart.Find("Heart model") : null;
        float reach = arena != null ? arenaHalfLength - 5f : 8f;   // right along the deck: far apart, ~24 m between them
        for (int i = 0; i < blackHearts; i++)
        {
            float z = blackHearts > 1 ? Mathf.Lerp(-1f, 1f, i / (float)(blackHearts - 1)) * reach : 0f;
            Vector3 p = arena != null ? arena.TransformPoint(new Vector3(i % 2 == 0 ? -4.5f : 4.5f, 0f, z)) : transform.position + new Vector3(i * 6f - 6f, 0f, 6f);
            p.y = Ground(p);
            var g = new GameObject("Black heart");
            g.transform.position = p + Vector3.up * blackHeartHeight * 0.6f;
            var col = g.AddComponent<SphereCollider>(); col.radius = blackHeartHeight * 0.45f;
            GameObject m;
            if (src != null)   // the same anatomical heart as its own, big and black
            {
                float k = blackHeartHeight / 1.3f;
                m = Instantiate(src.gameObject, g.transform);
                m.SetActive(true);
                m.transform.localPosition = src.localPosition * heart.lossyScale.y * k;
                m.transform.localRotation = Quaternion.identity;
                m.transform.localScale = src.lossyScale * k;
                foreach (var c in m.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var l in m.GetComponentsInChildren<Light>(true)) Destroy(l);
            }
            else
            {
                m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(m.GetComponent<Collider>());
                m.transform.SetParent(g.transform, false);
                m.transform.localScale = Vector3.one * blackHeartHeight;
            }
            foreach (var r in m.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int j = 0; j < mats.Length; j++) mats[j] = BossHeart.BlackMat();
                r.sharedMaterials = mats;
                r.enabled = true;
            }
            var h = g.AddComponent<BossHeart>();
            h.boss = this; h.health = blackHeartHealth; h.height = blackHeartHeight; h.tetherMat = boltMat;
            h.anchor = Centre;
            hearts.Add(h);
            BloodFx.Spray(p, Vector3.up, 40, 6f);
            Boom(p, 0.8f);
        }
    }

    // a black heart was shot to pieces
    public void HeartBroken(BossHeart h)
    {
        Boom(h.transform.position, 1.3f);
        BloodFx.Spray(h.transform.position, Vector3.up, 60, 8f);
        Roar(0.6f);
        int broken = hearts.FindAll(x => x != null && (x == h || x.Dead)).Count;
        Truth.Set("Heart" + broken + "Freed");
        if (heartNameLines != null && broken >= 1 && broken <= heartNameLines.Length && DialogueBox.Instance != null) DialogueBox.Instance.SayNow(heartNameLines[broken - 1]);
        else if (hearts.Exists(x => x != null && x != h && !x.Dead) && DialogueBox.Instance != null) DialogueBox.Instance.SayNow("One down!");
    }

    // the rings, beams, orbs and bolts of whatever attack was running
    void ClearTransients()
    {
        foreach (var lr in GetComponentsInChildren<LineRenderer>()) Destroy(lr.gameObject);
        foreach (var mk in marks) if (mk != null) Destroy(mk);
        marks.Clear();
        foreach (var o in orbs) if (o != null) Destroy(o);
        orbs.Clear();
        foreach (var g in spawned) if (g != null && g.name == "BloodBolt") Destroy(g);
        ClearBlood();
    }

    void ClearHearts()
    {
        foreach (var h in hearts) if (h != null) Destroy(h.gameObject);
        hearts.Clear();
        if (shield != null) Destroy(shield.gameObject);
        sealedBoss = hovering = heartsDone = false;
    }

    IEnumerator NextPhase(int to, string lines)
    {
        phase = to;
        yield return new WaitWhile(() => busy);
        busy = true;
        Locomote(0f);
        Play(roarState);
        Roar(1f);
        Boom(transform.position, 0.6f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(lines);
        DropPickup();
        if (to >= 3 && model != null) StartCoroutine(BurnRed());
        yield return new WaitForSeconds(1.8f);
        summonTimer = 0.5f;
        busy = false;
    }

    IEnumerator Die()
    {
        defeated = true;
        fighting = false;
        stunned = false;
        ClearHearts();
        ClearTransients();
        if (healthBar != null) healthBar.SetActive(false);
        foreach (var d in drowned) if (d.z != null && !d.z.IsDead) d.z.KillInstant(d.t.position + Vector3.up, Vector3.up * 200f);
        MusicManager.Stop(4f);

        // the killing blow: everything slows while it roars
        float ts0 = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0.35f;
        Play(roarState);
        Roar(1.2f);
        yield return new WaitForSecondsRealtime(1.3f);
        Time.timeScale = ts0;

        // take the camera; you and the van hold still
        var carCtl = carInteract != null ? carInteract.carController : null;
        bool hadCar = carCtl != null && carCtl.enabled;
        if (InVan)
        {
            if (carCtl is PrometeoCarController pc) { pc.ThrottleOff(); pc.Brakes(); }
            if (carCtl != null) carCtl.enabled = false;
            if (carRb != null) { carRb.linearVelocity = Vector3.zero; carRb.angularVelocity = Vector3.zero; }
        }
        var locked = new List<Behaviour>();
        foreach (var b in lockDuring ?? new Behaviour[0]) if (b != null && b.enabled) { b.enabled = false; locked.Add(b); }
        if (fader != null) yield return fader.FadeTo(1f, 0.25f);
        SetBars(1f);
        if (cutsceneCam != null) cutsceneCam.enabled = true;
        float fogBefore = RenderSettings.fogDensity;
        RenderSettings.fogDensity = Mathf.Min(fogBefore, burstFog);

        // where it goes in: off the front of the bridge, the very end (where the judgement waits), out to sea
        Vector3 p0 = transform.position;
        Vector3 outDir = arena != null ? Flat(arena.forward).normalized : Flat(transform.forward);
        Vector3 along = Vector3.Cross(Vector3.up, outDir);
        Vector3 edge = arena != null ? arena.TransformPoint(new Vector3(0f, 0f, arenaHalfLength + 0.5f)) : p0 + outDir * 8f;
        edge.y = p0.y;
        Transform m = model != null ? model : transform;
        Quaternion r0 = m.localRotation;

        // ---- 1. it crashes down onto the deck
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.3f));
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(deathLines);
        Vector3 camA = SafeCam(p0 + Vector3.up * 2f, p0 - outDir * 17f + along * 7f + Vector3.up * 2.2f);
        if (!string.IsNullOrEmpty(dieState) && animator != null && animator.HasState(0, Animator.StringToHash(dieState))) Play(dieState);
        for (float t = 0f; t < 2.6f; t += Time.deltaTime)
        {
            Place(camA + Random.insideUnitSphere * 0.05f * Mathf.Max(0f, 1f - t), Centre());
            yield return null;
        }
        Boom(p0, 1.4f);

        // cut: it has crawled, bleeding, most of the way to the rail (the deck is wide: this keeps the next shots close)
        if (RevisedPlotDirector.Instance != null)
            yield return RevisedPlotDirector.Instance.AmalgamationAccounts(this);
        if (fader != null) yield return fader.FadeTo(1f, 0.2f);
        p0 = edge - outDir * 9f;
        p0.y = Ground(p0);
        transform.position = p0;
        transform.rotation = Quaternion.LookRotation(-along);
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.3f));

        // ---- 2. hands come up over the rail: the drowned, coming for it
        MakeZombieTemplate();
        var grab = new List<(Transform t, Animator a, Vector3 from, Vector3 to, float delay)>();
        for (int i = 0; i < dragCount && zombieTemplate != null; i++)
        {
            float a = Mathf.Lerp(-5.5f, 5.5f, dragCount > 1 ? i / (float)(dragCount - 1) : 0.5f) + Random.Range(-0.6f, 0.6f);
            Vector3 below = edge + along * a + outDir * 1.2f;
            below.y = waterY + 0.3f;
            Vector3 onDeck = p0 + outDir * Random.Range(0.5f, 3f) + along * (a * 0.55f);
            onDeck.y = Ground(onDeck);
            var g = Instantiate(zombieTemplate, below, Quaternion.LookRotation(Flat(onDeck - below)));
            foreach (var mb in g.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);   // props: no AI, no health
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            g.name = "Drowned (dragging it down)";
            g.SetActive(true);
            var an = g.GetComponentInChildren<Animator>();
            if (an != null && an.HasState(0, Animator.StringToHash("Crawl"))) an.Play("Crawl", 0, Random.value);
            spawned.Add(g);
            grab.Add((g.transform, an, below, onDeck, i * 0.14f));
        }
        var shriek = Sounds.Clip("Hell City/Zombie scream", null);
        if (shriek != null) AudioSource.PlayClipAtPoint(shriek, edge, 1f);
        Vector3 camB = SafeCam(edge + Vector3.up * 1.5f, edge + along * 10f - outDir * 1.8f + Vector3.up * 1.3f);
        for (float t = 0f; t < 2.6f; t += Time.deltaTime)
        {
            foreach (var gz in grab)
            {
                float k = Mathf.Clamp01((t - gz.delay) / 1.4f);
                Vector3 top = new Vector3(gz.from.x, edge.y + 0.3f, gz.from.z);
                Vector3 p = k < 0.5f
                    ? Vector3.Lerp(gz.from, top, k / 0.5f)                                       // up the side of the bridge
                    : Vector3.Lerp(top, gz.to, Mathf.SmoothStep(0f, 1f, (k - 0.5f) / 0.5f));     // over the rail, onto it
                gz.t.position = p;
                if (k >= 1f && gz.a != null && gz.a.HasState(0, Animator.StringToHash("Feed")) && !gz.a.GetCurrentAnimatorStateInfo(0).IsName("Feed")) gz.a.CrossFadeInFixedTime("Feed", 0.2f);
            }
            Place(camB, Vector3.Lerp(edge + Vector3.up, Centre(), 0.55f));
            yield return null;
        }

        // ---- 3. they drag it across the deck to the edge, clawing at the boards
        Quaternion outRot = Quaternion.LookRotation(outDir);
        var offs = new List<Vector3>();
        foreach (var gz in grab) offs.Add(Quaternion.Inverse(outRot) * (gz.t.position - p0));
        var drag = MakeSource(Sounds.Clip("Demon Fight/Boss dragged under", null) ?? ProceduralAudio.Rumble(), true, 0.9f);
        drag.Play();
        Vector3 dragTo = edge - outDir * 1.5f;
        dragTo.y = p0.y;
        Roar(0.9f);
        for (float t = 0f; t < 3.6f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 3.6f);
            Vector3 pos = Vector3.Lerp(p0, dragTo, k * k) + along * Mathf.Sin(t * 9f) * 0.12f;   // jerked along in tugs
            transform.position = pos;
            m.localRotation = r0 * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 6f) * 5f);
            for (int i = 0; i < grab.Count; i++) grab[i].t.position = pos + outRot * offs[i] + outDir * Mathf.Sin(t * 8f + i) * 0.15f;
            if (Random.value < 0.25f) BloodFx.Spray(pos + Random.insideUnitSphere * 2f + Vector3.up * 0.3f, Vector3.up - outDir, 8, 3f);
            Vector3 cam = SafeCam(pos + Vector3.up * 2f, pos - outDir * 3f + along * 15f + Vector3.up * 4.5f);
            Place(cam, Centre());
            yield return null;
        }

        // ---- 4. over the edge, and under
        Vector3 waterPt = edge + outDir * 5f;
        waterPt.y = waterY;
        Vector3 camD = edge + outDir * 17f + along * 6f;
        camD.y = waterY + 3.2f;
        Vector3 fromPos = transform.position;
        for (float t = 0f; t < 1.1f; t += Time.deltaTime)
        {
            float k = t / 1.1f;
            Vector3 pos = Vector3.Lerp(fromPos, waterPt, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.5f;
            transform.position = pos;
            m.localRotation = r0 * Quaternion.Euler(-80f * k, 0f, 0f);
            for (int i = 0; i < grab.Count; i++) grab[i].t.position = pos + outRot * offs[i];
            Place(camD, Vector3.Lerp(edge, waterPt, 0.6f) + Vector3.up * 1.5f);
            yield return null;
        }
        for (int i = 0; i < 3; i++) BloodFx.Spray(waterPt + Random.insideUnitSphere * 2f, Vector3.up + Random.insideUnitSphere * 0.4f, 90, 12f);
        Boom(waterPt, 2.2f);
        Roar(0.7f);

        // it thrashes, and they pull it down
        for (float t = 0f; t < 3.8f; t += Time.deltaTime)
        {
            float k = t / 3.8f;
            Vector3 pos = waterPt + Vector3.down * (k * k * 14f) + Vector3.up * Mathf.Sin(t * 7f) * 0.4f * (1f - k);
            transform.position = pos;
            for (int i = 0; i < grab.Count; i++) grab[i].t.position = pos + outRot * offs[i];
            if (Random.value < 0.5f * (1f - k) + 0.1f) BloodFx.Spray(waterPt + Flat(Random.insideUnitSphere) * 3f, Vector3.up, 6, 3f + 5f * (1f - k));
            Place(camD + Vector3.down * k * 1.2f, Vector3.Lerp(waterPt + Vector3.up * 1.5f, waterPt, k));
            drag.volume = Mathf.Lerp(0.9f, 0.2f, k);
            yield return null;
        }
        SetModelVisible(false);
        foreach (var gz in grab) if (gz.t != null) Destroy(gz.t.gameObject);
        m.localRotation = r0;

        // the water closes over it: a last few bubbles of blood, then nothing
        for (float t = 0f; t < 2f; t += Time.deltaTime)
        {
            if (Random.value < 0.12f * (1f - t / 2f)) BloodFx.Spray(waterPt + Flat(Random.insideUnitSphere) * 1.5f, Vector3.up, 4, 2f);
            Place(camD + Vector3.down * 1.2f, waterPt);
            yield return null;
        }
        StartCoroutine(FadeSource(drag, 1.5f));

        // ---- back to you
        if (fader != null) yield return fader.FadeTo(1f, 0.5f);
        if (cutsceneCam != null) cutsceneCam.enabled = false;
        SetBars(0f);
        RenderSettings.fogDensity = fogBefore;
        foreach (var b in locked) if (b != null) b.enabled = true;
        if (carCtl != null && hadCar && InVan) carCtl.enabled = true;
        foreach (var w in fightWalls) if (w != null) w.SetActive(false);
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.6f));
        yield return new WaitForSeconds(1f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.Say(afterLines);
        yield return new WaitForSeconds(5f);
        Active = null;
        onDefeated.Invoke();
    }

    // ---------------------------------------------------------------- checkpoint

    // died in the fight: back at the start of the bridge, it's waiting, full health, straight into the fight
    public void ResetFight()
    {
        StopAllCoroutines();
        ClearHearts();
        ClearBlood();
        foreach (var g in spawned) if (g != null) Destroy(g);
        spawned.Clear();
        drowned.Clear();
        foreach (var c in GetComponentsInChildren<LineRenderer>()) Destroy(c.gameObject);
        foreach (var m in marks) if (m != null) Destroy(m);
        marks.Clear();
        health = maxHealth;
        defeated = false;
        stunned = false;
        busy = false;
        begun = true;
        Active = this;
        foreach (var o in orbs) if (o != null) Destroy(o);
        orbs.Clear();
        if (fightInVan && carInteract != null && carRb != null)
        {
            // back behind the wheel, the van fixed up
            var ch = carRb.GetComponent<CarHealth>();
            if (ch != null) ch.Repair();
            PlaceVan();
            if (!carInteract.InCar) carInteract.SendMessage("EnterCar");
        }
        else if (carInteract != null) carInteract.ForceExit(true);   // the checkpoint unlocked the wreck: it stays a wreck
        transform.SetPositionAndRotation(homePos, homeRot);
        if (model != null) { model.localRotation = modelRot0; model.localScale = modelScale0; }
        SetModelVisible(true);
        gameObject.SetActive(true);
        if (cutsceneCam != null) cutsceneCam.enabled = false;
        SetBars(0f);
        StartFight();
        Play(roarState);
        Roar(0.8f);
        actTimer = 2.5f;
    }

    // ---------------------------------------------------------------- helpers

    Quaternion modelRot0 = Quaternion.identity;
    Vector3 modelScale0 = Vector3.one;
    [Header("Testing")]
    public bool testHearts = false;          // same, but straight to the black hearts (health just above heartsBelow, phase 3)
    public bool testHalfHealth = false;      // press Play in MainGameScene: 3 s later you're in the bridge fight, it at 49%

    IEnumerator Start()
    {
        if (model != null) { modelRot0 = model.localRotation; modelScale0 = model.localScale; }
#if UNITY_EDITOR
        // HellScape > Play > Final Boss sets this for one run (so the scene itself needn't be edited)
        if (UnityEditor.SessionState.GetBool("HellScape.TestHearts", false)) testHearts = true;
#endif
        if (!testHalfHealth && !testHearts) yield break;
        // past the title screen, into the hell world (as if back from the prologue), then the fight
        if (TitleScreen.Showing) { SleepSequence.WakeInHell = true; UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name); yield break; }
        yield return new WaitForSeconds(3f);
        ResetFight();
        health = maxHealth * (testHearts ? heartsBelow + 0.01f : 0.49f);
        phase = testHearts ? 3 : 2;
        UpdateBar();
#if UNITY_EDITOR
        UnityEditor.SessionState.EraseBool("HellScape.TestHearts");
#endif
    }

    void Hurt(float dmg)
    {
        if (InVan)
        {
            var ch = carRb != null ? carRb.GetComponent<CarHealth>() : null;
            if (ch != null) ch.TakeDamage(dmg * vanDamageScale);
            return;
        }
        if (playerHealth != null && !playerHealth.IsDead) playerHealth.TakeDamage(dmg);
    }

    void Knock(Vector3 v)
    {
        if (InVan) { if (carRb != null) carRb.AddForce(v * carRb.mass * 0.5f, ForceMode.Impulse); return; }
        var body = player.GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic) body.linearVelocity = v;
    }

    bool Grounded()
    {
        if (InVan) return true;
        return Physics.Raycast(player.position + Vector3.up * 0.3f, Vector3.down, 0.75f, ~0, QueryTriggerInteraction.Ignore);
    }

    bool InArena(Vector3 p, float margin)
    {
        if (arena == null) return true;
        Vector3 l = arena.InverseTransformPoint(p);
        return Mathf.Abs(l.x) < arenaHalfWidth - margin && Mathf.Abs(l.z) < arenaHalfLength - margin;
    }

    void MoveTo(Vector3 p)
    {
        if (!InArena(p, 1.5f)) return;
        p.y = Ground(p);
        transform.position = p;
    }

    float Ground(Vector3 p)
    {
        float best = float.MinValue;
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, p.y + 4f, p.z), Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform) || h.collider.GetComponentInParent<Zombie>() != null) continue;
            if (player != null && h.collider.transform.IsChildOf(player)) continue;
            if (carRb != null && h.collider.transform.IsChildOf(carRb.transform)) continue;
            best = Mathf.Max(best, h.point.y);
        }
        return best > float.MinValue ? best : (arena != null ? arena.position.y : p.y);
    }

    void FaceToward(Vector3 p, float rate)
    {
        Vector3 to = Flat(p - transform.position);
        if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), rate * Time.deltaTime);
    }

    Vector3 Centre()
    {
        if (heart != null) return heart.position;   // skinned renderers can report huge bounds
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return transform.position + Vector3.up * 3f;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) if (!(r is LineRenderer)) b.Encapsulate(r.bounds);
        return b.center;
    }

    void Play(string state)
    {
        if (animator == null || string.IsNullOrEmpty(state) || !animator.isActiveAndEnabled) return;
        int h = Animator.StringToHash(state);
        if (animator.HasState(0, h)) animator.CrossFadeInFixedTime(h, 0.15f, 0, 0f);
        else foreach (var p in animator.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == state) { animator.SetTrigger(state); break; }
    }

    float stepPhase;
    AudioSource stompSrc;
    AudioClip stompClip;

    // one deep footfall per stride while it moves
    void Stomps(float speed)
    {
        if (speed < 0.5f || defeated) { stepPhase = 0.6f; return; }
        stepPhase += speed * Time.deltaTime / Mathf.Max(0.5f, stompSpacing);
        if (stepPhase < 1f) return;
        stepPhase -= 1f;
        if (stompClip == null) stompClip = Sounds.Clip("Demon Fight/Boss footsteps", null) ?? ProceduralAudio.Stomp();
        if (stompSrc == null)
        {
            stompSrc = gameObject.AddComponent<AudioSource>();
            stompSrc.playOnAwake = false;
            stompSrc.spatialBlend = 0.65f;          // mostly felt everywhere, a little placed
            stompSrc.rolloffMode = AudioRolloffMode.Linear;
            stompSrc.minDistance = 20f;
            stompSrc.maxDistance = 160f;
            stompSrc.dopplerLevel = 0f;
        }
        stompSrc.pitch = Random.Range(0.88f, 1.04f);
        stompSrc.PlayOneShot(stompClip, stompLevel * Sounds.Volume("Demon Fight/Boss footsteps"));
    }

    void Locomote(float speed)
    {
        Stomps(speed);
        if (animator == null || !animator.isActiveAndEnabled) return;
        bool moving = speed > 0.1f;
        string want = crawling && !string.IsNullOrEmpty(stunState) ? stunState
                    : speed > walkSpeed * 2.5f && !string.IsNullOrEmpty(chargeState) ? chargeState
                    : moving || string.IsNullOrEmpty(idleState) ? moveState : idleState;
        int h = Animator.StringToHash(want);
        var info = animator.GetCurrentAnimatorStateInfo(0);
        if (!busy || want == chargeState)
            if (animator.HasState(0, h) && !info.IsName(want) && !animator.IsInTransition(0)) animator.CrossFadeInFixedTime(h, 0.25f);
        foreach (var p in animator.parameters)
            if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) { animator.SetFloat("Speed", moving ? moveClipSpeed : 0f); break; }
        animator.speed = crawling ? crawlAnimSpeed : moving && want == moveState ? Mathf.Clamp(speed / Mathf.Max(0.1f, moveClipSpeed * transform.lossyScale.y), 0.5f, 2f) : 1f;
    }

    void Roar(float volume = 1f)
    {
        // its own scream if there is one (as recorded); otherwise a zombie's, pitched right down
        var own = Sounds.Clip("Demon Fight/Boss scream", null);
        var clip = own != null ? own : Sounds.Clip("Hell City/Zombie scream", null);
        if (clip == null) return;
        float pitch = own != null ? 1f : 0.45f;
        var s = MakeSource(clip, false, volume * (own != null ? Sounds.Volume("Demon Fight/Boss scream") : 1f));
        s.pitch = pitch;
        s.maxDistance = 120f;
        s.Play();
        Destroy(s.gameObject, Mathf.Min(clip.length / pitch + 0.2f, 4f));   // (the recording is long: each roar is just its opening)
    }

    AudioSource MakeSource(AudioClip clip, bool loop, float volume)
    {
        var s = new GameObject("DemonSound").AddComponent<AudioSource>();
        s.transform.SetParent(transform, false);
        s.transform.localPosition = Vector3.up * 4f;
        s.clip = clip; s.loop = loop; s.volume = volume; s.spatialBlend = 0.5f; s.maxDistance = 120f;
        return s;
    }

    IEnumerator FadeSource(AudioSource s, float time)
    {
        float v0 = s.volume;
        for (float t = 0f; t < time && s != null; t += Time.deltaTime) { s.volume = v0 * (1f - t / time); yield return null; }
        if (s != null) Destroy(s.gameObject);
    }

    // a hit on the deck: dust of blood, a thud, the camera jolts
    void Boom(Vector3 at, float size)
    {
        BloodFx.Spray(at + Vector3.up * 0.3f, Vector3.up, Mathf.RoundToInt(30 * size), 5f * size);
        var thud = Sounds.Clip("Silo Fight/Fall thud", null) ?? Sounds.Clip("Prologue/Street impact", null);
        if (thud != null) AudioSource.PlayClipAtPoint(thud, at, size);
        var cam = Camera.main;
        if (cam != null && Vector3.Distance(cam.transform.position, at) < 25f) StartCoroutine(CamShake(cam.transform, 0.35f * size));
    }

    IEnumerator CamShake(Transform cam, float amount)
    {
        Vector3 p0 = cam.localPosition;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            if (cam == null) yield break;
            cam.localPosition = p0 + Random.insideUnitSphere * amount * 0.25f * (1f - t / 0.4f);
            yield return null;
        }
        if (cam != null) cam.localPosition = p0;
    }

    void Shake(Vector3 camPos, float amount)
    {
        if (cutsceneCam != null) cutsceneCam.transform.position = camPos + Random.insideUnitSphere * amount * 0.2f;
    }

    readonly List<GameObject> marks = new List<GameObject>();

    GameObject Circle(Vector3 at, float radius)
    {
        var g = new GameObject("SlamMark");
        marks.Add(g);   // (not parented to it: it runs off during the chase and the circle must stay put)
        var lr = g.AddComponent<LineRenderer>();
        lr.loop = true; lr.useWorldSpace = true; lr.positionCount = 40;
        lr.sharedMaterial = ringMat;
        lr.widthMultiplier = 0.35f;
        g.transform.position = at;
        SetCircle(g, radius, 0.3f);
        return g;
    }

    void SetCircle(GameObject g, float radius, float width)
    {
        if (g == null) return;
        var lr = g.GetComponent<LineRenderer>();
        Vector3 c = g.transform.position + Vector3.up * 0.08f;
        for (int i = 0; i < lr.positionCount; i++)
        {
            float a = i * Mathf.PI * 2f / lr.positionCount;
            lr.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
        }
        lr.widthMultiplier = width;
    }

    void DropPickup()
    {
        if (healthPickup == null || arena == null) return;
        Vector3 p = arena.TransformPoint(new Vector3(Random.Range(-arenaHalfWidth + 2f, arenaHalfWidth - 2f), 0f, -arenaHalfLength + Random.Range(4f, 12f)));
        p.y = Ground(p) + 0.6f;
        var g = Instantiate(healthPickup, p, Quaternion.identity);
        g.SetActive(true);
        spawned.Add(g);
    }

    IEnumerator Pulse()
    {
        var l = heart.GetComponentInChildren<Light>();
        if (l == null) yield break;
        float i0 = l.intensity;
        l.intensity = i0 * 2.5f;
        yield return new WaitForSeconds(0.08f);
        if (l != null) l.intensity = i0;
    }

    IEnumerator BurnRed()
    {
        var lights = GetComponentsInChildren<Light>();
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            foreach (var l in lights) if (l != null) l.intensity *= 1f + Time.deltaTime * 0.4f;
            yield return null;
        }
    }

    void UpdateBar()
    {
        if (healthFill != null) healthFill.localScale = new Vector3(Mathf.Clamp01(health / maxHealth), 1f, 1f);
    }

    HashSet<Renderer> keepOff;   // off in the scene to begin with (the heart's plain ball, under the beating model): stay off

    void SetModelVisible(bool on)
    {
        keepOff ??= new HashSet<Renderer>(GetComponentsInChildren<Renderer>(true).Where(r => !r.enabled));
        foreach (var r in GetComponentsInChildren<Renderer>(true)) if (!(r is LineRenderer) && !keepOff.Contains(r)) r.enabled = on;
        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = on;
        foreach (var l in GetComponentsInChildren<Light>(true)) l.enabled = on;
    }

    void Place(Vector3 pos, Vector3 look)
    {
        if (cutsceneCam == null) return;
        cutsceneCam.transform.position = pos;
        if ((look - pos).sqrMagnitude > 0.01f) cutsceneCam.transform.rotation = Quaternion.LookRotation(look - pos);
    }

    void SetBars(float k)
    {
        CutsceneHUD.Hide(k > 0f);
        foreach (var b in new[] { barTop, barBottom })
            if (b != null) b.sizeDelta = new Vector2(b.sizeDelta.x, barHeight * k);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
