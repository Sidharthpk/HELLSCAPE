using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// In-memory progress for a run. Scene doors rebuild the destination, but must not rebuild its puzzles/pickups.
public interface ISceneProgress
{
    object CaptureProgress();
    void RestoreProgress(object state);
}

public static class SceneProgress
{
    static readonly Dictionary<string, object> states = new Dictionary<string, object>();
    static float? health;
    static object weapon;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { states.Clear(); health = null; weapon = null; }

    static string Key(Component component)
    {
        string path = "";
        for (var t = component.transform; t != null; t = t.parent)
            path = "/" + t.name + "[" + t.GetSiblingIndex() + "]" + path;
        return component.gameObject.scene.name + path + ":" + component.GetType().FullName;
    }

    public static void Capture(Scene scene, Transform player)
    {
        foreach (var component in RevisedPlot.All<MonoBehaviour>(scene))
            if (component is ISceneProgress progress) states[Key(component)] = progress.CaptureProgress();
        var hp = player != null ? player.GetComponent<PlayerHealth>() : null;
        health = hp != null ? hp.currentHealth : (float?)null;
        var guns = RevisedPlot.All<GunController>(scene);
        weapon = GunController.Equipped && guns.Length > 0 ? guns[0].CaptureLoadout() : null;
    }

    public static void Restore(Scene scene, Transform player)
    {
        // Power before keypads, then door poses: restoring never replays the CCTV/encounter cutscenes.
        var components = RevisedPlot.All<MonoBehaviour>(scene);
        foreach (var component in components)
            if (component is PuzzleCounter && states.TryGetValue(Key(component), out var state))
                ((ISceneProgress)component).RestoreProgress(state);
        foreach (var component in components)
            if (component is ISceneProgress progress && !(component is PuzzleCounter) && !(component is FuseBox) && !(component is KesslerDoor) && states.TryGetValue(Key(component), out var state))
                progress.RestoreProgress(state);
        // Pickups hide on use; inserted fuses must then be shown in their slots. Door poses settle last.
        foreach (var component in components)
            if (component is ISceneProgress progress && (component is FuseBox || component is KesslerDoor) && states.TryGetValue(Key(component), out var state))
                progress.RestoreProgress(state);
        var hp = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (hp != null && health.HasValue) hp.Revive(Mathf.Clamp(health.Value, 1f, hp.maxHealth));
        var guns = RevisedPlot.All<GunController>(scene);
        if (weapon != null && guns.Length > 0) guns[0].RestoreLoadout(weapon);
        health = null;
        weapon = null;
    }
}
