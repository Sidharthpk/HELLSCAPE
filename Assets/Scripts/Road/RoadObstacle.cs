using UnityEngine;

// Anything on the road that hurts the car when hit. Added automatically by RoadObstacleSpawner,
// or put it on hand-placed props yourself.
public class RoadObstacle : MonoBehaviour
{
    public float damage = 10f;
    public float hitCooldown = 1f;   // one hit per bump, not one per contact frame

    private float lastHitTime = -999f;

    // true if this contact should count as a new hit
    public bool TryHit()
    {
        if (Time.time - lastHitTime < hitCooldown) return false;
        lastHitTime = Time.time;
        return true;
    }
}
