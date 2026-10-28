using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverScreen : MonoBehaviour
{
    public CanvasGroup panel;            // full-screen panel with "YOU DIED" text + buttons, starts hidden
    public MonoBehaviour playerMovementScript;
    public MonoBehaviour punchController;
    public float fadeDuration = 1.5f;

    [Header("Dying: you drop before the screen comes up")]
    public Camera playerCam;             // PlayerCamera: it buckles and hits the ground
    public float collapseTime = 1.6f;    // (real seconds)
    public float kneelHeight = 0.85f;    // eye above the ground on your knees

    private bool shown = false;
    public static bool Open { get; private set; }   // dying or dead: no pausing over it

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => Open = false;

    void OnDestroy() => Open = false;
    private Transform camT;
    private Vector3 camLocalPos;
    private Quaternion camLocalRot;
    private readonly List<GameObject> hidden = new List<GameObject>();

    void Start()
    {
        ApplyTypography();
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
    }

    public void ApplyTypography()
    {
        if (panel == null) return;
        var font = GameFont.Main;
        if (font == null) return;
        foreach (var text in panel.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            // Preserve every layer of the existing YOU DIED title treatment.
            bool title = text.text.Replace(" ", "").Replace("\n", "").ToUpperInvariant().Contains("YOUDIED");
            for (var parent = text.transform; parent != null && parent != panel.transform; parent = parent.parent)
                title |= parent.name == "YouDied";
            if (title) continue;
            text.font = font;
            text.fontSharedMaterial = font.material;
        }
    }

    public void Show()
    {
        if (shown) return;
        shown = true;
        Open = true;

        if (playerMovementScript != null) playerMovementScript.enabled = false;
        if (punchController != null) punchController.enabled = false;

        StartCoroutine(DieThenShow());
    }

    IEnumerator DieThenShow()
    {
        if (playerCam == null) { var c = GameObject.Find("PlayerCamera"); if (c != null) playerCam = c.GetComponent<Camera>(); }
        if (playerCam != null && playerCam.isActiveAndEnabled) yield return Collapse();
        else yield return new WaitForSecondsRealtime(0.6f);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        yield return FadeIn();
    }

    // the legs go: down onto your knees, then the head falls back and you're looking at the sky; the last heartbeat
    IEnumerator Collapse()
    {
        camT = playerCam.transform;
        camLocalPos = camT.localPosition;
        camLocalRot = camT.localRotation;
        hidden.Clear();
        foreach (Transform child in camT)   // the arms and the gun go limp: out of shot
            if (child.gameObject.activeSelf && child.GetComponentInChildren<Renderer>(true) != null) { child.gameObject.SetActive(false); hidden.Add(child.gameObject); }

        CutsceneHUD.Hide(true);
        Time.timeScale = 0.4f;
        var beat = Sounds.Clip("Prologue/Heartbeat", null);
        if (beat != null) AudioSource.PlayClipAtPoint(beat, camT.position, 0.9f);

        Vector3 startPos = camT.position;
        Quaternion startRot = camT.rotation;
        float groundY = startPos.y - 1.6f;
        foreach (var h in Physics.RaycastAll(startPos, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(camT.root)) { groundY = Mathf.Max(groundY, h.point.y); }
        Vector3 fwd = camT.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        Quaternion level = Quaternion.LookRotation(fwd, Vector3.up);
        Vector3 kneelPos = new Vector3(startPos.x, Mathf.Min(startPos.y, groundY + kneelHeight), startPos.z);
        Quaternion kneelRot = level * Quaternion.Euler(18f, 0f, 3f);               // head dropping as the knees hit
        Vector3 skyPos = kneelPos - fwd * 0.15f;                                    // sagging back on your heels
        Quaternion skyRot = level * Quaternion.Euler(-72f, 0f, 5f);                // ...and up at the sky

        // (the camera stays on your body: anything still shaking your head writes a pose that means something. Pinned
        // where the fall puts it, every frame after the shakes have had their say, until you're stood back up)
        pinned = true;
        float down = collapseTime * 0.45f;
        for (float t = 0f; t < down; t += Time.unscaledDeltaTime)
        {
            float k = t / down;
            pinPos = Vector3.Lerp(startPos, kneelPos, k * k);                      // the legs give, faster as you go
            pinRot = Quaternion.Slerp(startRot, kneelRot, Mathf.SmoothStep(0f, 1f, k));
            camT.SetPositionAndRotation(pinPos, pinRot);
            yield return null;
        }
        var thud = Sounds.Clip("Silo Fight/Push and fall thud", null);
        if (thud != null) AudioSource.PlayClipAtPoint(thud, kneelPos, 0.8f);
        for (float t = 0f; t < collapseTime; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / collapseTime);
            pinPos = Vector3.Lerp(kneelPos, skyPos, k);
            pinRot = Quaternion.Slerp(kneelRot, skyRot, k);
            camT.SetPositionAndRotation(pinPos, pinRot);
            yield return null;
        }
        pinPos = skyPos; pinRot = skyRot;
        camT.SetPositionAndRotation(skyPos, skyRot);
        // a moment looking up, the world going dark
        yield return new WaitForSecondsRealtime(1f);
    }

    IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            panel.alpha = t / fadeDuration;
            yield return null;
        }
        panel.alpha = 1f;
        panel.interactable = true;
        panel.blocksRaycasts = true;

        Time.timeScale = 0f; // freeze zombies once the screen is up
    }

    // put your head back on your shoulders (the camera was dropped to the ground)
    void StandBackUp()
    {
        pinned = false;
        rehomeUntil = Time.unscaledTime + 1.5f;
        if (camT == null) return;
        if (originalParent != null) camT.SetParent(originalParent, false);
        camT.localPosition = camHome;   // (not where it was when you died: that may have been mid-shake, or mid-shove)
        camT.localRotation = camLocalRot;
        foreach (var g in hidden) if (g != null) g.SetActive(true);
        hidden.Clear();
        camT = null;
        CutsceneHUD.Hide(false);
    }

    private Transform originalParent;
    private Vector3 camHome, pinPos;
    private Quaternion pinRot;
    private bool pinned;
    private float rehomeUntil;
    void Awake()
    {
        if (playerCam == null) { var c = GameObject.Find("PlayerCamera"); if (c != null) playerCam = c.GetComponent<Camera>(); }
        if (playerCam != null) { originalParent = playerCam.transform.parent; camHome = playerCam.transform.localPosition; }
    }

    void LateUpdate()
    {
        if (pinned && camT != null) camT.SetPositionAndRotation(pinPos, pinRot);
        // a head shake that began while you lay dead (a blow on a door, say) took the camera on the floor for its rest
        // pose, and puts it back there as it ends: for a moment after you're up, your eyes stay where your eyes are
        else if (Time.unscaledTime < rehomeUntil && playerCam != null && playerCam.transform.parent == originalParent) playerCam.transform.localPosition = camHome;
    }

    // hook these to the buttons' OnClick
    // back to the last checkpoint; from the very start only if none was reached yet
    public void Restart()
    {
        Time.timeScale = 1f;
        Open = false;
        if (Checkpoint.Current != null)
        {
            StopAllCoroutines();
            StandBackUp();
            shown = false;
            panel.alpha = 0f;
            panel.interactable = false;
            panel.blocksRaycasts = false;
            Checkpoint.RespawnCurrent();
            return;
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
