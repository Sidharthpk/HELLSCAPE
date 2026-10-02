using System.Collections;
using UnityEngine;

// The two transformations in the silo, played in-engine from the player camera (letterboxed, controls locked,
// the flood holding its breath meanwhile):
//   Lost()   a colleague you let drown rises back out of the blood as one of the Lost: a cold blue glow under the
//            surface, their ghost drifting up out of it, a last word to you.
//   Damned() a colleague who'd owned up to something rotten comes back out of the blood as themselves, soaked
//            red, says it again - and burns into one of the Damned, screaming in your face (like Kessler did).
public class TurningCutscene : MonoBehaviour
{
    public BloodFlood flood;
    public Transform player;
    public Camera playerCam;
    public FirstPersonController fpc;
    public Behaviour[] lockDuring;           // movement, punching, the gun, the interactor
    public ScreenFader fader;
    public RectTransform letterboxTop, letterboxBottom;
    public float letterboxHeight = 110f;
    public AudioSource sfx;

    [Header("Lost")]
    [TextArea] public string lostSpeech = "Sam: ...you let go of me.|Sam: It's so cold down there. So quiet.|He's one of the Lost now. Because of me.";
    [Header("Damned")]
    [TextArea] public string damnedSpeech = "Maya: I said it. Down there... I said it out loud.|Maya: We all signed it. We all KNEW what he was selling.";
    [TextArea] public string damnedAfter = "...and it TOOK her.";

    public bool Playing { get; private set; }

    private float fov0;
    private Vector3 camPos0;
    private Light glow;
    private GameObject standIn;

    // ---------------------------------------------------------------- Sam -> the Lost

    public IEnumerator Lost(Survivor sv, System.Func<GameObject> spawnGhost)
    {
        Begin();
        Vector3 spot = new Vector3(sv.transform.position.x, flood.SurfaceY, sv.transform.position.z);
        yield return LookAt(spot, 1.0f);

        // the cold under the blood
        glow = MakeGlow(spot - Vector3.up * 0.6f, new Color(0.5f, 0.85f, 1f));
        Play("Silo Fight/Colleague turns into the Lost", ProceduralAudio.Whisper(4f), 0.9f);
        for (float t = 0f; t < 1.6f; t += Time.deltaTime)
        {
            glow.intensity = Mathf.Lerp(0f, 4f, t / 1.6f) * (0.8f + 0.2f * Mathf.PerlinNoise(t * 9f, 0f));
            glow.transform.position = new Vector3(spot.x, flood.SurfaceY - 0.6f, spot.z);
            Aim(glow.transform.position);
            yield return null;
        }

        // out it comes, and the camera follows it up
        var ghost = spawnGhost();
        Transform head = ghost != null ? ghost.transform : glow.transform;
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(lostSpeech);
        for (float t = 0f; t < 5.5f || (DialogueBox.Instance != null && DialogueBox.Instance.Busy && t < 12f); t += Time.deltaTime)
        {
            AimSmooth(head.position + Vector3.up * 1.3f, 3f);
            playerCam.fieldOfView = Mathf.Lerp(fov0, fov0 * 0.72f, Mathf.SmoothStep(0f, 1f, t / 4f));
            if (glow != null) glow.intensity = Mathf.Lerp(4f, 0f, t / 3f);
            yield return null;
        }
        yield return End();
    }

    // ---------------------------------------------------------------- Maya -> the Damned

    public IEnumerator Damned(Survivor sv, Vector3 step, RisenDamned risen)
    {
        Begin();
        Vector3 under = new Vector3(step.x, flood.SurfaceY - 0.4f, step.z);
        yield return LookAt(under, 1.0f);

        // something red under the blood, then her, standing up out of it
        glow = MakeGlow(under, new Color(1f, 0.12f, 0.05f));
        Play("Silo Fight/Blood flood rumble", ProceduralAudio.Rumble(3f), 0.7f);
        for (float t = 0f; t < 1.2f; t += Time.deltaTime) { glow.intensity = Mathf.Lerp(0f, 5f, t / 1.2f); yield return null; }

        standIn = StandIn(sv);
        Renderer[] renderers = standIn != null ? standIn.GetComponentsInChildren<Renderer>() : new Renderer[0];
        var block = new MaterialPropertyBlock();
        Color soaked = new Color(0.55f, 0.1f, 0.08f);
        Tint(renderers, block, soaked);
        Vector3 from = step - Vector3.up * 1.9f;
        Vector3 face = Flat(player.position - step);
        if (standIn != null) standIn.transform.SetPositionAndRotation(from, Quaternion.LookRotation(face));
        for (float t = 0f; t < 2.6f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 2.6f);
            if (standIn != null) standIn.transform.position = Vector3.Lerp(from, step, k);
            AimSmooth(Vector3.Lerp(from, step, k) + Vector3.up * 1.5f, 4f);
            yield return null;
        }

