using UnityEngine;

// Actual captured images on the existing terminal: the apparition only exists during capture.
public class OfficeCctvMemory : MonoBehaviour
{
    SequencePuzzle[] tapes;
    Transform player, template;
    Camera playerCamera, capture;
    RenderTexture frame;
    Material screen;
    GameObject panel;
    public void Initialize(SequencePuzzle[] terminals, Transform who, Camera view, Transform body)
    {
        tapes = terminals; player = who; playerCamera = view; template = body;
        if (tapes.Length == 0) return;
        capture = new GameObject("CCTV memory camera").AddComponent<Camera>();
        capture.transform.SetParent(transform); capture.enabled = false;
        capture.fieldOfView = 65f; capture.nearClipPlane = .08f; capture.farClipPlane = 75f;
        frame = new RenderTexture(512, 288, 24); frame.name = "Impossible CCTV"; frame.wrapMode = TextureWrapMode.Repeat; frame.Create();
        capture.targetTexture = frame;
        screen = new Material(Shader.Find("Universal Render Pipeline/Unlit")); screen.mainTexture = frame;
        panel = GameObject.CreatePrimitive(PrimitiveType.Quad); panel.name = "Impossible CCTV feed";
        Destroy(panel.GetComponent<Collider>());
        panel.transform.SetParent(tapes[0].transform, false);
        // the Screen cube spans local z -.13..+.03 and y 1.28..2.70: sit just in front of its face, centred on it
        panel.transform.localPosition = new Vector3(0f, 1.99f, -.14f);
        panel.transform.localScale = new Vector3(1.9f, 1.07f, 1f);
        panel.GetComponent<Renderer>().sharedMaterial = screen;
        panel.SetActive(false);
    }
    public void ShowStep(int step)
    {
        if (capture == null || step < 1 || step > 3) return;
        panel.SetActive(false);
        Vector3 floor = player.position - Vector3.up;
        var arrival = GameObject.Find("OfficeArrival");
        Vector3 at = step == 2 ? player.position - player.forward * 3f + player.right * .65f + Vector3.up * 1.2f
            : arrival != null ? arrival.transform.position + arrival.transform.forward * 3f + Vector3.up : playerCamera.transform.position;
        Vector3 target = step == 2 ? player.position + Vector3.up * .3f : at + (arrival != null ? arrival.transform.forward : player.forward) * 10f;
        capture.transform.SetPositionAndRotation(at, Quaternion.LookRotation(target - at));
        GameObject figure = null;
        if (template != null && step < 3)
        {
            figure = OfficeBodies.Clone(template);
            Vector3 position = step == 2 ? floor - player.forward * 1.1f - player.right * .5f : at + capture.transform.forward * 6f - Vector3.up * 1.8f;
            figure.transform.SetPositionAndRotation(position, Quaternion.LookRotation(at - position));
            foreach (var r in figure.GetComponentsInChildren<SkinnedMeshRenderer>()) r.sharedMaterial = WitnessLook.Load("BurntMale");
        }
        GameObject doubleBody = null;
        var hidden = new System.Collections.Generic.List<Renderer>();
        foreach (var r in player.GetComponentsInChildren<Renderer>()) if (r.enabled) { hidden.Add(r); r.enabled = false; }
        if (step == 2 && template != null) {
            doubleBody = OfficeBodies.Clone(template); doubleBody.transform.SetPositionAndRotation(floor, player.rotation);
            foreach (var r in doubleBody.GetComponentsInChildren<SkinnedMeshRenderer>()) r.sharedMaterial = WitnessLook.Load("IntactMale");
        }
        try { capture.Render(); }
        finally {
            foreach (var r in hidden) if (r != null) r.enabled = true;
            if (doubleBody != null) { doubleBody.SetActive(false); Destroy(doubleBody); }
        }
        screen.mainTextureScale = step == 3 ? new Vector2(2f, 2f) : Vector2.one;
        if (figure != null) { figure.SetActive(false); Destroy(figure); }
        panel.SetActive(true);
        if (tapes[0].display != null)
        {
            // Keep captions below the image instead of obscuring the impossible feed.
            tapes[0].display.transform.localPosition = new Vector3(0f, 1.36f, -.14f);
            tapes[0].display.fontSize = .38f;
            tapes[0].display.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        }
    }
    void Update()
    {
        if (panel != null && RevisedPlotDirector.Instance != null && !RevisedPlotDirector.Instance.Echo) panel.SetActive(false);
    }
    void OnDestroy()
    {
        if (frame != null) { frame.Release(); Destroy(frame); }
        if (screen != null) Destroy(screen);
    }
}
