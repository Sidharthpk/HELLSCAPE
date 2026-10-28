using System.Collections;
using UnityEngine;

// Story V4, Act 2: home, as the dead remember it. A copy of Flat 2B in the red city (HellScape > Red City: Red Flat
// builds it and wires these fields). The fridge note is blank, Derek's face is burned out of the photo, the bathroom
// is still locked with the shower running. The mark pulls you to the table: an Echo memory of the kitchen a year
// ago, in daylight, Derek at the counter with his back to you. Then your phone under the sofa: his voicemail (07:24).
// Once it's heard the objective turns to the office, and leaving says why you can't just drive there.
// HS rows 441-459.
public class RedFlat : MonoBehaviour, ISceneProgress
{
    public static bool Done { get; private set; }   // the voicemail is heard
    public static bool Arrived { get; private set; }   // in the flat (through the door, or woken on the sofa)

    public Transform table, sofaSeat, photoSpot;
    public Interactable fridgeNote;
    public PrologueDoor frontDoor, bathroomDoor;
    public Teleporter way, exit;                    // in from the street, and back out
    public Light[] lamps;
    public GameObject derek;                         // the CCTV Derek, copied for the memory (never facing you)
    public ScreenFader fader;

    GameObject glow, phone;
    Color[] lampColour; float[] lampPower;
    bool arrived;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetProgress() => Done = Arrived = false;

    public object CaptureProgress() => (arrived, glow != null && glow.activeSelf, phone != null && phone.activeSelf);
    public void RestoreProgress(object state)
    {
        var saved = ((bool arrived, bool glow, bool phone))state;
        arrived = saved.arrived;
        // Leaving during the arrival speech must not strand the unfinished memory on a revisit.
        if (glow != null) glow.SetActive(!Done && (saved.glow || saved.arrived && !saved.phone));
        if (phone != null) phone.SetActive(saved.phone && !Done);
    }

    void Start()
    {
        lampColour = new Color[lamps.Length]; lampPower = new float[lamps.Length];
        for (int i = 0; i < lamps.Length; i++)
        {
            lampColour[i] = lamps[i].color; lampPower[i] = lamps[i].intensity;
            lamps[i].color = new Color(1f, .22f, .15f); lamps[i].intensity = .55f;
        }
        if (fridgeNote != null) { fridgeNote.line = HS.S("442"); fridgeNote.once = false; }
        if (photoSpot != null) Photo();
        if (bathroomDoor != null)
        {
            var handle = bathroomDoor.GetComponent<Interactable>();
            handle.onUse.RemoveAllListeners();
            bathroomDoor.enabled = false;           // its Start would hook Toggle back on
            handle.prompt = "Bathroom door"; handle.once = false; handle.line = HS.S("444");
            var shower = new GameObject("Shower running (red flat)").AddComponent<AudioSource>();
            shower.transform.SetParent(transform);
            shower.transform.position = bathroomDoor.transform.position + Vector3.up;
            shower.clip = PrologueDirector.ShowerHiss(); shower.loop = true; shower.spatialBlend = 1f; shower.rolloffMode = AudioRolloffMode.Linear;
            shower.minDistance = 1.5f; shower.maxDistance = 14f; shower.volume = .35f;
            shower.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 2200f;
            shower.Play();
        }
        if (frontDoor != null && exit != null)
        {
            var handle = frontDoor.GetComponent<Interactable>();
            handle.onUse.RemoveAllListeners();
            frontDoor.enabled = false;
            handle.prompt = "Leave the flat"; handle.once = false; handle.line = "";
            handle.onUse.AddListener(exit.Go);
            exit.onArrive.AddListener(() => FirstSightCutscene.PlayOnce(exit));   // the first time you step out: the red city, seen
        }
        if (way != null) way.onArrive.AddListener(ArriveNow);

        glow = Glow("The mark's pull (table)", table != null ? table.position + Vector3.up * .06f : transform.position, new Color(1f, .35f, .1f));
        var touch = glow.AddComponent<Interactable>(); touch.prompt = "Touch the glow"; touch.line = "";
        touch.onUse.AddListener(() => StartCoroutine(Memory()));
        glow.SetActive(false);

        Vector3 under = sofaSeat != null ? sofaSeat.position + sofaSeat.forward * .5f + Vector3.up * .03f : transform.position;
        phone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        phone.name = "Your phone (under the sofa)";
        phone.transform.SetParent(transform);
        phone.transform.SetPositionAndRotation(under, sofaSeat != null ? sofaSeat.rotation : Quaternion.identity);
        phone.transform.localScale = new Vector3(.08f, .012f, .16f);
        phone.GetComponent<Renderer>().material.SetColor("_BaseColor", new Color(.02f, .02f, .03f));
        phone.GetComponent<BoxCollider>().size = new Vector3(4f, 20f, 2.5f);   // easy to find with the crosshair
        var screen = new GameObject("Screen light").AddComponent<Light>();
        screen.transform.SetParent(phone.transform, false); screen.transform.localPosition = Vector3.up * 8f;
        screen.type = LightType.Point; screen.range = 1.2f; screen.intensity = 1.5f; screen.color = new Color(.6f, .8f, 1f);
        var pick = phone.AddComponent<Interactable>(); pick.prompt = "Pick up your phone";
        var call = phone.AddComponent<ReadableNote>();
        call.style = ReadableNote.Style.Phone;
        call.title = "LAST CALL";
        call.subtitle = "DEREK";
        call.body = "Call log\n\n> PLAY";
        call.footer = "07:24";
        call.beforeLine = HS5.Id(84);   // V5.1 A02: My phone.
        call.afterLine = HS5.Id(85) + "|*In the office: [V] opens the Echo. [J] is your evidence.*";
        pick.onUse.AddListener(() => { Done = true; if (exit != null) exit.arriveLine = HS5.S("A02_go_office"); });
        phone.SetActive(false);
    }

