using System.Collections;
using UnityEngine;

// One of the apartment kit's doors, usable with F like everything else (the kit's own scripts want a mouse click
// and are switched off), and openable from the story: the flat door is left ajar, the killer creeps out of the
// closet and flings the stairwell doors open as he runs.
[RequireComponent(typeof(Interactable))]
public class PrologueDoor : MonoBehaviour
{
    public Animator anim;                   // the kit's door animator
    public string openState = "Opening", closeState = "Closing";   // closets: ClosetOpening / ClosetClosing
    public bool startOpen;
    public AudioSource sfx;
    public AudioClip openClip, slamClip;    // procedural creak / thud if empty

    private Interactable it;

    public bool IsOpen { get; private set; }

    IEnumerator Start()
    {
        it = GetComponent<Interactable>();
        it.once = false;
        it.onUse.AddListener(Toggle);
        openClip = Sounds.Clip("Prologue/Door creak", openClip != null ? openClip : ProceduralAudio.Creak(0.9f));
        slamClip = Sounds.Clip("Prologue/Door flung open", slamClip != null ? slamClip : ProceduralAudio.Thud());
        yield return null;                  // after the animator's first update
        if (startOpen) Set(true, instant: true);
        UpdatePrompt();
    }

    public void Toggle() => Set(!IsOpen);
    public void Open() => Set(true);

    // flung open by someone running through: faster, louder
    public void Burst()
    {
        if (IsOpen) return;
        Set(true);
        anim.speed = 2.2f;
        Play(slamClip, 0.8f);
    }

    public void Set(bool open, bool instant = false)
    {
        if (IsOpen == open) return;
        IsOpen = open;
        anim.speed = 1f;
        anim.Play(open ? openState : closeState, 0, instant ? 1f : 0f);
        if (!instant) Play(openClip, 0.35f);
        UpdatePrompt();
    }

    void UpdatePrompt()
    {
        if (it != null) it.prompt = IsOpen ? "Close door" : "Open door";
    }

    void Play(AudioClip c, float volume)
    {
        if (sfx != null && c != null) sfx.PlayOneShot(c, volume);
    }
}
