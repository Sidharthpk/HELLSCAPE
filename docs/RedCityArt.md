# Red city art pass — 23 October 2026

- Charred facade variants retain the original building textures under soot patches, vertical scorch streaks, and pale ash. Windows lose their clean emission and glossy finish.
- Asphalt and pavement variants add faded, dry surface wear, ash patches, fine grit, and irregular broken cracks.
- Burning trees are reduced from **229 to 117**. Every other tree is removed along each detected street row, with neighboring rows offset. Disabled trees no longer consume the nearby fire-light budget.
- The red-city bloom intensity is reduced from **1.10 to 0.85**, with scatter reduced from **0.70 to 0.65**.

`BurntCityAppearance` under `HellWorld` swaps the exterior materials only while the red city is active. The original materials and Prologue assets remain available for the clean city. Generated material variants and the city-specific atmosphere profile are in `Assets/Generated/BurntCity`.

Reapply with **HellScape > Hell City > Burn exteriors, weather roads and alternate trees**. The operation reuses its material assets and repeats the same tree arrangement. A scene backup is stored at `Backups/RedCityArt-20261023/MainGameScene.unity`.

The visual review checks material activation, both URP shaders, tree density, and clean/red material restoration in Play mode. Results and screenshots are in `Library/RedCityArtPass.result` and `Library/RedCityArtPass/`. Colliders and gameplay routes are unchanged, apart from removing the inactive trees' own trunk colliders.
