using UnityEngine;
using UnityEngine.UI;

// The silo filling with blood. The surface rises from the floor to just under the top landing and carries you up,
// but only if you keep swimming: an air bar drains while you're in it, every press of Space is a kick that tops
// it up and pushes you up. Let it run low and you sink; let it empty and you go under, and after a moment you drown.
public class BloodFlood : MonoBehaviour
{
    public Transform surface;               // flat plane, disabled until Begin()
    public float startY;
    public float endY;
    public float riseTime = 40f;
    public float radius = 7.5f;             // horizontal reach from the surface's centre (the silo wall)

    [Header("Player")]
    public Rigidbody player;
    public float floatOffset = -0.35f;      // player root sits this far from the surface (used when head is empty)
    public Transform head;                  // the player camera: keeps the eyes eyesAbove the surface so you can see
    public float eyesAbove = 0.55f;
    public float buoyancy = 4f;
    public GameObject inBloodOverlay;       // red screen tint while swimming

    [Header("Swimming (mash Space)")]
    public float drainPerSecond = 0.24f;    // a full bar lasts about four seconds without a kick
    public float perKick = 0.1f;            // ~2.5-3 kicks a second keeps you up
    public float kickLift = 0.35f;          // a kick pushes you up this far on top of where the air puts you
    public float sinkDepth = 1.6f;          // how far under the surface an empty bar drags your eyes
    public float drownAfter = 1.6f;         // seconds with an empty bar before you drown
    public GameObject airBar;               // the bar's root (shown while swimming)
    public RectTransform airFill;           // scaled on x
    public Image airFillImage;              // tinted as it empties
    public CanvasGroup underTint;           // full-screen red that thickens as you sink (optional)
    public PlayerHealth health;
    [TextArea] public string firstLine = "It's over my head-- kick. KICK!";
    [TextArea] public string lowLine = "Maya: Don't stop! KICK!";
    [TextArea] public string lowLineSam = "Sam: Don't stop! KICK!";   // (when Maya isn't with you)

    [Header("Colleagues (Survivor) in the blood")]
    public float survivorHead = 1.45f;      // their feet sit this far under the surface (head just out)
    public float survivorGrip = 1.5f;       // seconds of a nearly empty bar before the last one holding on goes under
    [TextArea] public string clingLine = "Maya: We came down the stairs-- we can't swim in this! Don't let us go under!";
    [TextArea] public string clingLineSam = "Sam: It's coming up the stairs-- I can't swim in this! Don't let me go under!";
    [TextArea] public string mayaDrownLine = "Maya: I can't-- I can't hold--|No-- MAYA!";
    [TextArea] public string samDrownLine = "Sam: Help me-- please--|SAM!";

    [Header("Killed again in the red city: they come back as the Lost or the Damned")]
    public Material ghostMaterial;                     // the Lost's blue (the office ghosts' material)
    public RuntimeAnimatorController lostController;   // a floating pose (optional)
    public RisenDamned risenDamned;                    // the Damned one of them comes back as
    public TurningCutscene cutscene;                   // plays each transformation (optional: without it they just appear)
    [TextArea] public string lostLine = "He's up there. Over the blood. Blue, like the others in the city...|Sam's one of the Lost now. Because of me.";
    [TextArea] public string damnedLine = "Something's dragging itself out of the blood.|...Maya? She admitted it. Down there, she said it out loud...|...and it TOOK her.";

    [Header("Sound")]
    public AudioSource rumble;              // procedural rumble if it has no clip
    public AudioSource sfx;                 // kicks and the gurgle (2D)
    public UnityEngine.Events.UnityEvent onBegin = new UnityEngine.Events.UnityEvent();   // e.g. let the colleagues be picked up, show the way out

    private bool running, swimming, drowned, warned, told;
    private float t, air = 1f, emptyFor, kickTimer, lowFor, outFor;
    private GameObject risenLost;
    private bool holding;

