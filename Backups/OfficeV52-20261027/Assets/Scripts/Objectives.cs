using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Announce objectives in the center (the Objective Box art), then fold away into "OBJECTIVE (O)" at the top left.
// O opens the box there to read it again; O again folds it back. The choice is kept for the next objective.
public class ObjectiveUI : MonoBehaviour
{
    public static string Current { get; private set; } = "";
    public static bool OverCutscene;   // a cutscene that shows where to go: the objective comes up over it
    public const KeyCode Key = KeyCode.O;

    static ObjectiveUI ui;
    static bool expanded;              // O: the box open in the corner, or just the words
    static readonly Vector2 DockMargin = new Vector2(36f, 28f);   // docked: this far in from the left edge and the top
    const float DockWidth = 540f;
    const float OpenTime = .22f;       // seconds to open or fold the box
    CanvasGroup all, strip;
    TextMeshProUGUI stripText, eyebrow, tab;
    float aspect;   // the art's width per unit of height
    float open;     // 0 folded to the words .. 1 the box open (docked only)
    const float Hold = 2.6f, Lift = .8f, Delay = .7f;
    float elapsed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Current = ""; ui = null; OverCutscene = false; expanded = false; }

    void OnDestroy() { if (ui == this) Current = ""; }   // the scene went, and its objective with it

    public static void Set(string text)
    {
        text = text ?? "";
        if (text == Current) return;
        Current = text;
        if (text == "") { if (ui != null) ui.all.alpha = 0f; return; }
        if (ui == null) ui = Build();
        ui.stripText.text = text;
        ui.elapsed = -Delay;
        ui.all.alpha = 0f;

    }

    void Update()
    {
        // Dock before checking dialogue, so combat chatter does not restart a centre announcement.
        if (BossHUDLayout.AnyVisible) elapsed = Mathf.Max(elapsed, Hold + Lift);
        bool talking = elapsed < Hold + Lift && DialogueBox.Instance != null && DialogueBox.Instance.Busy
                       || DialogueBox.Instance != null && DialogueBox.Instance.CardOpen;
        bool show = Current != "" && !talking && (!CutsceneHUD.Hidden || OverCutscene) &&!TitleScreen.Showing && !GameOverScreen.Open && !EndingSequence.Playing;
        all.alpha = show && elapsed >= 0f ? 1f : 0f;
        if (!show) return;
        // O: open or fold the box (during an announcement it just skips straight to the corner)
        if (Input.GetKeyDown(Key) && !PauseMenu.Paused && elapsed >= 0f) Toggle();
        elapsed += Time.deltaTime;
        open = Mathf.MoveTowards(open, expanded ? 1f : 0f, Time.unscaledDeltaTime / OpenTime);
        LayoutAnnouncement(elapsed);
    }

    // what O does: open or fold the box in the corner; during an announcement, skip it to the corner
    public static void Toggle()
    {
        if (ui == null) return;
        if (ui.elapsed < Hold + Lift) ui.elapsed = Hold + Lift;
        else expanded = !expanded;
    }

    void LayoutAnnouncement(float seconds)
    {
        float lift = Mathf.SmoothStep(0f, 1f, (seconds - Hold) / Lift);
        // the box is shown while it announces, and in the corner only when opened; folded, only the words show
        float boxShown = Mathf.Lerp(1f, Mathf.SmoothStep(0f, 1f, open), lift);
        strip.alpha = boxShown;
        tab.alpha = 1f - boxShown;
        var bar = (RectTransform)strip.transform;
        bar.localScale = Vector3.one * Mathf.Lerp(.35f, 1f, boxShown);   // it grows out of the words in the corner
        float screenWidth = ((RectTransform)bar.parent).rect.width;
        float width = Mathf.Lerp(Mathf.Min(720f, Mathf.Max(100f, screenWidth - 64f)), Mathf.Min(DockWidth, screenWidth - DockMargin.x * 2f), lift);
        // the art's proportions; taller only if a long objective needs it. Text starts past the red icon (top left).
        float left = width * .12f, right = width * .06f;
        stripText.fontSize = Mathf.Lerp(32f, 27f, lift);
        eyebrow.fontSize = Mathf.Lerp(18f, 16f, lift);
        float textHeight = stripText.GetPreferredValues(stripText.text, Mathf.Max(40f, width - left - right), Mathf.Infinity).y;
        float height = Mathf.Max(width / aspect, textHeight + width * .17f);
        // from the middle of the screen to the top left corner, kept clear of both edges
        var corner = new Vector2(0f, 1f);
        bar.anchorMin = bar.anchorMax = Vector2.Lerp(new Vector2(.5f, .5f), corner, lift);
        bar.pivot = Vector2.Lerp(new Vector2(.5f, .5f), corner, lift);
        bar.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(DockMargin.x, -DockMargin.y), lift);
        bar.sizeDelta = new Vector2(width, height);
        // "OBJECTIVE" level with the icon, the objective under it
        eyebrow.rectTransform.offsetMin = new Vector2(left, 0f);
        eyebrow.rectTransform.offsetMax = new Vector2(-right, -width * .045f);
        stripText.rectTransform.offsetMin = new Vector2(left, width * .04f);
        stripText.rectTransform.offsetMax = new Vector2(-right, -width * .1f);
    }

    static ObjectiveUI Build()
    {
        var go = new GameObject("Objective", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        var o = go.AddComponent<ObjectiveUI>();
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 190;   // over the HUD, under the story canvas (dialogue, fades)
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = .5f;
        o.all = go.GetComponent<CanvasGroup>(); o.all.alpha = 0f; o.all.blocksRaycasts = false; o.all.interactable = false;

        // the box: the Objective Box art (Resources/HellscapeUI), typewriter text
        var art = Resources.Load<Sprite>("HellscapeUI/Objective Box");
        var frame = new GameObject("Strip", typeof(RectTransform)).AddComponent<Image>();
        frame.transform.SetParent(go.transform, false);
        frame.sprite = art; frame.raycastTarget = false;
        if (art == null) frame.color = new Color(.035f, .042f, .052f, .96f);
        o.aspect = art != null ? art.rect.width / art.rect.height : 3.6f;
        var bar = frame.rectTransform;
        o.strip = bar.gameObject.AddComponent<CanvasGroup>();
        o.stripText = Label(bar, 24f, new Color(.94f, .94f, .92f), TextAlignmentOptions.MidlineLeft);
        o.eyebrow = Label(bar, 15f, DialoguePresentation.Blood * 1.25f, TextAlignmentOptions.TopLeft);
        o.eyebrow.text = "OBJECTIVE"; o.eyebrow.characterSpacing = 6f;

        // folded: just the words in the corner, with the key to open it
        o.tab = Label(go.transform, 22f, new Color(.94f, .94f, .92f), TextAlignmentOptions.TopLeft);
        o.tab.text = "<color=#E01A14>OBJECTIVE</color>  (" + Key + ")"; o.tab.characterSpacing = 4f;
        var tr = o.tab.rectTransform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0f, 1f);
        tr.anchoredPosition = new Vector2(DockMargin.x, -DockMargin.y); tr.sizeDelta = new Vector2(400f, 40f);
        o.tab.alpha = 0f;

        return o;
    }

    // (fills its parent: the caller pulls the edges in)
    static TextMeshProUGUI Label(Transform parent, float size, Color colour, TextAlignmentOptions align)
    {
        var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.font = DialoguePresentation.HeadFont;
        text.fontSize = size; text.color = colour; text.alignment = align; text.raycastTarget = false;
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        return text;
    }
}

