using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;

    public DamageVignette vignette;
    public GameOverScreen gameOver;

    [Header("Regeneration")]
    public float regenDelay = 5f;           // seconds without a hit before it starts
    public float regenPerSecond = 2f;       // slow: about 50 s from empty to full

    private bool isDead = false;
    private float lastHit = -999f;

    public bool Regenerating { get; private set; }

    void Update()
    {
        Regenerating = !isDead && currentHealth < maxHealth && Time.time - lastHit >= regenDelay;
        if (Regenerating) Heal(regenPerSecond * Time.deltaTime);
    }

    void Start()
    {
        currentHealth = maxHealth;
        if (vignette != null) vignette.SetDamage(0f);
    }

    public bool IsDead => isDead;

    // back from a checkpoint
    public void Revive(float health)
    {
        isDead = false;
        currentHealth = health;
        if (vignette != null) vignette.SetDamage(1f - (currentHealth / maxHealth));
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        if (vignette != null) vignette.SetDamage(1f - (currentHealth / maxHealth));
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (amount > 0f) lastHit = Time.time;

        if (vignette != null)
            vignette.SetDamage(1f - (currentHealth / maxHealth));

        if (currentHealth <= 0f)
        {
            isDead = true;
            if (gameOver != null) gameOver.Show();
        }
    }
}
