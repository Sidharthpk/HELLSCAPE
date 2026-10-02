using UnityEngine;
using UnityEngine.Events;
using TMPro;

// Puzzle 2: a door keypad. Wire each digit button's Interactable.onUse to Press("7").
public class Keypad : MonoBehaviour
{
    public string code = "1304";
    public TextMeshPro display;
    public bool powered;
    [TextArea] public string noPowerLine = "Dead. No power.";
    [TextArea] public string wrongLine = "Wrong code.";
    public AudioSource beep;
    public UnityEvent onSolved = new UnityEvent();

    private string entry = "";
    private bool solved;

    void Start() => Refresh();

    public void SetPowered(bool on) { powered = on; Refresh(); }

    public void Press(string digit)
    {
        if (solved) return;
        if (!powered) { Say(noPowerLine); return; }
        if (beep != null) { beep.pitch = 1f + digit[0] * 0.01f; beep.Play(); }

        entry += digit;
        Refresh();
        if (entry.Length < code.Length) return;

        if (entry == code)
        {
            solved = true;
            if (display != null) { display.text = "OPEN"; display.color = Color.green; }
            onSolved.Invoke();
        }
        else
        {
            entry = "";
            if (display != null) display.text = "ERR";
            Say(wrongLine);
        }
    }

    void Refresh()
    {
        if (display == null || solved) return;
        display.color = new Color(1f, 0.2f, 0.15f);
        display.text = powered ? entry.PadRight(code.Length, '_') : "";
    }

    static void Say(string s) { if (!string.IsNullOrEmpty(s) && DialogueBox.Instance != null) DialogueBox.Instance.Say(s); }
}
