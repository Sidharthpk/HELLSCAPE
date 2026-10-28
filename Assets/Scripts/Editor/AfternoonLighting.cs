using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
// Manual scene setup; importing this script does not modify scenes.
public static class AfternoonLighting {
 static T Effect<T>(VolumeProfile p) where T:VolumeComponent {if(!p.TryGet<T>(out var c)){c=p.Add<T>();AssetDatabase.AddObjectToAsset(c,p);}return c;}
 [MenuItem("HellScape/City/Afternoon lighting and stable shadows")]
 public static void Apply(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first.");
 if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new Exception("Save scene edits first.");
 var setup=EditorSceneManager.GetSceneManagerSetup();string backup="Backups/AfternoonLighting-20261020";Directory.CreateDirectory(backup);var report=new StringBuilder();
 foreach(string path in Directory.GetFiles("Assets/Settings","*ForwardRenderer.asset")){
 if(!File.Exists(backup+"/"+Path.GetFileName(path)))File.Copy(path,backup+"/"+Path.GetFileName(path));
 foreach(var o in AssetDatabase.LoadAllAssetsAtPath(path).Where(o=>o!=null&&o.name=="ScreenSpaceAmbientOcclusion")){
 var so=new SerializedObject(o);void F(string n,float v){var p=so.FindProperty("m_Settings."+n);if(p!=null)p.floatValue=v;}void I(string n,int v){var p=so.FindProperty("m_Settings."+n);if(p!=null)p.intValue=v;}
 F("Intensity",.55f);F("Radius",.25f);F("DirectLightingStrength",.1f);F("Falloff",70);I("Source",0);I("BlurQuality",1);so.ApplyModifiedPropertiesWithoutUndo();report.AppendLine(path+": softened ambient occlusion");
 }}
 try{foreach(string name in new[]{"MainGameScene","Prologue"}){
 string path="Assets/Scenes/"+name+".unity";if(!File.Exists(backup+"/"+name+".unity"))File.Copy(path,backup+"/"+name+".unity");var s=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
 var all=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();var lights=all.SelectMany(t=>t.GetComponents<Light>()).ToArray();
 Light sun=null;
 if(name=="Prologue")sun=lights.FirstOrDefault(l=>l.type==LightType.Directional&&l.name=="Sun");
 else {var sleep=all.SelectMany(t=>t.GetComponents<SleepSequence>()).FirstOrDefault();sun=sleep!=null?sleep.sun:null;if(sun==null)sun=lights.FirstOrDefault(l=>l.name=="City night fill");}
 if(sun!=null){foreach(var l in lights.Where(l=>l.type==LightType.Directional&&l!=sun))l.enabled=false;sun.gameObject.SetActive(true);sun.enabled=true;sun.shadows=LightShadows.Soft;sun.shadowBias=.6f;sun.shadowNormalBias=.3f;sun.shadowStrength=.75f;RenderSettings.sun=sun;}
 string profilePath="Assets/Generated/CitySurfaces/"+(name=="Prologue"?"AfternoonAtmosphere":"CityAtmosphere")+".asset";
 var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
 var bloom=Effect<Bloom>(profile);bloom.intensity.Override(name=="Prologue"?.32f:.25f);bloom.threshold.Override(1.15f);bloom.scatter.Override(.65f);bloom.highQualityFiltering.Override(true);bloom.clamp.Override(8f);
 var tone=Effect<Tonemapping>(profile);tone.mode.Override(TonemappingMode.ACES);
 var color=Effect<ColorAdjustments>(profile);color.postExposure.Override(0);color.contrast.Override(4);color.saturation.Override(2);
 var vig=Effect<Vignette>(profile);vig.intensity.Override(.12f);vig.smoothness.Override(.6f);
 Volume volume;
 if(name=="Prologue"){
 volume=all.SelectMany(t=>t.GetComponents<Volume>()).FirstOrDefault(v=>v.name=="Prologue Atmosphere");
 if(sun!=null){sun.intensity=1.35f;sun.color=new Color(1,.94f,.83f);sun.transform.rotation=Quaternion.Euler(42,-125,0);}
 var skyPath="Assets/Generated/CitySurfaces/AfternoonSky.mat";var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);if(sky==null){sky=new Material(Shader.Find("Skybox/Procedural"));AssetDatabase.CreateAsset(sky,skyPath);}sky.SetFloat("_SunDisk",2);sky.SetFloat("_SunSize",.025f);sky.SetFloat("_AtmosphereThickness",1);sky.SetFloat("_Exposure",1);sky.SetColor("_SkyTint",new Color(.5f,.52f,.55f));sky.SetColor("_GroundColor",new Color(.32f,.34f,.37f));sky.EnableKeyword("_SUNDISK_HIGH_QUALITY");RenderSettings.skybox=sky;EditorUtility.SetDirty(sky);
 RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.57f,.65f,.76f);RenderSettings.ambientEquatorColor=new Color(.44f,.47f,.51f);RenderSettings.ambientGroundColor=new Color(.25f,.23f,.21f);RenderSettings.reflectionIntensity=.45f;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.66f,.73f,.8f);RenderSettings.fogDensity=.0012f;
 }else {volume=all.SelectMany(t=>t.GetComponents<Volume>()).FirstOrDefault(v=>v.name=="City Atmosphere");if(sun!=null){sun.intensity=.4f;sun.color=new Color(.85f,.64f,.59f);sun.transform.rotation=Quaternion.Euler(42,-125,0);}}
 if(volume==null)volume=new GameObject(name=="Prologue"?"Prologue Atmosphere":"City Atmosphere").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;volume.weight=1;volume.priority=name=="Prologue"?5:-5;
 foreach(var c in profile.components)EditorUtility.SetDirty(c);EditorUtility.SetDirty(profile);
 foreach(var camera in all.SelectMany(t=>t.GetComponents<Camera>())){camera.allowHDR=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;}
 foreach(var probe in all.SelectMany(t=>t.GetComponents<ReflectionProbe>())){probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;probe.bakedTexture=null;}
 EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);AssetDatabase.SaveAssets();
 CitySurfaceReview.Capture(name+"-afternoon-road",new Vector3(-4,10,25),new Vector3(65,2,25));CitySurfaceReview.Capture(name+"-afternoon-flat",new Vector3(-38,7,-65),new Vector3(20,3,-65));
 report.AppendLine(name+": active directional lights="+s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).Count(l=>l.enabled&&l.type==LightType.Directional)+" missing scripts="+all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
 }}finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}File.WriteAllText("Library/AfternoonLighting.result",report.ToString());
 }
}
