using UnityEngine;

public class CarHealth : MonoBehaviour
{
    public float maxHealth = 300f;
    public float currentHealth;

    [Header("Damage Tuning")]
    public float minImpactToDamage = 3f;   // ignore gentle bumps
    public float damageMultiplier = 2f;
    public float damageScale = 1f;   // the Demon King's chase turns this down: fleeing him is meant to be survivable
    public float maxSingleHitDamage = 25f;
    public float obstacleFullDamageSpeed = 10f; // impact speed at which an obstacle deals its listed damage

    [Header("Sparks")]
    public GameObject sparks;             // Particle Pack SparksEffect: a burst where the van is hit
    public float sparkGap = 0.2f;         // (the horde tears at it every frame: one burst at most this often)
    private float nextSpark;

    public DamageVignette vignette;
    public CarInteract carInteract; // auto-found if left empty

    [Header("Destroyed Message")]
    public GameObject destroyedMessage;   // e.g. "CAR DESTROYED" text on the Canvas, disabled by default
    public float messageDuration = 3f;

    private bool isDead = false;

    void Start()
    {
        maxHealth = Mathf.Max(maxHealth, 300f);   // (the scene still stores the old 100)
        currentHealth = maxHealth;
        if (destroyedMessage != null) destroyedMessage.SetActive(false);
        if (carInteract == null) carInteract = FindFirstObjectByType<CarInteract>();
        if (vignette != null) vignette.SetDamage(0f);
    }

    void OnCollisionEnter(Collision col)
    {
        if (isDead) return;

        // zombies shouldn't damage the car
        if (col.collider.GetComponentInParent<Zombie>() != null) return;

        float impact = col.relativeVelocity.magnitude;
        if (col.contactCount > 0) { var c = col.GetContact(0); hitNormal = c.normal; }

        // road obstacles always hurt, scaled by speed (a tap is half damage, ramming it is double)
        RoadObstacle obstacle = col.collider.GetComponentInParent<RoadObstacle>();
        if (obstacle != null)
        {
            if (obstacle.TryHit())
            {
                hitPoint = ContactPoint(col);
                TakeDamage(obstacle.damage * Mathf.Clamp(impact / obstacleFullDamageSpeed, 0.5f, 2f));
            }
            return;
        }

        if (impact < minImpactToDamage) return;
        hitPoint = ContactPoint(col);   // (TakeDamage sparks here instead of somewhere on the body)
        TakeDamage(Mathf.Min(impact * damageMultiplier, maxSingleHitDamage));
    }

    public void Repair()
    {
        isDead = false;   // (a checkpoint puts a wrecked car back on the road)
        currentHealth = maxHealth;
        if (vignette != null) vignette.SetDamage(0f);
    }

    public void TakeDamage(float dmg)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - dmg * damageScale);
        Spark();

        if (vignette != null)
            vignette.SetDamage(1f - (currentHealth / maxHealth));

        if (currentHealth <= 0f)
        {
            isDead = true;
            if (destroyedMessage != null) StartCoroutine(ShowDestroyedMessage());
            // wrecked: throw the player out on foot, no getting back in
            if (carInteract != null) carInteract.ForceExit(true);
            if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(HS.S("528-529"));
            if (vignette != null) vignette.SetDamage(0f);
        }
    }

    // at the crash's contact point, or (a claw, an orb, a bolt) somewhere on the bodywork
    private Vector3? hitPoint;
    private Vector3 hitNormal;
    static Vector3? ContactPoint(Collision col) => col.contactCount > 0 ? col.GetContact(0).point : (Vector3?)null;

    void Spark()
    {
        Vector3? at = hitPoint; hitPoint = null;
        if (sparks == null || Time.time < nextSpark) return;
        nextSpark = Time.time + sparkGap;
        Vector3 n = hitNormal;
        if (at == null)
        {
            var b = new Bounds(transform.position, Vector3.one);
            foreach (var r in GetComponentsInChildren<Renderer>()) if (!(r is ParticleSystemRenderer)) b.Encapsulate(r.bounds);
            var p = b.center + Vector3.Scale(b.extents, new Vector3(Random.Range(-1f, 1f), Random.Range(0f, .6f), Random.Range(-1f, 1f)));
            n = (p - b.center).normalized;
            at = p;
        }
        var g = Instantiate(sparks, at.Value, Quaternion.LookRotation(n));
        foreach (var ps in g.GetComponentsInChildren<ParticleSystem>(true)) { var m = ps.main; m.loop = false; }
        Destroy(g, 2.5f);
    }

    System.Collections.IEnumerator ShowDestroyedMessage()
    {
        destroyedMessage.SetActive(true);
        yield return new WaitForSeconds(messageDuration);
        destroyedMessage.SetActive(false);
    }
}