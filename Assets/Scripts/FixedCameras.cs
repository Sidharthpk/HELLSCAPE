using UnityEngine;

// The driving camera for the escape. As the user settled it (2026-10-01, after trying the others): CINEMATIC, one
// film set-up per stretch of road (see "the cinematic director" below), at most 3 camera changes before the tunnel
// and 4 after it, the last stretch (the bridge) from IN FRONT of the van; in the garage, the tunnel and the boss
// arena the chase camera rides behind the van.
// Two earlier designs are still here, switched off: topDown (high over the van, tilted: "boring"), and, with both
// flags off, a handful of fixed spots along the road (HellScape > Finale > Place Escape
// Cameras), at most three before the tunnel and five after it. Each stands behind the start of its stretch looking
// down the road the way you're driving, so "forward" is always up the screen and the next bend is in the picture;
// it stays put while the van is near, panning and zooming to keep it the same size; once the van is more than
// maxDistance away it glides along behind it (so the van is never a speck through a long lens), and it hard-cuts
// to the next spot when the van crosses into its stretch. Inside the tunnel and in the arena the ordinary chase
// camera rides behind the van.
// Aiming (right mouse) and the lock-on (middle mouse) still take the camera over while used.
// Lives on the car camera, next to CameraFollow (which it switches off while it's in charge).
public class FixedCameras : MonoBehaviour
{
    [System.Serializable]
    public class Spot
    {
        public Transform cam;
        public float from, to;               // metres along the road this camera covers
        public bool arena;                   // the boss arena: picked by where the van is, not by distance
        public bool chase;                   // no fixed spot here: the chase camera follows the van (tunnel, arena)
    }

    public CarInteract car;
    public CameraFollow follow;
    public Transform[] road;                 // the escape road's points, in order
    public Spot[] spots;
    public float fov = 52f;                  // widest (van close to the camera)
    public float minFov = 9f;                // tightest (van at the far end of the stretch)
    public float frameWidth = 32f;           // metres of street kept across the picture where the van is
    public float panSpeed = 5f;              // how quickly a camera swings round to follow the van
    public float zoomSpeed = 2.5f;
    public float leadTime = 0.35f;           // it looks a little ahead of where the van is going
    public float lookAhead = 6f;             // ...and this far up the road in front of its nose
    [Header("Keeping up (a spot only stays put while the van is near it)")]
    public float maxDistance = 34f;          // further off than this and the camera comes along behind the van
    public float maxHeight = 9f;             // ...and never looks down from higher than this above it
    public float glide = 0.45f;              // seconds it takes to settle when it moves
    public float bossClear = 5f;             // it keeps this far in front of Kessler when he's behind the van
    public float minDistance = 9f;           // ...but never closer to the van than this
    public float bossHeight = 6.5f;          // and at least this high above the van while he's pushing it in

    [Header("Cinematic (what the user settled on: film set-ups, one per stretch of road, never a follow camera)")]
    public bool cinematic = true;
    public float shotFrame = 26f;            // metres of street across the picture where the van is (standing cameras zoom to keep it)
    public float sideDistance = 6.5f;        // the camera riding beside the van: out from its flank
    public float sideBack = 3.5f;            // and this far back from its middle, so the road ahead is in the picture
    public float sideHeight = 2f;
    public float sideFov = 60f;
    public float frontAhead = 11f;           // the camera riding in front of the van (the bridge): this far ahead of it
    public float frontSide = 2.2f;           // ...off to one side of its path
    public float frontHeight = 1.9f;
    public float frontFov = 50f;

    [Header("Top-down (tried and dropped: 'looks boring')")]
    public bool topDown = false;             // on (and cinematic off) = high over the van, tilted
    public float topHeight = 28f;            // metres above the van
    public float topBack = 14f;              // ...and this far behind where it looks, so it's tilted, not a map
    public float topAhead = 6f;              // it looks this far in front of the van (the van sits low in the picture)
    public float topLead = 0.12f;            // ...plus this many seconds of its speed
    public float topFov = 55f;
    public float topSpeedZoom = 0.3f;        // this much higher and further back at full speed
    public float topTurn = 1.6f;             // how quickly the picture turns to the way you're driving
    public float arenaHeight = 34f;          // the boss arena: a little higher, to keep Kessler and his attacks in view
    public float roofBelow = 14f;            // a roof this close over the van: the chase camera takes it instead

    float roofedUntil = -1f;
    Vector3 glideVel, heading;
    bool trailing;                           // the camera is moving with the van (not standing on its spot)
    bool inTop;

