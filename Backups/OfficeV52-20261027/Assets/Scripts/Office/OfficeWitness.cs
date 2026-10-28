using UnityEngine;

// V5.1: the four colleagues are innocent. Each stays Lost until the Driver has (a) found their evidence in the office,
// (b) seen their memory in the Echo, and (c) told them the truth (the A3B_<name>_talk rows of HellscapeDialogueV5.csv).
// Any order. Flags live in Truth ("FoundPriya", "SawPriya", "FreedPriya"); evidence also goes in the journal [J].
public class OfficeWitness : MonoBehaviour
{
    GhostNPC ghost;
    GameObject evidence;
    string Name => ghost.ghostName;

    // what each colleague's evidence says, and what their Echo memory shows
    static readonly string[,] Case =
    {
        // name, evidence title, evidence body, journal account, echo memory
        { "Priya", "RECORD U-0117", "U-0117\nName field: REMOVED\nEdited by: P. (front desk)\n\nShift extension approved after the shutdown was scheduled.", "U-0117 was Ruth Ellery. Priya's shift was extended after the shutdown was scheduled.", "*The Echo. Priya at the records terminal, taking a name out of U-0117. Her extension is approved. Nobody tells her to leave.*" },
        { "Ben", "ADMIN LOG", "ADMIN LOGIN: SECURITY (emergency account)\nCAMERAS: OFF\nALARMS: OFF\nDOORS: LOCKED\n\nBuilding occupancy: 4", "Mercer used the emergency administrator login. The cameras went first, then the alarms. Four people were inside.", "*The Echo. Ben at the servers. The emergency account logs in. The cameras die first, then the alarms.*" },
        { "Nadia", "INCIDENT REPORT", "FIRE - ELECTRICAL FAULT\nPrepared 05:20\nFire logged 06:40\n\nEvacuation order: CANCELLED (security)", "The electrical-fault report was written before the fire. The evacuation order was cancelled from security.", "*The Echo. Nadia's report, printed an hour before the fire. Someone cancels the evacuation order.*" },
        { "Omar", "ALARM PANEL", "Main loop: CUT\nCuts are clean. Not fire damage.\n\nAll exits: SECURITY LOCK", "The alarm wires were cut cleanly. Security locked every exit.", "*The Echo. Omar at the alarm panel. The wires are already cut. The exits lock one by one.*" },
    };

    int Row() { for (int i = 0; i < 4; i++) if (Case[i, 0] == Name) return i; return 0; }

    // HS5-rows: A3B_priya_talk A3B_ben_talk A3B_nadia_talk A3B_omar_talk A3B_all_freed A3B_tunnel_direction
    public void Initialize(GhostNPC npc)
    {
        ghost = npc;
        ghost.answerSeconds = 0f;
        EchoMemory();
        PlaceEvidence();
        if (Truth.Has("Freed" + Name)) ghost.gameObject.SetActive(false);   // already at peace (a retry or reload)
    }

    // in the Echo they stand and show what happened; seeing it is what counts (replayable: a timeout never closes it)
    void EchoMemory()
    {
        string who = Name;
        ghost.Replace(new[] { new GhostNode { line = Case[Row(), 4] } });
        ghost.opening = null;
        ghost.afterLine = Case[Row(), 4];
        ghost.onFinished.RemoveAllListeners();
        ghost.onFinished.AddListener(() => { Truth.Set("Saw" + who); });
    }

    // the evidence lies on the desk nearest to where they were placed
    void PlaceEvidence()
    {
        if (!FindDesk(ghost.transform.position, out Vector3 spot, out Vector3 facing)) return;
        int i = Row();
        string who = Name;
        evidence = EvidenceProp.Spawn(spot + facing * .75f, who + " evidence", Case[i, 1], Case[i, 2], Case[i, 3], "*Evidence recorded. [J] to review.*", () =>
        {
            Truth.Set("Found" + who);
            if (who == "Priya") Truth.Set("KnowsRuth");
        });
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
    public bool RedTalk()
    {
        if (RevisedPlotDirector.Instance == null || RevisedPlotDirector.Instance.Echo) return false;
        string who = Name;
        if (Truth.Has("Freed" + who)) { DialogueBox.Instance?.Say(who + ": Keep going."); return true; }
        if (!Truth.Ready(who))
        {
            DialogueBox.Instance?.Say(who + ": " + (Truth.Has("Found" + who) ? "I can almost see it. The Echo might show me." : "I don't remember how it ended. There must be something of mine left in this office."));
            return true;
        }
        Release();
        return true;
    }

    void Release()
    {
        string who = Name;
        Truth.Set("Freed" + who);
        ghost.fate = GhostNPC.Fate.Confessed;   // (the soul is at peace: WitnessLook's released mode)
        if (TryGetComponent<WitnessLook>(out var look)) look.Set(WitnessLook.Mode.Confessed);
        string lines = HS5.S("A3B_" + who.ToLowerInvariant() + "_talk", who);
        if (Truth.AllFreed) lines += "|" + HS5.S("A3B_all_freed") + "|" + HS5.S("A3B_tunnel_direction");   // all four: the way on
        DialogueBox.Instance?.Say(lines);
        FindFirstObjectByType<OfficeHorrorDirector>()?.WitnessResolved(GhostNPC.Fate.Confessed);
        StartCoroutine(Ascend());
    }

    // V5.1 soul release: once the truth is said they rise slowly and are gone
    System.Collections.IEnumerator Ascend()
    {
        yield return null;
        while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;
        yield return new WaitForSeconds(.8f);
        ghost.enabled = false;   // (no more drifting; the whisper stops with it)
        foreach (var walk in GetComponents<PainWalk>()) walk.enabled = false;
        var light = GetComponentInChildren<Light>();
        Vector3 from = transform.position;
        for (float t = 0f; t < 4f; t += Time.deltaTime)
        {
            float k = t / 4f;
            transform.position = from + Vector3.up * (k * k * 3f);
            if (light != null) light.intensity *= 1f - .6f * Time.deltaTime;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = k < .85f || Mathf.FloorToInt(t * 12f) % 2 == 0;   // (a last flicker)
            yield return null;
        }
        gameObject.SetActive(false);
    }

    // a timeout in the Echo is not the end of their memory: stand them up for another look (no softlock)
    public void Resolved(GhostNPC.Fate fate)
    {
        if (fate != GhostNPC.Fate.Gone) return;
        EchoMemory();
        ghost.fate = GhostNPC.Fate.Undecided;
    }
}
