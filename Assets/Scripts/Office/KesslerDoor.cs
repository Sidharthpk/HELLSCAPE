using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Kessler's office door (the user's room-door model). Locked until the keypad code is in; then the latch clicks and
// the door swings open on the model's own take (it opens between 1 and ~3 s, then would close again: we hold it open).
// The office's room doors are the same model with this on them (OfficeEchoDoors): [F] in the Echo, quick.
public class KesslerDoor : MonoBehaviour, ISceneProgress
{
    public Animator door;                   // "Open" state = the model's DoorOpen take
    public float clipLength = 7.5f;
    public float openFrom = 1f, openTo = 3.1f;
    public float speed = 1f;                // how fast the take plays
    public float latchDelay = 0.5f;
    public Collider blocker;                // fills the doorway until it's open
    public NavMeshObstacle carve;           // optional: cuts the walk map while the door is shut
    public Interactable lockedPrompt;       // "Locked." on the blocker, switched off once open
    public bool echoOnly;                   // the door only opens in the Echo and is fused shut again in the red city
    public AudioSource sfx;
    public AudioClip latch, creak;          // procedural stand-ins if empty

    private bool open, echo;
    private string lockedEcho;
    const string FusedLine = "The lock and hinges are fused shut. This doorway only works in the Echo.";

    public object CaptureProgress() => open;
    public void RestoreProgress(object state)
    {
        StopAllCoroutines();
        open = (bool)state;
        bool opened = open && (!echoOnly || echo);
        Block(!opened);
        if (lockedPrompt != null) lockedPrompt.enabled = !opened;
        Pose(opened);
    }

    static readonly List<KesslerDoor> all = new List<KesslerDoor>();
    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    // a shut door between a and b (the shadows can't pass one)
    public static bool Shut(Vector3 a, Vector3 b)
    {
        var ray = new Ray(a, b - a); float d = Vector3.Distance(a, b);
        foreach (var k in all) if (k.blocker != null && k.blocker.enabled && k.blocker.Raycast(ray, out _, d)) return true;
        return false;
    }

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
        yield return new WaitForSeconds(latchDelay);
        if (sfx != null) Sounds.OneShot(sfx, "Office/Kessler door creak", creak != null ? creak : ProceduralAudio.Creak(), 0.8f);
        if (door != null)
        {
            door.Play("Open", 0, openFrom / clipLength);
            door.speed = speed;
        }
        yield return new WaitForSeconds((openTo - openFrom) * 0.5f / speed);
        Block(false);   // wide enough to walk through
        yield return new WaitForSeconds((openTo - openFrom) * 0.5f / speed);
        if (door != null) door.speed = 0f;              // hold it open
    }

    void Block(bool shut)
    {
        if (blocker != null) blocker.enabled = shut;
        if (carve != null) carve.enabled = shut;
    }

    void Pose(bool opened)
    {
        if (door != null) { door.Play("Open", 0, opened ? openTo / clipLength : 0f); door.speed = 0f; door.Update(0f); }
    }

    public void ReleaseForEscape()
    {
        StopAllCoroutines(); echoOnly = false; open = true;
        Block(false);
        if (lockedPrompt != null) lockedPrompt.enabled = false;
        Pose(true);
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
        Block(!on);
        if (lockedPrompt != null) lockedPrompt.enabled = !on;
        Pose(on);
    }
}
