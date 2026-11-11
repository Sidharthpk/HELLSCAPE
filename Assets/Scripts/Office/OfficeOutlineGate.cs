using UnityEngine;

// The outline and PSX dither passes live on the shared renderer; this turns them up only while the player is inside the office zone.
public class OfficeOutlineGate : MonoBehaviour
{
    public Transform player;
    public Bounds zone;
    public Material outline, psx;
    public float psxAmount = 0.55f;
    public float strength = 0.6f;
    float now;

    void Update()
    {
        if (outline == null || player == null) return;
        now = Mathf.MoveTowards(now, zone.Contains(player.position) ? strength : 0f, Time.deltaTime * 2f);
        outline.SetFloat("_Strength", now);
        if (psx != null) psx.SetFloat("_Amount", psxAmount * now / strength);
    }

    void OnDisable() { if (outline != null) outline.SetFloat("_Strength", 0f); if (psx != null) psx.SetFloat("_Amount", 0f); }
}
