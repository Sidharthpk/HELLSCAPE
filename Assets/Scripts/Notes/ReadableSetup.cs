using UnityEngine;
using UnityEngine.SceneManagement;

// Turns the story's written things into ReadableNotes when a scene loads (found by name on their Interactable),
// so nothing in the scenes has to be edited: the notice in the lobby, Derek's fridge note and phone, and in the
// office the cleaning rota, the calendar, Kessler's door-code sticky note, the bloody note on your desk, the
// incident report and Kessler's ledger. An object that already has a ReadableNote (set up by hand) is left alone.
public static class ReadableSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Apply();
    }

    static void OnLoaded(Scene s, LoadSceneMode m) => Apply();

    public static void Apply()
    {
        foreach (var it in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (it.GetComponent<ReadableNote>() != null) continue;
            var n = Make(it.gameObject.name);
            if (n == null) continue;
            var r = it.gameObject.AddComponent<ReadableNote>();
            n(r);
        }
    }

    static System.Action<ReadableNote> Make(string name)
    {
        switch (name)
        {
            // ---------------------------------------------------------------- the prologue
            case "Notice": return n =>
            {
                n.style = ReadableNote.Style.Notice;
                n.title = "NOTICE TO RESIDENTS";
                n.body = "Building CCTV is out of service until further notice.\n\nPlease keep your doors locked at all times.";
                n.footer = "- Management";
                n.afterLine = "Figures. Good thing Derek put up our own camera.";
            };
            case "Fridge note": return n =>
            {
                n.style = ReadableNote.Style.Paper;
                n.body = "Bro -\n\nRent's due Friday.\nDon't touch my leftovers.\n\nBreakfast's on me when you're back from your run.";
                n.footer = "- D";
                n.afterLine = "He was waiting up for me.";
            };
            case "Derek's phone": return n =>
            {
                n.style = ReadableNote.Style.Phone;
                n.title = "3 missed calls from K";
                n.subtitle = "K";
                n.body = "Keep your mouth shut about the silo.\n\nOr you go in it too.";
                n.footer = "Read 02:47";
                n.afterLine = "...K? Kessler? Our boss?|What did you get yourself into, Derek...";
            };

            // ---------------------------------------------------------------- the office
            case "CleaningRota": return n =>
            {
                n.style = ReadableNote.Style.Report;
                n.title = "NIGHT CLEANING ROTA";
                n.subtitle = "T. ROURKE - Kessler & Vane Logistics";
                n.body = "03:10    Silo 2 walkway\n\n00:30    Reception\n\n02:15    Server closet\n\n01:00    Meeting room";   // the order the CCTV puzzle is worked out from
                n.scrawl = "he never finished his route.";
            };
            case "Calendar": return n =>
            {
                n.style = ReadableNote.Style.Calendar;
                n.month = "APRIL";
                n.firstWeekday = 2;
                n.days = 30;
                n.circledDay = 13;
                n.dayNote = "Tomas - last shift! cake??";
                n.scrawl = "ACCIDENT";
                n.afterLine = "The 13th. Circled in red.|Someone crossed it out and wrote ACCIDENT over it.";
            };
            case "StickyNote": return n =>
            {
                n.style = ReadableNote.Style.Sticky;
                n.body = "Door code =\nTHE DAY.\n\nDay first, then month.\n\nDon't write it down!";
                n.footer = "- K";
                n.afterLine = "...thanks, Kessler.";
            };
            case "YourDesk_Note": return n =>
            {
                n.style = ReadableNote.Style.BloodySticky;
                n.beforeLine = "My desk. My keys should be right... here.|Gone.|There's a sticky note. It's wet.";
                n.body = "COME GET THEM.\n\nSILO 2.";
            };
            case "IncidentReport": return n =>
            {
                n.style = ReadableNote.Style.Report;
                n.title = "INCIDENT REPORT";
                n.subtitle = "Kessler & Vane Logistics  -  13/04";
                n.body = "Night cleaner T. Rourke fell into Silo 2 while working unsupervised.\n\nCause: ACCIDENTAL.\n\nNo further action required.";
                n.footer = "Witnesses:\nD. Hale        M. Ortiz\nS. Park        ________";
                n.afterLine = "...and mine. That's my signature.|I never read it. I just signed what Kessler put in front of me.";
            };
            case "KesslerLedger": return n =>
            {
                n.style = ReadableNote.Style.Ledger;
                n.beforeLine = "A second ledger, hidden under the real books.";
                n.title = "SILO 2 - NIGHT DELIVERIES";
                n.subtitle = "From city morgue. NO MANIFEST.";
                n.body = "K x2 ...................... 40,000\n\nL x1 ..................... 150,000\n\nCORNEAS x2 ................ ...\n\nHEART x1 .................. ...";
                n.footer = "12/04: The cleaner opened a truck.\nHandle it.\nD. does it or D. goes down with me.";
                n.afterLine = "...kidneys. Livers. These were people.|The next day, Tomas was 'an accident'.";
            };
        }
        return null;
    }
}
