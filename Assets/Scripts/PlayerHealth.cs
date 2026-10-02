using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;

    public DamageVignette vignette;
    public GameOverScreen gameOver;

    private bool isDead = false;

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

        currentHealth -= amount;

        if (vignette != null)
            vignette.SetDamage(1f - (currentHealth / maxHealth));

        if (currentHealth <= 0f)
        {
            isDead = true;
            if (gameOver != null) gameOver.Show();
        }
    }
}
