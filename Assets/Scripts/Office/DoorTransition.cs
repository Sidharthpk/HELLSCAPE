using System.Collections;
using UnityEngine;

// The Resident Evil door: every time you go through a Teleporter the screen cuts to black, then to a lone door
// in the dark (the user's room-door model, on a stage far off the map). The latch clicks, the door creaks open
// away from you and the camera pushes slowly through the doorway into the black, then the next area fades in.
// Teleporter.Go calls Play() and moves the player while the screen is black at the end.
public class DoorTransition : MonoBehaviour
{
    public Camera cam;                      // the stage camera, disabled when idle
    public Animator door;                   // plays "Open" (the model's own open-and-close take)
    public Transform camStart, camEnd;      // the push: in front of the door -> through the doorway
    public ScreenFader fader;
    public Behaviour[] lockDuring;          // player movement / look / interaction

    [Header("Timing")]
    public float holdBeforeOpen = 0.9f;     // staring at the closed door
    public float clipLength = 7.5f;         // the model's take: opens 1.0-3.0 s, closes again 5.0-7.5 s
    public float doorOpenAt = 1.0f;         // the clip's door starts moving here...
    public float pushTime = 3.2f;           // ...and the camera pushes in over this long
    public float fadeTime = 0.35f;

    [Header("Sound")]
    public AudioSource sfx;                 // 2D
    public AudioClip latchClip, creakClip, stepClip;   // procedural stand-ins if empty
    public AudioClip fullClip;              // one recording of the whole door (Sound Manager: General/Door transition (full)) - replaces the three
    public float fullLatchAt = 2.35f;       // where the latch is in that recording: it's lined up with the door starting to open

    private bool busy;
    public bool Busy => busy;

    void Awake()
    {
        if (cam != null) cam.enabled = false;
        if (door != null) door.gameObject.SetActive(false);
    }

    // ends on a black screen: the caller moves the player, then fades back in
    public IEnumerator Play()
    {
        busy = true;
        var wasOn = new bool[lockDuring.Length];   // only hand back what was on (the rifle isn't yours yet in the office)
        for (int i = 0; i < lockDuring.Length; i++)
            if (lockDuring[i] != null) { wasOn[i] = lockDuring[i].enabled; lockDuring[i].enabled = false; }
        if (fader != null) yield return fader.FadeTo(1f, fadeTime);

        latchClip = Sounds.Clip("General/Door transition latch", latchClip != null ? latchClip : ProceduralAudio.Click());
        creakClip = Sounds.Clip("General/Door transition creak", creakClip != null ? creakClip : ProceduralAudio.Creak());
        stepClip = Sounds.Clip("General/Door transition step", stepClip != null ? stepClip : ProceduralAudio.Thud());
        fullClip = Sounds.Clip("General/Door transition (full)", fullClip);
        bool full = fullClip != null && sfx != null;

        door.gameObject.SetActive(true);
        door.Play("Open", 0, 0f);
        door.speed = 0f;                    // held shut until the latch
        cam.transform.SetPositionAndRotation(camStart.position, camStart.rotation);
        cam.enabled = true;
        if (fader != null) yield return fader.FadeTo(0f, fadeTime);

        yield return new WaitForSeconds(holdBeforeOpen);
        if (full)
        {
            sfx.clip = fullClip;
            sfx.volume = Sounds.Volume("General/Door transition (full)");
            sfx.time = Mathf.Clamp(fullLatchAt - 0.05f, 0f, fullClip.length - 0.1f);
            sfx.Play();   // plays on through the black: the door shuts behind you as the next area comes up
        }
        else if (sfx != null) sfx.PlayOneShot(latchClip, 0.9f * Sounds.Volume("General/Door transition latch"));
        door.Play("Open", 0, doorOpenAt / clipLength);
        door.speed = 1f;
        yield return new WaitForSeconds(0.12f);
        if (sfx != null && !full) sfx.PlayOneShot(creakClip, 0.8f * Sounds.Volume("General/Door transition creak"));

        // slow push through the doorway with a faint walking sway, fading out as it enters the dark
        bool fading = false;
        for (float t = 0f; t < pushTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / pushTime);
            Vector3 sway = new Vector3(Mathf.Sin(t * 4.2f) * 0.015f, Mathf.Abs(Mathf.Sin(t * 4.2f)) * 0.02f, 0f);
            cam.transform.position = Vector3.Lerp(camStart.position, camEnd.position, k) + cam.transform.rotation * sway;
            cam.transform.rotation = Quaternion.Slerp(camStart.rotation, camEnd.rotation, k);
            if (!fading && t > pushTime - fadeTime * 2f)
            {
                fading = true;
                if (sfx != null && !full) sfx.PlayOneShot(stepClip, 0.35f * Sounds.Volume("General/Door transition step"));
                if (fader != null) StartCoroutine(fader.FadeTo(1f, fadeTime * 2f));
            }
            yield return null;
        }
        if (fader != null) fader.fade.alpha = 1f;

        cam.enabled = false;
        door.gameObject.SetActive(false);
        for (int i = 0; i < lockDuring.Length; i++) if (lockDuring[i] != null && wasOn[i]) lockDuring[i].enabled = true;
        busy = false;
    }
}