    // a cutscene is playing: no drain, no sinking, no drowning, no bar
    public void Hold(bool on)
    {
        holding = on;
        if (on) { air = Mathf.Max(air, 0.75f); lowFor = emptyFor = 0f; }
        if (airBar != null) airBar.SetActive(!on && swimming && !drowned);
    }
    private Survivor damnedPending;

    public float SurfaceY => surface.position.y;
    public float Air => air;

    void Start()
    {
        if (surface != null) surface.gameObject.SetActive(false);
        if (inBloodOverlay != null) inBloodOverlay.SetActive(false);
        if (airBar != null) airBar.SetActive(false);
        if (underTint != null) underTint.alpha = 0f;
        if (health == null && player != null) health = player.GetComponent<PlayerHealth>();
        if (sfx == null) { sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false; sfx.spatialBlend = 0f; }
    }

    public void Begin()
    {
        if (running) return;
        running = true;
        MusicManager.Area("Escape");
        surface.position = new Vector3(surface.position.x, startY, surface.position.z);
        surface.gameObject.SetActive(true);
        if (rumble != null)
        {
            if (rumble.clip == null) rumble.clip = ProceduralAudio.Rumble();
            Sounds.Apply(rumble, "Silo Fight/Blood flood rumble");
            rumble.loop = true;
            rumble.Play();
        }
        onBegin.Invoke();
        foreach (var sv in Survivor.All) if (sv != null && sv.Turned) sv.gameObject.SetActive(false);   // (onBegin shows them all)
        BringColleagues();
        Say(MayaHere ? clingLine : clingLineSam);
    }

    bool MayaHere
    {
        get
        {
            foreach (var sv in Survivor.All)
                if (sv != null && sv.displayName == "Maya" && sv.gameObject.activeInHierarchy && !sv.Drowned) return true;
            return false;
        }
    }

    // Maya and Sam slide down into the blood beside you and hold on while you swim them up
    void BringColleagues()
    {
        int i = 0;
        foreach (var sv in Survivor.All)
        {
            if (sv == null || !sv.gameObject.activeInHierarchy || sv.Drowned || !InSilo(sv.transform.position)) continue;
            Transform p = player.transform;
            Vector3 fwd = p.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            Vector3 at = p.position + side * (i % 2 == 0 ? 1.3f : -1.3f) - fwd * 1.4f;
            at.y = startY - 0.3f;
            sv.transform.position = at;
            sv.transform.rotation = Quaternion.LookRotation(fwd);
            sv.Clinging = true;
            i++;
        }
    }

    // back to the silo floor after drowning: the blood drains and comes again
    public void ResetFlood()
    {
        if (!running) return;
        t = 0f;
        air = 1f;
        emptyFor = 0f;
        lowFor = 0f;
        drowned = warned = false;
        surface.position = new Vector3(surface.position.x, startY, surface.position.z);
        StopAllCoroutines();
        if (cutscene != null) cutscene.Abort();
        if (risenLost != null) Destroy(risenLost);
        if (risenDamned != null) risenDamned.ResetRisen();
        damnedPending = null;
        foreach (var sv in Survivor.All)
            if (sv != null && !sv.Turned && InSilo(sv.transform.position)) { sv.gameObject.SetActive(true); sv.ResetToStart(); }
        BringColleagues();
        if (rumble != null) { rumble.volume = 1f; if (!rumble.isPlaying) rumble.Play(); }
        SetSwimming(false);
    }

