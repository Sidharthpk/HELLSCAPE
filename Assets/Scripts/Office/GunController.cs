using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// The AK a colleague throws down to you in the silo. On the player camera; stays off until Equip().
// Hitscan, full auto, R to reload. Animator states: Idle, Shoot, Reload, Draw.
public class GunController : MonoBehaviour
{
    public static bool Equipped;

    public Camera cam;
    public GameObject viewModel;            // AK + arms, child of the camera, disabled by default
    public Animator animator;
    public GameObject fistsRig;             // the punching arms, hidden while the gun is out

    [Header("Shooting")]
    public float fireRate = 6.5f;           // shots a second (user: 9 was too rapid)
    public float damage = 14f;
    public float range = 150f;
    public int magSize = 30;
    public float reloadTime = 2.2f;
    public float kickBack = 0.06f;          // viewmodel recoil

    [Header("FX")]
    public AudioSource source;
    public AudioClip shotClip;              // procedural bang if empty
    public AudioClip reloadClip;            // procedural click if empty
    public Light muzzleLight;
    public TextMeshProUGUI ammoText;

    // the Elias fight (SiloEncounter): one magazine at a time. Empty, you say so, Maya throws you the next one and you
    // have to find it. Off everywhere else: the reloads are free.
    [HideInInspector] public bool limited;
    private int spare;
    private bool emptyCalled;
    private System.Action onEmpty;