    // in through the front door, or waking up on the sofa after the prologue (SleepSequence)
    public void ArriveNow() { if (!arrived) StartCoroutine(Arrive()); }

    IEnumerator Arrive()
    {
        arrived = Arrived = true;
        yield return RevisedPlotDirector.Speak(HS5.S("A02_flat_enter"));
        yield return new WaitForSeconds(4f);
        yield return RevisedPlotDirector.Speak(HS.S("458"));
        glow.SetActive(true);
    }

    // a year ago, the same kitchen in daylight
    IEnumerator Memory()
    {
        glow.SetActive(false);
        var fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null) fpc.playerCanMove = false;
        if (fader != null) yield return fader.FadeTo(1f, .6f);

        bool fog = RenderSettings.fog; Color ambient = RenderSettings.ambientLight; var mode = RenderSettings.ambientMode;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.75f, .72f, .66f);
        var homeLook = GetComponent<BurntHomeAppearance>();
        if (homeLook != null) homeLook.ShowClean(true);
        for (int i = 0; i < lamps.Length; i++) { lamps[i].color = new Color(1f, .95f, .85f); lamps[i].intensity = 2.4f; }
        GameObject him = null;
        if (derek != null && table != null && fpc != null)
        {
            // at the counter past the table, his back to you
            Vector3 away = table.position - fpc.transform.position; away.y = 0f; away.Normalize();
            Vector3 at = table.position + away * 1.4f; at.y = sofaSeat != null ? sofaSeat.position.y : table.position.y - .9f;
            him = Instantiate(derek, at, Quaternion.LookRotation(away), transform);
            him.name = "Derek (memory)";
            him.transform.localScale = derek.transform.lossyScale;   // (the office's copy is scaled with its rig)
            foreach (var mb in him.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
            foreach (var c in him.GetComponentsInChildren<Collider>(true)) Destroy(c);
            him.SetActive(true);
        }
        if (fader != null) yield return fader.FadeTo(0f, .8f);
        yield return RevisedPlotDirector.Speak(HS5.Id(81) + "|" + HS5.Id(82) + "|" + HS5.Id(83));   // V5.1: Derek, the last morning
        if (fader != null) yield return fader.FadeTo(1f, .8f);

        if (him != null) Destroy(him);
        if (homeLook != null) homeLook.ShowClean(false);
        RenderSettings.fog = fog; RenderSettings.ambientMode = mode; RenderSettings.ambientLight = ambient;
        for (int i = 0; i < lamps.Length; i++) { lamps[i].color = new Color(1f, .22f, .15f); lamps[i].intensity = .55f; }
        phone.SetActive(true);
        if (fader != null) yield return fader.FadeTo(0f, .6f);
        if (fpc != null) fpc.playerCanMove = true;
    }

    // the beach photo, Derek's face burned out of it
    void Photo()
    {
        var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "Beach photo (burned)";
        frame.transform.SetParent(transform);
        frame.transform.SetPositionAndRotation(photoSpot.position + Vector3.up * .12f, photoSpot.rotation * Quaternion.Euler(-12f, 0f, 0f));
        frame.transform.localScale = new Vector3(.2f, .24f, .02f);
        frame.GetComponent<Renderer>().material.SetColor("_BaseColor", new Color(.15f, .1f, .07f));
        var look = frame.AddComponent<Interactable>(); look.prompt = "Look at the photo"; look.line = HS.S("443"); look.once = false;
        Quad(frame.transform, new Vector3(0f, 0f, .51f), new Vector3(.85f, .85f, 1f), new Color(.6f, .45f, .3f));       // sand and sun, scorched
        Quad(frame.transform, new Vector3(.18f, .2f, .52f), new Vector3(.3f, .25f, 1f), new Color(.02f, .015f, .01f));  // the hole where his face was
    }

    void Quad(Transform parent, Vector3 at, Vector3 size, Color colour)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localPosition = at; q.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); q.transform.localScale = size;
        q.GetComponent<Renderer>().material.SetColor("_BaseColor", colour);
    }

    GameObject Glow(string name, Vector3 at, Color colour)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = name;
        g.transform.SetParent(transform);
        g.transform.position = at; g.transform.localScale = Vector3.one * .12f;
        var m = g.GetComponent<Renderer>().material;
        m.SetColor("_BaseColor", colour); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", colour * 3f);
        g.GetComponent<SphereCollider>().radius = 2.5f;
        var l = new GameObject("Light").AddComponent<Light>();
        l.transform.SetParent(g.transform, false);
        l.type = LightType.Point; l.range = 2.5f; l.intensity = 2f; l.color = colour;
        return g;
    }

    void Update()
    {
        if (glow != null && glow.activeSelf) glow.transform.localScale = Vector3.one * (.12f + .025f * Mathf.Sin(Time.time * 4f));
    }
}
