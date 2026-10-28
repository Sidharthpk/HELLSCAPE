using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// RE-style dread in the office: low health or something hunting you squeezes the picture. A pulsing dark-red
// vignette (faster the closer to death), chromatic aberration, a lens bulge and drained colour. Its own runtime
// volume, weighted 0..1, only while you are in the zone. Put in by HellScape > Office Look.
public class OfficeDread : MonoBehaviour
{
    public Transform player;
    public Bounds zone;
    public PlayerHealth health;
    public float lowHealth = 0.4f;      // below this share of health the dread starts
    public float threatRange = 14f;     // the stalker this close = full dread
    public float ease = 2f;             // how fast it comes and goes

    Volume vol; Vignette vig; LensDistortion lens;
    OfficeStalker stalker;
    float level, hurt, findAt;

    void Start()
    {
        if (health == null) health = FindFirstObjectByType<PlayerHealth>();
        vol = new GameObject("Office dread").AddComponent<Volume>();
        vol.transform.SetParent(transform, false);
        vol.isGlobal = true; vol.priority = 20f; vol.weight = 0f;
        var p = vol.profile;   // (an instance: ours to change every frame)
        vig = p.Add<Vignette>(true);
        vig.color.Override(new Color(0.35f, 0f, 0f)); vig.smoothness.Override(0.5f); vig.rounded.Override(true);
        p.Add<ChromaticAberration>(true).intensity.Override(0.7f);
        lens = p.Add<LensDistortion>(true);
        p.Add<ColorAdjustments>(true).saturation.Override(-70f);
    }

    void Update()
    {
        if (player == null || vol == null) return;
        if (stalker == null && Time.time > findAt)
        {
            findAt = Time.time + 2f;
            stalker ??= FindFirstObjectByType<OfficeStalker>();
        }

        float target = 0f;
        if (zone.Contains(player.position))
        {
            hurt = health != null && !health.IsDead ? Mathf.Clamp01(1f - health.currentHealth / health.maxHealth / lowHealth) : 0f;
            float threat = 0f;
            if (stalker != null && stalker.Present)
                threat = Mathf.Max(threat, 1f - Vector3.Distance(player.position, stalker.Position) / threatRange);
            target = Mathf.Max(hurt, threat);
        }
        level = Mathf.MoveTowards(level, target, ease * Time.deltaTime);
        vol.weight = level;
        if (level <= 0f) return;

        // a heartbeat: double thump, quicker as health drops
        float beat = Mathf.Repeat(Time.time * Mathf.Lerp(1.1f, 2.2f, hurt), 1f);
        float thump = Mathf.Max(Mathf.Exp(-beat * 18f), 0.6f * Mathf.Exp(-Mathf.Abs(beat - 0.22f) * 18f));
        vig.intensity.Override(0.45f + 0.15f * thump);
        lens.intensity.Override(-0.15f - 0.12f * thump);
    }
}
