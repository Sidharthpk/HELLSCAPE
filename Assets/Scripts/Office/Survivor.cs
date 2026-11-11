using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// A colleague who made it. Press F on them to bring them along (Interactable.onUse -> Join),
// or walk away and leave them. Followers teleport with you and get in the car with you.
public class Survivor : MonoBehaviour
{
    public static readonly List<Survivor> All = new List<Survivor>();

    public string displayName = "Maya";
    public Animator animator;
    public Transform player;
    public float followDistance = 2.2f;
    public float speed = 3.8f;
    [TextArea] public string joinLine = "Maya: Okay. Okay, I'm right behind you.";

    // Everyone in the red city has died once already. Die again here and you don't get to leave: you become one of
    // the Lost (drifting, blue, harmless) or, if you've owned up to something rotten, one of the Damned.
    public enum Fate { Lost, Damned }
    public Fate ifKilled = Fate.Lost;
    [TextArea] public string turnedLine = "";   // said when they come back as it (empty = BloodFlood's default)

    public bool Following { get; private set; }
    public bool Rescued { get; private set; }   // followed you all the way into the car
    public bool Drowned { get; private set; }   // you let them go under in the silo
    public bool Turned { get; private set; }    // said their sin out loud: the city took them (never coming with you)
    public bool Clinging { get; set; }          // BloodFlood: swimming up beside you (follows without having joined)
    public float? FloatAt { get; set; }         // BloodFlood: where to hold the feet while they tread blood (null = on the ground)
    public float GroundY { get; set; }  // the floor under them last time they stood on it

    private Vector3 startPos;
    private Quaternion startRot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        All.Clear();
        SceneManager.sceneLoaded += (s, m) => All.RemoveAll(x => x == null);
    }

    void Awake()
    {
        All.Add(this);
        startPos = transform.position;
        startRot = transform.rotation;
        GroundY = startPos.y;
    }
    void OnDestroy() => All.Remove(this);

    void Start()
    {
        if (player == null) { var p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.transform; }
    }

    public void Join()
    {
        if (Following) return;
        Following = true;
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(joinLine)) DialogueBox.Instance.Say(joinLine);
    }

    void Update()
    {
        if (player == null || !player.gameObject.activeInHierarchy || Drowned) { SetSpeed(0f); return; }

        Vector3 to = player.position - transform.position; to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 6f * Time.deltaTime);

        // don't stand inside each other: step aside from any other survivor that's too close
        Vector3 apart = Vector3.zero;
        foreach (var o in All)
        {
            if (o == this || o == null || !o.gameObject.activeInHierarchy) continue;
            Vector3 d = transform.position - o.transform.position; d.y = 0f;
            if (d.magnitude < 0.9f) apart += (d.sqrMagnitude < 0.0001f ? transform.right * (All.IndexOf(this) % 2 == 0 ? 1f : -1f) : d.normalized) * (0.9f - d.magnitude);
        }

        // each follower keeps its own distance behind you, so they trail in a line rather than a clump
        float keep = followDistance + 1.1f * FollowIndex();
        if (!(Following || Clinging) || to.magnitude <= keep)
        {
            if (apart != Vector3.zero) { transform.position += apart * 3f * Time.deltaTime; SnapToGround(); }
            SetSpeed(0f);
            return;
        }

        float s = to.magnitude > keep * 3f ? speed * 1.4f : speed;
        transform.position += (to.normalized * s + apart * 3f) * Time.deltaTime;
        SnapToGround();
        SetSpeed(s);
    }

    int FollowIndex()
    {
        int i = 0;
        foreach (var o in All)
        {
            if (o == this) return i;
            if (o != null && (o.Following || o.Clinging) && o.gameObject.activeInHierarchy) i++;
        }
        return i;
    }

    // the floor under our feet: the nearest surface below knee height. Never the pipe or walkway overhead
    // (RaycastAll comes back in no particular order, and the first hit used to be the ceiling in the silo).
    void SnapToGround()
    {
        float feet = transform.position.y;
        RaycastHit best = default;
        bool found = false;
        foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * 0.6f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.CompareTag("Player")) continue;
            if (hit.collider.GetComponentInParent<Survivor>() != null) continue;   // not on top of each other
            if (!found || hit.distance < best.distance) { best = hit; found = true; }
        }
        if (found && best.point.y <= feet + 0.6f)
        {
            GroundY = best.point.y;
            if (!FloatAt.HasValue) transform.position = new Vector3(transform.position.x, best.point.y, transform.position.z);
        }
    }

    // afloat: the flood holds them at its surface (after Update's walking and ground snap)
    void LateUpdate()
    {
        if (!FloatAt.HasValue) return;
        float bob = Drowned ? 0f : Mathf.Sin(Time.time * 2.3f + startPos.x) * 0.06f;
        transform.position = new Vector3(transform.position.x, FloatAt.Value + bob, transform.position.z);
        SetSpeed(Following || Clinging ? 1.2f : 0.4f);   // treading: a slow walk cycle reads as kicking
    }

    // the flood took them: BloodFlood drags them down, then hides them
    public void Drown()
    {
        if (Drowned) return;
        Drowned = true;
        Following = false;
        Clinging = false;
        var it = GetComponent<Interactable>();
        if (it != null) it.enabled = false;
    }

    // they said it out loud and became one of the Damned (the body that takes their place is someone else's job)
    public void Turn()
    {
        Turned = true;
        Following = false;
        Clinging = false;
        FloatAt = null;
        gameObject.SetActive(false);
    }

    // silo floor checkpoint: the flood starts over, everyone back where they were hiding
    public void ResetToStart()
    {
        Drowned = false;
        Following = false;
        Clinging = false;
        FloatAt = null;
        transform.SetPositionAndRotation(startPos, startRot);
        GroundY = startPos.y;
        var it = GetComponent<Interactable>();
        if (it != null) it.enabled = true;
    }

    void SetSpeed(float s) { if (animator != null) animator.SetFloat("Speed", s); }

    // Teleporter: arrive just behind the player
    public void WarpBehind(Transform p, int index)
    {
        transform.position = p.position - p.forward * (1.5f + index) + p.right * (index % 2 == 0 ? 0.8f : -0.8f);
        SnapToGround();
    }

    // CarInteract: followers climb into the car with you
    public static void BoardAll(Vector3 carPos, float range = 25f)
    {
        foreach (var s in All)
        {
            if (s == null || !s.Following || !s.gameObject.activeInHierarchy) continue;
            if (Vector3.Distance(s.transform.position, carPos) > range) continue;
            s.Rescued = true;
            s.gameObject.SetActive(false);
        }
    }
}
