using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The CCTV desk in the server room (the user's control-room set): every monitor shows one of the office cameras,
// live. Swipe Vane's pass at the desk and the screens roll back to the night of 13/04 and the tape plays: Derek and
// Tomas (SequencePuzzle.PlayTape -> its onSolved, the "Derek kills Tomas" cutscene).
public class CctvDesk : MonoBehaviour
{
    public SequencePuzzle tape;
    public StabCutscene fireTape;          // V5.1: the office fire recording (FireTape), played before the Tomas tape
    public Renderer[] screens;              // quads over the monitors
    public Camera[] feeds;                  // one per screen, switched off: rendered here, a few at a time
    public float frameTime = 0.5f;          // seconds between two frames of the same feed
    public float watchRange = 14f;          // nobody renders the feeds from across the office
    public OfficeV52Sequence officeSequence;
    public bool Rolling => rolling;
    [Header("Recovered recordings, in playback order")]
    public StabCutscene[] recoveredRecordings = new StabCutscene[0];
    public int CurrentRecording { get; private set; } = -1;
    public int RecordingCount => recoveredRecordings != null && recoveredRecordings.Length > 0 ? recoveredRecordings.Length : fireTape != null ? 1 : 0;
    readonly List<Behaviour> playbackControls = new List<Behaviour>();
    StabCutscene[] playingRecordings;
    bool hidPlaybackHud;

    RenderTexture[] frames;
    Material[] looks;
    int next;
    float timer;
    bool rolling;

    void Start()
    {
        frames = new RenderTexture[feeds.Length];
        for (int i = 0; i < feeds.Length; i++)
        {
            feeds[i].enabled = false;
            frames[i] = new RenderTexture(192, 144, 16) { name = "CCTV " + feeds[i].name };
            feeds[i].targetTexture = frames[i];
        }
        looks = new Material[screens.Length];
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        for (int i = 0; i < screens.Length; i++)
        {
            looks[i] = new Material(unlit) { mainTexture = frames.Length > 0 ? frames[i % frames.Length] : null, color = new Color(.75f, .85f, .78f) };   // a green-grey tube
            screens[i].sharedMaterial = looks[i];
        }
    }

    void Update()
    {
        var cam = Camera.main;
        if (rolling || frames.Length == 0 || cam == null || (cam.transform.position - transform.position).sqrMagnitude > watchRange * watchRange) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = frameTime / frames.Length;
        feeds[next].Render();
        next = (next + 1) % feeds.Length;
    }

    // the desk's Interactable (it has already checked for the pass)
    public void Watch()
    {
        if (tape == null || rolling) return;
        if (officeSequence != null && Truth.Has("OfficeV52.CCTV")) return;
        if (officeSequence != null && officeSequence.Opening != null && !officeSequence.Opening.Powered)
        { DialogueBox.Instance?.Say("The recorder has no power. I need to replace the missing fuses."); return; }
        if (officeSequence != null && !Inventory.Has("SecurityPass"))
        { DialogueBox.Instance?.Say(tape.noPowerLine); return; }
        StartCoroutine(Rewind());
    }

    IEnumerator Rewind()
    {
        rolling = true;
        tape.SetPowered(true);
        // one screen after another drops to snow, then the tape
        foreach (var m in looks) { m.mainTexture = Snow(); yield return new WaitForSeconds(.25f); }
        yield return new WaitForSeconds(1f);
        if (officeSequence != null)
        {
            var recordings = recoveredRecordings != null && recoveredRecordings.Length > 0
                ? recoveredRecordings : fireTape != null ? new[] { fireTape } : new StabCutscene[0];
            if (recordings.Length == 0 || System.Array.Exists(recordings, c => c == null || c.actors == null || c.cam == null))
            {
                Debug.LogError("[Office CCTV] A recovered recording is missing its cast or camera. Assign all recordings before completing this sequence.");
                RestoreLiveFeeds(); rolling = false;
                yield break;
            }
            playingRecordings = recordings;
            hidPlaybackHud = !CutsceneHUD.Hidden;
            if (hidPlaybackHud) CutsceneHUD.Hide(true);
            foreach (var film in recordings)
            {
                film.keepControlsLocked = true;
                foreach (var control in film.lockDuring ?? new Behaviour[0])
                    if (control != null && control.enabled && !playbackControls.Contains(control))
                    { playbackControls.Add(control); control.enabled = false; }
            }
            try
            {
                for (int i = 0; i < recordings.Length; i++)
                {
                    CurrentRecording = i;
                    recordings[i].holdBlackAfter = i < recordings.Length - 1;
                    yield return recordings[i].Run();
                    while (DialogueBox.Instance != null && DialogueBox.Instance.Busy) yield return null;
                    if (recordings[i].GetComponent<TomasTape>() != null)
                    { Truth.Set("SawTomasTape"); EvidenceJournal.Collect(EvidenceJournal.Tape); }
                    if (recordings[i].GetComponent<OfficeEntryTape>() != null)
                    {
                        Truth.Set("SawMercerEnterKesslerOffice");
                        EvidenceJournal.Record("Mercer enters Kessler's office", "After shooting Tomas in the silo and returning upstairs, Mercer entered Arthur Kessler's office.");
                    }
                }
                tape.CompleteRecording();
                officeSequence.RecordingFinished();
            }
            finally
            {
                ReleasePlaybackControls();
                RestoreLiveFeeds(); rolling = false; CurrentRecording = -1;
            }
            yield break;
        }
        // V5.1 A3C: the live recordings are gone; the Echo archive holds Mercer's fire order and Vane's execution
        var box = DialogueBox.Instance;
        if (box != null && !Truth.Has("SawFireTape"))
        {
            if (fireTape != null)
            {
                box.Say(HS5.S("A3C_cctv_on") + "|" + HS5.S("A3C_cctv_hint"));
                yield return null;
                while (box.Busy) yield return null;
                fireTape.Play();   // the recording: Mercer and Kessler, then Vane's execution (its own lines)
                yield return null;
                while (fireTape.Playing) yield return null;
                box.Say(HS5.S("A3C_vane_reacts") + "|" + HS5.S("A3C_armory_code"));
            }
            else box.Say(HS5.S("A3C_cctv_on") + "|" + HS5.S("A3C_cctv_hint") + "|" + HS5.S("A3C_fire_tape") + "|" + HS5.S("A3C_vane_death_tape") + "|" + HS5.S("A3C_vane_reacts") + "|" + HS5.S("A3C_armory_code"));
            yield return null;
            while (box.Busy) yield return null;
            Truth.Set("SawFireTape");
            EvidenceJournal.Record("Mercer's fire order", "Mercer disabled the cameras and secured the exits. Kessler asked about the staff and said: proceed. Then Mercer shot Vane. 13/04 is on the calendar.");
        }
        tape.PlayTape();
        // after the tape: back to the cameras
        for (int i = 0; i < looks.Length; i++) looks[i].mainTexture = frames.Length > 0 ? frames[i % frames.Length] : null;
        rolling = false;
    }

    void RestoreLiveFeeds()
    {
        if (looks == null) return;
        for (int i = 0; i < looks.Length; i++)
            if (looks[i] != null) looks[i].mainTexture = frames.Length > 0 ? frames[i % frames.Length] : null;
    }
    void ReleasePlaybackControls()
    {
        if (playingRecordings != null)
            foreach (var film in playingRecordings) if (film != null) { film.keepControlsLocked = false; film.holdBlackAfter = false; }
        playingRecordings = null;
        foreach (var control in playbackControls) if (control != null) control.enabled = true;
        playbackControls.Clear();
        if (hidPlaybackHud) { CutsceneHUD.Hide(false); hidPlaybackHud = false; }
    }
    void OnDisable() => ReleasePlaybackControls();

    static Texture2D snow;
    static Texture2D Snow()
    {
        if (snow != null) return snow;
        snow = new Texture2D(96, 72) { filterMode = FilterMode.Point };
        var px = new Color[96 * 72];
        for (int i = 0; i < px.Length; i++) { float v = Random.value; px[i] = new Color(v, v, v); }
        snow.SetPixels(px);
        snow.Apply();
        return snow;
    }

    void OnDestroy()
    {
        if (frames != null) foreach (var f in frames) if (f != null) f.Release();
        if (looks != null) foreach (var m in looks) Destroy(m);
    }
}
