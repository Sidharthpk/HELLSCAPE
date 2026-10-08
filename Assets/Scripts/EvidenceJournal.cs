using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Case evidence is separate from consumable keys. Survives checkpoints and the prologue transition.
public static class EvidenceJournal
{
    public const string Invoice = "Harlow invoice", Statement = "Shift witness statement", Ledger = "Vane's ledger", Tape = "Tomas CCTV", Phone = "5 AM phone call";
    static readonly Dictionary<string, string> accounts = new Dictionary<string, string>();
    public static void Record(string title, string account) { accounts[title] = account; Collect(title); }
    static readonly HashSet<string> found = new HashSet<string>();
    public static bool ContainerInspected, VaneConfessed, PlayerConfessed, TomasDamned;
    public static string[] Found => found.OrderBy(x => x).ToArray();
    public static bool Has(string id) => found.Contains(id);
    public static event System.Action<string> Collected;
    public static void Collect(string id) { if (!string.IsNullOrEmpty(id) && found.Add(id)) Collected?.Invoke(id); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Collected = null;
        found.Clear(); accounts.Clear();
        ContainerInspected = VaneConfessed = PlayerConfessed = TomasDamned = false;
    }

    public static void ResetHell()
    {
        found.Clear(); accounts.Clear();
        VaneConfessed = PlayerConfessed = TomasDamned = false;
    }

    public static string Account(string id)
    {
        if (accounts.TryGetValue(id, out var account)) return account;
        switch (id)
        {
            case Invoice: return "Harlow Clinic. KEEP COLD collection. Approved by Vane, code V-0500. Disposal of T. Rourke appears on the linked account.";
            case Statement: return "Every night-shift worker signed the account calling Tomas's death a fall. My signature is there. I did not witness an accident.";
            case Ledger: return "Vane paid Harlow, sold material taken from unclaimed bodies, and used the silo to destroy what remained.";
            case Tape: return "Derek caught Tomas opening the body bags. Vane threatened both brothers and ordered Derek to deal with him. Derek killed Tomas.";
            case Phone: return "05:00. Vane: Arthur, the Rourke brother is on his way. Kessler: Then you'd better not be there. He knew, and he left Vane to it.";
            default: return id;
        }
    }
}
