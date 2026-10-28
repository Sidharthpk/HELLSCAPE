using UnityEngine;

// The hell city's air: a black-red distance fog (SleepSequence supplies the colour and density) and, on top of it, low
// banks of drifting mist around whoever is playing. URP has no volumetric fog, so the banks are big soft particles that
// follow the camera: near the ground they thicken the air and hide the far end of every street.
public class HellMist : MonoBehaviour
{
    public Material mist;                                       // soft round particle, alpha-blended
    public Color tint = new Color(0.16f, 0.05f, 0.04f, 0.28f);  // the hell palette: dried blood, nearly black
    public float radius = 110f, height = 5f, lifetime = 30f;
    public float rate = 7f, perMetre = 0.12f;
    public Vector2 size = new Vector2(22f, 42f);

    ParticleSystem ps;

    void Start()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        ps = gameObject.AddComponent<ParticleSystem>();
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mist; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        gameObject.layer = 2;                                    // (Ignore Raycast: the map camera leaves it out)

        var m = ps.main;
        m.simulationSpace = ParticleSystemSimulationSpace.World; m.loop = true; m.playOnAwake = true;
        m.startLifetime = lifetime; m.startSpeed = 0f; m.maxParticles = 500;
        m.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        m.startColor = new ParticleSystem.MinMaxGradient(tint, new Color(tint.r * 0.4f, tint.g * 0.4f, tint.b * 0.4f, tint.a));

        var e = ps.emission; e.rateOverTime = rate; e.rateOverDistance = perMetre;
        var s = ps.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(radius * 2f, height, radius * 2f);

        var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;   // a slow crawl
        v.x = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f); v.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f); v.y = new ParticleSystem.MinMaxCurve(0f, 0f);   // (all three axes in the same mode)
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        var a = new Gradient();   // (fade in and out so nothing pops)
        a.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        var c = ps.colorOverLifetime; c.enabled = true; c.color = a;

        Follow();
        ps.Simulate(lifetime * 0.6f, true, true);   // the air is already full when the drive starts
        ps.Play();
    }

    void LateUpdate() { Follow(); }

    void Follow()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.transform.position;
        transform.position = new Vector3(p.x, p.y - 2.5f, p.z);   // (a couple of metres under the eye: a bank at street level)
    }
}
