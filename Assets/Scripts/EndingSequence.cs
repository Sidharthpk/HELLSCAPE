using System.Collections;
using System.Linq;
using UnityEngine;

// Bridge dead end: Kessler is dead, and what owns the city comes up to weigh you. There is no angel: the Demon
// King rises either way. Judgement.IsDamned weighs the killer's fate, how you treated the ghosts and the
// colleagues you saved:
//   more sin    -> "you're one of mine": you become one of the Damned and walk back into the city (the bad ending)
//   more virtue -> he has a use for you. You made Kessler SAY it; the Lost are hiding their sins from him and he
//                  can't take what isn't spoken. He marks you as his confessor: make them all confess (and put
//                  down what they turn into), and he'll let you go. TO BE CONTINUED.
// Staging: you are put near the very end of the bridge, looking out over the edge, and he comes up out of the sea
// BEYOND the end of the deck (never through it), standing in the water and towering over the edge. He goes back
// down into the sea the same way. (ContinuedEndingBuilder places his points and yours from the arena's deck.)
// PlayEnding() is called by the bridge boss's death.
public class EndingSequence : MonoBehaviour
{
    [System.Serializable]
    public class Being
    {
        public GameObject root;             // disabled in scene by default
        public Animator animator;
        public string animTrigger = "Appear";
        public Transform startPoint;        // under the sea, beyond the end of the bridge
        public Transform endPoint;          // where it stands: in the sea just past the end of the deck
        public AudioSource sound;
        public GameObject endCard;          // final text on the Canvas, disabled by default
    }

    public Being demon;                     // the Demon King (both endings)

    [Header("Scene")]
    public CarInteract carInteract;
    public Rigidbody carRb;
    public Camera playerCamera;
    public MonoBehaviour playerMovementScript;
    public MonoBehaviour punchController;
    public ZombieSpawner spawner;
    public ScreenFader fader;               // optional

    [Header("Timing")]
    public float carStopTime = 2f;
    public float exitDelay = 1f;
    public float approachDuration = 4f;
    public float holdBeforeAnim = 1f;
    public float endCardDelay = 4f;

    [Header("Bad ending: you become one of them")]
    public GameObject playerDemon;          // disabled; appears where you stand and walks back to the city
    public Camera cutsceneCam;              // disabled; films the walk
    public Transform walkTarget;            // back along the bridge, toward the city
    public float walkSpeed = 1.5f;
    public float frontShotTime = 5f;        // walking toward the camera, the king looming behind
    public float backShotTime = 6f;         // from behind, walking off into the red haze
    public GameObject[] hideDuringCutscene; // HUD etc.
    public AudioSource demonLaugh;          // starts when the king empowers, fades out before the end card

    [Header("Not damned: he binds you instead (to be continued)")]
    public GameObject continuedCard;        // "TO BE CONTINUED" on the Canvas, disabled by default
    [TextArea] public string brandLine = "My hand-- it's BURNING. There's a mark in it. His mark.";
    [TextArea] public string boundLine = "He's gone. The fog at the end of the bridge never moved.|Behind me the city's still red. Still full of them, hiding what they did.|...fine. Who's next?";
    public float sinkTime = 5f;             // the king going back down
    public GameObject walker;               // the Meshy character (walk animation, looping), disabled; walks off toward the city
    public GameObject titleCard;            // HELLSCAPE / TO BE CONTINUED down the left side, disabled
    public AudioClip endTrack;              // replaces the laugh in this ending: fades in once the king has gone down
    public float endTrackVolume = 0.8f;
    public float walkerSpeed = 0.5f;        // m/s: the clip's own stride (2.27 m per 4.97 s loop), so the feet don't skate
    public float walkAwayTime = 16f;       // at least this long, and until the boundLine is done

    [Header("Where you stand for it")]
    public Transform standPoint;            // on the deck near the end of the bridge, facing out (empty: wherever you are)

    [Header("Framing the king")]
    public float maxFov = 85f;              // the view widens until all of him fits, head to feet, up to this
    public float headRoom = 1.06f;          // how much taller than him the frame is (his arms go up)

    [Header("The verdict (spoken before the end card)")]
    [TextArea] public string arriveLine = "The bridge just... ends. Red fog, black water, and nothing past it.|The sea's moving. Something's coming UP out of it.";