    private int ammo;
    private float nextShot;
    private bool reloading;
    private Vector3 restPos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        Equipped = false;
        holsters.Clear();
        SceneManager.sceneLoaded += (s, m) => { Equipped = false; holsters.Clear(); };
    }

    // put away (not lost) while your hands are busy: climbing, swimming, the judgement, or anything that asks
    static readonly System.Collections.Generic.HashSet<object> holsters = new System.Collections.Generic.HashSet<object>();
    public static void Holster(object who, bool on) { if (on) holsters.Add(who); else holsters.Remove(who); }
    public static bool Holstered => holsters.Count > 0 || Ladder.Climbing || BloodFlood.Swimming || EndingSequence.Playing;

    void Start()
    {
        shotClip = Sounds.Clip("Silo Fight/Rifle shot", shotClip != null ? shotClip : ProceduralAudio.Gunshot());
        reloadClip = Sounds.Clip("Silo Fight/Rifle reload", null);   // silent unless you put a sound in that slot
        if (viewModel != null) { restPos = viewModel.transform.localPosition; viewModel.SetActive(Equipped); }
        if (muzzleLight != null) muzzleLight.enabled = false;
        if (ammoText != null) ammoText.gameObject.SetActive(Equipped);
    }

    // getting into the van switches the player off mid-reload and kills the coroutine: don't stay "reloading" forever
    void OnDisable()
    {
        if (reloading) { reloading = false; if (ammo <= 0 && (!limited || spare > 0)) { ammo = magSize; if (limited) spare--; } UpdateAmmo(); }
    }

    public void Equip()
    {
        Equipped = true;
        ammo = magSize;
        if (fistsRig != null) fistsRig.SetActive(false);
        viewModel.SetActive(true);
        if (animator != null) animator.Play("Draw", 0, 0f);
        if (ammoText != null) ammoText.gameObject.SetActive(true);
        UpdateAmmo();
    }

    void Update()
    {
        if (!Equipped) return;

        bool show = !Holstered;
        if (viewModel != null && viewModel.activeSelf != show)
        {
            viewModel.SetActive(show);
            if (show && animator != null) animator.Play("Draw", 0, 0f);   // brought back up
        }
        // the van's own aim readout (DriveBy) takes over while you aim from the car: never two ammo counters at once
        bool showAmmo = show && !DriveBy.Aiming;
        if (ammoText != null && ammoText.gameObject.activeSelf != showAmmo) ammoText.gameObject.SetActive(showAmmo);
        if (!show) return;

        if (viewModel != null)
            viewModel.transform.localPosition = Vector3.Lerp(viewModel.transform.localPosition, restPos, 12f * Time.deltaTime);

        if (reloading) return;
        if (Input.GetKeyDown(KeyCode.R) && ammo < magSize && (!limited || spare > 0)) { StartCoroutine(Reload()); return; }
        if (Input.GetMouseButton(0) && Time.time >= nextShot)
        {
            if (ammo > 0) Fire();
            else if (!limited || spare > 0) StartCoroutine(Reload());
            else { nextShot = Time.time + 0.35f; Empty(); }   // click: no magazine
        }
    }

    // limited magazines on (the Elias fight) or off again; either way a full magazine in the gun
    public void SetLimited(bool on, System.Action whenEmpty = null)
    {
        limited = on;
        onEmpty = whenEmpty;
        spare = 0;
        emptyCalled = false;
        StopAllCoroutines();
        reloading = false;
        ammo = magSize;
        UpdateAmmo();
    }

    // you picked up the magazine: straight in if the gun's empty
    public void AddMag()
    {
        spare++;
        emptyCalled = false;
        UpdateAmmo();
        if (ammo <= 0 && !reloading) StartCoroutine(Reload());
    }

    void Empty()
    {
        if (!limited || emptyCalled || onEmpty == null) return;
        emptyCalled = true;
        onEmpty();
    }

    void Fire()
    {
        nextShot = Time.time + 1f / fireRate;
        ammo--;
        UpdateAmmo();
        if (ammo <= 0 && limited && spare <= 0) Empty();   // that was the last round

        if (animator != null) animator.Play("Shoot", 0, 0f);
        if (source != null) { source.pitch = Random.Range(0.92f, 1.08f); source.PlayOneShot(shotClip, Sounds.Volume("Silo Fight/Rifle shot")); }
        if (muzzleLight != null) StartCoroutine(Flash());
        if (viewModel != null) viewModel.transform.localPosition = restPos - Vector3.forward * kickBack;

        Vector3 dir = cam.transform.forward + Random.insideUnitSphere * 0.012f;
        KillerBoss.ShotFired(cam.transform.position, dir);   // (Elias may not be there by the time it lands)
        // nearest hit that isn't our own body (the camera sits at the edge of the player's capsule)
        var hits = Physics.RaycastAll(cam.transform.position, dir, range, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        int first = System.Array.FindIndex(hits, h => !h.collider.transform.IsChildOf(cam.transform.root));
        if (first < 0) return;
        var hit = hits[first];

        var boss = hit.collider.GetComponentInParent<KillerBoss>();
        if (boss != null) { boss.TakeDamage(damage, hit.point, dir); return; }

        var blackHeart = hit.collider.GetComponentInParent<BossHeart>();
        if (blackHeart != null) { blackHeart.Hit(damage, hit.point, dir); return; }

        var demon = hit.collider.GetComponentInParent<DemonBoss>();
        if (demon != null) { demon.TakeDamage(damage, hit.point, dir, hit.collider); return; }

        var z = hit.collider.GetComponentInParent<Zombie>();
        if (z != null) z.TakeDamage(Mathf.RoundToInt(damage), hit.point, dir * 250f);
    }

    IEnumerator Reload()
    {
        reloading = true;
        if (animator != null) animator.Play("Reload", 0, 0f);
        if (source != null && reloadClip != null) source.PlayOneShot(reloadClip);
        yield return new WaitForSeconds(reloadTime);
        if (source != null && reloadClip != null) source.PlayOneShot(reloadClip);
        if (limited) spare = Mathf.Max(0, spare - 1);
        ammo = magSize;
        UpdateAmmo();
        reloading = false;
    }

    IEnumerator Flash()
    {
        muzzleLight.enabled = true;
        yield return new WaitForSeconds(0.04f);
        muzzleLight.enabled = false;
    }

    void UpdateAmmo()
    {
        if (ammoText != null) ammoText.text = ammo + " / " + (limited ? "MAG x" + spare : "∞");
    }
}
