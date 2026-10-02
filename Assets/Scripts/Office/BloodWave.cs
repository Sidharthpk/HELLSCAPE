using System.Collections;
using UnityEngine;

// The giant orb of blood that chases you from the office to the bridge (it used to be a wall: the old wall parts
// are hidden when an orb is set). Begin() when you drive off with the keys (CarInteract.onEnter); Stop() from a
// StoryTrigger at the bridge.
// It opens on a letterboxed cutscene in three shots - the street shaking and glowing red behind the car, the orb
// heaving up out of it, then round behind the car with it looming over the rooftops - and a fade back to the
// car, where the music (Sound Manager area "Blood Orb") swells in. Then it rubber-bands: fast when you're far so
// it never drops away, but close up it can only gain a little on the car, so a clean run escapes and it only
// catches you if you stop, crash or turn back. At the bridge it sinks into the river and the music fades out.
public class BloodWave : MonoBehaviour
{
    public Transform car;
    public Transform playerRoot;
    [UnityEngine.Serialization.FormerlySerializedAs("bridge")]
    public Transform awayFrom;               // it spawns on the far side of the car from this: the way the road leaves
    public PlayerHealth health;

    public float startDistance = 70f;
    public float minStartDistance = 40f;
    public float cutsceneFog = 0.004f;       // the city's red fog thinned while the camera rolls, so the orb reads
    public float startDelay = 2.5f;          // after the cutscene: time to get the car moving
    public float nearSpeed = 10f;            // speed when right behind you
    public float farSpeed = 22f;             // speed when you've pulled away
    public float farDistance = 120f;
    public float closeDistance = 45f;        // inside this it moves at most gainOnCar faster than the car
    public float gainOnCar = 2.5f;
    public float catchDistance = 7f;
    public AudioSource roar;                 // procedural rumble if it has no clip

    [Header("The orb")]
    public Transform orb;                    // the model (child); rolls as it moves and hovers orbRadius over the street
    public float orbRadius = 18f;
    public GameObject[] hideWhenOrb;         // the old wall of blood
    public Light orbGlow;                    // red light under it (optional)

    [Header("Intro cutscene")]
    public Camera cutsceneCam;               // disabled when idle
    public RectTransform barTop, barBottom;  // letterbox (shared with the killer cutscenes)
    public float barHeight = 90f;
    public Behaviour carControl;             // frozen while the camera rolls
    public ScreenFader fader;
    public float introTime = 3.4f;           // (kept for old scenes; the shots below set the length)
    [TextArea] public string introLine = "The street... it's SHAKING--|What is THAT--";
    public string musicArea = "Blood Orb";   // Sound Manager area: its music swells in once the cutscene ends
    public float musicFadeIn = 4f;
    public float musicFadeOut = 5f;

    private bool running, intro;
    private float startedAt, rolled;
    private Rigidbody carRb;
    private Vector3 lastCar, lastPos;
    private float carSpeed;
    private Vector3 baseScale;
    private float fog0 = -1f;

    void Awake()
    {
        baseScale = transform.localScale;
        if (orb != null) orbScale = orb.localScale.x;
        if (cutsceneCam != null) cutsceneCam.enabled = false;
        if (orb != null && hideWhenOrb != null) foreach (var g in hideWhenOrb) if (g != null) g.SetActive(false);
    }

    // keep the GameObject disabled in the scene; Begin() switches it on
    public void Begin()
    {
        if (running) return;
        running = true;

        // behind the car (away from the road ahead), down whichever street gives the longest clear view, so the
        // cutscene can see it come up (never behind a block of flats)
        Vector3 back = Flat(car.position - awayFrom.position).normalized;
        Vector3 eye = car.position + Vector3.up * 3f;
        Vector3 away = back;
        float bestScore = float.MinValue, clearOf = 0f;
        for (int a = 0; a < 360; a += 10)
        {
            Vector3 d = Quaternion.Euler(0f, a, 0f) * back;
            float clear = Physics.SphereCast(eye, 2f, d, out RaycastHit h, startDistance, ~0, QueryTriggerInteraction.Ignore) ? h.distance : startDistance;
            float score = Mathf.Min(clear, startDistance) - 0.25f * Mathf.Abs(Mathf.DeltaAngle(0f, a));   // clear view first, then behind you
            if (score > bestScore) { bestScore = score; away = d; clearOf = clear; }
        }
        float spawn = Mathf.Clamp(clearOf - orbRadius - 3f, minStartDistance, startDistance);   // in the open street, not in the block at its end
        transform.position = new Vector3(car.position.x, car.position.y - 1f, car.position.z) + away * spawn;
        gameObject.SetActive(true);
        transform.rotation = Quaternion.LookRotation(Flat(car.position - transform.position));
        lastPos = transform.position;
        carRb = car.GetComponent<Rigidbody>();

        if (roar != null)
        {
            if (roar.clip == null) roar.clip = ProceduralAudio.Rumble();
            Sounds.Apply(roar, "Escape/Blood wave roar");
            roar.loop = true;
            roar.volume = 0f;
            roar.Play();
        }
        StartCoroutine(Intro());
    }

