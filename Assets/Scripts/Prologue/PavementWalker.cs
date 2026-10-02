using UnityEngine;

// A morning pedestrian in the prologue: walks up and down a stretch of pavement, pausing at each end
// (phone, shop window, bus stop). Feet kept on the ground by a ray; walk clip matched to the pace.
public class PavementWalker : MonoBehaviour
{
    public Vector3 a, b;
    public float speed = 1.3f;
    public Vector2 pause = new Vector2(1f, 6f);
    public float startAt;                   // 0..1 along a -> b
    public Animator animator;               // Civilian.controller: "Speed" 0 idle .. 1.3 walk
    public float walkClipSpeed = 1.3f;

    private float t, wait;
    private int dir = 1;

    void Start()
    {
        t = startAt;
        dir = Random.value < 0.5f ? 1 : -1;
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        float len = Mathf.Max(0.1f, Vector3.Distance(a, b));
        bool walking = wait <= 0f;
        if (walking)
        {
            t += dir * speed * Time.deltaTime / len;
            if (t >= 1f || t <= 0f)
            {
                t = Mathf.Clamp01(t);
                dir = -dir;
                wait = Random.Range(pause.x, pause.y);
            }
        }
        else wait -= Time.deltaTime;

        Vector3 p = Vector3.Lerp(a, b, t);
        if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore) && hit.point.y < p.y + 1f) p.y = hit.point.y;   // not onto a car roof
        transform.position = p;
        Vector3 face = (b - a) * dir;
        face.y = 0f;
        if (face.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 5f * Time.deltaTime);

        if (animator != null)
        {
            animator.SetFloat("Speed", walking ? walkClipSpeed : 0f, 0.2f, Time.deltaTime);
            animator.speed = walking ? speed / walkClipSpeed : 1f;
        }
    }
}
