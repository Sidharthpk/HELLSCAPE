using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
// Run manually; importing this script never changes scenes.
public static class CityLightingRepair {
 [MenuItem("HellScape/City/Repair lighting and smooth pavements")]
 public static void Apply(){
 if(EditorApplication.isPlayingOrWillChangePlaymode||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new Exception("Stop Play and save scene edits first.");
 string backup="Backups/CityLighting-20261020";Directory.CreateDirectory(backup);var log=new StringBuilder();
 string folder="Assets/Generated/CitySurfaces";
 string source="Assets/Versatile Studio Assets/Demo City By Versatile Studio/Textures/sideway_tile.png",dest=folder+"/PavementFiltered.png";
 if(!File.Exists(dest))AssetDatabase.CopyAsset(source,dest);
 var importer=(TextureImporter)AssetImporter.GetAtPath(dest);importer.maxTextureSize=512;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.mipmapEnabled=true;importer.mipMapBias=.25f;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.ClearPlatformTextureSettings("Standalone");importer.SaveAndReimport();
 var mat=AssetDatabase.LoadAssetAtPath<Material>(folder+"/sideway_tile_dry.mat");
 if(!File.Exists(backup+"/sideway_tile_dry.mat"))File.Copy(folder+"/sideway_tile_dry.mat",backup+"/sideway_tile_dry.mat");
 mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dest));mat.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(dest));mat.SetFloat("_BumpScale",.06f);mat.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(mat);
 foreach(var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset",new[]{"Assets/Settings"})){
 string path=AssetDatabase.GUIDToAssetPath(guid);var asset=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);var so=new SerializedObject(asset);
 if(!so.FindProperty("m_MainLightShadowsSupported").boolValue)continue;
 if(!File.Exists(backup+"/"+Path.GetFileName(path)))File.Copy(path,backup+"/"+Path.GetFileName(path));
 void Int(string n,int v){var p=so.FindProperty(n);if(p!=null)p.intValue=v;} void Float(string n,float v){var p=so.FindProperty(n);if(p!=null)p.floatValue=v;}
 Int("m_ShadowCascadeCount",4);Int("m_MainLightShadowmapResolution",2048);Float("m_ShadowDistance",90);Float("m_ShadowDepthBias",.7f);Float("m_ShadowNormalBias",.35f);Float("m_CascadeBorder",.15f);Int("m_SoftShadowQuality",2);Int("m_AdditionalLightsShadowResolutionTierLow",128);Int("m_AdditionalLightsShadowResolutionTierMedium",256);Int("m_AdditionalLightsShadowResolutionTierHigh",512);Int("m_MSAA",4);
 so.ApplyModifiedPropertiesWithoutUndo();log.AppendLine(asset.name+": 4 cascades, 90m shadow distance, reduced normal bias, 4x MSAA");
 }
 var setup=EditorSceneManager.GetSceneManagerSetup();try{foreach(string name in new[]{"MainGameScene","Prologue"}){
 string path="Assets/Scenes/"+name+".unity";if(!File.Exists(backup+"/"+name+".unity"))File.Copy(path,backup+"/"+name+".unity");var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
 var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();int stale=0,decorative=0;
 var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>("Assets/Settings/Game Realtime Lighting.lighting");if(settings!=null)Lightmapping.lightingSettings=settings;
 Lightmapping.lightingDataAsset=null;LightmapSettings.lightmaps=Array.Empty<LightmapData>();
 foreach(var r in all.SelectMany(t=>t.GetComponents<Renderer>())){if(r.lightmapIndex>=0&&r.lightmapIndex<65534)stale++;r.lightmapIndex=-1;r.realtimeLightmapIndex=-1;}
 if(name=="MainGameScene"){
 RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.28f,.30f,.36f);RenderSettings.ambientEquatorColor=new Color(.18f,.17f,.20f);RenderSettings.ambientGroundColor=new Color(.09f,.07f,.08f);
 var key=all.FirstOrDefault(t=>t.name=="City night fill");Light moon;if(key==null){var go=new GameObject("City night fill");moon=go.AddComponent<Light>();}else moon=key.GetComponent<Light>();
 moon.type=LightType.Directional;moon.intensity=.35f;moon.color=new Color(.72f,.78f,1f);moon.transform.rotation=Quaternion.Euler(48,-35,0);moon.shadows=LightShadows.Soft;moon.shadowBias=.7f;moon.shadowNormalBias=.35f;moon.shadowStrength=.8f;
 }
 var city=all.FirstOrDefault(t=>t.name=="STATIC_MODELS");var env=all.FirstOrDefault(t=>t.name=="ENVIRONMENT_OBJECTS");
 var anchors=new[]{new Vector3(0,4,-50),new Vector3(90,3,25),new Vector3(125,3,145),new Vector3(245,5,-80)};
 var selected=all.SelectMany(t=>t.GetComponents<Light>()).Where(l=>l.isActiveAndEnabled&&(l.type==LightType.Point||l.type==LightType.Spot)&&l.range>5&&((city!=null&&l.transform.IsChildOf(city))||(env!=null&&l.transform.IsChildOf(env)))).OrderBy(l=>anchors.Min(a=>Vector3.Distance(a,l.transform.position))).Take(8).ToHashSet();
 foreach(var light in all.SelectMany(t=>t.GetComponents<Light>())){
 light.lightmapBakeType=LightmapBakeType.Realtime;light.bakingOutput=new LightBakingOutput{isBaked=false,lightmapBakeType=LightmapBakeType.Realtime,occlusionMaskChannel=-1,probeOcclusionLightIndex=-1};
 bool cityLight=(city!=null&&light.transform.IsChildOf(city))||(env!=null&&light.transform.IsChildOf(env));
 if(light.type==LightType.Directional){light.shadows=LightShadows.Soft;light.shadowBias=.7f;light.shadowNormalBias=.35f;light.shadowNearPlane=.1f;}
 else if(cityLight){if(light.shadows!=LightShadows.None)decorative++;light.shadows=selected.Contains(light)?LightShadows.Soft:LightShadows.None;if(selected.Contains(light)){light.shadowBias=.5f;light.shadowNormalBias=.3f;var ld=light.GetUniversalAdditionalLightData();var ls=new SerializedObject(ld);var lp=ls.FindProperty("m_AdditionalLightsShadowResolutionTier");if(lp!=null){lp.intValue=0;ls.ApplyModifiedPropertiesWithoutUndo();}}}
 else if(light.shadows!=LightShadows.None){light.shadows=LightShadows.Soft;light.shadowBias=.5f;light.shadowNormalBias=.3f;var data=light.GetUniversalAdditionalLightData();data.usePipelineSettings=false;var serialized=new SerializedObject(data);var tier=serialized.FindProperty("m_AdditionalLightsShadowResolutionTier");if(tier!=null){tier.intValue=1;serialized.ApplyModifiedPropertiesWithoutUndo();}}
 }
 foreach(var camera in all.SelectMany(t=>t.GetComponents<Camera>())){camera.allowMSAA=true;}
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
 CitySurfaceReview.Capture(name+"-shadow-pavement",new Vector3(-38,7,-65),new Vector3(20,3,-65));
 CitySurfaceReview.Capture(name+"-shadow-road",new Vector3(-4,10,25),new Vector3(65,2,25));
 log.AppendLine(name+": cleared stale renderer lightmap indices="+stale+"; decorative light shadow casters disabled="+decorative+"; missing scripts="+all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
 }}finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}File.WriteAllText("Library/CityLightingRepair.result",log.ToString());
 }
}

