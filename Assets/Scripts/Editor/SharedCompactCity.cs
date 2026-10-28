using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class SharedCompactCity
{
    public static Vector3[] TrafficRoute()
    {
        var corners = new[] {new Vector3(-38,.71f,23),new Vector3(10,.82f,23),new Vector3(50,1.48f,23),new Vector3(90,2.4f,23),new Vector3(126.5f,3,23),new Vector3(245,5.7f,23),new Vector3(245,5.24f,-35),new Vector3(245,4.71f,-80),new Vector3(90,4.71f,-80),new Vector3(50,4.45f,-80),new Vector3(0,3.03f,-80),new Vector3(-38,2.7f,-80),new Vector3(-38,2,-40),new Vector3(-38,.76f,0),new Vector3(-38,.71f,23)};
        var points = new List<Vector3> { corners[0] };
        for (int i=1;i<corners.Length;i++)
        {
            int count=Mathf.CeilToInt(Vector3.Distance(corners[i-1],corners[i])/8f);
            for(int j=1;j<=count;j++) points.Add(Vector3.Lerp(corners[i-1],corners[i],j/(float)count));
        }
        return points.ToArray();
    }
    public static void TrimCollision(Mesh mesh)
    {
        var vertices=mesh.vertices;
        for(int sub=0;sub<mesh.subMeshCount;sub++)
        {
            var tri=mesh.GetTriangles(sub);var kept=new List<int>();
            for(int i=0;i<tri.Length;i+=3)
            {
                var p=(vertices[tri[i]]+vertices[tri[i+1]]+vertices[tri[i+2]])/3f;
                if(p.x < -480 || p.x > 600 || p.z < -360 || p.z > 270)continue;
                kept.Add(tri[i]);kept.Add(tri[i+1]);kept.Add(tri[i+2]);
            }
            mesh.SetTriangles(kept,sub);
        }
    }
    static Transform[] All(Scene s) => s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
    static Transform Find(Scene s, string n) => All(s).First(t => t.name == n);
    static T Get<T>(Scene s) where T : Component => s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).First();
    static GameObject Copy(Transform source, Scene scene, string name = null)
    {
        var go = Object.Instantiate(source.gameObject);
        go.name = name ?? source.name;
        go.transform.SetParent(null, true);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetPositionAndRotation(source.position, source.rotation);
        go.transform.localScale = source.lossyScale;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.lightmapIndex = -1;
        return go;
    }
    static void Pose(Transform target, Transform source)
    { target.SetPositionAndRotation(source.position, source.rotation); target.localScale = Vector3.Scale(target.localScale, new Vector3(source.lossyScale.x / target.lossyScale.x, source.lossyScale.y / target.lossyScale.y, source.lossyScale.z / target.lossyScale.z)); }
    static float Length(Transform[] points) => Enumerable.Range(1, points.Length - 1).Sum(i => Vector3.Distance(points[i - 1].position, points[i].position));
    static bool Old(Transform t) => t.name.StartsWith("сити исчерп") || t.name == "Street west (generated)" || t.name == "конт на скетч";
    static void RemoveOld(Scene s)
    {
        foreach (var t in All(s).Where(Old).ToArray()) if (t != null) Object.DestroyImmediate(t.gameObject);
        foreach (var t in All(s).Where(t => t.name == "Street barriers (generated)" || t.name == "Structure_02").ToArray())
        {
            // Retain legacy story references but remove their obsolete model and collision.
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) Object.DestroyImmediate(r);
            foreach (var f in t.GetComponentsInChildren<MeshFilter>(true)) Object.DestroyImmediate(f);
            foreach (var c in t.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            t.gameObject.SetActive(false); t.name = "Legacy story references (no scenery)";
        }
    }
    static void VisualOnly(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true)) if (c != null && c.GetType().Assembly == typeof(PrologueDirector).Assembly) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<NavMeshAgent>(true)) Object.DestroyImmediate(c);
    }

    [MenuItem("HellScape/City/Share compact layout with prologue")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        var main = SceneManager.GetSceneByName("MainGameScene");
        var pro = SceneManager.GetSceneByName("Prologue");
        if (!main.isLoaded) main = EditorSceneManager.OpenScene("Assets/Scenes/MainGameScene.unity", OpenSceneMode.Additive);
        if (!pro.isLoaded) pro = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive);
        if (pro.GetRootGameObjects().Any(g => g.name == "STATIC_MODELS")) throw new InvalidOperationException("Shared layout already installed.");
        SceneManager.SetActiveScene(main);
        var city = Find(main, "STATIC_MODELS");
        // Keep the complete central blocks at human scale; remove the unused multi-km ring.
        foreach (Transform part in city.GetComponentsInChildren<Transform>(true).First(t => t.name == "city_part_highway"))
            if (part.name != "city_part_highway1" && part.name != "city_part_highway2") part.gameObject.SetActive(false);
        foreach (var r in city.GetComponentsInChildren<Renderer>(true))
        {
            var p = r.bounds.center;
            if (p.x < -550 || p.x > 650 || p.z < -440 || p.z > 350)
            { r.enabled = false; foreach (var c in r.GetComponents<Collider>()) c.enabled = false; }
        }
        var horde = Get<HordeChase>(main);
        float before = Length(horde.road);
        var shape = new[] { new Vector3(118.5f,3,145), new Vector3(126.5f,3,115), new Vector3(126.5f,3,65), new Vector3(126.5f,3,35),
            new Vector3(120,3,25), new Vector3(90,2.4f,25), new Vector3(-38,.71f,25), new Vector3(-38,2.7f,-82),
            new Vector3(170,4.71f,-82), new Vector3(245,4.71f,-82), new Vector3(245,5.7f,25), new Vector3(245,5.7f,200),
            new Vector3(325,5.7f,200), new Vector3(405,5.2f,200) };
        // Keep corners exactly; distribute the remaining existing route transforms on straight stretches.
        var points = shape.ToList();
        while (points.Count < horde.road.Length)
        { int i = Enumerable.Range(0, points.Count - 1).OrderByDescending(k => Vector3.Distance(points[k], points[k + 1])).First(); points.Insert(i + 1, (points[i] + points[i + 1]) * .5f); }
        for (int i = 0; i < horde.road.Length; i++) horde.road[i].position = points[i];
        foreach (var cp in main.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Checkpoint>(true)).Where(c => c.name.Contains("Escape"))) cp.spawn.SetParent(Find(main, "Demo city - story connections"), true);
        Get<SleepSequence>(main).streetAnchor = null;
        RemoveOld(main);

        var shared = Copy(city, pro);
        Copy(Find(main, "Demo city - story connections"), pro);
        foreach (var n in new[] { "TEXTURES_FOR_SKETCH", "TEXTURES_FOR_SKETCH (1)", "bridge_Metal", "KesslerTunnel" }) VisualOnly(Copy(Find(main, n), pro));
        var proHome = Find(pro, "Old residential building (home)");
        Pose(proHome, Find(main, "Old residential building (home)"));
        foreach (var n in new[] { "NewOfficeBuilding Entrance", "modular_door_8" }) Pose(Find(pro, n), Find(main, n));
        var proSign = Find(pro, "Meshy_AI_KV_Logistics_1009173124_generate");
        Pose(proSign, Find(main, "Meshy_AI_KV_Logistics_1009173124_generate"));
        var director = Get<PrologueDirector>(pro);
        director.streetAnchor = shared.transform;
        director.homeCamAt = new Vector3(-38, 5, 29); director.homeLookAt = new Vector3(-31, 8, 60); director.groundY = .73f;
        var run = Get<DeliveryRun>(pro);
        run.depotBay.SetParent(Find(pro, "Deliveries"), true);
        run.depotBay.SetPositionAndRotation(new Vector3(118.5f,3,145), Quaternion.Euler(0,180,0));
        run.afterSpawn.SetPositionAndRotation(Find(main,"CityReturnPoint").position, Quaternion.Euler(0,180,0));
        director.fpc.transform.SetPositionAndRotation(run.afterSpawn.position, run.afterSpawn.rotation);
        run.van.transform.SetPositionAndRotation(new Vector3(-38,2.85f,-82), Quaternion.Euler(0,90,0));
        run.stops[0].bay.SetPositionAndRotation(new Vector3(-38,1,0), Quaternion.identity);
        run.stops[0].customer.transform.position = new Vector3(-48,1,0);
        run.stops[1].bay.SetPositionAndRotation(new Vector3(90,4.71f,-82), Quaternion.Euler(0,90,0));
        run.stops[1].customer.transform.SetPositionAndRotation(new Vector3(92,4.71f,-76), Quaternion.Euler(0,180,0));
        var scare = pro.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GrandmaScare>(true)).FirstOrDefault();
        if (scare != null) scare.transform.SetPositionAndRotation(new Vector3(-38,1.4f,-20), Quaternion.identity);
        RemoveOld(pro);
        // Old parked props and pedestrian routes belong to removed streets.
        foreach (var n in new[] { "Street extras" }) { var t = All(pro).FirstOrDefault(x=>x.name==n); if(t!=null)t.gameObject.SetActive(false); }
        int pedestrian = 0;
        foreach(var walker in pro.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PavementWalker>(true)))
        {
            int i = pedestrian++;
            walker.a = new Vector3(-20 + i % 6 * 35, 1 + i % 6 * .45f, i % 2 == 0 ? 16 : 34);
            walker.b = walker.a + Vector3.right * 22;
            walker.transform.position = Vector3.Lerp(walker.a,walker.b,walker.startAt);
        }
        foreach(var driver in pro.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LoopDriver>(true)))
            driver.path = TrafficRoute();
        EditorSceneManager.SaveScene(pro);
        EditorSceneManager.SaveScene(main);
        EditorSceneManager.CloseScene(pro,true);
        SceneManager.SetActiveScene(main);
        DemoCityMigration.FinishNavigation();
        Debug.Log("Escape driving distance " + before.ToString("F0") + "m -> " + Length(horde.road).ToString("F0") + "m. Shared city installed; old street models removed.");
    }

    public static string Capture(string sceneName, string file, Vector3 position, Vector3 target, float ortho = 0)
    {
        var s = EditorSceneManager.OpenScene("Assets/Scenes/"+sceneName+".unity",OpenSceneMode.Single);
        var fog = RenderSettings.fog; var ambient = RenderSettings.ambientLight; var mode=RenderSettings.ambientMode;
        var go = new GameObject("Temporary map screenshot camera"); var lightGo = new GameObject("Temporary overview light");
        var camera = go.AddComponent<Camera>(); var light = lightGo.AddComponent<Light>();
        RenderTexture rt = null; Texture2D texture=null;
        try
        {
            RenderSettings.fog = false; RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
            light.type=LightType.Directional;light.intensity=1.6f;light.color=new Color(1,.94f,.86f);light.transform.rotation=Quaternion.Euler(48,-35,0);
            camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=60;camera.farClipPlane=2200;
            camera.orthographic=ortho>0;camera.orthographicSize=ortho;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.2f);
            rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();
            var prev=RenderTexture.active;RenderTexture.active=rt;texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();RenderTexture.active=prev;
            string folder=Path.GetFullPath("docs/media/map-20261019");Directory.CreateDirectory(folder);string path=Path.Combine(folder,file+".png");File.WriteAllBytes(path,texture.EncodeToPNG());return path;
        }
        finally
        { camera.targetTexture=null;if(rt!=null){rt.Release();Object.DestroyImmediate(rt);}if(texture!=null)Object.DestroyImmediate(texture);Object.DestroyImmediate(go);Object.DestroyImmediate(lightGo);RenderSettings.fog=fog;RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=mode; }
    }
}
