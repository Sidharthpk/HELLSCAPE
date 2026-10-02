using UnityEngine;
using UnityEngine.UI;

// Your health, as a heart in the corner of the screen instead of a bar and a number. It beats (lub-dub) calmly
// when you're whole and faster and faster the more hurt you are; it cracks at two thirds and again at one third,
// flinches when you're hit, trembles when you're nearly gone, and stops, grey, when you die.
public class HeartbeatUI : MonoBehaviour
{
    public PlayerHealth health;
    public RectTransform heart;             // scaled by the beat
    public Image face;                      // the heart itself
    public Image glow;                      // the same shape behind it, pulsing
    public Sprite healthy, hurt, critical;
    public float calmBpm = 62f;             // at full health
    public float panicBpm = 178f;           // at death's door
    [Range(0f, 1f)] public float hurtBelow = 0.66f;
    [Range(0f, 1f)] public float criticalBelow = 0.33f;
    public float beatSize = 0.2f;           // how much it swells on the beat

    private float phase, lastHealth, flinch;
    private Vector2 home;
    private bool homed;

    void OnEnable()
    {
        if (heart != null && !homed) { home = heart.anchoredPosition; homed = true; }
        if (health != null) lastHealth = health.currentHealth;
    }

    void Update()
    {
        if (health == null || heart == null || face == null) return;
        float k = Mathf.Clamp01(health.currentHealth / Mathf.Max(1f, health.maxHealth));
        var look = k < criticalBelow ? critical : k < hurtBelow ? hurt : healthy;
        if (look != null && face.sprite != look) { face.sprite = look; if (glow != null) glow.sprite = look; }

        if (health.currentHealth < lastHealth - 0.01f) flinch = 1f;
        lastHealth = health.currentHealth;
        flinch = Mathf.MoveTowards(flinch, 0f, Time.deltaTime * 3.5f);

        float beat = 0f;
        if (!health.IsDead)
        {
            float bpm = Mathf.Lerp(calmBpm, panicBpm, Mathf.Pow(1f - k, 0.8f));
            phase = Mathf.Repeat(phase + bpm / 60f * Time.deltaTime, 1f);
            beat = Bump(phase, 0.07f, 0.055f) + 0.6f * Bump(phase, 0.27f, 0.06f);   // lub... dub
        }

        heart.localScale = Vector3.one * (1f + beatSize * beat + 0.18f * flinch);
        Vector2 shake = flinch > 0f ? Random.insideUnitCircle * 7f * flinch : Vector2.zero;
        if (k < criticalBelow && !health.IsDead) shake += Random.insideUnitCircle * 1.6f;
        heart.anchoredPosition = home + shake;
        face.color = health.IsDead ? new Color(0.42f, 0.42f, 0.42f) : Color.Lerp(Color.white, new Color(1f, 0.55f, 0.55f), flinch);
        if (glow != null)
        {
            glow.color = new Color(1f, 0.1f, 0.1f, health.IsDead ? 0f : 0.1f + 0.34f * beat);
            glow.rectTransform.localScale = Vector3.one * (1.25f + 0.3f * beat);
        }
    }

    static float Bump(float x, float at, float width)
    {
        float d = (x - at) / width;
        return Mathf.Exp(-d * d);
    }
}