        // she says it again - and it takes her
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(damnedSpeech);
        Play("Silo Fight/Colleague turns into the Damned", null, 1f);
        Vector3 body0 = step;
        for (float t = 0f; t < 4f || (DialogueBox.Instance != null && DialogueBox.Instance.Busy && t < 12f); t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / 5f);
            Color c = Color.Lerp(soaked, new Color(1f, 0.05f, 0.02f), k) * (Random.value < 0.06f * k ? 0.3f : 1f);
            c.a = 1f;
            Tint(renderers, block, c);
            if (standIn != null) standIn.transform.position = body0 + Random.insideUnitSphere * 0.05f * k;
            glow.intensity = 5f + 5f * k;
            playerCam.fieldOfView = Mathf.Lerp(fov0, fov0 * 0.7f, Mathf.SmoothStep(0f, 1f, t / 4f));
            AimSmooth(step + Vector3.up * 1.5f, 5f);
            yield return null;
        }

        // flash - and it's in your face, screaming
        if (fader != null) yield return fader.FadeTo(1f, 0.07f);
        if (standIn != null) Destroy(standIn);
        if (glow != null) Destroy(glow.gameObject);
        playerCam.fieldOfView = fov0;
        risen.Appear(step);
        yield return new WaitForSeconds(0.2f);
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 0.15f));
        yield return Shake(0.8f, 0.08f);
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(damnedAfter);
        yield return new WaitForSeconds(1.2f);
        yield return End();
    }

    // checkpoint respawn in the middle of one: put everything back
    public void Abort()
    {
        if (!Playing) return;
        StopAllCoroutines();
        if (standIn != null) Destroy(standIn);
        if (glow != null) Destroy(glow.gameObject);
        playerCam.fieldOfView = fov0;
        playerCam.transform.localPosition = camPos0;
        Bars(0f);
        Lock(false);
        flood.Hold(false);
        Playing = false;
    }

    // ---------------------------------------------------------------- helpers

    void Begin()
    {
        Playing = true;
        fov0 = playerCam.fieldOfView;
        camPos0 = playerCam.transform.localPosition;
        flood.Hold(true);
        Lock(true);
        StartCoroutine(BarsIn());
    }

    IEnumerator End()
    {
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.8f);
            Bars(1f - k);
            playerCam.fieldOfView = Mathf.Lerp(playerCam.fieldOfView, fov0, k);
            yield return null;
        }
        if (glow != null) Destroy(glow.gameObject);
        playerCam.fieldOfView = fov0;
        playerCam.transform.localPosition = camPos0;
        Bars(0f);
        Lock(false);
        flood.Hold(false);
        Playing = false;
    }

    IEnumerator BarsIn()
    {
        for (float t = 0f; t < 0.6f; t += Time.deltaTime) { Bars(Mathf.SmoothStep(0f, 1f, t / 0.6f)); yield return null; }
        Bars(1f);
    }

    void Bars(float k)
    {
        float h = letterboxHeight * k;
        foreach (var b in new[] { letterboxTop, letterboxBottom })
            if (b != null) b.sizeDelta = new Vector2(b.sizeDelta.x, h);   // shared with the other cutscenes: only resize
    }

    void Lock(bool on)
    {
        foreach (var b in lockDuring) if (b != null) b.enabled = !on;
        if (on && player.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    Light MakeGlow(Vector3 at, Color c)
    {
        var l = new GameObject("Turning glow").AddComponent<Light>();
        l.transform.position = at;
        l.color = c;
        l.range = 9f;
        l.intensity = 0f;
        return l;
    }

    // the colleague as they were: a copy of their body, idle
    GameObject StandIn(Survivor sv)
    {
        if (sv.animator == null) return null;
        var body = Instantiate(sv.animator.gameObject);
        body.name = sv.displayName + " (turning)";
        foreach (var c in body.GetComponentsInChildren<Collider>(true)) Destroy(c);
        body.transform.localScale = sv.animator.transform.lossyScale;
        var an = body.GetComponent<Animator>();
        if (an != null) { an.applyRootMotion = false; an.SetFloat("Speed", 0f); }
        return body;
    }

    static void Tint(Renderer[] rs, MaterialPropertyBlock b, Color c)
    {
        foreach (var r in rs)
        {
            r.GetPropertyBlock(b);
            b.SetColor("_BaseColor", c);
            r.SetPropertyBlock(b);
        }
    }

    IEnumerator LookAt(Vector3 point, float time)
    {
        Quaternion y0 = player.rotation, p0 = playerCam.transform.localRotation;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            Vector3 d = point - playerCam.transform.position;
            player.rotation = Quaternion.Slerp(y0, Quaternion.LookRotation(Flat(d)), k);
            playerCam.transform.localRotation = Quaternion.Slerp(p0, Pitch(d), k);
            yield return null;
        }
        Aim(point);
    }

    void Aim(Vector3 point)
    {
        Vector3 d = point - playerCam.transform.position;
        player.rotation = Quaternion.LookRotation(Flat(d));
        playerCam.transform.localRotation = Pitch(d);
        if (fpc != null) fpc.SetPitch(playerCam.transform.localEulerAngles.x);
    }

    void AimSmooth(Vector3 point, float rate)
    {
        Vector3 d = point - playerCam.transform.position;
        float k = 1f - Mathf.Exp(-rate * Time.deltaTime);
        player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(Flat(d)), k);
        playerCam.transform.localRotation = Quaternion.Slerp(playerCam.transform.localRotation, Pitch(d), k);
        if (fpc != null) fpc.SetPitch(playerCam.transform.localEulerAngles.x);
    }

    static Quaternion Pitch(Vector3 d) => Quaternion.Euler(Mathf.Clamp(-Mathf.Atan2(d.y, Flat(d).magnitude) * Mathf.Rad2Deg, -80f, 80f), 0f, 0f);

    IEnumerator Shake(float time, float amount)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            playerCam.transform.localPosition = camPos0 + Random.insideUnitSphere * amount * (1f - t / time);
            yield return null;
        }
        playerCam.transform.localPosition = camPos0;
    }

    void Play(string slot, AudioClip fallback, float vol)
    {
        if (sfx == null) return;
        var c = Sounds.Clip(slot, fallback);
        if (c != null) sfx.PlayOneShot(c, vol * Sounds.Volume(slot));
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v : Vector3.forward; }
}
