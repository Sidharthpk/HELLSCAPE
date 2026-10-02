using UnityEngine;

// Something written you can pick up and read: on an Interactable, F opens it full screen in the NoteViewer (a note,
// a sticky note, a notice, a phone, a form, a ledger page, a calendar) instead of reading it out in the dialogue box.
// F again puts it down. beforeLine is said first (you noticing it), afterLine once you've put it down (your reaction).
// Plain ASCII only: the pixel font has no fancy dashes or bullets.
public class ReadableNote : MonoBehaviour
{
    public enum Style { Paper, Sticky, BloodySticky, Notice, Phone, Report, Ledger, Calendar }

    public Style style = Style.Paper;
    public string title;
    public string subtitle;
    [TextArea(3, 12)] public string body;
    public string footer;                    // signature / sign-off
    public string scrawl;                    // written over it in red marker (or blood)

    [Header("Calendar")]
    public string month = "APRIL";
    public int firstWeekday = 1;             // 0 = Monday ... 6 = Sunday: which column day 1 falls in
    public int days = 30;
    public int circledDay = 13;
    public string dayNote;                   // written in the circled day's box (crossed out)

    [Header("Your thoughts ('|' splits lines)")]
    [TextArea] public string beforeLine;
    [TextArea] public string afterLine;

    public void Read() => NoteViewer.Show(this);
}
