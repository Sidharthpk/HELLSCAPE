using UnityEngine;

// Occasional stutter on a street lamp's fake light (cone, ground pool, glow).
public class LampFlicker : MonoBehaviour
{
    public Vector2 calmTime = new Vector2(1.5f, 6f);   // steady between bursts
    public Vector2 burstTime = new Vector2(0.15f, 0.6f);
    public float minBrightness = 0.15f;

    private Renderer[] renderers;
    private Color[] baseColors;
    private MaterialPropertyBlock block;
    private float timer;
    private bool bursting;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i].sharedMaterial.GetColor("_BaseColor");
        block = new MaterialPropertyBlock();
        timer = Random.Range(calmTime.x, calmTime.y);
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            bursting = !bursting;
            timer = bursting ? Random.Range(burstTime.x, burstTime.y) : Random.Range(calmTime.x, calmTime.y);
            if (!bursting) Apply(1f);
        }
        if (bursting) Apply(Random.value < 0.5f ? minBrightness : Random.Range(0.6f, 1f));
    }

    void Apply(float k)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Color c = baseColors[i];
            c.a *= k;
            block.SetColor("_BaseColor", c);
            renderers[i].SetPropertyBlock(block);
        }
    }
}
