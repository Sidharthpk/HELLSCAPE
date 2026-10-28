using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The first sight of the red city: you step out of the flat (RedFlat's exit door) and the camera leaves your eyes for a
// slow, letterboxed look at it: the street, the abandoned cars, the Damned shambling between them, the Lost hanging in
// the sky, then a long pull-back over the city as the HELLSCAPE title lands, and a fade back to where you stand.
// The actors are stand-ins (no AI, nothing follows you) and go when it is over. Plays once; skippable (CutsceneSkip).
// Lines: HS5 rows A02_street_1 .. A02_street_6. Preview of every shot: HellScape > Cutscenes > First Sight Frames.
public class FirstSightCutscene : MonoBehaviour
{
    public static bool Playing { get; private set; }
    static bool done;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => Playing = done = false;

    public struct Cam { public Vector3 pos, look; public float fov; }
    public struct Shot { public float seconds; public System.Func<float, Cam> at; }

    // wired by RedFlat to the flat's exit door (Teleporter.onArrive: the screen is still black)
    public static void PlayOnce(Teleporter door)
    {
        if (done || door == null) return;
        done = true;
        new GameObject("First sight of the city").AddComponent<FirstSightCutscene>().Begin(door);
    }

    void Begin(Teleporter door) => StartCoroutine(Run(door));

    // ---------------------------------------------------------------- the shots

    static float Ease(float k) => Mathf.SmoothStep(0f, 1f, k);
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }
    public static float Ground(float x, float z, float from = 9f)
        => Physics.Raycast(new Vector3(x, from, z), Vector3.down, out var h, 60f, ~0, QueryTriggerInteraction.Ignore) ? h.point.y : 0f;

    // where the actors stand (the abandoned cars along the highway east of the flat)
    public static readonly Vector3[] DamnedAt = { new Vector3(64f, 0f, 27.5f), new Vector3(69f, 0f, 30.5f), new Vector3(73f, 0f, 26.5f), new Vector3(80f, 0f, 29f), new Vector3(58f, 0f, 24f) };

    // the five shots; start/facing are where you stepped out
    public static Shot[] Plan(Vector3 start, Vector3 facing)
    {
        float gy = Ground(start.x, start.z);
        Vector3 eye = new Vector3(start.x, gy + 1.7f, start.z), dir = Flat(facing);
        Vector3 carsA = new Vector3(35f, Ground(35f, 22f) + 1.35f, 22f), carsB = new Vector3(51f, Ground(51f, 25f) + 1.35f, 25f);
        Vector3 carsLook = new Vector3(84f, Ground(84f, 30f) + 2.4f, 30f);
        Vector3 groupAt = new Vector3(69f, Ground(69f, 29f) + 1.5f, 29f);
        Vector3 lowA = new Vector3(55f, Ground(55f, 33f) + 0.9f, 33f), lowB = new Vector3(58.5f, Ground(58.5f, 32f) + 1.0f, 32f);
        Vector3 up0 = eye, up1 = new Vector3(start.x + 6f, 280f, start.z + 185f);   // (far enough back to take in the city, near enough that its ends fall outside the frame)
        Vector3 upLook0 = eye + new Vector3(0.25f, 2.3f, -1f).normalized * 25f, upLook1 = new Vector3(start.x + 20f, 0f, -35f);
        return new[]
        {
            new Shot { seconds = 9f, at = k => { float e = Ease(k); var p = eye + dir * (3.2f * e) + Vector3.up * (0.9f * e); return new Cam { pos = p, look = p + dir * 18f + Vector3.up * (0.8f + 3.5f * e), fov = Mathf.Lerp(60f, 52f, e) }; } },
            new Shot { seconds = 8f, at = k => { float e = Ease(k); return new Cam { pos = Vector3.Lerp(carsA, carsB, e), look = carsLook + Vector3.up * (0.6f * e), fov = 50f }; } },
            new Shot { seconds = 7f, at = k => { float e = Ease(k); return new Cam { pos = Vector3.Lerp(lowA, lowB, e), look = groupAt, fov = Mathf.Lerp(44f, 34f, e) }; } },
            new Shot { seconds = 30f, at = k =>
                {
                    float e = Ease(k), rise = Mathf.Pow(e, 1.7f);
                    return new Cam { pos = Vector3.Lerp(up0, up1, rise), look = Vector3.Lerp(upLook0, upLook1, Ease(Mathf.Clamp01(k * 1.25f))), fov = Mathf.Lerp(64f, 44f, e) };
                } },
        };
    }

    // ---------------------------------------------------------------- the actors

    // the Damned: the zombie model with everything switched off but its animation, shuffling or standing
    public static GameObject Damned(GameObject prefab, Transform parent, Vector3 at, float yaw, float speed)
    {
        var go = Instantiate(prefab, at, Quaternion.Euler(0f, yaw, 0f), parent);   // (the parent is inactive while this is built: nothing wakes)
        go.name = "Damned (cutscene)";
        foreach (var b in go.GetComponentsInChildren<Behaviour>(true)) if (!(b is Animator)) b.enabled = false;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) r.isKinematic = true;
        go.GetComponentInChildren<Animator>().applyRootMotion = false;
        go.AddComponent<Actor>().speed = speed;
        return go;
    }

    // the Lost: the floating pose in ghost blue; LostSoul drifts and flickers it (never notices you here)
    public static GameObject Lost(CityWalkers src, Transform parent, Vector3 at, float scale)
    {
        var go = Instantiate(src.lostModel, at, Quaternion.Euler(0f, Random.value * 360f, 0f), parent);
        go.name = "Lost (cutscene)";
        go.transform.localScale *= scale;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (src.ghost != null) { var mats = r.sharedMaterials; for (int k = 0; k < mats.Length; k++) mats[k] = src.ghost; r.sharedMaterials = mats; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var anim = go.GetComponentInChildren<Animator>() ?? go.AddComponent<Animator>();
        anim.runtimeAnimatorController = src.floatController;
        anim.applyRootMotion = false;
        var soul = go.AddComponent<LostSoul>();
        soul.noticeAfter = 1e9f;
        soul.driftRadius = Random.Range(3f, 7f) * scale; soul.driftSpeed = Random.Range(.35f, .7f); soul.bobHeight = .5f * scale;
        soul.alphaRange = new Vector2(.3f, .6f);
        return go;
    }

    // everything the shots look at, under one inactive root (the caller activates it)
    public static Transform Stage(Vector3 start, CityWalkers src)
    {
        var root = new GameObject("First sight (stage)").transform;
        root.gameObject.SetActive(false);
        if (src == null) return root;
        if (src.zombiePrefab != null)
            for (int i = 0; i < DamnedAt.Length; i++)
            {
                var p = DamnedAt[i];
                bool walks = i % 2 == 0;
                Damned(src.zombiePrefab, root, new Vector3(p.x, Ground(p.x, p.z), p.z), walks ? 262f : 80f + 25f * i, walks ? .4f : 0f);
            }
        if (src.lostModel != null)
        {
            var rng = new System.Random(5);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int i = 0; i < 14; i++) Lost(src, root, new Vector3(start.x + R(-16f, 16f), R(10f, 42f), start.z + R(-40f, -9f)), 1.5f);   // over the street, above you
            for (int i = 0; i < 8; i++) Lost(src, root, new Vector3(start.x + R(-70f, 70f), R(45f, 90f), start.z + R(-60f, 40f)), 5f);      // higher, bigger: you rise past them
            for (int i = 0; i < 18; i++) Lost(src, root, new Vector3(start.x + R(-300f, 260f), R(70f, 150f), R(-260f, 40f)), 12f);          // over the whole city: huge, from far
            for (int i = 0; i < 6; i++) Lost(src, root, new Vector3(R(40f, 110f), R(14f, 30f), R(10f, 48f)), 1f);                           // over the cars
        }
        return root;
    }

    // the picture lifted for the aerial: a temporary global volume on top that overrides only the exposure, at the level the
    // scene sets (the highest-priority colour adjustment that overrides it) plus the lift. The scene's own volumes are never touched.
    public class Grade
    {
        readonly GameObject go;
        readonly UnityEngine.Rendering.VolumeProfile profile;
        readonly UnityEngine.Rendering.Universal.ColorAdjustments adjust;
        readonly float exposure;

        public Grade()
        {
            float best = float.MinValue;
            foreach (var v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
                if (v.isGlobal && v.sharedProfile != null && v.priority > best && v.sharedProfile.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments c) && c.postExposure.overrideState)
                { best = v.priority; exposure = c.postExposure.value; }
            go = new GameObject("First sight grade") { hideFlags = HideFlags.HideAndDontSave };
            var volume = go.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true; volume.priority = 1000f; volume.weight = 1f;
            profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            adjust = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(false);
            adjust.postExposure.Override(exposure);
            volume.sharedProfile = profile;
        }
        public void Lift(float stops) => adjust.postExposure.value = exposure + stops;
        public void Restore()
        {
            if (Application.isPlaying) { Destroy(go); Destroy(profile); } else { DestroyImmediate(go); DestroyImmediate(profile); }
        }
        static void Destroy(Object o) => Object.Destroy(o);
        static void DestroyImmediate(Object o) => Object.DestroyImmediate(o);
    }

    // sets the walk (or stand) of a stand-in once it is awake, and shuffles it along its own facing
    class Actor : MonoBehaviour
    {
        public float speed;
        void Start() { var a = GetComponentInChildren<Animator>(); if (a != null) a.SetFloat("Speed", speed); }
        void Update() { if (speed > 0f) transform.position += transform.forward * (.5f * Time.deltaTime); }
    }

    // ---------------------------------------------------------------- the cutscene

    IEnumerator Run(Teleporter door)
    {
        Playing = true;
        var cam = Camera.main;
        var fpc = FindFirstObjectByType<FirstPersonController>();
        var fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
        string arriveLine = door.arriveLine; door.arriveLine = "";   // (said after, not over, the cutscene)
        Vector3 start = door.destination.position; Vector3 facing = door.destination.forward;

        bool canMove = fpc != null && fpc.playerCanMove, canLook = fpc != null && fpc.cameraCanMove;
        if (fpc != null) fpc.playerCanMove = fpc.cameraCanMove = false;
        var lp = cam.transform.localPosition; var lr = cam.transform.localRotation; float fov0 = cam.fieldOfView, fog0 = RenderSettings.fogDensity;
        var arms = cam.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();   // (the fists ride the camera)
        foreach (var r in arms) r.enabled = false;
        CutsceneHUD.Hide(true);
        var bars = StartCoroutine(Bars(1f));

        var stage = Stage(start, FindFirstObjectByType<CityWalkers>(FindObjectsInactive.Include));
        stage.gameObject.SetActive(true);
        var title = MakeTitle();
        var shots = Plan(start, facing);
        grade = new Grade();

        yield return new WaitForSeconds(1f);   // (the door's fade-in)
        yield return Play(cam, shots[0], fog0, fog0, "A02_street_1", 0.4f);
        yield return Play(cam, shots[1], fog0, fog0, "A02_street_2", 0.8f);
        yield return Play(cam, shots[2], fog0, fog0, "A02_street_3", 0.8f);
        // the long pull-back: the Lost overhead, the whole city, the title
        yield return Play(cam, shots[3], fog0, AerialFog, "A02_street_4", 1.2f,
            (0.45f, () => StartCoroutine(Bars(0f, 9f))), (0.45f, () => Say("A02_street_5")), (0.7f, () => StartCoroutine(TitleCard(title))));
        while (title.alpha > 0f || titleBusy) yield return null;

        if (fader != null) yield return fader.FadeTo(1f, 1f);
        cam.transform.localPosition = lp; cam.transform.localRotation = lr; cam.fieldOfView = fov0;
        RenderSettings.fogDensity = fog0;
        grade.Restore();
        foreach (var r in arms) if (r != null) r.enabled = true;
        Destroy(stage.gameObject); Destroy(title.transform.root.gameObject);
        StopCoroutine(bars);
        yield return StartCoroutine(Bars(0f, .05f));   // (behind the black)
        CutsceneHUD.Hide(false);
        if (fpc != null) { fpc.playerCanMove = canMove; fpc.cameraCanMove = canLook; }
        if (fader != null) yield return fader.FadeTo(0f, 1.2f);
        yield return RevisedPlotDirector.Speak(HS5.S("A02_street_6"));
        if (!string.IsNullOrEmpty(arriveLine)) { door.arriveLine = arriveLine; DialogueBox.Instance?.Say(arriveLine); }
        Playing = false;
    }

    static void Say(string trigger) { if (DialogueBox.Instance != null) DialogueBox.Instance.Say(HS5.S(trigger)); }

    // one shot; the lines (if any) start after `lineAt` seconds and the shot holds until they are spoken
    IEnumerator Play(Camera cam, Shot shot, float fogFrom, float fogTo, string lines, float lineAt, params (float k, System.Action what)[] cues)
    {
        float t = 0f; bool said = lines == null; int cue = 0;
        while (true)
        {
            t += Time.deltaTime;
            if (!said && t >= lineAt) { Say(lines); said = true; }
            float k = Mathf.Clamp01(t / shot.seconds);
            while (cue < cues.Length && k >= cues[cue].k) cues[cue++].what();
            if (t >= shot.seconds && said && (DialogueBox.Instance == null || !DialogueBox.Instance.Busy)) break;
            var c = shot.at(k);
            // a breath of handheld: never perfectly still
            float s = Time.time;
            c.pos += new Vector3(Mathf.PerlinNoise(s * .7f, 1f) - .5f, Mathf.PerlinNoise(s * .8f, 5f) - .5f, Mathf.PerlinNoise(s * .6f, 9f) - .5f) * .06f;
            cam.transform.position = c.pos;
            cam.transform.rotation = Quaternion.LookRotation(c.look - c.pos);
            cam.fieldOfView = c.fov;
            RenderSettings.fogDensity = Mathf.Lerp(fogFrom, fogTo, Ease(k));
            if (fogTo != fogFrom) grade.Lift(AerialLift * Ease(k));   // (the pull-back: the city lifted out of the dark)
            yield return null;
        }
        // (skip the pause the shot above would add before the cut: the next shot starts at once)
    }

    // ---------------------------------------------------------------- bars and title

    IEnumerator Bars(float to, float seconds = .8f)
    {
        var top = GameObject.Find("LetterboxTop")?.GetComponent<RectTransform>();
        var bottom = GameObject.Find("LetterboxBottom")?.GetComponent<RectTransform>();
        if (top == null) yield break;
        float from = top.sizeDelta.y, h = 130f * to;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float v = Mathf.Lerp(from, h, Mathf.SmoothStep(0f, 1f, t / seconds));
            top.sizeDelta = new Vector2(top.sizeDelta.x, v);
            if (bottom != null) bottom.sizeDelta = new Vector2(bottom.sizeDelta.x, v);
            yield return null;
        }
        top.sizeDelta = new Vector2(top.sizeDelta.x, h);
        if (bottom != null) bottom.sizeDelta = new Vector2(bottom.sizeDelta.x, h);
    }

    bool titleBusy;
    Grade grade;
    public const float AerialLift = 0.5f, AerialFog = 0.0007f;   // what the pull-back ends on: stops of exposure, and a thin fog (at that distance the red fog would wash the city out)

    // the HELLSCAPE art (Resources/HellscapeUI), centred above the dialogue; over the letterbox, under the skip prompt
    static CanvasGroup MakeTitle()
    {
        var go = new GameObject("First sight title", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 205;
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = .5f;
        var group = go.GetComponent<CanvasGroup>(); group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
        var art = Resources.Load<Sprite>("HellscapeUI/HellScape Title");
        RectTransform rt;
        if (art != null)
        {
            var img = new GameObject("Logo", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.sprite = art; img.preserveAspect = true; img.raycastTarget = false;
            var o = img.gameObject.AddComponent<Outline>(); o.effectColor = new Color(.015f, 0f, 0f, .8f); o.effectDistance = new Vector2(3f, -3f);
            var sh = img.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, .85f); sh.effectDistance = new Vector2(10f, -14f);
            rt = img.rectTransform; float aspect = art.rect.height / art.rect.width, w = Mathf.Min(1700f, 900f / aspect);   // (nearly the full width, never taller than the screen)
            rt.sizeDelta = new Vector2(w, w * aspect);
        }
        else
        {
            var tx = new GameObject("Logo", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            tx.font = GameFont.Main; tx.text = "HELLSCAPE"; tx.fontSize = 190f; tx.characterSpacing = 28f; tx.color = new Color(.85f, .08f, .04f); tx.alignment = TextAlignmentOptions.Center;
            rt = tx.rectTransform; rt.sizeDelta = new Vector2(1600f, 300f);
        }
        rt.SetParent(go.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = new Vector2(0f, 60f);
        return group;
    }

    // slams in out of the dark, a flicker as it lands, holds, and burns out
    IEnumerator TitleCard(CanvasGroup g)
    {
        titleBusy = true;
        var tr = g.transform.GetChild(0);
        for (float t = 0f; t < 1.6f; t += Time.deltaTime) { float k = t / 1.6f; g.alpha = k; tr.localScale = Vector3.one * Mathf.Lerp(1.18f, 1f, Ease(k)); yield return null; }
        for (float t = 0f; t < .6f; t += Time.deltaTime) { g.alpha = Random.value < .3f ? .3f : 1f; yield return null; }
        g.alpha = 1f; tr.localScale = Vector3.one;
        yield return new WaitForSeconds(3f);
        for (float t = 0f; t < 1.5f; t += Time.deltaTime) { g.alpha = 1f - t / 1.5f; yield return null; }
        g.alpha = 0f;
        titleBusy = false;
    }
}
