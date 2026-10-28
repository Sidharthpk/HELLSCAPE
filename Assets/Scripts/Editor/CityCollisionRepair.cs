using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class CityCollisionRepair {
 static CityCollisionRepair(){EditorApplication.delayCall+=()=>{if(!File.Exists("Library/CityCollisionRepair.request"))return;File.Delete("Library/CityCollisionRepair.request");try{Apply();}catch(Exception e){File.WriteAllText("Library/CityCollisionRepair.error",e.ToString());}};}
 static Transform[] All(Scene s)=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
 [MenuItem("HellScape/City/Repair main city ground collision")]
 public static void Apply(){
 if(EditorApplication.isPlayingOrWillChangePlaymode||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new Exception("Stop Play and save edits before collision repair.");
 var setup=EditorSceneManager.GetSceneManagerSetup();var log=new StringBuilder();try{
 foreach(var name in new[]{"Prologue","MainGameScene"}){
 var s=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity",OpenSceneMode.Single);var all=All(s);var city=all.First(t=>t.name=="STATIC_MODELS");
 foreach(var c in city.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.name.Contains("city_part")))log.AppendLine(name+" BEFORE "+c.name+" enabled="+c.enabled+" active="+c.gameObject.activeInHierarchy+" trigger="+c.isTrigger+" mesh="+(c.sharedMesh?c.sharedMesh.name:"MISSING"));
 var samples=new[]{new Vector3(-38,0,25),new Vector3(90,0,25),new Vector3(245,0,25),new Vector3(-38,0,-82),new Vector3(90,0,-82),new Vector3(245,0,-82)};
 Physics.SyncTransforms();foreach(var p in samples)log.AppendLine(name+" BEFORE road "+p+" ground="+Physics.Raycast(p+Vector3.up*12,Vector3.down,25,~0,QueryTriggerInteraction.Ignore));
 if(name=="MainGameScene"){
 string backup="Backups/CityCollision-20261020";Directory.CreateDirectory(backup);if(!File.Exists(backup+"/MainGameScene.unity"))File.Copy(s.path,backup+"/MainGameScene.unity");
 int added=0;
 foreach(var f in city.GetComponentsInChildren<MeshFilter>(true)){
 bool ground=f.name.StartsWith("city_part_demo_main")||f.name.StartsWith("city_part_highway");var r=f.GetComponent<Renderer>();if(!ground||f.sharedMesh==null||r==null||!r.enabled||!f.gameObject.activeInHierarchy)continue;
 var c=f.GetComponent<MeshCollider>();if(c==null){c=f.gameObject.AddComponent<MeshCollider>();added++;}c.sharedMesh=f.sharedMesh;c.convex=false;c.isTrigger=false;c.enabled=true;EditorUtility.SetDirty(c);
 log.AppendLine("Ground mesh collider: "+f.name+" vertices="+f.sharedMesh.vertexCount);
 }
 // The former aggregate collision can be disabled independently of the visible map.
 // Exact visible road meshes above now supply its ground, without restoring closed fence passages.
 Physics.SyncTransforms();int failures=0;
 foreach(var p in samples){bool hit=Physics.Raycast(p+Vector3.up*12,Vector3.down,out var h,25,~0,QueryTriggerInteraction.Ignore);if(!hit)failures++;log.AppendLine("AFTER road "+p+" ground="+hit+(hit?" "+h.collider.name+" y="+h.point.y:""));}
 foreach(var t in all.Where(t=>new[]{"FallWakePoint","CityReturnPoint","Outside Flat 2B (street)"}.Contains(t.name))){bool hit=Physics.Raycast(t.position+Vector3.up*2,Vector3.down,out var h,15,~0,QueryTriggerInteraction.Ignore);log.AppendLine("Spawn "+t.name+" grounded="+hit+(hit?" "+h.collider.name:""));}
 EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);log.AppendLine("Added "+added+" matching visible-ground colliders. Road failures="+failures);
 }
 }
 }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}File.WriteAllText("Library/CityCollisionRepair.result",log.ToString());
 }
}
