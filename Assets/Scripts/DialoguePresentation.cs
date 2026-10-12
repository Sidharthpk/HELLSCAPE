using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shared by runtime and editor builders, so existing scenes adopt the dialogue design on Play.
// Only presentation is rebuilt: dialogue queues, choices and story callbacks remain with their owners.
// The pixel font (GameFont), charcoal panels and restrained red accents.
[AddComponentMenu("")]
public class DialoguePresentation : MonoBehaviour
{
    const string RootName = "CinematicDialogue";
    public static readonly Color Blood = new Color(0.78f, 0.05f, 0.04f);    // the HUD's red (BloodBar.Red)
    public static readonly Color Bone = new Color(0.94f, 0.94f, 0.92f);
    static readonly Color Muted = new Color(0.66f, 0.69f, 0.72f);
    static readonly Color Ink = new Color(0.035f, 0.042f, 0.052f, 1f);
    static readonly Color Edge = new Color(0.24f, 0.27f, 0.30f, 1f);

    [SerializeField] RectTransform root;
    [SerializeField] TextMeshProUGUI speaker, body, hint;
    [SerializeField] RectTransform rule, eyebrow, shade;
    bool narration;
    [SerializeField] Button[] buttons;
    [SerializeField] TextMeshProUGUI[] labels;
    [SerializeField] bool choices;
    public void SetCountdown(float seconds)
    {
        if (eyebrow == null || !eyebrow.TryGetComponent<TextMeshProUGUI>(out var label)) return;
        label.text = "MEMORY FADING";
        if (fading == null)
        {
            fading = BloodBar.Make(eyebrow, "Memory left", new Vector2(220f, 28f));
            var track = (RectTransform)fading.transform.parent;
            track.anchorMin = track.anchorMax = new Vector2(0f, .5f); track.pivot = new Vector2(0f, .5f); track.anchoredPosition = new Vector2(206f, 2f);
        }
        if (seconds > fadingFrom) fadingFrom = seconds;   // (the first value of a question is its whole time)
        fading.transform.parent.gameObject.SetActive(true);
        fading.fillAmount = fadingFrom > 0f ? Mathf.Clamp01(seconds / fadingFrom) : 0f;
        fading.color = BloodBar.Red * (seconds <= 5f ? 1f + .4f * Mathf.Sin(Time.unscaledTime * 14f) : 1f);
    }
    Image fading;
    float fadingFrom;
    int count;
    float lastWidth = -1f, lastHeight = -1f;

    static TMP_FontAsset BodyFont => GameFont.Main;
    public static TMP_FontAsset HeadFont => GameFont.Main;

    public static DialoguePresentation Subtitle(DialogueBox dialogue)
    {
        var v = Prepare(dialogue.group.gameObject, false);
        dialogue.text = v.body;
        dialogue.nameText = v.speaker;
        dialogue.hintText = v.hint;
        dialogue.accent = v.rule.GetComponent<Graphic>();
        dialogue.border = new Graphic[0];
        dialogue.playerColor = Bone;   // (the scenes have the old amber saved on the box)
        v.hint.text = "<color=#C70D0A>[" + dialogue.advanceKey.ToString().ToUpperInvariant() + "]</color>";
        return v;
    }

    public static DialoguePresentation Choices(ChoiceDialogue dialogue)
    {
        // Existing scenes wrap the panel in a 100x100 controller object.
        if (dialogue.panel.transform.parent == dialogue.transform && dialogue.transform is RectTransform container)
            Stretch(container, 0f, 0f, 0f, 0f);
        var v = Prepare(dialogue.panel, true);
        dialogue.text = v.body;
        return v;
    }

