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
    public GameObject lostModel;             // Female Dynamic Pose: the Lost
    public RuntimeAnimatorController floatController;   // holds her floating pose
    public Material ghost;                   // the Lost Soul's blue Unlit material
    public Transform player;
    public int damned = 12;
    public int lost = 16;
    public float minDistance = 14f;          // nobody starts on top of you
    public float maxDistance = 110f;
    public float calmFor = 40f;              // the Damned ignore you for this long after the city wakes

    void Start()
    {
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
            if (isLost) { if (lostModel != null) SpawnLost(at); }
            else
            {
                var go = Instantiate(zombiePrefab, at, Quaternion.Euler(0f, Random.value * 360f, 0f), transform);
                go.name = "Damned (walking)";
                go.SetActive(true);
                var ai = go.GetComponent<ZombieAI>();
                if (ai == null) continue;
                ai.dormantChance = 0f;
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

    // the Lost: the female floating pose in the ghost blue, drifting over the street (LostSoul drifts and flickers her)
    void SpawnLost(Vector3 at)
    {
        var go = Instantiate(lostModel, at + Vector3.up * Random.Range(3.5f, 11f), Quaternion.Euler(0f, Random.value * 360f, 0f), transform);
        go.name = "Lost (floating)";
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (ghost != null) { var mats = r.sharedMaterials; for (int k = 0; k < mats.Length; k++) mats[k] = ghost; r.sharedMaterials = mats; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var anim = go.GetComponentInChildren<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = floatController;
        anim.applyRootMotion = false;
        var soul = go.AddComponent<LostSoul>();
        soul.driftRadius = Random.Range(4f, 9f);
        soul.driftSpeed = Random.Range(.4f, .8f);
    }
}
