using System.Collections;
using System.Collections.Generic;
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
[DefaultExecutionOrder(100)]
public partial class EndingSequence : MonoBehaviour
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
    [TextArea] public string brandLine = "My hands... they're burning again. His mark is opening something.|The real world. I can cross into it now, not just this city.";
    [TextArea] public string boundLine = "He's gone. The fog at the end of the bridge never moved.|Behind me the city's still red. Still full of them, hiding what they did.|...fine. Who's next?";
    public float sinkTime = 5f;             // the king going back down
    public GameObject walker;               // the confessor (injured walk, looping), disabled; limps off toward the city
    public GameObject cursed;               // the same man standing, lifting his hands to look at the marks, disabled: the shot before the walk
    public float cursedSpeed = 0.55f;       // his 3 s clip, slowed
    public float cursedShot = 7f;           // how long the camera stays in front of him, pushing in
    public float hurtsAt = 2.6f;            // when he says it (HS_0651), hands up
    public GameObject titleCard;            // HELLSCAPE / TO BE CONTINUED down the left side, disabled
    public AudioClip endTrack;              // replaces the laugh in this ending: fades in once the king has gone down
    public float endTrackVolume = 0.8f;
    public float walkerSpeed = 0.33f;       // m/s: matched to the clip's stride, so the feet don't skate
    public float walkAwayTime = 16f;       // at least this long, and until the boundLine is done
    public float besideTime = 2.5f;        // the camera stays at his shoulder this long before it starts to pull out

    [Header("The mark: you look at your palms as it burns in")]
    public Texture2D markedSkin;            // the arms' skin, reddened, the Sign of Judgement (Tools/ending/Judgement.png) etched in each palm (Tools/ending/make_marked_fists.py)
    public float handsFov = 42f;

    [Header("First death: the fall in the prologue (plays on waking in hell)")]
    [TextArea] public string deathLines = "The Demon King: Thou didst strike the street, driver, and the street struck back.|The Demon King: Not every ember is mine to quench. Some I brand, and return to the furnace of the living.|The Demon King: Bear my mark. It shall be a lantern in a country that has forgotten thee.";
    [TextArea] public string deathBrandLine = "My hands... the Sign of Judgement, burned into both of them.";

    [Header("Where you stand for it")]
    public Transform standPoint;            // on the deck near the end of the bridge, facing out (empty: wherever you are)

    [Header("Framing the king")]
    public float maxFov = 85f;              // the view widens until all of him fits, head to feet, up to this
    public float headRoom = 1.06f;          // how much taller than him the frame is (his arms go up)

    [Header("The verdict (spoken before the end card)")]
    [TextArea] public string arriveLine = "The bridge just... ends. Red fog, black water, and nothing past it.|The sea's moving. Something's coming UP out of it.";

    [Header("Post-credits call (script 8.5, rows 642-650): seconds")]
    public float callRingSeconds = 5.4f;    // the telephone rings on the black screen this long before it is picked up
    public float callLinePause = 0.9f;      // silence between lines
    public float callRevealPause = 2.6f;    // the longer silence before "Your brother's dead, Derek."
    public float callEndSilence = 2.5f;     // after the line goes dead, before TO BE CONTINUED

    [Header("Testing")]
    public bool testOnStart = false;       // press Play in MainGameScene and the ending runs after 3 s (tick "testDamned" for the bad one)
    public bool testDamned = false;

    private bool played = false, walkedAway = false;
    Being framedBeing;

    void LateUpdate()
    {
        if (framedBeing == null || !Playing || framedBeing.root == null || !framedBeing.root.activeInHierarchy) return;
        FacePlayer(framedBeing.root.transform);
        Frame(framedBeing, 4f);
    }

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
        // V5.1: one judgement, no branching ending (A08_judgement)
        return HS5.S("A08_judgement");
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
        CutsceneHUD.Hide(true);
        foreach (var ghost in FindObjectsByType<GhostNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None)) ghost.CancelConversation();
        if (DialogueBox.Instance != null) DialogueBox.Instance.playerName = "CONFESSOR";   // (what the King calls you: your name plate for the judgement)
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

        bool damned = false;   // V5.1: no branching ending
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
        framedBeing = being;

        if (RevisedPlotDirector.Instance != null)
        {
            yield return RevisedPlotDirector.Instance.PlayerConfession();
        }

        // the verdict: it reads back what you did, and passes sentence
        if (DialogueBox.Instance != null)
        {
            DialogueBox.Instance.SayNow(VerdictLines(damned));
            Sounds.OneShot(being.sound, "Ending/Demon King talking", null);   // his voice; the end track comes in once he's gone
            yield return null;
            while (DialogueBox.Instance.Busy)
            {
                FacePlayer(being.root.transform);
                Frame(being, 4f);
                yield return null;
            }
        }
        framedBeing = null;
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

        // post-credits: the card goes, the screen stays black, the telephone rings. TO BE CONTINUED after it, both endings.
        if (RevisedPlotDirector.Instance != null)
        {
            // the end track is gone before the telephone rings
            if (demonLaugh != null && demonLaugh.isPlaying) yield return FadeOutAudio(demonLaugh, 3f);
            else yield return new WaitForSeconds(2f);
            if (card != null) card.SetActive(false);
            yield return RevisedPlotDirector.Instance.PostCredits(callRingSeconds, callLinePause, callRevealPause, callEndSilence);
            if (damned && continuedCard != null)
            {
                card = continuedCard;
                if (fader != null && fader.transform.parent == card.transform.parent && card.transform.GetSiblingIndex() < fader.transform.GetSiblingIndex())
                    card.transform.SetSiblingIndex(fader.transform.GetSiblingIndex());
            }
            var continued = card != null ? card.transform.Find("Line2") : null;
            if (continued != null) continued.gameObject.SetActive(true);
            if (card != null) card.SetActive(true);
        }

        // Enter: back to the title, as a new game
        var hint = card != null ? card.transform.Find("MenuHint") : null;
        if (hint != null) hint.gameObject.SetActive(true);
        while (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) yield return null;
        var menu = FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
        if (menu != null) menu.ToTitle();
        else { NewGame.Reset(); UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name); }
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
        var arms = Arms();
        bool marked = false;
        for (float t = 0f; t < flare; t += Time.deltaTime)
        {
            float k = t / flare;
            // at the height of the flare (it hides the change) the mark is on your skin
            if (!marked && k >= 0.5f && arms != null && markedSkin != null)
            {
                marked = true;
                arms.material.mainTexture = markedSkin;
                arms.transform.parent.gameObject.SetActive(true);   // (the fists are put away while you carry the rifle)
            }
            if (fader != null) fader.fade.alpha = 0.6f * Mathf.Sin(k * Mathf.PI);
            playerCamera.transform.localPosition = cam0 + Random.insideUnitSphere * 0.07f * (1f - k);
            Frame(king, 4f);
            yield return null;
        }
        playerCamera.transform.localPosition = cam0;
        if (fader != null) fader.fade.alpha = 0f;
        if (img != null) img.color = black;
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(brandLine)) DialogueBox.Instance.SayNow(brandLine);
        if (marked) yield return LookAtHands(arms, 2.2f);
        else for (float w = 0f; w < 2.2f; w += Time.deltaTime) { Frame(king, 4f); yield return null; }

        yield return RevisedPlotDirector.WaitForDialogue();

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
            for (float w = 0f; DialogueBox.Instance.Busy; w += Time.deltaTime) { TurnCamera(city, 2.2f); yield return null; }
        }
        yield return new WaitForSeconds(0.6f);
    }

    SkinnedMeshRenderer Arms()
    {
        var fists = playerCamera != null ? playerCamera.GetComponentInChildren<PunchController>(true) : null;
        return fists != null ? fists.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
    }

    // Your hands come up, palms to you (the arms' Inspect clip), and there it is on each palm: his mark, still glowing.
    IEnumerator LookAtHands(SkinnedMeshRenderer arms, float burn = 1f)
    {
        var anim = arms.GetComponentInParent<Animator>();
        if (anim == null) yield break;
        anim.enabled = true; anim.speed = 1f;
        anim.Play("Inspect", 0, 0f);
        anim.Update(0f);

        var glow = new GameObject("MarkGlow").AddComponent<Light>();
        glow.transform.SetParent(playerCamera.transform, false);
        glow.transform.localPosition = new Vector3(0f, -0.05f, 0.2f);
        glow.type = LightType.Point; glow.color = new Color(1f, 0.55f, 0.45f); glow.range = 1.6f; glow.intensity = 0f; glow.shadows = LightShadows.None;

        float fovWas = playerCamera.fieldOfView, total = anim.GetCurrentAnimatorStateInfo(0).length;
        const float fade = 0.9f;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, Mathf.Min(t, total - t) / fade));
            playerCamera.fieldOfView = Mathf.Lerp(fovWas, handsFov, k);
            glow.intensity = k * burn * (0.28f + 0.1f * Mathf.Sin(Time.time * 9f) + (burn > 1f ? 0.08f * Random.value : 0f));
            yield return null;
        }
        playerCamera.fieldOfView = fovWas;
        Destroy(glow.gameObject);
    }

    // Not damned, the last shot: you (the Meshy walker) head off toward the city while the boundLine plays over it,
    // the camera at his shoulder at first, then slowly pulling back, rising and swinging off toward the skyline;
    // HELLSCAPE fades in on the left with TO BE CONTINUED along the bottom.
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

        cutsceneCam.enabled = true;
        var listener = cutsceneCam.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = true;
        Camera.SetupCurrent(cutsceneCam);

        // first, from in front of him: he stands there breathing, lifts his hands, looks at the marks. "It hurts..."
        // The camera creeps in on his face and hands until they come down again.
        if (cursed != null)
        {
            cursed.transform.SetPositionAndRotation(feet, Quaternion.LookRotation(dir));
            cursed.SetActive(true);
            var anim = cursed.GetComponent<Animator>();
            if (anim != null) anim.speed = cursedSpeed;
            Vector3 chest = feet + Vector3.up * 1.4f;
            Vector3 far = chest + dir * 2.3f + left * 0.8f + Vector3.up * 0.1f, near = chest + dir * 1.55f + left * 0.5f + Vector3.up * 0.1f;
            float fovWas = cutsceneCam.fieldOfView;
            cutsceneCam.fieldOfView = fovWas * 0.72f;
            if (fader != null) StartCoroutine(fader.FadeTo(0f, 1.2f));
            bool said = false;
            for (float t = 0f; t < cursedShot || (DialogueBox.Instance != null && DialogueBox.Instance.Busy && t < cursedShot + 6f); t += Time.deltaTime)
            {
                if (!said && t >= hurtsAt) { said = true; if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(HS.T(651)); }
                cutsceneCam.transform.position = Vector3.Lerp(far, near, Mathf.SmoothStep(0f, 1f, t / cursedShot));
                cutsceneCam.transform.rotation = Quaternion.LookRotation(chest - cutsceneCam.transform.position);
                yield return null;
            }
            cutsceneCam.fieldOfView = fovWas;
            cursed.SetActive(false);   // (cut: behind him now, and he's walking)
        }
        else if (fader != null) StartCoroutine(fader.FadeTo(0f, 1.2f));

        body.SetPositionAndRotation(feet, Quaternion.LookRotation(dir));
        walker.SetActive(true);
        // the camera starts right beside him, at his shoulder, then slowly pulls back and up
        Vector3 beside = left * 1.2f - dir * 1.8f + Vector3.up * 1.5f;
        Vector3 camEnd = feet - dir * 8f + left * 4f + Vector3.up * 3.5f;
        Vector3 skyline = feet + dir * 120f + Vector3.up * 12f;
        float fovEnd = cutsceneCam.fieldOfView, fovStart = fovEnd * 0.72f;
        cutsceneCam.transform.position = feet + beside;
        cutsceneCam.transform.rotation = Quaternion.LookRotation(feet + Vector3.up * 1.3f + dir * 1.5f + left * 0.75f - cutsceneCam.transform.position);

        CanvasGroup titleGroup = null;
        if (titleCard != null)
        {
            if (fader != null && fader.transform.parent == titleCard.transform.parent) titleCard.transform.SetSiblingIndex(fader.transform.GetSiblingIndex());   // over the later blackout
            titleGroup = titleCard.GetComponent<CanvasGroup>();
            if (titleGroup == null) titleGroup = titleCard.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;
            var continued = titleCard.transform.Find("Line2");   // TO BE CONTINUED: not until the telephone call is over
            if (continued != null) continued.gameObject.SetActive(false);
            titleCard.SetActive(true);
        }
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(boundLine)) DialogueBox.Instance.SayNow(boundLine);

        float quietAt = -1f;   // when his last line ended: only then does the title come up
        for (float t = 0f; t < 45f; t += Time.deltaTime)
        {
            bool talking = DialogueBox.Instance != null && DialogueBox.Instance.Busy;
            if (!talking && quietAt < 0f) quietAt = t;
            if (t > walkAwayTime && quietAt >= 0f && t > quietAt + 5f) break;
            body.position += dir * walkerSpeed * Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / walkAwayTime));
            float pull = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - besideTime) / Mathf.Max(0.1f, walkAwayTime - besideTime)));
            cutsceneCam.transform.position = Vector3.Lerp(body.position + beside, camEnd, pull);
            cutsceneCam.fieldOfView = Mathf.Lerp(fovStart, fovEnd, pull);
            // aim left of him so he sits on the right of the frame (less so up close), easing off toward the skyline as he shrinks
            float aside = Mathf.Min(2.5f, 0.35f * Vector3.Distance(cutsceneCam.transform.position, body.position));
            Vector3 aim = Vector3.Lerp(body.position + Vector3.up * 1.3f + dir * 1.5f * (1f - pull) + left * aside, skyline + left * 2.5f, k * 0.3f);
            cutsceneCam.transform.rotation = Quaternion.Slerp(cutsceneCam.transform.rotation, Quaternion.LookRotation(aim - cutsceneCam.transform.position), 1f - Mathf.Exp(-3f * Time.deltaTime));
            if (titleGroup != null) titleGroup.alpha = quietAt < 0f ? 0f : Mathf.Clamp01((t - quietAt) / 2f);
            yield return null;
        }
        yield return RevisedPlotDirector.WaitForDialogue();
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

    AudioSource voidSfx;   // the soul void's own 2D one-shots
    private float kingTop = 12f, kingBottom, fov0 = 60f;   // his height above / below his pivot; your normal view

    // his upper body fills the picture, looking up at him: the view narrows (or widens) until his head (and raised
    // arms) sit at the top edge and his waist at the bottom. His head always wins: if he can't all fit, the waist goes.
    public float waistHeight = 0.55f;       // the lowest part of him in view, as a fraction of his full height
    public float minFov = 30f;              // how tight the view may close on him
    void Frame(Being king, float speed)
    {
        if (playerCamera == null) return;
        Vector3 p = king.root.transform.position, eye = playerCamera.transform.position;
        float flat = Mathf.Max(1f, new Vector2(p.x - eye.x, p.z - eye.z).magnitude);
        float top = Mathf.Atan2(p.y + kingTop * headRoom - eye.y, flat) * Mathf.Rad2Deg + 1.5f;   // (a little sky over his head)
        float bottom = Mathf.Atan2(Mathf.Max(p.y + Mathf.Lerp(kingBottom, kingTop, waistHeight), eye.y - 1.8f) - eye.y, flat) * Mathf.Rad2Deg;   // (nothing to see below the deck)
        float fov = Mathf.Clamp(top - bottom, minFov, maxFov);
        float aim = Mathf.Min(top - fov / 2f, 40f);                      // head at the top edge (TurnCamera never looks higher than 40)
        fov = Mathf.Clamp(Mathf.Max(fov, 2f * (top - aim)), minFov, maxFov);   // looking up as far as it goes: widen so the head stays in
        float k = 1f - Mathf.Exp(-2.5f * Time.deltaTime);
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, fov, k);
        TurnCamera(new Vector3(p.x, eye.y + Mathf.Tan(aim * Mathf.Deg2Rad) * flat, p.z), speed);
    }

    public float kingHeadHeight = 0.88f;    // where his head is, as a fraction of his full height
    public float kingHeadFov = 13f;         // how tight the view on it is

    // the soul-void king is huge: a narrow view on his head, never the rest of him
    void Bust(Being king)
    {
        if (playerCamera == null) return;
        Vector3 p = king.root.transform.position;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, kingHeadFov, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
        TurnCamera(p + Vector3.up * kingTop * kingHeadHeight, 4f);
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

    [Header("First death: the soul void (plays on waking in hell)")]
    public float soulWalkTime = 60f;        // you walk the endless floor this long before it calls STOP
    public float waveEvery = 10f;           // seconds between waves of shadow people (wave n brings n of them)
    public float soulFogDensity = 0.04f;    // black exp-squared fog: half faded at about 21 m
    public float kingFogDensity = 0.012f;   // eased down while he rises, so his shadow can be seen
    public int skyEyes = 500;               // eyes in the sky by the end of the walk (Resources/VoidEyes: the MovingEyes gif as a flipbook)
    public float aheadEvery = 10f;          // metres walked the way you are looking before a shadow person stands in your way
    public GameObject shadeModel;           // the shadow people's body, already dark red with red eyes (HellScape > Void: Meshy Cast); none = a blacked-out zombie
    [Range(0f, 1f)] public float kingWaist = 0.45f;   // how much of him (from the feet) stays under the floor: only his top half rises

    // The first mark, on waking in hell after the prologue fall: you died on that street. You are on SoulFloor, an
    // endless textured floor under a pure black sky and black fog. Shadow people appear ahead of you and follow you
    // (never attacking: get close and it's a jumpscare), more of them every ten seconds, until a voice shouts STOP.
    // Then "???" rises to the waist out of the floor, a huge shadow never shown in colour, shouts, and marks your hands. SleepSequence calls it while the
    // screen is black; you are put back where you were afterwards.
    // the void's voices, far apart (script, act 01): most of the sixty seconds is silence
    IEnumerator VoidWhispers()
    {
        foreach (var rows in new[] { "101-102", "103-104", "105-106" })
        {
            yield return new WaitForSeconds(soulWalkTime * .22f);
            if (DialogueBox.Instance != null) DialogueBox.Instance.Say(HS.S(rows));
        }
    }

    public IEnumerator DeathVision()
    {
        var dream = playerCamera != null ? Resources.Load<DreamHall>("DreamHall") : null;   // the cathedral dream (EndingDream.cs); without it, the old black void
        if (dream != null) { yield return DreamWalk(dream); yield break; }
        if (demon == null || demon.root == null || playerCamera == null) yield break;
        Transform you = playerMovementScript != null ? playerMovementScript.transform : playerCamera.transform.root;
        var body = you.GetComponent<Rigidbody>();
        bool wasKinematic = body != null && body.isKinematic;
        Vector3 pos = you.position; Quaternion rot = you.rotation;
        float fovWas = playerCamera.fieldOfView;
        // what the void changes (all put back at the end)
        bool fogWas = RenderSettings.fog; Color fogColorWas = RenderSettings.fogColor;
        FogMode fogModeWas = RenderSettings.fogMode; float fogDensityWas = RenderSettings.fogDensity;
        Material skyWas = RenderSettings.skybox;
        var ambientModeWas = RenderSettings.ambientMode; Color ambientWas = RenderSettings.ambientLight;
        var clearWas = playerCamera.clearFlags; Color backgroundWas = playerCamera.backgroundColor;
        fov0 = fovWas;
        // the void: no hands, no HUD, no footsteps (all back once the King is done with you)
        bool hudWasHidden = CutsceneHUD.Hidden; CutsceneHUD.Hide(true);
        var armsRig = Arms() != null ? Arms().transform.parent.gameObject : null;
        if (armsRig != null) armsRig.SetActive(false);
        var steps = you.GetComponentInChildren<PlayerFootStep>(); bool stepsWere = steps != null && steps.enabled;
        if (steps != null) steps.enabled = false;
        MusicManager.Stop(0.5f);
        if (body != null) { body.linearVelocity = Vector3.zero; body.isKinematic = true; }
        you.SetPositionAndRotation(new Vector3(0f, -3000f, 0f), Quaternion.identity);
        playerCamera.transform.localRotation = Quaternion.identity;
        Physics.SyncTransforms();
        yield return null;   // (the hell fog follows you: let it arrive, then put it away)
        var mist = new List<GameObject>();
        foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            if (ps.gameObject.activeInHierarchy && Vector3.Distance(ps.transform.position, playerCamera.transform.position) < 150f)
            { mist.Add(ps.gameObject); ps.gameObject.SetActive(false); }

        // pure black sky, black fog
        playerCamera.clearFlags = CameraClearFlags.SolidColor; playerCamera.backgroundColor = Color.black;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = Color.black;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.black; RenderSettings.fogDensity = soulFogDensity;

        // SoulFloor: one big textured plane that slides along under you in whole tiles, so it never ends
        float feet = 1f, lowest = float.MaxValue;
        foreach (var c in you.GetComponentsInChildren<Collider>()) if (!c.isTrigger) lowest = Mathf.Min(lowest, c.bounds.min.y);
        if (lowest < float.MaxValue) feet = you.position.y - lowest;
        float floorY = you.position.y - feet - 0.01f;
        const float size = 600f, tile = 6f;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "SoulFloor";
        DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.localScale = Vector3.one * (size / 10f);
        var box = floor.AddComponent<BoxCollider>(); box.size = new Vector3(10f, 0.2f, 10f); box.center = new Vector3(0f, -0.1f, 0f);
        var floorMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        var floorTex = Resources.Load<Texture2D>("SoulFloor");
        if (floorTex != null) { floorTex.wrapMode = TextureWrapMode.Repeat; floorMat.mainTexture = floorTex; floorMat.mainTextureScale = new Vector2(size / tile, size / tile); }
        floorMat.color = new Color(0.6f, 0.6f, 0.6f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;
        floor.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        floor.transform.position = new Vector3(0f, floorY, 0f);
        you.position = new Vector3(0f, floorY + feet + 0.05f, 0f);
        if (body != null) { body.isKinematic = false; body.linearVelocity = Vector3.zero; }
        Physics.SyncTransforms();

        var shadeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); shadeMat.color = new Color(0.22f, 0.015f, 0.015f);   // slightly red
        var template = MakeShadeTemplate(shadeMat);
        // whispers and a tense bed, both fading in over the whole walk and swelling as the shadows gather
        AudioSource Bed(string slot, float vol) { var a = new GameObject(slot).AddComponent<AudioSource>(); a.spatialBlend = 0f; a.loop = true; a.volume = 0f; a.clip = Sounds.Clip(slot, null); a.ignoreListenerPause = true; if (a.clip != null) a.Play(); return a; }
        voidSfx = Bed("Void sfx", 0f); voidSfx.loop = false; voidSfx.volume = 1f;   // (the king's own source is switched off until he rises)
        var whispers = Bed("Ending/Shadow whispers", 0f); var bed = Bed("Ending/Shadow music", 0f);
        float whisperVol = Sounds.Volume("Ending/Shadow whispers"), bedVol = Sounds.Volume("Ending/Shadow music");
        var shades = new List<SoulFollower>();
        var king = demon;
        var eyes = MakeSkyEyes(out var eyesMat);
        Vector3 lastPos = you.position; float walked = 0f;

        // walk: the endless floor and the people who follow you
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 1.5f));
        if (playerMovementScript != null) playerMovementScript.enabled = true;
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow(HS.S("98-100"));
        StartCoroutine(VoidWhispers());
        float clock = 0f, nextWave = 3f; int wave = 0; bool scared = false;
        while (clock < soulWalkTime)
        {
            clock += Time.deltaTime;
            float swell = Mathf.Clamp01(clock / soulWalkTime);
            bed.volume = 0.7f * bedVol * swell * swell;                       // very slow fade in, then fear
            whispers.volume = wave > 0 ? 0.9f * whisperVol * Mathf.Lerp(0.1f, 1f, swell) : 0f;   // from the first shadow, louder as it goes
            Vector3 p = you.position;
            floor.transform.position = new Vector3(Mathf.Round(p.x / tile) * tile, floorY, Mathf.Round(p.z / tile) * tile);
            if (eyes != null) eyes.transform.position = playerCamera.transform.position;   // a sky: it goes where you go
            // keep walking the way you look and one of them is standing there: turn, or walk into it
            Vector3 look = playerCamera.transform.forward; look.y = 0f;
            walked += Mathf.Max(0f, Vector3.Dot(p - lastPos, look.normalized)); lastPos = p;
            if (walked >= aheadEvery)
            {
                walked = 0f;
                if (shades.Count < 22) shades.Add(SpawnShade(template, you, floorY, 1.6f, 12f, 9f, 13f));
                else
                {
                    // (all of them are out: the one furthest behind in the fog is the one in front of you now)
                    SoulFollower far = null; float best = 0f;
                    foreach (var s in shades) if (s != null && (s.transform.position - p).sqrMagnitude > best) { best = (s.transform.position - p).sqrMagnitude; far = s; }
                    if (far != null) far.transform.position = AheadSpot(you, floorY, 12f, 9f, 13f, out _);
                }
                voidSfx.PlayOneShot(ProceduralAudio.Rumble(.8f), 0.2f);
            }
            if (clock >= nextWave)
            {
                nextWave += waveEvery; wave++;
                for (int i = 0; i < wave && shades.Count < 22; i++) shades.Add(SpawnShade(template, you, floorY, 1.1f + 0.05f * wave + Random.Range(0f, 0.2f)));
                voidSfx.PlayOneShot(ProceduralAudio.Rumble(1.2f), 0.3f);
            }
            foreach (var s in shades)
                if (s != null && s.caught) { yield return SoulScare(s, shades, you, floorY); break; }
            // halfway through, one of them breaks into a dead sprint at you: no time to react
            if (!scared && clock > soulWalkTime * 0.5f)
            {
                scared = true;
                var runner = SpawnShade(template, you, floorY, 20f);
                // (Derek's own run if the shade has one; otherwise the walk, sped up)
                bool runs = runner.animator != null && runner.animator.HasState(0, Animator.StringToHash("Run"));
                if (runs) runner.animator.Play("Run", 0, 0f);
                runner.animSpeed = runs ? 1.6f : 2.8f; shades.Add(runner);
            }
            yield return null;
        }

        // STOP: the Demon King comes, and the whispers and the music cut out
        Destroy(whispers.gameObject); Destroy(bed.gameObject);
        if (playerMovementScript != null) playerMovementScript.enabled = false;
        if (body != null) body.linearVelocity = Vector3.zero;
        foreach (var s in shades) if (s != null) s.frozen = true;
        Sounds.OneShot(voidSfx, "Ending/Demon rises", null, 1f);
        if (DialogueBox.Instance != null)
        {
            DialogueBox.Instance.SayNow(HS.S("107"));
            Sounds.OneShot(voidSfx, "Ending/Demon King talking", null);
            yield return null;
            Vector3 cam0 = playerCamera.transform.localPosition;
            for (float t = 0f; DialogueBox.Instance.Busy; t += Time.deltaTime)
            {
                playerCamera.transform.localPosition = cam0 + Random.insideUnitSphere * 0.04f;
                yield return null;
            }
            playerCamera.transform.localPosition = cam0;
        }

        // size him first (his pivot is at his feet), then he rises out of the floor ahead of you, only to the waist (the
        // floor hides the rest of him): a shadow, no colour, nothing of him given away
        king.root.SetActive(true);
        Vector3 kingScale0 = king.root.transform.localScale;
        king.root.transform.localScale = kingScale0 * 4f;   // huge
        var rends = king.root.GetComponentsInChildren<Renderer>();   // (only the body that's switched on: the old one is still in there)
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            kingTop = b.max.y - king.root.transform.position.y;
            kingBottom = b.min.y - king.root.transform.position.y;
        }
        var black = new Material(Shader.Find("Universal Render Pipeline/Unlit")); black.color = Color.black;
        var swapped = new List<(Renderer r, Material[] mats)>();
        var hidden = new List<Renderer>();
        foreach (var r in rends)
        {
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
            {
                swapped.Add((r, r.sharedMaterials));
                var m = new Material[r.sharedMaterials.Length]; for (int i = 0; i < m.Length; i++) m[i] = black;
                r.sharedMaterials = m;
            }
            else if (r.enabled) { r.enabled = false; hidden.Add(r); }
        }
        var lights = new List<Light>();
        foreach (var l in king.root.GetComponentsInChildren<Light>(true)) if (l.enabled) { l.enabled = false; lights.Add(l); }

        Vector3 ahead = playerCamera.transform.forward; ahead.y = 0f;
        ahead = ahead.sqrMagnitude > 0.01f ? ahead.normalized : Vector3.forward;
        float dist = kingTop * 0.85f;   // (the view can't tilt past 40 degrees: his head has to stay in it)
        Vector3 standAt = new Vector3(you.position.x, floorY, you.position.z) + ahead * dist + Vector3.down * kingTop * kingWaist;
        Vector3 sunk = standAt + Vector3.down * (kingTop - kingBottom + 2f);
        king.root.transform.position = sunk;
        FacePlayer(king.root.transform);

        // a dim pale haze behind him, or a black shadow on a black sky would be invisible
        var haze = GameObject.CreatePrimitive(PrimitiveType.Quad);
        DestroyImmediate(haze.GetComponent<Collider>());
        haze.name = "Soul haze";
        // (well behind him: his cape and spines reach tens of metres back, and would poke through a nearer one)
        Vector3 hazeAt = standAt + ahead * kingTop * .8f + Vector3.up * kingTop * kingHeadHeight;
        haze.transform.position = hazeAt;
        haze.transform.rotation = Quaternion.LookRotation(hazeAt - playerCamera.transform.position);
        haze.transform.localScale = Vector3.one * kingTop * 6f;
        var hazeTex = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = Mathf.Clamp01(new Vector2(x - 63.5f, y - 63.5f).magnitude / 64f);
                hazeTex.SetPixel(x, y, new Color(.9f, .025f, .025f, Mathf.Pow(1f - d, 1.6f)));
            }
        hazeTex.Apply();
        var hazeShader = Shader.Find("Sprites/Default");
        var hazeMat = new Material(hazeShader != null ? hazeShader : Shader.Find("Universal Render Pipeline/Unlit")) { mainTexture = hazeTex };
        haze.GetComponent<Renderer>().sharedMaterial = hazeMat;
        haze.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Sounds.OneShot(king.sound, "Ending/Demon rises", null, 0.8f);
        const float rise = 4f;
        for (float t = 0f; t < rise; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / rise);
            king.root.transform.position = Vector3.Lerp(sunk, standAt, k);
            if (eyesMat != null) eyesMat.SetFloat("_Dim", Mathf.Lerp(1f, 0.35f, k));   // the sky stays, behind him, but his haze has to read
            RenderSettings.fogDensity = Mathf.Lerp(soulFogDensity, kingFogDensity, k);
            FacePlayer(king.root.transform);
            Bust(king);
            yield return null;
        }
        // up: the shout (then he idles)
        if (king.animator != null) king.animator.SetTrigger(king.animTrigger);
        Sounds.OneShot(king.sound, "Ending/Demon rises", null, 1f);
        for (float t = 0f; t < 4f; t += Time.deltaTime) { Bust(king); yield return null; }
        if (DialogueBox.Instance != null && !string.IsNullOrEmpty(deathLines))
        {
            DialogueBox.Instance.SayNow(deathLines.Replace("The Demon King:", "???:"));   // (his name is not given yet)
            Sounds.OneShot(king.sound, "Ending/Demon King talking", null);
            yield return null;
            while (DialogueBox.Instance.Busy) { FacePlayer(king.root.transform); Bust(king); yield return null; }
        }

        // the brand: red flare, then the hands
        var arms = Arms();
        if (arms != null && markedSkin != null)
        {
            Sounds.OneShot(king.sound, "Ending/Demon rises", null, 0.8f);
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                float k = t / 1.1f;
                if (k >= 0.5f && arms.material.mainTexture != markedSkin)
                {
                    arms.material.mainTexture = markedSkin;
                    arms.transform.parent.gameObject.SetActive(true);
                }
                if (fader != null) fader.fade.alpha = 0.6f * Mathf.Sin(k * Mathf.PI);
                Bust(king);
                yield return null;
            }
            if (fader != null) fader.fade.alpha = 0f;
            // the fists are held down at your sides after the fall: bring them up into the guard to look at them
            var punch = playerCamera.GetComponentInChildren<PunchController>(true);
            if (punch != null) { punch.Draw(); yield return new WaitForSeconds(1.2f); }
            if (DialogueBox.Instance != null && !string.IsNullOrEmpty(deathBrandLine)) DialogueBox.Instance.SayNow(deathBrandLine.Replace("The Demon King:", "???:"));
            yield return LookAtHands(arms, 0.8f);
            yield return RevisedPlotDirector.WaitForDialogue();
        }

        if (fader != null) yield return fader.FadeTo(1f, 1.2f);
        foreach (var s in shades) if (s != null) Destroy(s.gameObject);
        Destroy(voidSfx.gameObject);
        Destroy(template); Destroy(floor); Destroy(haze);
        if (eyes != null) Destroy(eyes.gameObject); if (eyesMat != null) Destroy(eyesMat);
        Destroy(floorMat); Destroy(black); Destroy(hazeMat); Destroy(hazeTex);
        foreach (var (r, mats) in swapped) if (r != null) r.sharedMaterials = mats;
        foreach (var r in hidden) if (r != null) r.enabled = true;
        foreach (var l in lights) if (l != null) l.enabled = true;
        king.root.transform.localScale = kingScale0;
        king.root.SetActive(false);
        if (!hudWasHidden) CutsceneHUD.Hide(false);
        if (armsRig != null) armsRig.SetActive(true);
        if (steps != null) steps.enabled = stepsWere;
        foreach (var g in mist) if (g != null) g.SetActive(true);
        RenderSettings.fog = fogWas; RenderSettings.fogColor = fogColorWas;
        RenderSettings.fogMode = fogModeWas; RenderSettings.fogDensity = fogDensityWas;
        RenderSettings.skybox = skyWas;
        RenderSettings.ambientMode = ambientModeWas; RenderSettings.ambientLight = ambientWas;
        playerCamera.clearFlags = clearWas; playerCamera.backgroundColor = backgroundWas;
        playerCamera.fieldOfView = fovWas;
        playerCamera.transform.localRotation = Quaternion.identity;
        if (body != null) { body.linearVelocity = Vector3.zero; body.isKinematic = true; }
        you.SetPositionAndRotation(pos, rot);
        if (body != null) body.isKinematic = wasKinematic;
        Physics.SyncTransforms();
    }

    // a black copy of a zombie (no scripts, no physics) to stand in as a shadow person; plain primitives if there's none
    GameObject MakeShadeTemplate(Material black)
    {
        GameObject t;
        if (shadeModel != null)
        {
            t = Instantiate(shadeModel);   // (its own walk and its own dark red look, eyes and all)
            t.name = "Soul shade";
            t.SetActive(false);
            return t;
        }
        var z = (Component)FindFirstObjectByType<CivilianWander>(FindObjectsInactive.Include) ?? FindFirstObjectByType<ZombieAI>(FindObjectsInactive.Include);
        if (z != null)
        {
            t = Instantiate(z.gameObject);
            foreach (var c in t.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(c);   // (scripts first: they may require the rest)
            foreach (var c in t.GetComponentsInChildren<CharacterJoint>(true)) DestroyImmediate(c);
            foreach (var c in t.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(c);
            foreach (var c in t.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var c in t.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) DestroyImmediate(c);
            foreach (var c in t.GetComponentsInChildren<Light>(true)) DestroyImmediate(c);
            foreach (var c in t.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(c);
        }
        else
        {
            t = new GameObject("Shade");
            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule); torso.transform.SetParent(t.transform, false);
            torso.transform.localPosition = Vector3.up * 0.95f; torso.transform.localScale = new Vector3(0.55f, 0.95f, 0.4f);
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere); head.transform.SetParent(t.transform, false);
            head.transform.localPosition = Vector3.up * 2.05f; head.transform.localScale = Vector3.one * 0.38f;
            foreach (var c in t.GetComponentsInChildren<Collider>()) DestroyImmediate(c);
        }
        t.name = "Soul shade";
        var walk = Resources.Load<RuntimeAnimatorController>("OfficeWalk");   // the plain walk, not the zombie shuffle
        var ta = t.GetComponentInChildren<Animator>(true);
        if (ta != null && walk != null) ta.runtimeAnimatorController = walk;
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        {
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
            {
                var m = new Material[r.sharedMaterials.Length]; for (int i = 0; i < m.Length; i++) m[i] = black;
                r.sharedMaterials = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else r.enabled = false;
        }
        t.SetActive(false);
        return t;
    }

    // The sky of the void fills with eyes: one, then a few, then faster and faster until the whole dome is looking at
    // you. Billboard particles on a hemisphere round the camera, each playing the flipbook at its own pace
    // (Resources/VoidEyes.png, 8 x 6 frames, drawn by HellScape/VoidEyes as a sky behind everything).
    ParticleSystem MakeSkyEyes(out Material mat)
    {
        mat = null;
        var tex = Resources.Load<Texture2D>("VoidEyes"); var shader = Shader.Find("HellScape/VoidEyes");
        if (tex == null || shader == null) return null;
        mat = new Material(shader) { mainTexture = tex };
        var go = new GameObject("Void eyes");
        go.transform.position = playerCamera.transform.position;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        const float life = 3600f, loop = 3.72f;   // (they never die; the gif is 3.72 s long)
        var main = ps.main;
        main.duration = soulWalkTime; main.loop = false; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * .8f, life * 1.2f);   // (the flipbook runs over the lifetime: no two at the same pace)
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(3f, 7.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-.3f, .3f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.45f, .45f, .45f), Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = skyEyes + 50;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 30f; shape.radiusThickness = 0f;
        shape.rotation = new Vector3(-90f, 0f, 0f);   // the dome, over your head
        var em = ps.emission;   // slow, then a flood: the area under this curve is skyEyes
        float peak = (skyEyes / soulWalkTime - .5f) * 3f;
        em.rateOverTime = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .5f, 0f, 0f), new Keyframe(1f, .5f + peak, 2f * peak, 0f)));
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true; sheet.numTilesX = 8; sheet.numTilesY = 6;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);
        sheet.cycleCount = Mathf.RoundToInt(life / loop);
        var col = ps.colorOverLifetime;   // each one opens out of the black
        col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 2.5f / life), new GradientAlphaKey(1f, 1f) });
        col.color = fade;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    // a place on the floor ahead of where you're looking, within `spread` degrees and `near`..`far` metres
    Vector3 AheadSpot(Transform you, float floorY, float spread, float near, float far, out Vector3 dir)
    {
        Vector3 fwd = playerCamera.transform.forward; fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
        dir = Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * fwd;
        Vector3 at = you.position + dir * Random.Range(near, far); at.y = floorY;
        return at;
    }

    // one shadow person, 15 to 24 m ahead of where you're looking (or as given), walking at you
    SoulFollower SpawnShade(GameObject template, Transform you, float floorY, float speed, float spread = 60f, float near = 15f, float far = 24f)
    {
        Vector3 at = AheadSpot(you, floorY, spread, near, far, out var dir);
        var g = Instantiate(template, at, Quaternion.LookRotation(-dir));
        g.SetActive(true);
        g.transform.localScale = template.transform.localScale * Random.Range(0.95f, 1.12f);
        var f = g.AddComponent<SoulFollower>();
        f.target = you; f.speed = speed;
        f.animator = g.GetComponentInChildren<Animator>();
        if (f.animator != null) f.animator.applyRootMotion = false;
        return f;
    }

    // one got to you: it's suddenly right in your face, a flash, and they're all gone. (The count keeps climbing.)
    // a full-screen red vignette (the HUD's own is hidden in the void): clear in the middle, blood red at the edges
    CanvasGroup RedVignette()
    {
        var canvas = new GameObject("Scare vignette").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var group = canvas.gameObject.AddComponent<CanvasGroup>();
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
                tex.SetPixel(x, y, new Color(.7f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, 1.2f, d))));
            }
        tex.Apply();
        var img = new GameObject("Red edges").AddComponent<UnityEngine.UI.RawImage>();
        img.transform.SetParent(canvas.transform, false);
        img.texture = tex; img.raycastTarget = false;
        var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return group;
    }

    IEnumerator SoulScare(SoulFollower s, List<SoulFollower> shades, Transform you, float floorY)
    {
        if (playerMovementScript != null) playerMovementScript.enabled = false;
        var body = you.GetComponent<Rigidbody>();
        if (body != null) body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
        foreach (var o in shades) if (o != null) o.frozen = true;
        Transform cam = playerCamera.transform;
        Vector3 to = s.transform.position - cam.position; to.y = 0f;
        Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : cam.forward;
        s.transform.position = new Vector3(cam.position.x, floorY, cam.position.z) + dir * 0.6f;
        s.transform.rotation = Quaternion.LookRotation(-dir);
        Physics.SyncTransforms();
        Sounds.OneShot(voidSfx, "Ending/Shadow scream", null, 1f);
        Sounds.OneShot(voidSfx, "Office/Vane jumpscare", ProceduralAudio.Whisper(1.4f), 0.6f);
        voidSfx.PlayOneShot(ProceduralAudio.Stomp(), 1f);
        // right in your face: zoomed in on his head, a black silhouette staring at you with two red dots for eyes,
        // the edges of the screen gone red and the picture shaking
        s.gameObject.AddComponent<ShadeStare>().target = cam;
        Transform head = null;
        foreach (var b in s.GetComponentsInChildren<Transform>()) if (b.name == "Head" || b.name.EndsWith(":Head")) head = b;
        playerCamera.fieldOfView = 30f;   // zoomed in: his face fills the picture
        var vignette = RedVignette();
        Vector3 cam0 = cam.localPosition;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            TurnCamera(head != null ? head.position : s.transform.position + Vector3.up * 1.65f, 20f);
            cam.localPosition = cam0 + Random.insideUnitSphere * 0.035f;
            cam.rotation *= Quaternion.Euler(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f), Random.Range(-4f, 4f));
            vignette.alpha = 0.75f + 0.25f * Mathf.Sin(t * 40f);
            yield return null;
        }
        Destroy(vignette.GetComponentInChildren<UnityEngine.UI.RawImage>().texture); Destroy(vignette.gameObject);
        cam.localPosition = cam0;
        if (fader != null) yield return fader.FadeTo(1f, 0.12f);
        foreach (var o in shades) if (o != null) Destroy(o.gameObject);
        shades.Clear();
        playerCamera.fieldOfView = fov0;
        yield return new WaitForSeconds(0.5f);
        if (fader != null) yield return fader.FadeTo(0f, 0.8f);
        if (playerMovementScript != null) playerMovementScript.enabled = true;
    }
}
