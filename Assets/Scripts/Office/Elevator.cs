using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// The freight elevator in the silo complex: hit the lift switch beside it (Open), the doors grind open,
// step in and they shut behind you (on the killer's face) while the car crawls down to the lower walkway.
// Ride() still works as a button inside the car.
public class Elevator : MonoBehaviour
{
    public Rigidbody car;                   // kinematic; its colliders carry the player
    public float drop = 13.8f;
    public float travelTime = 7f;
    public GameObject[] topDoors;           // closed until the lift switch is hit (open at start if requireSwitch is off)
    public bool requireSwitch = true;
    public float boardDelay = 0.6f;         // once inside an open car, it leaves after this long
    public float doorCloseDelay = 0f;       // doors stay open this long after onDepart
    public GameObject[] bottomDoors;        // closed until the car arrives
    public Transform player;
    public Bounds inside;                   // world-space box the player must be in (car at the top)
    public AudioSource sfx;                 // clanks; rumble while moving
    [TextArea] public string notInsideLine = "The button... I need to be inside first.";
    [TextArea] public string openLine = "*clank* The lift doors grind open. GET IN.";
    public UnityEvent onOpen = new UnityEvent();
    public UnityEvent onDepart = new UnityEvent();
    public UnityEvent onArrive = new UnityEvent();

    private bool used, open;
    private float insideFor;

    void Start()
    {
        open = !requireSwitch;
        foreach (var d in topDoors) if (d != null) d.SetActive(!open);
        foreach (var d in bottomDoors) if (d != null) d.SetActive(true);
    }

    // the lift switch
    public void Open()
    {
        if (open || used) return;
        open = true;
        StartCoroutine(OpenDoors());
    }

    IEnumerator OpenDoors()
    {
        Sounds.OneShot(sfx, "Silo Complex/Lift doors", ProceduralAudio.Thud(), 0.8f);
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(openLine)) DialogueBox.Instance.Say(openLine);
        yield return new WaitForSeconds(0.4f);
        foreach (var d in topDoors) if (d != null) d.SetActive(false);
        onOpen.Invoke();
    }

    void Update()
    {
        if (!open || used || player == null) return;
        insideFor = inside.Contains(player.position) ? insideFor + Time.deltaTime : 0f;
        if (insideFor >= boardDelay) Ride();
    }

    public void Ride()
    {
        if (used) return;
        if (!open) { Open(); return; }
        if (!inside.Contains(player.position))
        {
            if (DialogueBox.Instance != null) DialogueBox.Instance.Say(notInsideLine);
            return;
        }
        used = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        onDepart.Invoke();
        if (doorCloseDelay > 0f) yield return new WaitForSeconds(doorCloseDelay);   // e.g. the killer's sprint cutscene
        foreach (var d in topDoors) if (d != null) d.SetActive(true);
        Sounds.OneShot(sfx, "Silo Complex/Lift doors", ProceduralAudio.Thud(), 0.9f);
        if (sfx != null) { if (sfx.clip == null) sfx.clip = Sounds.Clip("Silo Complex/Lift ride rumble", ProceduralAudio.Rumble()); sfx.loop = true; sfx.Play(); }
        yield return new WaitForSeconds(0.8f);

        Vector3 from = car.position, to = from - Vector3.up * drop;
        for (float t = 0f; t < travelTime; t += Time.fixedDeltaTime)
        {
            car.MovePosition(Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / travelTime)));
            yield return new WaitForFixedUpdate();
        }
        car.MovePosition(to);

        if (sfx != null) sfx.Stop();
        yield return new WaitForSeconds(0.5f);
        foreach (var d in bottomDoors) if (d != null) d.SetActive(false);
        onArrive.Invoke();
    }
}
