using UnityEngine;

// Checked before an interaction spends inventory or fires a UnityEvent.
public class RealmInteraction : MonoBehaviour
{
    [TextArea] public string unavailable = "Burnt through. The mechanism only survives in the Echo.";
    public bool Allow()
    {
        if (RevisedPlotDirector.Instance == null || RevisedPlotDirector.Instance.Echo) return true;
        if (DialogueBox.Instance != null) DialogueBox.Instance.Say(unavailable);
        return false;
    }
}
