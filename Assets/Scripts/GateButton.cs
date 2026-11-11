using UnityEngine;
using System.Collections;

public class GateButton : MonoBehaviour
{
    public Transform gate;
    public Vector3 slideOffset = new Vector3(3f, 0f, 0f);
    public float slideDuration = 2f;
    public string openedLine = "Where the hell am I...?";  // said once the door is fully open (skipped if onOpened is used)
    public UnityEngine.Events.UnityEvent onOpened = new UnityEngine.Events.UnityEvent();          // e.g. GateCutscene.Play

    private bool activated = false;
    private Vector3 closedPosition;
    private Vector3 openPosition;

    void Awake()
    {
        closedPosition = gate.position;
        openPosition = closedPosition + gate.right * slideOffset.x + gate.up * slideOffset.y + gate.forward * slideOffset.z;
    }

    // snap open/closed without animating (SleepSequence: open in the day, shut when you wake in hell)
    public void SetOpen(bool open)
    {
        StopAllCoroutines();
        activated = open;
        gate.position = open ? openPosition : closedPosition;
    }

    public void OnPunched()
    {
        if (activated) return;
        activated = true;
        StartCoroutine(SlideGate());
    }

    IEnumerator SlideGate()
    {
        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            gate.position = Vector3.Lerp(closedPosition, openPosition, t / slideDuration);
            yield return null;
        }
        gate.position = openPosition;
        if (onOpened.GetPersistentEventCount() > 0) onOpened.Invoke();
        else if (!string.IsNullOrEmpty(openedLine) && DialogueBox.Instance != null) DialogueBox.Instance.Say(openedLine);
    }
}