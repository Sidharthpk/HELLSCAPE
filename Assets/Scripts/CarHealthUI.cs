using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CarHealthUI : MonoBehaviour
{
    public CarHealth carHealth;
    public Slider healthSlider;
    public TextMeshProUGUI healthText;   // optional, e.g. "CAR 75%"; put it inside the slider so it hides with it
    public string format = "CAR {0}%";
    public CarInteract carInteract;      // auto-found if left empty
    public GameObject[] showWhileDriving; // e.g. the speedometer text, hidden on foot

    [Header("Hit Flash")]
    public Color normalColor = Color.white;
    public Color hitColor = Color.red;
    public float flashTime = 0.4f;

    private float lastHealth;
    private float flashTimer;

    void Start()
    {
        if (carInteract == null) carInteract = FindFirstObjectByType<CarInteract>();
        if (carHealth != null) lastHealth = carHealth.maxHealth;
    }

    void Update()
    {
        if (carHealth == null || healthSlider == null) return;

        // only show the bar while driving
        bool driving = carInteract != null && carInteract.InCar && !TitleScreen.Showing;
        if (healthSlider.gameObject.activeSelf != driving)
        {
            healthSlider.gameObject.SetActive(driving);
            foreach (var go in showWhileDriving)
                if (go != null) go.SetActive(driving);
        }

        float percent = carHealth.currentHealth / carHealth.maxHealth * 100f;
        healthSlider.value = percent;

        if (healthText != null)
        {
            if (carHealth.currentHealth < lastHealth) flashTimer = flashTime;
            flashTimer -= Time.deltaTime;

            healthText.text = string.Format(format, Mathf.CeilToInt(percent));
            healthText.color = flashTimer > 0f ? hitColor : normalColor;
        }
        lastHealth = carHealth.currentHealth;
    }
}
