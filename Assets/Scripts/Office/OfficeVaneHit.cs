using UnityEngine;

// Only attached to the office form. The silo boss keeps its own original damage path.
public sealed class OfficeVaneHit : MonoBehaviour
{
    public VaneHunt hunt;
    public bool Hit() => hunt != null && hunt.BulletHit();
}
