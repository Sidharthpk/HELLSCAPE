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
    public float eyeHeightOnGround = 0.25f;

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
        var font = Resources.Load<TMPro.TMP_FontAsset>("SourceSans3-Semibold SDF");
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

    // the legs go: a lurch forward, the world tips sideways, the ground comes up; the last heartbeat
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
        Vector3 endPos = new Vector3(startPos.x, groundY + eyeHeightOnGround, startPos.z) + fwd * 0.5f;
        // lying on your side, looking along the ground
        Quaternion endRot = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(8f, 0f, 78f);

        // (the camera stays on your body: anything still shaking your head writes a pose that means something. Pinned
        // where the fall puts it, every frame after the shakes have had their say, until you're stood back up)
        pinned = true;
        bool hit = false;
        for (float t = 0f; t < collapseTime; t += Time.unscaledDeltaTime)
        {
            float k = t / collapseTime;
            float drop = k * k * k;                                            // knees first, then the fall
            float sway = Mathf.Sin(k * Mathf.PI) * 0.2f;
            pinPos = Vector3.Lerp(startPos, endPos, drop) + Vector3.Cross(Vector3.up, fwd) * sway;
            pinRot = Quaternion.Slerp(startRot, endRot, Mathf.SmoothStep(0f, 1f, k));
            camT.SetPositionAndRotation(pinPos, pinRot);
            if (!hit && drop > 0.97f)
            {
                hit = true;
                var thud = Sounds.Clip("Silo Fight/Push and fall thud", null);
                if (thud != null) AudioSource.PlayClipAtPoint(thud, endPos, 1f);
            }
            yield return null;
        }
        pinPos = endPos; pinRot = endRot;
        camT.SetPositionAndRotation(endPos, endRot);
        // a moment on the ground, the world going dark
        yield return new WaitForSecondsRealtime(0.8f);
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
