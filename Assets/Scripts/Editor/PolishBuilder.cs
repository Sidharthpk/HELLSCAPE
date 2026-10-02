using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Playtest polish, round 3:
//   HellScape > Characters > Unmask Elias           - the killer's mask meshes off (MainGameScene + Prologue): his own face
//   HellScape > UI > Restyle Death Screen           - "YOU DIED" like the title: big red Oswald, white DOS Pixel options, no frame
//   HellScape > Finale > Wire Boss Death Cutscene   - the drag-under cutscene gets your controls to lock
//   HellScape > Hell City > No Title After Deer     - the HELLSCAPE card no longer slams in after the deer cutscene
public static class PolishBuilder
{
    const string PrologueScene = "Assets/Scenes/Prologue.unity";

    [MenuItem("HellScape/Characters/Unmask Elias")]
    public static void Unmask()
    {
        int n = UnmaskIn(EditorSceneManager.GetActiveScene());
        var path = AssetDatabase.FindAssets("Prologue t:Scene").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.EndsWith("/Prologue.unity"));
        if (path != null)
        {
            var already = EditorSceneManager.GetSceneByPath(path);
            var sc = already.isLoaded ? already : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            n += UnmaskIn(sc);
            EditorSceneManager.SaveScene(sc);
            if (!already.isLoaded) EditorSceneManager.CloseScene(sc, true);
        }
        Debug.Log("Unmask Elias: " + n + " mask pieces switched off.");
    }

    static int UnmaskIn(UnityEngine.SceneManagement.Scene sc)
    {
        int n = 0;
        foreach (var root in sc.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if ((r.name.Contains("J8_Mask") || r.name.Contains("J9_Mask") || r.sharedMaterials.Any(m => m != null && m.name == "Jason_Mask")) && r.enabled)
                {
                    Undo.RecordObject(r, "Unmask");
                    r.enabled = false;
                    n++;
                }
        if (n > 0) EditorSceneManager.MarkSceneDirty(sc);
        return n;
    }

    [MenuItem("HellScape/UI/Restyle Death Screen")]
    public static void RestyleDeath()
    {
        var story = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.name == "StoryCanvas").transform;
        var title = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.name == "TitleCanvas").transform;
        var panel = story.Find("GameOverPanel");
        var titleRed = title.Find("UI/Title/Red").GetComponent<TMP_Text>();
        var titleMenu = title.Find("UI/Start").GetComponent<TMP_Text>();
        var titleShade = title.Find("UI/Shade").GetComponent<Image>();

        // the backdrop: black, darkest at the edges (the title's shade)
        var bg = panel.GetComponent<Image>();
        Undo.RecordObject(bg, "Death screen");
        bg.color = new Color(0f, 0f, 0f, 0.86f);
        var shade = panel.Find("Shade");
        if (shade == null)
        {
            shade = new GameObject("Shade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).transform;
            Undo.RegisterCreatedObjectUndo(shade.gameObject, "Death screen");
            shade.SetParent(panel, false);
            shade.SetSiblingIndex(0);
            var rt = (RectTransform)shade;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        var si = shade.GetComponent<Image>();
        si.sprite = titleShade.sprite;
        si.color = titleShade.color;
        si.raycastTarget = false;

        // no frame
        var frame = panel.Find("Frame");
        if (frame != null) { Undo.RecordObject(frame.gameObject, "Death screen"); frame.gameObject.SetActive(false); }

        // YOU DIED: the title's red, with its drop shadow, no outline
        var yd = panel.Find("YouDied");
        foreach (Transform t in yd)
        {
            var tm = t.GetComponent<TMP_Text>();
            Undo.RecordObject(tm, "Death screen");
            Undo.RecordObject(t.gameObject, "Death screen");
            tm.font = titleRed.font;
            tm.fontSize = 170f;
            if (t.name == "Outline") t.gameObject.SetActive(false);
            if (t.name == "Face") tm.color = titleRed.color;
            if (t.name == "Shadow") { tm.color = new Color(0f, 0f, 0f, 0.85f); ((RectTransform)t).anchoredPosition = new Vector2(7f, -7f); }
        }

        // the options: plain white pixel words like START / QUIT, red when you point at them
        foreach (var (name, label, y) in new[] { ("RestartButton", "RETRY", -90f), ("QuitButton", "QUIT", -165f) })
        {
            var b = panel.Find(name);
            if (b == null) continue;
            var img = b.GetComponent<Image>();
            Undo.RecordObject(img, "Death screen");
            img.sprite = null;
            img.color = new Color(0f, 0f, 0f, 0f);   // still catches the mouse
            Undo.RecordObject(b, "Death screen");
            ((RectTransform)b).anchoredPosition = new Vector2(0f, y);
            ((RectTransform)b).sizeDelta = new Vector2(360f, 60f);
            var txt = b.GetComponentInChildren<TMP_Text>(true);
            Undo.RecordObject(txt, "Death screen");
            txt.text = label;
            txt.font = titleMenu.font;
            txt.fontSize = titleMenu.fontSize;
            txt.color = Color.white;
            var btn = b.GetComponent<Button>();
            Undo.RecordObject(btn, "Death screen");
            btn.transition = Selectable.Transition.None;
            var changer = b.GetComponents<MonoBehaviour>().FirstOrDefault(m => m.GetType().Name == "ButtonTextColorChanger");
            if (changer != null)
            {
                var so = new SerializedObject(changer);
                so.FindProperty("defaultColor").colorValue = Color.white;
                so.FindProperty("highlightedColor").colorValue = titleRed.color;
                so.FindProperty("pressedColor").colorValue = new Color(0.45f, 0.03f, 0.02f);
                so.ApplyModifiedProperties();
            }
        }

        // the hint under it, like the title's [ENTER]
        var hint = panel.Find("Hint");
        var titleHint = title.Find("UI/Hint").GetComponent<TMP_Text>();
        if (hint == null)
        {
            hint = Object.Instantiate(titleHint.gameObject, panel).transform;
            hint.name = "Hint";
            Undo.RegisterCreatedObjectUndo(hint.gameObject, "Death screen");
        }
        var hr = (RectTransform)hint;
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0.5f, 0.5f);
        hr.sizeDelta = new Vector2(1000f, 40f);
        hr.anchoredPosition = new Vector2(0f, -240f);
        var ht = hint.GetComponent<TMP_Text>();
        ht.text = "the city isn't finished with you";
        ht.alignment = TextAlignmentOptions.Center;
        // on top of everything else on the story canvas (the dialogue box, the HUD)
        panel.SetAsLastSibling();
        var fader = story.Find("ScreenFader");
        if (fader != null) fader.SetAsLastSibling();
        hint.gameObject.SetActive(true);

        // where the YOU DIED sits
        ((RectTransform)yd).anchoredPosition = new Vector2(0f, 90f);

        var go = Object.FindFirstObjectByType<GameOverScreen>(FindObjectsInactive.Include);
        Undo.RecordObject(go, "Death screen");
        go.playerCam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name == "PlayerCamera");
        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Debug.Log("Death screen restyled.");
    }

    // Kessler fades to a ghost whenever he stands between the driving camera and the van (BossSeeThrough)
    [MenuItem("HellScape/Finale/See-Through Boss")]
    public static void SeeThroughBoss()
    {
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        if (boss == null) { Debug.LogError("See-through boss: open MainGameScene."); return; }
        var st = boss.GetComponent<BossSeeThrough>();
        if (st == null) st = Undo.AddComponent<BossSeeThrough>(boss.gameObject);
        st.boss = boss;
        EditorUtility.SetDirty(st);
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        Debug.Log("Kessler goes see-through when he's between the driving camera and the van.");
    }

    [MenuItem("HellScape/Finale/Wire Boss Death Cutscene")]
    public static void WireBossDeath()
    {
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        var kc = Object.FindFirstObjectByType<KesslerConfrontation>(FindObjectsInactive.Include);
        var player = GameObject.FindGameObjectWithTag("Player");
        Undo.RecordObject(boss, "Boss death");
        boss.lockDuring = kc != null ? kc.lockDuring.Where(b => b != null && (player == null || b.transform.IsChildOf(player.transform))).ToArray() : new Behaviour[0];
        var water = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(r => r.name.StartsWith("WaterBlock"));
        if (water != null) boss.waterY = water.bounds.max.y;
        EditorUtility.SetDirty(boss);
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
        Debug.Log("Boss death: " + boss.lockDuring.Length + " controls locked while it plays, water at " + boss.waterY);
    }

    [MenuItem("HellScape/UI/Bleeding Title Text")]
    public static void BleedingText()
    {
        int n = 0;
        foreach (var tm in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool title = tm.transform.parent != null && tm.transform.parent.name == "Title" && tm.transform.parent.parent != null && tm.transform.parent.parent.name == "UI";
            bool died = tm.transform.parent != null && tm.transform.parent.name == "YouDied" && (tm.name == "Face" || tm.name == "Shadow");
            if (!title && !died) continue;
            var b = tm.GetComponent<TextBleedIn>();
            if (b == null) b = Undo.AddComponent<TextBleedIn>(tm.gameObject);
            Undo.RecordObject(b, "Bleed");
            b.delay = title ? 1.1f : 0.35f;       // the title waits for its screen's fade; YOU DIED comes up right behind the fade
            b.duration = title ? 2.6f : 2.2f;
            // thin, gaunt letters set wide apart (Oswald Bold eaten back; below about -0.5 the strokes break up)
            b.to = -0.4f;
            Undo.RecordObject(tm, "Bleed");
            tm.characterSpacing = 18f;
            EditorUtility.SetDirty(tm);
            EditorSceneManager.MarkSceneDirty(tm.gameObject.scene);
            EditorUtility.SetDirty(b);
            n++;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Bleeding title text on " + n + " texts.");
    }

    [MenuItem("HellScape/Office/Body Bag Jumpscares")]
    public static void BodyBagScares()
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Enemies/body-bag/source/Body.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        var office = GameObject.Find("OfficeAct").transform.Find("Office");
        var bags = office.Cast<Transform>().Where(t => t.name == "Body" && PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) != null).ToArray();
        var kc = Object.FindFirstObjectByType<KesslerConfrontation>(FindObjectsInactive.Include);
        var player = GameObject.FindGameObjectWithTag("Player");
        var cam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.name == "PlayerCamera");
        for (int i = 0; i < bags.Length; i++)
        {
            var s = bags[i].GetComponent<BodyBagScare>();
            if (s == null) s = Undo.AddComponent<BodyBagScare>(bags[i].gameObject);
            Undo.RecordObject(s, "Scare");
            s.clip = clip;
            s.big = true;                         // whichever you reach first does the full scare; the other only twitches
            s.player = player != null ? player.transform : null;
            s.playerCam = cam;
            s.lockDuring = kc != null ? kc.lockDuring.Where(b => b != null && (player == null || b.transform.IsChildOf(player.transform))).ToArray() : new Behaviour[0];
            s.hideDuring = cam.transform.Cast<Transform>().Where(t => t.GetComponentInChildren<Renderer>(true) != null).Select(t => t.gameObject).ToArray();
            EditorUtility.SetDirty(s);
        }
        EditorSceneManager.MarkSceneDirty(office.gameObject.scene);
        Debug.Log("Body bag jumpscares on " + bags.Length + " bags.");
    }

    [MenuItem("HellScape/Hell City/No Title After Deer")]
    public static void NoTitleAfterDeer()
    {
        var dc = Object.FindFirstObjectByType<DeerCutscene>(FindObjectsInactive.Include);
        Undo.RecordObject(dc, "No title");
        for (int i = dc.onDone.GetPersistentEventCount() - 1; i >= 0; i--)
            if (dc.onDone.GetPersistentTarget(i) is TitleCard) UnityEventTools.RemovePersistentListener(dc.onDone, i);
        EditorUtility.SetDirty(dc);
        EditorSceneManager.MarkSceneDirty(dc.gameObject.scene);
        Debug.Log("Deer cutscene: title card removed (" + dc.onDone.GetPersistentEventCount() + " calls left after it).");
    }
}
