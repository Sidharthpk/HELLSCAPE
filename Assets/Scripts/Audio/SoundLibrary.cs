using System.Collections.Generic;
using UnityEngine;

// Every sound and piece of music in HellScape, in one place (Assets/Resources/SoundLibrary.asset; open it with
// HellScape > Sound Manager). Grouped by scene / area. Leave a clip empty and the game keeps its built-in
// sound; drop your own in and it's used instead. Several clips in one slot: a random one each time.
[CreateAssetMenu(menuName = "HellScape/Sound Library", fileName = "SoundLibrary")]
public class SoundLibrary : ScriptableObject
{
    [System.Serializable]
    public class Slot
    {
        public string name;
        [Tooltip("What it is and where it plays.")]
        public string note;
        public AudioClip[] clips = new AudioClip[0];
        [Range(0f, 2f)] public float volume = 1f;
    }

    [System.Serializable]
    public class Area
    {
        public string name;
        [Tooltip("Music for this area. Crossfades in when you get there; empty = no music here.")]
        public AudioClip music;
        [Range(0f, 1f)] public float musicVolume = 0.6f;
        [Tooltip("An optional extra ambience loop for this area, under the music.")]
        public AudioClip ambience;
        [Range(0f, 1f)] public float ambienceVolume = 0.5f;
        public List<Slot> sounds = new List<Slot>();
    }

    [Range(0f, 1f)] public float masterMusicVolume = 1f;
    [Range(0.1f, 8f)] public float crossfadeSeconds = 2.5f;
    public List<Area> areas = new List<Area>();

    Dictionary<string, Slot> slots;
    Dictionary<string, Area> byName;

    void OnEnable() { slots = null; byName = null; }
    void OnValidate() { slots = null; byName = null; }

    // "Area/Slot name"
    public Slot Find(string id)
    {
        if (slots == null)
        {
            slots = new Dictionary<string, Slot>();
            foreach (var a in areas)
                foreach (var s in a.sounds)
                    if (!string.IsNullOrEmpty(s.name)) slots[a.name + "/" + s.name] = s;
        }
        slots.TryGetValue(id, out var slot);
        return slot;
    }

    public Area FindArea(string name)
    {
        if (byName == null)
        {
            byName = new Dictionary<string, Area>();
            foreach (var a in areas) if (!string.IsNullOrEmpty(a.name)) byName[a.name] = a;
        }
        byName.TryGetValue(name ?? "", out var area);
        return area;
    }
}
