# Dead City lighting

**Superseded 2026-11-01 by the brown city, now pure darkness (2026-11-02): street lamps are the only city light, no tree fire, 56 trees, no baked lighting.** Retune with the constants in `Assets/Scripts/Editor/BrownCityLook.cs`, then **HellScape > Hell City > Brown City Look** (it does not save the scene). Do not re-run Dead City Look or Cinematic atmosphere: they bring back the green fires and teal air. If static objects change, run **HellScape > Hell City > Bake Occlusion** (occlusion data only, no lightmaps). The volumetric fog is `DeadCityScatteringFeature` + `DeadCityScattering.shader`: density and near colour read `RenderSettings`, so cutscenes and the blood tide drive it, and it fades into the skybox texture by the camera's far clip. Rollback: `Backups/BrownCity-20261101/`.

The open city's look: teal storm, charcoal buildings, amber lamp pools, emerald tree fires, black water. MainGameScene only. Prologue and OfficeSilo were not changed by this pass.

Re-apply or retune: edit the constants at the top of `Assets/Scripts/Editor/DeadCityLook.cs`, then run **HellScape > Hell City > Dead City Look**. It is safe to re-run. Do not run **Burning Trees, Lamps And Look** or **Bake Hell City Lighting**: they put the old fog back and re-bake.

## How it is wired

No new manager. It feeds the systems already there.

| Piece | Where |
| --- | --- |
| Sky, fog, key light, ambient on waking | `SleepSequence.ApplyLook`, fields on *Hell Wake (after the fall)*; `deadCityAir` is on in MainGameScene only |
| Grade | `Hell Global Volume` now uses `VP_HS_DeadCity_Base` (the name stays: DemonBoss finds it by name) |
| Red for the chase and bridge | `SleepSequence.BloodTide(seconds)` / `BloodTideOff()`, called from DemonBoss: chase start, `LightArena`, `ResetChase` |
| Lamps | `HellTrees.lamps`: the nearest `maxLamps` are lit, like the tree fires |
| Tree fire | Same particle systems, green `DC_*` materials and textures; the light count is `HellTrees.maxLit` |

## Sky

`03_Storm_Sky_VISUAL_CONCEPT.png` was rejected as a skybox: its left/right seam differs about twice as much as neighbouring columns, and it has a painted skyline well below the equator. Instead `StormSkyCube.png` is the existing seamless red cross, regraded by brightness into the palette, with emerald added from the view direction so it stays seamless. Horizon and below blend to the fog colour so towers and the sea fade into it.

The kit's tint and exposure are baked into the pixels and the material stays neutral. A skybox tint darkens the horizon below the fog colour and they stop meeting.

## Values: kit vs final

| Setting | Kit start | Final | Why |
| --- | --- | --- | --- |
| Skybox tint / exposure | #C8E0DD / 0.75 | neutral / 1 (in the texture) | Horizon has to equal the fog |
| Fog | Exponential, #294B50, 0.004 | Exponential, #294B50, 0.0035 | Slightly more road view |
| Ambient (gradient) | #446B70 / #294A4F / #101C20 | #47605F / #2B3D3F / #11191B | Less saturated: every wall came out the same teal |
| Key light | #8DAFB5, 0.30, shadow 0.75 | same; pitch 26 deg; bounce 0.8 | |
| Post exposure, contrast | -0.15, +18 | same | |
| Saturation | -14 | -18 | Keeps the sky from going neon |
| Color filter, white balance | #E2EEEA, -8 / -3 | same | |
| Tonemapping | ACES | ACES | |
| Bloom | 1.10 / 0.22 / 0.62, HQ off | same | |
| Vignette | #030909, 0.20, 0.45 | same | |
| Chromatic aberration | 0.015 | same | |
| Lens distortion, motion blur, DoF | off | off | The old profile's DoF was blurring the city |
| Film grain | Thin1, 0.08 | not in this profile | RetroLook keeps it (Thin1, 0.15) so the pause menu's grain toggle still works |
| Working lamp | #D49A45, about 1.5, 11 m, 95 deg | 1.5 x height squared, range 2.6 x height + 3, 95 deg, inner 15 deg, no shadows | URP units: 1.5 lights nothing at pole height |
| Corrupted lamp | #B5DBAA, about 1.0, 9 m | 0.9 x height squared, range 2.2 x height + 3 | |
| Tree light | about 1.2, 6 m | 34, 15 m; 10 lit at once | Same units reason |
| Flicker | about +-12% | floor 0.8, span 0.5 on noise | |
| Mist | 0.06 to 0.15 alpha, #416F6B | alpha 0.1, follow-mist rate 70 to 28, plus pockets at the burning trees; nothing within 10 m of the camera, full by 18 m | User asked for a clear halo. No shoreline pockets: water writes no depth, so they speckled |
| Water | smoothness 0.70 | 0.15 | At 0.70 the sea mirrored the fog and came out brighter than the sky |

Lamp mix: of the poles HellCityPass left working, about 3 in 4 amber, 1 in 5 green, the odd one dark. 263 amber, 76 green, 771 dark (dark includes the poles that were already off), 407 lights in all, 44 lit at a time.

## Files

New: `Assets/Scripts/Editor/DeadCityLook.cs`, `Assets/Generated/DeadCity/` (13 MB), `docs/DeadCityLighting/` (screenshots).

Changed: `Assets/Scenes/MainGameScene.unity`, `Assets/Scripts/SleepSequence.cs`, `Assets/Scripts/HellTrees.cs`, `Assets/Scripts/Office/DemonBoss.cs`, `Assets/Scripts/Editor/VaneFinalFormBuilder.cs` (Vane 24 m to 12 m).

## Rollback

`Backups/DeadCityLighting-20261030/` holds the pre-change scene, materials, profiles and the two scripts it copied. Copy `MainGameScene.unity` back over `Assets/Scenes/` for the old look. The script fields do nothing while `deadCityAir` is off, so the code can stay. The backup does not include `DemonBoss.cs`; its three added lines are harmless on their own.
