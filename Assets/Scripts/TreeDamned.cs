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
            float a = (start + k * 360f / perTree + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;
            var p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(2.2f, radius + 0.8f);
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

// stands still next to the tree watching the flames for a while (runs after ZombieAI so its wander can't move it),
// then walks off to a spot 15-25 m away and hands over to the normal wander. Never touches the height: no hopping.
[DefaultExecutionOrder(100)]
public class DamnedCircle : MonoBehaviour
{
    public Vector3 centre;
    public float radius = 3.6f, dir = 1f;   // radius/dir unused now, kept so the spawner still compiles
    ZombieAI ai; NavMeshAgent agent; float leaveAt;

    void Start() { ai = GetComponent<ZombieAI>(); agent = GetComponent<NavMeshAgent>(); leaveAt = Time.time + Random.Range(8f, 25f); }

    void Update()
    {
        if (ai == null || agent == null || !agent.isOnNavMesh) return;
        if (!ai.Calm) { Destroy(this); return; }   // noticed you: normal zombie again

        if (Time.time < leaveAt)
        {
            agent.isStopped = true;
            Vector3 face = centre - transform.position; face.y = 0f;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 2f * Time.deltaTime);
            return;
        }

        Vector2 r = Random.insideUnitCircle.normalized * Random.Range(15f, 25f);
        Vector3 to = transform.position + new Vector3(r.x, 0f, r.y);
        if (NavMesh.SamplePosition(to, out var hit, 6f, NavMesh.AllAreas)) { ai.SetHome(hit.position); agent.isStopped = false; agent.SetDestination(hit.position); }
        Destroy(this);
    }
}