// The main scene's objectives, read off where the story stands (the last checkpoint, the office's stage, what you
// carry) rather than set from each beat: nothing to keep in step when a beat is rewritten. Added by RevisedPlotDirector.
public class StoryObjectives : MonoBehaviour
{
    OfficeHorrorDirector office; KesslerConfrontation vane; CarInteract van; DemonBoss boss; KillerCutscenes killer;

    void Start()
    {
        office = GetComponent<OfficeHorrorDirector>();
        vane = FindFirstObjectByType<KesslerConfrontation>(FindObjectsInactive.Include);
        van = FindFirstObjectByType<CarInteract>(FindObjectsInactive.Include);
        boss = FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        killer = FindFirstObjectByType<KillerCutscenes>(FindObjectsInactive.Include);
    }

    void Update() => ObjectiveUI.Set(Now());

    string Now()
    {
        if (Checkpoint.Current == null) return null;   // not woken yet
        switch (Checkpoint.Current.name.Replace("Checkpoint - ", ""))
        {
            case "Hell city": return RedFlat.Done ? "Go to the office - Kessler & Vane" : RedFlat.Arrived ? "Look around the flat" : "Go home - Flat 2B";
            case "Office entrance":
                if (Inventory.Has("SiloKey") || vane != null && vane.Turned) return "Head to the silo";
                if (vane != null && vane.Armed) return "Confront Vane";
                int stage = office != null ? office.Stage : 0;
                return stage < 3 ? "Find the three fuses" : stage == 3 ? "Find the code for Vane's door" : "Play the security tape";
            case "Kessler tunnel": return "RUN - get to the silo";
            case "Silo complex": return killer != null && killer.ScareSeen ? "Get away from Vane" : "RUN - get to the silo";   // (until he shoves you down)
            case "Lower bridge": return "Down the tower stairs";
            case "Silo floor": return BloodFlood.Swimming ? "Swim - get out of the silo" : Inventory.Has("CarKeys") ? "Get back to the van" : "Survive Vane";
            case "Escape": return van != null && van.InCar ? "Drive - get to the bridge" : "Get in the van";
            case "Bridge": return boss != null && boss.Defeated ? null : "Bring it down";
        }
        return null;
    }
}
