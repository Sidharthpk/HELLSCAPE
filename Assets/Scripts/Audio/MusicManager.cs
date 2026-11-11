using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Music per area, from the SoundLibrary. Lives across scene loads (made on first use). The story calls
// MusicManager.Area("Office") etc. as you move about; each area's music (and optional ambience loop)
// crossfades in, and an area with no music assigned fades the music out.
public class MusicManager : MonoBehaviour
{
    static MusicManager instance;

    AudioSource[] music = new AudioSource[2];
    AudioSource ambience;
    int current;
    string area;
    Coroutine fading, ambFading;

    public static string CurrentArea => instance != null ? instance.area : null;

    static MusicManager Get()
    {
        if (instance == null)
        {
            var go = new GameObject("MusicManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MusicManager>();
        }
        return instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { instance = null; overrides.Clear(); }

    // music an area picks up during play (e.g. the hell city's street trigger): used instead of the library's
    static readonly System.Collections.Generic.Dictionary<string, (AudioClip clip, float volume)> overrides =
        new System.Collections.Generic.Dictionary<string, (AudioClip, float)>();

    // give an area this music from now on, and play it now if that's where you are (or startNow)
    public static void SetAreaMusic(string areaName, AudioClip clip, float volume, float fade = -1f, bool startNow = true)
    {
        if (string.IsNullOrEmpty(areaName)) return;
        overrides[areaName] = (clip, volume);
        if (!startNow) return;
        var m = Get();
        m.area = null;                 // force the switch even if we're already in that area
        Area(areaName, fade);
    }

    void Awake()
    {
        for (int i = 0; i < 2; i++) music[i] = Source(true);
        ambience = Source(true);
    }

    AudioSource Source(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        s.volume = 0f;
        s.ignoreListenerPause = false;
        return s;
    }

    // does this area have music of its own in the library?
    public static bool HasMusic(string areaName)
    {
        var lib = Sounds.Library;
        var a = lib != null ? lib.FindArea(areaName) : null;
        return a != null && a.music != null;
    }

    // switch to an area's music; the same area again does nothing
    public static void Area(string areaName) => Area(areaName, -1f);

    // ...with a fade of your own (a slow swell in after a cutscene); fade < 0 uses the library's crossfade
    public static void Area(string areaName, float fadeSeconds)
    {
        if (string.IsNullOrEmpty(areaName)) return;
        var lib = Sounds.Library;
        if (lib == null) return;
        var m = Get();
        if (m.area == areaName) return;
        m.area = areaName;
        var a = lib.FindArea(areaName);
        float fade = fadeSeconds >= 0f ? fadeSeconds : lib.crossfadeSeconds;
        if (overrides.TryGetValue(areaName, out var o) && (a == null || a.music == null))   // the library's own music wins
            m.CrossTo(o.clip, o.volume * lib.masterMusicVolume, fade);
        else
            m.CrossTo(a != null ? a.music : null, a != null ? a.musicVolume * lib.masterMusicVolume : 0f, fade);
        m.AmbienceTo(a != null ? a.ambience : null, a != null ? a.ambienceVolume : 0f, fade);
    }

    // fade everything out (the fall off the roof, a cutscene that wants silence)
    public static void Stop(float fade = 1.5f)
    {
        if (instance == null) return;
        instance.area = null;
        instance.CrossTo(null, 0f, fade);
        instance.AmbienceTo(null, 0f, fade);
    }

    void CrossTo(AudioClip clip, float volume, float fade)
    {
        var from = music[current];
        if (clip != null && from.clip == clip && from.isPlaying)
        {
            if (fading != null) StopCoroutine(fading);
            fading = StartCoroutine(Fade(from, null, volume, fade));
            return;
        }
        current = 1 - current;
        var to = music[current];
        to.clip = clip;
        to.volume = 0f;
        if (clip != null) to.Play();
        if (fading != null) StopCoroutine(fading);
        fading = StartCoroutine(Fade(to, from, volume, fade));
    }

    void AmbienceTo(AudioClip clip, float volume, float fade)
    {
        if (ambFading != null) StopCoroutine(ambFading);
        ambFading = StartCoroutine(SwapAmbience(clip, volume, fade));
    }

    IEnumerator SwapAmbience(AudioClip clip, float volume, float fade)
    {
        if (ambience.clip != clip)
        {
            float v0 = ambience.volume;
            for (float t = 0f; t < fade * 0.5f && ambience.isPlaying; t += Time.unscaledDeltaTime)
            {
                ambience.volume = Mathf.Lerp(v0, 0f, t / (fade * 0.5f));
                yield return null;
            }
            ambience.Stop();
            ambience.clip = clip;
            ambience.volume = 0f;
            if (clip != null) ambience.Play();
        }
        float a0 = ambience.volume;
        for (float t = 0f; t < fade * 0.5f; t += Time.unscaledDeltaTime)
        {
            ambience.volume = Mathf.Lerp(a0, volume, t / (fade * 0.5f));
            yield return null;
        }
        ambience.volume = volume;
    }

    static IEnumerator Fade(AudioSource up, AudioSource down, float volume, float time)
    {
        float u0 = up.volume, d0 = down != null ? down.volume : 0f;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            up.volume = Mathf.Lerp(u0, volume, k);
            if (down != null) down.volume = Mathf.Lerp(d0, 0f, k);
            yield return null;
        }
        up.volume = volume;
        if (up.clip == null || volume <= 0f) up.Stop();
        if (down != null) { down.volume = 0f; down.Stop(); }
    }
}
