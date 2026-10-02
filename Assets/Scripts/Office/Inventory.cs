using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Key items the player carries (fuses, silo key, car keys). Counted, so three "Fuse" pickups stack.
public static class Inventory
{
    static readonly Dictionary<string, int> items = new Dictionary<string, int>();

    public static bool Has(string id) => !string.IsNullOrEmpty(id) && items.TryGetValue(id, out int n) && n > 0;

    public static void Add(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        items.TryGetValue(id, out int n);
        items[id] = n + 1;
    }

    public static void Take(string id)
    {
        if (Has(id)) items[id]--;
    }

    // statics survive a restart (GameOverScreen reloads the scene), so wipe them on every load
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        items.Clear();
        SceneManager.sceneLoaded += (s, m) => items.Clear();
    }
}

// Every choice that weighs on the bridge judgement.
public static class Judgement
{
    public static bool KilledKiller, SparedKiller;
    public static int Karma;                // kind (+) or cruel (-) answers to the office ghosts

    // more sins than virtues -> the demon comes for you
    public static bool IsDamned()
    {
        int sin = 0;
        if (KilledKiller) sin++;
        if (SparedKiller) sin--;
        if (Karma >= 2) sin--;
        if (Karma <= -2) sin++;
        foreach (var s in Survivor.All)
            if (s != null && !s.Turned) sin += s.Rescued ? -1 : 1;   // (someone the city took was never yours to save)
        return sin > 0;
    }

    static void Reset()
    {
        KilledKiller = SparedKiller = false;
        Karma = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        Reset();
        SceneManager.sceneLoaded += (s, m) => Reset();
    }
}
