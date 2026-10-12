using UnityEngine;
using UnityEngine.AI;

// There are no Damned in the office any more (the dead there are the colleagues, the Eye and the shadow figures of
// OfficeLurkers). What is left is the navmesh cut at Vane's door: while the door is shut, the Eye's navmesh is cut
// there too.
public class OfficeDamned : MonoBehaviour
{
    public Collider doorBlocker;
    public NavMeshObstacle doorCarve;

    void Update()
    {
        if (doorCarve != null && doorBlocker != null) doorCarve.enabled = doorBlocker.enabled;
    }
}
