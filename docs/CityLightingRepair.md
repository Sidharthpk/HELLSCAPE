# Lighting and pavement repair — 20 October 2026

- Retained the project's realtime lighting strategy. Detached baked lighting data and reset renderer baked/realtime lightmap indices in MainGameScene and Prologue. No fresh lightmap bake was performed.
- Shadow-enabled URP presets use four cascades, a 90 metre shadow distance, soft filtering, lower normal bias and 4x MSAA. Low presets that disable shadows remain unchanged.
- Decorative city lights no longer all request shadows. Up to eight active lights nearest the story streets retain low-resolution shadow maps; story/interior light shadows remain enabled where originally present. Directional lights use soft shadows. A subtle directional night fill and restrained ambient fill keep the main city readable without the obsolete lighting data.
- The footpath uses a separate texture copy imported at 512px with trilinear filtering, 8x anisotropy and a slight mip bias. Tile layout/UVs are preserved; material normal intensity is reduced from 0.25 to 0.06. Original demo texture is untouched.
- Backups are under `Backups/CityLighting-20261020`. Review images are `docs/media/city-cleanup/*shadow*.png`.

Checks: the editor utility compiles and executes; both scenes have zero missing scripts. Captures are editor camera renders with the saved scene lighting, not a full gameplay walkthrough.
