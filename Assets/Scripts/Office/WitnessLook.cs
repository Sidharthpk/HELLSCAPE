using UnityEngine;

// What a dead office worker looks like in the red city: burnt, from the NPC textures the prologue used (the
// OfficeHorror/Burnt* materials). The way you question them changes it, live:
//   Lost       denial: fades back to the pale ghost, ash coming off the skin, less of a body each time
//   Damned     justification: red cracks glow under the char, the body swells, embers
//   Confessed  owning it: the fire settles, the colour comes back, they stand like a person again
public class WitnessLook : MonoBehaviour
{
    public enum Mode { Burnt, Lost, Damned, Confessed }
    public Mode mode = Mode.Burnt;

    SkinnedMeshRenderer[] skins;
    Material ghostMat, burnt, intact;
    public bool Echo { get; set; }
    MaterialPropertyBlock block;
    ParticleSystem ash, embers, smoke;
    Transform body;
    Vector3 scale0;
    float lost, damned, calm, flash;

    public static Material Load(string name) => Resources.Load<Material>("OfficeHorror/" + name);

    void Awake()
    {
        var ghost = GetComponent<GhostNPC>();
        body = ghost != null && ghost.body != null ? ghost.body : transform;
        skins = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        block = new MaterialPropertyBlock();
        if (skins.Length == 0) { enabled = false; return; }
        ghostMat = skins[0].sharedMaterial;
        burnt = Load(skins[0].sharedMesh != null && skins[0].sharedMesh.name == "Cylinder" ? "BurntFemale" : "BurntMale");
        intact = Load(skins[0].sharedMesh != null && skins[0].sharedMesh.name == "Cylinder" ? "IntactFemale" : "IntactMale");
        scale0 = body.localScale;
        smoke = Emitter("smoke", new Color(.12f, .1f, .1f, 1f), .06f, 2f, .25f, -.02f, 3f);
        ash = Emitter("ash", new Color(.55f, .55f, .55f, 1f), .045f, 1.8f, .05f, .12f, 0f);
        embers = Emitter("embers", new Color(1f, .35f, .05f, 1f), .035f, 1.1f, .6f, -.15f, 0f);
        Apply(true);
    }

    public void Set(Mode m) { mode = m; }

    // a red crack across the body for a moment (anger)
    public void Flash(float strength = 1f) { flash = Mathf.Max(flash, strength); }

    ParticleSystem Emitter(string name, Color c, float size, float life, float speed, float gravity, float rate) => Emitter(body, name, c, size, life, speed, gravity, rate, 1.3f);

    public static ParticleSystem Emitter(Transform body, string name, Color c, float size, float life, float speed, float gravity, float rate, float height)
    {
        var go = new GameObject("Witness " + name);
        go.transform.SetParent(body, false);
        go.transform.localPosition = Vector3.up * height;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.startColor = c; main.startSize = size; main.startLifetime = life; main.startSpeed = speed;
        main.gravityModifier = gravity; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 60;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(.45f, .8f, .3f);
        var em = ps.emission; em.rateOverTime = rate;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { color = Color.white };
        ps.Play();
        return ps;
    }

    static void Rate(ParticleSystem ps, float rate) { var e = ps.emission; e.rateOverTime = rate; }

    void Apply(bool instant)
    {
        float k = instant ? 1f : 1f - Mathf.Exp(-2f * Time.deltaTime);
        lost = Mathf.Lerp(lost, mode == Mode.Lost ? 1f : 0f, k);
        damned = Mathf.Lerp(damned, mode == Mode.Damned ? 1f : 0f, k);
        calm = Mathf.Lerp(calm, mode == Mode.Confessed ? 1f : 0f, k);
    }

    void LateUpdate()
    {
        Apply(false);
        flash = Mathf.Max(0f, flash - Time.deltaTime * 1.5f);
        bool ghostly = lost > .5f || (Echo && mode != Mode.Damned);   // the Echo shows the soul, not the burnt body
        bool human = false;
        foreach (var s in skins)
        {
            if (s == null) continue;
            var npc = GetComponent<GhostNPC>();
            s.enabled = !(npc != null && npc.fate == GhostNPC.Fate.Gone && lost > .95f);
            var material = ghostly ? ghostMat : human && intact != null ? intact : burnt;
            if (material != null && s.sharedMaterial != material) s.sharedMaterial = material;
            s.GetPropertyBlock(block);   // (GhostNPC's own flicker writes the alpha first)
            if (ghostly)
            {
                Color c = block.isEmpty ? ghostMat.GetColor("_BaseColor") : block.GetColor("_BaseColor"); c.a *= Mathf.Lerp(1f, .55f, lost);
                block.SetColor("_BaseColor", Color.Lerp(c, new Color(.35f, .6f, .9f, c.a), .6f));
                block.SetColor("_EmissionColor", Color.black);
            }
            else
            {
                // soot, lifting towards the real clothes as they own it; red cracks pulse when damned
                Color tint = human ? Color.white : Color.Lerp(new Color(.26f, .19f, .16f), new Color(.7f, .62f, .56f), calm);
                float pulse = .55f + .45f * Mathf.Sin(Time.time * 4.5f + transform.position.x);
                Color glow = new Color(1f, .04f, .015f) * (damned * 1.6f * pulse + flash * 2.2f);
                block.SetColor("_BaseColor", tint);
                block.SetColor("_EmissionColor", glow + new Color(.03f, .005f, 0f) * (1f - calm));
            }
            s.SetPropertyBlock(block);
        }
        // swollen when damned, and an unsteady ragged shape when lost
        body.localScale = Vector3.Scale(scale0, new Vector3(1f + .12f * damned, 1f + .07f * damned, 1f + .12f * damned));
        Rate(smoke, ghostly ? 0f : Mathf.Lerp(4f, 1f, calm) + damned * 3f);
        Rate(ash, ghostly && lost < .5f ? 0f : 3f + lost * 22f);
        Rate(embers, damned * 16f);
    }
}
