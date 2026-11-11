using System.Collections;
using TMPro;
using UnityEngine;

// Six seconds of exploration per Echo visit. Conversations hold the remaining time through both speakers, choices and the final reply.
public class EchoVisit : MonoBehaviour
{
    public const float Duration = 6f;
    public float Remaining { get; private set; }
    public bool Returning { get; private set; }
    public bool HeldForVane => vane != null && vane.IsTalking || encounter != null && (encounter.PreparingAmbush || encounter.Ambushing);
    public bool HeldForConversation => GhostNPC.Conversing
        || (DialogueBox.Instance != null && DialogueBox.Instance.Busy)
        || (ChoiceDialogue.Instance != null && ChoiceDialogue.Instance.Open);
    RevisedPlotDirector plot;
    OfficeHorrorDirector horror;
    OfficeRealmPuzzles puzzles;
    Transform player;
    GhostNPC vane;
    GhostNPC[] witnesses;
    StabCutscene[] films;
    KesslerConfrontation encounter;
    Vector3 lastSafe;
    TextMeshProUGUI counter;

    public void Initialize(RevisedPlotDirector director, Transform body, GhostNPC[] ghosts, KesslerConfrontation confrontation)
    {
        plot = director; player = body; witnesses = ghosts; encounter = confrontation;
        foreach (var g in ghosts) if (g.ghostName == "Vane") vane = g;
        horror = GetComponent<OfficeHorrorDirector>(); puzzles = GetComponent<OfficeRealmPuzzles>();
        films = RevisedPlot.All<StabCutscene>(gameObject.scene);
        var canvas = new GameObject("Echo time", typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(transform, false); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 380;
        var scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        counter = new GameObject("Echo countdown").AddComponent<TextMeshProUGUI>(); counter.transform.SetParent(canvas.transform, false);
        counter.fontSize = 23f; counter.alignment = TextAlignmentOptions.TopRight; counter.raycastTarget = false;
        var rect = counter.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f); rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-48f, -50f); rect.sizeDelta = new Vector2(320f, 40f);
        // no bar: the time left shows in the blue edge glow, which breathes faster as it runs out (EchoOfficeMemory)
        counter.gameObject.SetActive(false);
    }
    public void RealmChanged(bool echo)
    {
        Remaining = echo ? Duration : 0f;
        if (echo && player != null) lastSafe = player.position;
    }
    public void AdvanceClock(float delta)
    {
        if (plot == null || !plot.Echo || Returning || HeldForVane || PauseMenu.Paused || HeldForConversation || (horror != null && horror.Switching)) return;
        Remaining = Mathf.Max(0f, Remaining - Mathf.Max(0f, delta));
    }
    void Update()
    {
        if (plot == null || player == null) return;
        counter.gameObject.SetActive(plot.Echo && !PauseMenu.Paused && !GameOverScreen.Open && !TitleScreen.Showing);
        counter.text = HeldForVane ? "ECHO  /  VANE" : "ECHO";
        bool low = Remaining <= 2f && !HeldForVane && !HeldForConversation;
        counter.color = low ? new Color(1f, .55f, .4f) : new Color(.68f, .88f, 1f);
        if (!plot.Echo || TitleScreen.Showing || PauseMenu.Paused || GameOverScreen.Open || EndingSequence.Playing) return;
        if (puzzles == null || puzzles.CanSwitch(player, false)) lastSafe = player.position;
        AdvanceClock(Time.deltaTime);
        if (Remaining > 0f || Returning || HeldForVane || HeldForConversation || horror.Switching || plot.InSequence) return;
        // An already-running camera cut cannot be interrupted halfway through its cleanup.
        foreach (var film in films) if (film != null && film.Playing) return;
        Returning = true;
        foreach (var ghost in witnesses)
            if (ghost != null && ghost != vane && ghost.IsTalking) { DialogueBox.Instance?.Clear(); ghost.ExpireEcho(); }
        ChoiceDialogue.Instance?.Close();
        if (NoteViewer.IsOpen) NoteViewer.Close();
        StartCoroutine(Return());
    }
    IEnumerator Return()
    {
        while (NoteViewer.Blocking || HeldForConversation || HeldForVane) yield return null;
        horror.RequestSwitch(false, () =>
        {
            // At expiry, finish on the last clear side of a door, hidden behind the eyelids.
            if (puzzles != null && !puzzles.CanSwitch(player, false))
            {
                player.position = lastSafe;
                if (player.TryGetComponent<Rigidbody>(out var rb)) { rb.position = lastSafe; if (!rb.isKinematic) rb.linearVelocity = Vector3.zero; }
                Physics.SyncTransforms();
            }
            plot.SetEcho(false);
        }, true);
        while (horror.Switching) yield return null;
        Returning = false;
    }
    public void Cancel()
    {
        StopAllCoroutines(); Returning = false;
        if (counter != null) counter.gameObject.SetActive(false);
    }
    void OnDisable() => Cancel();
}
