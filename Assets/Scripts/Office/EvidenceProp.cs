using UnityEngine;

// A paper lying on a surface that is read with F ([J] keeps it). Used for the V5.1 truth evidence (OfficeWitness, SiloEncounter).
public static class EvidenceProp
{
    // `onFirstRead` runs once, the first time it is read. Returns null when there is no floor/desk under `near`.
    public static GameObject Spawn(Vector3 near, string name, string title, string body, string account, string line, System.Action onFirstRead)
    {
        if (!Physics.Raycast(near + Vector3.up * 2f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)) return null;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = hit.point + Vector3.up * .003f;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        go.transform.localScale = new Vector3(.22f, .004f, .3f);
        go.GetComponent<Renderer>().material.color = new Color(.8f, .76f, .65f);
        var note = go.AddComponent<ReadableNote>();
        note.title = title; note.body = body; note.afterLine = line;
        var use = go.AddComponent<Interactable>();
        use.once = false; use.prompt = "Read the " + title.ToLowerInvariant();
        bool read = false;
        use.onUse.AddListener(() =>
        {
            if (read) return;
            read = true;
            if (!string.IsNullOrEmpty(account)) EvidenceJournal.Record(title, account);
            onFirstRead?.Invoke();
        });
        return go;
    }
}
