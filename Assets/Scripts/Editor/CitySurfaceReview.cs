using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
// Manual editor utility; no automatic scene changes on load.
public static class CitySurfaceReview {
 static Transform[] All(Scene s)=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
 [MenuItem("HellScape/City/Apply dry roads and balanced prologue lighting")]
 static void Apply(){
 var original=EditorSceneManager.GetSceneManagerSetup();
 if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new Exception("Save current edits before applying surface cleanup.");
 string folder="Assets/Generated/CitySurfaces";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
 string backup="Backups/CitySurfaces-20261020";Directory.CreateDirectory(backup);
 var replacements=new System.Collections.Generic.Dictionary<Material,Material>();
 foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Versatile Studio Assets/Demo City By Versatile Studio/Materials"})){
 var source=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
 if(!source.name.StartsWith("asphalt_")&&source.name!="sideway_tile")continue;
 string path=folder+"/"+source.name+"_dry.mat";var dry=AssetDatabase.LoadAssetAtPath<Material>(path);
 if(dry==null){dry=new Material(source);AssetDatabase.CreateAsset(dry,path);}
 dry.SetTexture("_SpecGlossMap",null);dry.SetTexture("_MetallicGlossMap",null);dry.SetTexture("_OcclusionMap",null);
 foreach(var keyword in new[]{"_SPECGLOSSMAP","_METALLICSPECGLOSSMAP","_OCCLUSIONMAP","_SPECULAR_SETUP"})dry.DisableKeyword(keyword);
 dry.SetFloat("_WorkflowMode",1);dry.SetFloat("_Metallic",0);dry.SetFloat("_Smoothness",.12f);dry.SetFloat("_GlossMapScale",.12f);dry.SetFloat("_OcclusionStrength",0);dry.SetFloat("_EnvironmentReflections",0);dry.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
 if(source.name.StartsWith("asphalt_")){dry.SetTexture("_BumpMap",null);dry.DisableKeyword("_NORMALMAP");}else {
 dry.SetFloat("_BumpScale",.06f);
 var filtered=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/PavementFiltered.png");
 if(filtered!=null){dry.SetTexture("_BaseMap",filtered);dry.SetTexture("_MainTex",filtered);}
 }
 dry.SetColor("_BaseColor",new Color(.8f,.8f,.8f,1));EditorUtility.SetDirty(dry);replacements[source]=dry;
 }
 var report=new StringBuilder();
 try {foreach(var name in new[]{"MainGameScene","Prologue"}){
 string path="Assets/Scenes/"+name+".unity";if(!File.Exists(backup+"/"+name+".unity"))File.Copy(path,backup+"/"+name+".unity");
 var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);int count=0,duplicates=0;var all=All(scene);
 foreach(var renderer in all.SelectMany(t=>t.GetComponents<Renderer>())){
 var materials=renderer.sharedMaterials;bool changed=false;for(int i=0;i<materials.Length;i++)if(materials[i]!=null&&replacements.TryGetValue(materials[i],out var replacement)){materials[i]=replacement;changed=true;}
 if(changed){renderer.sharedMaterials=materials;EditorUtility.SetDirty(renderer);count++;}
 }
 var seen=new System.Collections.Generic.HashSet<string>();var city=all.First(t=>t.name=="STATIC_MODELS");
 foreach(var f in city.GetComponentsInChildren<MeshFilter>(true)){
 var r=f.GetComponent<MeshRenderer>();if(r==null||!r.enabled||!f.gameObject.activeInHierarchy||f.sharedMesh==null)continue;
 string key=f.sharedMesh.GetInstanceID()+"|"+f.transform.localToWorldMatrix.ToString("F4")+"|"+string.Join(",",r.sharedMaterials.Select(m=>m?m.GetInstanceID():0));
 if(!seen.Add(key)){r.enabled=false;duplicates++;}
 }
 if(name=="Prologue"){
 foreach(var l in all.SelectMany(t=>t.GetComponents<Light>()).Where(l=>l.type==LightType.Directional&&l.gameObject.activeInHierarchy)){l.intensity=1f;l.color=new Color(1,.92f,.84f);l.shadows=LightShadows.Soft;l.shadowStrength=.8f;}
 RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.52f,.57f,.64f);RenderSettings.ambientEquatorColor=new Color(.43f,.44f,.46f);RenderSettings.ambientGroundColor=new Color(.28f,.26f,.24f);RenderSettings.ambientIntensity=.7f;RenderSettings.reflectionIntensity=.35f;RenderSettings.fogColor=new Color(.46f,.49f,.53f);RenderSettings.fogDensity=.0018f;
 var sky=RenderSettings.skybox;if(sky!=null){string skyPath=folder+"/MorningSkyBalanced.mat";var copy=AssetDatabase.LoadAssetAtPath<Material>(skyPath);if(copy==null){copy=new Material(sky);AssetDatabase.CreateAsset(copy,skyPath);}if(copy.HasProperty("_Exposure"))copy.SetFloat("_Exposure",.75f);RenderSettings.skybox=copy;EditorUtility.SetDirty(copy);}
 string profilePath=folder+"/PrologueBalanced.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
 if(!profile.TryGet<Tonemapping>(out var tone)){tone=profile.Add<Tonemapping>();AssetDatabase.AddObjectToAsset(tone,profile);}tone.mode.Override(TonemappingMode.Neutral);
 if(!profile.TryGet<ColorAdjustments>(out var color)){color=profile.Add<ColorAdjustments>();AssetDatabase.AddObjectToAsset(color,profile);}color.postExposure.Override(-.1f);color.saturation.Override(-8);color.contrast.Override(5);
 if(!profile.TryGet<Bloom>(out var bloom)){bloom=profile.Add<Bloom>();AssetDatabase.AddObjectToAsset(bloom,profile);}bloom.intensity.Override(.12f);bloom.threshold.Override(1.4f);
 foreach(var v in all.SelectMany(t=>t.GetComponents<Volume>()).Where(v=>v.name=="Prologue Atmosphere")){v.sharedProfile=profile;v.priority=5;v.weight=1;}
 foreach(var c in profile.components)EditorUtility.SetDirty(c);EditorUtility.SetDirty(profile);
 }
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
 report.AppendLine(name+": dry surface renderers="+count+", exact duplicate renderers disabled="+duplicates);
 Capture(name+"-after",new Vector3(-38,8,-65),new Vector3(35,4,-65));
 Capture(name+"-overview",new Vector3(80,380,-400),new Vector3(80,0,-40));
 }}finally{EditorSceneManager.RestoreSceneManagerSetup(original);}
 File.WriteAllText("Library/CitySurfaceReview/applied.txt",report.ToString());
 }
 public static void Capture(string name,Vector3 p,Vector3 target){var go=new GameObject("Review camera");var c=go.AddComponent<Camera>();c.transform.position=p;c.transform.LookAt(target);c.farClipPlane=1800;c.fieldOfView=65;var data=c.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;var rt=new RenderTexture(1280,800,24);var prev=RenderTexture.active;Texture2D tex=null;try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex=new Texture2D(1280,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,800),0,0);tex.Apply();Directory.CreateDirectory("docs/media/city-cleanup");File.WriteAllBytes("docs/media/city-cleanup/"+name+".png",tex.EncodeToPNG());}finally{RenderTexture.active=prev;c.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(tex)UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(go);}}
}
