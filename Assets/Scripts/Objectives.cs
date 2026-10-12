using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Announce objectives in the center, then lift into a slim, opaque top bar.
public class ObjectiveUI : MonoBehaviour
{
    public static string Current { get; private set; } = "";
    public static bool OverCutscene;   // a cutscene that shows where to go: the objective comes up over it

    static ObjectiveUI ui;
    CanvasGroup all, strip;
    TextMeshProUGUI stripText, eyebrow;
    DialoguePanelGraphic panel;
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
        bool talking = elapsed < Hold + Lift && DialogueBox.Instance != null && DialogueBox.Instance.Busy;
        bool show = Current != "" && !talking && (!CutsceneHUD.Hidden || OverCutscene) &&!TitleScreen.Showing && !GameOverScreen.Open && !EndingSequence.Playing;
        all.alpha = show && elapsed >= 0f ? 1f : 0f;
        if (!show) return;
        elapsed += Time.deltaTime;
        LayoutAnnouncement(elapsed);
    }

    void LayoutAnnouncement(float seconds)
    {
        float lift = Mathf.SmoothStep(0f, 1f, (seconds - Hold) / Lift);
        var bar = (RectTransform)strip.transform;
        float screenWidth = ((RectTransform)bar.parent).rect.width;
        float centerWidth = Mathf.Min(690f, Mathf.Max(100f, screenWidth - 64f));
        float width = Mathf.Lerp(centerWidth, screenWidth, lift);
        float padding = Mathf.Lerp(24f, 10f, lift);
        stripText.fontSize = Mathf.Lerp(30f, 24f, lift);
        float textHeight = stripText.GetPreferredValues(stripText.text, Mathf.Max(40f, width - 64f), Mathf.Infinity).y;
        float height = Mathf.Max(Mathf.Lerp(104f, 54f, lift), textHeight + padding * 2f + (1f - lift) * 20f);
        eyebrow.alpha = 1f - lift;
        panel.corner = Mathf.Lerp(10f, 0f, lift);   // square once it is the full-width bar
        bar.anchorMin = bar.anchorMax = new Vector2(.5f, Mathf.Lerp(.5f, 1f, lift));
        bar.pivot = new Vector2(.5f, Mathf.Lerp(.5f, 1f, lift));
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(width, height);
        stripText.rectTransform.offsetMin = new Vector2(32f, padding);
        stripText.rectTransform.offsetMax = new Vector2(-32f, -padding - (1f - lift) * 20f);   // room for the eyebrow
        panel.SetVerticesDirty();
    }

    static ObjectiveUI Build()
    {
        var go = new GameObject("Objective", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        var o = go.AddComponent<ObjectiveUI>();
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 190;   // over the HUD, under the story canvas (dialogue, fades)
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = .5f;
        o.all = go.GetComponent<CanvasGroup>(); o.all.alpha = 0f; o.all.blocksRaycasts = false; o.all.interactable = false;

        // the strip along the top: the dialogue box's chamfered charcoal panel with a blood-red edge, typewriter text
        var bar = Panel("Strip", go.transform, out o.panel);
        bar.anchorMin = new Vector2(0f, 1f); bar.anchorMax = Vector2.one; bar.pivot = new Vector2(.5f, 1f); bar.sizeDelta = new Vector2(0f, 54f); bar.anchoredPosition = Vector2.zero;
        o.strip = bar.gameObject.AddComponent<CanvasGroup>();
        o.stripText = Label(bar, 24f, new Color(.94f, .94f, .92f), TextAlignmentOptions.Center);
        o.stripText.rectTransform.offsetMin = new Vector2(40f, 16f);
        o.stripText.rectTransform.offsetMax = new Vector2(-40f, -16f);
        // "OBJECTIVE" over the announcement; fades as the bar lifts
        o.eyebrow = Label(bar, 15f, DialoguePresentation.Blood * 1.25f, TextAlignmentOptions.Top);
        o.eyebrow.text = "OBJECTIVE"; o.eyebrow.characterSpacing = 6f;
        o.eyebrow.rectTransform.offsetMin = new Vector2(0f, 0f);
        o.eyebrow.rectTransform.offsetMax = new Vector2(0f, -10f);

        return o;
    }

    static RectTransform Panel(string name, Transform parent, out DialoguePanelGraphic graphic)
    {
        graphic = new GameObject(name, typeof(RectTransform)).AddComponent<DialoguePanelGraphic>();
        graphic.transform.SetParent(parent, false);
        graphic.color = new Color(.035f, .042f, .052f, .96f); graphic.edge = DialoguePresentation.Blood; graphic.corner = 10f; graphic.thickness = 1.5f;
        graphic.raycastTarget = false;
        return graphic.rectTransform;
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
    OfficeHorrorDirector office; KesslerConfrontation vane; CarInteract van; DemonBoss boss;

    void Start()
    {
        office = GetComponent<OfficeHorrorDirector>();
        vane = FindFirstObjectByType<KesslerConfrontation>(FindObjectsInactive.Include);
        van = FindFirstObjectByType<CarInteract>(FindObjectsInactive.Include);
        boss = FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
    }

    void Update() => ObjectiveUI.Set(Now());

    string Now()
    {
        if (Checkpoint.Current == null) return null;   // not woken yet
        switch (Checkpoint.Current.name.Replace("Checkpoint - ", ""))
        {
            case "Hell city": return "Go to the depot - Kessler & Vane";
            case "Office entrance":
                if (Inventory.Has("SiloKey")) return "RUN - the silo door";
                if (vane != null && vane.Armed) return "Confront Vane";
                int stage = office != null ? office.Stage : 0;
                return stage < 3 ? "Find the three fuses" : stage == 3 ? "Find the code for Vane's door" : "Play the security tape";
            case "Kessler tunnel": return "RUN - get to the silo";
            case "Silo complex": return "Get away from Elias";
            case "Lower bridge": return "Down the tower stairs";
            case "Silo floor": return BloodFlood.Swimming ? "Swim - get out of the silo" : Inventory.Has("CarKeys") ? "Get back to the van" : "Survive Elias";
            case "Escape": return van != null && van.InCar ? "Drive - get to the bridge" : "Get in the van";
            case "Bridge": return boss != null && boss.Defeated ? null : "Bring it down";
        }
        return null;
    }
}
