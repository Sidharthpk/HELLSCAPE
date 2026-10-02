using UnityEngine;

// The Kessler & Vane van in the prologue: the morning's last two deliveries. Getting in is an Interactable on the
// van ("Get in the van", F like everything else); F again gets you out once it has stopped. While you drive, the
// first-person player is switched off and the van's chase camera takes over.
public class PrologueVan : MonoBehaviour
{
    public PrometeoCarController controller;
    public Rigidbody body;
    public Camera vanCamera;                // CameraFollow on it
    public GameObject player;               // the first-person player root
    public Camera playerCamera;
    public Transform exitPoint;             // driver's side
    public Interactable getIn;              // on the van
    public AudioSource engine;
    public KeyCode key = KeyCode.F;
    public bool locked;                     // parked at the depot for good

    public bool InVan { get; private set; }
    private float enteredAt;
    public float Speed => body != null ? body.linearVelocity.magnitude : 0f;

    void Start()
    {
        if (getIn != null) getIn.onUse.AddListener(Enter);
        // (the prologue may already have put you in the seat before this runs: keep that, don't switch it all off)
        SetDriving(InVan);
    }

    public void Enter()
    {
        if (InVan || locked) return;
        InVan = true;
        enteredAt = Time.time;   // the same F press that got you in mustn't get you straight out
        player.SetActive(false);
        SetDriving(true);
    }

    public void Exit()
    {
        if (!InVan) return;
        InVan = false;
        controller.ThrottleOff();
        controller.Brakes();
        SetDriving(false);
        player.transform.position = exitPoint.position;
        Vector3 f = transform.forward; f.y = 0f;
        player.transform.rotation = Quaternion.LookRotation(f.sqrMagnitude > 0.01f ? f : Vector3.forward);
        player.SetActive(true);
    }

    void SetDriving(bool on)
    {
        controller.enabled = on;
        if (vanCamera != null)
        {
            vanCamera.enabled = on;
            var l = vanCamera.GetComponent<AudioListener>(); if (l != null) l.enabled = on;
        }
        if (playerCamera != null)
        {
            var l = playerCamera.GetComponent<AudioListener>(); if (l != null) l.enabled = !on;
        }
        if (engine != null) { if (on) engine.Play(); else engine.Stop(); }
        if (getIn != null) getIn.enabled = !on && !locked;
        if (!on && body != null) { body.linearVelocity = Vector3.Project(body.linearVelocity, Vector3.up); }
    }

    // parked for good at the depot: out, and no getting back in
    public void Lock()
    {
        locked = true;
        if (InVan) Exit();
        if (getIn != null) getIn.enabled = false;
    }

    void Update()
    {
        if (InVan && Time.time - enteredAt > 0.4f && Input.GetKeyDown(key) && Speed < 2.5f) Exit();
    }
}
