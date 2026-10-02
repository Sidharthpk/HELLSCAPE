using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// The first thing you see in hell: a zombie hunched over a dead deer in the street, eating. Walk up and the camera
// leaves you: a low shot creeping in on it, a close-up of it feeding, then it snaps round and screams straight into
// the lens. Back to you, and the street wakes up (onDone: the zombie spawner, the HELLSCAPE title).
// Played by the Deer Zombie Trigger. Built by HellScape > Hell City > Build Deer Cutscene.
public class DeerCutscene : MonoBehaviour
{
    public Transform feeder;                 // the feeding zombie (its holder: the zombie itself is reset to 0 inside it)
    public Animator feederAnim;
    public Transform deer;
    public string feedState = "zombiebitting2";
    public string screamState = "Scream";

    [Header("The player")]
    public Transform player;
    public Camera playerCam;
    public Behaviour[] lockDuring;           // movement, look, punching, gun
    public GameObject[] hideDuring;          // the arms and the gun on the camera

    [Header("Look")]
    public RectTransform letterboxTop;
    public RectTransform letterboxBottom;
    public float letterboxHeight = 130f;
    public ScreenFader fader;
    public float fogDuring = 0.012f;

    [TextArea] public string feedLine = "...what is that?|It's-- it's EATING it.";
    [TextArea] public string afterLine = "It saw me. I need to get off this street.";

    public UnityEvent onDone;

    bool played;

    public void Play()
    {
        if (played) return;
        played = true;
        // driving past it: no cutscene, the street just wakes up
        if (feeder == null || playerCam == null || !playerCam.isActiveAndEnabled) { onDone?.Invoke(); return; }
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // ---- hand the camera over
        foreach (var b in lockDuring) if (b != null) b.enabled = false;
        if (player != null && player.TryGetComponent(out Rigidbody prb) && !prb.isKinematic) prb.linearVelocity = new Vector3(0f, prb.linearVelocity.y, 0f);
        var hidden = new System.Collections.Generic.List<GameObject>();
        foreach (var g in hideDuring) if (g != null && g.activeSelf) { g.SetActive(false); hidden.Add(g); }
        CutsceneHUD.Hide(true);

        Transform cam = playerCam.transform;
        Transform camParent = cam.parent;
        Vector3 camLocalPos = cam.localPosition;
        Quaternion camLocalRot = cam.localRotation;
        float fov0 = playerCam.fieldOfView;
        float fog0 = RenderSettings.fogDensity;

        if (fader != null) yield return fader.FadeTo(1f, 0.25f);
        cam.SetParent(null, true);
        RenderSettings.fogDensity = Mathf.Min(fog0, fogDuring);
        SetBars(1f);

        Vector3 z = feeder.position;
        Vector3 toPlayer = player != null ? Flat(player.position - z) : Flat(-feeder.forward);
        if (toPlayer.sqrMagnitude < 0.01f) toPlayer = Vector3.forward;
        toPlayer.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, toPlayer);
        Vector3 body = z + Vector3.up * 0.55f;
        Vector3 head = Head();

        var moans = Sounds.Clip("Hell City/Zombie moans", null);
        var src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 1f; src.minDistance = 2f; src.maxDistance = 25f; src.loop = true;
        src.transform.position = z;
        if (moans != null) { src.clip = moans; src.pitch = 0.8f; src.volume = 0.8f; src.Play(); }

