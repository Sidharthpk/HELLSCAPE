using UnityEngine;

// Looks sounds up in the SoundLibrary by "Area/Slot name". Every call takes the game's own sound as a fallback,
// so an empty slot changes nothing.
public static class Sounds
{
    static SoundLibrary lib;
    static bool loaded;

    public static SoundLibrary Library
    {
        get
        {
            if (!loaded) { lib = Resources.Load<SoundLibrary>("SoundLibrary"); loaded = true; }
            return lib;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { lib = null; loaded = false; }

    static SoundLibrary.Slot Slot(string id) => Library != null ? Library.Find(id) : null;

    // the library's clip (a random one if there are several), or the fallback
    public static AudioClip Clip(string id, AudioClip fallback)
    {
        var s = Slot(id);
        if (s == null || s.clips == null || s.clips.Length == 0) return fallback;
        var c = s.clips[Random.Range(0, s.clips.Length)];
        return c != null ? c : fallback;
    }

    // all of the slot's clips, or the fallback set
    public static AudioClip[] Clips(string id, AudioClip[] fallback)
    {
        var s = Slot(id);
        if (s == null || s.clips == null || s.clips.Length == 0) return fallback;
        foreach (var c in s.clips) if (c == null) return fallback;
        return s.clips;
    }

    public static bool Has(string id)
    {
        var s = Slot(id);
        return s != null && s.clips != null && s.clips.Length > 0 && s.clips[0] != null;
    }

    // the slot's volume slider (1 when there's no slot)
    public static float Volume(string id) { var s = Slot(id); return s != null ? s.volume : 1f; }

    // a one-shot through the library
    public static void OneShot(AudioSource src, string id, AudioClip fallback, float volume = 1f)
    {
        if (src == null) return;
        var c = Clip(id, fallback);
        if (c != null) src.PlayOneShot(c, volume * Volume(id));
    }

    // put the library's clip (and volume) on a source; call before it plays
    public static void Apply(AudioSource src, string id)
    {
        if (src == null) return;
        var s = Slot(id);
        if (s == null) return;
        bool wasPlaying = src.isPlaying;
        if (s.clips != null && s.clips.Length > 0 && s.clips[0] != null && src.clip != s.clips[0])
        {
            src.clip = Clip(id, src.clip);
            if (wasPlaying) src.Play();
        }
        src.volume *= s.volume;
    }
}
