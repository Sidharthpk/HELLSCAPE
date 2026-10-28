using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Five Damned circle every burning tree. They are only spawned while you are near the tree (660 zombies at once would
// be far too many) and are removed again once you are far away and they have not noticed you. Sits next to HellTrees.
public class TreeDamned : MonoBehaviour
{
    public GameObject zombiePrefab;          // PrimeZombie
    public int perTree = 5;
    public float spawnRange = 55f;
    public float despawnRange = 85f;
    public float radius = 3.6f;
    public int maxAlive = 40;

    readonly Dictionary<int, List<GameObject>> alive = new Dictionary<int, List<GameObject>>();
    float next;

    void OnDisable() { foreach (var l in alive.Values) foreach (var g in l) if (g != null) Destroy(g); alive.Clear(); }

    void Update()
    {
        var trees = HellTrees.Instance;
        var cam = Camera.main;
        if (trees == null || cam == null || zombiePrefab == null || Time.time < next) return;
        next = Time.time + 0.5f;
        Vector3 c = cam.transform.position;

        var gone = new List<int>();
        int count = 0;
        foreach (var kv in alive)
        {
            Vector3 b = trees.Base(kv.Key);
            bool far = (new Vector3(b.x, c.y, b.z) - c).sqrMagnitude > despawnRange * despawnRange;
            kv.Value.RemoveAll(g => g == null);
            bool hunting = kv.Value.Exists(g => g.GetComponent<ZombieAI>().Chasing);
            if (far && !hunting) { foreach (var g in kv.Value) Destroy(g); gone.Add(kv.Key); }
            else count += kv.Value.Count;
        }
        foreach (int i in gone) alive.Remove(i);

        for (int i = 0; i < trees.Count && count + perTree <= maxAlive; i++)
        {
            if (alive.ContainsKey(i) || trees.lights[i] == null) continue;
            Vector3 b = trees.Base(i);
            if ((new Vector3(b.x, c.y, b.z) - c).sqrMagnitude > spawnRange * spawnRange) continue;
            alive[i] = Spawn(b);
            count += perTree;
            break;   // one tree per tick
        }
    }

    List<GameObject> Spawn(Vector3 centre)
    {
        var list = new List<GameObject>();
        float start = Random.value * 360f, dir = Random.value < .5f ? 1f : -1f;
        for (int k = 0; k < perTree; k++)
        {
            float a = (start + k * 360f / perTree) * Mathf.Deg2Rad;
            var p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            if (!NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) continue;
            var go = Instantiate(zombiePrefab, hit.position, Quaternion.identity, transform);
            go.name = "Damned (circling)";
            go.SetActive(true);
            var ai = go.GetComponent<ZombieAI>();
            if (ai == null) { Destroy(go); continue; }
            ai.dormantChance = 0f; ai.sneakChance = 0f;
            ai.walkSpeedRange = new Vector2(.5f, .7f);
            ai.sightRange = 12f; ai.hearRange = 4f;
            var c = go.AddComponent<DamnedCircle>();
            c.centre = centre; c.radius = radius; c.dir = dir;
            list.Add(go);
        }
        return list;
    }
}

// walks a ring around a point while the zombie is still calm (runs after ZombieAI, so its wander never gets a say),
// keeping its feet on the visible ground (the navmesh can sit a little above or below the road's surface)
[DefaultExecutionOrder(100)]
public class DamnedCircle : MonoBehaviour
{
    public Vector3 centre;
    public float radius = 3.6f, dir = 1f;
    ZombieAI ai; NavMeshAgent agent; float next, nextGround, baseOffset, wantOffset;

    void Start() { ai = GetComponent<ZombieAI>(); agent = GetComponent<NavMeshAgent>(); baseOffset = wantOffset = agent.baseOffset; }

    void Update()
    {
        if (ai == null || agent == null || !agent.isOnNavMesh) return;

        if (Time.time >= nextGround)
        {
            nextGround = Time.time + 0.15f;
            var o = agent.nextPosition + Vector3.up * 1.5f;
            var hits = Physics.RaycastAll(o, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; float y = 0f;
            foreach (var h in hits)
                if (!h.collider.transform.IsChildOf(transform) && h.distance < best && Mathf.Abs(h.point.y - agent.nextPosition.y) < 0.8f) { best = h.distance; y = h.point.y; }
            if (best < float.MaxValue) wantOffset = baseOffset + (y - agent.nextPosition.y);
        }
        agent.baseOffset = Mathf.MoveTowards(agent.baseOffset, wantOffset, 0.6f * Time.deltaTime);   // eased: never a pop

        // calm: turn the body to the tree while walking the ring; otherwise the agent steers it again
        agent.updateRotation = !ai.Calm;
        if (ai.Calm)
        {
            Vector3 face = centre - transform.position; face.y = 0f;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 4f * Time.deltaTime);
        }

        if (!ai.Calm || Time.time < next) return;
        next = Time.time + 0.3f;
        agent.autoBraking = false; agent.stoppingDistance = 0f;   // one steady walk, not a stop at every waypoint
        Vector3 d = transform.position - centre; d.y = 0f;
        float a = Mathf.Atan2(d.z, d.x) + dir * 0.5f;   // a point 2 m further round the ring
        var to = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
        if (NavMesh.SamplePosition(to, out var hit, 2f, NavMesh.AllAreas)) { agent.isStopped = false; agent.SetDestination(hit.position); }
    }
}