    // ---------------------------------------------------------------- the cutscene

    IEnumerator Intro()
    {
        intro = true;
        bool hadControl = carControl != null && carControl.enabled;
        if (carControl != null) carControl.enabled = false;
        bool wasKinematic = carRb != null && carRb.isKinematic;
        if (carRb != null) { carRb.linearVelocity = Vector3.zero; carRb.angularVelocity = Vector3.zero; carRb.isKinematic = true; }   // not an inch while the camera rolls
        MusicManager.Stop(1.2f);

        // cut in from black, so there's no jump from the car camera
        if (fader != null) yield return fader.FadeTo(1f, 0.3f);
        if (cutsceneCam != null) cutsceneCam.enabled = true;
        SetBars(1f);
        fog0 = RenderSettings.fogDensity;
        RenderSettings.fogDensity = Mathf.Min(fog0, cutsceneFog);
        SetRise(0f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(introLine);

        Vector3 c = car.position;
        Vector3 toOrb = Flat(transform.position - c).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, toOrb);
        Vector3 ground = transform.position;
        float full = OrbTop();

        // shot 1: low beside the car, looking back down the street. It shakes; a red glow wells up out of the road.
        Vector3 s1 = SafeCam(c + Vector3.up * 1.5f, c + side * 3.2f - toOrb * 1.2f + Vector3.up * 1.1f);
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.35f));
        for (float t = 0f; t < 2.2f; t += Time.deltaTime)
        {
            float k = t / 2.2f;
            SetRise(0.02f * k);
            if (orbGlow != null) orbGlow.intensity = Mathf.Lerp(0f, 8f, k);
            if (roar != null) roar.volume = Mathf.Lerp(0f, 0.6f, k) * Sounds.Volume("Escape/Blood wave roar");
            Place(s1 + Random.insideUnitSphere * 0.03f * k, ground + Vector3.up * 4f);
            yield return null;
        }

        // shot 2: further back and higher, the orb heaving up out of the street, the camera tilting up with it
        Vector3 s2a = SafeCam(c + Vector3.up * 1.5f, c - toOrb * 6f + side * 5f + Vector3.up * 2.2f);
        Vector3 s2b = SafeCam(c + Vector3.up * 1.5f, c - toOrb * 9f + side * 6f + Vector3.up * 3.2f);
        for (float t = 0f; t < 3.4f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 3.4f);
            SetRise(Mathf.Lerp(0.02f, 1f, k));
            if (roar != null) roar.volume = Mathf.Lerp(0.6f, 1f, k) * Sounds.Volume("Escape/Blood wave roar");
            Vector3 top = ground + Vector3.up * Mathf.Lerp(3f, full * 0.8f, k);
            Place(Vector3.Lerp(s2a, s2b, k) + Random.insideUnitSphere * 0.06f * (1f - k), top);
            yield return null;
        }

        // shot 3: low behind the car, looking up past it at the orb looming over the street
        Vector3 s3a = SafeCam(c + Vector3.up * 1.5f, c - toOrb * 3.2f + side * 1.4f + Vector3.up * 0.9f);
        Vector3 s3b = SafeCam(c + Vector3.up * 1.5f, c - toOrb * 4.6f + side * 0.6f + Vector3.up * 1.5f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow("DRIVE!");
        for (float t = 0f; t < 2.2f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 2.2f);
            Place(Vector3.Lerp(s3a, s3b, k), ground + Vector3.up * full * 0.45f);
            yield return null;
        }

        // fade out, back to the car, fade in; the music swells in
        if (fader != null) yield return fader.FadeTo(1f, 0.35f);
        if (cutsceneCam != null) cutsceneCam.enabled = false;
        SetBars(0f);
        RenderSettings.fogDensity = fog0;
        if (carRb != null) carRb.isKinematic = wasKinematic;
        if (carControl != null && hadControl) carControl.enabled = true;
        MusicManager.Area(musicArea, musicFadeIn);
        if (fader != null) yield return fader.FadeTo(0f, 0.6f);

        startedAt = Time.time;
        lastCar = new Vector3(car.position.x, 0f, car.position.z);
        carSpeed = 0f;
        intro = false;
    }

    // a camera spot that isn't inside a wall: pulled back towards the anchor if something is in the way
    // (the car itself doesn't count: the low shots sit right beside it)
    Vector3 SafeCam(Vector3 anchor, Vector3 want)
    {
        Vector3 d = want - anchor;
        float nearest = float.MaxValue;
        foreach (var h in Physics.SphereCastAll(anchor, 0.35f, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(car) || h.distance <= 0f) continue;
            if (h.distance < nearest) nearest = h.distance;
        }
        return nearest < float.MaxValue ? anchor + d.normalized * Mathf.Max(0.3f, nearest - 0.3f) : want;
    }

    void Place(Vector3 pos, Vector3 look)
    {
        if (cutsceneCam == null) return;
        cutsceneCam.transform.position = pos;
        cutsceneCam.transform.rotation = Quaternion.LookRotation(look - pos);
    }

    // 0 = still under the street, 1 = fully up (the orb hovering, or the old wall at full height)
    void SetRise(float k)
    {
        if (orb != null)
        {
            orb.localPosition = Vector3.up * Mathf.Lerp(-orbRadius * 1.05f, orbRadius * 0.92f, k) / Mathf.Max(0.0001f, transform.localScale.y);
            orb.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, k) * orbScale;
        }
        else transform.localScale = new Vector3(baseScale.x, baseScale.y * Mathf.Max(0.05f, k), baseScale.z);
    }

    private float orbScale = -1f;
    float OrbTop() => orb != null ? orbRadius * 1.9f : 26f;

    // ---------------------------------------------------------------- the chase

    Transform Target() => playerRoot != null && playerRoot.gameObject.activeInHierarchy ? playerRoot : car;

    void Update()
    {
        if (!running || intro) return;

        Transform target = Target();
        Vector3 to = target.position - transform.position; to.y = 0f;
        float dist = to.magnitude;

        if (dist < catchDistance)
        {
            running = false;
            if (health != null) health.TakeDamage(99999f);
            return;
        }

        // how fast the car really moves (not its rigidbody velocity: pinned against a wall it can read fast and go nowhere)
        Vector3 carFlat = car.position; carFlat.y = 0f;
        if (Time.deltaTime > 0f) carSpeed = Mathf.Lerp(carSpeed, Vector3.Distance(carFlat, lastCar) / Time.deltaTime, 5f * Time.deltaTime);
        lastCar = carFlat;

        if (Time.time - startedAt < startDelay) { FaceTarget(target); return; }
        float speed = Mathf.Lerp(nearSpeed, farSpeed, dist / farDistance);
        if (dist < closeDistance && target == car)
            speed = carSpeed < 3f ? nearSpeed                          // stalled or crashed: it rushes in
                                  : Mathf.Min(speed, carSpeed + gainOnCar);   // moving: it only creeps up on you
        transform.position += to.normalized * speed * Time.deltaTime;
        // keep to the street's height (the road climbs and dips)
        transform.position = new Vector3(transform.position.x, Mathf.Lerp(transform.position.y, car.position.y - 1f, 2f * Time.deltaTime), transform.position.z);
        FaceTarget(target);
        Roll();
    }

    // the orb rolls along the street as it comes
    void Roll()
    {
        if (orb == null) return;
        Vector3 moved = transform.position - lastPos; moved.y = 0f;
        lastPos = transform.position;
        if (moved.sqrMagnitude < 0.000001f) return;
        rolled += moved.magnitude / Mathf.Max(1f, orbRadius) * Mathf.Rad2Deg;
        orb.localRotation = Quaternion.AngleAxis(rolled, Vector3.right);   // forward is +z: rolls toward you
    }

    void FaceTarget(Transform target)
    {
        Vector3 to = target.position - transform.position; to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 2f * Time.deltaTime);
    }

    // a checkpoint respawn: gone again until you drive off
    public void ResetWave()
    {
        running = false;
        if (intro)
        {
            intro = false;
            if (cutsceneCam != null) cutsceneCam.enabled = false;
            SetBars(0f);
            if (carControl != null) carControl.enabled = false;   // CarInteract turns it back on when you get in
            if (carRb != null) carRb.isKinematic = false;
            if (fader != null) fader.fade.alpha = 0f;
            if (fog0 > 0f) RenderSettings.fogDensity = fog0;
        }
        StopAllCoroutines();
        if (roar != null) { roar.Stop(); roar.volume = 1f; }
        transform.localScale = baseScale;
        if (orb != null && orbScale > 0f) { orb.localScale = Vector3.one * orbScale; orb.localPosition = Vector3.up * orbRadius * 0.92f; }
        if (MusicManager.CurrentArea == musicArea) MusicManager.Area("Escape");
        gameObject.SetActive(false);
    }

    // reaches the bridge and sinks into the river; the music fades away
    public void Stop()
    {
        if (!running) return;
        running = false;
        MusicManager.Stop(musicFadeOut);
        StartCoroutine(Collapse());
    }

    IEnumerator Collapse()
    {
        Vector3 s0 = transform.localScale, p0 = transform.position;
        float g0 = orbGlow != null ? orbGlow.intensity : 0f;
        for (float t = 0f; t < 3f; t += Time.deltaTime)
        {
            float k = t / 3f;
            if (orb != null) transform.position = p0 - Vector3.up * orbRadius * 2.2f * k * k;   // down under the water
            else
            {
                transform.localScale = new Vector3(s0.x, s0.y * (1f - k), s0.z);
                transform.position = p0 - Vector3.up * k * 2f;
            }
            if (orbGlow != null) orbGlow.intensity = g0 * (1f - k);
            if (roar != null) roar.volume = 1f - k;
            yield return null;
        }
        gameObject.SetActive(false);
    }

    void SetBars(float k)
    {
        foreach (var b in new[] { barTop, barBottom })
            if (b != null) b.sizeDelta = new Vector2(b.sizeDelta.x, barHeight * k);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v : Vector3.forward; }
}
