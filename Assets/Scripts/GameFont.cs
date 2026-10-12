using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// The one font of the game: Special Elite, a worn typewriter (Apache 2.0, licence beside it in Resources), on every
// text but the title lettering (HELLSCAPE, YOU DIED) and the blood on the office wall. New UI asks for GameFont.Main;
// texts saved in the scenes with another font are switched as the scene loads (ponytail: at load, so outside Play
// the editor still shows the saved fonts; bake it into the builders if that grates).
public static class GameFont
{
    static TMP_FontAsset main;
    static Font ttf;

    public static TMP_FontAsset Main => main != null ? main : (main = Resources.Load<TMP_FontAsset>("SpecialElite SDF") ?? TMP_Settings.defaultFontAsset);
    // (the same face for the legacy Text, TextMesh and OnGUI)
    public static Font Ttf => ttf != null ? ttf : (ttf = Resources.Load<Font>("SpecialElite-Regular"));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init() { SceneManager.sceneLoaded -= Apply; SceneManager.sceneLoaded += Apply; }

    static bool Title(string text)
    {
        text = (text ?? "").Replace(" ", "").Replace("\n", "").ToUpperInvariant();
        return text == "HELLSCAPE" || text == "YOUDIED";
    }

    static void Apply(Scene scene, LoadSceneMode mode)
    {
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.font == Main || (t.font != null && t.font.name.StartsWith("Nosifer")) || Title(t.text)) continue;
            t.font = Main;
            t.fontSharedMaterial = Main.material;
        }
        if (Ttf == null) return;
        foreach (var t in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)) t.font = Ttf;
    }
}
