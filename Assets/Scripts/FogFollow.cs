using UnityEngine;

// Keeps a fog particle volume centred on whichever camera is rendering (on foot or in the car),
// so the mist is always around the player without filling the whole map.
public class FogFollow : MonoBehaviour
{
    public float heightOffset = -1.5f;   // relative to the camera

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.transform.position;
        transform.position = new Vector3(p.x, p.y + heightOffset, p.z);
    }
}
