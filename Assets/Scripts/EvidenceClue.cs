using UnityEngine;

public class EvidenceClue : MonoBehaviour
{
    public string evidenceId;
    public bool prologueLabel;
    void Start()
    {
        var item = GetComponent<Interactable>();
        if (item != null) item.onUse.AddListener(Collect);
    }
    public void Collect()
    {
        if (prologueLabel) EvidenceJournal.ContainerInspected = true;
        else EvidenceJournal.Collect(evidenceId);
    }
}
