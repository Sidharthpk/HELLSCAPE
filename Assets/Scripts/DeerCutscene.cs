using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// The first thing you see in hell (no animal dies in this city): the street full of the Lost and the Damned, all
// walking, none of them going anywhere. Walk on and the camera leaves you: it turns slowly from one of them to the
// next while you ask where you are. Back to you, and the street wakes up (onDone: the zombie spawner).
// Played by the Deer Zombie Trigger (the name is left over from when a zombie sat eating a deer here).
public class DeerCutscene : MonoBehaviour
{
    [Header("The Lost and the Damned")]
    public float lookRange = 70f;            // the ones it looks at: the nearest within this

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

    [TextArea] public string feedLine = "Where am I?";
    [TextArea] public string afterLine = "None of them look at me.";

    public UnityEvent onDone;

    bool played;

    public void Play()
    {
        if (played) return;
        played = true;
        // driving past it: no cutscene, the street just wakes up
        if (playerCam == null || !playerCam.isActiveAndEnabled) { onDone?.Invoke(); return; }
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

        // who it looks at: the nearest few of the Lost and the Damned out in the street
        Vector3 eye = cam.position;
        var seen = new System.Collections.Generic.List<Transform>();
        foreach (var w in CityWalkers.Walkers)
            if (w != null && Vector3.Distance(w.position, eye) < lookRange) seen.Add(w);
        seen.Sort((x, y) => (x.position - eye).sqrMagnitude.CompareTo((y.position - eye).sqrMagnitude));
        if (seen.Count > 4) seen.RemoveRange(4, seen.Count - 4);

        var moans = Sounds.Clip("Hell City/Zombie moans", null);
        var src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0f; src.loop = true;
        if (moans != null) { src.clip = moans; src.pitch = 0.7f; src.volume = 0.35f; src.Play(); }

        playerCam.fieldOfView = 55f;
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.4f));
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(feedLine);

        // a slow turn from where you were looking, to each of them in turn, a long look at each
        Quaternion look = cam.rotation;
        for (int i = 0; i < Mathf.Max(1, seen.Count); i++)
        {
            Quaternion to;
            if (seen.Count > 0 && seen[i] != null) to = Quaternion.LookRotation(Flat(seen[i].position - eye) + Vector3.up * 0.15f);
            else to = Quaternion.AngleAxis(60f * (i + 1), Vector3.up) * look;
            Transform follow = seen.Count > 0 ? seen[i] : null;
            float turn = 1.6f, hold = 2.0f;
            Quaternion from = look;
            for (float t = 0f; t < turn + hold; t += Time.deltaTime)
            {
                if (follow != null) to = Quaternion.LookRotation(Flat(follow.position - eye) + Vector3.up * 0.15f);
                cam.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / turn));
                playerCam.fieldOfView = Mathf.Lerp(55f, 42f, Mathf.SmoothStep(0f, 1f, t / (turn + hold)));
                yield return null;
            }
            look = cam.rotation;
            playerCam.fieldOfView = 55f;
        }
        src.Stop();

        // ---- back to you
        if (fader != null) yield return fader.FadeTo(1f, 0.2f);
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

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    void SetBars(float k)
    {
        foreach (var b in new[] { letterboxTop, letterboxBottom })
            if (b != null) b.sizeDelta = new Vector2(b.sizeDelta.x, letterboxHeight * k);
    }
}

