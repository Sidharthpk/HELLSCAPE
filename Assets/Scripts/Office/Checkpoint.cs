using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

// A place to come back to when you die. Activate() makes it the current one (story beats call it:
// waking in hell, entering the office, arriving at the silo, landing on the silo floor, leaving with the keys).
// GameOverScreen.Restart() -> Checkpoint.RespawnCurrent(): the player is revived here and onRespawn resets
// whatever killed them (the chase, the boss, the blood wave). With no checkpoint yet the scene reloads.
public class Checkpoint : MonoBehaviour
{
    public static Checkpoint Current { get; private set; }

    public string title = "Checkpoint";
    public Transform spawn;                 // player root pose on respawn
    public float health = 100f;             // what you come back with
    public float clearZombiesRadius = 25f;  // nobody waiting on the spot you come back to
    public Transform[] restorePoses;        // e.g. the car: put back where it stood when this checkpoint was reached
    public UnityEvent onRespawn = new UnityEvent();

    [Header("Player (found if empty)")]
    public Transform player;
    public ScreenFader fader;
    public TextMeshProUGUI notice;          // "CHECKPOINT" flash in a corner (optional)

    private Vector3[] savedPos;
    private Quaternion[] savedRot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => Current = null;

    void OnDestroy() { if (Current == this) Current = null; }   // scene reload

    public void Activate()
    {
        if (Current == this) return;
        Current = this;
        savedPos = new Vector3[restorePoses.Length];
        savedRot = new Quaternion[restorePoses.Length];
        for (int i = 0; i < restorePoses.Length; i++)
            if (restorePoses[i] != null) { savedPos[i] = restorePoses[i].position; savedRot[i] = restorePoses[i].rotation; }
        if (notice != null) StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        notice.text = title.ToUpper();
        notice.gameObject.SetActive(true);
        for (float t = 0f; t < 2.5f; t += Time.unscaledDeltaTime)
        {
            notice.alpha = Mathf.Min(t / 0.3f, (2.5f - t) / 0.6f, 1f) * 0.8f;
            yield return null;
        }
        notice.gameObject.SetActive(false);
    }

    public static bool RespawnCurrent()
    {
        if (Current == null) return false;
        Current.StartCoroutine(Current.Respawn());
        return true;
    }

    IEnumerator Respawn()
    {
        if (player == null) { var p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.transform; }
        if (fader == null) fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
        if (fader != null) fader.fade.alpha = 1f;

        // out of the car if the wave got you in it
        var car = FindFirstObjectByType<CarInteract>(FindObjectsInactive.Include);
        if (car != null) car.ForceExit(false);   // also unlocks it if it was wrecked
        player.gameObject.SetActive(true);

        for (int i = 0; i < restorePoses.Length; i++)
        {
            var t = restorePoses[i];
            if (t == null) continue;
            var rb = t.GetComponent<Rigidbody>();
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            t.SetPositionAndRotation(savedPos[i], savedRot[i]);
        }

        var body = player.GetComponent<Rigidbody>();
        if (body != null) { body.isKinematic = false; body.linearVelocity = Vector3.zero; }
        player.SetPositionAndRotation(spawn.position, spawn.rotation);
        Physics.SyncTransforms();

        foreach (var z in FindObjectsByType<ZombieAI>(FindObjectsSortMode.None))
            if (Vector3.Distance(z.transform.position, spawn.position) < clearZombiesRadius) Destroy(z.gameObject);

        var ph = player.GetComponent<PlayerHealth>();
        if (ph != null) ph.Revive(Mathf.Min(health, ph.maxHealth));

        var move = player.GetComponent<FirstPersonController>();
        if (move != null) move.enabled = true;
        var punch = player.GetComponentInChildren<PunchController>(true);
        if (punch != null) punch.enabled = !GunController.Equipped;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        onRespawn.Invoke();
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow("...not yet.");
        if (fader != null) yield return fader.FadeTo(0f, 1.2f);
    }
}
