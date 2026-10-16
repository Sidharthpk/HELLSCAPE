using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Announce objectives in the center, then lift into a smaller box docked at the top right (the Objective Box art).
public class ObjectiveUI : MonoBehaviour
{
    public static string Current { get; private set; } = "";
    public static bool OverCutscene;   // a cutscene that shows where to go: the objective comes up over it

    static ObjectiveUI ui;
    public static float TopInset(Canvas target)
    {
        if (ui == null || ui.all == null || ui.all.alpha <= .01f || ui.strip == null || target == null) return 0f;
        var source = ui.GetComponent<Canvas>();
        // (only once it has docked in the corner: while it announces in the middle the top is free)
        return ui.docked * (((RectTransform)ui.strip.transform).rect.height + DockMargin.y) * source.scaleFactor / Mathf.Max(.01f, target.rootCanvas.scaleFactor);
    }
    static readonly Vector2 DockMargin = new Vector2(36f, 28f);   // docked: this far in from the right edge and the top
    const float DockWidth = 540f;
    CanvasGroup all, strip;
    TextMeshProUGUI stripText, eyebrow;
    float aspect;   // the art's width per unit of height
    float docked;   // 0 announcing in the middle .. 1 docked top right
    const float Hold = 2.6f, Lift = .8f, Delay = .7f;
    float elapsed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Current = ""; ui = null; OverCutscene = false; }

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
        elapsed += Time.deltaTime;
        LayoutAnnouncement(elapsed);
    }

    void LayoutAnnouncement(float seconds)
    {
        float lift = Mathf.SmoothStep(0f, 1f, (seconds - Hold) / Lift);
        docked = lift;
        var bar = (RectTransform)strip.transform;
        float screenWidth = ((RectTransform)bar.parent).rect.width;
        float width = Mathf.Lerp(Mathf.Min(720f, Mathf.Max(100f, screenWidth - 64f)), Mathf.Min(DockWidth, screenWidth - DockMargin.x * 2f), lift);
        // the art's proportions; taller only if a long objective needs it. Text starts past the red icon (top left).
        float left = width * .12f, right = width * .06f;
        stripText.fontSize = Mathf.Lerp(32f, 27f, lift);
        eyebrow.fontSize = Mathf.Lerp(18f, 16f, lift);
        float textHeight = stripText.GetPreferredValues(stripText.text, Mathf.Max(40f, width - left - right), Mathf.Infinity).y;
        float height = Mathf.Max(width / aspect, textHeight + width * .17f);
        // from the middle of the screen to the top right corner, kept clear of both edges
        bar.anchorMin = bar.anchorMax = Vector2.Lerp(new Vector2(.5f, .5f), Vector2.one, lift);
        bar.pivot = Vector2.Lerp(new Vector2(.5f, .5f), Vector2.one, lift);
        bar.anchoredPosition = Vector2.Lerp(Vector2.zero, -DockMargin, lift);
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
            case "Hell city": return "Go to the depot - Kessler & Vane";
            case "Office entrance":
                if (Inventory.Has("SiloKey") || vane != null && vane.Turned) return "Head to the silo";
                if (vane != null && vane.Armed) return "Confront Vane";
                int stage = office != null ? office.Stage : 0;
                return stage < 3 ? "Find the three fuses" : stage == 3 ? "Find the code for Vane's door" : "Play the security tape";
            case "Kessler tunnel": return "RUN - get to the silo";
            case "Silo complex": return killer != null && killer.ScareSeen ? "Get away from Elias" : "RUN - get to the silo";   // (until he shoves you down)
            case "Lower bridge": return "Down the tower stairs";
            case "Silo floor": return BloodFlood.Swimming ? "Swim - get out of the silo" : Inventory.Has("CarKeys") ? "Get back to the van" : "Survive Elias";
            case "Escape": return van != null && van.InCar ? "Drive - get to the bridge" : "Get in the van";
            case "Bridge": return boss != null && boss.Defeated ? null : "Bring it down";
        }
        return null;
    }
}
