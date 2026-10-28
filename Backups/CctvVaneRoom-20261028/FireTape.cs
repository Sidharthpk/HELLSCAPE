using System.Collections;
using UnityEngine;

// V5.1 A3C: the CCTV reconstruction of the office fire (StabCutscene "Cutscene - Mercer fire order" runs it in place of
// its timer). A locked door with the fire behind it, Mercer standing at it: the recording's exchange with Kessler, then
// Mercer turns to the camera and aims for Vane's execution; the shot ends the tape. The door rattles harder as the glow
// rises. Built by HellScape > Office > Fire Tape (FireTapeBuilder); CctvDesk plays it before the Tomas tape.
public class FireTape : MonoBehaviour
{
    public Animator mercer;                 // Mercer: "Idle" (first frame of the aim, held), "Shoot" (turns into the aim and holds it)
    public Transform door;                  // shakes
    public Light[] glow;                    // the fire behind the door
    public Light flash;
    public float shotAt = 1.75f;            // seconds into "Shoot": the end of the aim
    public string fireLines = "A3C_fire_tape", executionLines = "A3C_vane_death_tape";
    public string personalWeaponLines;

    Vector3 doorHome;
    Coroutine burn;
    float rattle;                           // 0..1

    void Awake() => GetComponent<StabCutscene>().sequence = Beats;

    IEnumerator Beats()
    {
        doorHome = door.localPosition;
        glow = System.Array.FindAll(glow, l => l != null);   // (the scene's lights were cleared: some are gone)
        rattle = 0f;
        mercer.gameObject.SetActive(true);
        mercer.Play("Idle", 0, 0f); mercer.speed = 0f;
        if (flash != null) flash.enabled = false;
        foreach (var l in glow) l.intensity = 1.5f;
        burn = StartCoroutine(Burn());

        // the building is already burning: they are still inside
        yield return new WaitForSeconds(1.6f);
        if (!string.IsNullOrEmpty(personalWeaponLines)) yield return Talk(personalWeaponLines);
        yield return Talk(fireLines);
        yield return new WaitForSeconds(.6f);

        // Vane, off camera: Mercer turns to the lens and aims
        Say(executionLines);
        mercer.speed = 1f;
        mercer.Play("Shoot", 0, 0f);
        yield return null;
        var info = mercer.GetCurrentAnimatorClipInfo(0);
        float len = info.Length > 0 ? info[0].clip.length : shotAt + .05f;
        while (Seconds(len) < shotAt - .12f) yield return null;
        if (Busy) { mercer.speed = 0f; while (Busy) yield return null; yield return new WaitForSeconds(.4f); mercer.speed = 1f; }
        while (Seconds(len) < shotAt) yield return null;
        Shoot();
        yield return new WaitForSeconds(.5f);   // the recording cuts
    }

    IEnumerator Burn()
    {
        for (float t = 0f; ; t += Time.deltaTime)
        {
            rattle = Mathf.Min(1f, t / 12f);
            float k = Mathf.PerlinNoise(t * 6f, 3f);
            foreach (var l in glow) l.intensity = Mathf.Lerp(1.5f, 7f, rattle) * Mathf.Lerp(.7f, 1.2f, k);
            door.localPosition = doorHome + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * .035f * rattle;
            yield return null;
        }
    }

    float Seconds(float len)
    {
        var s = mercer.GetCurrentAnimatorStateInfo(0);
        return s.IsName("Shoot") ? s.normalizedTime * len : 0f;
    }

    void Shoot()
    {
        if (burn != null) StopCoroutine(burn);
        rattle = 0f;
        door.localPosition = doorHome;
        var bang = Sounds.Clip("Silo Fight/Rifle shot", ProceduralAudio.Gunshot());
        var src = gameObject.GetOrAdd<AudioSource>();
        src.spatialBlend = 0f;
        src.PlayOneShot(bang, Sounds.Volume("Silo Fight/Rifle shot"));
        if (flash != null) StartCoroutine(Flash());
        foreach (var l in glow) l.intensity = 0f;   // (the tape drops out)
    }

    IEnumerator Flash() { flash.enabled = true; yield return new WaitForSeconds(.07f); flash.enabled = false; }

    static bool Busy => DialogueBox.Instance != null && DialogueBox.Instance.Busy;
    static void Say(string trigger) { if (DialogueBox.Instance != null) DialogueBox.Instance.Say(HS5.S(trigger)); }
    static IEnumerator Talk(string trigger)
    {
        while (Busy) yield return null;
        Say(trigger);
        yield return null;
        while (Busy) yield return null;
    }
}