        // ---- 1. low and wide, creeping in on it
        playerCam.fieldOfView = 45f;
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.4f));
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(feedLine);
        Vector3 wideFrom = z + toPlayer * 6f + side * 2.2f + Vector3.up * 0.7f;
        Vector3 wideTo = z + toPlayer * 4f + side * 1.4f + Vector3.up * 0.6f;
        for (float t = 0f; t < 3.2f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 3.2f);
            Place(cam, Vector3.Lerp(wideFrom, wideTo, k), body);
            yield return null;
        }

        // ---- 2. closer, over it, looking down at it tearing into the deer
        Vector3 closeFrom = z + toPlayer * 2.6f - side * 1.2f + Vector3.up * 1.5f;
        Vector3 closeTo = z + toPlayer * 2.0f - side * 0.9f + Vector3.up * 1.3f;
        Vector3 CloseLook() => Vector3.Lerp(body, Head(), 0.5f);
        float closeFor = 2.6f;
        for (float t = 0f; t < closeFor; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / closeFor);
            Place(cam, Vector3.Lerp(closeFrom, closeTo, k) + Random.insideUnitSphere * 0.004f, CloseLook());
            // let the line finish, but never hang on it
            if (t > closeFor - 0.1f && closeFor < 5f && DialogueBox.Instance != null && DialogueBox.Instance.Busy) closeFor += Time.deltaTime;
            yield return null;
        }

        // ---- 3. it snaps round at the camera and screams
        Quaternion faceRot0 = feeder.rotation;
        Quaternion faceCam = Quaternion.LookRotation(toPlayer);
        Vector3 screamCam = z + toPlayer * 2.4f + Vector3.up * 1.2f;
        bool canScream = feederAnim != null && feederAnim.HasState(0, Animator.StringToHash(screamState));
        if (canScream) feederAnim.CrossFadeInFixedTime(screamState, 0.15f, 0);
        src.Stop();
        var scream = Sounds.Clip("Hell City/Zombie scream", null);
        if (scream != null) { src.loop = false; src.pitch = 0.85f; src.volume = 1f; src.PlayOneShot(scream, Sounds.Volume("Hell City/Zombie scream")); }
        for (float t = 0f; t < 2.4f; t += Time.deltaTime)
        {
            feeder.rotation = Quaternion.Slerp(faceRot0, faceCam, Mathf.SmoothStep(0f, 1f, t / 0.35f));
            float shake = t < 0.4f ? 0f : 0.05f * (1f - t / 2.4f);
            Vector3 p = Vector3.Lerp(screamCam, Head() + toPlayer * 1.3f, Mathf.SmoothStep(0f, 1f, t / 1.6f));
            Place(cam, p + Random.insideUnitSphere * shake, Head());
            playerCam.fieldOfView = Mathf.Lerp(45f, 38f, t / 2.4f);
            yield return null;
        }

        // ---- back to you
        if (fader != null) yield return fader.FadeTo(1f, 0.2f);
        if (canScream) feederAnim.Play(feedState, 0, 0f);
        feeder.rotation = faceRot0;
        Destroy(src, 0.1f);
        cam.SetParent(camParent, false);
        cam.localPosition = camLocalPos;
        cam.localRotation = camLocalRot;
        playerCam.fieldOfView = fov0;
        RenderSettings.fogDensity = fog0;
        SetBars(0f);
        CutsceneHUD.Hide(false);
        foreach (var g in hidden) if (g != null) g.SetActive(true);
        foreach (var b in lockDuring) if (b != null) b.enabled = true;
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.4f));
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(afterLine)) DialogueBox.Instance.Say(afterLine);
        onDone?.Invoke();
    }

    Vector3 Head()
    {
        if (feederAnim != null)
        {
            var h = FindBone(feederAnim.transform, "Head");
            if (h != null) return h.position;
        }
        return feeder.position + Vector3.up * 0.8f;
    }

    static Transform FindBone(Transform root, string suffix)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>())
            if (t.name.EndsWith(":" + suffix) || t.name == suffix) return t;
        return null;
    }

    static void Place(Transform cam, Vector3 at, Vector3 look)
    {
        cam.position = at;
        cam.rotation = Quaternion.LookRotation(look - at);
    }

    void SetBars(float k)
    {
        foreach (var b in new[] { letterboxTop, letterboxBottom })
            if (b != null) b.sizeDelta = new Vector2(b.sizeDelta.x, letterboxHeight * k);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
