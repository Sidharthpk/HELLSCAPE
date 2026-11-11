# Main game: Versatile Studio demo layout

`Assets/Scenes/MainGameScene.unity` now uses the complete authored `demo_city_night` scene as its environment base. The original scene path and GUID are retained for title, prologue, checkpoint and build-settings references. The source demo asset is untouched.

The previous per-building replacement and old street building/road roots have been removed from both scenes. Prologue now shares the demo city layout and retained story landmark placement. Empty legacy reference hierarchies contain no scenery or collision. The requested flat, office, bridge and tunnel are retained.

All 5,971 transforms under the demo's STATIC_MODELS hierarchy match the source positions, rotations and scales. Buildings were not rearranged or resized. Selected perimeter fence sections and one tree are inactive to allow access to the story structures. A separate generated collision mesh opens these passages without changing the imported model.

- Home: north of the central avenue; arrival approximately (-38, 1.8, 49).
- Office exterior: north of the city, reached through the service lane. The original interior and door teleporters are retained.
- Van: office forecourt, approximately (118.5, 3, 145). Escape checkpoint follows it.
- Ending bridge: northeast extension reached from the east avenue. Its encounter, triggers and checkpoint move together.
- Office/silo footbridge and Kessler tunnel: retained with their existing internal route and teleporter links.
- Navigation and city collision assets: `Assets/Generated/DemoCity`.

Validation: zero changed demo transforms; zero missing scripts/materials; complete navigation paths from home to office and office to bridge; driving-route clearance probes; Play-mode wake-up, movement, van grounding and Escape checkpoint verified. The full story and driving sequence have not been played end to end.

Migration entry point: `DemoCityMigration.Build()` (one-time operation; refuses an already migrated scene). `FinishNavigation()` rebuilds the generated access collision and navigation. Pre-migration scene snapshots are in `Backups/CityMigration`.

## Compact shared layout — 2026-10-19

The unused outer highway sections 3–18 are disabled, distant scenery is culled, and the generated city collision is bounded to the playable district. Downtown buildings retain their authored placement and full scale. Highway closures mark the ends of the remaining frontage. The approximately 1.1 km escape driving route is retained: shortening it across the central block would intersect buildings. Prologue delivery locations, traffic and pedestrian routes have been relocated to the new streets.

Both scenes have 5,971 matching city transforms and zero missing scripts. Navigation paths from home to office and office to bridge are complete after compaction. The full revised prologue and story have not been played end to end. Unity still reports large-triangle warnings on imported blood-pool meshes.

Scene backups: `Backups/CityCompact-20261019`. Actual Unity screenshots: `docs/media/map-20261019` (main overview, flat, bridge, tunnel, prologue overview). These use temporary brighter preview lighting, with game lighting restored afterward. `SharedCompactCity.Build()` is a guarded one-time migration helper; saved scenes are the final layout.
