using System.Collections.Generic;
using System.Linq;

// V5.1 truth flags (docs: HellScape_V5_1 section 5). A flag is a name: "FoundPriya", "SawPriya", "FreedPriya",
// "KnowsRuth", "FoundOwnTag", "VaneForm1Defeated", "VaneConfessed" ... Like EvidenceJournal this is static, so it
// survives scene loads, Echo timeouts and checkpoint retries within a session.
public static class Truth
{
    public static readonly string[] Colleagues = { "Priya", "Ben", "Nadia", "Omar" };
    static readonly HashSet<string> flags = new HashSet<string>();
    public static event System.Action<string> Changed;

    public static bool Has(string flag) => flags.Contains(flag);
    public static void Set(string flag) { if (!string.IsNullOrEmpty(flag) && flags.Add(flag)) Changed?.Invoke(flag); }
    public static int Count(string prefix) => flags.Count(f => f.StartsWith(prefix));
    public static string[] All => flags.OrderBy(f => f).ToArray();

    // evidence found + Echo memory seen: the colleague can now be told the truth
    public static bool Ready(string who) => Has("Found" + who) && Has("Saw" + who);
    public static bool AllFreed => Colleagues.All(c => Has("Freed" + c));

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() { flags.Clear(); Changed = null; }
}
