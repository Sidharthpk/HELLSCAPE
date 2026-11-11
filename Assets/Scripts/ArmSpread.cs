using UnityEngine;

// Fists 2025 holds the hands almost touching: after the Animator has posed the arms, each shoulder (and the whole arm
// with it) is moved outward by `spread`, so every clip keeps its shape with the hands further apart. On top, a living
// sway: a slow breath up and down and a wandering drift side to side, each arm a little out of step with the other.
public class ArmSpread : MonoBehaviour
{
    public float spread = 0.05f;            // rig units per side (the hands start ~0.06 from the middle)
    public float swayUp = 0.006f, swaySide = 0.004f;    // rig units: breath up/down, drift left/right
    public float breathRate = 0.28f, driftRate = 0.22f; // cycles per second (~3.5 s a breath)
    private Transform left, right;
    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "ORG-shoulder.L") left = t;
            else if (t.name == "ORG-shoulder.R") right = t;
        }
    }

    // (every clip keys the shoulders, so the Animator resets them each frame: no build-up; skip if it's off)
    void LateUpdate()
    {
        if (animator == null || !animator.isActiveAndEnabled || left == null || right == null) return;
        left.position += transform.TransformVector(new Vector3(-spread, 0f, 0f) + Sway(0f));
        right.position += transform.TransformVector(new Vector3(spread, 0f, 0f) + Sway(1.7f));
    }

    // one arm's offset: the breath is shared (a touch out of phase), the side drift is Perlin noise of its own
    Vector3 Sway(float seed)
    {
        float t = Time.time;
        float up = Mathf.Sin((t * breathRate + seed * .08f) * Mathf.PI * 2f) * swayUp;
        float side = (Mathf.PerlinNoise(t * driftRate, seed * 13.1f) - .5f) * 2f * swaySide;
        float fwd = (Mathf.PerlinNoise(seed * 7.3f, t * driftRate * .8f) - .5f) * swaySide;
        return new Vector3(side, up, fwd);
    }
}
