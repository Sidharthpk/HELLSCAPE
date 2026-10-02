using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

// Plays one of the two-person stab animations (a single FBX holding both characters) as a story beat.
//  cam set   -> a flashback: cut to that camera (e.g. the CCTV replay), then back to the player
//  cam null  -> watched first-person: the player is frozen and turned to face it (Derek's death in the silo)
public class StabCutscene : MonoBehaviour
{
    public GameObject actors;               // the FBX rig, hidden until played
    public Animator animator;
    public string state = "Stab";
    public float length = 10f;

    [Header("Viewing")]
    public Camera cam;                      // null = through the player's eyes
    public GameObject overlay;              // e.g. the CCTV "REC" text, shown while cam is on
    public Transform player;
    public Behaviour[] lockDuring;          // movement, punching, gun, interacting
    public ScreenFader fader;

    [Header("Sound")]
    public string soundSlot = "";           // Sound Library slot played while it rolls (empty on a flashback = "Office/CCTV stabbing")
    public AudioClip sound;                 // used when the slot is empty
    public float soundVolume = 1f;

    [Header("After")]
    public GameObject[] hideAfter;          // e.g. the killer's half of the rig: the real boss takes over from here
    public bool hideActorsAfter;            // flashbacks vanish; a body on the floor stays
    public GameObject showAfter;            // e.g. a blood pool, placed under whoever is left (the body)
    [TextArea] public string afterLine;
    public UnityEvent onFinished = new UnityEvent();

    public bool Playing { get; private set; }
    public Vector3 HiddenSpot { get; private set; }   // where the hideAfter parts stood when they were hidden

    void Start()
    {
        if (actors != null && !Playing) actors.SetActive(false);
        if (cam != null) cam.enabled = false;
        if (overlay != null) overlay.SetActive(false);
        if (showAfter != null) showAfter.SetActive(false);
        if (cam != null && string.IsNullOrEmpty(soundSlot)) soundSlot = "Office/CCTV stabbing";   // the tape of Derek and Tomas
    }

    private AudioSource audioSrc;

    void StartSound()
    {
        var clip = Sounds.Clip(soundSlot, sound);
        if (clip == null) return;
        if (audioSrc == null)
        {
            audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0f;   // it's the tape's audio: heard, not placed
        }
        audioSrc.clip = clip;
        audioSrc.loop = false;
        audioSrc.volume = soundVolume * Sounds.Volume(soundSlot);
        audioSrc.Play();
    }

    IEnumerator FadeSound(float time)
    {
        if (audioSrc == null || !audioSrc.isPlaying) yield break;
        float v0 = audioSrc.volume;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            audioSrc.volume = Mathf.Lerp(v0, 0f, t / time);
            yield return null;
        }
        audioSrc.Stop();
    }

    public void Play() { if (!Playing) StartCoroutine(Run()); }

    // atBlack runs while the screen is black at the end (swap in the boss there)
    public IEnumerator Run(Action atBlack = null)
    {
        if (Playing) yield break;
        Playing = true;
        Lock(true);

        if (cam != null) yield return Fade(1f, 0.4f);
        actors.SetActive(true);
        animator.Play(state, 0, 0f);
        StartSound();
        if (cam != null)
        {
            cam.enabled = true;
            if (overlay != null) overlay.SetActive(true);
            yield return Fade(0f, 0.6f);
        }
        else if (player != null)
        {
            Vector3 to = actors.transform.position - player.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) player.rotation = Quaternion.LookRotation(to);
        }

        yield return new WaitForSeconds(length);

        StartCoroutine(FadeSound(0.6f));
        yield return Fade(1f, cam != null ? 0.5f : 0.15f);
        if (hideAfter != null && hideAfter.Length > 0)
        {
            HiddenSpot = CentreOf(hideAfter);
            foreach (var g in hideAfter) if (g != null) g.SetActive(false);
        }
        if (showAfter != null)
        {
            showAfter.transform.position = CentreOf(actors.GetComponentsInChildren<Renderer>().Select(r => r.gameObject).ToArray()) + Vector3.up * 0.02f;
            showAfter.SetActive(true);
        }
        if (hideActorsAfter) actors.SetActive(false);
        if (cam != null) cam.enabled = false;
        if (overlay != null) overlay.SetActive(false);
        atBlack?.Invoke();
        yield return Fade(0f, cam != null ? 0.8f : 0.3f);

        Lock(false);
        Playing = false;
        if (!string.IsNullOrEmpty(afterLine) && DialogueBox.Instance != null) DialogueBox.Instance.Say(afterLine);
        onFinished.Invoke();
    }

    void Lock(bool on)
    {
        if (lockDuring == null) return;
        foreach (var b in lockDuring) if (b != null) b.enabled = !on;
        if (on && player != null && player.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.linearVelocity = Vector3.zero;
    }

    IEnumerator Fade(float to, float time)
    {
        if (fader != null) yield return fader.FadeTo(to, time);
    }

    static Vector3 CentreOf(GameObject[] parts)
    {
        Bounds? b = null;
        foreach (var g in parts)
        {
            if (g == null) continue;
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                if (b == null) b = r.bounds;
                else { var bb = b.Value; bb.Encapsulate(r.bounds); b = bb; }
            }
        }
        if (b == null) return Vector3.zero;
        var c = b.Value.center;
        c.y = b.Value.min.y;
        return c;
    }
}
