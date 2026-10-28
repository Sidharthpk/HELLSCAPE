using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Doorway between the city and an interior (office, silo). Call Go() from an Interactable:
// fade to black, move the player (and any colleagues following), fade back in.
public class Teleporter : MonoBehaviour
{
    public Transform player;                 // FirstPersonController root
    public Transform destination;
    public ScreenFader fader;
    public float fadeTime = 0.8f;
    public AudioSource doorSound;
    public DoorTransition transition;        // the Resident Evil door; plain fade if empty
    [TextArea] public string arriveLine;
    public string musicArea;                 // Sound Library area whose music starts on arrival (empty = no change)
    public UnityEvent onArrive = new UnityEvent();

    [Header("Into another scene (the office + silo are their own scene)")]
    public string loadScene;                 // empty = same scene, move the player to destination
    public string arriveAt;                  // name of the object to stand at once that scene has loaded
    public string arriveCheckpoint;          // name of the Checkpoint to activate there (optional)

    public static string ArriveAt, ArriveCheckpoint, ArriveMusic;   // handed to SceneArrival across the load
    public static string ArriveLine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetArrival() => ArriveAt = ArriveCheckpoint = ArriveMusic = ArriveLine = null;

    private bool busy;

    public void Go()
    {
        if (name == "Front door (go home)")
        {
            var run = FindFirstObjectByType<DeliveryRun>(FindObjectsInactive.Include);
            if (run != null && run.Started && !run.Done)
            {
                DialogueBox.Instance?.Say("Gotta finish the last delivery, then I'm done for the day.");
                return;
            }
        }
        if (name == "ExitDoor")
        {
            var encounter = FindFirstObjectByType<KesslerConfrontation>(FindObjectsInactive.Include);
            if (encounter != null && encounter.Armed)
            {
                DialogueBox.Instance?.Say("Vane is between me and the street. I have to take the silo route.");
                return;
            }
        }
        if (loadScene == "OfficeSilo" && Inventory.Has("CarKeys"))
        {
            DialogueBox.Instance?.Say("I have the keys. I need to get to the van.");
            return;
        }
        if (!string.IsNullOrEmpty(loadScene) &&
            (!Application.CanStreamedLevelBeLoaded(loadScene) || string.IsNullOrEmpty(arriveAt)))
        {
            Debug.LogError("[Teleporter] Invalid scene or arrival on " + name + ": " + loadScene + " / " + arriveAt);
            return;
        }
        if (!busy) StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        busy = true;
        if (doorSound != null) doorSound.Play();
        if (transition != null && !transition.Busy) yield return transition.Play();
        else if (fader != null) yield return fader.FadeTo(1f, fadeTime);

        if (!string.IsNullOrEmpty(loadScene))
        {
            SceneProgress.Capture(gameObject.scene, player);
            ArriveAt = arriveAt; ArriveCheckpoint = arriveCheckpoint; ArriveMusic = musicArea; ArriveLine = arriveLine;
            SleepSequence.WakeInHell = true;   // no title screen on the way in
            UnityEngine.SceneManagement.SceneManager.LoadScene(loadScene);
            yield break;
        }

        var rb = player.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;
        player.SetPositionAndRotation(destination.position, destination.rotation);
        Physics.SyncTransforms();

        int i = 0;
        foreach (var s in Survivor.All)
            if (s != null && s.Following && s.gameObject.activeInHierarchy) s.WarpBehind(player, i++);

        MusicManager.Area(musicArea);
        onArrive.Invoke();
        yield return new WaitForSeconds(0.2f);
        if (fader != null) yield return fader.FadeTo(0f, fadeTime);
        if (!string.IsNullOrEmpty(arriveLine) && DialogueBox.Instance != null) DialogueBox.Instance.Say(arriveLine);
        busy = false;
    }
}
