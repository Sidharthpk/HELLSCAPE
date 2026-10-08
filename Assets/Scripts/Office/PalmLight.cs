using UnityEngine;

// The light in your hands. In the blacked-out office (InteriorDarkness.blackoutUntilPowered, before the fuses are in)
// Q plays the arms' push (ArmsRig_push.R) and holds it at frame 13, palms raised: the mark on them gives off a beam
// where you look, a flashlight. Q again plays the rest of the clip (hands coming down), then the fists come back up
// into the guard. The fuses going in lowers them too. Sits next to PunchController (InteriorDarkness adds it); the
// fists are off while the hands are up.
public class PalmLight : MonoBehaviour
{
    public KeyCode key = KeyCode.Q;
    public Color color = new Color(1f, 0.8f, 0.58f);
    public float range = 24f, angle = 65f, intensity = 8f;
    [TextArea] public string hint = "I can't see a thing...|The mark on my palms. It's glowing. [Q] to raise my hands.";

    const string Push = "ArmsRig_push_R";     // the user's clip, as a state on ArmsController (30 fps, 23 frames)
    const float ClipFrames = 23f, HoldFrame = 13f;
    const float holdAt = HoldFrame / ClipFrames;   // normalized time of the pause: palms raised

    enum Phase { Down, Raising, Held, Lowering }
    Phase phase;

    PunchController punch;
    Animator arms;
    Transform cam, palmR, palmL;
    Light beam, glowR, glowL;
    bool hinted;
    float phaseAt;

    void Start()
    {
        punch = GetComponent<PunchController>();
        if (punch == null) { enabled = false; return; }
        arms = punch.armsAnimator; cam = punch.cam;
        foreach (var t in arms.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "palm.03.R") palmR = t;
            else if (t.name == "palm.03.L") palmL = t;
        }
        beam = MakeLight("Palm beam", LightType.Spot);
        beam.range = range; beam.spotAngle = angle; beam.innerSpotAngle = angle * 0.45f; beam.intensity = intensity;
        beam.shadows = LightShadows.Hard;
        glowR = MakeLight("Palm glow R", LightType.Point); glowR.range = 3f; glowR.intensity = 1.8f;
        glowL = MakeLight("Palm glow L", LightType.Point); glowL.range = 2.5f; glowL.intensity = 1.2f;
        Show(false);
    }

    Light MakeLight(string name, LightType type)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.type = type; l.color = color;
        return l;
    }

    void Show(bool v) { beam.enabled = glowR.enabled = glowL.enabled = v; }

    void Update()
    {
        bool allowed = InteriorDarkness.Blackout && !GunController.Equipped && Time.timeScale > 0f && !PauseMenu.Paused;
        if (allowed && !hinted && DialogueBox.Instance != null) { hinted = true; DialogueBox.Instance.Say(hint); }
        if (allowed && Input.GetKeyDown(key)) Set(phase == Phase.Down);
        else if (!allowed && (phase == Phase.Raising || phase == Phase.Held)) Set(false);

        var st = arms.GetCurrentAnimatorStateInfo(0);
        bool inPush = st.IsName(Push) && !arms.IsInTransition(0);
        switch (phase)
        {
            case Phase.Raising:
                // (timeout: if the clip never reports in, e.g. another state took over, still raise the light)
                if ((inPush && st.normalizedTime >= holdAt) || Time.time - phaseAt > 0.6f)
                {
                    arms.Play(Push, 0, holdAt);   // frame 13, exactly
                    arms.speed = 0f;              // ...and paused there
                    phase = Phase.Held;
                    Show(true);
                }
                break;
            case Phase.Held:
                beam.intensity = intensity * (0.94f + 0.06f * Mathf.Sin(Time.time * 17f) * Mathf.Sin(Time.time * 5.3f));
                break;
            case Phase.Lowering:
                if ((inPush && st.normalizedTime >= 0.98f) || Time.time - phaseAt > 0.8f)
                {
                    arms.speed = 1f;
                    arms.Play("GuardDraw", 0, 0f);   // hands down, then the fists back up into the guard
                    punch.enabled = true;
                    phase = Phase.Down;
                }
                break;
        }
    }

    // true: raise the hands; false: let the clip play out and bring them down
    void Set(bool up)
    {
        if (up)
        {
            if (phase != Phase.Down) return;
            punch.enabled = false;                 // no punching with your hands up
            arms.speed = 1f;
            arms.CrossFadeInFixedTime(Push, 0.1f, 0, 0f);
            phase = Phase.Raising;
            phaseAt = Time.time;
        }
        else
        {
            if (phase != Phase.Raising && phase != Phase.Held) return;
            Show(false);
            arms.speed = 1f;                       // (resumes from the pause)
            phase = Phase.Lowering;
            phaseAt = Time.time;
        }
    }

    void LateUpdate()
    {
        if (phase != Phase.Held) return;
        // the beam leaves the palm and goes where you look; the palms glow
        Vector3 from = (palmR != null ? palmR.position : cam.position + cam.forward * 0.5f) + cam.forward * 0.12f;
        beam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(cam.forward));
        if (palmR != null) glowR.transform.position = palmR.position;
        if (palmL != null) glowL.transform.position = palmL.position;
    }

    void OnDestroy()
    {
        foreach (var l in new[] { beam, glowR, glowL }) if (l != null) Destroy(l.gameObject);
    }
}
