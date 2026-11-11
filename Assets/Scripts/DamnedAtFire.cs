using UnityEngine;
using UnityEngine.AI;

// A Damned loitering at a burning tree: while it is calm and standing still, it turns to look at the flames.
public class DamnedAtFire : MonoBehaviour
{
    public Vector3 fire;
    ZombieAI ai;
    NavMeshAgent agent;

    void Start() { ai = GetComponent<ZombieAI>(); agent = GetComponent<NavMeshAgent>(); }

    void Update()
    {
        if (ai == null || agent == null || !ai.Calm || agent.velocity.sqrMagnitude > 0.02f) return;
        Vector3 to = fire - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 2f * Time.deltaTime);
    }
}
