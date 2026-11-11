using UnityEngine;

// The van's dash radio in the prologue: look at it from the driver's seat and press R. The song fades in at 0:25 into the track and
// fades out when it's switched off. Leave the van with it playing and it goes muffled and quiet, as if it's still on in the cab.
public class VanRadio : MonoBehaviour
{
    public PrologueVan van;
    public Transform radio;                 // the dash unit (AlpineRadio)
    public AudioClip song;
    public float startAt = 25f, volume = 0.22f, fadeTime = 2.5f, lookAngle = 14f;
    public KeyCode key = KeyCode.R;

    AudioSource src;
    AudioLowPassFilter lp;
    bool on, started;
    float level, outside;                   // level: fade 0..1; outside: 0 in the cab .. 1 out of it (smoothed)
    public float Level => level;            // (VanCabinAudio ducks the rain and engine under it)

    void Start()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.clip = song; src.loop = true; src.playOnAwake = false; src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 2f; src.maxDistance = 25f;
        lp = gameObject.AddComponent<AudioLowPassFilter>();
        if (radio == null) radio = FindDeep(transform, "AlpineRadio");
    }

    static Transform FindDeep(Transform t, string n)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == n) return c;
        return null;
    }

    bool Looking()
    {
        var cam = van.vanCamera != null ? van.vanCamera.transform : null;
        return van.InVan && cam != null && radio != null && Vector3.Angle(cam.forward, radio.position - cam.position) < lookAngle;
    }

    void Update()
    {
        bool look = Looking();
        van.radioHint = look ? (on ? "Turn off the radio" : "Turn on the radio") : null;
        if (look && Input.GetKeyDown(key))
        {
            on = !on;
            if (on)
            {
                if (!started) { src.time = song.length > startAt + 5f ? startAt : 0f; src.Play(); started = true; }
                else src.UnPause();
            }
        }

        level = Mathf.MoveTowards(level, on ? 1f : 0f, Time.deltaTime / fadeTime);
        if (!on && level <= 0f && started) src.Pause();

        outside = Mathf.MoveTowards(outside, van.InVan ? 0f : 1f, Time.deltaTime * 2f);
        src.volume = level * volume * Mathf.Lerp(1f, .45f, outside);
        src.spatialBlend = outside * .9f;                                   // (heard from the van's place, not the listener's head)
        lp.cutoffFrequency = Mathf.Lerp(22000f, 650f, outside);             // (through the van's body: all bass, no highs)
    }
}