    void Update()
    {
        if (!running) return;
        t += Time.deltaTime;
        float y = Mathf.Lerp(startY, endY, Mathf.SmoothStep(0f, 1f, t / riseTime));
        surface.position = new Vector3(surface.position.x, y, surface.position.z);
        if (rumble != null && t > riseTime) rumble.volume = Mathf.MoveTowards(rumble.volume, 0f, 0.3f * Time.deltaTime);

        FloatSurvivors();
        if (!swimming && t > riseTime) LetGo(); else outFor = 0f;
        if (damnedPending != null && !swimming && t > riseTime && outFor > 1.5f && (cutscene == null || !cutscene.Playing)) DamnedRises();
        if (!swimming || drowned || holding) return;

        // the kicks: read here (key presses are per frame), used in FixedUpdate
        if (Input.GetKeyDown(KeyCode.Space)) Kick();
        kickTimer -= Time.deltaTime;

        // the swim gets harder the higher the blood (and the more tired you are)
        float drain = drainPerSecond * Mathf.Lerp(0.8f, 1.25f, Mathf.InverseLerp(startY, endY, SurfaceY));
        air = Mathf.Max(0f, air - drain * Time.deltaTime);

        if (air < 0.3f && !warned) { warned = true; Say(MayaHere ? lowLine : lowLineSam); }
        if (air > 0.6f) warned = false;

        // hold on too long with nothing left and the one behind you lets go
        lowFor = air < 0.2f ? lowFor + Time.deltaTime : Mathf.Max(0f, lowFor - 0.5f * Time.deltaTime);
        if (lowFor >= survivorGrip) { lowFor = -2.5f; LoseOne(); }   // a moment of grace before the next one slips

        emptyFor = air <= 0f ? emptyFor + Time.deltaTime : 0f;
        if (emptyFor >= drownAfter) Drown();

        UpdateUi();
    }

    void FixedUpdate()
    {
        if (!running || player == null) return;

        // only inside the silo: once you climb out and teleport away it must let go of you
        Vector3 off = player.position - surface.position; off.y = 0f;
        float feet = player.position.y - player.transform.lossyScale.y;   // capsule half-height at the player's scale
        bool inBlood = off.magnitude < radius && feet < SurfaceY - 0.2f;
        if (inBlood != swimming) SetSwimming(inBlood);
        if (!inBlood) return;

        // where the air puts your eyes: at the surface when it's full, sinkDepth under when it's empty
        float rest = head != null ? eyesAbove - (head.position.y - player.position.y) : floatOffset;
        float sink = sinkDepth * SinkFraction;
        float target = SurfaceY + rest - sink + (kickTimer > 0f ? kickLift : 0f);
        Vector3 v = player.linearVelocity;
        v.y = Mathf.Clamp((target - player.position.y) * buoyancy, -2f, 5f);
        player.linearVelocity = v;
    }

    // one stroke (Space; also callable from a gamepad button or a test)
    public void Kick()
    {
        if (!swimming || drowned) return;
        air = Mathf.Min(1f, air + perKick);
        kickTimer = 0.18f;
        Sounds.OneShot(sfx, "Silo Fight/Swim stroke", null, Random.Range(0.5f, 0.8f));
    }

    bool InSilo(Vector3 p) { Vector3 d = p - surface.position; d.y = 0f; return d.magnitude < radius; }

    // how far under the air bar drags you (0 full .. 1 empty)
    float SinkFraction => drowned ? 1.5f : 1f - Mathf.SmoothStep(0f, 1f, air / 0.55f);

    // anyone the blood has reached treads it: the ones following you go under with you when you tire
    void FloatSurvivors()
    {
        foreach (var sv in Survivor.All)
        {
            if (sv == null || !sv.gameObject.activeInHierarchy || sv.Drowned || !InSilo(sv.transform.position)) continue;
            if (sv.Clinging || SurfaceY > sv.GroundY + survivorHead - 0.25f)
                sv.FloatAt = SurfaceY - survivorHead - ((sv.Following || sv.Clinging) && swimming ? SinkFraction * sinkDepth * 0.8f : 0f);
            else
                sv.FloatAt = null;
        }
    }

