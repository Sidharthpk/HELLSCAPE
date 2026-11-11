using UnityEngine;
using UnityEngine.Events;

// Generic trigger volume for story beats: wire anything to onEnter in the inspector
// (start the zombie spawner, play a sound, start the ending, ...).
public class StoryTrigger : MonoBehaviour
{
    public bool triggerOnce = true;
    public bool playerOnFoot = true;  // fire when the player walks in
    public bool playerInCar = true;   // fire when the car drives in
    public UnityEvent onEnter = new UnityEvent();

    private bool fired = false;

    public void Rearm() => fired = false;   // e.g. a checkpoint respawn replays the beat

    void OnTriggerEnter(Collider other)
    {
        if (fired && triggerOnce) return;

        bool onFoot = playerOnFoot && other.CompareTag("Player");
        bool inCar = playerInCar && other.attachedRigidbody != null
                     && other.attachedRigidbody.GetComponent<CarHealth>() != null;
        if (!onFoot && !inCar) return;

        fired = true;
        onEnter.Invoke();
    }
}
