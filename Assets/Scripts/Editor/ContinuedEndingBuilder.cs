using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The angel ending is gone: when you aren't damned the Demon King binds you as his confessor and the game ends on
// TO BE CONTINUED (EndingSequence.Bound). This turns the old angel end card into that card and hands it to the
// ending. It also stages the scene: the king comes up out of the sea just PAST the end of the deck (he used to rise
// through the deck and stand sunk in it), and you are stood near the end of the bridge facing him.
// HellScape > Finale > To Be Continued Ending. (MainGameScene must be open.)
public static class ContinuedEndingBuilder
{
    public const string Title = "TO BE CONTINUED";
    public const string Sub = "The city is full of the Lost. Every one of them is hiding something.";

    [MenuItem("HellScape/Finale/To Be Continued Ending")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "Stop Play mode first.";
        var ending = Object.FindFirstObjectByType<EndingSequence>(FindObjectsInactive.Include);
        if (ending == null) return "No EndingSequence in the open scenes.";
        if (ending.demon.endCard == null) return "The demon end card is missing.";
        Transform canvas = ending.demon.endCard.transform.parent;
        Transform card = canvas.Find("EndCard_Continued");
        if (card == null) card = canvas.Find("EndCard_Angel");
        if (card == null)
        {
            // (no angel card to convert: copy the demon one)
            card = Object.Instantiate(ending.demon.endCard, canvas).transform;
            card.SetSiblingIndex(ending.demon.endCard.transform.GetSiblingIndex());
        }
        card.name = "EndCard_Continued";
        var texts = card.GetComponentsInChildren<TextMeshProUGUI>(true);
        var title = texts.FirstOrDefault(t => t.name == "Line1") ?? texts.FirstOrDefault();
        var sub = texts.FirstOrDefault(t => t.name == "Line2");
        if (title != null)
        {
            title.text = Title;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;          // it's a longer line than "Judged, and found..." was at this size
            title.fontSizeMax = title.fontSize;
            title.fontSizeMin = 40f;
            EditorUtility.SetDirty(title);
        }
        if (sub != null) { sub.text = Sub; EditorUtility.SetDirty(sub); }
        card.gameObject.SetActive(false);

        ending.continuedCard = card.gameObject;
        EditorUtility.SetDirty(ending);
        var angel = GameObject.Find("BridgeEnding/Angel");
        if (angel != null) angel.SetActive(false);
        string stage = Stage(ending);
        EditorSceneManager.MarkSceneDirty(ending.gameObject.scene);
        return "To-be-continued card wired: '" + Title + "'. " + stage;
    }

    const float KingPastEdge = 11f;     // his middle, this far beyond the end of the deck (he's ~12 m deep)
    const float KingFeetUnder = 3f;     // he stands shin-deep in the sea
    const float YouFromEdge = 9f;       // where you stand, back from the edge

    static string Stage(EndingSequence ending)
    {
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        if (boss == null || boss.arena == null) return "No boss arena: the king was not moved.";
        var scene = ending.gameObject.scene;
        Vector3 c = boss.arena.position, fwd = boss.arena.forward; fwd.y = 0f; fwd.Normalize();
        bool back = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        Physics.SyncTransforms();
        float Deck(Vector3 p)
        {
            float best = float.NaN;
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, c.y + 6f, p.z), Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.gameObject.scene == scene && h.normal.y > 0.5f && h.collider.GetComponentInParent<Rigidbody>() == null && (float.IsNaN(best) || h.point.y > best)) best = h.point.y;
            return best;
        }
        float deckY = Deck(c);
        if (float.IsNaN(deckY)) { Physics.queriesHitBackfaces = back; return "No deck under the arena centre: the king was not moved."; }
        // out along the centre line until the deck stops for good
        float edge = 0f; int gap = 0;
        for (float d = 0f; d < boss.arenaHalfLength + 60f && gap < 6; d += 0.5f)
        {
            float y = Deck(c + fwd * d);
            if (!float.IsNaN(y) && Mathf.Abs(y - deckY) < 1.5f) { edge = d; gap = 0; } else gap++;
        }
        Physics.queriesHitBackfaces = back;
        Vector3 edgePt = c + fwd * edge; edgePt.y = deckY;

        var king = ending.demon;
        bool was = king.root.activeSelf;
        king.root.SetActive(true);
        var rs = king.root.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        float feet = b.min.y - king.root.transform.position.y, height = b.size.y;
        king.root.SetActive(was);

        Vector3 stand = edgePt + fwd * KingPastEdge;
        stand.y = boss.waterY - KingFeetUnder - feet;
        Undo.RecordObject(king.endPoint, "Stage the king");
        Undo.RecordObject(king.startPoint, "Stage the king");
        king.endPoint.position = stand;
        king.startPoint.position = stand + Vector3.down * (height + 1.5f);   // all of him under the water

        Transform parent = king.endPoint.parent;
        Transform you = parent != null ? parent.Find("Ending - where you stand") : null;
        if (you == null) { you = new GameObject("Ending - where you stand").transform; you.SetParent(parent, false); }
        you.SetPositionAndRotation(edgePt - fwd * YouFromEdge, Quaternion.LookRotation(fwd));
        ending.standPoint = you;
        EditorUtility.SetDirty(ending);
        return $"King in the sea {KingPastEdge:0} m past the deck edge ({edge:0.0} m from the arena centre): rises {king.startPoint.position.y:0.0} -> {stand.y:0.0}, head {stand.y + feet + height - deckY:0.0} m above the deck; you stand {YouFromEdge:0} m from the edge.";
    }
}
