using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

// Tomas walks out of the office with Vane and Maya crawling after him, then drags them up into a floating pose and
// they flash red as they are fused into him. Visual-only clones: no copied AI, health or triggers.
public class TomasAmalgamation : MonoBehaviour
{
    public bool Prepared => tomas != null && vane != null && maya != null;
    public bool Absorbing { get; private set; }
    public bool Formed { get; private set; }
    GameObject tomas, vane, maya, remains, tomasBody;
    RuntimeAnimatorController crawlCtrl, floatCtrl;
    readonly List<Material> solids = new List<Material>();
    readonly List<Transform> corpses = new List<Transform>();
    readonly List<Vector3> starts = new List<Vector3>();
    readonly List<LineRenderer> sinews = new List<LineRenderer>();
    readonly List<Light> lights = new List<Light>();
    readonly List<Renderer[]> skins = new List<Renderer[]>();
    Material tissue;
    Vector3 centre;
    Quaternion facing;
    MaterialPropertyBlock block;

    public void Prepare(DemonBoss boss)
    {
        if (Prepared) return;
        var scene = gameObject.scene;
        var guide = RevisedPlot.All<GhostNPC>(scene).FirstOrDefault(g => g.ghostName == "Tomas");
        var risen = RevisedPlot.All<RisenDamned>(scene).FirstOrDefault();
        tomas = Template(guide != null && guide.animator != null ? guide.animator.gameObject : null, "Tomas - fusion source");
        vane = Template(boss.damnedForm, "Vane - ruined Damned shell");
        maya = Template(risen != null && risen.damned != null ? risen.damned.gameObject : null, "Maya - ruined Damned shell");
        crawlCtrl = risen != null && risen.anim != null ? risen.anim.runtimeAnimatorController : null;
        floatCtrl = Resources.Load<RuntimeAnimatorController>("OfficeHorror/DamnedFloat");   // HellScape > Build Damned Float Pose
        tissue = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        tissue.color = new Color(.29f, .025f, .035f); tissue.SetFloat("_Smoothness", .8f);
    }

