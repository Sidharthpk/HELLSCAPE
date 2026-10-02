using System.Collections;
using TMPro;
using UnityEngine;

// Shooting from the van, GTA style. Hold RIGHT MOUSE while driving: you lean out of the driver's window, the camera
// swings to an over-the-shoulder aim you steer with the mouse (WASD still drives), a crosshair comes up.
// LEFT MOUSE fires the rifle (the one Maya threw you - no rifle, no drive-by), R reloads. Hits the damned, the killer,
// and Kessler (his heart takes the usual extra). Let go of right mouse and the chase camera takes back over.
public class DriveBy : MonoBehaviour
{
    public CarInteract car;
    public Camera carCamera;
    public CameraFollow follow;                  // switched off while aiming
    public Vector3 window = new Vector3(-1.15f, 1.85f, 0.55f);   // the driver's window, in the car's space
    public Vector3 pivot = new Vector3(0f, 2.3f, 0f);            // (the old orbit camera's centre; unused now)
    public float camDistance = 5.2f;
    public float shoulder = 0.9f;
    [Header("Leaning out (the aim view is your own eyes, out of the driver's window)")]
    public Vector3 leanOut = new Vector3(-1.62f, 2.05f, 0.4f);   // your head, hung out of the driver's door (car space)
    public float overRoof = 0.75f;               // aiming across to the other side you pull yourself up this much, over the roof
    public float aimFov = 58f;
    public float sensitivity = 2.2f;
    public Vector2 pitchLimits = new Vector2(-25f, 35f);

    [Header("Rifle")]
    public bool needsRifle = true;               // GunController.Equipped
    public float fireRate = 8f;
    public float damage = 14f;
    public float range = 160f;
    public int magSize = 30;
    public float reloadTime = 2f;
    public float spread = 0.018f;                // wider than on foot: you're hanging out of a moving van

    [Header("Lock-on (middle mouse)")]
    public float lockRange = 170f;
    public float lockAssist = 9f;                // how hard the aim is pulled onto the target
    public Vector3 lockCamOffset = new Vector3(1.6f, 4.6f, 9f);   // not aiming: behind the van, away from the target

    [Header("UI")]
    public TextMeshProUGUI crosshair;
    public TextMeshProUGUI ammoText;
    public TextMeshProUGUI lockMarker;           // over the target while locked

    public static bool Aiming { get; private set; }
    public static Transform LockTarget { get; private set; }

    float yaw, pitch, nextShot, fov0;
    int ammo;
    bool reloading;
    AudioSource source;
    AudioClip shot;
    Light flash;
    LineRenderer tracer;
    Material tracerMat;

