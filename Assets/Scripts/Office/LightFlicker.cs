using UnityEngine;

// Dying office fluorescent: steady, then a stutter of on/off.
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    public Vector2 calmTime = new Vector2(2f, 7f);
    public Vector2 burstTime = new Vector2(0.1f, 0.5f);
    public float minBrightness = 0.05f;

    private Light lamp;
    private float baseIntensity;
    private float timer;
    private bool bursting;

    void Awake()
    {
        lamp = GetComponent<Light>();
        baseIntensity = lamp.intensity;
        timer = Random.Range(calmTime.x, calmTime.y);
    }

    // dim (or brighten) the lamp for good: it flickers around the new level (InteriorDarkness.lightDim)
    public void ScaleBase(float k)
    {
        if (lamp == null) Awake();
        baseIntensity *= k;
        lamp.intensity *= k;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            bursting = !bursting;
            timer = bursting ? Random.Range(burstTime.x, burstTime.y) : Random.Range(calmTime.x, calmTime.y);
            if (!bursting) lamp.intensity = baseIntensity;
        }
        if (bursting)
            lamp.intensity = baseIntensity * (Random.value < 0.5f ? minBrightness : Random.Range(0.5f, 1f));
    }
}
