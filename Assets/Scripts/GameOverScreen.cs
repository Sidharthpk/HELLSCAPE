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
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
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

        camT.SetParent(null, true);
        bool hit = false;
        for (float t = 0f; t < collapseTime; t += Time.unscaledDeltaTime)
        {
            float k = t / collapseTime;
            float drop = k * k * k;                                            // knees first, then the fall
            float sway = Mathf.Sin(k * Mathf.PI) * 0.2f;
            camT.position = Vector3.Lerp(startPos, endPos, drop) + Vector3.Cross(Vector3.up, fwd) * sway;
            camT.rotation = Quaternion.Slerp(startRot, endRot, Mathf.SmoothStep(0f, 1f, k));
            if (!hit && drop > 0.97f)
            {
                hit = true;
                var thud = Sounds.Clip("Silo Fight/Push and fall thud", null);
                if (thud != null) AudioSource.PlayClipAtPoint(thud, endPos, 1f);
            }
            yield return null;
        }
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
        if (camT == null) return;
        if (originalParent != null) camT.SetParent(originalParent, false);
        camT.localPosition = camLocalPos;
        camT.localRotation = camLocalRot;
        foreach (var g in hidden) if (g != null) g.SetActive(true);
        hidden.Clear();
        camT = null;
        CutsceneHUD.Hide(false);
    }

    private Transform originalParent;
    void Awake()
    {
        if (playerCam == null) { var c = GameObject.Find("PlayerCamera"); if (c != null) playerCam = c.GetComponent<Camera>(); }
        if (playerCam != null) originalParent = playerCam.transform.parent;
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
