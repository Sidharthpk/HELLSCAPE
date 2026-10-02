using UnityEngine;
using TMPro;

// On the player camera: shows the key and what it does ("F  Inspect") when looking at an Interactable, and uses it
// on that key.
public class PlayerInteractor : MonoBehaviour
{
    public float range = 2.6f;
    public KeyCode key = KeyCode.F;
    public TextMeshProUGUI promptText;      // what it does
    public TextMeshProUGUI keyText;         // the key, on its own chip (empty: "[F] ..." written into promptText)
    public GameObject promptRoot;           // the whole prompt (empty: promptText's own object)

    private Interactable current;
    private Transform body;

    // the player's own body (its rigidbody), not transform.root: in the prologue the player sits under a scene
    // root that also holds the things you interact with
    Transform Body => body != null ? body : body = GetComponentInParent<Rigidbody>() is Rigidbody rb ? rb.transform : transform.root;

    GameObject Root => promptRoot != null ? promptRoot : promptText != null ? promptText.gameObject : null;

    void Update()
    {
        current = null;
        // a note is up (F puts it down, it doesn't pick the next thing up), or you're in the middle of talking to someone
        if (NoteViewer.Blocking || PauseMenu.Paused || GhostNPC.Conversing)
        {
            if (Root != null) Root.SetActive(false);
            return;
        }
        if (ChoiceDialogue.Instance == null || !ChoiceDialogue.Instance.Open)
            current = Find();

        if (Root != null)
        {
            Root.SetActive(current != null);
            if (current != null)
            {
                if (keyText != null) { keyText.text = key.ToString(); promptText.text = current.prompt; }
                else promptText.text = "[" + key + "] " + current.prompt;
            }
        }

        if (current != null && Input.GetKeyDown(key)) current.Use();
    }

    // nearest hit; trigger volumes are see-through unless they're something to talk to (ghosts)
    Interactable Find()
    {
        var hits = Physics.RaycastAll(transform.position, transform.forward, range, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(Body)) continue;   // the camera sits at the edge of our own capsule
            var it = hit.collider.GetComponentInParent<Interactable>();
            if (hit.collider.isTrigger && it == null) continue;
            return it != null && it.Available ? it : null;
        }
        return null;
    }

    void OnDisable()
    {
        if (Root != null) Root.SetActive(false);
    }
}
