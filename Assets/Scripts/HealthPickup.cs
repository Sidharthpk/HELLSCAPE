using UnityEngine;

// A glowing pickup: walk into it to heal. Spins and bobs so it reads from across the bridge.
public class HealthPickup : MonoBehaviour
{
    public float amount = 40f;
    public string line = "";                // optional line when picked up

    private Vector3 start;

    void OnEnable() => start = transform.position;

    void Update()
    {
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        transform.position = start + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.15f;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        var ph = other.GetComponentInParent<PlayerHealth>();
        if (ph == null || ph.currentHealth >= ph.maxHealth) return;
        ph.Heal(amount);
        if (!string.IsNullOrEmpty(line) && DialogueBox.Instance != null) DialogueBox.Instance.Say(line);
        Destroy(gameObject);
    }
}
