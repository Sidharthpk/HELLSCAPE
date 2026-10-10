using System.Collections;
using UnityEngine;

public class OfficeRealmPuzzles : MonoBehaviour
{
    Keypad[] pads;
    SequencePuzzle[] tapes;
    KesslerDoor[] doors;
    public void Initialize()
    {
        pads = RevisedPlot.All<Keypad>(gameObject.scene);
        tapes = RevisedPlot.All<SequencePuzzle>(gameObject.scene);
        doors = RevisedPlot.All<KesslerDoor>(gameObject.scene);
        foreach (var p in pads) p.echoOnly = true;
        foreach (var p in tapes) p.echoOnly = true;
        foreach (var d in doors) d.echoOnly = true;
        foreach (var item in RevisedPlot.All<Interactable>(gameObject.scene))
            for (int i = 0; i < item.onUse.GetPersistentEventCount(); i++)
                if (item.onUse.GetPersistentTarget(i) is FuseBox && item.onUse.GetPersistentMethodName(i) == "InsertNext")
                {
                    var gate = item.gameObject.AddComponent<RealmInteraction>();
                    gate.unavailable = "The breaker contacts have melted. Keep the fuses. Fit them in the Echo, where this panel still works.";
                }
        SetEcho(false);
    }

    public bool CanSwitch(Transform player, bool announce = true)
    {
        if (player == null) return true;
        foreach (var door in doors ?? new KesslerDoor[0])
        {
            if (door.blocker == null) continue;
            // Disabled collider bounds are empty; use its box geometry instead.
            var box = door.blocker as BoxCollider;
            if (box == null) continue;
            Vector3 local = box.transform.InverseTransformPoint(player.position) - box.center;
            Vector3 pad = new Vector3(.6f, 1.2f, .6f);
            Vector3 half = box.size * .5f + pad;
            if (Mathf.Abs(local.x) < half.x && Mathf.Abs(local.y) < half.y && Mathf.Abs(local.z) < half.z)
            {
                if (announce) DialogueBox.Instance?.Say("I can't switch here. I'm standing in the doorway.");
                return false;
            }
        }
        return true;
    }

    public void SetEcho(bool on)
    {
        foreach (var pad in pads ?? new Keypad[0]) pad.ApplyRealm(on);
        foreach (var tape in tapes ?? new SequencePuzzle[0]) tape.ApplyRealm(on);
        foreach (var door in doors ?? new KesslerDoor[0]) door.ApplyRealm(on);
    }
}
