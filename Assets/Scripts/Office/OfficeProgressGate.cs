using UnityEngine;

// Runs before an Interactable spends an item or invokes its existing UnityEvents.
public sealed class OfficeProgressGate : MonoBehaviour
{
    public enum Rule { KesslerDoor, Armory, Locker, Pistol, Keys, Silo, Street }
    public OfficeV52Sequence sequence;
    public Rule rule;
    [TextArea] public string unavailable;

    public bool Allow()
    {
        if (sequence == null || !sequence.isActiveAndEnabled) return true;
        bool allowed = false;
        switch (rule)
        {
            case Rule.KesslerDoor: allowed = OfficeV52Sequence.Has("KesslerCode"); break;   // Kessler's office: the keypad (1304)
            case Rule.Armory: allowed = true; break;                                         // the sequence's Pry() explains what's missing
            case Rule.Locker: allowed = OfficeV52Sequence.Has("Armory"); break;
            case Rule.Pistol:
            case Rule.Keys: allowed = OfficeV52Sequence.Has("Locker"); break;
            case Rule.Silo: allowed = sequence.CanFree && Truth.AllFreed; break;
            case Rule.Street: allowed = !OfficeV52Sequence.Has("Choked") || sequence.Completed; break;
        }
        if (!allowed) DialogueBox.Instance?.Say(unavailable);
        return allowed;
    }
}
