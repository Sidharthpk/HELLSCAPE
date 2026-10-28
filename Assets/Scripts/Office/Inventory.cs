using System.Collections.Generic;
using UnityEngine;

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

    // A run spans Prologue, MainGameScene and OfficeSilo. Only a new run clears carried items.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        items.Clear();
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
    }
}
