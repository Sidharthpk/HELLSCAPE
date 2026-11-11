# Afternoon lighting — 20 October 2026

Prologue uses a procedural blue afternoon sky, warm directional sun at 42 degrees, light distance haze, and a saved AfternoonAtmosphere volume. The volume uses ACES tonemapping, bloom (0.32 intensity, 1.15 threshold), mild colour grading and a restrained vignette. This is distance fog, not a volumetric-light scattering effect.

High/Very High/Ultra screen-space ambient occlusion was reduced from intensity 3/radius 0.035 to intensity 0.55/radius 0.25 with depth-normal input and medium blur, reducing excessive small-scale darkening. Each scene now has exactly one active directional light. MainGameScene uses its existing story Sun reference; the additional night fill is disabled to avoid competing directional lights. Its red sky remains intact, with a low-priority CityAtmosphere bloom volume below story volumes.

Game and cutscene cameras have HDR and post-processing enabled. Reflection probes use realtime captures with all faces updated together. No fresh lightmap bake was performed.

Validation: editor utility compiled and executed; both scenes have zero missing scripts and one active directional light. Street and apartment exterior captures were inspected. A moving-camera playthrough has not been performed, so remaining temporal artifacts are not ruled out. Images: docs/media/city-cleanup/*afternoon*.png. Backups: Backups/AfternoonLighting-20261020.
