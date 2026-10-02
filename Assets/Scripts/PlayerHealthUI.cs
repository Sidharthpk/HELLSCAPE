using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public Slider healthSlider;
    public TMPro.TextMeshProUGUI healthText;   // optional, e.g. "75"

    void Update()
    {
        if (playerHealth == null || healthSlider == null) return;

        // player object is disabled while driving, so hide the bar with it; and never over the title screen
        bool onFoot = playerHealth.gameObject.activeInHierarchy && !TitleScreen.Showing;
        if (healthSlider.gameObject.activeSelf != onFoot)
            healthSlider.gameObject.SetActive(onFoot);

        healthSlider.value = Mathf.Max(0f, playerHealth.currentHealth) / playerHealth.maxHealth * 100f;
        if (healthText != null) healthText.text = Mathf.CeilToInt(healthSlider.value) + "%";
    }
}
