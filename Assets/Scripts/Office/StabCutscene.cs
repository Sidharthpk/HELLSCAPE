using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

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
    public string recordingStamp;           // explicit camera identity, applied after legacy text migration
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

    Volume look;            // the tape: grey, grainy, bent (same grade as Derek's tape in the prologue)
    GameObject scanlines;
    string stamp;
    Quaternion camRest;

    public Func<IEnumerator> sequence;      // set by a script on this object (TomasTape): runs in place of `length`
    [NonSerialized] public bool keepControlsLocked; // CCTV playlist owns the input lock across camera changes
    [NonSerialized] public bool holdBlackAfter;      // next recording opens directly from the black cut

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

    void TapeOn(bool on)
    {
        if (cam == null) return;
        if (on && look == null)
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            var ca = p.Add<ColorAdjustments>(); ca.saturation.Override(-100f); ca.contrast.Override(25f); ca.postExposure.Override(0.4f);
            var fg = p.Add<FilmGrain>(); fg.type.Override(FilmGrainLookup.Large02); fg.intensity.Override(1f);
            var vg = p.Add<Vignette>(); vg.intensity.Override(0.5f);
            var ab = p.Add<ChromaticAberration>(); ab.intensity.Override(0.45f);
            var ld = p.Add<LensDistortion>(); ld.intensity.Override(-0.25f);
            look = new GameObject("Tape look").AddComponent<Volume>();
            look.transform.SetParent(transform, false);
            look.isGlobal = true; look.priority = 20; look.profile = p;
        }
        if (look != null) look.weight = on ? 1f : 0f;
        if (on && scanlines == null && overlay != null && overlay.transform.parent != null)
        {
            var tex = new Texture2D(1, 4) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            for (int y = 0; y < 4; y++) tex.SetPixel(0, y, new Color(0f, 0f, 0f, y == 0 ? 0.35f : 0f));
            tex.Apply();
            scanlines = new GameObject("Tape scanlines", typeof(RectTransform));
            scanlines.transform.SetParent(overlay.transform.parent, false);
            var rt = (RectTransform)scanlines.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetSiblingIndex(overlay.transform.GetSiblingIndex());
            var img = scanlines.AddComponent<RawImage>(); img.texture = tex; img.uvRect = new Rect(0f, 0f, 1f, 270f); img.raycastTarget = false;
        }
        if (scanlines != null) scanlines.SetActive(on);
    }

    // ticking clock, PLAY mark and the odd tracking glitch
    IEnumerator Tape()
    {
        var text = overlay != null ? overlay.GetComponent<TMPro.TMP_Text>() : null;
        if (text != null && stamp == null) stamp = !string.IsNullOrEmpty(recordingStamp) ? recordingStamp : text.text;
        camRest = cam.transform.localRotation;
        for (float s = 0f; Playing && cam.enabled; s += Time.deltaTime)
        {
            if (text != null) text.text = stamp + $":{(int)s % 60:00}   <color=#e0e0e0>PLAY ></color>";
            bool glitch = UnityEngine.Random.value < 0.05f;
            cam.transform.localRotation = camRest * Quaternion.Euler(glitch ? UnityEngine.Random.Range(-0.6f, 0.6f) : 0f, glitch ? UnityEngine.Random.Range(-0.6f, 0.6f) : 0f, 0f);
            if (scanlines != null) scanlines.transform.localPosition = new Vector3(0f, glitch ? UnityEngine.Random.Range(-18f, 18f) : 0f, 0f);
            yield return null;
        }
        cam.transform.localRotation = camRest;
    }

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
        // Replacement actors have their own Animator. Restart every participant on the
        // same frame, including replays, instead of relying on OnEnable's default state.
        foreach (var actor in actors.GetComponentsInChildren<Animator>())
        {
            if (actor.runtimeAnimatorController == null || !actor.HasState(0, Animator.StringToHash(state))) continue;
            actor.speed = 1f;
            actor.Play(state, 0, 0f);
            actor.Update(0f);
        }
        StartSound();
        if (cam != null)
        {
            cam.enabled = true;
            if (overlay != null) overlay.SetActive(true);
            TapeOn(true);
            StartCoroutine(Tape());
            yield return Fade(0f, 0.6f);
        }
        else if (player != null)
        {
            Vector3 to = actors.transform.position - player.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) player.rotation = Quaternion.LookRotation(to);
        }

        yield return sequence != null ? sequence() : new WaitForSeconds(length);

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
        TapeOn(false);
        atBlack?.Invoke();
        if (!holdBlackAfter) yield return Fade(0f, cam != null ? 0.8f : 0.3f);

        Lock(false);
        Playing = false;
        if (!string.IsNullOrEmpty(afterLine) && DialogueBox.Instance != null) DialogueBox.Instance.Say(afterLine);
        onFinished.Invoke();
    }

    void Lock(bool on)
    {
        if (!on && keepControlsLocked) return;
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
