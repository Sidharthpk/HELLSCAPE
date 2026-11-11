using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The burnt dead of the office, lying where they fell: against the fuse box, outside the CCTV room, by the door
// they never got through. Most never move. Then, one by one while you are not looking, they do: one is gone, one
// has turned to face where you were, one is sitting up, one is standing by the doorway. Not all of them, ever.
// Only in the red city; the Echo is the clean memory.
public class OfficeBodies : MonoBehaviour
{
    public class Body { public Transform t; public Animator anim; public string pose; public Vector3 home; }

    public static bool staticCorpses = false;   // the old eight bodies on the floor and against the walls (off: they spawned in odd places)
    public readonly List<Body> bodies = new List<Body>();
    Transform player; Camera cam;
    Transform maleTemplate, femaleTemplate;
    int shifts;
    bool echo;

    // (x, z) of each: what they were doing when it happened
    static readonly (float x, float z, string pose)[] Spots =
    {
        (1004.3f, 987.8f, "lie"),    // beside the fuse box
        (1013.6f, 1029.4f, "wall"),  // outside the CCTV room
        (1018.6f, 1029.2f, "lie"),   // at the printer
        (1017.0f, 1019.6f, "wall"),  // at the keypad door: tried to get out
        (1005.4f, 1018.4f, "lie"),   // under a desk near yours
        (1019.4f, 1026.8f, "lie"),   // reaching for the silo door
        (997.4f, 1008.6f, "wall"),   // by the first fuse
        (1010.5f, 1000.5f, "lie"),   // mid-corridor
    };

    public void Build(Transform player, Camera cam, Transform male, Transform female)
    {
        this.player = player; this.cam = cam; maleTemplate = male; femaleTemplate = female;
        // The static corpses (lying, or leaned on a wall, shifting when unseen) ended up in walls and odd spots; the
        // office's dead are the four burnt witnesses now, walking in pain (PainWalk). Only the temporary figures
        // (Standing) are still made from the templates.
        if (!staticCorpses) return;
        for (int i = 0; i < Spots.Length; i++)
        {
            var b = Make(i % 3 == 1 ? female : male);
            if (b == null) continue;
            if (!Settle(b, new Vector3(Spots[i].x, 0f, Spots[i].z), Spots[i].pose, i)) { Destroy(b.t.gameObject); continue; }
            bodies.Add(b);
        }
    }

