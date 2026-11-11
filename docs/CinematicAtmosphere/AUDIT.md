# Cinematic atmosphere audit — 30 October 2026

Unity 6000.3.24f1; installed Universal RP 17.3.0. Live Editor confirmed MainGameScene, Ultra quality, Forward+ (renderer mode 2), Render Graph compatibility mode OFF. The legacy m_EnableRenderGraph field is obsolete; current RenderGraphSettings enables Render Graph. No volumetric package or custom fog Renderer Feature found. Existing SSAO, Office Outline and PSX Dither features must be retained. No Decal Renderer Feature present.

Existing runtime ownership: SleepSequence.ApplyLook applies exterior sky/fog/sun/ambient and BloodTide handles crimson chase/bridge grading. HellTrees pools 44 streetlights and 10 fire lights. InteriorDarkness owns interior fog and Echo lighting; RevisedPlotDirector.SetEcho delegates to it and EchoOfficeMemory. DemonBoss changes fog, sun and Hell Global Volume for scripted events. Existing camera anti-aliasing is preserved.

Global Volumes: Hell Global Volume priority 1 (VP_HS_DeadCity_Base), City Atmosphere -5, Day Global Volume 1, Blood Tide Volume 2, plus runtime RetroLook -10. City grading must use a separate profile, preserve blood volume, and avoid duplicate overrides. RetroLook owns the accessibility grain toggle.

Sky: Generated/DeadCity/MAT_HS_DeadCity_StormSky (cubemap). Fog: exponential .0035, #294B50. Ambient #47605F/#2B3D3F/#11191B, key light .3, #8DAFB5. Existing Ground Fog (Particle Pack) plus Local Mist pockets, teal soft particles, camera clear halo 10–18 m. Existing green DC flame flipbooks and pooled lights. Water_DeadCity uses the working water shader with .15 smoothness. Existing asphalt variants disable environment reflections and have .12 smoothness. Many lamps use height=3 from self-intersections.

13 existing realtime reflection probes, OnAwake, baked data already removed before this task; LightmapSettings has zero lightmaps and Game Realtime Lighting disables baked/realtime GI. Probes can be reused; no planar reflections required.

Backup: Backups/CinematicAtmosphere-20261030/pre-overhaul.zip plus MainGameScene-live-before.unity (includes pre-existing unsaved Editor state); git status and HEAD saved alongside. Existing user modifications were not committed, reset, or discarded.

Planned writes: MainGameScene; new DeadCityAtmosphere.cs, DeadCityScatteringFeature.cs, DeadCityScattering.shader; editor-only CinematicAtmosphereSetup/CinematicLightingReview. New isolated assets under Generated/CinematicAtmosphere: copied Dead City profile, copied sky/water/mist materials, asphalt material variants and generated wetness/cookie textures, copied quality renderers with tuned existing SSAO plus local scattering feature. Each quality pipeline receives an additional renderer entry; only this scene’s player/car cameras select it. Original renderer/profile/material assets remain intact. No building/road/lamp/gameplay transforms, meshes, colliders, NavMesh, AI, physics, story or objectives are to be changed.

Benchmark: actual PlayerCamera, position (-10,2.8,28), look target (12,5,85), original FOV, 1600x900 capture, near van/intersection with existing lamp poles and distant towers. Baseline Play Mode screenshot and Editor frame observations saved before edits. No standalone performance claim is implied.
