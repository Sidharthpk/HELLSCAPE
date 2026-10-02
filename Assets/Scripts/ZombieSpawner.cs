using UnityEngine;
using System.Collections.Generic;

public class ZombieSpawner : MonoBehaviour
{
    public GameObject[] zombiePrefabs; // mix walkers and crawlers
    public Transform player;
    public Transform car;

    public float minSpawnRadius = 20f;
    public float maxSpawnRadius = 40f;
    public float despawnDistance = 70f;
    public float spawnInterval = 1.5f;
    public int maxAlive = 40;

    [Header("Hordes at fixed points")]
    public Transform[] spawnPoints;          // spread along the streets; each wakes once as you get close
    public float activateDistance = 55f;
    public Vector2Int hordeSize = new Vector2Int(3, 7);
    public float hordeRadius = 6f;

    [Header("No-zombie zones (e.g. the bridge)")]
    public BoxCollider[] noZombieZones;      // nothing spawns inside; zombies that wander in are removed
    public Bounds[] interiors;               // the office, the silo complex: no zombies at all while you're in one

    private List<GameObject> alive = new List<GameObject>();
    private HashSet<Transform> usedPoints = new HashSet<Transform>();
    private float timer;

    void Update()
    {
        alive.RemoveAll(g => g == null);

        Transform origin = Origin();
        if (origin == null) return;

        // on the bridge: no new zombies around you, and clear any that followed you in
        for (int i = alive.Count - 1; i >= 0; i--)
            if (InNoZombieZone(alive[i].transform.position)) { Destroy(alive[i]); alive.RemoveAt(i); }
        if (InNoZombieZone(origin.position)) return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval && alive.Count < maxAlive)
        {
            timer = 0f;
            Vector2 dir = Random.insideUnitCircle.normalized;
            float r = Random.Range(minSpawnRadius, maxSpawnRadius);
            SpawnNear(origin.position + new Vector3(dir.x * r, 0f, dir.y * r), 8f);
        }

        WakeHordes(origin.position);
        DespawnFarZombies(origin.position);
    }

    bool InNoZombieZone(Vector3 p)
    {
        if (interiors != null)
            foreach (var b in interiors)
                if (b.Contains(p)) return true;
        if (noZombieZones == null) return false;
        foreach (var z in noZombieZones)
            if (z != null && z.ClosestPoint(p) == p) return true;
        return false;
    }

    // on foot, follow the player; while driving the player object is disabled, so follow the car
    Transform Origin()
    {
        if (player != null && player.gameObject.activeInHierarchy) return player;
        return car != null ? car : player;
    }

    void WakeHordes(Vector3 origin)
    {
        if (spawnPoints == null) return;
        foreach (var p in spawnPoints)
        {
            if (p == null || usedPoints.Contains(p)) continue;
            if (Vector3.Distance(p.position, origin) > activateDistance) continue;

            usedPoints.Add(p);
            int n = Random.Range(hordeSize.x, hordeSize.y + 1);
            for (int i = 0; i < n && alive.Count < maxAlive; i++)
            {
                Vector2 o = Random.insideUnitCircle * hordeRadius;
                SpawnNear(p.position + new Vector3(o.x, 0f, o.y), 4f);
            }
        }
    }

    void SpawnNear(Vector3 pos, float snap)
    {
        if (zombiePrefabs.Length == 0) return;

        // snap to navmesh so the agent spawns on valid ground
        if (!UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit hit, snap, UnityEngine.AI.NavMesh.AllAreas))
            return;
        if (InNoZombieZone(hit.position)) return;

        GameObject prefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Length)];
        GameObject z = Instantiate(prefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        ZombieAI ai = z.GetComponent<ZombieAI>();
        if (ai != null) { ai.player = player; ai.car = car; }

        alive.Add(z);
    }

    void DespawnFarZombies(Vector3 origin)
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            if (alive[i] == null) continue;
            if (Vector3.Distance(alive[i].transform.position, origin) > despawnDistance)
            {
                Destroy(alive[i]);
                alive.RemoveAt(i);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
        foreach (var p in spawnPoints)
            if (p != null) Gizmos.DrawWireSphere(p.position, hordeRadius);
    }
}