    // a copy of a ghost's body, stripped of everything that made it a ghost
    public static GameObject Clone(Transform template)
    {
        var go = Instantiate(template.gameObject);
        go.SetActive(true);
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.gameObject.SetActive(false); Destroy(ps.gameObject); }
        foreach (var c in go.GetComponentsInChildren<Component>(true))
            if (c is GhostNPC || c is Interactable || c is Collider || c is AudioSource || c is WitnessLook || c is SoulConviction) Destroy(c);
        go.transform.localScale = template.lossyScale;
        return go;
    }

    Body Make(Transform template)
    {
        if (template == null) return null;
        var go = Clone(template);
        go.name = "Burnt body";
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var fem = r is SkinnedMeshRenderer s && s.sharedMesh != null && s.sharedMesh.name == "Cylinder";
            r.sharedMaterial = WitnessLook.Load(fem ? "BurntFemale" : "BurntMale");
        }
        // still smouldering: a little smoke and a dull ember light so they can be seen at all
        WitnessLook.Emitter(go.transform, "smoulder", new Color(.14f, .12f, .11f, 1f), .05f, 2f, .2f, -.02f, 2.5f, .4f);
        var ember = new GameObject("ember").AddComponent<Light>();
        ember.transform.SetParent(go.transform, false); ember.transform.localPosition = new Vector3(0f, .6f, .5f);
        ember.type = LightType.Point; ember.color = new Color(1f, .35f, .12f); ember.range = 3.2f; ember.intensity = .9f; ember.shadows = LightShadows.None;
        var b = new Body { t = go.transform, anim = go.GetComponentInChildren<Animator>() };
        if (b.anim != null) { b.anim.SetFloat("Speed", 0f); StartCoroutine(Freeze(b.anim)); }
        return b;
    }

    IEnumerator Freeze(Animator a) { yield return null; yield return null; if (a != null) a.speed = 0f; }

    public bool Floor(Vector3 near, out Vector3 floor)
    {
        floor = near;
        if (!Physics.Raycast(new Vector3(near.x, 5.2f, near.z), Vector3.down, out var h, 3f, ~0, QueryTriggerInteraction.Ignore) || h.normal.y < .9f) return false;
        if (Mathf.Abs(h.point.y - 3.4f) > .35f) return false;   // the office floor, not a desk
        floor = h.point; return true;
    }

    // find free floor near the spot and lay the body down (or lean it against the nearest wall)
    bool Settle(Body b, Vector3 anchor, string pose, int seed)
    {
        for (int i = 0; i < 20; i++)
        {
            Vector2 o = i == 0 ? Vector2.zero : Random.insideUnitCircle.normalized * (.4f + i * .15f);
            if (!Floor(anchor + new Vector3(o.x, 0f, o.y), out var f)) continue;
            if (pose == "wall" && TryWall(b, f)) return true;
            if (pose == "lie" || pose == "wall")
            {
                float yaw = Random.Range(0f, 360f);
                Quaternion q = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-90f, 0f, 0f);
                var box = f + Vector3.up * .25f;
                if (Physics.CheckBox(box, new Vector3(.4f, .22f, .95f), Quaternion.Euler(0f, yaw, 0f), ~0, QueryTriggerInteraction.Ignore)) continue;
                b.t.SetPositionAndRotation(f + Vector3.up * .17f, q);
                b.pose = "lie"; b.home = b.t.position; return true;
            }
        }
        return false;
    }

    bool TryWall(Body b, Vector3 floor)
    {
        float best = 1.6f; Vector3 dir = default, hit = default;
        for (int a = 0; a < 8; a++)
        {
            Vector3 d = Quaternion.Euler(0f, a * 45f, 0f) * Vector3.forward;
            if (Physics.Raycast(floor + Vector3.up * .7f, d, out var h, 1.6f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(h.normal.y) < .3f && h.distance < best)
            { best = h.distance; dir = d; hit = h.point; }
        }
        if (dir == default) return false;
        Vector3 feet = new Vector3(hit.x, floor.y, hit.z) - dir * 1.15f;
        // leaning back on the wall: head towards it
        b.t.SetPositionAndRotation(feet + Vector3.up * .1f, Quaternion.LookRotation(-dir) * Quaternion.Euler(-58f, 0f, 0f));
        if (Physics.CheckCapsule(feet + Vector3.up * .5f, feet + Vector3.up * 1.1f - dir * .5f, .3f, ~0, QueryTriggerInteraction.Ignore)) return false;
        b.pose = "wall"; b.home = b.t.position; return true;
    }

    public void SetEcho(bool on)
    {
        echo = on;
        foreach (var b in bodies) if (b.t != null) b.t.gameObject.SetActive(!on && b.pose != "gone");
    }

    bool Watched(Vector3 p)
    {
        if (cam == null) return false;
        Vector3 to = p + Vector3.up * .5f - cam.transform.position;
        if (to.magnitude < 5f) return true;   // too close to move under your nose
        if (Vector3.Angle(cam.transform.forward, to) >= 62f) return false;
        foreach (var h in Physics.RaycastAll(cam.transform.position, to.normalized, to.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!h.transform.IsChildOf(player.root) && !h.transform.IsChildOf(transform)) return false;
        return true;
    }

    // do the next change, if one of them is out of your sight. False: try again in a moment.
    public bool Shift()
    {
        if (echo || bodies.Count == 0) return false;
        var pool = bodies.FindAll(b => b.t != null && b.pose != "gone" && !Watched(b.t.position));
        if (pool.Count == 0) return false;
        var b = pool[Random.Range(0, pool.Count)];
        switch (Mathf.Min(shifts, 4))
        {
            case 0: b.pose = "gone"; b.t.gameObject.SetActive(false); break;   // one is missing
            case 1: Face(b); break;                                              // one has turned to look
            case 2: SitUp(b); break;                                             // one is sitting up
            case 3: StandAt(b, DoorSpot()); break;                               // one is standing by the doorway
            default: StandAt(b, b.t.position); break;                            // and then the rest start to stand
        }
        shifts++;
        return true;
    }

    Vector3 DoorSpot() => new Vector3(1017.6f, 3.4f, 1023.6f);

    void Face(Body b)
    {
        Vector3 d = player.position - b.t.position; d.y = 0f;
        // lying: spin about the body's own long axis so the face rolls round towards you
        b.t.rotation = Quaternion.LookRotation(d.normalized) * Quaternion.Euler(-90f, 180f, 0f);
    }

    void SitUp(Body b)
    {
        Vector3 d = player.position - b.t.position; d.y = 0f;
        b.t.rotation = Quaternion.LookRotation(d.normalized) * Quaternion.Euler(-25f, 0f, 0f);
        b.t.position = new Vector3(b.t.position.x, b.home.y - .05f, b.t.position.z);
        b.pose = "sit";
    }

    void StandAt(Body b, Vector3 at)
    {
        if (Floor(at, out var f))
        {
            Vector3 d = player.position - f; d.y = 0f;
            b.t.SetPositionAndRotation(f, Quaternion.LookRotation(d.sqrMagnitude > .01f ? d.normalized : Vector3.forward));
            b.pose = "stand";
        }
    }

    // a temporary upright figure (the employees at their desks): returns the clone; the caller removes it
    public GameObject Standing(Vector3 at, float yaw, bool female)
    {
        // never inside a wall or a desk: it needs free floor to stand on
        if (Physics.CheckCapsule(at + Vector3.up * .4f, at + Vector3.up * 1.5f, .3f, ~0, QueryTriggerInteraction.Ignore)) return null;
        var b = Make(female ? femaleTemplate : maleTemplate);
        if (b == null) return null;
        b.t.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        return b.t.gameObject;
    }
}
