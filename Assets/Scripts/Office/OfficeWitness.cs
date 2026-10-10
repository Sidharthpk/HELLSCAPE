using UnityEngine;

// Optional witnesses never own keys or puzzle completion. Losing a memory costs its testimony.
public class OfficeWitness : MonoBehaviour
{
    GhostNPC ghost;
    string admission, greeting;
    string[] questions, redReplies;
    bool greeted;

    // Rows of the script (HS): 3.2 the red-city talk, 3.3 the shared Echo questioning, 3.4 each one's three ends.
    // HS-rows: 160,164,166,169,173,175,178,182,184,187,191,193,213-236
    public void Initialize(GhostNPC npc)
    {
        ghost = npc;
        string n = ghost.ghostName;
        int own;   // first row of this colleague's confessed / damned / lost pairs
        switch (n)
        {
            case "Priya": own = 213; greeting = HS.S("157-159", n); Red(n, 160, "161-163", 164, "165", 166, "167"); break;
            case "Ben": own = 219; greeting = HS.S("168", n); Red(n, 169, "170-172", 173, "174", 175, "176"); break;
            case "Nadia": own = 225; greeting = HS.S("177", n); Red(n, 178, "179-181", 182, "183", 184, "185"); break;
            default: own = 231; greeting = HS.S("186", n); Red(n, 187, "188-190", 191, "192", 193, "194"); break;
        }
        admission = HS.T(own);
        string confessed = HS.S(own + "-" + (own + 1), n), damned = HS.S((own + 2) + "-" + (own + 3), n), lost = HS.S((own + 4) + "-" + (own + 5), n);
        ghost.answerSeconds = 30f;
        ghost.opening = HS.S("195-198", n);
        ghost.Replace(new[] {
            Node(HS.T(199),
                Choice("What did you do?", HS.S("200-201", n), 1),
                Choice("Vane ordered you. Is that enough?", HS.S("202-203", n), 1),
                Choice("Forget it. You were not there.", HS.S("204-206", n) + "|" + lost, -1, 1)),
            Node("...",
                Choice("Tell me exactly what you did.", HS.S("207-208", n) + "|" + confessed, -1, 3),
                Choice("Tell me why you had to.", HS.S("209-210", n) + "|" + damned, -1, 2),
                Choice("Nothing happened.", HS.S("211-212", n) + "|" + lost, -1, 1))
        });
        ghost.afterLine = "...";
        // (no more standing at desks: they walk the office in pain, PainWalk)
    }

    void Red(string n, int q1, string a1, int q2, string a2, int q3, string a3)
    {
        questions = new[] { HS.T(q1), HS.T(q2), HS.T(q3), "Leave" };
        redReplies = new[] { HS.S(a1, n), HS.S(a2, n), HS.S(a3, n) };
    }

    static readonly System.Collections.Generic.List<Vector3> taken = new System.Collections.Generic.List<Vector3>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetTaken() => taken.Clear();

    // nearest desk-height top with clear floor beside it: stand there, facing the desk
    static bool FindDesk(Vector3 from, out Vector3 spot, out Vector3 facing)
    {
        spot = from; facing = Vector3.forward;
        float best = float.MaxValue;
        var dirs = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        for (float x = -12f; x <= 12f; x += .3f)
            for (float z = -12f; z <= 12f; z += .3f)
            {
                Vector3 p = from + new Vector3(x, 0f, z);
                if (!Top(p, from.y, out float h) || h < .6f || h > 1.1f) continue;
                foreach (var d in dirs)
                {
                    Vector3 s = p + d * .75f;
                    float dist = (s - from).sqrMagnitude;
                    if (dist >= best || !Top(p - d * .3f, from.y, out float deep) || Mathf.Abs(deep - h) > .1f) continue;   // a real top, not a ledge
                    if (!Top(s, from.y, out float floor) || Mathf.Abs(floor) > .15f) continue;
                    if (taken.Exists(t => (t - s).sqrMagnitude < 4f)) continue;   // one worker per desk
                    if (Physics.CheckCapsule(s + Vector3.up * .45f, s + Vector3.up * 1.5f, .25f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    best = dist; spot = new Vector3(s.x, from.y, s.z); facing = -d;
                }
            }
        if (best < float.MaxValue) taken.Add(spot);
        return best < float.MaxValue;
    }

    static bool Top(Vector3 p, float floorY, out float height)
    {
        height = 0f;
        p.y = floorY + 1.5f;
        if (!Physics.Raycast(p, Vector3.down, out var hit, 1.8f, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < .9f) return false;
        if (hit.collider.GetComponentInParent<GhostNPC>() != null) return false;
        height = hit.point.y - floorY; return true;
    }
    static GhostNode Node(string line, params GhostChoice[] choices) => new GhostNode { line = line, choices = choices };
    static GhostChoice Choice(string text, string reply, int next, int fate = 0) => new GhostChoice { text = text, reply = reply, next = next, fate = fate };

    public bool RedTalk()
    {
        if (RevisedPlotDirector.Instance == null || RevisedPlotDirector.Instance.Echo) return false;
        if (ghost.fate != GhostNPC.Fate.Undecided) { DialogueBox.Instance?.Say(ghost.ghostName + ": " + ghost.afterLine); return true; }
        if (greeted) Ask();
        else { greeted = true; DialogueBox.Instance?.Say(greeting); StartCoroutine(AskAfter()); }
        return true;
    }

    System.Collections.IEnumerator AskAfter()
    {
        yield return RevisedPlotDirector.WaitForDialogue();
        if (RevisedPlotDirector.Instance != null && !RevisedPlotDirector.Instance.Echo && !ChoiceDialogue.Instance.Open) Ask();
    }

    void Ask() => ChoiceDialogue.Instance.Show(ghost.ghostName, "...", questions, i =>
    {
        if (i < redReplies.Length) DialogueBox.Instance?.Say(redReplies[i]);
    }, transform);

    public void Resolved(GhostNPC.Fate fate)
    {
        ghost.afterLine = fate == GhostNPC.Fate.Confessed ? HS.T(237) : fate == GhostNPC.Fate.Damned ? HS.T(238) : fate == GhostNPC.Fate.Gone ? "..." : HS.T(239);
        if (fate == GhostNPC.Fate.Confessed) EvidenceJournal.Record(ghost.ghostName + " testimony", admission);
        FindFirstObjectByType<OfficeHorrorDirector>()?.WitnessResolved(fate);
    }
}
