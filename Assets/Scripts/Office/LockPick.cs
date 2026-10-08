using System;
using UnityEngine;

// A lockpicking puzzle drawn with IMGUI (no prefab). Mouse X / A-D swings the pick, hold SPACE to turn the lock: it
// only gives all the way when the pick sits in the hidden sweet spot (the nearer you are, the further it turns).
// Forcing a jammed lock snaps the pick; out of picks = failed. Esc walks away. Movement/look are held off meanwhile.
public class LockPick : MonoBehaviour
{
    public static bool Active { get; private set; }

    public int picksPerTry = 3;
    public float window = 5f;       // degrees either side of the sweet spot that opens it
    public float falloff = 30f;
    public float timeLimit = 30f;   // seconds before he's through; a snapped pick costs snapPenalty of them
    public float snapPenalty = 4f;
    public Action beat;             // called every few seconds (faster as time runs out): a blow on the door     // degrees over which the lock's give fades out

    bool ownsSession;
    public static int ClosedFrame { get; private set; } = -1;
    public void Cancel() { if (ownsSession) Finish(null); }

    Behaviour[] held; bool[] was;
    Action solved; Action<bool> failed;   // failed(true) = out of picks, failed(false) = walked away
    float timeLeft, nextBeat, sweet, angle, turn, strain, msgUntil;
    int picks;
    string msg = "";

    public void Begin(Behaviour[] lockDuring, Action onSolved, Action<bool> onFailed)
    {
        if (Active) return;
        solved = onSolved; failed = onFailed;
        held = lockDuring ?? new Behaviour[0];
        was = new bool[held.Length];
        for (int i = 0; i < held.Length; i++) if (held[i] != null) { was[i] = held[i].enabled; held[i].enabled = false; }
        sweet = UnityEngine.Random.Range(-65f, 65f);
        angle = turn = strain = 0f;
        picks = picksPerTry;
        timeLeft = timeLimit; nextBeat = timeLimit;
        msg = "";
        ownsSession = Active = true;
    }

    void Finish(Action then)
    {
        if (!ownsSession) return;
        ownsSession = Active = false;
        ClosedFrame = Time.frameCount;
        for (int i = 0; i < held.Length; i++) if (held[i] != null && was[i]) held[i].enabled = true;
        then?.Invoke();
    }

    void OnDisable() { if (ownsSession) Finish(null); }

    void Update()
    {
        if (!ownsSession || PauseMenu.Paused || GameOverScreen.Open) return;
        float dt = Time.unscaledDeltaTime;
        if (Input.GetKeyDown(KeyCode.Escape)) { Finish(() => failed?.Invoke(false)); return; }

        angle = Mathf.Clamp(angle + Input.GetAxis("Mouse X") * 3f + Input.GetAxisRaw("Horizontal") * 80f * dt, -90f, 90f);
        timeLeft -= dt;
        if (timeLeft <= nextBeat - 2.5f + (timeLeft < 10f ? 1.5f : 0f)) { nextBeat = timeLeft; beat?.Invoke(); }
        if (timeLeft <= 0f) { Say("He's in."); Finish(() => failed?.Invoke(true)); return; }
        float off = Mathf.Abs(angle - sweet);
        float give = off <= window ? 1f : 0.85f * Mathf.Clamp01(1f - (off - window) / falloff);

        if (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0))
        {
            turn = Mathf.MoveTowards(turn, 1f, 0.9f * dt);
            if (turn > give) { turn = give; strain += dt; } else strain = Mathf.Max(0f, strain - dt);
            if (turn >= 1f) { Say("Click."); Finish(solved); return; }
            if (strain > 0.9f)
            {
                strain = turn = 0f;
                if (--picks <= 0) { Say("No picks left."); Finish(() => failed?.Invoke(true)); return; }
                timeLeft -= snapPenalty;
                Say("The pick snapped.");
            }
        }
        else { turn = Mathf.MoveTowards(turn, 0f, 2.5f * dt); strain = 0f; }
    }

    void Say(string s) { msg = s; msgUntil = Time.unscaledTime + 1.5f; }

    void OnGUI()
    {
        if (!ownsSession || PauseMenu.Paused || GameOverScreen.Open) return;
        var white = Texture2D.whiteTexture;
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f, s = Mathf.Min(Screen.width, Screen.height) / 1080f * 260f;
        float panic = 1f - Mathf.Clamp01(timeLeft / timeLimit);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (4f + 10f * panic));
        GUI.color = new Color(0.12f * panic * (0.4f + pulse), 0f, 0f, 0.85f);   // black going blood red, throbbing
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);

        // the lock face, turning as it gives
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(turn * 90f, new Vector2(cx, cy));
        GUI.color = new Color(0.45f, 0.4f, 0.3f);
        GUI.DrawTexture(new Rect(cx - s * 0.5f, cy - s * 0.5f, s, s), white);
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(cx - s * 0.05f, cy - s * 0.3f, s * 0.1f, s * 0.35f), white);   // the keyway
        GUI.matrix = m;

        // the pick comes up from below, jittering when strained
        float jit = strain > 0f ? Mathf.Sin(Time.unscaledTime * 90f) * 2.5f * strain : 0f;
        GUIUtility.RotateAroundPivot(angle + jit, new Vector2(cx, cy + s * 0.05f));
        GUI.color = new Color(0.85f, 0.85f, 0.9f);
        GUI.DrawTexture(new Rect(cx - 3f, cy + s * 0.05f, 6f, s * 0.9f), white);
        GUI.matrix = m;
        GUI.color = Color.white;

        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(s * 0.09f) };
        style.normal.textColor = new Color(1f, 0.8f, 0.3f);
        GUI.Label(new Rect(0, cy - s * 0.95f, Screen.width, s * 0.2f), "LOCKPICK   -   picks: " + picks, style);
        style.fontSize = Mathf.RoundToInt(s * (0.2f + 0.04f * pulse * panic));
        style.normal.textColor = Color.Lerp(Color.white, Color.red, panic);
        GUI.Label(new Rect(0, cy - s * 1.3f, Screen.width, s * 0.35f), "HE'S AT THE DOOR   " + Mathf.Max(0f, timeLeft).ToString("0.0"), style);
        style.fontSize = Mathf.RoundToInt(s * 0.09f);
        style.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        GUI.Label(new Rect(0, cy + s * 1.1f, Screen.width, s * 0.2f), "Mouse / A-D: move the pick     Hold SPACE: turn the lock     Esc: back off", style);
        if (Time.unscaledTime < msgUntil)
        {
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(0, cy + s * 1.35f, Screen.width, s * 0.2f), msg, style);
        }
    }
}
