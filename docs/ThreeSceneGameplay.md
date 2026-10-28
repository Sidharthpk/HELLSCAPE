# Three-scene gameplay repair — 23 October 2026

The build starts in `MainGameScene` for the title, plays `Prologue`, then returns to the red city. The office and silo are in `OfficeSilo`. Its front exit returns to the city for exploration; its silo exit returns for the van escape and bridge encounter.

## Run state and revisits

- Inventory, evidence, and judgement now last for the run instead of resetting on each scene load. Returning to title/new game still clears them.
- Weapon ownership, ammunition, and player health carry through scene doors. Fists stay hidden when arriving with the gun equipped.
- `SceneProgress` stores scene-local interaction, fuse, keypad, CCTV sequence, door, witness, horror-stage, and investigation progress in memory. Office revisits cannot duplicate collected pickups or reset partial puzzles.
- Inserted fuses restore after pickup visibility, so they stay visible in their slots.
- The red apartment preserves its investigation across city reloads and resumes an unfinished memory after an early departure.
- Once Vane's confrontation starts, the street exit directs the player to the silo route. Once the van keys have been recovered, the office entrance directs the player to the van.

## Door handoff and Echo

- The destination finishes the door fade, dialogue, music, placement, and checkpoint handoff. The screen stays black through initial scene placement.
- Office arrival selects its entrance checkpoint; the silo return selects the escape checkpoint. Ordinary office-to-city travel also leaves a usable city checkpoint.
- Office blackout accounting is released when its scene unloads.
- Echo doorway clearance now transforms a world-space player margin into the model's coordinates. Imported doors use large scale factors and rotated axes; the old local margin could cover roughly sixty metres instead of sixty centimetres.
- Blink duration uses scaled elapsed time without a per-frame cap, keeping its duration consistent at low frame rates while still freezing during pause.

## Regression tools

- **HellScape > Tests > Three scene gameplay** exercises actual scene loads, office revisits, real fuse insertion, inventory/evidence/judgement/weapon/health transfer, vehicle entry callbacks, retries, and new-game cleanup. It restores `MainGameScene` in the editor after completion.
- **Story regression** opens `OfficeSilo`, includes a scaled/rotated doorway regression, and writes screenshots to `Library/GameplayTestArtifacts/Story` instead of overwriting imported documentation textures.
- **Checkpoint retry** now runs city/bridge checkpoints in `MainGameScene` and office/tunnel/silo checkpoints in `OfficeSilo`.
- Play shortcuts open the appropriate scene, including the office ambush debug tool.
- The editor playtest logger tolerates an empty dialogue text during scene/cutscene changes.

Results are written to `Library/GameplaySceneRegression.result`, `Library/StoryRegression.result`, and `Library/CheckpointRetry.result`. The checks use disposable Play-mode state and do not save gameplay scene changes.

## Validation completed

Run in Unity 6000.3.24f1 on 23 October 2026:

| Suite | Passing checks | Failures |
| --- | ---: | ---: |
| Three-scene gameplay | 59 | 0 |
| Office story regression | 80 | 0 |
| Checkpoint retries | 11 | 0 |

The assertion counts exclude the two final suite-summary lines. Both runtime and editor assemblies compile. All 129 serialized project-script callbacks checked across the three scene files resolve to existing methods, and there are no dangling local object references in those scene files. The editor was returned to `MainGameScene` with Play mode stopped.

These are automated integration checks, including shortened cinematics and scripted movement. They do not constitute a full manual playthrough of every delivery, combat encounter, or ending branch.
