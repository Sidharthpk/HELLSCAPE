using System.Collections;
using UnityEngine;

// Kessler's office door (the user's room-door model). Locked until the keypad code is in; then the latch clicks and
// the door swings open on the model's own take (it opens between 1 and ~3 s, then would close again: we hold it open).
public class KesslerDoor : MonoBehaviour
{
    public Animator door;                   // "Open" state = the model's DoorOpen take
    public float clipLength = 7.5f;
    public float openFrom = 1f, openTo = 3.1f;
    public Collider blocker;                // fills the doorway until it's open
    public Interactable lockedPrompt;       // "Locked." on the blocker, switched off once open
    public bool echoOnly;                   // the door only opens in the Echo and is fused shut again in the red city
    public AudioSource sfx;
    public AudioClip latch, creak;          // procedural stand-ins if empty

    private bool open, echo;
    private string lockedEcho;
    const string FusedLine = "The lock and hinges are fused shut. This doorway only works in the Echo.";

    void Start()
    {
        if (door != null) { door.Play("Open", 0, 0f); door.speed = 0f; }   // shut
    }

    public void Unlock()
    {
        if (echoOnly && !echo) return;
        if (!open) StartCoroutine(Open());
    }

    IEnumerator Open()
    {
        open = true;
        if (lockedPrompt != null) lockedPrompt.enabled = false;
        if (sfx != null) Sounds.OneShot(sfx, "Office/Kessler door latch", latch != null ? latch : ProceduralAudio.Click(), 1f);
        yield return new WaitForSeconds(0.5f);
        if (sfx != null) Sounds.OneShot(sfx, "Office/Kessler door creak", creak != null ? creak : ProceduralAudio.Creak(), 0.8f);
        if (door != null)
        {
            door.Play("Open", 0, openFrom / clipLength);
            door.speed = 1f;
        }
        yield return new WaitForSeconds((openTo - openFrom) * 0.5f);
        if (blocker != null) blocker.enabled = false;   // wide enough to walk through
        yield return new WaitForSeconds((openTo - openFrom) * 0.5f);
        if (door != null) door.speed = 0f;              // hold it open
    }

    public void ReleaseForEscape()
    {
        StopAllCoroutines(); echoOnly = false; open = true;
        if (blocker != null) blocker.enabled = false;
        if (lockedPrompt != null) lockedPrompt.enabled = false;
        if (door != null) { door.Play("Open", 0, openTo / clipLength); door.speed = 0f; door.Update(0f); }
    }

    // Leaving the Echo shuts an opened door again; entering it restores the open door.
    public void ApplyRealm(bool on)
    {
        echo = on;
        if (echoOnly) StopAllCoroutines();
        if (!echoOnly) return;
        if (lockedPrompt != null)
        {
            if (lockedEcho == null) lockedEcho = lockedPrompt.line;
            lockedPrompt.line = on ? lockedEcho : FusedLine;
        }
        if (!open) return;
        if (blocker != null) blocker.enabled = !on;
        if (lockedPrompt != null) lockedPrompt.enabled = !on;
        if (door != null) { door.Play("Open", 0, on ? openTo / clipLength : 0f); door.speed = 0f; door.Update(0f); }
    }
}
