using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// The way-finding for a drive (the map is huge): a round top-down map with you as a red arrow and the place you're
// headed as a gold dot (pinned to the rim when it is further than the map shows; the stops after it as small dim
// dots), and a marker with the distance over that place in the world, pinned to the edge of the screen when it is off
// to the side or behind. Target is whatever the owner points it at: DeliveryRun's beacon in the prologue (bottom
// right, where the speedometer was), EscapeRoute's next checkpoint in the hell city (top right).
public class DeliveryHud : MonoBehaviour
{
    public static Transform Target;

    // what the owner supplies
    public Func<bool> Show = () => true;                    // false: hidden (cutscenes, menus...)
    public Func<Transform> Me;                              // what the arrow follows: the van or the person
    public Func<bool> Driving = () => false;                // zoomed out and the van's camera for the marker
    public Func<Camera> Cam;                                // the camera the world marker is drawn through
    public Func<IEnumerable<Vector3>> Further;              // the stops after Target, for the map
    public bool topRight;                                   // else bottom right

    const float Size = 250f, WalkRadius = 70f, DriveRadius = 130f, MarkerMargin = 70f, HideWithin = 6f;
    static readonly Color Rim = new Color(.35f, .03f, .03f, 1f), Back = new Color(.03f, .02f, .02f, 1f), You = new Color(1f, .08f, .08f, 1f), Gold = new Color(1f, .78f, .3f, 1f), Dim = new Color(1f, .78f, .3f, .55f);

    Camera mapCam; RenderTexture mapTex;
    RectTransform canvasRect, mini, youArrow, blip, marker; TextMeshProUGUI markerText;
    readonly List<RectTransform> dots = new List<RectTransform>();
    Sprite disc;
    float radius = WalkRadius; int frame;