    [Header("Testing")]
    public bool testOnStart = false;        // press Play in MainGameScene and the ending runs after 3 s (tick "testDamned" for the bad one)
    public bool testDamned = false;

    private bool played = false, walkedAway = false;

    IEnumerator Start()
    {
        if (!testOnStart) yield break;
        yield return new WaitForSeconds(3f);
        if (testDamned) Judgement.Karma = -99;
        PlayEnding();
    }

    // what the king says: everything you did in the city, read back to you, then the sentence
    public static string VerdictLines(bool damned)
    {
        const string who = "The Demon King: ";
        var l = new System.Collections.Generic.List<string>();
        l.Add(who + (damned ? "Still in your own skin. Still pretending. Let's see what you brought me."
                            : "Still in your own skin. Good. Stay in it. I have a use for it."));
        l.Add(who + "You signed a lie that buried a man. You never even read it.");
        if (Judgement.KilledKiller) l.Add(who + "You killed Elias Rourke. A brother, avenging his brother.");
        else if (Judgement.SparedKiller) l.Add(who + "You spared Elias Rourke. You let him tell you who Tomas was.");
        var turned = Survivor.All.Where(x => x != null && x.Turned).Select(x => x.displayName).ToList();
        if (turned.Count > 0) l.Add(who + "And the moment you showed him mercy, " + string.Join(" and ", turned) + " could not hold her tongue. She gave herself to me.");
        var saved = Survivor.All.Where(x => x != null && x.Rescued).Select(x => x.displayName).ToList();
        var lost = Survivor.All.Where(x => x != null && !x.Rescued && !x.Turned).Select(x => x.displayName).ToList();
        if (saved.Count > 0) l.Add(who + "You carried " + string.Join(" and ", saved) + " out of the blood.");
        if (lost.Count > 0) l.Add(who + "You left " + string.Join(" and ", lost) + " behind.");
        if (Judgement.Karma >= 2) l.Add(who + "You were kind to the dead, when kindness cost you nothing.");
        else if (Judgement.Karma <= -2) l.Add(who + "You were cruel to the dead. They remember.");
        if (damned)
        {
            l.Add(who + "It isn't enough. It never was. You're one of mine now.");
            return string.Join("|", l);
        }
        l.Add(who + "It is not enough to leave. But it is enough to be useful.");
        l.Add(who + "Kessler hid from me for forty years. One conversation with you, and he SAID it. That is how I take them.");
        l.Add(who + "My city is full of the Lost. Every one of them is hiding a sin from me, and I cannot touch what is not spoken.");
        l.Add(who + "So you will speak to them for me. You will make them confess. Every last one.");
        l.Add("...and when they turn? When they come at me, like he did?");
        l.Add(who + "Then you put them down, and you bring me what is left.");
        l.Add(who + "Finish my work, confessor, and I will let you go. Until then, the bridge stays shut.");
        return string.Join("|", l);
    }

