using UnityEngine;
using UnityEngine.Events;

// Anything the player can look at and press F on: notes, pickups, doors, keypad buttons.
// Needs a non-trigger collider on it or a child so PlayerInteractor's ray can hit it.
public class Interactable : MonoBehaviour
{
    public string prompt = "Inspect";
    [TextArea] public string line;              // said on use ('|' splits lines)
    public string giveItem;                     // added to the Inventory on use
    public string requiredItem;                 // must be carried to use this
    public bool consumeItem = true;
    [TextArea] public string lockedLine = "It won't open.";
    public bool once = true;
    public bool hideOnUse;                      // pickups vanish
    public UnityEvent onUse = new UnityEvent();

    private bool used;

    public bool Available => isActiveAndEnabled && !(once && used);

    public void Use()
    {
        if (!Available) return;

        if (!string.IsNullOrEmpty(requiredItem))
        {
            if (!Inventory.Has(requiredItem)) { Say(lockedLine); return; }
            if (consumeItem) Inventory.Take(requiredItem);
        }

        used = true;
        Inventory.Add(giveItem);
        if (TryGetComponent(out ReadableNote note)) note.Read();   // something written: held up to read, not read out
        else Say(line);
        onUse.Invoke();
        if (hideOnUse) gameObject.SetActive(false);
    }

    static void Say(string s)
    {
        if (!string.IsNullOrEmpty(s) && DialogueBox.Instance != null) DialogueBox.Instance.Say(s);
    }
}
