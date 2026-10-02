using UnityEngine;
using UnityEngine.Events;
using TMPro;

// Puzzle 3: press buttons in the right order (the CCTV replay of the cleaner's last shift).
// A wrong press starts over.
public class SequencePuzzle : MonoBehaviour
{
    public string[] order = { "Lobby", "Break Room", "Server Room", "Silo" };
    public bool powered;
    public TextMeshPro display;
    [TextArea] public string noPowerLine = "The screen is black.";
    [TextArea] public string wrongLine = "Static. The tape rewinds itself.";
    [TextArea] public string stepLine = "CAM: {0}... he was here.";
    public AudioSource wrongSound;
    public UnityEvent onSolved = new UnityEvent();

    private int step;
    private bool solved;

    public void SetPowered(bool on)
    {
        powered = on;
        if (display != null) display.text = on ? "REPLAY 13/04  ??:??" : "";
    }

    public void Press(string id)
    {
        if (solved) return;
        if (!powered) { Say(noPowerLine); return; }

        if (order[step] == id)
        {
            step++;
            if (display != null) display.text = "REPLAY 13/04  " + step + "/" + order.Length;
            if (step == order.Length)
            {
                solved = true;
                onSolved.Invoke();
            }
            else Say(string.Format(stepLine, id));
        }
        else
        {
            step = 0;
            if (display != null) display.text = "SIGNAL LOST";
            if (wrongSound != null) wrongSound.Play();
            Say(wrongLine);
        }
    }

    static void Say(string s) { if (!string.IsNullOrEmpty(s) && DialogueBox.Instance != null) DialogueBox.Instance.Say(s); }
}
