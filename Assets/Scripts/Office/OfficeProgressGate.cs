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
            case Rule.KesslerDoor: allowed = Truth.Has("OfficeV52.Transformed"); break;
            case Rule.Armory:
                allowed = Truth.Has("OfficeV52.OfficeAccessed") && Truth.Has("OfficeV52.CombinationClue");
                break;
            case Rule.Locker: allowed = Truth.Has("OfficeV52.ArmoryOpened"); break;
            case Rule.Pistol:
            case Rule.Keys: allowed = Truth.Has("OfficeV52.LockerUnlocked"); break;
            case Rule.Silo: allowed = Truth.Has("OfficeV52.Pistol") && Inventory.Has("SiloKey"); break;
            case Rule.Street: allowed = !Truth.Has("OfficeV52.Transformed") || sequence.Completed; break;
        }
        if (!allowed) DialogueBox.Instance?.Say(unavailable);
        return allowed;
    }
}