    // out at the top: each one climbs onto the first step they reach (they keep paddling after you till then);
    // then it's up to you whether they come along
    void LetGo()
    {
        outFor += Time.deltaTime;
        foreach (var sv in Survivor.All)
        {
            if (sv == null || !sv.Clinging || !sv.gameObject.activeInHierarchy) continue;
            Vector3 feet = sv.transform.position;
            if (StepAt(feet, out float y)) { Land(sv, new Vector3(feet.x, y, feet.z)); continue; }
            if (outFor < 2f) continue;
            // nothing under them (still over the shaft): they haul themselves out next to you
            Vector3 side = feet - player.position; side.y = 0f;
            Vector3 at = player.position + (side.sqrMagnitude > 0.01f ? side.normalized : player.transform.right) * 0.9f;
            if (StepAt(at, out y)) Land(sv, new Vector3(at.x, y, at.z));
            else Land(sv, new Vector3(player.position.x, player.position.y - player.transform.lossyScale.y, player.position.z));
        }
    }

    // a step just above or just below the surface (not the shaft floor far below)
    bool StepAt(Vector3 p, out float y)
    {
        y = 0f;
        float best = float.MaxValue;
        foreach (var hit in Physics.RaycastAll(new Vector3(p.x, SurfaceY + 2.5f, p.z), Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<Survivor>() != null || hit.collider.attachedRigidbody == player) continue;
            if (hit.distance < best) { best = hit.distance; y = hit.point.y; }
        }
        return best < float.MaxValue;
    }

    static void Land(Survivor sv, Vector3 at)
    {
        sv.Clinging = false;
        sv.FloatAt = null;
        sv.transform.position = at;
        sv.GroundY = at.y;
    }

    void LoseOne()
    {
        Survivor last = null;
        foreach (var sv in Survivor.All)
            if (sv != null && sv.gameObject.activeInHierarchy && (sv.Following || sv.Clinging) && !sv.Drowned && sv.FloatAt.HasValue) last = sv;
        if (last == null) return;
        last.Drown();
        bool maya = last.displayName == "Maya";
        Say(maya ? mayaDrownLine : samDrownLine);
        var clips = Sounds.Clips("Silo Fight/Survivor drowning", null);
        if (clips != null && clips.Length > 0)
        {
            var c = clips[Mathf.Min(maya ? 0 : 1, clips.Length - 1)];
            if (c != null) sfx.PlayOneShot(c, Sounds.Volume("Silo Fight/Survivor drowning"));
        }
        StartCoroutine(GoUnder(last));
    }

    System.Collections.IEnumerator GoUnder(Survivor sv)
    {
        float from = sv.FloatAt ?? sv.transform.position.y;
        for (float k = 0f; k < 2.2f; k += Time.deltaTime)
        {
            sv.FloatAt = Mathf.Lerp(from, SurfaceY - survivorHead - 2.8f, k / 2.2f) + Mathf.Sin(k * 18f) * 0.05f * (1f - k / 2.2f);
            yield return null;
        }
        sv.FloatAt = null;
        sv.gameObject.SetActive(false);
        yield return new WaitForSeconds(1.5f);

        // dead twice: they don't get to leave
        if (sv.ifKilled == Survivor.Fate.Lost)
        {
            if (cutscene != null) cutscene.StartCoroutine(cutscene.Lost(sv, () => LostRises(sv, false)));
            else LostRises(sv, true);
        }
        else damnedPending = sv;   // it comes out onto the stairs once you're out of the blood
    }

