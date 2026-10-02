using UnityEngine;
using UnityEngine.UI;

// The van's state while you drive, as a small picture of the van where your heart sits on foot: no bar, no number.
// It goes through the same stages as the van's own paint (VanDamageLook): as new, scuffed, rusted and cracked,
// scorched and smoking, burnt out. It jolts when the van takes a hit and rattles once it's nearly gone.
// Shown and hidden with the object it's on (CarHealthUI switches the gauge on while you're driving).
public class VanHealthUI : MonoBehaviour
{
    public CarHealth health;
    public RectTransform icon;              // moved and scaled by the jolt
    public Image face;
    public Sprite[] stages;                 // 0 = as new ... last = wrecked
    public float[] below = { 0.8f, 0.6f, 0.4f, 0.2f };   // health fraction under which stage 1, 2, 3, 4 shows (as VanDamageLook)

    private float lastHealth, jolt;
    private Vector2 home;
    private bool homed;

    void OnEnable()
    {
        if (icon != null && !homed) { home = icon.anchoredPosition; homed = true; }
        if (health != null) lastHealth = health.currentHealth;
        jolt = 0f;
    }

    void Update()
    {
        if (health == null || icon == null || face == null || stages == null || stages.Length == 0) return;
        float k = health.maxHealth > 0f ? Mathf.Clamp01(health.currentHealth / health.maxHealth) : 1f;
        int stage = 0;
        for (int i = 0; i < below.Length; i++) if (k < below[i]) stage = i + 1;
        var look = stages[Mathf.Min(stage, stages.Length - 1)];
        if (look != null && face.sprite != look) face.sprite = look;

        if (health.currentHealth < lastHealth - 0.01f) jolt = 1f;
        lastHealth = health.currentHealth;
        jolt = Mathf.MoveTowards(jolt, 0f, Time.unscaledDeltaTime * 3.5f);

        bool wrecked = k <= 0f;
        Vector2 shake = jolt > 0f ? Random.insideUnitCircle * 8f * jolt : Vector2.zero;
        if (k < below[below.Length - 1] && !wrecked) shake += Random.insideUnitCircle * 1.4f;   // rattling apart
        icon.anchoredPosition = home + shake;
        icon.localScale = Vector3.one * (1f + 0.14f * jolt);
        face.color = wrecked ? new Color(0.5f, 0.5f, 0.5f) : Color.Lerp(Color.white, new Color(1f, 0.45f, 0.45f), jolt);
    }
}
