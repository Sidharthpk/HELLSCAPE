using UnityEngine;

// The centre dot (BA_Simple Cursor) shows only while you walk about in control: never in a cutscene, the van or a fade.
[RequireComponent(typeof(Canvas))]
public class GameplayOnlyCursor : MonoBehaviour
{
    Canvas canvas;
    PrologueDirector d;

    void Awake() { canvas = GetComponent<Canvas>(); d = FindFirstObjectByType<PrologueDirector>(FindObjectsInactive.Include); }

    void LateUpdate()
    {
        bool playing = d != null && d.fpc != null && d.fpc.isActiveAndEnabled && d.fpc.playerCanMove && d.fpc.cameraCanMove
            && d.playerCam != null && d.playerCam.isActiveAndEnabled
            && (d.cine == null || !d.cine.isActiveAndEnabled)
            && !CutsceneHUD.Hidden && !EndingSequence.Playing;
        if (canvas.enabled != playing) canvas.enabled = playing;
    }
}
