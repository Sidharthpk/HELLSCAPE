using UnityEngine;
using UnityEngine.AI;

public class Zombie : MonoBehaviour
{
    public int health = 30;
    public GameObject bloodSplashPrefab; // optional; a code-built spray is used if empty
    public Material bloodMaterial;       // optional; used by the code-built spray
    public Rigidbody[] ragdollBones;
    public Collider mainCollider;
    public Animator animator;

    [Header("Hit Sounds")]
    public AudioClip[] punchHitSounds;
    public AudioClip[] carHitSounds;
    public float hitVolume = 1f;

    [Header("Moaning")]
    public AudioClip[] moanSounds;
    public Vector2 moanInterval = new Vector2(4f, 10f);
    public float moanVolume = 0.6f;
    public float moanMaxDistance = 25f;
    public Vector2 moanLength = new Vector2(3f, 5.5f);   // play a snippet of the clip, not the whole thing
    public float moanFade = 0.8f;                        // fade in/out time for each snippet
    public float globalMoanGap = 3f;                     // only one zombie in the whole horde moans per gap

    private static float nextGlobalMoan;

    private NavMeshAgent agent;
    private ZombieAI ai;
    private AudioSource voice;
    private float moanTimer;
    private bool isDead = false;
    public bool IsDead => isDead;

    void Start()
    {
        moanSounds = Sounds.Clips("Hell City/Zombie moans", moanSounds);
        punchHitSounds = Sounds.Clips("Hell City/Punch hits", punchHitSounds);
        carHitSounds = Sounds.Clips("Hell City/Car hits a zombie", carHitSounds);
        agent = GetComponent<NavMeshAgent>();
        ai = GetComponent<ZombieAI>();
        SetRagdoll(false);
        CarZombieHit.IgnoreCar(this);   // the car runs zombies over through a trigger, never by solid contact

        voice = GetComponent<AudioSource>();
        if (voice == null) voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.maxDistance = moanMaxDistance;

        // random first delay so a fresh wave doesn't moan in unison
        moanTimer = Random.Range(0f, moanInterval.y);
    }

    void Update()
    {
        if (isDead || moanSounds.Length == 0) return;

        moanTimer -= Time.deltaTime;
        if (moanTimer > 0f) return;
        moanTimer = Random.Range(moanInterval.x, moanInterval.y);

        // stale value from a previous play session (static survives when domain reload is off)
        if (nextGlobalMoan - Time.time > globalMoanGap) nextGlobalMoan = 0f;
        if (Time.time < nextGlobalMoan) return;

        Camera cam = Camera.main;
        if (cam == null || Vector3.Distance(cam.transform.position, transform.position) > moanMaxDistance) return;

        nextGlobalMoan = Time.time + globalMoanGap;
        AudioClip clip = RandomClip(moanSounds);
        float len = Mathf.Min(Random.Range(moanLength.x, moanLength.y), clip.length);
        StartCoroutine(Moan(clip, len));
    }

    // snippet of the moan clip with a soft fade in and out, so it never cuts off abruptly
    System.Collections.IEnumerator Moan(AudioClip clip, float len)
    {
        voice.clip = clip;
        voice.pitch = Random.Range(0.85f, 1.15f);
        voice.time = Random.Range(0f, clip.length - len);
        voice.volume = 0f;
        voice.Play();

        float fade = Mathf.Min(moanFade, len * 0.4f);
        for (float t = 0f; t < len && !isDead; t += Time.deltaTime)
        {
            float k = Mathf.Min(t / fade, (len - t) / fade, 1f);
            voice.volume = moanVolume * Mathf.Clamp01(k);
            yield return null;
        }
        voice.Stop();
    }

    // it's seen you: a long, low, loud howl (its moan, dragged down), heard from further off than the moans
    public void Scream()
    {
        if (isDead || voice == null || moanSounds == null || moanSounds.Length == 0) return;
        StopAllCoroutines();
        bool own = Sounds.Has("Hell City/Zombie scream");
        AudioClip clip = own ? Sounds.Clip("Hell City/Zombie scream", null) : RandomClip(moanSounds);
        voice.maxDistance = moanMaxDistance * 1.6f;
        voice.clip = clip;
        voice.pitch = own ? Random.Range(0.92f, 1.08f) : Random.Range(0.6f, 0.78f);   // the moan, dragged down, if there's no scream of your own
        voice.time = own ? 0f : Random.Range(0f, Mathf.Max(0f, clip.length - 2.5f));
        voice.volume = Sounds.Volume("Hell City/Zombie scream");
        voice.Play();
        nextGlobalMoan = Time.time + globalMoanGap;   // no idle moans talking over it
        StartCoroutine(StopAfter(2.4f));
    }

    System.Collections.IEnumerator StopAfter(float t)
    {
        yield return new WaitForSeconds(t);
        for (float k = 1f; k > 0f; k -= Time.deltaTime / 0.4f) { voice.volume = k; yield return null; }
        voice.Stop();
    }

    public void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitForce)
    {
        if (isDead) return;

        health -= amount;
        if (ai != null) ai.Alert();   // hit: it knows where you are now, and so do the ones around it
        SpawnBlood(hitPoint, hitForce, 15, 3f);
        PlayHitSound(punchHitSounds, hitPoint);

        if (health <= 0)
            Die(hitForce, hitPoint);
    }

    // car hits: bigger spray, heavier sound
    public void KillInstant(Vector3 hitPoint, Vector3 hitForce)
    {
        if (isDead) return;
        SpawnBlood(hitPoint, hitForce, 60, 7f);
        PlayHitSound(carHitSounds, hitPoint);
        Die(hitForce, hitPoint);
    }

    void Die(Vector3 force, Vector3 hitPoint)
    {
        isDead = true;

        if (ai != null) ai.enabled = false;
        if (agent != null) agent.enabled = false;
        if (voice != null) voice.Stop();

        SetRagdoll(true);
        CarZombieHit.IgnoreCar(this);   // enabling the ragdoll's colliders reset the ignore: bodies must not snag the car

        Rigidbody closest = null;
        float minDist = Mathf.Infinity;
        foreach (var rb in ragdollBones)
        {
            float d = Vector3.Distance(rb.position, hitPoint);
            if (d < minDist) { minDist = d; closest = rb; }
        }
        if (closest != null)
            closest.AddForce(force, ForceMode.Impulse);

        Destroy(gameObject, 6f);
    }

    void SetRagdoll(bool state)
    {
        animator.enabled = !state;
        mainCollider.enabled = !state;
        foreach (var rb in ragdollBones)
        {
            rb.isKinematic = !state;
            rb.GetComponent<Collider>().enabled = state;
        }
    }

    void PlayHitSound(AudioClip[] clips, Vector3 point)
    {
        if (clips.Length > 0)
            AudioSource.PlayClipAtPoint(RandomClip(clips), point, hitVolume);
    }

    AudioClip RandomClip(AudioClip[] clips) => clips[Random.Range(0, clips.Length)];

    void SpawnBlood(Vector3 point, Vector3 direction, int count, float speed)
    {
        Quaternion rot = direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction) : Quaternion.identity;

        if (bloodSplashPrefab != null)
        {
            Instantiate(bloodSplashPrefab, point, rot);
            return;
        }

        BloodFx.Spray(point, direction, count, speed, bloodMaterial);
    }
}
