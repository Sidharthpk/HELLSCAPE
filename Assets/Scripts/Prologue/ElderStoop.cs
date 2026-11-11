using UnityEngine;

// Makes one of the pack's grey-haired women an old lady: a stoop (the spine leans forward after the animation has
// run, whatever it is playing) and a walking stick in her hand.
[RequireComponent(typeof(Animator))]
public class ElderStoop : MonoBehaviour
{
    public float spine = 11f, chest = 7f, neck = 4f;   // degrees forward
    public float caneLength = 0.95f;

    Animator anim;
    Transform hand, cane;

    void Start()
    {
        anim = GetComponent<Animator>();
        if (anim == null || !anim.isHuman) { enabled = false; return; }
        hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null) return;
        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = "Walking stick";
        Destroy(c.GetComponent<Collider>());
        // (not parented: the model is scaled and turned, which would skew a thin stick; it follows her hand instead)
        c.GetComponent<Renderer>().sharedMaterial = Stick();
        cane = c.transform;
    }

    static Material stick;
    static Material Stick()
    {
        if (stick == null) { stick = new Material(Shader.Find("Universal Render Pipeline/Lit")); stick.SetColor("_BaseColor", new Color(0.28f, 0.17f, 0.09f)); }
        return stick;
    }

    void OnEnable() { if (cane != null) cane.gameObject.SetActive(true); }
    void OnDisable() { if (cane != null) cane.gameObject.SetActive(false); }   // she's gone indoors: so is the stick
    void OnDestroy() { if (cane != null) Destroy(cane.gameObject); }

    void LateUpdate()
    {
        Lean(HumanBodyBones.Spine, spine);
        Lean(HumanBodyBones.Chest, chest);
        Lean(HumanBodyBones.Neck, neck);
        if (cane == null || hand == null) return;
        // upright, the tip on the ground below the hand

        cane.rotation = Quaternion.identity;
        cane.localScale = new Vector3(0.035f, caneLength * 0.5f, 0.035f);
        cane.position = hand.position + Vector3.down * (caneLength * 0.5f - 0.12f);
    }

    void Lean(HumanBodyBones bone, float degrees)
    {
        var t = anim.GetBoneTransform(bone);
        if (t != null) t.rotation = Quaternion.AngleAxis(degrees, transform.right) * t.rotation;
    }
}