    public static bool Active { get; private set; }

    Camera cam;
    Spot current;
    float[] along;                           // cumulative distance at each road point

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (road != null && road.Length > 0)
        {
            along = new float[road.Length];
            for (int i = 1; i < road.Length; i++) along[i] = along[i - 1] + Flat(road[i].position - road[i - 1].position).magnitude;
        }
    }

    void OnDisable() { Active = false; current = null; }

    void LateUpdate()
    {
        bool on = car != null && car.InCar && cam != null && cam.enabled && !DriveBy.Aiming && DriveBy.LockTarget == null && spots != null && spots.Length > 0;
        if (!on)
        {
            if (Active && follow != null && car != null && car.InCar && !DriveBy.Aiming && DriveBy.LockTarget == null) follow.enabled = true;
            current = null;
            Active = false;
            inTop = false;
            inCine = false;
            return;
        }

        Transform van = car.transform;
        Spot pick = Pick(van.position);
        if (pick == null) return;

        // under open sky the road is filmed: cut to cut (cinematic), or from overhead (topDown). Under a roof (the
        // tunnel, the garage) and in the boss arena the chase camera rides behind the van instead.
        bool roofed = Roofed(van);
        bool fromTop = inTop || inCine;
        // (cinematic: the only roof that counts is the garage you start in; the tunnel is its own stretch, and the
        //  gantry over the bridge must not cost a camera change)
        bool garage = roofed && Along(van.position) < 12f;
        if (cinematic && !pick.chase && !garage && Cinematic(van))
        {
            current = pick;
            inTop = false;
            return;
        }
        inCine = false;
        if (!cinematic && topDown && (pick.arena || !pick.chase) && !roofed)
        {
            current = pick;
            TopDown(van, pick.arena);
            return;
        }
        inTop = false;

        // the chase camera's stretches: hand over, starting right behind the van
        if (pick.chase || pick.cam == null || cinematic || (topDown && roofed))
        {
            if (follow != null && (pick != current || !follow.enabled || fromTop))
            {
                follow.enabled = true;
                if (pick != current || fromTop) follow.Snap();
            }
            current = pick;
            Active = false;
            return;
        }

        Active = true;
        if (follow != null && follow.enabled) follow.enabled = false;

        // what it looks at: a little up the road from the van
        var rb = van.GetComponent<Rigidbody>();
        Vector3 fwd = Flat(van.forward).normalized;
        Vector3 target = van.position + Vector3.up * 1.2f + fwd * lookAhead + (rb != null ? Flat(rb.linearVelocity) * leadTime : Vector3.zero);
        bool cut = pick != current;
        current = pick;
        Vector3 at = Place(pick.cam.position, van);
        // (a smoothed camera trails its target by speed x glide: aim that far ahead while it's keeping up with the
        //  van, or at 60 km/h it sits 7 m further back than asked, which is right where Kessler is)
        if (trailing && rb != null && !cut) at += Flat(rb.linearVelocity) * glide;
        // a hard cut to a new spot, like the old games; after that it only moves to keep up
        transform.position = cut ? at : Vector3.SmoothDamp(transform.position, at, ref glideVel, glide);
        if (cut) glideVel = Vector3.zero;

        Vector3 to = target - transform.position;
        Quaternion look = Quaternion.LookRotation(to);
        // zoomed so the same width of street stays in the picture however far down the stretch the van is
        float wide = 2f * Mathf.Atan(frameWidth * 0.5f / Mathf.Max(cam.aspect, 0.1f) / Mathf.Max(to.magnitude, 1f)) * Mathf.Rad2Deg;
        float wantFov = Mathf.Clamp(wide, minFov, fov);
        if (cut)
        {
            transform.rotation = look;
            cam.fieldOfView = wantFov;
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-panSpeed * Time.deltaTime));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, wantFov, 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime));
        }
    }

    // ---------------------------------------------------------------- the cinematic director
    //
    // The chase filmed like a film, with FEW cuts (the user's limits: no more than 3 camera changes before the
    // tunnel and no more than 4 after it, counting the cuts into the tunnel and into the boss arena). The road is
    // divided into the stretches of Plan below, and each stretch has ONE set-up for its whole length:
    //   Side / SideOther   the camera rides beside the van, off its rear quarter ("a camera on the side of the car")
    //   Front              the camera rides in front of the van, looking back at it and at what's chasing it
    //                      (the bridge: a straight line, so driving at the lens is easy)
    //   PassBy             standing low at the kerb part-way down the stretch: you come at it and tear past
    //   Behind             standing in the road behind the start of the stretch: you drive away from it
    //   Crane              standing high over the far part of the stretch
    // A standing camera pans and zooms with the van. It is only used if, from where it would stand, it can see the
    // road for the whole length of its stretch (checked as the van gets there); otherwise the next set-up on the
    // stretch's list is tried, and every list ends in one that rides with the van, which always works. So nothing
    // cuts in the middle of a stretch. Kessler never counts as in the way: BossSeeThrough ghosts him.
    enum Shot { Side, SideOther, Front, PassBy, Behind, Crane }

    // metres along the road. The tunnel (198-369) is the chase camera's; the last straight, 660 on, is the bridge.
    static readonly (float from, float to, Shot[] tries)[] Plan =
    {
        (float.MinValue, 95f, new[] { Shot.Side }),
        (95f, 198f, new[] { Shot.PassBy, Shot.Behind, Shot.SideOther }),
        (369f, 480f, new[] { Shot.Side }),
        (480f, 660f, new[] { Shot.Crane, Shot.PassBy, Shot.SideOther }),
        (660f, float.MaxValue, new[] { Shot.Front }),
    };

    bool inCine, shotStanding;
    int stretch = -1;
    Shot shot;
    float sideSign = 1f, sideOut, sideNow, hiddenFor;
    Vector3 shotPos;

    bool Cinematic(Transform van)
    {
        var rb = van.GetComponent<Rigidbody>();
        Vector3 vel = rb != null ? Flat(rb.linearVelocity) : Vector3.zero;
        Vector3 fwd = Flat(van.forward).normalized;
        float a = Along(van.position);

        // which stretch (a van that has backed up a few metres over a boundary doesn't flip the camera back)
        int want = stretch;
        for (int i = 0; i < Plan.Length; i++) if (a >= Plan[i].from && a < Plan[i].to) want = i;
        if (want < 0) return false;
        if (stretch >= 0 && want < stretch && a > Plan[stretch].from - 15f) want = stretch;
        bool cut = !inCine || want != stretch;
        if (cut)
        {
            stretch = want;
            float from = Mathf.Max(Plan[stretch].from, 0f), to = Mathf.Min(Plan[stretch].to, along[road.Length - 1] + 40f);
            bool found = false;
            foreach (var s in Plan[stretch].tries) if (SetUp(s, van, a, from, to)) { found = true; break; }
            if (!found) SetUp(Shot.Front, van, a, from, to);
            hiddenFor = 0f;
            heading = shot == Shot.Front ? DirAt(a) : vel.magnitude > 3f ? vel.normalized : fwd;
            sideNow = sideOut;
        }
        else if (shotStanding)
        {
            // something has come between a standing camera and the van after all (a wreck shoved across the road):
            // ride beside it for the rest of the stretch
            hiddenFor = Sees(shotPos, van.position + Vector3.up * 1.3f) ? 0f : hiddenFor + Time.deltaTime;
            if (hiddenFor > 0.8f && (SetUp(Shot.SideOther, van, a, 0f, 0f) || SetUp(Shot.Front, van, a, 0f, 0f))) { cut = true; heading = vel.magnitude > 3f ? vel.normalized : fwd; sideNow = sideOut; }
        }
        inCine = true;
        Active = true;
        if (follow != null && follow.enabled) follow.enabled = false;

        Vector3 pos, look; float wantFov;
        if (shotStanding)
        {
            pos = shotPos;
            look = van.position + Vector3.up * 1.1f + fwd * 2.5f + vel * 0.2f;
            float wide = 2f * Mathf.Atan(shotFrame * 0.5f / Mathf.Max(cam.aspect, 0.1f) / Mathf.Max((look - pos).magnitude, 1f)) * Mathf.Rad2Deg;
            wantFov = Mathf.Clamp(wide, 14f, 58f);
        }
        else if (shot == Shot.Front)
        {
            // ahead of the van down the road's own line (not the van's nose: weaving doesn't swing the picture)
            heading = Vector3.Slerp(heading, DirAt(a), 1f - Mathf.Exp(-2f * Time.deltaTime)).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, heading);
            pos = van.position + heading * frontAhead + side * frontSide + Vector3.up * frontHeight;
            look = van.position + Vector3.up * 1.5f - heading * 3f;   // past the van, at what's coming after it
            wantFov = frontFov;
        }
        else
        {
            // beside the van, on the heading it's really travelling (eased, so a swerve doesn't shake the picture),
            // tucked in when a wall comes close on that side
            Vector3 dir = vel.magnitude > 3f && Vector3.Dot(vel.normalized, fwd) > 0.3f ? vel.normalized : fwd;
            heading = Vector3.Slerp(heading, dir, 1f - Mathf.Exp(-2.5f * Time.deltaTime)).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, heading) * sideSign;
            float room = Mathf.Clamp(Room(van.position + Vector3.up * 1.5f - heading * sideBack, side) - 1f, 2.2f, sideOut);
            sideNow = cut ? room : Mathf.Lerp(sideNow, room, 1f - Mathf.Exp(-4f * Time.deltaTime));
            pos = van.position + side * sideNow - heading * sideBack + Vector3.up * sideHeight;
            look = van.position + heading * 9f + Vector3.up * 1f;
            wantFov = sideFov;
        }

        Quaternion rot = Quaternion.LookRotation(look - pos);
        if (cut)
        {
            transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = wantFov;
            glideVel = Vector3.zero;
        }
        else
        {
            transform.position = shotStanding ? pos : Vector3.SmoothDamp(transform.position, pos + vel * 0.12f, ref glideVel, 0.12f);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-(shotStanding ? panSpeed : 9f) * Time.deltaTime));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, wantFov, 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime));
        }
        return true;
    }

    // where this set-up puts the camera for the stretch from..to (the van is at 'a'); false = it doesn't work here
    bool SetUp(Shot s, Transform van, float a, float from, float to)
    {
        float y = van.position.y;
        if (s == Shot.Front) { shot = s; shotStanding = false; return true; }
        if (s == Shot.Side || s == Shot.SideOther)
        {
            // whichever flank has the room (SideOther: the one Side didn't take, if there's room there too)
            Vector3 r = Vector3.Cross(Vector3.up, Flat(van.forward).normalized);
            float right = Room(van.position + Vector3.up * 1.5f, r), left = Room(van.position + Vector3.up * 1.5f, -r);
            float sign = right >= left ? 1f : -1f;
            if (s == Shot.SideOther && (sideSign > 0f ? left : right) >= 3.5f) sign = -sideSign;
            float room = sign > 0f ? right : left;
            if (room < 3.5f) return false;
            shot = s; shotStanding = false;
            sideSign = sign;
            sideOut = Mathf.Min(sideDistance, room - 1f);
            return true;
        }
        // a standing camera, on either side of the road, that sees the whole stretch from where it stands
        foreach (float sign in new[] { 1f, -1f })
        {
            Vector3 p;
            switch (s)
            {
                case Shot.PassBy: { float at = Mathf.Lerp(from, to, 0.55f); p = At(at) + Vector3.Cross(Vector3.up, DirAt(at)) * 6.5f * sign; p.y = y + 1.3f; break; }
                case Shot.Crane: { float at = Mathf.Lerp(from, to, 0.68f); p = At(at) + Vector3.Cross(Vector3.up, DirAt(at)) * 3f * sign; p.y = y + 13f; break; }
                default: { p = At(from - 6f) + Vector3.Cross(Vector3.up, DirAt(from)) * 2.5f * sign; p.y = y + 4.5f; break; }
            }
            if (Physics.CheckSphere(p, 0.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
            bool clear = Sees(p, van.position + Vector3.up * 1.3f);
            for (float x = from + 2f; clear && x <= to - 2f; x += 6f) { Vector3 q = At(x); q.y = y + 1.3f; clear = Sees(p, q); }
            if (!clear) continue;
            shot = s; shotStanding = true; shotPos = p;
            return true;
        }
        return false;
    }

    // metres of clear air out from p along dir (up to the side camera's reach)
    float Room(Vector3 p, Vector3 dir)
    {
        float best = sideDistance + 1f;
        foreach (var h in Physics.RaycastAll(p, dir, sideDistance + 1f, ~0, QueryTriggerInteraction.Ignore))
            if (Solid(h.collider) && h.distance < best) best = h.distance;
        return best;
    }

    // nothing solid between these two points (zombies, wrecks, lamp posts and Kessler don't hide a van)
    bool Sees(Vector3 from, Vector3 target)
    {
        Vector3 to = target - from;
        foreach (var h in Physics.RaycastAll(from, to.normalized, Mathf.Max(0f, to.magnitude - 1.5f), ~0, QueryTriggerInteraction.Ignore))
            if (Solid(h.collider)) return false;
        return true;
    }

    bool Solid(Collider c)
    {
        if (c.transform.IsChildOf(car.transform) || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)) return false;
        if (c.GetComponentInParent<Zombie>() != null || c.GetComponentInParent<DemonBoss>() != null) return false;
        return c.bounds.size.x >= 1.5f || c.bounds.size.z >= 1.5f;
    }

    // the road's middle 'a' metres along it, and which way it runs there
    Vector3 At(float a)
    {
        if (road == null || road.Length < 2 || along == null) return car.transform.position;
        float total = along[road.Length - 1];
        if (a <= 0f) return road[0].position + DirAt(0f) * a;
        if (a >= total) return road[road.Length - 1].position + DirAt(total) * (a - total);
        int i = 0; while (i < road.Length - 2 && along[i + 1] < a) i++;
        return Vector3.Lerp(road[i].position, road[i + 1].position, Mathf.InverseLerp(along[i], along[i + 1], a));
    }

    Vector3 DirAt(float a)
    {
        if (road == null || road.Length < 2 || along == null) return Flat(car.transform.forward).normalized;
        int i = 0; while (i < road.Length - 2 && along[i + 1] < a) i++;
        return Flat(road[i + 1].position - road[i].position).normalized;
    }

    // a real roof low over the van (a building, the tunnel): not a lamp post, a sign or the bridge's girders high up.
    // Held for half a second after it ends, so the cut happens once you're properly out from under it.
    bool Roofed(Transform van)
    {
        foreach (var h in Physics.RaycastAll(van.position + Vector3.up * 2.4f, Vector3.up, roofBelow, ~0, QueryTriggerInteraction.Ignore))
        {
            var c = h.collider;
            if (c.transform.IsChildOf(van) || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)) continue;
            if (c.GetComponentInParent<Zombie>() != null || c.GetComponentInParent<DemonBoss>() != null) continue;
            if (c.bounds.size.x < 6f || c.bounds.size.z < 6f) continue;
            roofedUntil = Time.time + 0.5f;
            break;
        }
        return Time.time < roofedUntil;
    }

    // High over the van and a little behind where it looks, tilted so the street has depth. The picture's "up" is the
    // way you're driving (eased, so a swerve doesn't swing the world), it rises with speed to show more road ahead,
    // and nothing that chases you can stand between it and the van.
    void TopDown(Transform van, bool arena)
    {
        Active = true;
        if (follow != null && follow.enabled) follow.enabled = false;
        var rb = van.GetComponent<Rigidbody>();
        Vector3 vel = rb != null ? Flat(rb.linearVelocity) : Vector3.zero;
        Vector3 fwd = Flat(van.forward).normalized;
        // the way you're going; reversing or sliding sideways doesn't turn the picture round
        Vector3 dir = vel.magnitude > 3f && Vector3.Dot(vel.normalized, fwd) > 0.3f ? vel.normalized : fwd;
        bool snap = !inTop || heading == Vector3.zero;
        inTop = true;
        heading = snap ? dir : Vector3.Slerp(heading, dir, 1f - Mathf.Exp(-topTurn * Time.deltaTime)).normalized;

        float k = 1f + topSpeedZoom * Mathf.Clamp01(vel.magnitude / 28f);
        Vector3 focus = van.position + heading * topAhead + vel * topLead;
        Vector3 at = focus + Vector3.up * (arena ? arenaHeight : topHeight) * k - heading * topBack * k;

        // a roof over the van (not a lamp post or a girder): come down under it
        Vector3 from = van.position + Vector3.up * 2.4f, d = at - from;
        float keep = d.magnitude;
        foreach (var h in Physics.RaycastAll(from, d.normalized, keep, ~0, QueryTriggerInteraction.Ignore))
        {
            var c = h.collider;
            if (c.transform.IsChildOf(van) || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)) continue;
            if (c.GetComponentInParent<Zombie>() != null || c.GetComponentInParent<DemonBoss>() != null) continue;
            if (c.bounds.size.x < 6f || c.bounds.size.z < 6f) continue;
            keep = Mathf.Min(keep, Mathf.Max(5f, h.distance - 0.8f));
        }
        at = from + d.normalized * keep;

        if (snap) { transform.position = at; glideVel = Vector3.zero; }
        else transform.position = Vector3.SmoothDamp(transform.position, at + vel * glide, ref glideVel, glide);   // (+ vel x glide: no lag)
        Quaternion look = Quaternion.LookRotation(focus - transform.position, heading);
        transform.rotation = snap ? look : Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-panSpeed * Time.deltaTime));
        cam.fieldOfView = snap ? topFov : Mathf.Lerp(cam.fieldOfView, topFov, 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime));
    }

    // Where the camera stands for this spot. The spot itself while the van is near it; once the van is further off
    // than maxDistance the camera comes along the same sight line behind it (a van 100 m away is a dot through a
    // telescope), no higher above it than maxHeight, and in front of anything solid that would hide it.
    Vector3 Place(Vector3 spot, Transform van)
    {
        Vector3 p = spot, v = van.position;
        Vector3 back = Flat(p - v);
        trailing = back.magnitude > maxDistance;
        if (trailing) p = v + back.normalized * maxDistance + Vector3.up * (p.y - v.y);
        p.y = Mathf.Min(p.y, v.y + maxHeight);

        Vector3 from = v + Vector3.up * 1.6f, d = p - from;
        float keep = d.magnitude;
        foreach (var h in Physics.RaycastAll(from, d.normalized, keep, ~0, QueryTriggerInteraction.Ignore))
        {
            var c = h.collider;
            if (c.transform.IsChildOf(van)) continue;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;                    // wrecks, ragdolls
            if (c.GetComponentInParent<Zombie>() != null || c.GetComponentInParent<DemonBoss>() != null) continue;
            if (c.bounds.size.x < 1.5f && c.bounds.size.z < 1.5f) continue;                                    // a lamp post doesn't hide a van
            keep = Mathf.Min(keep, Mathf.Max(4f, h.distance - 0.6f));
        }
        // Kessler hunts from behind the van, which is where the camera stands: stay in front of him, between him and
        // the van, so he isn't a wall of demon across the foreground (when he's right on your bumper BossSeeThrough
        // fades him instead)
        var boss = DemonBoss.Active;
        if (boss != null && boss.isActiveAndEnabled)
        {
            Vector3 dn = d.normalized, toBoss = boss.transform.position + Vector3.up * 3f - from;
            float t = Vector3.Dot(toBoss, dn);
            if (t > 0f && t < keep + bossClear && (toBoss - dn * t).magnitude < 7f)
            {
                keep = Mathf.Min(keep, Mathf.Max(minDistance, t - bossClear));
                trailing = true;
                // this close in, keep the height: looking down on the van and the road ahead, not along its roof
                Vector3 near = from + dn * keep;
                near.y = Mathf.Max(near.y, v.y + Mathf.Min(maxHeight, bossHeight));
                return near;
            }
        }
        if (keep < d.magnitude - 0.01f) trailing = true;
        return from + d.normalized * keep;
    }

    Spot Pick(Vector3 p)
    {
        var boss = DemonBoss.Active;
        if (boss != null && boss.arena != null && InArena(boss, p))
        {
            var arena = System.Array.Find(spots, s => s.arena);
            if (arena != null) return arena;
        }
        float a = Along(p);
        Spot last = null, first = null;
        foreach (var s in spots)
        {
            if (s.arena) continue;
            if (first == null) first = s;
            if (a >= s.from && a < s.to) return s;
            last = s;
        }
        // past the last stretch (up the bridge): the last road camera, or the first if before it all
        return a < 0f ? first : last;
    }

    static bool InArena(DemonBoss b, Vector3 p)
    {
        Vector3 l = b.arena.InverseTransformPoint(p);
        return Mathf.Abs(l.x) < b.arenaHalfWidth + 2f && Mathf.Abs(l.z) < b.arenaHalfLength + 2f;
    }

    // how far along the road p is (projected onto the nearest segment)
    public float Along(Vector3 p)
    {
        if (road == null || road.Length < 2 || along == null) return 0f;
        float best = float.MaxValue, at = 0f;
        for (int i = 0; i < road.Length - 1; i++)
        {
            Vector3 a = Flat(road[i].position), b = Flat(road[i + 1].position), q = Flat(p);
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(q - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            float d = (a + ab * t - q).sqrMagnitude;
            if (d < best) { best = d; at = along[i] + ab.magnitude * t; }
        }
        // beyond the last point: keep counting
        Vector3 lastDir = Flat(road[road.Length - 1].position - road[road.Length - 2].position).normalized;
        float beyond = Vector3.Dot(Flat(p - road[road.Length - 1].position), lastDir);
        if (beyond > 0f && at >= along[road.Length - 1] - 0.5f) at = along[road.Length - 1] + beyond;
        return at;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
