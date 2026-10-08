using UnityEngine;

// A burnt office worker, not judged yet, walking in pain: hunched over, head hanging, one arm clutched to the belly,
// a lurching gait that speeds up and nearly stops, a stumble now and then, a moan when you are near, and an ember
// glow so they can be seen in the dark. Only in the red city while they are undecided: in the Echo the soul stands
// as it was, and once they've said their piece (WitnessLook calm) they straighten up. Goes on the four witnesses
// (OfficeHorrorDirector adds it next to WitnessLook).
[RequireComponent(typeof(GhostNPC))]
public class PainWalk : MonoBehaviour
{
    public float spine = 24f, chest = 12f, neck = 16f;      // degrees forward, at full pain
    public float roam = 3.6f;                                 // how far from where they were placed they wander
    public Vector2 speed = new Vector2(0.12f, 0.8f);        // m/s, slowest and fastest of the lurch

    GhostNPC ghost;
    WitnessLook look;
    Animator anim;
    Transform bSpine, bChest, bNeck, bUpperL, bLowerL;
    readonly System.Collections.Generic.Dictionary<Transform, Quaternion> applied = new System.Collections.Generic.Dictionary<Transform, Quaternion>();
    AudioSource moan;
    AudioClip[] moans;
    Light ember;
    float seed, pain, nextMoan, stumbleAt, stumbling;
    Vector3 body0;

    void Start()
    {
        ghost = GetComponent<GhostNPC>();
        look = GetComponent<WitnessLook>();
        anim = ghost.animator != null ? ghost.animator : GetComponentInChildren<Animator>();
        seed = Random.value * 50f;
        ghost.working = false;                 // no desk: they're hurt and lost, not at work
        ghost.driftRadius = roam;
        ghost.driftSpeed = speed.y;
        ghost.holdRange = 2.6f;                // they stop and turn to you only when you're close
        if (anim != null && anim.isHuman)
        {
            bSpine = anim.GetBoneTransform(HumanBodyBones.Spine);
            bChest = anim.GetBoneTransform(HumanBodyBones.Chest);
            bNeck = anim.GetBoneTransform(HumanBodyBones.Neck);
            bUpperL = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            bLowerL = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        }
        moan = gameObject.AddComponent<AudioSource>();
        moan.spatialBlend = 1f; moan.rolloffMode = AudioRolloffMode.Linear; moan.maxDistance = 14f; moan.playOnAwake = false;
        moans = new[] { Sounds.Clip("Office/Witness moan", null) ?? ProceduralAudio.Whisper(1.4f), ProceduralAudio.Creak(0.9f) };
        nextMoan = Random.Range(2f, 8f);
        stumbleAt = Random.Range(3f, 7f);
        body0 = ghost.body != null ? ghost.body.localPosition : Vector3.zero;

        // a dull ember light on the burnt body: the only way to see them in the blacked-out office
        ember = new GameObject("Witness ember").AddComponent<Light>();
        ember.transform.SetParent(transform, false);
        ember.transform.localPosition = new Vector3(0f, 1f, 0.35f);
        ember.type = LightType.Point; ember.color = new Color(1f, .38f, .12f); ember.range = 3.4f; ember.intensity = 1.1f; ember.shadows = LightShadows.None;

        // in the Echo the same person is a soul: a pale blue light round them, so they show in the dark and you can find the truth
        soulLight = new GameObject("Witness soul light").AddComponent<Light>();
        soulLight.transform.SetParent(transform, false);
        soulLight.transform.localPosition = new Vector3(0f, 1.1f, 0.4f);
        soulLight.type = LightType.Point; soulLight.color = new Color(.45f, .7f, 1f); soulLight.range = 4.2f; soulLight.intensity = 1.6f; soulLight.shadows = LightShadows.None;
        soulLight.enabled = false;
    }
    Light soulLight;

    bool Soul => look != null && look.Echo;

    void Update()
    {
        if (ghost == null) return;
        // pain: full while undecided in the red city; eases when they've owned it; none for a soul in the Echo
        float want = Soul ? 0f : look != null && look.mode == WitnessLook.Mode.Confessed ? 0.15f : 1f;
        pain = Mathf.MoveTowards(pain, want, Time.deltaTime * 0.8f);
        if (ember != null) ember.enabled = !Soul;
        if (soulLight != null) soulLight.enabled = Soul && (ghost.fate != GhostNPC.Fate.Gone);

        // the lurch: a few steps, almost stopping, a few more
        float n = Mathf.PerlinNoise(seed, Time.time * 0.45f);
        ghost.driftSpeed = Mathf.Lerp(speed.x, speed.y, n * n) * Mathf.Lerp(1f, 0.85f, pain);

        // a stumble: they catch themselves
        stumbleAt -= Time.deltaTime;
        if (stumbleAt <= 0f && pain > 0.5f) { stumbling = 0.6f; stumbleAt = Random.Range(4f, 9f); }
        stumbling = Mathf.Max(0f, stumbling - Time.deltaTime);

        if (!Soul && pain > 0.5f && Time.timeScale > 0f)
        {
            nextMoan -= Time.deltaTime;
            if (nextMoan <= 0f)
            {
                nextMoan = Random.Range(6f, 14f);
                if (!moan.isPlaying) { moan.pitch = Random.Range(0.4f, 0.55f); moan.PlayOneShot(moans[Random.Range(0, moans.Length)], 0.5f * Sounds.Volume("Office/Ghost whispers")); }
            }
        }
        if (ghost.body != null)   // sinks and lurches on a stumble
            ghost.body.localPosition += new Vector3(0f, -0.12f * Mathf.Sin(stumbling / 0.6f * Mathf.PI), 0f);
    }

    void LateUpdate()
    {
        if (anim == null || pain < 0.01f) return;
        float tremble = Mathf.Sin(Time.time * 9f + seed) * 1.5f * pain;
        float lurch = stumbling > 0f ? Mathf.Sin(stumbling / 0.6f * Mathf.PI) * 14f : 0f;
        Bend(bSpine, (spine + lurch) * pain + tremble);
        Bend(bChest, chest * pain);
        Bend(bNeck, neck * pain + Mathf.Sin(Time.time * 1.7f + seed) * 6f * pain);
        // one arm clutched across the belly
        if (bUpperL != null && bLowerL != null && pain > 0.3f)
        {
            Pose(bUpperL, Quaternion.AngleAxis(-38f * pain, transform.right) * Quaternion.AngleAxis(24f * pain, transform.forward));
            Pose(bLowerL, Quaternion.AngleAxis(-70f * pain, transform.right));
        }
    }

    void Bend(Transform t, float degrees) { if (t != null) Pose(t, Quaternion.AngleAxis(degrees, transform.right)); }

    // after the animation has posed the bone this frame (and not stacking on one it didn't re-pose: a culled animator)
    void Pose(Transform t, Quaternion by)
    {
        if (applied.TryGetValue(t, out var last) && t.localRotation == last) return;
        t.rotation = by * t.rotation;
        applied[t] = t.localRotation;
    }
}