    static DialoguePresentation Prepare(GameObject owner, bool isChoice)
    {
        var v = owner.GetComponent<DialoguePresentation>();
        if (v == null) v = owner.AddComponent<DialoguePresentation>();
        // Existing child objects stay available for serialized references and editor undo.
        for (int i = owner.transform.childCount - 1; i >= 0; i--)
        {
            var child = owner.transform.GetChild(i);
            child.gameObject.SetActive(false);
            if (child.name != RootName) continue;
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
        var legacy = owner.GetComponent<Graphic>();
        if (legacy != null) legacy.enabled = false;
        var ownerRect = (RectTransform)owner.transform;
        ownerRect.anchorMin = Vector2.zero;
        ownerRect.anchorMax = Vector2.one;
        ownerRect.offsetMin = ownerRect.offsetMax = Vector2.zero;
        ownerRect.localScale = Vector3.one;
        v.choices = isChoice;
        v.root = Node(RootName, owner.transform);
        v.root.pivot = new Vector2(0.5f, 0f);
        Place(v.root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1160f, 200f));
        v.speaker = Text("Speaker", v.root, HeadFont, 24f, Blood, TextAlignmentOptions.TopLeft);
        v.speaker.characterSpacing = 2f;
        v.body = Text("Line", v.root, BodyFont, isChoice ? 28f : 30f, Bone, TextAlignmentOptions.TopLeft);
        v.body.lineSpacing = 10f;
        v.hint = Text("Hint", v.root, HeadFont, 20f, Muted, TextAlignmentOptions.BottomRight);
        v.rule = Node("Rule", v.root);   // the speaker's colour: a square beside the name, or the line over your answers
        var line = v.rule.gameObject.AddComponent<Image>();
        line.color = Blood;
        line.raycastTarget = false;
        // Opaque charcoal surface with a fine neutral edge.
        v.shade = Node("Box", v.root);
        v.shade.SetAsFirstSibling();
        var box = v.shade.gameObject.AddComponent<DialoguePanelGraphic>();
        box.color = Ink; box.edge = Edge; box.corner = 10f; box.thickness = 1f; box.raycastTarget = false;
        if (isChoice) Stretch(v.shade, -26f, -26f, -26f, -22f); else Stretch(v.shade, 0f, 0f, 0f, 0f);

        if (isChoice)
        {
            v.eyebrow = Text("ResponseLabel", v.root, HeadFont, 18f, Muted, TextAlignmentOptions.TopLeft).rectTransform;
            v.eyebrow.GetComponent<TMP_Text>().text = "YOUR ANSWER";
            v.eyebrow.GetComponent<TMP_Text>().characterSpacing = 4f;
            v.hint.text = "[1-4] CHOOSE";
            v.buttons = new Button[4];
            v.labels = new TextMeshProUGUI[4];
            for (int i = 0; i < 4; i++)
            {
                var card = Node("Response " + (i + 1), v.root);
                var graphic = card.gameObject.AddComponent<DialoguePanelGraphic>();
                graphic.color = new Color(0.09f, 0.105f, 0.125f, 1f); graphic.edge = new Color(0.28f, 0.31f, 0.35f, 1f);
                graphic.corner = 6f; graphic.thickness = 1f;
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = graphic;
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(2.3f, 1.6f, 1.5f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color(3f, 1.9f, 1.7f, 1f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.12f;
                button.colors = colors;
                // The graphic's vertex tint is multiplied by the selectable's transition color.
                var number = Text("Key", card, HeadFont, 28f, Bone, TextAlignmentOptions.Center);   // (red would vanish on the lit card)
                number.text = (i + 1).ToString();
                Place(number.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(36f, 42f));
                var divider = Node("Divider", card);
                var ink = divider.gameObject.AddComponent<Image>(); ink.color = new Color(0.78f, 0.05f, 0.04f, 0.5f); ink.raycastTarget = false;
                Place(divider, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(65f, 0f), new Vector2(1f, 32f));
                v.labels[i] = Text("Answer", card, BodyFont, 24f, Bone, TextAlignmentOptions.MidlineLeft);
                Stretch(v.labels[i].rectTransform, 86f, 14f, 24f, 14f);
                v.labels[i].lineSpacing = 6f;
                v.buttons[i] = button;
                card.gameObject.SetActive(false);
            }
        }
        v.Layout();
        return v;
    }

    public void ShowChoices(string who, string question, string[] options, Color color, System.Action<int> select)
    {
        if (eyebrow != null) eyebrow.GetComponent<TMP_Text>().text = "YOUR ANSWER";
        fadingFrom = 0f;
        if (fading != null) fading.transform.parent.gameObject.SetActive(false);
        speaker.text = who.ToUpperInvariant();
        speaker.color = color;
        rule.GetComponent<Graphic>().color = color;
        body.text = question;
        count = Mathf.Min(options.Length, 4);
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].onClick.RemoveAllListeners();
            buttons[i].gameObject.SetActive(i < count);
            if (i >= count) continue;
            labels[i].text = options[i];
            int index = i;
            buttons[i].onClick.AddListener(() => select(index));
        }
        hint.text = "[" + (count == 1 ? "1" : "1-" + count) + "] CHOOSE";
        Layout();
        if (count > 0 && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
    }

    public void RefreshLine() => Layout();

    // Narration: no box, no name. Just the words in the middle, with a hard shadow, like a caption on old footage.
    public void SetNarration(bool on)
    {
        if (choices) return;
        narration = on;
        speaker.gameObject.SetActive(!on);
        rule.gameObject.SetActive(!on);
        if (shade != null) shade.gameObject.SetActive(!on);
        var m = body.fontMaterial;
        if (on)
        {
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.95f));
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 1.2f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -1.2f);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.5f);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        }
        else m.DisableKeyword("UNDERLAY_ON");
        body.alignment = on ? TextAlignmentOptions.Top : TextAlignmentOptions.TopLeft;
        body.UpdateMeshPadding();
        Layout();
    }

    public void RestoreFocus()
    {
        if (count == 0 || EventSystem.current == null) return;
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(root))
            EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
    }

    void LateUpdate()
    {
        var parent = transform as RectTransform;
        if (parent != null && (!Mathf.Approximately(parent.rect.width, lastWidth) || !Mathf.Approximately(parent.rect.height, lastHeight))) Layout();
    }

    void Layout()
    {
        if (root == null || body == null) return;
        var available = ((RectTransform)transform).rect;
        lastWidth = available.width; lastHeight = available.height;
        float width = Mathf.Min(1180f, Mathf.Max(280f, available.width * 0.82f));
        float bottom = Mathf.Max(32f, available.height * 0.075f);
        root.anchoredPosition = new Vector2(0f, bottom);
        const float pad = 32f;
        float textWidth = choices ? width - 48f : width - pad * 2f;
        float bodyHeight = Mathf.Max(34f, body.GetPreferredValues(body.text, textWidth, Mathf.Infinity).y + 6f);
        if (!choices)
        {
            // the name (a square of the speaker's colour beside it), the words under it, the key in the corner
            float height = bodyHeight + (narration ? 28f : 92f);
            root.sizeDelta = new Vector2(width, height);
            // a bar down the box's left edge in the speaker's colour
            Place(rule, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(5f, -20f));
            rule.pivot = new Vector2(0f, .5f);
            Place(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, -16f), new Vector2(textWidth, 32f));
            speaker.rectTransform.pivot = new Vector2(0f, 1f);
            Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(pad, narration ? -14f : -56f), new Vector2(textWidth, bodyHeight));
            body.rectTransform.pivot = new Vector2(0f, 1f);
            Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-pad + 8f, 8f), new Vector2(120f, 26f));
            hint.rectTransform.pivot = new Vector2(1f, 0f);
            return;
        }

        int columns = width >= 850f && available.width >= available.height * 1.2f ? 2 : 1;
        float cardWidth = (width - (columns - 1) * 14f) / columns;
        float[] rowHeights = new float[4];
        for (int i = 0; i < count; i++)
            rowHeights[i / columns] = Mathf.Max(rowHeights[i / columns], 78f,
                labels[i].GetPreferredValues(labels[i].text, cardWidth - 110f, Mathf.Infinity).y + 32f);
        int rows = Mathf.CeilToInt(count / (float)columns);
        float cardsHeight = 0f;
        for (int row = 0; row < rows; row++) cardsHeight += rowHeights[row] + (row == 0 ? 0f : 12f);
        float total = cardsHeight + bodyHeight + 126f;
        root.sizeDelta = new Vector2(width, total);
        // Fit the full response set on shorter displays without truncating any answer.
        float scale = Mathf.Min(1f, (available.height - bottom - 40f) / Mathf.Max(total, 1f));
        root.localScale = Vector3.one * Mathf.Max(0.35f, scale);
        Place(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(width, 40f));
        speaker.rectTransform.pivot = new Vector2(0f, 1f);
        Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -50f), new Vector2(textWidth, bodyHeight));
        body.rectTransform.pivot = new Vector2(0f, 1f);
        Place(eyebrow, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, cardsHeight + 39f), new Vector2(250f, 24f));
        eyebrow.pivot = Vector2.zero;
        Place(rule, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, cardsHeight + 32f), new Vector2(width, 2f));
        rule.pivot = Vector2.zero;
        float y = cardsHeight + 22f;
        for (int i = 0; i < count; i++)
        {
            int row = i / columns, col = i % columns;
            if (i > 0 && col == 0) y -= rowHeights[row - 1] + 12f;
            var rt = (RectTransform)buttons[i].transform;
            Place(rt, Vector2.zero, Vector2.zero, new Vector2(col * (cardWidth + 14f), y), new Vector2(cardWidth, rowHeights[row]));
            rt.pivot = new Vector2(0f, 1f);
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnLeft = col > 0 ? buttons[i - 1] : null;
            nav.selectOnRight = col + 1 < columns && i + 1 < count ? buttons[i + 1] : null;
            nav.selectOnUp = i >= columns ? buttons[i - columns] : null;
            nav.selectOnDown = i + columns < count ? buttons[i + columns] : null;
            buttons[i].navigation = nav;
        }
        Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -16f), new Vector2(width, 32f));
        hint.rectTransform.pivot = new Vector2(1f, 0f);
    }

    static RectTransform Node(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        return rt;
    }

    static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
    {
        var rt = Node(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.fontSize = size; t.color = color; t.alignment = alignment;
        // The bundled example heading font has a bevel material; use a private, flat UI instance.
        t.fontMaterial.DisableKeyword("BEVEL_ON");
        t.fontMaterial.DisableKeyword("GLOW_ON");
        t.fontMaterial.DisableKeyword("UNDERLAY_ON");
        t.fontMaterial.SetFloat("_FaceDilate", 0f);
        t.fontMaterial.SetFloat("_OutlineSoftness", 0f);
        t.outlineWidth = 0f;
        t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    static void Place(RectTransform rt, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
    }
}
