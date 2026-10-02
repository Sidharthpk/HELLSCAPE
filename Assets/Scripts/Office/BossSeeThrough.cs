using System.Collections.Generic;
using UnityEngine;

// Kessler is nine metres of demon and every driving camera stands behind the van, which is where he hunts from:
// he kept standing in the picture between the camera and the van, and you died to things you couldn't see.
// While you're driving, whenever his body is on the line from the camera to the van he fades to a ghost you can
// see the van and the road through (his heart keeps glowing), and comes back solid as soon as he's out of the way.
// On the boss, next to DemonBoss. On foot the camera is your own eyes, so nothing fades.
public class BossSeeThrough : MonoBehaviour
{
    public DemonBoss boss;
    [Range(0.05f, 1f)] public float ghostAlpha = 0.2f;
    public float fadeSpeed = 7f;            // per second
    public float reach = 2.2f;              // his arms and the cross stick out this far past his hitbox

    private readonly List<Material> mats = new List<Material>();
    private readonly List<int> queues = new List<int>();
    private readonly List<float> alphas = new List<float>();
    private float shown = 1f;
    private bool ghosted;
    private Collider body;

    void Awake()
    {
        if (boss == null) boss = GetComponent<DemonBoss>();
        body = GetComponent<CapsuleCollider>();                       // his one hitbox
        if (body == null) body = GetComponentInChildren<CapsuleCollider>();
        if (body == null) body = GetComponent<Collider>();
    }

    // his own copies of the materials, made the first time he's in the way
    void Collect()
    {
        Transform root = boss != null && boss.model != null ? boss.model : transform;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            foreach (var m in r.materials)   // (instances: the shared assets are never touched)
            {
                if (m == null || !m.HasProperty("_Surface") || !m.HasProperty("_BaseColor")) continue;
                mats.Add(m); queues.Add(m.renderQueue); alphas.Add(m.GetColor("_BaseColor").a);
            }
        }
    }

    bool Blocking()
    {
        if (boss == null || body == null || boss.carInteract == null || !boss.carInteract.InCar) return false;
        if (DriveBy.Aiming) return false;                                    // leaning out with the rifle: he's the target, solid
        var cam = boss.carInteract.carCamera;
        if (cam == null || !cam.enabled) return false;
        // a cutscene camera over the top of it: he's the star of those, leave him solid
        foreach (var c in Camera.allCameras) if (c != cam && c.depth > cam.depth && c.targetTexture == null) return false;

        Vector3 from = cam.transform.position, to = boss.carInteract.transform.position + Vector3.up * 1.2f;
        Bounds b = body.bounds;
        b.Expand(new Vector3(reach * 2f, 0f, reach * 2f));
        if (b.Contains(from)) return true;                                   // the camera is inside him
        float len = Vector3.Distance(from, to);
        return b.IntersectRay(new Ray(from, (to - from) / Mathf.Max(0.01f, len)), out float hit) && hit < len;
    }

    void LateUpdate()
    {
        float want = Blocking() ? ghostAlpha : 1f;
        if (Mathf.Approximately(shown, want)) return;
        shown = Mathf.MoveTowards(shown, want, fadeSpeed * Time.unscaledDeltaTime);
        if (mats.Count == 0) Collect();

        bool ghost = shown < 0.999f;
        if (ghost != ghosted)
        {
            ghosted = ghost;
            for (int i = 0; i < mats.Count; i++) Surface(mats[i], ghost, queues[i]);
        }
        for (int i = 0; i < mats.Count; i++)
        {
            var c = mats[i].GetColor("_BaseColor");
            c.a = alphas[i] * shown;
            mats[i].SetColor("_BaseColor", c);
        }
    }

    // URP Lit between opaque and see-through. Depth is still written, so only his nearest surface shows: a clean
    // ghost, not a tangle of his insides.
    static void Surface(Material m, bool transparent, int opaqueQueue)
    {
        m.SetFloat("_Surface", transparent ? 1f : 0f);
        m.SetInt("_SrcBlend", (int)(transparent ? UnityEngine.Rendering.BlendMode.SrcAlpha : UnityEngine.Rendering.BlendMode.One));
        m.SetInt("_DstBlend", (int)(transparent ? UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : UnityEngine.Rendering.BlendMode.Zero));
        m.SetInt("_ZWrite", 1);
        if (transparent) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); else m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
        m.renderQueue = transparent ? 3000 : opaqueQueue;
    }

    void OnDestroy()
    {
        foreach (var m in mats) if (m != null) Destroy(m);
    }
}
