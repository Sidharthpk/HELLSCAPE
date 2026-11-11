using UnityEngine;

// Streams the burning trees' Particle Pack fires (WildFire up top, FireBall low): only those within `distance` of the camera are on.
public class TreeFireLOD : MonoBehaviour
{
    public GameObject[] fires;       // one per tree, positioned at the tree
    public float distance = 90f;     // switch on inside this, off beyond distance + 15 (no flicker at the edge)

    Vector3[] pos;
    float next;

    void Start()
    {
        pos = new Vector3[fires.Length];
        for (int i = 0; i < fires.Length; i++) pos[i] = fires[i].transform.position;
    }

    void Update()
    {
        if (Time.time < next) return;
        next = Time.time + 0.4f;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 c = cam.transform.position;
        for (int i = 0; i < fires.Length; i++)
        {
            if (fires[i] == null) continue;
            float d = (pos[i] - c).magnitude;
            bool on = fires[i].activeSelf ? d < distance + 15f : d < distance;
            if (on != fires[i].activeSelf) fires[i].SetActive(on);
        }
    }
}
