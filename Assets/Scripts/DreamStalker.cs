using UnityEngine;

// A stalker in the dream hall (EndingSequence.DreamWalk): a black person with red eyes who watches you from far off.
// He only ever moves while you are not looking at him, closes to `holdAt` metres and stops there. Once the chase
// begins (`running`) they all run at you whether you look or not, but never touch you.
public class DreamStalker : MonoBehaviour
{
    public Camera cam;
    public Transform target;
    public float speed = 4.4f;
    public float runSpeed = 6.4f;       // a little slower than your sprint
    public float holdAt = 14f;          // he walks until he is this close (the dream lowers it as it goes on)
    public Animator animator;
    public bool frozen;                 // the dream has stopped them all
    public bool running;                // the chase
    public bool IsSeen { get; private set; }

    public static bool Spotted;         // someone has seen one of them: the dream's horror music starts
    static AudioClip step;
    AudioSource feet;
    float stride, lane;

    void Start() { lane = transform.position.x; }

    void Awake()
    {
        feet = gameObject.AddComponent<AudioSource>();
        feet.spatialBlend = 1f; feet.minDistance = 3f; feet.maxDistance = 45f; feet.volume = .5f;
        if (step == null) step = ProceduralAudio.Thud();
    }

    // in the picture, anywhere on screen
    bool Seen()
    {
        if (cam == null) return false;
        Vector3 v = cam.WorldToViewportPoint(transform.position + Vector3.up * 1.3f);
        return v.z > 0f && v.x > -.1f && v.x < 1.1f && v.y > -.3f && v.y < 1.3f;
    }

    void Update()
    {
        if (target == null) return;
        Vector3 to = target.position - transform.position; to.y = 0f;
        float d = to.magnitude;
        IsSeen = Seen();
        if (IsSeen && d < 32f) Spotted = true;
        bool walk = !frozen && d > holdAt && (running || !IsSeen);
        if (animator != null) animator.speed = walk ? (running ? 1.9f : 1f) : 0f;
        if (!walk) return;
        // he keeps to his own lane across the nave (they do not pile up), and looks at you the whole way
        Vector3 goal = new Vector3(lane, transform.position.y, target.position.z) - transform.position; goal.y = 0f;
        Vector3 dir = goal.sqrMagnitude > .01f ? goal.normalized : to / d;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to / d), 4f * Time.deltaTime);
        transform.position += dir * (running ? runSpeed : speed) * Time.deltaTime;
        stride -= Time.deltaTime;
        if (stride <= 0f) { stride = running ? .28f : .62f; feet.PlayOneShot(step, running ? .8f : 1f); }   // (running: fast, many, behind you)
    }
}
