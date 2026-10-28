using UnityEngine;

// A fire's light: restless flicker around the intensity it was set to.
[RequireComponent(typeof(Light))]
public class FireFlicker : MonoBehaviour
{
    public float depth = 0.45f;   // how far it dips below and rises above the base level
    public float speed = 11f;

    public float wander = 2.5f;   // the light drifts about this far through the crown, so the glow on the walls licks and moves

    Light lamp;
    float baseIntensity, seed;
    Vector3 basePos;

    void Awake()
    {
        lamp = GetComponent<Light>();
        baseIntensity = lamp.intensity;
        basePos = transform.localPosition;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float t = Time.time * speed;
        float n = Mathf.PerlinNoise(t, seed) * (0.6f + 0.4f * Mathf.PerlinNoise(t * 2.7f, seed + 9f));
        lamp.intensity = baseIntensity * (1f - depth + 2f * depth * n);
        float w = Time.time * speed * 0.35f;
        transform.localPosition = basePos + wander * new Vector3(
            Mathf.PerlinNoise(w, seed + 20f) - 0.5f, 0.6f * (Mathf.PerlinNoise(w, seed + 40f) - 0.5f), Mathf.PerlinNoise(w, seed + 60f) - 0.5f) * 2f;
    }
}