    void Start()
    {
        mapTex = new RenderTexture(512, 512, 16) { name = "Delivery map" };
        var camGo = new GameObject("Map camera");
        camGo.transform.SetParent(transform, false);
        mapCam = camGo.AddComponent<Camera>();
        mapCam.orthographic = true; mapCam.targetTexture = mapTex; mapCam.enabled = false;   // (drawn by hand a few times a second)
        mapCam.clearFlags = CameraClearFlags.SolidColor; mapCam.backgroundColor = Back;
        mapCam.nearClipPlane = 1f; mapCam.farClipPlane = 600f; mapCam.allowHDR = false; mapCam.allowMSAA = false;
        mapCam.cullingMask &= ~(1 << 2);   // (not the hell mist, which sits on Ignore Raycast)
        var data = camGo.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false; data.renderShadows = false; data.antialiasing = AntialiasingMode.None;

        var canvasGo = new GameObject("Delivery HUD", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var c = canvasGo.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 60;
        var s = canvasGo.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920f, 1080f); s.matchWidthOrHeight = 1f;
        canvasRect = (RectTransform)canvasGo.transform;

        disc = OfficeMinimap.Disc();
        var rim = Make<Image>("Map", canvasGo.transform, new Vector2(Size + 12f, Size + 12f)); rim.sprite = disc; rim.color = Rim;
        var corner = topRight ? Vector2.one : new Vector2(1f, 0f);
        rim.rectTransform.anchorMin = rim.rectTransform.anchorMax = rim.rectTransform.pivot = corner;
        rim.rectTransform.anchoredPosition = topRight ? new Vector2(-36f, -130f) : new Vector2(-36f, 88f);
        mini = rim.rectTransform;
        var face = Make<Image>("Face", mini, new Vector2(Size, Size)); face.sprite = disc; face.color = Color.white;
        face.gameObject.AddComponent<Mask>();
        var map = Make<RawImage>("Picture", face.transform, new Vector2(Size, Size)); map.texture = mapTex;
        var dot = Make<Image>("Target", mini, new Vector2(22f, 22f)); dot.sprite = disc; dot.color = Gold; blip = dot.rectTransform;
        var arrow = Make<Image>("You", mini, new Vector2(30f, 30f)); arrow.sprite = OfficeMinimap.Arrow(); arrow.color = You; youArrow = arrow.rectTransform;

        var m = Make<Image>("Marker", canvasGo.transform, new Vector2(34f, 34f)); m.color = Gold;
        m.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);   // a diamond
        marker = m.rectTransform;
        var t = Make<TextMeshProUGUI>("Distance", canvasGo.transform, new Vector2(200f, 40f));
        if (GameFont.Main != null) t.font = GameFont.Main;
        t.fontSize = 26f; t.alignment = TextAlignmentOptions.Center; t.color = Gold; t.textWrappingMode = TextWrappingModes.NoWrap;
        markerText = t;
        SetShown(false);
    }

    void OnDestroy() { if (mapTex != null) mapTex.Release(); }

    void SetShown(bool on)
    {
        if (mini.gameObject.activeSelf != on) mini.gameObject.SetActive(on);
        if (markerText != null && !on) { marker.gameObject.SetActive(false); markerText.gameObject.SetActive(false); }
    }

    // onto the dial: the offset in metres, kept inside the rim
    Vector2 OnMap(Vector3 off, float rimAt)
    {
        Vector2 p = new Vector2(off.x, off.z) * (Size / (2f * radius));
        return p.magnitude > rimAt ? p.normalized * rimAt : p;
    }

    void LateUpdate()
    {
        Transform me = Me != null ? Me() : null;
        if (me == null) { SetShown(false); return; }
        bool show = Show == null || Show();
        SetShown(show);
        if (!show) return;

        bool driving = Driving != null && Driving();
        Vector3 pos = me.position;
        radius = Mathf.MoveTowards(radius, driving ? DriveRadius : WalkRadius, Time.unscaledDeltaTime * 60f);

        // the picture: straight down over you, north up
        if (frame++ % 4 == 0)
        {
            mapCam.orthographicSize = radius;
            mapCam.transform.SetPositionAndRotation(pos + Vector3.up * 250f, Quaternion.Euler(90f, 0f, 0f));
            // (a lit-up, fog-free picture: the hell city's own light is far too dark to read a map by)
            float amb = RenderSettings.ambientIntensity; bool fog = RenderSettings.fog;
            RenderSettings.ambientIntensity = Mathf.Max(amb, 2.4f); RenderSettings.fog = false;
            mapCam.Render();
            RenderSettings.ambientIntensity = amb; RenderSettings.fog = fog;
        }
        Camera cam = Cam != null ? Cam() : null;
        float yaw = driving || cam == null ? me.eulerAngles.y : cam.transform.eulerAngles.y;
        youArrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);

        float rimAt = Size / 2f - 11f;
        bool hasTarget = Target != null && Target.gameObject.activeInHierarchy;
        blip.gameObject.SetActive(hasTarget);

        // the stops after it: small dim dots (only those on the dial)
        int n = 0;
        if (Further != null)
            foreach (var p in Further())
            {
                Vector3 off = p - pos;
                if (new Vector2(off.x, off.z).magnitude > radius * 0.98f) continue;
                if (n == dots.Count)
                {
                    var d = Make<Image>("Next stop", mini, new Vector2(12f, 12f)); d.sprite = disc; d.color = Dim;
                    d.transform.SetSiblingIndex(blip.GetSiblingIndex());   // under the target and the arrow
                    dots.Add(d.rectTransform);
                }
                dots[n].gameObject.SetActive(true);
                dots[n++].anchoredPosition = OnMap(off, rimAt);
            }
        for (int i = n; i < dots.Count; i++) dots[i].gameObject.SetActive(false);

        if (!hasTarget) { marker.gameObject.SetActive(false); markerText.gameObject.SetActive(false); return; }

        Vector3 to = Target.position - pos;
        blip.anchoredPosition = OnMap(to, rimAt);

        // the marker in the world
        float dist = new Vector2(to.x, to.z).magnitude;
        bool near = dist < HideWithin;
        marker.gameObject.SetActive(cam != null && !near); markerText.gameObject.SetActive(cam != null && !near);
        if (cam == null || near) return;
        Vector3 sp = cam.WorldToScreenPoint(Target.position + Vector3.up * 3f);
        if (sp.z < 0f) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; sp.x = sp.x < Screen.width / 2f ? 0f : Screen.width; }   // behind: out at whichever side it is nearer
        float k = canvasRect.rect.height / Screen.height;   // (the canvas scales with the height)
        float w = canvasRect.rect.width, h = canvasRect.rect.height;
        Vector2 at = new Vector2(sp.x * k - w / 2f, sp.y * k - h / 2f);
        at.x = Mathf.Clamp(at.x, -w / 2f + MarkerMargin, w / 2f - MarkerMargin);
        at.y = Mathf.Clamp(at.y, -h / 2f + MarkerMargin, h / 2f - MarkerMargin);
        marker.anchoredPosition = at;
        markerText.rectTransform.anchoredPosition = at + new Vector2(0f, -46f);
        markerText.text = Mathf.RoundToInt(dist) + " m";
    }

    static T Make<T>(string name, Transform parent, Vector2 size) where T : Graphic
    {
        var g = new GameObject(name, typeof(RectTransform)).AddComponent<T>();
        g.transform.SetParent(parent, false);
        g.rectTransform.sizeDelta = size;
        g.raycastTarget = false;
        return g;
    }
}
