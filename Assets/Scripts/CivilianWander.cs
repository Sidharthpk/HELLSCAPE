using UnityEngine;
using UnityEngine.AI;

// Day-time pedestrians: stroll to random points on the NavMesh, pause, repeat.
// Put them under the SleepSequence's dayWorld so they vanish when you wake up.
public class CivilianWander : MonoBehaviour
{
    public float wanderRadius = 20f;
    public Vector2 speedRange = new Vector2(1f, 1.6f);
    public Vector2 pauseRange = new Vector2(1f, 5f);
    public Animator animator;               // optional, expects a "Speed" float

    private NavMeshAgent agent;
    private Vector3 home;
    private float pauseTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        agent.speed = Random.Range(speedRange.x, speedRange.y);
        home = transform.position;
        PickDestination();
    }

    void Update()
    {
        if (!agent.isOnNavMesh) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            pauseTimer -= Time.deltaTime;
            if (pauseTimer <= 0f) PickDestination();
        }

        if (animator != null) animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    void PickDestination()
    {
        pauseTimer = Random.Range(pauseRange.x, pauseRange.y);
        Vector3 target = home + Random.insideUnitSphere * wanderRadius;
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }
}
