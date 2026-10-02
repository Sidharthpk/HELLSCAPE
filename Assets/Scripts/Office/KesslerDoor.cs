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
    public AudioSource sfx;
    public AudioClip latch, creak;          // procedural stand-ins if empty

    private bool open;

    void Start()
    {
        if (door != null) { door.Play("Open", 0, 0f); door.speed = 0f; }   // shut
    }

    public void Unlock()
    {
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
}
