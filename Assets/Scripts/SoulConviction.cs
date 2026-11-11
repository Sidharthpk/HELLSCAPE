using UnityEngine;

// Per-renderer properties keep imported ghost materials and other characters unchanged.
public class SoulConviction : MonoBehaviour
{
    [Range(0f, 1f)] public float rage = .2f;
    public bool confessed;
    public Color calm = new Color(.35f, .72f, .95f, .5f);   // the resting colour
    [Range(0f, 1f)] public float soot;                      // creeping darkening (Tomas, as the office turns)
    Renderer[] skins;
    MaterialPropertyBlock block;
    float surge;
    void Awake() { skins = GetComponentsInChildren<Renderer>(true); block = new MaterialPropertyBlock(); }
    public void Exert(float strength = 1f) { rage = Mathf.Max(rage, strength * .7f); surge = 2.5f; }
    // an almost-whole body in the real clothes, instead of the translucent ghost (Tomas)
    public void Solidify(Material m, Color tint)
    {
        calm = tint;
        if (skins == null) Awake();
        foreach (var s in skins) if (s != null && !(s is ParticleSystemRenderer)) s.sharedMaterial = m;
    }
    public void Confess() { confessed = true; surge = 0; }
    void LateUpdate()
    {
        if (skins == null) return;
        surge = Mathf.Max(0f, surge - Time.deltaTime);
        float pulse = confessed ? 0f : surge > 0 ? 1f : Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * (2f + rage * 5f))), 14f) * rage;
        foreach (var skin in skins)
        {
            if (skin == null) continue;
            skin.GetPropertyBlock(block);
            Color tint = Color.Lerp(calm * Color.Lerp(Color.white, new Color(.55f, .5f, .5f, 1f), soot), new Color(1f, .03f, .015f, .95f), pulse);
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            block.SetColor("_EmissionColor", new Color(pulse * 2f, .06f, .08f));
            skin.SetPropertyBlock(block);
        }
    }
}