    // a scriptless, colliderless, switched-off copy of a body
    GameObject Template(GameObject source, string label)
    {
        if (source == null) return null;
        bool was = source.activeSelf; source.SetActive(false);   // copied while off: nothing in it wakes up
        var copy = Instantiate(source, transform); source.SetActive(was);
        copy.name = label; copy.SetActive(false);
        foreach (var mb in copy.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(mb);
        foreach (var n in copy.GetComponentsInChildren<NavMeshAgent>(true)) DestroyImmediate(n);
        foreach (var j in copy.GetComponentsInChildren<Joint>(true)) DestroyImmediate(j);
        foreach (var c in copy.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (var rb in copy.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
        foreach (var r in copy.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = r.sharedMaterials.Select(Solid).ToArray();
        return copy;
    }

    // Tomas is a see-through Unlit ghost: in the fusion he has to be a body
    Material Solid(Material m)
    {
        if (m == null || m.renderQueue < 3000) return m;
        var solid = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.62f, .66f, .72f), name = "Tomas solid" };
        solid.SetFloat("_Smoothness", .2f); solids.Add(solid); return solid;
    }

    static GameObject Live(GameObject template, Vector3 at, Quaternion rot, Transform parent)
    {
        var body = Instantiate(template, at, rot, parent); body.SetActive(true);
        foreach (var r in body.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        foreach (var a in body.GetComponentsInChildren<Animator>(true)) { a.enabled = true; a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        return body;
    }

    // the animator that actually drives the skeleton (the root of a zombie prefab can hold a second, avatar-less one)
    static Animator Rig(GameObject body)
    {
        var all = body.GetComponentsInChildren<Animator>(true);
        return all.FirstOrDefault(a => a.avatar != null) ?? all.FirstOrDefault();
    }

    // Tomas, walking
    public GameObject CreateTomas(Vector3 position, Quaternion rotation)
    {
        if (tomas == null) return null;
        tomasBody = Live(tomas, position, rotation, null); tomasBody.name = "Tomas, collecting his killers";
        Tomas(1.3f); return tomasBody;
    }

    public void Tomas(float speed)
    {
        if (tomasBody == null) return;
        var a = Rig(tomasBody);
        if (a != null) a.SetFloat("Speed", speed);
    }

    // Vane and Maya crawling in his wake
    public void Stage(Vector3 position, Quaternion rotation)
    {
        ClearRemains(); centre = position; facing = rotation;
        Formed = false; Absorbing = false;
        remains = new GameObject("Vane and Maya at the office exit");
        var sources = new[] { vane, maya };
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null) continue;
            var corpse = Live(sources[i], position, rotation, remains.transform);
            corpse.name = i == 0 ? "Vane crawling" : "Maya crawling";
            var an = Rig(corpse);
            if (an != null && crawlCtrl != null) { an.runtimeAnimatorController = crawlCtrl; an.Play("Crawl", 0, i * .4f); an.SetFloat("Speed", .5f); }
            corpses.Add(corpse.transform); starts.Add(position);
            skins.Add(corpse.GetComponentsInChildren<Renderer>());
            var strip = new GameObject("Torn connective tissue").AddComponent<LineRenderer>();
            strip.transform.SetParent(remains.transform, false); strip.sharedMaterial = tissue;
            strip.positionCount = 9; strip.startWidth = .1f; strip.endWidth = .23f; strip.numCapVertices = 4;
            strip.enabled = false; sinews.Add(strip);
            var light = new GameObject("Fusion flash").AddComponent<Light>();
            light.transform.SetParent(corpse.transform, false); light.transform.localPosition = Vector3.up * 1.2f;
            light.color = new Color(1f, .05f, .02f); light.range = 6f; light.intensity = 0f; lights.Add(light);
        }
    }

    // they crawl after him, a couple of metres back, one to each side
    public void Follow(Vector3 tomasAt, Vector3 dir)
    {
        if (Absorbing) return;
        centre = tomasAt; facing = Quaternion.LookRotation(dir);
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        for (int i = 0; i < corpses.Count; i++)
        {
            Vector3 want = tomasAt - dir * (2.4f + i * .5f) + side * (i == 0 ? -.9f : .9f);
            want.y = tomasAt.y;
            corpses[i].position = Vector3.Lerp(corpses[i].position, want, 1f - Mathf.Exp(-3f * Time.deltaTime));
            corpses[i].rotation = Quaternion.Slerp(corpses[i].rotation, Quaternion.LookRotation(dir), 4f * Time.deltaTime);
        }
    }

    public void Absorb(float progress)
    {
        EvidenceJournal.TomasDamned = true;
        if (!Absorbing)
        {
            Absorbing = true; Tomas(0f);
            for (int i = 0; i < corpses.Count; i++)
            {
                starts[i] = corpses[i].position;
                var an = Rig(corpses[i].gameObject);
                if (an != null && floatCtrl != null) { an.runtimeAnimatorController = floatCtrl; an.Play("Float", 0, 0f); }   // Floating Pose 2
            }
        }
        if (block == null) block = new MaterialPropertyBlock();
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        for (int i = 0; i < corpses.Count; i++)
        {
            float side = i == 0 ? -1f : 1f;
            Vector3 into = centre + facing * new Vector3(side * .45f, 1.6f, 0f);
            Vector3 at = Vector3.Lerp(starts[i], into, k) + Vector3.up * Mathf.Sin(Mathf.Min(1f, k * 2f) * Mathf.PI * .5f) * 1.1f * (1f - k);
            corpses[i].position = at;
            Vector3 toward = centre - at; toward.y = 0f;
            corpses[i].rotation = Quaternion.LookRotation(toward.sqrMagnitude > .01f ? toward : facing * Vector3.forward);
            corpses[i].localScale = Vector3.one * (1f - .5f * k);
            // red flashing, faster and harder as they go in
            bool on = Mathf.Sin(Time.time * (14f + 22f * k) + i * 2f) > 0f || Random.value < .15f;
            lights[i].intensity = on ? 3f + 12f * k : 0f;
            block.SetColor("_BaseColor", on ? Color.Lerp(Color.white, new Color(1f, .05f, .05f), .4f + .6f * k) : new Color(.35f, .05f, .05f));
            foreach (var r in skins[i]) if (r != null) r.SetPropertyBlock(block);
            var strip = sinews[i]; strip.enabled = true;
            for (int j = 0; j < strip.positionCount; j++)
            {
                float t = j / (float)(strip.positionCount - 1);
                strip.SetPosition(j, Vector3.Lerp(corpses[i].position + Vector3.up * .8f, centre + Vector3.up * 1.2f, t)
                    + Vector3.up * Mathf.Sin(t * Mathf.PI) * (.16f + .09f * Mathf.Sin(Time.time * 13f + i)));
            }
        }
    }

    public void Complete() { ClearRemains(); Formed = true; }
    void ClearRemains()
    {
        if (remains != null) Destroy(remains);
        corpses.Clear(); starts.Clear(); sinews.Clear(); lights.Clear(); skins.Clear(); Absorbing = false;
    }
    void OnDestroy()
    {
        ClearRemains();
        if (tissue != null) Destroy(tissue);
        foreach (var m in solids) if (m != null) Destroy(m);
    }
}
