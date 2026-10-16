using TMPro;
using UnityEngine;

// The objective owns the top strip; boss labels and health tracks occupy the row below it.
[ExecuteAlways]
public class BossHUDLayout : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= Loaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += Loaded;
        Install();
    }

    static void Loaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Install();

    public static void Install()
    {
        foreach (var rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rect.name == "BossBar" || rect.name == "DemonHealthBar")
            {
                if (!rect.TryGetComponent<BossHUDLayout>(out var layout)) layout = rect.gameObject.AddComponent<BossHUDLayout>();
                layout.Refresh();
            }
    }

    public static bool AnyVisible
    {
        get
        {
            foreach (var layout in FindObjectsByType<BossHUDLayout>(FindObjectsSortMode.None))
            {
                var canvas = layout.GetComponentInParent<Canvas>();
                if (layout.isActiveAndEnabled && canvas != null && canvas.enabled) return true;
            }
            return false;
        }
    }

    void LateUpdate() => Refresh();
    void OnEnable() => Refresh();

    public void Refresh()
    {
        if (!(transform is RectTransform rect) || !(rect.parent is RectTransform parent)) return;
        var canvas = GetComponentInParent<Canvas>();
        float inset = Application.isPlaying ? ObjectiveUI.TopInset(canvas) : 64f;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(Mathf.Min(720f, Mathf.Max(200f, parent.rect.width - 64f)), 36f);
        rect.anchoredPosition = new Vector2(0f, -(inset + 16f + rect.sizeDelta.y * .5f));
        if (TryGetComponent<TMP_Text>(out var title))
        {
            title.enableAutoSizing = true; title.fontSizeMin = 18f; title.fontSizeMax = 28f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
        }
        var back = rect.Find("Back") as RectTransform;
        if (back != null)
        {
            back.sizeDelta = new Vector2(Mathf.Min(600f, rect.sizeDelta.x - 40f), 16f);
            back.anchoredPosition = new Vector2(0, -30f);
        }
    }
}
