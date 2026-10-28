using UnityEngine;

public class PlayerFootStep : MonoBehaviour
{
    Rigidbody rb;
    AudioSource audioSource;
    public AudioClip[] footSteps;
    public AudioClip[] metalSteps;          // on the silo's metal stairs and walkways (Sound Manager: General/Player footsteps (metal))
    static readonly string[] metalAreas = { "Silo Complex", "Silo Chase", "Silo Fight" };
    public float stepInterval = 0.45f;
    public float minSpeedToStep = 0.5f;

    public float crouchVolume = 0.3f, crouchIntervalScale = 1.4f, crouchCutoff = 900f;

    private float stepTimer;
    FirstPersonController fpc;
    AudioSource muffled;

    // a second source with a low-pass on it, so the player's main AudioSource keeps its other sounds clear
    AudioSource Muffled()
    {
        if (muffled != null) return muffled;
        var go = new GameObject("Footsteps (muffled)");
        go.transform.SetParent(transform, false);
        muffled = go.AddComponent<AudioSource>();
        muffled.playOnAwake = false; muffled.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        muffled.spatialBlend = audioSource.spatialBlend; muffled.volume = audioSource.volume;
        go.AddComponent<AudioLowPassFilter>().cutoffFrequency = crouchCutoff;
        return muffled;
    }

    void Start()
    {
        fpc = GetComponentInParent<FirstPersonController>();
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        footSteps = Sounds.Clips("General/Player footsteps", footSteps);   // HellScape Sound Manager
        metalSteps = Sounds.Clips("General/Player footsteps (metal)", metalSteps);
    }

    static bool OnMetal() => System.Array.IndexOf(metalAreas, MusicManager.CurrentArea) >= 0;

    void Update()
    {
        bool hasInput = Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f;
        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        bool isMoving = horizontalVel.magnitude > minSpeedToStep;

        if (hasInput && isMoving)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                stepTimer = stepInterval;
                var set = OnMetal() && metalSteps != null && metalSteps.Length > 0 ? metalSteps : footSteps;
                if (set == null || set.Length == 0) return;
                AudioClip clip = set[Random.Range(0, set.Length)];
                float volume = Sounds.Volume(set == metalSteps ? "General/Player footsteps (metal)" : "General/Player footsteps");
                bool quiet = fpc != null && fpc.IsCrouched;   // crouched: soft, muffled steps (Vane hunts by sound)
                if (quiet) stepTimer = stepInterval * crouchIntervalScale;
                var src = quiet ? Muffled() : audioSource;
                src.pitch = Random.Range(0.94f, 1.06f);   // no two steps quite the same
                src.PlayOneShot(clip, quiet ? volume * crouchVolume : volume);
            }
        }
        else
        {
            stepTimer = 0f;
        }
    }
}