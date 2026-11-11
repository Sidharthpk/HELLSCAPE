using UnityEngine;

// A pedestrian walking with an umbrella: the right arm is held up (two-bone reach on the humanoid arm, run in LateUpdate
// after the walk clip has played) with the umbrella over the head; the left arm keeps swinging. The umbrella is spawned at
// Start in a random colour. Added to the street life by HellScape > Prologue > Give Pedestrians Umbrellas.
public class UmbrellaCarrier : MonoBehaviour
{
    public GameObject umbrellaPrefab;       // pivot at the grip, built by UmbrellaBuilder (submesh 0 = cloth, 1 = frame)
    public Material[] canopies;             // one is picked at random for the cloth
    public Animator animator;
    static readonly Vector3 handOffset = new Vector3(.30f, 0f, .12f);     // hand target from the shoulder, body space: right, (height set from the head), forward (metres)
    const float lean = .25f;                // how far the umbrella top leans in toward the head
    public float headroom = .08f;           // gap between the top of the head and the canopy's rim on that side (metres)
    const float rimAboveGrip = .24f;        // the prefab's canopy rim is .36 m above the grip, tipped .12 m lower on the head side by the lean

    Transform upper, lower, hand, head, umbrella, body;
    float headTop;                          // the top of the hair above the head bone (metres)

    void Start()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman || umbrellaPrefab == null) { enabled = false; return; }
        Spawn();
    }

    public void Spawn()
    {
        body = animator.transform;
        upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        lower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (upper == null || lower == null || hand == null || head == null) { enabled = false; return; }
        headTop = 0f;
        foreach (var sk in animator.GetComponentsInChildren<SkinnedMeshRenderer>()) headTop = Mathf.Max(headTop, sk.bounds.max.y - head.position.y);
        headTop = Mathf.Clamp(headTop, .1f, .3f);
        if (umbrella != null) Destroy(umbrella.gameObject);
        umbrella = Instantiate(umbrellaPrefab, body).transform;
        umbrella.name = "Umbrella";
        umbrella.localScale = Vector3.one / body.lossyScale.x;   // the rig is scaled (0.4 on the PSX people): keep the umbrella and the reach in real metres
        var r = umbrella.GetComponent<Renderer>();
        if (r != null && canopies != null && canopies.Length > 0)
        {
            var mats = r.sharedMaterials;
            mats[0] = canopies[Random.Range(0, canopies.Length)];
            r.sharedMaterials = mats;
        }
    }

    void LateUpdate() { Pose(); }

    public void Pose()
    {
        if (umbrella == null) return;
        Vector3 shoulder = upper.position;
        float up = head.position.y + headTop + headroom - rimAboveGrip - shoulder.y;   // the rim just clears the hair
        Vector3 target = shoulder + (body.right * handOffset.x + Vector3.up * up + body.forward * handOffset.z);
        Vector3 pole = shoulder + (body.right * .8f - Vector3.up * .6f - body.forward * .2f);   // the elbow stays out and down
        Reach(upper, lower, hand, target, pole);
        umbrella.position = hand.position;
        umbrella.rotation = Quaternion.FromToRotation(Vector3.up, (Vector3.up - body.right * lean).normalized);
    }

    static void Reach(Transform up, Transform lo, Transform hd, Vector3 target, Vector3 pole)
    {
        Vector3 a = up.position, b = lo.position, c = hd.position;
        float la = (b - a).magnitude, lb = (c - b).magnitude;
        Vector3 to = target - a;
        float d = Mathf.Clamp(to.magnitude, Mathf.Abs(la - lb) + .001f, la + lb - .001f);
        Vector3 dir = to.normalized;
        Vector3 pp = Vector3.ProjectOnPlane(pole - a, dir);
        if (pp.sqrMagnitude < 1e-6f) pp = Vector3.ProjectOnPlane(Vector3.down, dir);
        pp.Normalize();
        float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
        Vector3 elbow = a + (dir * cosA + pp * Mathf.Sqrt(1f - cosA * cosA)) * la;
        up.rotation = Quaternion.FromToRotation(b - a, elbow - a) * up.rotation;
        lo.rotation = Quaternion.FromToRotation(hd.position - lo.position, a + dir * d - lo.position) * lo.rotation;
    }
}
