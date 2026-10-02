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

    private float stepTimer;

    void Start()
    {
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
                audioSource.pitch = Random.Range(0.94f, 1.06f);   // no two steps quite the same
                audioSource.PlayOneShot(clip, Sounds.Volume(set == metalSteps ? "General/Player footsteps (metal)" : "General/Player footsteps"));
            }
        }
        else
        {
            stepTimer = 0f;
        }
    }
}