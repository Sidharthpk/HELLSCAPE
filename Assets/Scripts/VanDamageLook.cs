using System.Collections.Generic;
using UnityEngine;

// The van shows its own damage: no health bar. As CarHealth drops, the paint job is swapped for the next, more
// wrecked version of the same texture (scuffed -> rusted and clawed -> scorched -> burnt out; made by
// Tools/van_damage/make_van_damage.py from Kesller.png), and once it's badly hurt the bonnet starts to smoke.
// A repair (checkpoint) puts the clean paint back.
public class VanDamageLook : MonoBehaviour
{
    public CarHealth health;
    public Texture2D[] stages;                      // 0 = as new ... last = wrecked
    public float[] below = { 0.8f, 0.6f, 0.4f, 0.2f };   // health fraction under which stage 1, 2, 3, 4 shows
    public Material paint;                          // the van's shared material (found from the first stage if empty)
    public Vector3 smokeAt = new Vector3(0f, 1.3f, 2.1f);   // on the bonnet, in the van's own space
    public int smokeFromStage = 3;

    private Material live;
    private int shown = -1;
    private ParticleSystem smoke;

    void Awake()
    {
        if (health == null) health = GetComponent<CarHealth>();
        // one private copy of the paint for this van: the shared material asset is never touched
        var mine = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var m = r.sharedMaterial;
            if (m == null || r is ParticleSystemRenderer) continue;
            if (paint != null ? m == paint : stages != null && stages.Length > 0 && m.mainTexture == stages[0]) { mine.Add(r); if (paint == null) paint = m; }
        }
        if (paint == null) { enabled = false; return; }
        live = new Material(paint) { name = paint.name + " (this van)" };
        foreach (var r in mine) r.sharedMaterial = live;
    }

    void Update()
    {
        if (health == null || live == null || stages == null || stages.Length == 0) return;
        float k = health.maxHealth > 0f ? health.currentHealth / health.maxHealth : 1f;
        int stage = 0;
        for (int i = 0; i < below.Length; i++) if (k < below[i]) stage = i + 1;
        stage = Mathf.Min(stage, stages.Length - 1);
        if (stage == shown) return;
        bool worse = stage > shown && shown >= 0;
        shown = stage;
        if (stages[stage] != null)
        {
            live.mainTexture = stages[stage];
            if (live.HasProperty("_BaseMap")) live.SetTexture("_BaseMap", stages[stage]);
        }
        Smoke(stage, worse);
    }

    void Smoke(int stage, bool puff)
    {
        if (stage < smokeFromStage && smoke == null) return;
        if (smoke == null) smoke = MakeSmoke();
        var em = smoke.emission;
        em.rateOverTime = stage < smokeFromStage ? 0f : stage == smokeFromStage ? 9f : 24f;
        if (puff && stage >= smokeFromStage) smoke.Emit(14);
    }

    // square puffs, like everything else in this city
    ParticleSystem MakeSmoke()
    {
        var go = new GameObject("Engine smoke");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = smokeAt;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 90;
        main.gravityModifier = -0.08f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f; shape.radius = 0.35f;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.6f, 1.6f), new Keyframe(1f, 0f)));
        var em = ps.emission;
        em.rateOverTime = 0f;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", new Color(0.07f, 0.06f, 0.06f, 0.62f));
        mat.SetFloat("_Surface", 1f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    void OnDestroy()
    {
        if (live != null) Destroy(live);
    }
}
