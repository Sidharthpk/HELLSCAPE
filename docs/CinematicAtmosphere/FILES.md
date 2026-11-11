# File manifest

Existing files modified by this task (relative to the live task checkpoint, not the already-dirty Git HEAD):

- `Assets/Scenes/MainGameScene.unity`
- `Assets/Scripts/SleepSequence.cs`
- `Assets/Scripts/RetroLook.cs`
- `Assets/Shaders/BurntCityInput.hlsl`
- `Assets/Settings/High_PipelineAsset.asset`
- `Assets/Settings/Low_PipelineAsset.asset`
- `Assets/Settings/Medium_PipelineAsset.asset`
- `Assets/Settings/Ultra_PipelineAsset.asset`
- `Assets/Settings/Very High_PipelineAsset.asset`
- `Assets/Settings/Very Low_PipelineAsset.asset`

New implementation and review files:

- `Assets/Scripts/DeadCityAtmosphere.cs`
- `Assets/Scripts/DeadCityScatteringFeature.cs`
- `Assets/Shaders/DeadCityScattering.shader`
- `Assets/Scripts/Editor/CinematicAtmosphereSetup.cs`
- `Assets/Scripts/Editor/CinematicLightingReview.cs`
- `Assets/Scripts/Editor/CinematicLightingBridge.cs`
- `Assets/Scripts/Editor/CinematicLightingValidation.cs`
- `Tools/verify_cinematic_scope.py`

New rendering assets:

- `Assets/Generated/CinematicAtmosphere/asphalt_2-5_tracks_dry_Cinematic.mat`
- `Assets/Generated/CinematicAtmosphere/asphalt_2-5_tracks_dry_WornWet.mat`
- `Assets/Generated/CinematicAtmosphere/asphalt_2_tracks_dry_Cinematic.mat`
- `Assets/Generated/CinematicAtmosphere/asphalt_2_tracks_dry_WornWet.mat`
- `Assets/Generated/CinematicAtmosphere/asphalt_4_tracks_dry_Cinematic.mat`
- `Assets/Generated/CinematicAtmosphere/asphalt_4_tracks_dry_WornWet.mat`
- `Assets/Generated/CinematicAtmosphere/asphalt_extra_dry_Cinematic.mat`
- `Assets/Generated/CinematicAtmosphere/asphalt_extra_dry_WornWet.mat`
- `Assets/Generated/CinematicAtmosphere/asphalt_highway_dry_Cinematic.mat`
- `Assets/Generated/CinematicAtmosphere/asphalt_highway_dry_WornWet.mat`
- `Assets/Generated/CinematicAtmosphere/asphalt_square_dry_Cinematic.mat`
- `Assets/Generated/CinematicAtmosphere/asphalt_square_dry_WornWet.mat`
- `Assets/Generated/CinematicAtmosphere/AsphaltWetness.png`
- `Assets/Generated/CinematicAtmosphere/GroundFog_Cinematic.mat`
- `Assets/Generated/CinematicAtmosphere/High_PipelineAsset_ForwardRenderer_Cinematic.asset`
- `Assets/Generated/CinematicAtmosphere/LampCookie.png`
- `Assets/Generated/CinematicAtmosphere/Low_PipelineAsset_ForwardRenderer_Cinematic.asset`
- `Assets/Generated/CinematicAtmosphere/MAT_HS_CinematicStormSky.mat`
- `Assets/Generated/CinematicAtmosphere/Medium_PipelineAsset_ForwardRenderer_Cinematic.asset`
- `Assets/Generated/CinematicAtmosphere/StormSkyCinematic.png`
- `Assets/Generated/CinematicAtmosphere/Ultra_PipelineAsset_ForwardRenderer_Cinematic.asset`
- `Assets/Generated/CinematicAtmosphere/Very High_PipelineAsset_ForwardRenderer_Cinematic.asset`
- `Assets/Generated/CinematicAtmosphere/Very Low_PipelineAsset_ForwardRenderer_Cinematic.asset`
- `Assets/Generated/CinematicAtmosphere/VP_HS_CinematicDeadCity.asset`
- `Assets/Generated/CinematicAtmosphere/Water_CinematicDeadCity.mat`

Unity generated matching `.meta` files for new Unity assets and the folder. No original DeadCity materials, volume profiles, or renderer assets were overwritten. The six original pipeline assets only gained a second renderer entry; their default renderer and rendering settings remain unchanged relative to the task checkpoint.

Each copied quality renderer contains its existing feature set, its existing SSAO tuned where present, and one new `Dead City Local Scattering` subasset.