    void Start()
    {
        ammo = magSize;
        shot = Sounds.Clip("Silo Fight/Rifle shot", ProceduralAudio.Gunshot());
        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        flash = new GameObject("DriveBy flash").AddComponent<Light>();
        flash.transform.SetParent(transform, false);
        flash.transform.localPosition = window + Vector3.left * 0.6f;
        flash.color = new Color(1f, 0.75f, 0.4f); flash.range = 8f; flash.intensity = 6f; flash.enabled = false;
        tracer = new GameObject("DriveBy tracer").AddComponent<LineRenderer>();
        tracerMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        tracerMat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.5f));
        tracer.sharedMaterial = tracerMat;
        tracer.widthMultiplier = 0.035f;
        tracer.positionCount = 2;
        tracer.enabled = false;
        ShowUI(false);
    }

    bool CanAim => car != null && car.InCar && (!needsRifle || GunController.Equipped) && !TitleScreen.Showing && Time.timeScale > 0f
                   && carCamera != null && carCamera.enabled;

    void Update()
    {
        UpdateLock();
        // nothing may leave the chase camera switched off while you're just driving
        if (car != null && car.InCar && !Aiming && LockTarget == null && follow != null && !follow.enabled && !FixedCameras.Active) follow.enabled = true;
        bool want = CanAim && Input.GetMouseButton(1);
        if (want && !Aiming) BeginAim();
        else if (!want && Aiming) EndAim();
        if (!Aiming) return;

        yaw += Input.GetAxis("Mouse X") * sensitivity;
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensitivity, pitchLimits.x, pitchLimits.y);

        if (reloading) return;
        if (Input.GetKeyDown(KeyCode.R) && ammo < magSize) { StartCoroutine(Reload()); return; }
        if (Input.GetMouseButton(0) && Time.time >= nextShot)
        {
            if (ammo <= 0) StartCoroutine(Reload());
            else Fire();
        }
    }

    // ---------------------------------------------------------------- lock-on

    void UpdateLock()
    {
        bool driving = car != null && car.InCar;
        if (driving && Input.GetMouseButtonDown(2))
        {
            if (LockTarget != null) Unlock();
            else
            {
                var boss = DemonBoss.Active != null ? DemonBoss.Active : FindFirstObjectByType<DemonBoss>();
                if (boss != null && !boss.Defeated && boss.heart != null && boss.heart.gameObject.activeInHierarchy
                    && boss.GetComponentInChildren<Renderer>() != null && boss.GetComponentInChildren<Renderer>().enabled
                    && Vector3.Distance(boss.heart.position, transform.position) < lockRange)
                {
                    LockTarget = boss.heart;
                    if (follow != null && !Aiming) follow.enabled = false;
                }
            }
        }
        if (LockTarget == null) { if (lockMarker != null && lockMarker.gameObject.activeSelf) lockMarker.gameObject.SetActive(false); return; }
        var b = LockTarget.GetComponentInParent<DemonBoss>();
        if (!driving || b == null || b.Defeated || Vector3.Distance(LockTarget.position, transform.position) > lockRange * 1.3f) { Unlock(); return; }
        if (lockMarker != null && carCamera != null)
        {
            Vector3 sp = carCamera.WorldToScreenPoint(LockTarget.position);
            lockMarker.gameObject.SetActive(sp.z > 0f);
            lockMarker.rectTransform.position = new Vector3(sp.x, sp.y, 0f);
        }
    }

    void Unlock()
    {
        LockTarget = null;
        if (lockMarker != null) lockMarker.gameObject.SetActive(false);
        if (follow != null && !Aiming) follow.enabled = true;
    }

    void LateUpdate()
    {
        if (LockTarget != null && !Aiming && carCamera != null) { LockCam(); return; }
        if (!Aiming || carCamera == null) return;
        if (LockTarget != null)
        {
            // pulled onto the target (the mouse still nudges it)
            Vector3 d = LockTarget.position - carCamera.transform.position;
            float wantYaw = Mathf.DeltaAngle(transform.eulerAngles.y, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            float wantPitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, pitchLimits.x, pitchLimits.y);
            float k = 1f - Mathf.Exp(-lockAssist * Time.deltaTime);
            yaw = Mathf.LerpAngle(yaw, wantYaw, k);
            pitch = Mathf.Lerp(pitch, wantPitch, k);
        }
        // the aim is relative to the van's heading, so it turns with you as you steer
        Quaternion aim = Quaternion.Euler(pitch, transform.eulerAngles.y + yaw, 0f);
        // your head, out of the driver's window: the van's flank runs along the right of the picture. Aim across to
        // the passenger side and you haul yourself up to shoot over the roof instead of into it.
        float across = Mathf.InverseLerp(10f, 70f, Mathf.DeltaAngle(0f, yaw));
        Vector3 head = leanOut + Vector3.up * overRoof * across;
        Vector3 want = transform.TransformPoint(head);
        // scraping a wall on the driver's side: pull your head in
        Vector3 sill = transform.TransformPoint(new Vector3(window.x, head.y, head.z));
        if (Physics.Raycast(sill, (want - sill).normalized, out RaycastHit h, (want - sill).magnitude + 0.35f, ~0, QueryTriggerInteraction.Ignore)
            && !h.collider.transform.IsChildOf(transform))
            want = sill + (want - sill).normalized * Mathf.Max(0f, h.distance - 0.35f);
        carCamera.transform.position = want;   // (fixed to the van: a smoothed position would trail behind at speed)
        carCamera.transform.rotation = Quaternion.Slerp(carCamera.transform.rotation, aim, 25f * Time.deltaTime);
        if (rifle != null) rifle.transform.localPosition = Vector3.Lerp(rifle.transform.localPosition, rifleHome, 14f * Time.deltaTime);   // settling after the kick
    }

    // locked on but not aiming: behind the van on the far side from the target, both in the frame
    void LockCam()
    {
        Vector3 van = transform.position + Vector3.up * 1.5f;
        Vector3 away = transform.position - LockTarget.position; away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, away);
        Vector3 want = transform.position + away * lockCamOffset.z + Vector3.up * lockCamOffset.y + right * lockCamOffset.x;
        if (Physics.SphereCast(van, 0.3f, (want - van).normalized, out RaycastHit h, (want - van).magnitude, ~0, QueryTriggerInteraction.Ignore)
            && !h.collider.transform.IsChildOf(transform) && h.collider.GetComponentInParent<DemonBoss>() == null)
            want = van + (want - van).normalized * Mathf.Max(1f, h.distance - 0.3f);
        Vector3 look = Vector3.Lerp(van, LockTarget.position, 0.4f);
        carCamera.transform.position = Vector3.Lerp(carCamera.transform.position, want, 6f * Time.deltaTime);
        carCamera.transform.rotation = Quaternion.Slerp(carCamera.transform.rotation, Quaternion.LookRotation(look - carCamera.transform.position), 8f * Time.deltaTime);
    }

    void BeginAim()
    {
        Aiming = true;
        // you lean out already looking at Kessler if he's about (wherever the driving camera happened to be
        // pointing means nothing from here); otherwise up the road
        yaw = 0f; pitch = 3f;
        var boss = DemonBoss.Active;
        if (boss != null && boss.isActiveAndEnabled && Vector3.Distance(boss.transform.position, transform.position) < lockRange)
        {
            Vector3 d = (boss.heart != null ? boss.heart.position : boss.transform.position + Vector3.up * 4f) - transform.TransformPoint(leanOut);
            yaw = Mathf.DeltaAngle(transform.eulerAngles.y, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            pitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, pitchLimits.x, pitchLimits.y);
        }
        if (follow != null) follow.enabled = false;
        fov0 = follow != null ? follow.baseFOV : 60f;
        carCamera.fieldOfView = aimFov;
        carCamera.transform.SetPositionAndRotation(transform.TransformPoint(leanOut), Quaternion.Euler(pitch, transform.eulerAngles.y + yaw, 0f));   // a cut, not a swoop
        ShowRifle(true);
        ShowUI(true);
        UpdateAmmo();
    }

    void EndAim()
    {
        Aiming = false;
        if (follow != null) follow.enabled = LockTarget == null;   // locked: the lock camera takes it
        if (carCamera != null) carCamera.fieldOfView = fov0;
        ShowRifle(false);
        ShowUI(false);
    }

    // the rifle and your arms in the picture while you aim: a copy of the on-foot view model (the player himself
    // is switched off while you drive), held in front of the van's camera exactly as it sits in front of your own
    GameObject rifle;
    Vector3 rifleHome;

    void ShowRifle(bool on)
    {
        if (rifle == null && on)
        {
            var gun = FindFirstObjectByType<GunController>(FindObjectsInactive.Include);
            if (gun == null || gun.viewModel == null || carCamera == null) return;
            rifle = Instantiate(gun.viewModel, carCamera.transform);
            rifle.name = "DriveBy rifle";
            rifle.transform.localPosition = rifleHome = gun.viewModel.transform.localPosition;
            rifle.transform.localRotation = gun.viewModel.transform.localRotation;
            rifle.transform.localScale = gun.viewModel.transform.localScale;
            foreach (var l in rifle.GetComponentsInChildren<Light>(true)) l.enabled = false;        // (its own muzzle light: ours flashes)
            foreach (var a in rifle.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
        }
        if (rifle != null) rifle.SetActive(on);
    }

    void Fire()
    {
        nextShot = Time.time + 1f / fireRate;
        ammo--;
        UpdateAmmo();
        source.pitch = Random.Range(0.92f, 1.08f);
        source.PlayOneShot(shot, Sounds.Volume("Silo Fight/Rifle shot"));
        StartCoroutine(Flash());

        // from the middle of the screen, past the van
        Vector3 dir = (carCamera.transform.forward + Random.insideUnitSphere * spread).normalized;
        Vector3 from = carCamera.transform.position;
        Vector3 end = from + dir * range;
        var hits = Physics.RaycastAll(from, dir, range, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (car.playerRoot != null && hit.collider.transform.IsChildOf(car.playerRoot.transform)) continue;
            end = hit.point;
            var demon = hit.collider.GetComponentInParent<DemonBoss>();
            if (demon != null) { demon.TakeDamage(damage, hit.point, dir, hit.collider); break; }
            var boss = hit.collider.GetComponentInParent<KillerBoss>();
            if (boss != null) { boss.TakeDamage(damage, hit.point, dir); break; }
            var z = hit.collider.GetComponentInParent<Zombie>();
            if (z != null) { z.TakeDamage(Mathf.RoundToInt(damage), hit.point, dir * 250f); break; }
            break;   // a wall
        }
        StartCoroutine(Tracer(carCamera.transform.position + carCamera.transform.forward * 1.1f - carCamera.transform.up * 0.18f + carCamera.transform.right * 0.2f, end));
        // a little kick
        if (rifle != null) rifle.transform.localPosition = rifleHome + Vector3.back * 0.05f;
        pitch = Mathf.Clamp(pitch - 0.35f, pitchLimits.x, pitchLimits.y);
        yaw += Random.Range(-0.2f, 0.2f);
    }

    IEnumerator Tracer(Vector3 a, Vector3 b)
    {
        tracer.SetPosition(0, a); tracer.SetPosition(1, b);
        tracer.enabled = true;
        yield return new WaitForSeconds(0.03f);
        tracer.enabled = false;
    }

    IEnumerator Flash()
    {
        flash.enabled = true;
        yield return new WaitForSeconds(0.04f);
        flash.enabled = false;
    }

    IEnumerator Reload()
    {
        reloading = true;
        if (ammoText != null) ammoText.text = "RELOADING";
        yield return new WaitForSeconds(reloadTime);
        ammo = magSize;
        reloading = false;
        UpdateAmmo();
    }

    void ShowUI(bool on)
    {
        if (crosshair != null) crosshair.gameObject.SetActive(on);
        if (ammoText != null) ammoText.gameObject.SetActive(on);
    }

    void UpdateAmmo()
    {
        if (ammoText != null && !reloading) ammoText.text = ammo + " / " + magSize;
    }

    void OnDisable() { if (Aiming) EndAim(); if (LockTarget != null) Unlock(); }
}
