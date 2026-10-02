using System.Collections.Generic;
using UnityEngine;

// Scatters random obstacles along the road each playthrough.
// Place empty GameObjects along the middle of the road, in driving order, and drag them into roadPoints.
public class RoadObstacleSpawner : MonoBehaviour
{
    public Transform[] roadPoints;
    public float roadWidth = 8f;
    public int count = 30;
    public float minSpacing = 6f;

    [System.Serializable]
    public class ObstacleType
    {
        public GameObject prefab;           // barrel, cone, wrecked car, body bag... (models work too)
        public float damage = 10f;
        public bool pushable = false;       // light props get knocked around, heavy ones stay put
        public float mass = 50f;
        [Range(0f, 1f)] public float weight = 1f; // relative spawn chance
    }
    public ObstacleType[] obstacles;

    [Header("Placement")]
    public LayerMask groundMask = ~0;
    public Transform keepClearOf;           // e.g. the car, so nothing spawns on top of it
    public float keepClearRadius = 12f;
    public BoxCollider[] noSpawnZones;      // e.g. the tunnel: nothing spawns inside these boxes

    private readonly List<Vector3> placed = new List<Vector3>();

    void Start()
    {
        if (roadPoints == null || roadPoints.Length < 2 || obstacles.Length == 0) return;

        int attempts = count * 10;
        while (placed.Count < count && attempts-- > 0)
            TrySpawn();
    }

    void TrySpawn()
    {
        // random point on a random road segment, offset sideways within the road width
        int i = Random.Range(0, roadPoints.Length - 1);
        Vector3 a = roadPoints[i].position, b = roadPoints[i + 1].position;
        Vector3 along = Vector3.Lerp(a, b, Random.value);
        Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized);
        Vector3 pos = along + side * Random.Range(-roadWidth * 0.5f, roadWidth * 0.5f);

        if (keepClearOf != null && Vector3.Distance(pos, keepClearOf.position) < keepClearRadius) return;
        if (noSpawnZones != null)
            foreach (var z in noSpawnZones)
                if (z != null && z.ClosestPoint(pos) == pos) return; // inside the box
        foreach (var p in placed)
            if (Vector3.Distance(p, pos) < minSpacing) return;

        // cast from just above the road point so tunnels/bridges don't catch it on the roof
        if (!Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, groundMask, QueryTriggerInteraction.Ignore))
            return;

        ObstacleType type = PickType();
        if (type == null || type.prefab == null) return;

        // fit the collider unrotated, then spin it, so the box matches the mesh
        GameObject go = Instantiate(type.prefab, hit.point, Quaternion.identity, transform);
        Setup(go, type, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        placed.Add(pos);
    }

    ObstacleType PickType()
    {
        float total = 0f;
        foreach (var o in obstacles) total += o.weight;
        float r = Random.value * total;
        foreach (var o in obstacles)
        {
            r -= o.weight;
            if (r <= 0f) return o;
        }
        return obstacles[obstacles.Length - 1];
    }

    void Setup(GameObject go, ObstacleType type, Quaternion rot)
    {
        // raw models have no collider: fit a box around the visible mesh
        if (go.GetComponentInChildren<Collider>() == null)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(bounds.center);
                box.size = go.transform.InverseTransformVector(bounds.size);
                box.size = new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), Mathf.Abs(box.size.z));
            }
        }

        go.transform.rotation = rot;

        // sit on the ground instead of half-buried
        Collider col = go.GetComponentInChildren<Collider>();
        if (col != null)
        {
            Physics.SyncTransforms();
            float lift = go.transform.position.y - col.bounds.min.y;
            go.transform.position += Vector3.up * lift;
        }

        if (type.pushable && go.GetComponent<Rigidbody>() == null)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = type.mass;
        }

        var obstacle = go.GetComponent<RoadObstacle>();
        if (obstacle == null) obstacle = go.AddComponent<RoadObstacle>();
        obstacle.damage = type.damage;
    }

    void OnDrawGizmosSelected()
    {
        if (roadPoints == null) return;
        Gizmos.color = Color.red;
        for (int i = 0; i < roadPoints.Length - 1; i++)
        {
            if (roadPoints[i] == null || roadPoints[i + 1] == null) continue;
            Vector3 a = roadPoints[i].position, b = roadPoints[i + 1].position;
            Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized) * roadWidth * 0.5f;
            Gizmos.DrawLine(a + side, b + side);
            Gizmos.DrawLine(a - side, b - side);
        }
    }
}
