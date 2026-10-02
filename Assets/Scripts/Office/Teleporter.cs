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

    private bool busy;

    public void Go()
    {
        if (!busy) StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        busy = true;
        if (doorSound != null) doorSound.Play();
        if (transition != null && !transition.Busy) yield return transition.Play();
        else if (fader != null) yield return fader.FadeTo(1f, fadeTime);

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