    public static bool Playing { get; private set; }   // (the gun is put away for the judgement)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        Playing = false;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => Playing = false;
    }

    public void PlayEnding()
    {
        if (played) return;
        played = true;
        Playing = true;
        StartCoroutine(RunEnding());
    }

    IEnumerator RunEnding()
    {
        MusicManager.Area("Ending");
        // silence the horde so nothing interrupts the scene
        if (spawner != null) spawner.enabled = false;
        foreach (var ai in FindObjectsByType<ZombieAI>(FindObjectsSortMode.None))
        {
            ai.enabled = false;
            if (ai.agent != null && ai.agent.enabled && ai.agent.isOnNavMesh) ai.agent.isStopped = true;
            if (ai.animator != null) ai.animator.SetFloat("Speed", 0f);
        }

        // the car gives out at the dead end
        if (carInteract != null && carInteract.InCar)
        {
            if (carInteract.carController is PrometeoCarController prometeo)
            {
                prometeo.ThrottleOff();
                prometeo.Brakes();
            }
            carInteract.carController.enabled = false;

            if (carRb != null)
            {
                Vector3 v0 = carRb.linearVelocity;
                float s = 0f;
                while (s < carStopTime)
                {
                    s += Time.deltaTime;
                    carRb.linearVelocity = Vector3.Lerp(v0, Vector3.zero, s / carStopTime);
                    yield return null;
                }
                carRb.linearVelocity = Vector3.zero;
            }

            yield return new WaitForSeconds(exitDelay);
            carInteract.ForceExit(true);
        }

        if (playerMovementScript != null) playerMovementScript.enabled = false;
        if (punchController != null) punchController.enabled = false;
        if (standPoint != null) yield return TakeYourPlace();
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(arriveLine)) DialogueBox.Instance.SayNow(arriveLine);

        bool damned = Judgement.IsDamned();
        Being being = demon;

        being.root.SetActive(true);
        being.root.transform.position = being.startPoint.position;
        Sounds.Apply(being.sound, "Ending/Demon rises");
        if (being.sound != null) being.sound.Play();
        LightUp(being.root.transform, new Color(1f, 0.28f, 0.1f), 14f, 55f);   // unlit, the king is a smudge in the haze

        // look at the middle of the model, not its pivot (big beings have their pivot at their feet)
        Vector3 lookOffset = Vector3.zero;
        var renderers = being.root.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            lookOffset = b.center - being.root.transform.position;
            kingTop = b.max.y - being.root.transform.position.y;
            kingBottom = b.min.y - being.root.transform.position.y;
        }
        fov0 = playerCamera != null ? playerCamera.fieldOfView : 60f;

        float t = 0f;
        while (t < approachDuration)
        {
            t += Time.deltaTime;
            being.root.transform.position = Vector3.Lerp(
                being.startPoint.position, being.endPoint.position, t / approachDuration);
            FacePlayer(being.root.transform);
            Frame(being, 4f);
            yield return null;
        }

        for (float w = 0f; w < holdBeforeAnim; w += Time.deltaTime) { Frame(being, 4f); yield return null; }

        // the verdict: it reads back what you did, and passes sentence
        if (DialogueBox.Instance != null)
        {
            DialogueBox.Instance.SayNow(VerdictLines(damned));
            yield return null;
            for (float w = 0f; DialogueBox.Instance.Busy && w < 90f; w += Time.deltaTime)
            {
                FacePlayer(being.root.transform);
                Frame(being, 4f);
                yield return null;
            }
        }
        if (being.animator != null) being.animator.SetTrigger(being.animTrigger);
        if (demonLaugh != null && (damned || endTrack == null)) { demonLaugh.pitch = 1f; demonLaugh.Play(); }   // (not damned: the track comes in after he goes down)

        if (damned)
        {
            for (float w = 0f; w < endCardDelay; w += Time.deltaTime) { Frame(being, 4f); yield return null; }
            if (playerDemon != null && cutsceneCam != null) yield return BecomeOneOfThem();
        }
        else yield return Bound(being, lookOffset);
        if (demonLaugh != null && demonLaugh.isPlaying && !walkedAway) StartCoroutine(FadeOutAudio(demonLaugh, 2.5f));
        if (fader != null) yield return fader.FadeTo(1f, 2f);
        var card = walkedAway ? titleCard : damned || continuedCard == null ? being.endCard : continuedCard;
        if (card != null)
        {
            // over the black, not under it (the fader comes later on the canvas than the cards do)
            if (fader != null && fader.transform.parent == card.transform.parent && card.transform.GetSiblingIndex() < fader.transform.GetSiblingIndex())
                card.transform.SetSiblingIndex(fader.transform.GetSiblingIndex());
            card.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // a short blackout, and you're at the end of the bridge looking out over the edge (wherever the fight left you)
    IEnumerator TakeYourPlace()
    {
        Transform you = playerMovementScript != null ? playerMovementScript.transform : playerCamera.transform.root;
        if (fader != null) yield return fader.FadeTo(1f, 0.6f);

        // keep your feet as high off the ground as they are now
        float height = 1f;
        foreach (var h in Physics.RaycastAll(you.position, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(you)) { height = h.distance; break; }
        Vector3 spot = standPoint.position;
        // not inside the van, if that's where it stopped
        if (carRb != null)
        {
            Vector3 d = carRb.position - spot; d.y = 0f;
            if (d.magnitude < 6f) spot += standPoint.right * (Vector3.Dot(d, standPoint.right) > 0f ? -7f : 7f);
        }
        if (Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 8f, ~0, QueryTriggerInteraction.Ignore)) spot = ground.point;
        var body = you.GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic) body.linearVelocity = Vector3.zero;
        Vector3 fwd = standPoint.forward; fwd.y = 0f;
        you.SetPositionAndRotation(spot + Vector3.up * height, Quaternion.LookRotation(fwd));
        if (playerCamera != null) playerCamera.transform.localRotation = Quaternion.identity;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(0.3f);
        if (fader != null) yield return fader.FadeTo(0f, 0.9f);
    }

    // Not damned: his mark burns into your hand, he goes back down under the bridge, and you turn round to the
    // city you now have to work through.
    IEnumerator Bound(Being king, Vector3 lookOffset)
    {
        // the mark: a red flare over everything, your view jolting
        var img = fader != null ? fader.GetComponent<UnityEngine.UI.Image>() : null;
        Color black = img != null ? img.color : Color.black;
        if (img != null) img.color = new Color(0.85f, 0.06f, 0.02f, black.a);
        Vector3 cam0 = playerCamera.transform.localPosition;
        Sounds.OneShot(king.sound, "Ending/Demon rises", null, 0.8f);
        const float flare = 1.1f;
        for (float t = 0f; t < flare; t += Time.deltaTime)
        {
            float k = t / flare;
            if (fader != null) fader.fade.alpha = 0.6f * Mathf.Sin(k * Mathf.PI);
            playerCamera.transform.localPosition = cam0 + Random.insideUnitSphere * 0.07f * (1f - k);
            Frame(king, 4f);
            yield return null;
        }
        playerCamera.transform.localPosition = cam0;
        if (fader != null) fader.fade.alpha = 0f;
        if (img != null) img.color = black;
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(brandLine)) DialogueBox.Instance.SayNow(brandLine);
        for (float w = 0f; w < 2.2f; w += Time.deltaTime) { Frame(king, 4f); yield return null; }

        // he goes back down the way he came
        Vector3 from = king.root.transform.position, to = king.startPoint.position;
        for (float t = 0f; t < sinkTime; t += Time.deltaTime)
        {
            king.root.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / sinkTime));
            Vector3 watch = king.root.transform.position + lookOffset;
            watch.y = Mathf.Max(watch.y, playerCamera.transform.position.y - 4f);   // watch him go under; don't end up staring at your feet
            TurnCamera(watch, 3f);
            yield return null;
        }
        king.root.SetActive(false);
        if (endTrack != null && demonLaugh != null)
        {
            demonLaugh.Stop();
            demonLaugh.clip = endTrack; demonLaugh.pitch = 1f; demonLaugh.loop = true; demonLaugh.volume = 0f;
            demonLaugh.Play();
            StartCoroutine(FadeInAudio(demonLaugh, endTrackVolume, 4f));
        }

        if (walker != null && cutsceneCam != null) { yield return WalkAway(); walkedAway = true; yield break; }

        // and you turn round: the city
        Vector3 city = walkTarget != null ? walkTarget.position + Vector3.up * 4f : playerCamera.transform.position - playerCamera.transform.forward * 30f;
        float wide = playerCamera.fieldOfView;
        for (float t = 0f; t < 2.2f; t += Time.deltaTime)
        {
            playerCamera.fieldOfView = Mathf.Lerp(wide, fov0, Mathf.SmoothStep(0f, 1f, t / 2.2f));   // back to your own eyes
            TurnCamera(city, 2.2f);
            yield return null;
        }
        playerCamera.fieldOfView = fov0;
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(boundLine))
        {
            DialogueBox.Instance.SayNow(boundLine);
            yield return null;
            for (float w = 0f; DialogueBox.Instance.Busy && w < 30f; w += Time.deltaTime) { TurnCamera(city, 2.2f); yield return null; }
        }
        yield return new WaitForSeconds(0.6f);
    }

    // Not damned, the last shot: you (the Meshy walker) head off toward the city while the boundLine plays over it,
    // the camera hanging back, rising and swinging off toward the skyline; HELLSCAPE / TO BE CONTINUED fades in on the left.
    IEnumerator WalkAway()
    {
        if (fader != null) yield return fader.FadeTo(1f, 0.6f);

        GameObject playerRoot = playerMovementScript != null ? playerMovementScript.gameObject : playerCamera.transform.root.gameObject;
        Vector3 feet = playerRoot.transform.position;
        playerRoot.SetActive(false);
        foreach (var g in hideDuringCutscene) if (g != null) g.SetActive(false);
        if (Physics.Raycast(feet + Vector3.up, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)) feet = hit.point;

        Vector3 dir = walkTarget != null ? walkTarget.position - feet : -playerCamera.transform.forward;
        dir.y = 0f; dir.Normalize();
        Vector3 left = -Vector3.Cross(Vector3.up, dir);
        Transform body = walker.transform;
        body.SetPositionAndRotation(feet, Quaternion.LookRotation(dir));
        walker.SetActive(true);

        cutsceneCam.enabled = true;
        var listener = cutsceneCam.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = true;
        Camera.SetupCurrent(cutsceneCam);
        Vector3 camStart = feet - dir * 5f + left * 3f + Vector3.up * 1.7f;
        Vector3 skyline = feet + dir * 120f + Vector3.up * 12f;

        CanvasGroup titleGroup = null;
        if (titleCard != null)
        {
            if (fader != null && fader.transform.parent == titleCard.transform.parent) titleCard.transform.SetSiblingIndex(fader.transform.GetSiblingIndex());   // over the later blackout
            titleGroup = titleCard.GetComponent<CanvasGroup>();
            if (titleGroup == null) titleGroup = titleCard.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;
            titleCard.SetActive(true);
        }
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 1.2f));
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(boundLine)) DialogueBox.Instance.SayNow(boundLine);

        for (float t = 0f; t < 45f; t += Time.deltaTime)
        {
            bool talking = DialogueBox.Instance != null && DialogueBox.Instance.Busy;
            if (t > walkAwayTime && !talking) break;
            body.position += dir * walkerSpeed * Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / walkAwayTime));
            cutsceneCam.transform.position = camStart + Vector3.up * (t * 0.35f) - dir * (t * 0.1f);
            // aim left of him so he sits on the right of the frame, easing off toward the skyline as he shrinks
            Vector3 aim = Vector3.Lerp(body.position + Vector3.up * 1.1f + left * 2.5f, skyline + left * 2.5f, k * 0.7f);
            cutsceneCam.transform.rotation = Quaternion.Slerp(cutsceneCam.transform.rotation, Quaternion.LookRotation(aim - cutsceneCam.transform.position), 1f - Mathf.Exp(-3f * Time.deltaTime));
            if (titleGroup != null) titleGroup.alpha = Mathf.Clamp01((t - 3f) / 2f);
            yield return null;
        }
        if (titleGroup != null) titleGroup.alpha = 1f;
    }

    IEnumerator FadeInAudio(AudioSource source, float volume, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            source.volume = Mathf.Lerp(0f, volume, t / duration);
            yield return null;
        }
        source.volume = volume;
    }

    IEnumerator FadeOutAudio(AudioSource source, float duration)
    {
        float start = source.volume;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            source.volume = Mathf.Lerp(start, 0f, t / duration);
            yield return null;
        }
        source.Stop();
        source.volume = start;
    }

    // Bad ending: black flash, you are the demon now, walking back toward the hell city.
    IEnumerator BecomeOneOfThem()
    {
        if (fader != null) yield return fader.FadeTo(1f, 0.6f);

        // swap the first-person player for the demon body at the same spot
        GameObject playerRoot = playerMovementScript != null ? playerMovementScript.gameObject : playerCamera.transform.root.gameObject;
        Vector3 feet = playerRoot.transform.position;
        playerRoot.SetActive(false);
        foreach (var g in hideDuringCutscene) if (g != null) g.SetActive(false);
        if (Physics.Raycast(feet + Vector3.up, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)) feet = hit.point;

        Transform body = playerDemon.transform;
        Vector3 toCity = walkTarget.position - feet; toCity.y = 0f; toCity.Normalize();
        body.SetPositionAndRotation(feet, Quaternion.LookRotation(toCity));
        playerDemon.SetActive(true);
        if (body.Find("EndingLight") == null)   // keep the new you readable against the dark road
        {
            var g = new GameObject("EndingLight");
            g.transform.position = body.position + toCity * 2.5f + Vector3.up * 2.6f;
            g.transform.SetParent(body, true);
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.32f, 0.12f);
            l.intensity = 5f;
            l.range = 9f;
            l.shadows = LightShadows.None;
        }

        cutsceneCam.enabled = true;
        var listener = cutsceneCam.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = true;
        Camera.SetupCurrent(cutsceneCam);

        if (fader != null) StartCoroutine(fader.FadeTo(0f, 1.2f));

        // shot 1: low, in front of you, walking into camera with the king behind
        for (float t = 0f; t < frontShotTime; t += Time.deltaTime)
        {
            body.position += body.forward * walkSpeed * Time.deltaTime;
            Vector3 camPos = body.position + body.forward * (6.5f - t * 0.25f) + Vector3.up * 1.4f + body.right * 1.2f;
            cutsceneCam.transform.position = camPos;
            cutsceneCam.transform.LookAt(body.position + Vector3.up * 2.4f);
            yield return null;
        }

        // shot 2: from behind, walking off toward the city
        for (float t = 0f; t < backShotTime; t += Time.deltaTime)
        {
            body.position += body.forward * walkSpeed * Time.deltaTime;
            Vector3 camPos = body.position - body.forward * (4.5f + t * 0.6f) + Vector3.up * (2.2f + t * 0.35f);
            cutsceneCam.transform.position = camPos;
            cutsceneCam.transform.LookAt(body.position + body.forward * 8f + Vector3.up * 1.5f);
            yield return null;
        }
    }

    private float kingTop = 12f, kingBottom, fov0 = 60f;   // his height above / below his pivot; your normal view

    // look at the king so that ALL of him is in the picture: aim a little above his middle and widen the view
    // until his head (and raised arms) and his feet both fit
    void Frame(Being king, float speed)
    {
        if (playerCamera == null) return;
        Vector3 p = king.root.transform.position, eye = playerCamera.transform.position;
        float flat = Mathf.Max(1f, new Vector2(p.x - eye.x, p.z - eye.z).magnitude);
        float top = Mathf.Atan2(p.y + kingTop * headRoom - eye.y, flat) * Mathf.Rad2Deg;
        float bottom = Mathf.Atan2(Mathf.Max(p.y + kingBottom, eye.y - 1.8f) - eye.y, flat) * Mathf.Rad2Deg;   // (nothing to see below the deck)
        float aim = Mathf.Min(Mathf.Lerp(bottom, top, 0.5f), 40f);                                              // (TurnCamera never looks higher)
        float need = 2f * Mathf.Max(top - aim, aim - bottom) + 3f;
        float k = 1f - Mathf.Exp(-2.5f * Time.deltaTime);
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, Mathf.Clamp(need, fov0, maxFov), k);
        TurnCamera(new Vector3(p.x, eye.y + Mathf.Tan(aim * Mathf.Deg2Rad) * flat, p.z), speed);
    }

    // turn the player's view toward a point by yaw and pitch separately: a quaternion slerp over a big turn
    // (from the car door round and up to a being in the sky) rolls the horizon on the way
    void TurnCamera(Vector3 target, float speed)
    {
        if (playerCamera == null) return;
        Vector3 to = target - playerCamera.transform.position;
        float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
        pitch = Mathf.Max(pitch, -40f);   // never stare straight up: the hell skybox has an ugly seam at the zenith
        Vector3 e = playerCamera.transform.eulerAngles;
        float k = 1f - Mathf.Exp(-speed * Time.deltaTime);
        playerCamera.transform.rotation = Quaternion.Euler(Mathf.LerpAngle(e.x, pitch, k), Mathf.LerpAngle(e.y, yaw, k), 0f);
    }

    // a warm light a little in front of a being so it reads against the dark sky
    void LightUp(Transform being, Color c, float intensity, float range)
    {
        if (being.Find("EndingLight") != null) return;
        var g = new GameObject("EndingLight");
        Vector3 toYou = playerCamera != null ? playerCamera.transform.position - being.position : being.forward;
        toYou.y = 0f;
        g.transform.position = being.position + toYou.normalized * 8f + Vector3.up * 10f;   // world space: the model may be scaled
        g.transform.SetParent(being, true);
        var l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
    }

    void FacePlayer(Transform t)
    {
        if (playerCamera == null) return;
        Vector3 p = playerCamera.transform.position;
        t.LookAt(new Vector3(p.x, t.position.y, p.z));
    }
}
