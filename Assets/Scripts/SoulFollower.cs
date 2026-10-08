using UnityEngine;

// A shadow person in the soul void (EndingSequence.DeathVision): walks straight at you, never attacks. Get within
// reach and it's the jumpscare. Slower than a walking player, so you can always get away; there are just more of
// them every ten seconds.
public class SoulFollower : MonoBehaviour
{
    public Transform target;
    public float speed = 2.4f;
    public float reach = 2f;
    public float animSpeed = 1f;   // the walk cycle sped up for the one that sprints
    public Animator animator;
    public bool frozen;
    bool? hasSpeed;
    public bool caught;     // within reach: the scene's loop sees this and plays the scare

    void Update()
    {
        if (target == null) return;
        Vector3 to = target.position - transform.position; to.y = 0f;
        float d = to.magnitude;
        if (frozen || d < 0.01f) { SetAnim(0f); return; }
        if (d < reach) { caught = true; return; }
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 5f * Time.deltaTime);
        transform.position += to / d * speed * Time.deltaTime;
        SetAnim(1f);
    }

    void SetAnim(float v)
    {
        if (animator == null) return;
        if (hasSpeed == null) { hasSpeed = false; foreach (var p in animator.parameters) if (p.name == "Speed") hasSpeed = true; }
        if (hasSpeed == true) animator.SetFloat("Speed", v);
        animator.speed = v > 0f ? animSpeed : 0f;
    }
}
