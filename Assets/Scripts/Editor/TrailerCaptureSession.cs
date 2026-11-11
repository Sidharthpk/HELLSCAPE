using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class TrailerCaptureSession
{
    const string Key = "TrailerCaptureSession";
    const string Root = "Logs/TrailerCapture";
    static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    static int lastFrame;
    static bool originalRunInBackground;
    static double deadline;

    public static void Begin()
    {
        SessionState.SetBool(Key, true);
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Unity is already playing.");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.delayCall += () =>
        {
            string request = Path.Combine(Root, "start.capture");
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            Begin();
        };
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            SessionState.EraseBool(Key);
            originalRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 540;
            Steps.Push(Run());
            EditorApplication.update += Tick;
        };
    }

    static void Log(string line) => File.AppendAllText(Path.Combine(Root, "capture.txt"), line + Environment.NewLine);

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Capture timed out.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            while (Steps.Count > 0)
            {
                var top = Steps.Peek();
                if (!top.MoveNext()) { Steps.Pop(); continue; }
                if (top.Current is IEnumerator child) { Steps.Push(child); continue; }
                return;
            }
            Log("Capture complete.");
            Stop();
        }
        catch (Exception error)
        {
            Log(error.ToString());
            Stop();
        }
    }

    static void Stop()
    {
        EditorApplication.update -= Tick;
        Application.runInBackground = originalRunInBackground;
        Time.captureFramerate = 0;
        Time.timeScale = 1f;
        EditorApplication.isPlaying = false;
    }

    static IEnumerator Wait(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
    }

    static void Put(Transform player, Vector3 position, Vector3 facing)
    {
        player.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
        var body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            body.rotation = player.rotation;
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }
        Physics.SyncTransforms();
    }

    static IEnumerator Run()
    {
        SleepSequence.WakeInHell = true;
        SceneManager.LoadScene("OfficeSilo");
        DialogueBox.AutoAdvanceAfter = 0.65f;
        yield return Wait(2f);
        var silo = Object.FindFirstObjectByType<SiloEncounter>(FindObjectsInactive.Include);
        var player = silo.player;
        var camera = player.GetComponentInChildren<Camera>();
        var leap = Object.FindObjectsByType<StoryTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(trigger => trigger.name.StartsWith("LeapTrigger"));
        var cp = Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(checkpoint => checkpoint.name == "Checkpoint - Office entrance");
        cp.Activate();
        Log("Office camera " + camera.name + " checkpoint " + cp.transform.position);
        Vector3 position = cp.transform.position;
        Vector3 facing = Vector3.ProjectOnPlane(cp.transform.forward, Vector3.up).normalized;
        var body = player.GetComponent<Rigidbody>();
        body.isKinematic = true;
        var palm = player.GetComponent<PalmLight>();
        if (palm != null) palm.Raise();
        yield return Capture("office_pov", 105, frame =>
        {
            float t = frame / 30f;
            Put(player, position + facing * (t * 0.64f) + Vector3.up * (Mathf.Sin(t * 9f) * .027f),
                Quaternion.Euler(1f, Mathf.Lerp(-10f, 13f, t / 3.5f), 0) * facing);
            return camera;
        });

        leap.gameObject.SetActive(false);
        body.isKinematic = false;
        Vector3 into = Vector3.ProjectOnPlane(silo.siloCenter.position - leap.transform.position, Vector3.up).normalized;
        Put(player, leap.transform.position - into * .16f, into);
        camera.transform.localRotation = Quaternion.identity;
        if (palm != null) palm.enabled = false;
        Log("Silo leap " + player.position + " center " + silo.siloCenter.position);
        yield return Capture("silo_leap", 147, frame =>
        {
            if (frame == 3) silo.Leap();
            return camera;
        });
        Log("After leap " + player.position);

        SleepSequence.WakeInHell = true;
        SleepSequence.FallSpot = null;
        SceneManager.LoadScene("MainGameScene");
        yield return Wait(4f);
        var boss = Object.FindFirstObjectByType<DemonBoss>(FindObjectsInactive.Include);
        var van = boss.carInteract;
        var fixedCamera = van.carCamera.GetComponent<FixedCameras>();
        var vanBody = van.GetComponent<Rigidbody>();
        van.SendMessage("EnterCar");
        Time.timeScale = 4f;
        double timeout = EditorApplication.timeSinceStartup + 120;
        while (!boss.IsChasing && EditorApplication.timeSinceStartup < timeout) yield return null;
        Log("Chase started " + boss.IsChasing + " at " + vanBody.position);
        if (!boss.IsChasing) throw new InvalidOperationException("The bridge chase did not start.");
        van.GetComponent<CarHealth>().damageScale = 0f;
        PlaytestDriver.DriveVan(17f, 0f, true);
        timeout = EditorApplication.timeSinceStartup + 160;
        while (fixedCamera.Along(vanBody.position) < 686 && PlaytestDriver.Busy && EditorApplication.timeSinceStartup < timeout)
            yield return null;
        Time.timeScale = 1f;
        Log("Bridge approach " + fixedCamera.Along(vanBody.position) + "m " + PlaytestDriver.Report);
        yield return Capture("bridge_run", 342, _ => van.carCamera);
        Log("Bridge end " + fixedCamera.Along(vanBody.position) + "m");
    }

    static IEnumerator Capture(string name, int count, Func<int, Camera> move)
    {
        string folder = Path.Combine(Root, name);
        Directory.CreateDirectory(folder);
        var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        var buffer = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        Time.captureFramerate = 30;
        Log("Recording " + name);
        try
        {
            for (int frame = 0; frame < count; frame++)
            {
                var camera = move(frame);
                var previousTarget = camera.targetTexture;
                var previousActive = RenderTexture.active;
                try
                {
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    buffer.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                }
                finally
                {
                    camera.targetTexture = previousTarget;
                    RenderTexture.active = previousActive;
                }
                File.WriteAllBytes(Path.Combine(folder, $"f{frame + 1:00000}.jpg"), buffer.EncodeToJPG(92));
                yield return null;
            }
        }
        finally
        {
            Time.captureFramerate = 0;
            Object.DestroyImmediate(buffer);
            Object.DestroyImmediate(target);
        }
        Log($"Wrote {count} frames of {name}.");
    }
}

