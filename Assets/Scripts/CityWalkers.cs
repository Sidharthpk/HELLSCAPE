using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The hell city's first impression: the Lost (blue ghosts) and the Damned walking the streets, everywhere you look,
// none of them aware of you. Sits on an object under HellWorld and spawns them when the city wakes. The Damned are
// ordinary zombies kept calm for a while (then they notice you like any others); the Lost are only ghosts and never do.
public class CityWalkers : MonoBehaviour
{
    public GameObject zombiePrefab;          // PrimeZombie
    public Material ghost;                   // the Lost Soul's blue Unlit material
    public Transform player;
    public int damned = 12;
    public int lost = 16;
    public float minDistance = 14f;          // nobody starts on top of you
    public float maxDistance = 110f;
    public float calmFor = 40f;              // the Damned ignore you for this long after the city wakes

    public static readonly List<Transform> Walkers = new List<Transform>();

    void Start()
    {
        Walkers.Clear();
        if (zombiePrefab == null) return;
        if (player == null) { var p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.transform; }
        if (player == null) return;
        StartCoroutine(Spawn());
    }

    IEnumerator Spawn()
    {
        // not while you're still out cold: the death vision takes you off the street (nowhere near the navmesh), and
        // the calm would run out before you ever stood up
        yield return null;
        while (SleepSequence.Waking) yield return null;
        var calm = new List<ZombieAI>();
        for (int i = 0; i < damned + lost; i++)
        {
            bool isLost = i >= damned;
            if (!Point(isLost ? minDistance * .6f : minDistance * 1.6f, out var at)) continue;
            var go = Instantiate(zombiePrefab, at, Quaternion.Euler(0f, Random.value * 360f, 0f), transform);
            go.name = isLost ? "Lost (walking)" : "Damned (walking)";
            go.SetActive(true);
            Walkers.Add(go.transform);
            if (isLost) MakeLost(go);
            else
            {
                var ai = go.GetComponent<ZombieAI>();
                if (ai == null) continue;
                ai.crawlerChance = 0f; ai.feederChance = 0f; ai.dormantChance = 0f;
                ai.walkSpeedRange = new Vector2(.45f, .7f);
                ai.wanderRadius = 55f;
                ai.sightRange = 0f; ai.hearRange = 0f;
                calm.Add(ai);
            }
            if (i % 4 == 3) yield return null;   // a few a frame
        }
        yield return new WaitForSeconds(calmFor);
        foreach (var ai in calm) if (ai != null) { ai.sightRange = 18f; ai.hearRange = 5f; }
    }

    bool Point(float min, out Vector3 at)
    {
        for (int tries = 0; tries < 12; tries++)
        {
            Vector2 d = Random.insideUnitCircle.normalized * Random.Range(min, maxDistance);
            Vector3 p = player.position + new Vector3(d.x, 0f, d.y);
            if (NavMesh.SamplePosition(p, out var hit, 8f, NavMesh.AllAreas) && Vector3.Distance(hit.position, player.position) >= min) { at = hit.position; return true; }
        }
        at = default;
        return false;
    }

    void MakeLost(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        var ai = go.GetComponent<ZombieAI>(); if (ai != null) DestroyImmediate(ai);
        var z = go.GetComponent<Zombie>(); if (z != null) DestroyImmediate(z);
        foreach (var s in go.GetComponentsInChildren<AudioSource>(true)) Destroy(s);
        if (ghost != null)
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++) mats[k] = ghost;
                r.sharedMaterials = mats;
            }
        go.AddComponent<LostWalker>();
    }
}

// A Lost soul on its feet: shambles between random points of the street, never looks at you, flickers a little.
public class LostWalker : MonoBehaviour
{
    NavMeshAgent agent;
    Animator anim;
    Renderer[] rends;
    MaterialPropertyBlock block;
    float pause, seed;
    const float ClipSpeed = 0.41f;   // the Damned walk clip: m/s its feet cover at 1x (same as ZombieAI.walkClipSpeed)

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        rends = GetComponentsInChildren<Renderer>();
        seed = Random.value * 50f;
        pause = Random.Range(0f, 3f);
        if (agent != null) { agent.speed = Random.Range(.4f, .65f); agent.angularSpeed = 120f; agent.acceleration = 3f; }
        if (anim != null) anim.CrossFadeInFixedTime("Upright", 0f, 0, Random.value);
    }

    void Update()
    {
        if (agent == null || !agent.isOnNavMesh) return;
        if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + .3f))
        {
            pause -= Time.deltaTime;
            if (pause <= 0f)
            {
                pause = Random.Range(1f, 5f);
                Vector2 r = Random.insideUnitCircle * 45f;
                if (NavMesh.SamplePosition(transform.position + new Vector3(r.x, 0f, r.y), out var hit, 4f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
            }
        }
        if (anim != null) anim.speed = Mathf.Max(.25f, agent.velocity.magnitude / ClipSpeed);

        if (block == null) block = new MaterialPropertyBlock();
        float a = Mathf.Lerp(.18f, .5f, Mathf.PerlinNoise(seed, Time.time * 1.2f));
        foreach (var r in rends)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            var c = r.sharedMaterial.GetColor("_BaseColor"); c.a = a;
            block.SetColor("_BaseColor", c);
            r.SetPropertyBlock(block);
        }
    }
}
