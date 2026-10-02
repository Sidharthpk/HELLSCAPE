using UnityEngine;
using UnityEngine.Events;

// Puzzle 1: bring N items to one place (three fuses into the breaker box).
public class PuzzleCounter : MonoBehaviour
{
    public int required = 3;
    [TextArea] public string progressLine = "{0} of {1}.";
    public UnityEvent onSolved = new UnityEvent();

    private int count;

    public void Add()
    {
        count++;
        if (count >= required) onSolved.Invoke();
        else if (DialogueBox.Instance != null) DialogueBox.Instance.Say(string.Format(progressLine, count, required));
    }
}
