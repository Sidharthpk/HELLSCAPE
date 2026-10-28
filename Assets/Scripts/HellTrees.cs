using System;
using UnityEngine;

// The red city's trees: dead trees with their tops on fire (built by HellScape > Hell City > Build Burning Dead Trees).
// Sits on a root under HellWorld, so it wakes with the red city: the city's live trees are switched off for as long as
// it is on, and come back for the day. Every fire has a light, but only the nearest ones are lit (the quality setting
// allows 32 pixel lights), and they flicker. They are the red city's only light: every other light (the sun, the night
// fill) is switched off while it is on. The street lamps are not muted: HellCityPass decides which of them work.
public class HellTrees : MonoBehaviour
{
    public static HellTrees Instance { get; private set; }

    public GameObject[] originals;          // the city's live trees
    public Light[] lights;                  // one per burning tree, sitting in its crown
    public Light[] mute;                    // every other light of the city
    public int maxLit = 32;
    public Color lightColor = new Color(1f, 0.5f, 0.15f);
    public float intensity = 6f;
    public AudioClip fireClip;              // the crackle of a burning tree, heard in 3D only near the tree
    public int voices = 6;                  // how many fires can be heard at once (they follow the nearest ones)
    public float fireRange = 28f;
    public float fireVolume = 0.55f;

    float[] lightIntensity;
    float nextPick;
    AudioSource[] voice;
    int[] voiceFire;

    public int Count => lights.Length;
    public Vector3 Top(int i) => lights[i].transform.position;
    public Vector3 Base(int i) => lights[i].transform.parent.position;

    void OnEnable()
    {
        Instance = this;
        foreach (var o in originals) if (o != null) o.SetActive(false);
        foreach (var l in mute) if (l != null) l.enabled = false;
        lightIntensity = new float[lights.Length];
        lights = Array.FindAll(lights, l => l != null);   // (the trees were rebuilt: some fires are gone)
        for (int i = 0; i < lights.Length; i++) { lights[i].color = lightColor; lights[i].enabled = false; lightIntensity[i] = intensity * UnityEngine.Random.Range(0.8f, 1.2f); }
        nextPick = 0f;
        if (fireClip != null)
        {
            voice = new AudioSource[voices]; voiceFire = new int[voices];
            for (int v = 0; v < voices; v++)
            {
                var a = new GameObject("Fire sound " + v).AddComponent<AudioSource>();
                a.transform.SetParent(transform, false);
                a.clip = fireClip; a.loop = true; a.playOnAwake = false;
                a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 2f; a.maxDistance = fireRange; a.dopplerLevel = 0f;
                a.volume = 0f;
                voice[v] = a; voiceFire[v] = -1;
            }
        }
    }

    void OnDisable()
    {
        if (voice != null) { foreach (var a in voice) if (a != null) Destroy(a.gameObject); voice = null; }
        if (Instance == this) Instance = null;
        if (!gameObject.scene.isLoaded) return;   // (closing: nothing to give back)
        foreach (var o in originals) if (o != null) o.SetActive(true);
        foreach (var l in mute) if (l != null) l.enabled = true;
    }

    void Update()
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (Time.time >= nextPick)
        {
            nextPick = Time.time + 0.3f;
            var keys = new float[lights.Length];
            var order = new int[lights.Length];
            for (int i = 0; i < lights.Length; i++) { keys[i] = lights[i] != null && lights[i].gameObject.activeInHierarchy ? (lights[i].transform.position - cam.transform.position).sqrMagnitude : float.MaxValue; order[i] = i; if (lights[i] != null) lights[i].enabled = false; }
            Array.Sort(keys, order);
            for (int k = 0; k < Mathf.Min(maxLit, order.Length); k++) if (keys[k] < float.MaxValue) lights[order[k]].enabled = true;
            if (voice != null) AssignVoices(order, keys);
        }
        if (voice != null)
            for (int v = 0; v < voice.Length; v++)
            {
                // fade in and out so a voice moving to another tree never clicks
                float want = voiceFire[v] >= 0 ? fireVolume : 0f;
                voice[v].volume = Mathf.MoveTowards(voice[v].volume, want, Time.deltaTime * 1.5f);
                if (voice[v].volume <= 0f && voice[v].isPlaying) voice[v].Stop();
            }
        float t = Time.time;
        for (int i = 0; i < lights.Length; i++)
            if (lights[i].enabled) lights[i].intensity = lightIntensity[i] * (0.35f + 1.0f * Mathf.PerlinNoise(t * 13f, i * 3.7f) * (0.6f + 0.4f * Mathf.PerlinNoise(t * 31f, i * 1.3f)));
    }

    // the nearest fires within earshot each keep a voice at the foot of their crown; a voice only moves once it has faded out
    void AssignVoices(int[] order, float[] keys)
    {
        var near = new System.Collections.Generic.List<int>();
        for (int k = 0; k < order.Length && near.Count < voice.Length; k++)
            if (keys[k] < fireRange * fireRange) near.Add(order[k]);
        for (int v = 0; v < voice.Length; v++) if (voiceFire[v] >= 0 && !near.Contains(voiceFire[v])) voiceFire[v] = -1;
        foreach (int f in near)
        {
            if (Array.IndexOf(voiceFire, f) >= 0) continue;
            for (int v = 0; v < voice.Length; v++)
            {
                if (voiceFire[v] >= 0 || voice[v].volume > 0.01f) continue;
                voiceFire[v] = f;
                voice[v].transform.position = lights[f].transform.position;
                voice[v].time = UnityEngine.Random.value * Mathf.Max(0.1f, fireClip.length - 0.1f);
                voice[v].Play();
                break;
            }
        }
    }
}