    GameObject LostRises(Survivor sv, bool say)
    {
        if (sv.animator == null || ghostMaterial == null) return null;
        var body = Instantiate(sv.animator.gameObject);
        body.name = sv.displayName + " (the Lost)";
        foreach (var c in body.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(c);
        foreach (var c in body.GetComponentsInChildren<Collider>(true)) Destroy(c);
        body.transform.localScale = sv.animator.transform.lossyScale;
        foreach (var r in body.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = ghostMaterial;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var an = body.GetComponent<Animator>();
        if (an != null)
        {
            if (lostController != null) an.runtimeAnimatorController = lostController;
            else an.SetFloat("Speed", 0f);
            an.applyRootMotion = false;
        }
        var root = new GameObject(body.name);
        root.transform.position = new Vector3(sv.transform.position.x, SurfaceY - 1.2f, sv.transform.position.z);
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        var glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = Vector3.up * 1.2f;
        glow.color = new Color(0.5f, 0.85f, 1f);
        glow.intensity = 1.4f;
        glow.range = 6f;
        var rl = root.AddComponent<RisenLost>();
        rl.flood = this;
        rl.watch = player.transform;
        risenLost = root;
        if (say) Say(string.IsNullOrEmpty(sv.turnedLine) ? lostLine : sv.turnedLine);
        return root;
    }

    // onto a step a few metres from you, as near the blood as possible: it has just crawled out of it
    void DamnedRises()
    {
        var sv = damnedPending;
        damnedPending = null;
        if (risenDamned == null) return;
        Vector3 c = surface.position, best = Vector3.zero;
        float bestY = float.MaxValue;
        for (int a = 0; a < 72; a++)
            for (float r = 3.5f; r < radius; r += 0.5f)
            {
                Vector3 o = c + Quaternion.Euler(0f, a * 5f, 0f) * Vector3.forward * r;
                o.y = SurfaceY + 1.5f;
                if (!Physics.Raycast(o, Vector3.down, out RaycastHit hit, 2.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.attachedRigidbody == player || hit.collider.GetComponentInParent<Survivor>() != null) continue;
                if (hit.point.y < SurfaceY - 0.1f) continue;
                Vector3 d = hit.point - player.position; d.y = 0f;
                if (d.magnitude < 4f || d.magnitude > 9f) continue;
                if (hit.point.y < bestY) { bestY = hit.point.y; best = hit.point; }
            }
        if (bestY == float.MaxValue) return;
        if (cutscene != null) { cutscene.StartCoroutine(cutscene.Damned(sv, best, risenDamned)); return; }
        risenDamned.Rise(best);
        Say(string.IsNullOrEmpty(sv.turnedLine) ? damnedLine : sv.turnedLine);
    }

    public static bool Swimming { get; private set; }   // (the gun is put away while you swim)

    void OnDisable() { if (swimming) Swimming = false; }

    void SetSwimming(bool on)
    {
        swimming = on;
        Swimming = on;
        player.useGravity = !on;
        if (inBloodOverlay != null) inBloodOverlay.SetActive(on);
        if (airBar != null) airBar.SetActive(on && !drowned);
        if (!on && underTint != null) underTint.alpha = 0f;
        if (on && !told) { told = true; Say(firstLine); }
    }

    void UpdateUi()
    {
        if (airFill != null) airFill.localScale = new Vector3(air, 1f, 1f);
        if (airFillImage != null)
        {
            Color ok = new Color(0.92f, 0.9f, 0.85f), bad = new Color(0.85f, 0.05f, 0.05f);
            Color c = Color.Lerp(bad, ok, Mathf.InverseLerp(0.15f, 0.6f, air));
            if (air < 0.25f && Mathf.Repeat(Time.time * 4f, 1f) < 0.5f) c *= 0.55f;   // flashing when it's nearly gone
            c.a = 1f;
            airFillImage.color = c;
        }
        // eyes going under: the view thickens to red
        if (underTint != null && head != null)
            underTint.alpha = Mathf.Clamp01((SurfaceY - head.position.y + 0.1f) / 0.6f) * 0.85f;
    }

    void Drown()
    {
        drowned = true;
        if (airBar != null) airBar.SetActive(false);
        Sounds.OneShot(sfx, "Silo Fight/Drowning", null, 1f);
        if (health != null) health.TakeDamage(health.currentHealth + 1f);
    }

    static void Say(string s) { if (!string.IsNullOrEmpty(s) && DialogueBox.Instance != null) DialogueBox.Instance.SayNow(s); }
}
