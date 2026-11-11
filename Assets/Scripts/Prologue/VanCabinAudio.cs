using UnityEngine;

// Inside the van's cab the rain on the roof and the engine come through the bodywork: muffled (low-pass) and a little quieter.
// With the radio on they duck a bit more under the song. Out of the van they go back to normal (the rain's filter is left alone
// then, so the flat's own indoor muffle still works). The rain has its own object (Street ambience) so only it is filtered.
[DefaultExecutionOrder(300)]   // (after anything that sets their volume this frame)
public class VanCabinAudio : MonoBehaviour
{
    public PrologueVan van;
    public VanRadio radio;
    public AudioSource rain;                    // PrologueDirector.ambience when left empty
    public float rainCutoff = 900f, engineCutoff = 1600f;   // Hz in the cab
    public float cabVolume = 0.75f, radioDuck = 0.6f;       // volume in the cab; and under the radio at full

    class Ducked { public AudioSource src; public AudioLowPassFilter lp; public float baseVol = -1f, written = -1f; }
    Ducked r, e;
    float inCab;

    void Start()
    {
        if (rain == null) { var d = FindFirstObjectByType<PrologueDirector>(); if (d != null) rain = d.ambience; }
        r = Make(rain); e = Make(van != null ? van.engine : null);
    }

    static Ducked Make(AudioSource s) => s == null ? null : new Ducked { src = s };

    void LateUpdate()
    {
        inCab = Mathf.MoveTowards(inCab, van != null && van.InVan ? 1f : 0f, Time.deltaTime * 2f);
        float song = radio != null ? radio.Level : 0f;
        float mul = Mathf.Lerp(1f, cabVolume * Mathf.Lerp(1f, radioDuck, song), inCab);
        Apply(r, mul, rainCutoff);
        Apply(e, mul, engineCutoff);
    }

    void Apply(Ducked d, float mul, float cutoff)
    {
        if (d == null || d.src == null) return;
        // (others fade these sources too: whatever they last set is the base, this only scales it)
        if (d.written < 0f || !Mathf.Approximately(d.src.volume, d.written)) d.baseVol = d.src.volume;
        if (inCab > 0f || d.written >= 0f)
        {
            d.src.volume = d.baseVol * mul;
            d.written = inCab > 0f ? d.src.volume : -1f;   // (fully out: stop scaling, the base is the source's own again)
        }
        if (inCab > 0f)
        {
            if (d.lp == null) d.lp = d.src.GetComponent<AudioLowPassFilter>();
            if (d.lp == null) d.lp = d.src.gameObject.AddComponent<AudioLowPassFilter>();
            d.lp.enabled = true;
            d.lp.cutoffFrequency = Mathf.Lerp(22000f, cutoff, inCab);
        }
        else if (d.lp != null && d.lp.cutoffFrequency > 21000f) d.lp.cutoffFrequency = 22000f;
    }
}
