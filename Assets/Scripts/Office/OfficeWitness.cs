using UnityEngine;

// Optional witnesses never own keys or puzzle completion. Losing a memory costs its testimony.
public class OfficeWitness : MonoBehaviour
{
    GhostNPC ghost;
    string admission, excuse, denial;
    string[] redReplies;
    public void Initialize(GhostNPC npc)
    {
        ghost = npc;
        switch (ghost.ghostName)
        {
            case "Priya":
                admission = "I passed the statement around. I hadn't seen a fall. I told the others it was only attendance because I wanted them to sign. That was my lie.";
                excuse = "They needed their shifts! I kept everyone employed. I'd hand them that paper again.";
                denial = "There wasn't a statement. I only remember the reception bell. Nothing else happened.";
                redReplies = new[] { "I handed out forms. That's reception work. Ask whoever wrote them.", "Vane had his own office. I wasn't paid to watch his door.", "The date is on the calendar. I didn't put it there for you." }; break;
            case "Ben":
                admission = "I heard Tomas shouting. I shut the break-room door and turned the music up. I chose not to hear him.";
                excuse = "One man screaming doesn't make him my responsibility. I kept the others out of trouble. I'd shut that door again.";
                denial = "It was just the radio. We were having cake. There wasn't anyone shouting.";
                redReplies = new[] { "That sound could have been the radio. The walls carry everything.", "Vane didn't eat with us. Whatever he did was his business.", "Try the cleaning rota. Omar wrote down everywhere he went. I stayed here." }; break;
            case "Nadia":
                admission = "I stamped the report without checking it. Then I filed it where nobody would ask. I made the lie official.";
                excuse = "An unsigned report gets people fired. My stamp kept this place running. That matters more than one cleaner.";
                denial = "That isn't my stamp. I don't remember that day. Stop putting it in front of me.";
                redReplies = new[] { "A stamp doesn't mean I witnessed anything. It means the form arrived.", "His phone was always ringing. I didn't listen. I never listened.", "Your keys went through the slot at seven. Check your desk for what happened after. Don't ask me to explain it." }; break;
            default:
                admission = "I cleaned the blood before anyone could photograph it. Vane asked, but I took the bucket. I destroyed what would have proved it.";
                excuse = "Blood on the floor shuts a business down. I made it safe. Someone had to clean up his mess.";
                denial = "There was no blood. Just water. It was always only water.";
                redReplies = new[] { "I clean whatever's on the floor. I don't ask how it got there.", "He signed the work orders. That's all I know about Vane.", "The rota has the camera order. Sort it by time. You can read it yourself." }; break;
        }
        ghost.answerSeconds = 18f;
        ghost.Replace(new[] {
            Node("I remember the report. Don't make me remember the rest.",
                Choice("Tell me what you did yourself.", "I can still see my hands. I keep trying to say someone made them move.", 1),
                Choice("Vane gave the orders. Does that excuse you?", "He did. But I was there. Ask me again before I forget.", 1),
                Choice("We can pretend you weren't there.", denial, -1, 1)),
            Node("If I say it, I have to live with it. What do you want from me?",
                Choice("Your own actions. No excuses.", admission, -1, 3),
                Choice("Tell me why it was necessary.", excuse, -1, 2),
                Choice("Forget it. Nothing happened here.", denial, -1, 1))
        });
        ghost.afterLine = "I've told you what I can remember.";
        // (no more standing at desks: they walk the office in pain, PainWalk)
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
        ChoiceDialogue.Instance.Show(ghost.ghostName, "Why are you still asking?", new[] { "What did you do that night?", "What do you know about Vane?", "Where should I look?", "Leave" }, i =>
        {
            if (i < redReplies.Length) DialogueBox.Instance?.Say(ghost.ghostName + ": " + redReplies[i]);
        }, transform);
        return true;
    }

    public void Resolved(GhostNPC.Fate fate)
    {
        ghost.afterLine = fate == GhostNPC.Fate.Confessed ? "I did it. Remember my words, not his version."
            : fate == GhostNPC.Fate.Damned ? "I'd do it again. Stop looking at me like that."
            : fate == GhostNPC.Fate.Gone ? "Only ash remains. The memory is gone." : "I can't remember my name. There was only the bell...";
        if (fate == GhostNPC.Fate.Confessed) EvidenceJournal.Record(ghost.ghostName + " testimony", admission);
        FindFirstObjectByType<OfficeHorrorDirector>()?.WitnessResolved(fate);
    }
}
