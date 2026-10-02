using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A body bag on the office floor that isn't as dead as it looks. Walk up to it and it goes off: the lights stutter,
// a sting and a scream, your head is wrenched round onto it and it thrashes inside the bag (the model's own
// ArmatureAction), the room strobing; then it goes still. The first bag you reach does the full scare; the other
// one (big = false) only twitches as you pass. Built by HellScape > Office > Body Bag Jumpscares.
public class BodyBagScare : MonoBehaviour
{
    public AnimationClip clip;               // Body|ArmatureAction from Body.fbx (sampled onto this object)
    public bool big = true;                  // the full jumpscare, or just a twitch
    public float triggerDistance = 2.6f;
    public float clipSpeed = 1.35f;          // it thrashes faster than the file's pace
    public float thrashTime = 1.2f;          // how long your head is held on it while it thrashes
    public float twitchTime = 0.6f;

    [Header("The player")]
    public Transform player;
    public Camera playerCam;
    public Behaviour[] lockDuring;           // movement / look / fists, off while your head is wrenched round
    public GameObject[] hideDuring;          // arms / gun on the camera

    [Header("Room")]
    public float lightRadius = 14f;          // lamps this close flicker
    [TextArea] public string afterLine = "It MOVED. Something's still-- something's in there.|...no. No. It's just a body. It's just a body.";
    [TextArea] public string twitchLine = "...did that one just move too?";

    static bool bigDone;                     // only one full scare per run: the second bag is a twitch
    bool fired;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { bigDone = false; UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => bigDone = false; }

    void Start()
    {
        if (player == null) { var p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.transform; }
        if (clip != null) clip.SampleAnimation(gameObject, 0f);   // lying still, first frame
    }

    void Update()
    {
        if (fired || player == null || !player.gameObject.activeInHierarchy) return;
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.magnitude > triggerDistance) return;
        if (!CanSee()) return;               // not through a wall: you have to actually be looking at it
        fired = true;
        bool full = big && !bigDone;
        if (full) bigDone = true;
        StartCoroutine(full ? Scare() : Twitch());
    }

    // ------------------------------------------------------------------ the full scare
    IEnumerator Scare()
    {
        var lamps = NearLights();
        var lampI = new List<float>();
        foreach (var l in lamps) lampI.Add(l.intensity);

        // a beat of wrongness first: the lights dip
        for (float t = 0f; t < 0.18f; t += Time.deltaTime)
        {
            for (int i = 0; i < lamps.Count; i++) lamps[i].intensity = lampI[i] * (Random.value < 0.5f ? 0.1f : 0.6f);
            yield return null;
        }

        foreach (var b in lockDuring) if (b != null) b.enabled = false;
        if (player.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        var hidden = new List<GameObject>();
        foreach (var g in hideDuring) if (g != null && g.activeSelf) { g.SetActive(false); hidden.Add(g); }

        Transform cam = playerCam.transform;
        Quaternion camLocal0 = cam.localRotation;
        float fov0 = playerCam.fieldOfView;
        Vector3 target = Centre();

        Play("Office/Kessler lunges", 1f, 1f);
        Play("Hell City/Zombie scream", 0.9f, 0.7f);

        // wrenched round onto it, and in close
        Quaternion from = cam.rotation;
        float dur = thrashTime;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float snap = Mathf.Clamp01(t / 0.14f);
            Quaternion look = Quaternion.LookRotation(target - cam.position);
            float shake = Mathf.Lerp(2.2f, 0.4f, t / dur);
            cam.rotation = Quaternion.Slerp(from, look, 1f - Mathf.Pow(1f - snap, 3f)) * Quaternion.Euler(Random.Range(-shake, shake), Random.Range(-shake, shake), Random.Range(-shake, shake) * 0.5f);
            playerCam.fieldOfView = Mathf.Lerp(fov0, fov0 * 0.62f, Mathf.Clamp01(t / 0.25f));
            if (clip != null) clip.SampleAnimation(gameObject, Mathf.Repeat(t * clipSpeed, clip.length));
            // the room strobes
            for (int i = 0; i < lamps.Count; i++) lamps[i].intensity = lampI[i] * (Mathf.PerlinNoise(t * 22f, i) > 0.45f ? 1.3f : 0.05f);
            yield return null;
        }

        // it goes still
        if (clip != null) clip.SampleAnimation(gameObject, 0f);
        for (int i = 0; i < lamps.Count; i++) lamps[i].intensity = lampI[i];
        Play("Prologue/Heartbeat", 0.8f, 1.15f);
        Quaternion held = cam.rotation;
        for (float t = 0f; t < 0.45f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.45f);
            cam.localRotation = Quaternion.Slerp(cam.parent != null ? Quaternion.Inverse(cam.parent.rotation) * held : held, camLocal0, k);
            playerCam.fieldOfView = Mathf.Lerp(fov0 * 0.62f, fov0, k);
            yield return null;
        }
        cam.localRotation = camLocal0;
        playerCam.fieldOfView = fov0;
        foreach (var g in hidden) if (g != null) g.SetActive(true);
        foreach (var b in lockDuring) if (b != null) b.enabled = true;
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(afterLine)) DialogueBox.Instance.SayNow(afterLine);
    }

    // ------------------------------------------------------------------ the aftershock: no camera grab, just a twitch as you pass
    IEnumerator Twitch()
    {
        Play("Office/Kessler lunges", 0.45f, 1.2f);
        float dur = twitchTime;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            if (clip != null) clip.SampleAnimation(gameObject, Mathf.Repeat(t * clipSpeed * 1.3f, clip.length));
            yield return null;
        }
        if (clip != null) clip.SampleAnimation(gameObject, 0f);
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(twitchLine)) DialogueBox.Instance.Say(twitchLine);
    }

    // a clear line from your eyes to the bag (the office walls are one-sided: check back faces too)
    bool CanSee()
    {
        if (playerCam == null) return true;
        Vector3 eye = playerCam.transform.position, tgt = Centre() + Vector3.up * 0.15f;
        float dist = Vector3.Distance(eye, tgt);
        bool bf = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        var hits = Physics.RaycastAll(eye, tgt - eye, dist, ~0, QueryTriggerInteraction.Ignore);
        Physics.queriesHitBackfaces = bf;
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(player.root) || h.collider.transform.IsChildOf(transform)) continue;
            if (dist - h.distance < 0.35f) continue;   // the floor right under it
            return false;
        }
        return true;
    }

    Vector3 Centre()
    {
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return transform.position + Vector3.up * 0.3f;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.center;
    }

    List<Light> NearLights()
    {
        var list = new List<Light>();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.enabled && l.type != LightType.Directional && (l.transform.position - transform.position).sqrMagnitude < lightRadius * lightRadius) list.Add(l);
        return list;
    }

    void Play(string slot, float volume, float pitch)
    {
        var c = Sounds.Clip(slot, null);
        if (c == null || playerCam == null) return;
        var go = new GameObject("Scare sound");
        go.transform.position = playerCam.transform.position;
        var s = go.AddComponent<AudioSource>();
        s.clip = c; s.pitch = pitch; s.volume = volume * Sounds.Volume(slot); s.spatialBlend = 0f;
        s.Play();
        Destroy(go, Mathf.Min(5f, c.length / Mathf.Max(0.1f, pitch) + 0.2f));   // (the heartbeat is a long loop: a few beats is enough)
    }
}
