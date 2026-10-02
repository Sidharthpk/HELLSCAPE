using UnityEngine;

// An analogue speedometer for the van: a dial and a needle, no digits ticking over. Reads the van's real speed
// over the ground (not the wheels, which spin and lock), eased so the needle sweeps instead of twitching, and
// rests dead on zero when you're stopped. On screen only while the van is being driven.
public class SpeedometerUI : MonoBehaviour
{
    public Rigidbody body;                  // the van
    public Behaviour drivingWhile;          // its controller: enabled = someone's at the wheel
    public GameObject dial;                 // everything that shows (a child; this object stays on to watch)
    public RectTransform needle;            // points up at rest in its own space; pivot on the hub
    public float maxKmh = 160f;
    public float zeroAngle = 135f;          // needle's z rotation at 0 ...
    public float fullAngle = -135f;         // ... and at maxKmh
    public float ease = 0.14f;              // seconds the needle takes to catch up
    public float restBelow = 0.8f;          // km/h: under this it reads zero (a parked rigidbody still creeps)

    private float shown, rate;

    void Start() => Show(false);

    void Update()
    {
        bool driving = body != null && drivingWhile != null && drivingWhile.isActiveAndEnabled && !TitleScreen.Showing;
        if (dial != null && dial.activeSelf != driving) Show(driving);
        if (!driving || needle == null) return;

        float kmh = body.linearVelocity.magnitude * 3.6f;
        if (kmh < restBelow) kmh = 0f;
        shown = Mathf.SmoothDamp(shown, Mathf.Min(kmh, maxKmh), ref rate, ease);
        needle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(zeroAngle, fullAngle, Mathf.Clamp01(shown / maxKmh)));
    }

    void Show(bool on)
    {
        if (dial != null) dial.SetActive(on);
        if (!on) return;
        // straight to the speed you're doing: no sweep up from zero each time it appears
        shown = body != null ? Mathf.Min(body.linearVelocity.magnitude * 3.6f, maxKmh) : 0f;
        rate = 0f;
    }
}
