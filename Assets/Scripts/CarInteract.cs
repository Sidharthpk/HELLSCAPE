using UnityEngine;

public class CarInteract : MonoBehaviour
{
    [Header("UI")]
    public GameObject promptUI;

    [Header("Player")]
    public GameObject playerRoot;
    public MonoBehaviour playerMovementScript;
    public MonoBehaviour punchController;
    public Camera playerCamera;

    [Header("Car")]
    public MonoBehaviour carController;
    public Camera carCamera;
    public Transform driverSeatExit;
    public AudioSource carEngineSound; // drag it in here

    [Header("Keys")]
    public string requiredItem = "CarKeys";  // left at the office; empty = no key needed
    [TextArea] public string noKeysLine = "...my keys. Where are my keys?|Still in my desk. At the office, down the street.";
    public GameObject[] showWhenKeysMissing; // e.g. the red beacon over the office door
    public UnityEngine.Events.UnityEvent onEnter = new UnityEngine.Events.UnityEvent(); // e.g. BloodWave.Begin

    private bool playerInRange = false;
    private bool inCar = false;
    public bool InCar => inCar;
    private bool locked = false; // car destroyed or story over: can't get back in

    void Start()
    {
        carController.enabled = false;
        carCamera.enabled = false;
        carCamera.GetComponent<AudioListener>().enabled = false;

        if (carEngineSound != null)
            carEngineSound.Stop(); // make sure it's silent from the start, regardless of Play On Awake
    }

    void Update()
    {
        if (!locked && playerInRange && Input.GetKeyDown(KeyCode.F))
        {
            if (inCar) ExitCar();
            else if (!string.IsNullOrEmpty(requiredItem) && !Inventory.Has(requiredItem)) NoKeys();
            else EnterCar();
        }
    }

    void NoKeys()
    {
        if (DialogueBox.Instance != null) DialogueBox.Instance.Say(noKeysLine);
        foreach (var g in showWhenKeysMissing)
            if (g != null) g.SetActive(true);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == playerRoot && !inCar && !locked)
        {
            playerInRange = true;
            promptUI.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject == playerRoot)
        {
            playerInRange = false;
            promptUI.SetActive(false);
        }
    }

    void EnterCar()
    {
        inCar = true;
        promptUI.SetActive(false);

        playerMovementScript.enabled = false;
        punchController.enabled = false;
        playerCamera.enabled = false;
        playerCamera.GetComponent<AudioListener>().enabled = false;
        playerRoot.SetActive(false);

        carController.enabled = true;
        carCamera.enabled = true;
        carCamera.GetComponent<AudioListener>().enabled = true;
        // the chase camera always follows when you get in, starting right behind the car
        var follow = carCamera.GetComponent<CameraFollow>();
        if (follow != null) { follow.enabled = true; follow.Snap(); }

        if (carEngineSound != null)
            carEngineSound.Play();

        Survivor.BoardAll(transform.position);
        onEnter.Invoke();
    }

    // kick the player out (car destroyed, bridge dead end); lockCar stops them getting back in
    public void ForceExit(bool lockCar)
    {
        locked = lockCar;
        if (inCar) ExitCar();
        if (locked) promptUI.SetActive(false);
    }

    void ExitCar()
    {
        inCar = false;

        // cut the throttle and brake, otherwise the wheels keep their last torque
        if (carController is PrometeoCarController prometeo)
        {
            prometeo.ThrottleOff();
            prometeo.Brakes();
        }
        carController.enabled = false;
        carCamera.enabled = false;
        carCamera.GetComponent<AudioListener>().enabled = false;

        if (carEngineSound != null)
            carEngineSound.Stop();

        playerRoot.SetActive(true);
        playerRoot.transform.position = driverSeatExit.position;
        playerMovementScript.enabled = true;
        punchController.enabled = true;
        playerCamera.enabled = true;
        playerCamera.GetComponent<AudioListener>().enabled = true;

        promptUI.SetActive(!locked);
    }
}