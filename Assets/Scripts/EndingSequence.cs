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
    [TextArea] public string brandLine = "My hands... they're burning again. His mark is opening something.|The real world. I can cross into it now, not just this city.";
    [TextArea] public string boundLine = "He's gone. The fog at the end of the bridge never moved.|Behind me the city's still red. Still full of them, hiding what they did.|...fine. Who's next?";
    public float sinkTime = 5f;             // the king going back down
    public GameObject walker;               // the Meshy character (walk animation, looping), disabled; walks off toward the city
    public GameObject titleCard;            // HELLSCAPE / TO BE CONTINUED down the left side, disabled
    public AudioClip endTrack;              // replaces the laugh in this ending: fades in once the king has gone down
    public float endTrackVolume = 0.8f;
    public float walkerSpeed = 0.5f;        // m/s: the clip's own stride (2.27 m per 4.97 s loop), so the feet don't skate
    public float walkAwayTime = 16f;       // at least this long, and until the boundLine is done
    public float besideTime = 2.5f;        // the camera stays at his shoulder this long before it starts to pull out

    [Header("The mark: you look down at your hands as it burns in")]
    public Texture2D markedSkin;            // the arms' skin, reddened, the Sign of Judgement (Assets/New Folder/Judgement.png) etched in the back of each hand (Tools/ending/make_marked_arms.py)
    public float handsHold = 3f;            // how long you stare at them
    public float handsPitch = 30f;          // looking this far down
    public float handsFov = 42f;
    public float handsRaise = 0.12f;        // the fists come up into the picture
    public Vector2 handsTwist = new Vector2(130f, -135f);   // forearms turned over (right, left): the backs of the fists face you

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
        l.Add(who + (damned ? "Still thou wearest thy own skin, and still thou pretendest. Let us weigh what thou hast carried."
                            : "Still thou wearest thy own skin. Keep it. I have a use for the vessel."));
        l.Add(who + "Thou didst sign a murder into a fall, and never asked what thine eyes had seen.");
        if (EvidenceJournal.ContainerInspected) l.Add(who + "A sealed weight was put in thy hands. Thou didst feel it, and carried it still.");
        if (Judgement.KilledKiller) l.Add(who + "Elias Rourke: thou hast ended him. A brother, avenging a brother, ended by thee.");
        else if (Judgement.SparedKiller) l.Add(who + "Elias Rourke: thou didst let him speak. He named his crime, and the one he did it for.");
        var turned = Survivor.All.Where(x => x != null && x.Turned).Select(x => x.displayName).ToList();
        if (turned.Count > 0) l.Add(who + string.Join(" and ", turned) + " clung to the excuse, and the excuse became the flesh. To speak is not to confess.");
        var saved = Survivor.All.Where(x => x != null && x.Rescued).Select(x => x.displayName).ToList();
        var lost = Survivor.All.Where(x => x != null && !x.Rescued && !x.Turned && !x.Drowned).Select(x => x.displayName).ToList();
        var drowned = Survivor.All.Where(x => x != null && !x.Rescued && !x.Turned && x.Drowned).Select(x => x.displayName).ToList();
        if (saved.Count > 0) l.Add(who + "Thou didst bear " + string.Join(" and ", saved) + " out of the red tide.");
        if (lost.Count > 0) l.Add(who + "Thou didst leave " + string.Join(" and ", lost) + " to the dark.");
        if (drowned.Count > 0) l.Add(who + "The flood took " + string.Join(" and ", drowned) + ". Their reckoning stays open.");
        if (Judgement.Karma >= 2) l.Add(who + "Thou wert gentle with the dead when gentleness cost thee nothing.");
        else if (Judgement.Karma <= -2) l.Add(who + "Thou wert cruel to the dead. The dead keep ledgers.");
        if (damned)
        {
            l.Add(who + (EvidenceJournal.PlayerConfessed ? "Thou hast spoken thy guilt aloud. Now the sentence: thou shalt wear the shape thy deeds have made." : "Thou still hidest behind another's name. Then my mark will cease to hold thy shape."));
            return string.Join("|", l);
        }
        l.Add(who + "Confession opens a hearing. It does not wash the blood. Thy sentence is to drag the hidden accounts into the light.");
        l.Add(who + "I see the sins of the dead as one sees stars. The living hide from me. Kessler yet breathes.");
        l.Add(who + "Vane gave up his own sins, and no man's but his own. Seek Kessler through the Echo.");
        l.Add(who + "Tomas armed a brother, then bound the dead to himself to storm my gate.");
        l.Add(who + "Thou hast broken the body he built. Tomas, Vane, Maya: each answers alone. To be wronged is not to own another's soul.");
        l.Add(who + "The Damned may yet confess, but the road narrows with every step away. Do not mistake ruin for judgement.");
        if (saved.Count > 0) l.Add(who + "Those thou broughtest receive their own hearing. They do not share thy sentence.");
        l.Add(who + "My mark is my heartbeat, and thou art its drum. It beats until the work is done.");
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

        if (RevisedPlotDirector.Instance != null)
        {
            yield return RevisedPlotDirector.Instance.PlayerConfession();
            damned = damned || !EvidenceJournal.PlayerConfessed;
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

        if (RevisedPlotDirector.Instance != null)
        {
            yield return new WaitForSeconds(2f);
            if (card != null) card.SetActive(false);
            yield return RevisedPlotDirector.Instance.PostCredits();
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

    // You look down, your fists come up and turn over, and there it is on the back of each hand: his mark, still
    // glowing. The arms' animator is stopped for it (the forearms are turned by hand) and started again after.
    IEnumerator LookAtHands(SkinnedMeshRenderer arms, float burn = 1f)
    {
        Transform cam = playerCamera.transform, rig = arms.transform.parent;
        var anim = rig.GetComponentInParent<Animator>();
        if (anim != null) { anim.Update(2f); anim.enabled = false; }   // (past any draw: settled in the guard)
        var fore = new Transform[2]; var rot0 = new Quaternion[2]; var axis = new Vector3[2];
        string[] side = { "R", "L" };
        foreach (var b in arms.bones)
            for (int s = 0; s < 2; s++)
            {
                if (b == null || b.name != "forearm." + side[s]) continue;
                fore[s] = b; rot0[s] = b.localRotation;
                Transform hand = b.Find("hand." + side[s]);
                Vector3 along = hand != null ? hand.position - b.position : b.up;
                axis[s] = b.parent.InverseTransformDirection(along.normalized);   // (in the parent's space: the camera turns under it)
            }

        var glow = new GameObject("MarkGlow").AddComponent<Light>();
        glow.transform.SetParent(cam, false);
        glow.transform.localPosition = new Vector3(0f, 0.05f, 0.25f);
        glow.type = LightType.Point; glow.color = new Color(1f, 0.55f, 0.45f); glow.range = 1.6f; glow.intensity = 0f; glow.shadows = LightShadows.None;

        Vector3 pos0 = rig.localPosition;
        float fovWas = playerCamera.fieldOfView, pitch0 = cam.eulerAngles.x, yaw = cam.eulerAngles.y;
        const float turn = 0.9f, back = 0.7f;
        float total = turn + handsHold + back;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            float k = t < turn ? t / turn : t > total - back ? (total - t) / back : 1f;
            k = Mathf.SmoothStep(0f, 1f, k);
            cam.rotation = Quaternion.Euler(Mathf.LerpAngle(pitch0, handsPitch, k), yaw, 0f);
            playerCamera.fieldOfView = Mathf.Lerp(fovWas, handsFov, k);
            rig.localPosition = pos0 + Vector3.up * handsRaise * k + Random.insideUnitSphere * 0.004f * k;   // they're shaking
            for (int s = 0; s < 2; s++)
                if (fore[s] != null) fore[s].localRotation = Quaternion.AngleAxis(handsTwist[s] * k, axis[s]) * rot0[s];
            glow.intensity = k * burn * (0.28f + 0.1f * Mathf.Sin(Time.time * 9f) + (burn > 1f ? 0.08f * Random.value : 0f));
            yield return null;
        }
        cam.rotation = Quaternion.Euler(pitch0, yaw, 0f);
        playerCamera.fieldOfView = fovWas;
        rig.localPosition = pos0;
        for (int s = 0; s < 2; s++) if (fore[s] != null) fore[s].localRotation = rot0[s];
        Destroy(glow.gameObject);
        if (anim != null) anim.enabled = true;
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
        body.SetPositionAndRotation(feet, Quaternion.LookRotation(dir));
        walker.SetActive(true);

        cutsceneCam.enabled = true;
        var listener = cutsceneCam.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = true;
        Camera.SetupCurrent(cutsceneCam);
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
            float pull = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - besideTime) / Mathf.Max(0.1f, walkAwayTime - besideTime)));
            cutsceneCam.transform.position = Vector3.Lerp(body.position + beside, camEnd, pull);
            cutsceneCam.fieldOfView = Mathf.Lerp(fovStart, fovEnd, pull);
            // aim left of him so he sits on the right of the frame (less so up close), easing off toward the skyline as he shrinks
            float aside = Mathf.Min(2.5f, 0.35f * Vector3.Distance(cutsceneCam.transform.position, body.position));
            Vector3 aim = Vector3.Lerp(body.position + Vector3.up * 1.3f + dir * 1.5f * (1f - pull) + left * aside, skyline + left * 2.5f, k * 0.3f);
            cutsceneCam.transform.rotation = Quaternion.Slerp(cutsceneCam.transform.rotation, Quaternion.LookRotation(aim - cutsceneCam.transform.position), 1f - Mathf.Exp(-3f * Time.deltaTime));
            if (titleGroup != null) titleGroup.alpha = Mathf.Clamp01((t - 3f) / 2f);
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

    // The first mark, on waking in hell after the prologue fall: you died on that street. You are on SoulFloor, an
    // endless textured floor under a pure black sky and black fog. Shadow people appear ahead of you and follow you
    // (never attacking: get close and it's a jumpscare), more of them every ten seconds, until a voice shouts STOP.
    // Then "???" rises: a huge shadow, never shown in colour, and marks your hands. SleepSequence calls it while the
    // screen is black; you are put back where you were afterwards.
    public IEnumerator DeathVision()
    {
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

        var black = new Material(Shader.Find("Universal Render Pipeline/Unlit")); black.color = Color.black;
        var shadeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); shadeMat.color = new Color(0.22f, 0.015f, 0.015f);   // slightly red
        var template = MakeShadeTemplate(shadeMat);
        // whispers and a tense bed, both fading in over the whole walk and swelling as the shadows gather
        AudioSource Bed(string slot, float vol) { var a = new GameObject(slot).AddComponent<AudioSource>(); a.spatialBlend = 0f; a.loop = true; a.volume = 0f; a.clip = Sounds.Clip(slot, null); a.ignoreListenerPause = true; if (a.clip != null) a.Play(); return a; }
        voidSfx = Bed("Void sfx", 0f); voidSfx.loop = false; voidSfx.volume = 1f;   // (the king's own source is switched off until he rises)
        var whispers = Bed("Ending/Shadow whispers", 0f); var bed = Bed("Ending/Shadow music", 0f);
        float whisperVol = Sounds.Volume("Ending/Shadow whispers"), bedVol = Sounds.Volume("Ending/Shadow music");
        var shades = new List<SoulFollower>();
        var king = demon;

        // walk: the endless floor and the people who follow you
        if (fader != null) StartCoroutine(fader.FadeTo(0f, 1.5f));
        if (playerMovementScript != null) playerMovementScript.enabled = true;
        if (DialogueBox.Instance != null) DialogueBox.Instance.SayNow("Where... is this?|Nothing. Just the floor, and the dark.");
        float clock = 0f, nextWave = 3f; int wave = 0; bool scared = false;
        while (clock < soulWalkTime)
        {
            clock += Time.deltaTime;
            float swell = Mathf.Clamp01(clock / soulWalkTime);
            bed.volume = 0.7f * bedVol * swell * swell;                       // very slow fade in, then fear
            whispers.volume = wave > 0 ? 0.9f * whisperVol * Mathf.Lerp(0.1f, 1f, swell) : 0f;   // from the first shadow, louder as it goes
            Vector3 p = you.position;
            floor.transform.position = new Vector3(Mathf.Round(p.x / tile) * tile, floorY, Mathf.Round(p.z / tile) * tile);
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
                runner.animSpeed = 2.8f; shades.Add(runner);
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
            DialogueBox.Instance.SayNow("???: STOP!");
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

        // size him first (his pivot is at his feet), then he rises out of the floor ahead of you: a shadow, no colour
        king.root.SetActive(true);
        Vector3 kingScale0 = king.root.transform.localScale;
        king.root.transform.localScale = kingScale0 * 4f;   // huge: only his head fits the picture
        var rends = king.root.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            kingTop = b.max.y - king.root.transform.position.y;
            kingBottom = b.min.y - king.root.transform.position.y;
        }
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
        float dist = kingTop * 1.15f;   // (the view can't tilt past 40 degrees, so not closer)
        Vector3 standAt = new Vector3(you.position.x, floorY, you.position.z) + ahead * dist;
        Vector3 sunk = standAt + Vector3.down * (kingTop - kingBottom + 2f);
        king.root.transform.position = sunk;
        FacePlayer(king.root.transform);

        // a dim pale haze behind him, or a black shadow on a black sky would be invisible
        var haze = GameObject.CreatePrimitive(PrimitiveType.Quad);
        DestroyImmediate(haze.GetComponent<Collider>());
        haze.name = "Soul haze";
        Vector3 hazeAt = standAt + ahead * 10f + Vector3.up * kingTop * kingHeadHeight;
        haze.transform.position = hazeAt;
        haze.transform.rotation = Quaternion.LookRotation(hazeAt - playerCamera.transform.position);
        haze.transform.localScale = Vector3.one * kingTop * 4f;
        var hazeTex = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = Mathf.Clamp01(new Vector2(x - 63.5f, y - 63.5f).magnitude / 64f);
                hazeTex.SetPixel(x, y, new Color(0.9f, 0.08f, 0.04f, Mathf.Pow(1f - d, 1.6f)));
            }
        hazeTex.Apply();
        var hazeShader = Shader.Find("Sprites/Default");
        var hazeMat = new Material(hazeShader != null ? hazeShader : Shader.Find("Universal Render Pipeline/Unlit")) { mainTexture = hazeTex };
        haze.GetComponent<Renderer>().sharedMaterial = hazeMat;
        haze.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Sounds.OneShot(king.sound, "Ending/Demon rises", null, 0.8f);
        if (king.animator != null) king.animator.SetTrigger(king.animTrigger);
        const float rise = 4f;
        for (float t = 0f; t < rise; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / rise);
            king.root.transform.position = Vector3.Lerp(sunk, standAt, k);
            RenderSettings.fogDensity = Mathf.Lerp(soulFogDensity, kingFogDensity, k);
            FacePlayer(king.root.transform);
            Bust(king);
            yield return null;
        }
        for (float t = 0f; t < 1f; t += Time.deltaTime) { Bust(king); yield return null; }
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
            if (DialogueBox.Instance != null && !string.IsNullOrEmpty(deathBrandLine)) DialogueBox.Instance.SayNow(deathBrandLine);
            yield return LookAtHands(arms, 0.8f);
            yield return RevisedPlotDirector.WaitForDialogue();
        }

        if (fader != null) yield return fader.FadeTo(1f, 1.2f);
        foreach (var s in shades) if (s != null) Destroy(s.gameObject);
        Destroy(voidSfx.gameObject);
        Destroy(template); Destroy(floor); Destroy(haze);
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

    // one shadow person, 15 to 24 m ahead of where you're looking, walking at you
    SoulFollower SpawnShade(GameObject template, Transform you, float floorY, float speed)
    {
        Vector3 fwd = playerCamera.transform.forward; fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
        Vector3 dir = Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * fwd;
        Vector3 at = you.position + dir * Random.Range(15f, 24f); at.y = floorY;
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
        foreach (var r in s.GetComponentsInChildren<Renderer>()) r.material.color = new Color(0.75f, 0.03f, 0.03f);   // lit up red, right in your face
        playerCamera.fieldOfView = 95f;
        Vector3 cam0 = cam.localPosition;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            TurnCamera(s.transform.position + Vector3.up * 1.65f, 20f);
            cam.localPosition = cam0 + Random.insideUnitSphere * 0.08f;
            yield return null;
        }
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
