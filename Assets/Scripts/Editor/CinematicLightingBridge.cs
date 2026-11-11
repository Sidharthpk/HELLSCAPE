using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Local opt-in editor review bridge. Only named review actions, no arbitrary code execution.
[InitializeOnLoad]
public static class CinematicLightingBridge
{
    [Serializable] public class Request { public string id, action, name; }
    static readonly string Input = "Temp/CinematicLightingCommand.json";
    static double next;
    static string last;
    static CinematicLightingBridge() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
        next = EditorApplication.timeSinceStartup + .5;
        if (!File.Exists(Input)) return;
        Request request;
        try { request = JsonUtility.FromJson<Request>(File.ReadAllText(Input)); } catch { return; }
        if (request == null || request.id == last) return;
        last = request.id;
        File.Delete(Input); // Consume before domain reloads; an import must not replay an action.
        Directory.CreateDirectory(CinematicLightingReview.Output);
        try
        {
            string report = "";
            switch(request.action)
            {
                case "open": EditorSceneManager.OpenScene("Assets/Scenes/MainGameScene.unity"); break;
                case "prototype": CinematicAtmosphereSetup.Prototype(); break;
                case "city": CinematicAtmosphereSetup.City(); break;
                case "play": CinematicLightingReview.Start(request.name); break;
                case "stop": EditorApplication.isPlaying = false; break;
                case "capture": CinematicLightingReview.Capture(request.name); break;
                case "position": CinematicLightingReview.Position(); break;
                case "status": report = Status(); break;
                case "validate": CinematicLightingValidation.Start(false); break;
                case "measure": CinematicLightingValidation.Start(true); break;
                case "motion": CinematicLightingValidation.StartMotion(); break;
                case "encounter": CinematicLightingValidation.StartEncounter(); break;
                case "echo": CinematicLightingValidation.StartEcho(); break;
                case "checkpoints": SessionState.SetBool(CinematicLightingReview.BackgroundKey,true); CheckpointRetryTest.Run(); break;
                case "refresh": AssetDatabase.Refresh(); break;
                default: throw new ArgumentException("Unknown lighting review action");
            }
            File.WriteAllText(CinematicLightingReview.Output + "/bridge-result.txt", request.id + " OK " + request.action + "\n" + report);
        }
        catch(Exception e) { File.WriteAllText(CinematicLightingReview.Output + "/bridge-result.txt", request.id + " FAILED " + e); Debug.LogException(e); }
    }
    static string Status()
    {
        var scene = EditorSceneManager.GetActiveScene();
        string report = "Project=" + Application.dataPath + "\nScene=" + scene.path + "\nPlaying=" + Application.isPlaying + "\nTime=" + Time.time + "\n";
        var city = UnityEngine.Object.FindFirstObjectByType<DeadCityAtmosphere>();
        if(city != null) report += "Atmosphere=" + city.State + "\nAllows=" + city.Allows(city.playerCamera) + "\n";
        report += "Fog=" + RenderSettings.fogMode + " " + RenderSettings.fogDensity + " " + RenderSettings.fogColor + "\nAmbient=" + RenderSettings.ambientMode + "\n";
        foreach(var c in Resources.FindObjectsOfTypeAll<Camera>().Where(c=>c.gameObject.scene.isLoaded)) report += c.name + " active=" + c.isActiveAndEnabled + " at=" + c.transform.position + "\n";
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/DeadCityScattering.shader");
        foreach(var message in ShaderUtil.GetShaderMessages(shader)) report += "Shader="+message.message+"\n";
        return report;
    }
}
