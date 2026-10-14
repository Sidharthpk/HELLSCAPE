using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Hold SPACE for two seconds during a cutscene to skip it: the prompt at the bottom fills, the picture fades to black,
// and it fades back in once the cutscene is over (ponytail: behind the black the cutscene is raced through, muted,
// not jumped: every cutscene is its own coroutine, so running it out keeps all their state changes intact; a long
// one holds the black for a second or two. A real jump needs a skip-to-end in each cutscene). Only touches
// Time.timeScale when it's normal (1), so the game-over slow-mo, pause and the boss slow-mo are left alone.
public class CutsceneSkip : MonoBehaviour
{
    const float Speed = 20f, HoldTime = 2f, FadeOut = .35f, FadeIn = .6f;
    public static int Extra;   // cutscenes that are not a class of their own (a delivery customer walking home, the Tomas change) hold this above 0 while they run
    public static bool Skipping { get; private set; }   // (DialogueBox lets its lines through)
    bool ours;
    float held, black, volume, idle;
    CanvasGroup prompt;
    RectTransform fill;
    Image curtain;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Make() { DontDestroyOnLoad(new GameObject("CutsceneSkip", typeof(CutsceneSkip))); }

    static bool InCutscene()
    {
        if (Extra > 0 || EndingSequence.Playing || OfficeFlashback.Playing) return true;
        foreach (var k in FindObjectsByType<KillerCutscenes>(FindObjectsSortMode.None)) if (k.Playing) return true;
        foreach (var s in FindObjectsByType<StabCutscene>(FindObjectsSortMode.None)) if (s.Playing) return true;
        foreach (var t in FindObjectsByType<TurningCutscene>(FindObjectsSortMode.None)) if (t.Playing) return true;
        return false;
    }

    void Awake() { Skipping = false; Build(); }

    void Update()
    {
        if (ours && Time.timeScale != Speed) Release();   // pause or a slow-mo took over: theirs now
        bool can = !TitleScreen.Showing && !GameOverScreen.Open && InCutscene();
        bool choosing = ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open;   // a choice is the player's: back to the picture
        if (!can || choosing) { Skipping = false; Release(); held = 0f; }
        else if (!Skipping)
        {
            held = Input.GetKey(KeyCode.Space) && Time.timeScale == 1f ? held + Time.unscaledDeltaTime : 0f;
            if (held >= HoldTime) { Skipping = true; held = 0f; }
        }

        black = Mathf.MoveTowards(black, Skipping ? 1f : 0f, Time.unscaledDeltaTime / (Skipping ? FadeOut : FadeIn));
        if (Skipping && black >= 1f && !ours && Time.timeScale == 1f) { volume = AudioListener.volume; AudioListener.volume = 0f; Time.timeScale = Speed; ours = true; }

        curtain.color = new Color(0f, 0f, 0f, black);
        // shown when a cutscene starts and whenever SPACE is touched; fades away after two untouched seconds
        idle = !can || Input.GetKey(KeyCode.Space) ? 0f : idle + Time.unscaledDeltaTime;
        if (!can) idle = 0f;
        float want = !can || choosing || Skipping || Time.timeScale != 1f || idle > 2f ? 0f : held > 0f ? 1f : .45f;
        prompt.alpha = Mathf.MoveTowards(prompt.alpha, want, Time.unscaledDeltaTime * 2.5f);
        fill.anchorMax = new Vector2(Mathf.Clamp01(held / HoldTime), 1f);
    }

    void Release()
    {
        if (!ours) return;
        ours = false;
        if (Time.timeScale == Speed) Time.timeScale = 1f;
        AudioListener.volume = volume;
    }

    void OnDisable() { Skipping = false; Release(); }

    // bottom right, clear of the dialogue box: the dialogue's charcoal panel with a blood-red bar that fills as you hold
    void Build()
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 210;   // over the story canvas (letterbox, dialogue)
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = .5f;
        var group = go.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;

        var panel = new GameObject("Panel", typeof(RectTransform)).AddComponent<DialoguePanelGraphic>();
        panel.transform.SetParent(go.transform, false);
        panel.color = new Color(.035f, .042f, .052f, .96f); panel.edge = DialoguePresentation.Blood; panel.corner = 8f; panel.thickness = 1.5f;
        panel.raycastTarget = false;
        prompt = panel.gameObject.AddComponent<CanvasGroup>(); prompt.alpha = 0f;
        var rt = panel.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f); rt.anchoredPosition = new Vector2(-32f, 24f); rt.sizeDelta = new Vector2(300f, 46f);

        var track = new GameObject("Track", typeof(RectTransform)).GetComponent<RectTransform>();
        track.SetParent(rt, false);
        track.anchorMin = Vector2.zero; track.anchorMax = new Vector2(1f, 0f); track.pivot = new Vector2(.5f, 0f);
        track.offsetMin = new Vector2(12f, 7f); track.offsetMax = new Vector2(-12f, 11f);
        var bar = new GameObject("Fill", typeof(RectTransform)).AddComponent<Image>();
        bar.transform.SetParent(track, false);
        bar.color = DialoguePresentation.Blood * 1.25f; bar.raycastTarget = false;
        fill = bar.rectTransform;
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.offsetMin = fill.offsetMax = Vector2.zero;

        var label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(rt, false);
        label.font = GameFont.Main; label.text = "HOLD  <color=#FFFFFF>SPACE</color>  TO SKIP";
        label.fontSize = 17f; label.color = new Color(.7f, .7f, .68f); label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; label.characterSpacing = 3f;
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(0f, 12f); label.rectTransform.offsetMax = Vector2.zero;

        curtain = new GameObject("Curtain", typeof(RectTransform)).AddComponent<Image>();   // last: over the prompt too
        curtain.transform.SetParent(go.transform, false);
        curtain.color = Color.clear; curtain.raycastTarget = false;
        curtain.rectTransform.anchorMin = Vector2.zero; curtain.rectTransform.anchorMax = Vector2.one;
        curtain.rectTransform.offsetMin = curtain.rectTransform.offsetMax = Vector2.zero;
    }
}
