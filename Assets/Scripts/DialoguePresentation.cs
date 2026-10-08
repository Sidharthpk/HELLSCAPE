using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shared by runtime and editor builders, so existing scenes adopt the dialogue design on Play.
// Only presentation is rebuilt: dialogue queues, choices and story callbacks remain with their owners.
[AddComponentMenu("")]
public class DialoguePresentation : MonoBehaviour
{
    const string RootName = "CinematicDialogue";
    public static readonly Color Gold = new Color(1f, 0.78f, 0.36f);
    static readonly Color Paper = new Color(0.96f, 0.94f, 0.88f);
    static readonly Color Muted = new Color(0.70f, 0.69f, 0.67f);

    [SerializeField] RectTransform root;
    [SerializeField] TextMeshProUGUI speaker, body, hint;
    [SerializeField] RectTransform rule, eyebrow, shade;
    bool narration;
    [SerializeField] Button[] buttons;
    [SerializeField] TextMeshProUGUI[] labels;
    [SerializeField] bool choices;
    public void SetCountdown(float seconds)
    {
        if (eyebrow != null && eyebrow.TryGetComponent<TextMeshProUGUI>(out var label))
            label.text = "MEMORY FADING  " + Mathf.CeilToInt(seconds) + "s";
    }
    int count;
    float lastWidth = -1f, lastHeight = -1f;

    static TMP_FontAsset BodyFont => Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") ?? TMP_Settings.defaultFontAsset;
    static TMP_FontAsset HeadFont => Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF") ?? BodyFont;

    public static DialoguePresentation Subtitle(DialogueBox dialogue)
    {
        var v = Prepare(dialogue.group.gameObject, false);
        dialogue.text = v.body;
        dialogue.nameText = v.speaker;
        dialogue.hintText = v.hint;
        dialogue.accent = v.rule.GetComponent<Graphic>();
        dialogue.border = new Graphic[0];
        v.hint.text = "<color=#FFCA5C>" + dialogue.advanceKey.ToString().ToUpperInvariant() + "</color>   CONTINUE";
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
        v.speaker = Text("Speaker", v.root, HeadFont, 25f, Gold, TextAlignmentOptions.TopLeft);
        v.speaker.characterSpacing = 4f;
        v.body = Text("Line", v.root, BodyFont, isChoice ? 30f : 33f, Paper,
            isChoice ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Top);
        v.body.lineSpacing = 5f;
        v.body.outlineColor = new Color(0f, 0f, 0f, 0.9f);
        v.body.outlineWidth = 0.18f;
        v.hint = Text("Hint", v.root, HeadFont, 19f, Muted, TextAlignmentOptions.BottomRight);
        v.hint.characterSpacing = 2f;
        v.rule = Node("Rule", v.root);
        var line = v.rule.gameObject.AddComponent<Image>();
        line.color = Gold;
        line.raycastTarget = false;

        if (isChoice)
        {
            v.eyebrow = Text("ResponseLabel", v.root, HeadFont, 18f, Muted, TextAlignmentOptions.TopLeft).rectTransform;
            v.eyebrow.GetComponent<TMP_Text>().text = "YOUR RESPONSE";
            v.eyebrow.GetComponent<TMP_Text>().characterSpacing = 3f;
            v.hint.text = "1–4  CHOOSE     /     ARROWS + ENTER     /     CLICK";
            v.buttons = new Button[4];
            v.labels = new TextMeshProUGUI[4];
            for (int i = 0; i < 4; i++)
            {
                var card = Node("Response " + (i + 1), v.root);
                var graphic = card.gameObject.AddComponent<DialoguePanelGraphic>();
                graphic.color = new Color(0.045f, 0.035f, 0.055f, 0.94f);
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = graphic;
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(3.6f, 3.1f, 2.0f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color(5f, 4f, 2f, 1f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.12f;
                button.colors = colors;
                // The graphic's vertex tint is multiplied by the selectable's transition color.
                var number = Text("Key", card, HeadFont, 28f, Gold, TextAlignmentOptions.Center);
                number.text = (i + 1).ToString();
                Place(number.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(36f, 42f));
                var divider = Node("Divider", card);
                var ink = divider.gameObject.AddComponent<Image>(); ink.color = new Color(1f, 0.78f, 0.36f, 0.42f); ink.raycastTarget = false;
                Place(divider, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(65f, 0f), new Vector2(1f, 32f));
                v.labels[i] = Text("Answer", card, BodyFont, 26f, Paper, TextAlignmentOptions.MidlineLeft);
                Stretch(v.labels[i].rectTransform, 86f, 14f, 24f, 14f);
                v.labels[i].lineSpacing = 3f;
                v.buttons[i] = button;
                card.gameObject.SetActive(false);
            }
        }
        else
        {
            var shadow = Node("SubtitleShade", v.root);
            v.shade = shadow;
            shadow.SetAsFirstSibling();
            var panel = shadow.gameObject.AddComponent<DialoguePanelGraphic>();
            panel.color = new Color(0.015f, 0.015f, 0.025f, 0.76f);
            panel.edge = new Color(0f, 0f, 0f, 0.12f);
            panel.corner = 16f;
            panel.raycastTarget = false;
            Stretch(shadow, -24f, -10f, -24f, -10f);
        }
        v.Layout();
        return v;
    }

    public void ShowChoices(string who, string question, string[] options, Color color, System.Action<int> select)
    {
        if (eyebrow != null) eyebrow.GetComponent<TMP_Text>().text = "YOUR RESPONSE";
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
        hint.text = (count == 1 ? "1" : "1–" + count) + "  CHOOSE     /     ARROWS + ENTER     /     CLICK";
        Layout();
        if (count > 0 && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
    }

    public void RefreshLine() => Layout();

    // Narration: no panel, name plate or rule. Just white italics with a drop shadow, like a storybook caption.
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
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 1.1f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -1.1f);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.4f);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
        }
        else m.DisableKeyword("UNDERLAY_ON");
        body.color = on ? Color.white : body.color;
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
        float textWidth = width - 48f;
        float bodyHeight = Mathf.Max(44f, body.GetPreferredValues(body.text, textWidth, Mathf.Infinity).y + 8f);
        if (!choices)
        {
            float height = bodyHeight + (narration ? 44f : 100f);
            root.sizeDelta = new Vector2(width, height);
            Place(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -8f), new Vector2(width - 48f, 40f));
            speaker.rectTransform.pivot = new Vector2(0f, 1f);
            Place(rule, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -49f), new Vector2(46f, 2f));
            rule.pivot = new Vector2(0f, 1f);
            Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, narration ? -8f : -57f), new Vector2(textWidth, bodyHeight));
            body.rectTransform.pivot = new Vector2(0.5f, 1f);
            Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 0f), new Vector2(300f, 32f));
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
        Place(rule, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, cardsHeight + 32f), new Vector2(width, 1f));
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
