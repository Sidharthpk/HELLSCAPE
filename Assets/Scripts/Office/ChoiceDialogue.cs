using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// A conversation: the speaker's line (their name on a plate in their colour, the words in white) and up to 4
// answers of yours (amber, like everything you say), picked with the number keys. While it's open you can't walk
// or look around, the picture is letterboxed (DialogueBox does the bars) and the camera turns to whoever you're
// talking to. GhostNPC drives it.
public class ChoiceDialogue : MonoBehaviour
{
    public static ChoiceDialogue Instance;

    public GameObject panel;
    public TextMeshProUGUI text;
    public Behaviour[] lockDuring;          // FirstPersonController, PunchController, GunController
    public AudioSource blip;
    public Color speakerColor = new Color(0.6f, 0.9f, 1f);   // (a speaker the dialogue box has no colour for)
    public Color answerColor = new Color(1f, 0.80f, 0.36f);  // your answers: the same amber as your lines
    public float turnTime = 0.45f;          // the camera coming round to face them

    public bool Open { get; private set; }

    private Action<int> onPick;
    public int Revision { get; private set; }
    public float SecondsLeft { get; private set; }
    Action onTimeout;
    public void SetDeadline(float seconds, Action expired) { SecondsLeft = Mathf.Max(0f, seconds); onTimeout = expired; }

    private int count;
    private float openedAt;
    private Coroutine turning;
    private DialoguePresentation presentation;
    private readonly List<Behaviour> stopped = new List<Behaviour>();
    private CursorLockMode previousLock;
    private bool previousCursor;

    void Awake()
    {
        Instance = this;
        presentation = DialoguePresentation.Choices(this);
        panel.SetActive(false);
    }

    public void Show(string speaker, string line, string[] options, Action<int> pick, Transform lookAt = null)
    {
        Revision++;
        onTimeout = null; SecondsLeft = 0f;
        onPick = pick;
        if (DialogueBox.Instance != null) DialogueBox.Instance.Raise();   // the cutscene letterbox must not cover the answers
        count = Mathf.Min(options.Length, 4);

        Color plate = DialogueBox.Instance != null ? DialogueBox.Instance.Plate(speaker) : speakerColor;
        if (!Open)
        {
            previousLock = Cursor.lockState;
            previousCursor = Cursor.visible;
            stopped.Clear();
            if (lockDuring != null)
                foreach (var b in lockDuring)
                    if (b != null && b.enabled) { stopped.Add(b); b.enabled = false; }
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (EventSystem.current == null)
            new GameObject("Dialogue EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        panel.SetActive(true);
        Open = true;
        openedAt = Time.unscaledTime;
        presentation.ShowChoices(speaker, line, options, plate, Select);
        if (blip != null) blip.Play();
        if (lookAt != null)
        {
            if (turning != null) StopCoroutine(turning);
            turning = StartCoroutine(TurnTo(lookAt));
        }
    }

    // you turn to face them (your controller is switched off while the panel is up, so nothing fights this)
    IEnumerator TurnTo(Transform who)
    {
        FirstPersonController fpc = null;
        foreach (var b in lockDuring ?? new Behaviour[0]) if (b is FirstPersonController f) fpc = f;
        if (fpc == null) yield break;
        var cam = fpc.GetComponentInChildren<Camera>();
        if (cam == null) yield break;
        Vector3 face = who.position + Vector3.up * 1.55f;
        Quaternion y0 = fpc.transform.rotation, p0 = cam.transform.localRotation;
        for (float t = 0f; t < turnTime && Open; t += Time.unscaledDeltaTime)
        {
            Vector3 d = face - cam.transform.position;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            if (flat.sqrMagnitude < 0.01f) break;
            float k = Mathf.SmoothStep(0f, 1f, t / turnTime);
            fpc.transform.rotation = Quaternion.Slerp(y0, Quaternion.LookRotation(flat), k);
            cam.transform.localRotation = Quaternion.Slerp(p0, Quaternion.Euler(-Mathf.Atan2(d.y, flat.magnitude) * Mathf.Rad2Deg, 0f, 0f), k);
            yield return null;
        }
        fpc.SetPitch(cam.transform.localEulerAngles.x);
        turning = null;
    }

    void Update()
    {
        if (!Open || PauseMenu.Paused || Time.unscaledTime - openedAt < 0.25f) return;   // don't eat the key that opened it
        if (onTimeout != null)
        {
            SecondsLeft = Mathf.Max(0f, SecondsLeft - Time.deltaTime);
            presentation.SetCountdown(SecondsLeft);
            if (SecondsLeft <= 0f) { var expired = onTimeout; Close(); expired(); return; }
        }
        presentation.RestoreFocus();
        for (int i = 0; i < count; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                Select(i);
                return;
            }
    }

    void Select(int index)
    {
        if (!Open || PauseMenu.Paused || Time.unscaledTime - openedAt < 0.25f || index < 0 || index >= count) return;
        var pick = onPick;
        onPick = null;
        Close();
        pick?.Invoke(index);
    }

    public void Close()
    {
        if (!Open) return;
        Revision++; onTimeout = null; SecondsLeft = 0f;
        onPick = null;
        if (turning != null) { StopCoroutine(turning); turning = null; }
        panel.SetActive(false);
        Open = false;
        foreach (var b in stopped) if (b != null) b.enabled = true;
        stopped.Clear();
        Cursor.lockState = previousLock;
        Cursor.visible = previousCursor;
    }
}
