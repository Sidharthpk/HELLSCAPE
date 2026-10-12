using UnityEngine;
using UnityEngine.Events;

// Walk on down the first street and it wakes up (onDone: the zombie spawner). There used to be a cutscene here, the
// camera turning from one of the Lost to the next while you asked where you were: removed, Tomas's cutscene on waking
// (RevisedPlotDirector.OpeningMark) does that job now. Played by the Deer Zombie Trigger (the name is left over from
// when a zombie sat eating a deer here).
public class DeerCutscene : MonoBehaviour
{
    // (what the cutscene used: still wired in the scene and by HellCityBuilder, nothing reads them)
    public float lookRange = 70f;
    public Transform player;
    public Camera playerCam;
    public Behaviour[] lockDuring;
    public GameObject[] hideDuring;
    public RectTransform letterboxTop;
    public RectTransform letterboxBottom;
    public float letterboxHeight = 130f;
    public ScreenFader fader;
    public float fogDuring = 0.012f;

    public UnityEvent onDone;

    bool played;

    public void Play()
    {
        if (played) return;
        played = true;
        onDone?.Invoke();
    }
}
