using System;
using System.IO;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PrologueOfficePass
{
    static IEnumerator review;
    static readonly System.Collections.Generic.List<string> results = new System.Collections.Generic.List<string>();
    static double deadline;
    static PrologueOfficePass()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("PrologueOfficeReview",false)) return;
            SessionState.EraseBool("PrologueOfficeReview");
            results.Clear(); deadline=EditorApplication.timeSinceStartup+240;
            review=Review();
        };
    }
    static void Poll()
    {
        if (review != null)
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Opening review timed out");
                if (review.MoveNext()) return;
                results.Add("PASS: Full opening reaches morning deliveries");
            }
            catch (Exception e) { results.Add("FAIL: " + e); }
            review=null; File.WriteAllLines("Library/PrologueOfficeReview.result",results);
            EditorApplication.isPlaying=false; return;
        }
        const string request = "Library/PrologueOfficePass.request";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        var command = File.ReadAllText(request).Trim(); File.Delete(request);
        try
        {
            if (command == "refresh") AssetDatabase.Refresh();
            else if (command == "apply") Apply();
            else if (command == "review") { SessionState.SetBool("PrologueOfficeReview",true); EditorApplication.isPlaying=true; }
            else Inventory();
        }
        catch (Exception e) { File.WriteAllText("Library/PrologueOfficePass.result", e.ToString()); }
    }

    const string Out = "Assets/Generated/PrologueOffice";
    static Vector3 P(float x, float y, float z) => new Vector3(982f + x, 3.42f + y, 870f + z);
    static string Fbx(string folder) => Directory.GetFiles("Assets/Perishables/Avatar/" + folder, "*.fbx", SearchOption.AllDirectories).Single().Replace('\\', '/');
    static string Humanoid(string folder,string name)
    {
        string path=Out+"/"+name+".fbx";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null) AssetDatabase.CopyAsset(Fbx(folder),path);
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        if(importer.animationType!=ModelImporterAnimationType.Human || importer.avatarSetup!=ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.animationType=ModelImporterAnimationType.Human;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            var clips=importer.clipAnimations.Length>0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach(var clip in clips) { clip.loopTime=true; clip.lockRootRotation=true; clip.lockRootPositionXZ=true; clip.lockRootHeightY=true; }
            importer.clipAnimations=clips; importer.SaveAndReimport();
        }
        var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        if(avatar==null || !avatar.isValid || !avatar.isHuman) throw new Exception("Humanoid retarget import failed: "+path);
        return path;
    }
    static AnimationClip CopyClip(string folder, string name, bool loop, bool freeze = false)
    {
        if(name=="Mercer standing idle")
        {
            if(AssetDatabase.LoadAssetAtPath<AnimationClip>(Out+"/Mercer standing idle.anim")!=null) AssetDatabase.DeleteAsset(Out+"/Mercer standing idle.anim");
            Humanoid(folder,"Mercer retarget rig");
            return MeshyCastBuilder.Clip(Humanoid("Omar_Idle","Standing idle retarget"));
        }
        var source = MeshyCastBuilder.Clip(Fbx(folder));
        var copy = Object.Instantiate(source); copy.name = name;
        // These Generic exports carry horizontal travel on Hips. The scene paths own locomotion.
        foreach (var binding in AnimationUtility.GetCurveBindings(copy))
        {
            var curve = AnimationUtility.GetEditorCurve(copy, binding);
            bool horizontalRoot = (binding.path.EndsWith("Hips") || binding.path.Length == 0) && (binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z");
            if (freeze || horizontalRoot) AnimationUtility.SetEditorCurve(copy, binding, AnimationCurve.Constant(0f, freeze ? 1f : source.length, curve.Evaluate(0f)));
        }
        var settings = AnimationUtility.GetAnimationClipSettings(copy);
        settings.loopTime = loop; settings.loopBlend = loop;
        AnimationUtility.SetAnimationClipSettings(copy, settings);
        var path = Out + "/" + name + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) { EditorUtility.CopySerialized(copy, existing); Object.DestroyImmediate(copy); return existing; }
        AssetDatabase.CreateAsset(copy, path); return copy;
    }
    static Animator Actor(string name, string folder, Transform parent, Vector3 position, float yaw, params (string state, AnimationClip clip)[] clips)
    {
        string path = Out + "/" + name + ".controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = controller.layers[0].stateMachine;
        foreach (var old in sm.states) sm.RemoveState(old.state);
        foreach (var (state, clip) in clips) { var s = sm.AddState(state); s.motion = clip; if (state == "Idle") sm.defaultState = s; }
        string fbx = name=="Mercer" ? Out+"/Mercer retarget rig.fbx" : Fbx(folder);
        string texture = Directory.GetFiles(Path.GetDirectoryName(Fbx(folder)), "*_texture_0.png").Single().Replace('\\', '/');
        var mat = MeshyCastBuilder.Mat("Prologue " + name, texture);
        var go = MeshyCastBuilder.Man(fbx, name + " (opening)", parent, name == "Omar" ? 1.78f : name == "Ben" ? 1.76f : name == "Mercer" ? 1.82f : 1.67f, mat, controller);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        if(name=="Mercer") go.transform.position+=Vector3.up*(position.y-MeshyCastBuilder.Bounds(go).min.y);
        return go.GetComponent<Animator>();
    }
    static Transform Marker(string name, Transform parent, Vector3 position, Vector3? look = null, float yaw = 0)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, true); t.SetPositionAndRotation(position, Quaternion.Euler(0,yaw,0));
        if (look.HasValue) t.LookAt(look.Value); return t;
    }
    static Material Mat(string name, Color color)
    {
        var path = Out + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.color = color; mat.SetFloat("_Smoothness", .25f); return mat;
    }
    static Transform Box(string name, Transform parent, Vector3 p, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, true);
        go.transform.position = p; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat; return go.transform;
    }
    static void PreviousOfficeTextures(Transform model)
    {
        const string source="Assets/Models/lowpoly-mobile-cartoon-office-rooms/textures/";
        Material Surface(string name,string original)
        {
            string path=Out+"/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) { mat=new Material(AssetDatabase.LoadAssetAtPath<Material>(source+"glsl_"+original+".mat")); AssetDatabase.CreateAsset(mat,path); }
            mat.name=name; mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(source+original+".png"));
            mat.SetColor("_BaseColor",Color.white); mat.mainTextureScale=Vector2.one; mat.mainTextureOffset=Vector2.zero;
            mat.SetColor("_EmissionColor",Color.black); mat.DisableKeyword("_EMISSION"); EditorUtility.SetDirty(mat); return mat;
        }
        var wall=Surface("Clean previous office wall","wall1");
        var floor=Surface("Clean previous office carpet","floor1");
        Directory.CreateDirectory(Out+"/Meshes"); AssetDatabase.Refresh();
        int index=0;
        foreach(var mf in model.GetComponentsInChildren<MeshFilter>())
        {
            var renderer=mf.GetComponent<Renderer>(); if(renderer==null || mf.sharedMesh==null) continue;
            var mats=renderer.sharedMaterials; bool changed=false;
            for(int i=0;i<mats.Length;i++)
            {
                if(mats[i].name=="Clean Warm grey plaster") { mats[i]=wall; changed=true; }
                else if(new[]{"Clean Service floor","Clean Office floor - charcoal","Clean Concrete floor"}.Contains(mats[i].name)) { mats[i]=floor; changed=true; }
            }
            if(!changed) continue;
            var mesh=Object.Instantiate(mf.sharedMesh); mesh.name=mf.sharedMesh.name+" (clean office metre UVs)";
            if(mesh.normals.Length==0) mesh.RecalculateNormals();
            var vertices=mesh.vertices; var normals=mesh.normals; var uv=new Vector2[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var p=mf.transform.TransformPoint(vertices[i]); var n=mf.transform.TransformDirection(normals[i]);
                uv[i]=Mathf.Abs(n.y)>.7f ? new Vector2(p.x-982f,p.z-870f)/2.5f : new Vector2((Mathf.Abs(n.x)>Mathf.Abs(n.z)?p.z-870f:p.x-982f)/3.4f,(p.y-3.4f)/3.4f);
            }
            mesh.uv=uv;
            string path=Out+"/Meshes/Surface "+index+++".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null) { AssetDatabase.CreateAsset(mesh,path); saved=mesh; }
            else { EditorUtility.CopySerialized(mesh,saved); Object.DestroyImmediate(mesh); }
            mf.sharedMesh=saved; renderer.sharedMaterials=mats;
        }
    }

    [MenuItem("HellScape/Prologue/New office opening")]
    public static void Apply()
    {
        Directory.CreateDirectory(Out); AssetDatabase.Refresh();
        var active = SceneManager.GetActiveScene();
        if (active.isDirty && active.name != "Prologue") throw new Exception("Save the open scene before applying the prologue opening.");
        var scene = active.name == "Prologue" ? active : EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity");
        Directory.CreateDirectory("Backups/PrologueOffice-20261023");
        const string backup = "Backups/PrologueOffice-20261023/Prologue.unity";
        if (!File.Exists(backup)) File.Copy(scene.path, backup);
        var d = Object.FindFirstObjectByType<PrologueDirector>(FindObjectsInactive.Include);
        var old = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Office Ground Floor");
        if (old != null) { old.name = "Previous office (opening replaced)"; old.gameObject.SetActive(false); }
        foreach (var name in new[] {"Priya", "Nadia", "Ben", "Omar", "Maya"})
        {
            var g = GameObject.Find(name); if (g != null) g.SetActive(false);
        }
        var previous = GameObject.Find("Prologue office opening"); if (previous != null) Object.DestroyImmediate(previous);
        // The old office's props and signs were authored as separate roots, outside its model.
        foreach (var r in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)))
        {
            var p=r.bounds.center;
            if(p.x>935f && p.x<1095f && p.z>855f && p.z<960f) r.gameObject.SetActive(false);
        }
        var root = new GameObject("Prologue office opening").transform;
        var room = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/New Office/Hellscape_Office_v3_Unity.fbx"), root);
        room.name = "Office Ground Floor (clean v3)";
        room.transform.SetPositionAndRotation(new Vector3(952.3f,3.4f,877f), Quaternion.Euler(0,180,0));
        var materialCopies = new System.Collections.Generic.Dictionary<Material,Material>();
        foreach (var r in room.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterials = r.sharedMaterials.Select(original =>
            {
                if (original == null) throw new Exception("Missing room material on " + r.name);
                if (materialCopies.TryGetValue(original, out var result)) return result;
                string path = Out + "/Clean " + original.name.Replace('/','_') + ".mat";
                result = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (result == null)
                {
                    result = new Material(original);
                    if (!original.shader.name.StartsWith("Universal Render Pipeline"))
                    {
                        var texture = original.mainTexture; var color = original.color;
                        result.shader = Shader.Find("Universal Render Pipeline/Lit"); result.mainTexture = texture; result.color = color;
                    }
                    result.name = "Clean " + original.name; AssetDatabase.CreateAsset(result,path);
                }
                if(original.name.Contains("ControlRoom") || original.name.Contains("SalaControl"))
                {
                    string key=original.name.Replace("_Baked","");
                    var textures=Directory.GetFiles("Assets/Perishables/control-room-by-amogusstrikesback2/textures","*.png");
                    int Prefix(string value) { int n=0; while(n<key.Length && n<value.Length && char.ToLowerInvariant(key[n])==char.ToLowerInvariant(value[n])) n++; return n; }
                    var best=textures.OrderByDescending(p=>Prefix(Path.GetFileNameWithoutExtension(p))).FirstOrDefault();
                    if(best!=null && Prefix(Path.GetFileNameWithoutExtension(best))>=Mathf.Min(25,key.Length))
                    {
                        result.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(best.Replace('\\','/')));
                        result.SetColor("_BaseColor",Color.white);
                    }
                    else result.SetColor("_BaseColor",new Color(.3f,.32f,.34f));
                    result.SetColor("_EmissionColor",Color.black); result.DisableKeyword("_EMISSION");
                    EditorUtility.SetDirty(result);
                }
                materialCopies[original] = result; return result;
            }).ToArray();
        }
        PreviousOfficeTextures(room.transform);
        var cream = Mat("Clean ceiling",new Color(.74f,.73f,.69f));
        Box("Office ceiling",root,P(0,3.5f,22),new Vector3(60,.12f,44),cream);
        foreach (float x in new[]{-23f,-12f,0f,12f,24f}) foreach(float z in new[]{4f,12f,20f,28f,36f})
        {
            var light = Marker("Office ceiling light",root,P(x,3.05f,z)).gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.color = new Color(1f,.94f,.84f); light.intensity = 2.4f; light.range = 11f; light.shadows = LightShadows.None;
            Box("Fluorescent panel",root,P(x,3.25f,z),new Vector3(1.2f,.05f,.3f),cream);
        }
        var seq = d.GetComponent<OfficeOpeningSequence>() ?? d.gameObject.AddComponent<OfficeOpeningSequence>();
        seq.room = root;
        var oi = CopyClip("Omar_Idle","Omar idle",true); var oa = CopyClip("Omar_pickingup","Omar bucket",false);
        var bi = CopyClip("Ben_Idle Stance","Ben idle look",true); var ba = CopyClip("Ben_Hit","Ben hit desktop",false);
        var ni = CopyClip("Nadia_Idle","Nadia idle",true); var na = CopyClip("Nadia_Standup","Nadia stand",false); var ns = CopyClip("Nadia_Standup","Nadia seated",true,true);
        var pi = CopyClip("priya_idle Sit","Priya seated",true); var pw = CopyClip("Priya_walk","Priya walk",true); var ps = CopyClip("Priya_SitWalk","Priya sit",false);
        var pstand = CopyClip("Priya_walk","Priya waiting",true,true);
        seq.omar = Actor("Omar","Omar_Idle",root,P(25,0,34),180,("Idle",oi),("Action",oa));
        seq.ben = Actor("Ben","Ben_Idle Stance",root,P(-7,0,21),270,("Idle",bi),("Action",ba));
        seq.nadia = Actor("Nadia","Nadia_Idle",root,P(-3.5f,0,16.8f),0,("Idle",ni),("Action",na),("Seated",ns));
        seq.priya = Actor("Priya","Priya_walk",root,P(0,0,17.7f),180,("Idle",pstand),("Walk",pw),("Action",ps),("Seated",pi));
        if(seq.mercer!=null) Object.DestroyImmediate(seq.mercer.gameObject);
        const string mercerFolder="Meshy_AI_Mercer_PS2_T_Pose_35K_biped";
        var mi=CopyClip(mercerFolder,"Mercer standing idle",true,true);
        seq.mercer=Actor("Mercer",mercerFolder,null,Vector3.zero,0,("Idle",mi));
        seq.mercer.gameObject.SetActive(false);
        seq.omarActionSeconds = Mathf.Min(oa.length,3.5f); seq.benActionSeconds = ba.length; seq.nadiaActionSeconds = na.length; seq.priyaSitSeconds = ps.length;
        seq.omarShot = Marker("Omar cleaner closet shot",root,P(24.7f,1.6f,32.1f),P(25,1.25f,34));
        seq.benShot = Marker("Ben shot",root,P(-5,1.6f,20),P(-7.3f,1.2f,21));
        seq.serverChairShot=Marker("Server electrical room chair shot",root,P(10.1f,1.1f,37.5f),P(11.85f,1.2f,37.5f));
        seq.serverExitShot=Marker("Leave server room chair",root,P(10.1f,1.6f,35.5f),P(15,1.5f,33));
        seq.nadiaShot = Marker("Nadia and reception shot",root,P(-3.9f,1.6f,19.6f),P(-2.4f,1.2f,16.2f));
        seq.priyaStart = Marker("Priya walk start",root,P(0,0,17.7f),null,180);
        seq.priyaSeat = Marker("Priya chair anchor",root,P(-2.2f,0,12.8f),null,180);
        seq.priyaShot = Marker("Priya shot",root,P(-2.2f,1.55f,11.05f),P(-2.2f,1.1f,12.8f));
        var chair = room.GetComponentsInChildren<Renderer>().First(r=>r.name.StartsWith("Reception chair 31"));
        var nadiaChair = Object.Instantiate(chair.gameObject,root); nadiaChair.name = "Nadia chair";
        var bounds = nadiaChair.GetComponent<Renderer>().bounds; nadiaChair.transform.position += P(-3.5f,.5f,16.45f)-bounds.center;
        var dark = Mat("Desktop dark plastic",new Color(.12f,.14f,.16f));
        var wood = Mat("Office desk wood",new Color(.48f,.33f,.2f));
        Box("Ben desk",root,P(-8.1f,.74f,21),new Vector3(.8f,.08f,1.8f),wood);
        Box("Ben desktop tower",root,P(-8.1f,1.08f,21),new Vector3(.35f,.6f,.4f),dark);
        Box("Ben monitor",root,P(-8.1f,1.14f,21.6f),new Vector3(.07f,.55f,.65f),dark);
        Box("Ben monitor stand",root,P(-8.1f,.85f,21.6f),new Vector3(.3f,.15f,.3f),dark);
        var hand=seq.omar.GetComponentsInChildren<Transform>().First(t=>t.name.EndsWith("RightHand"));
        seq.bucketGrip = Marker("Bucket carry grip",hand,hand.position-Vector3.up*.35f);
        var bucket = GameObject.CreatePrimitive(PrimitiveType.Cylinder); bucket.name = "Omar cleaning bucket"; bucket.transform.SetParent(root,true);
        bucket.transform.position = seq.omar.transform.position + seq.omar.transform.forward*.48f + Vector3.up*.2f;
        bucket.transform.localScale = new Vector3(.36f,.2f,.36f); bucket.GetComponent<Renderer>().sharedMaterial = Mat("Blue cleaning bucket",new Color(.15f,.35f,.48f)); seq.bucket=bucket.transform;
        var inside=GameObject.CreatePrimitive(PrimitiveType.Cylinder); inside.name="Open bucket interior"; inside.transform.SetParent(bucket.transform,false);
        inside.transform.localPosition=new Vector3(0,1.01f,0); inside.transform.localScale=new Vector3(.9f,.01f,.9f); inside.GetComponent<Renderer>().sharedMaterial=dark;
        var handle=new GameObject("Bucket handle").AddComponent<LineRenderer>(); handle.transform.SetParent(bucket.transform,false);
        handle.useWorldSpace=false; handle.positionCount=17; handle.widthMultiplier=.014f; handle.sharedMaterial=dark;
        for(int i=0;i<17;i++) { float a=i*Mathf.PI/16f; handle.SetPosition(i,new Vector3(Mathf.Cos(a)*.47f,1f+Mathf.Sin(a)*.75f,0)); }
        d.playFlashback = true; EditorUtility.SetDirty(seq); EditorUtility.SetDirty(d);
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Library/PrologueOfficePass.result","PASS: New clean office and four animated actors saved.\n");
    }

    static IEnumerator Review()
    {
        while (Object.FindFirstObjectByType<OfficeOpeningSequence>() == null) yield return null;
        var seq=Object.FindFirstObjectByType<OfficeOpeningSequence>();
        var d=Object.FindFirstObjectByType<PrologueDirector>();
        DialogueBox.AutoAdvanceAfter=.15f;
        while(DialogueBox.Instance == null) yield return null;
        DialogueBox.Instance.charDelay=.001f;
        Time.timeScale=2f;
        var captured=new System.Collections.Generic.HashSet<string>();
        foreach(var renderer in seq.room.GetComponentsInChildren<Renderer>(true))
            foreach(var mat in renderer.sharedMaterials)
                if(mat==null || mat.name.ToLowerInvariant().Contains("burnt") || mat.shader.name.Contains("Burnt")) throw new Exception("Burnt/missing material in opening: "+renderer.name);
        results.Add("PASS: Office uses clean materials throughout");
        var wall=AssetDatabase.LoadAssetAtPath<Material>(Out+"/Clean previous office wall.mat");
        var carpet=AssetDatabase.LoadAssetAtPath<Material>(Out+"/Clean previous office carpet.mat");
        if(wall==null || carpet==null || wall.mainTexture==null || carpet.mainTexture==null) throw new Exception("Previous office textures missing");
        int wallSlots=0,floorSlots=0;
        foreach(var renderer in seq.room.GetComponentsInChildren<Renderer>(true)) foreach(var mat in renderer.sharedMaterials)
        {
            if(mat==wall) wallSlots++; if(mat==carpet) floorSlots++;
            if(mat!=null && new[]{"Clean Warm grey plaster","Clean Service floor","Clean Office floor - charcoal","Clean Concrete floor"}.Contains(mat.name)) throw new Exception("Flat surface left untextured: "+renderer.name);
        }
        if(wallSlots<10 || floorSlots<5) throw new Exception("Incomplete wall and floor coverage");
        results.Add("PASS: Previous office wall and carpet textures cover "+wallSlots+" wall slots and "+floorSlots+" floor slots");
        string last=""; float changed=0;
        Directory.CreateDirectory("Library/PrologueOfficePreview");
        bool started=false;
        while(true)
        {
            string beat=seq.Beat??"";
            if(beat!=last) { last=beat; changed=Time.time; results.Add("BEAT: "+beat); }
            if(OfficeFlashback.Playing) started=true;
            if(started && !OfficeFlashback.Playing) break;
            if((beat.Contains("conversation") || beat.Contains("chair")) && Time.time-changed>.5f && captured.Add(beat))
            {
                Capture(d.cine,beat.Split(' ')[0]);
                foreach(var actor in new[]{seq.omar,seq.ben,seq.nadia,seq.priya})
                {
                    var b=MeshyCastBuilder.Bounds(actor.gameObject);
                    if(b.min.y<2.7f || b.max.y>6.7f) throw new Exception(actor.name+" pose outside office floor: "+b);
                }
            }
            if(seq.mercer!=null && seq.mercer.gameObject.activeInHierarchy && beat=="Leaving office" && DialogueBox.Instance.Busy && captured.Add("Mercer")) Capture(d.cine,"Mercer");
            yield return null;
        }
        if(!seq.PriyaSeated) throw new Exception("Priya never completed her seat transition");
        if(!captured.Contains("Mercer")) throw new Exception("Mercer van conversation was skipped");
        if(captured.Count<6) throw new Exception("Missing shot coverage: "+string.Join(",",captured));
        var van=Object.FindFirstObjectByType<PrologueVan>();
        while(van!=null && !van.InVan) yield return null;
        if(van==null) throw new Exception("Delivery van missing after opening");
        results.Add("PASS: Omar, Ben, Nadia, Priya and Mercer shots captured");
        results.Add("PASS: Priya walked and sat during Nadia's conversation");
        Time.timeScale=1f; DialogueBox.AutoAdvanceAfter=0;
    }
    static void Capture(Camera camera,string name)
    {
        var rt=new RenderTexture(1280,720,24); var previous=camera.targetTexture; var active=RenderTexture.active;
        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply(); File.WriteAllBytes("Library/PrologueOfficePreview/"+name+".png",texture.EncodeToPNG()); }
        finally { camera.targetTexture=previous; RenderTexture.active=active; Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt); }
    }
    static void Inventory()
    {
        var log = new StringBuilder();
        foreach (var path in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Perishables/Avatar" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".fbx")))
        {
            if (!new[] {"Omar_", "Ben_", "Nadia_", "Priya_", "priya_"}.Any(path.Contains)) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var animator = go.GetComponentInChildren<Animator>();
            log.AppendLine(path + " avatar=" + (animator != null ? animator.avatar?.name : "none"));
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__"))) log.AppendLine(" CLIP " + clip.name + " " + clip.length);
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Prologue.unity", OpenSceneMode.Additive);
        foreach (var t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)))
            if (new[] {"Omar", "Ben", "Nadia", "Priya", "Office Ground", "Mercer"}.Any(n => t.name.StartsWith(n))) log.AppendLine("SCENE " + t.name + " " + t.position + " scale=" + t.lossyScale);
        EditorSceneManager.CloseScene(scene, true);
        var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/New Office/Hellscape_Office_v3_Unity.fbx"));
        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (var r in model.GetComponentsInChildren<Renderer>()) log.AppendLine("MODEL " + r.name + " " + r.bounds + " mats=" + string.Join(",",r.sharedMaterials.Select(m=>m ? m.name : "NULL")));
        Object.DestroyImmediate(model);
        File.WriteAllText("Library/PrologueOfficeInventory.txt", log.ToString());
    }
}
