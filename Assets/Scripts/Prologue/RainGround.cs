using UnityEngine;

// Rain at street level: droplet splashes and ripples
// where the drops land. Rides along with the camera like RainFollow; stops under a roof.
public class RainGround : MonoBehaviour
{
    public Material dropMat, rippleMat;
    public float splashArea = 40f;
    public Collider[] blockers;             // trigger boxes (the clinic): rain and splashes inside them are killed

    ParticleSystem drops, ripples;
    float groundY;

    void Awake()
    {
        drops = MakeSystem("Splash", dropMat, false);
        ripples = MakeSystem("Ripple", rippleMat, true);
        foreach (var ps in new[] { GetComponent<ParticleSystem>(), drops, ripples })
        {
            var tr = ps.trigger; tr.enabled = true; tr.inside = ParticleSystemOverlapAction.Kill;
            for (int i = 0; blockers != null && i < blockers.Length; i++) tr.SetCollider(i, blockers[i]);
        }
    }

    ParticleSystem MakeSystem(string n, Material mat, bool ring)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // shape's forward = up
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startLifetime = ring ? new ParticleSystem.MinMaxCurve(0.5f, 0.8f) : new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        m.startSpeed = ring ? 0f : new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
        m.startSize = ring ? new ParticleSystem.MinMaxCurve(0.7f, 1.2f) : new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        m.gravityModifier = ring ? 0f : 3.5f;
        m.maxParticles = 4000;
        m.startColor = new Color(0.7f, 0.72f, 0.7f, ring ? 0.5f : 0.75f);
        var e = ps.emission; e.rateOverTime = ring ? 300f : 500f;
        var s = ps.shape;
        s.shapeType = ParticleSystemShapeType.Box;
        s.scale = new Vector3(splashArea, splashArea, 0f);
        if (ring)
        {
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.15f, 1f, 1f));
        }
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        if (ring) { r.renderMode = ParticleSystemRenderMode.HorizontalBillboard; }
        else { r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 2f; r.velocityScale = 0.04f; }
        ps.Play();
        return ps;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.isActiveAndEnabled)
        {
            cam = null;
            foreach (var c in Camera.allCameras) if (c.targetTexture == null && (cam == null || c.depth > cam.depth)) cam = c;
        }
        if (cam == null) return;
        var p = cam.transform.position;
        bool open = !Physics.Raycast(p, Vector3.up, 30f, ~0, QueryTriggerInteraction.Ignore);
        if (Physics.Raycast(p, Vector3.down, out var gh, 8f, ~0, QueryTriggerInteraction.Ignore)) groundY = gh.point.y + 0.02f;
        else groundY = p.y - 1.7f;
        // splash systems sit at ground level under the camera; no ground-hit = no splash
        foreach (var ps in new[] { drops, ripples })
        {
            ps.transform.position = new Vector3(p.x, groundY, p.z);
            var em = ps.emission; em.enabled = open;
        }
    }

}
