using UnityEngine;
using UnityEngine.Events;
using TMPro;

// Puzzle 3: press buttons in the right order (the CCTV replay of the cleaner's last shift).
// A wrong press starts over.
public class SequencePuzzle : MonoBehaviour, ISceneProgress
{
    public string[] order = { "Lobby", "Break Room", "Server Room", "Silo" };
    public bool powered;
    public bool echoOnly;                   // the burnt terminal only works in the Echo
    [TextArea] public string burntLine = "The terminal is melted. It only survives in the Echo.";
    public TextMeshPro display;
    [TextArea] public string noPowerLine = "The screen is black.";
    [TextArea] public string wrongLine = "Static. The tape rewinds itself.";
    [TextArea] public string stepLine = "CAM: {0}... he was here.";
    public AudioSource wrongSound;
    public UnityEvent onSolved = new UnityEvent();

    public System.Action<int> onStep;       // the step reached (1..n); -1 on a wrong press
    private int step;
    private bool solved, echo;

    public object CaptureProgress() => (step, solved, powered);
    public void RestoreProgress(object state)
    {
        (step, solved, powered) = ((int, bool, bool))state;
        SetPowered(powered);
    }

    public void SetPowered(bool on)
    {
        powered = on;
        if (display != null) display.text = echoOnly && !echo ? "NO SIGNAL" : solved ? "REPLAY COMPLETE" : on ? "REPLAY 13/04  " + step + "/" + order.Length : "";
    }

    public void ApplyRealm(bool on) { echo = on; SetPowered(powered); }

    public void Press(string id)
    {
        if (OfficeV52Sequence.Instance != null && OfficeV52Sequence.Instance.cctv.tape == this)
        { OfficeV52Sequence.Instance.cctv.Watch(); return; }
        if (solved || order == null || order.Length == 0) return;
        if (echoOnly && !echo) { Say(burntLine); return; }
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
            onStep?.Invoke(step);
        }
        else
        {
            step = 0;
            if (display != null) display.text = "SIGNAL LOST";
            if (wrongSound != null) wrongSound.Play();
            Say(wrongLine);
            onStep?.Invoke(-1);
        }
    }

    // the CCTV desk: the whole tape at once, no button order (onSolved plays Derek and Tomas)
    public void PlayTape()
    {
        if (solved) return;
        solved = true;
        if (display != null) display.text = "REPLAY COMPLETE";
        onSolved.Invoke();
    }

    // V5.2 uses the fire recording as the required playback; retain the older tape and its events for other scenes.
    public void CompleteRecording()
    {
        solved = true;
        SetPowered(powered);
    }

    static void Say(string s) { if (!string.IsNullOrEmpty(s) && DialogueBox.Instance != null) DialogueBox.Instance.Say(s); }
}
