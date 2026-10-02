using UnityEngine;
using UnityEngine.UI;

public class DamageVignette : MonoBehaviour
{
    public Image vignetteImage; // fullscreen red vignette sprite on the Canvas
    public float maxAlpha = 0.75f;
    public float fadeSpeed = 3f;

    [Header("Pulse at low health")]
    public float pulseThreshold = 0.7f; // damage % where pulsing starts
    public float pulseSpeed = 3f;
    public float pulseAmount = 0.15f;

    private float targetAlpha = 0f;
    private float damagePercent = 0f;

    public void SetDamage(float percent)
    {
        damagePercent = Mathf.Clamp01(percent);
        targetAlpha = damagePercent * maxAlpha;
    }

    void Update()
    {
        if (vignetteImage == null) return;

        float alpha = targetAlpha;

        if (damagePercent >= pulseThreshold)
            alpha += Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;

        Color c = vignetteImage.color;
        c.a = Mathf.Lerp(c.a, Mathf.Clamp01(alpha), fadeSpeed * Time.deltaTime);
        vignetteImage.color = c;
    }
}