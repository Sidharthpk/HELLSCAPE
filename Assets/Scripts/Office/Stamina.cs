using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One pool for running and for the light in your hands (office only). Sprinting and holding the palm light drain it,
// resting refills it. Empty: no sprinting and the hands come down until it is back over `recoverAt`.
// Bar bottom-left, labelled STAMINA; it hides when full and you are not using it.
public class Stamina : MonoBehaviour
{
    public static Stamina Instance { get; private set; }
    public float sprintSeconds = 7f;        // a full bar of running
    public float lightSeconds = 40f;        // a full bar of holding the light
    public float refillSeconds = 9f;        // empty to full while resting
    public float restDelay = 1f;            // seconds of rest before it refills
    public float recoverAt = .3f;

    public float Value { get; private set; } = 1f;
    public bool Exhausted { get; private set; }

    FirstPersonController fpc;
    PalmLight palm;
    float rest;
    bool sprintWas;
    Image fill; CanvasGroup group;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; if (fpc != null) fpc.enableSprint = true; }

    void Start()
    {
        fpc = GetComponent<FirstPersonController>();
        palm = GetComponentInChildren<PalmLight>(true);
        var canvas = new GameObject("Stamina HUD", typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 380;
        var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        group = canvas.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0f;
        fill = BloodBar.Make(canvas.transform, "Stamina", new Vector2(300f, 30f));
        fill.color = new Color(.86f, .78f, .55f);
        var track = (RectTransform)fill.transform.parent;
        track.anchorMin = track.anchorMax = new Vector2(0f, 0f); track.pivot = new Vector2(0f, .5f); track.anchoredPosition = new Vector2(48f, 150f);
        var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(track, false);
        if (GameFont.Main != null) label.font = GameFont.Main;
        label.text = "STAMINA"; label.fontSize = 22f; label.color = new Color(.9f, .88f, .8f, .85f); label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f); label.rectTransform.pivot = new Vector2(0f, 0f);
        label.rectTransform.anchoredPosition = new Vector2(2f, 2f); label.rectTransform.sizeDelta = new Vector2(300f, 28f);
    }

    public bool LightHeld => palm != null && palm.Held;

    void Update()
    {
        bool here = OfficeHorrorDirector.In(transform.position);
        if (!here) { Value = 1f; Exhausted = false; if (fpc != null) fpc.enableSprint = true; group.alpha = 0f; return; }
        float dt = Time.deltaTime;
        bool sprint = fpc != null && fpc.IsSprinting;
        float drain = (sprint ? 1f / sprintSeconds : 0f) + (LightHeld ? 1f / lightSeconds : 0f);
        if (drain > 0f) { Value = Mathf.Max(0f, Value - drain * dt); rest = 0f; }
        else if ((rest += dt) > restDelay) Value = Mathf.Min(1f, Value + dt / refillSeconds);
        if (Value <= 0f && !Exhausted)
        {
            Exhausted = true;
            if (palm != null) palm.Lower();
            DialogueBox.Instance?.SayNow("*Out of breath. My hands are shaking.*");
        }
        if (Exhausted && Value >= recoverAt) Exhausted = false;
        if (fpc != null) fpc.enableSprint = !Exhausted;
        fill.fillAmount = Value;
        fill.color = Exhausted ? BloodBar.Red : new Color(.86f, .78f, .55f);
        bool show = Value < .999f || sprint || LightHeld;
        group.alpha = Mathf.MoveTowards(group.alpha, show && !CutsceneHUD.Hidden ? 1f : 0f, dt * 3f);
    }
}
