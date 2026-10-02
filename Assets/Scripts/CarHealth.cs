using UnityEngine;

public class CarHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Damage Tuning")]
    public float minImpactToDamage = 3f;   // ignore gentle bumps
    public float damageMultiplier = 2f;
    public float maxSingleHitDamage = 25f;
    public float obstacleFullDamageSpeed = 10f; // impact speed at which an obstacle deals its listed damage

    public DamageVignette vignette;
    public CarInteract carInteract; // auto-found if left empty

    [Header("Destroyed Message")]
    public GameObject destroyedMessage;   // e.g. "CAR DESTROYED" text on the Canvas, disabled by default
    public float messageDuration = 3f;

    private bool isDead = false;

    void Start()
    {
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

        // road obstacles always hurt, scaled by speed (a tap is half damage, ramming it is double)
        RoadObstacle obstacle = col.collider.GetComponentInParent<RoadObstacle>();
        if (obstacle != null)
        {
            if (obstacle.TryHit())
                TakeDamage(obstacle.damage * Mathf.Clamp(impact / obstacleFullDamageSpeed, 0.5f, 2f));
            return;
        }

        if (impact < minImpactToDamage) return;
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

        currentHealth = Mathf.Max(0f, currentHealth - dmg);

        if (vignette != null)
            vignette.SetDamage(1f - (currentHealth / maxHealth));

        if (currentHealth <= 0f)
        {
            isDead = true;
            if (destroyedMessage != null) StartCoroutine(ShowDestroyedMessage());
            // wrecked: throw the player out on foot, no getting back in
            if (carInteract != null) carInteract.ForceExit(true);
            if (vignette != null) vignette.SetDamage(0f);
        }
    }

    System.Collections.IEnumerator ShowDestroyedMessage()
    {
        destroyedMessage.SetActive(true);
        yield return new WaitForSeconds(messageDuration);
        destroyedMessage.SetActive(false);
    }
}