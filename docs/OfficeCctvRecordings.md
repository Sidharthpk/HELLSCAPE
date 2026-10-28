# Recovered CCTV recordings

OfficeSilo now plays three recordings automatically, in this order:

1. **Silo Two:** Mercer shoots Tomas, Tomas falls into the silo, and Mercer walks back up the existing stairs before the camera cuts. This reuses the authored silo camera, landing, Mercer/Tomas actors and stair-exit waypoints.
2. **Kessler's office:** a separate fixed camera shows Mercer approaching the door, opening it, crossing the threshold and walking into the office. The room and animated doorway are visual copies of the actual authored Kessler room, placed offstage. Gameplay geometry and the real Echo-only door are unaffected.
3. **Vane office fire/execution:** the existing fire dialogue and shooting sequence now play inside a visual copy of the authored Vane office, with its desk, executive chair, sofa, bookcases and actual entrance.

The seven live monitor feeds remain unchanged. The red-backlight Vane encounter triggers only after the complete three-recording playlist.

## Scene references

Select `OfficeAct/Office/CCTV desk`:

- `CctvDesk.recoveredRecordings[0]`: `OfficeAct/Flashback (CCTV)/Cutscene - Derek kills Tomas`. Its old object/field names are retained to preserve references; the shooter is the existing Mercer model.
- `recoveredRecordings[1]`: `OfficeAct/Flashback (CCTV)/Cutscene - Mercer enters Kessler office`.
- `recoveredRecordings[2]`: `OfficeAct/Flashback (CCTV)/Cutscene - Mercer fire order`.
- `fireTape` remains assigned to the third recording for compatibility.

The second recording's `OfficeEntryTape` exposes its Mercer animator, recording-only door animator, route, walking speed, door timing and opening/closing holds. Its stage is at (3200, -300, 3000). Its camera frames the doorway from the corresponding inside corner of Kessler's room.

The isolated office stage uses the existing office meshes, Echo material mappings, Mercer model/walking and idle poses, animated door take and Beretta. The walking Mercer in both recordings carries the same existing pistol.

`StabCutscene.recordingStamp` supplies each camera's identity after the older Kessler-to-Vane text migration. Runtime-only playlist flags hold the input lock and black transition between recordings. The existing CutsceneHUD handles gameplay HUD suppression and restoration.

## Files

| File | Change |
| --- | --- |
| Assets/Scripts/Office/CctvDesk.cs | Ordered recorded-camera playlist; complete progression only after every recording; hold controls/HUD across cuts |
| Assets/Scripts/Office/TomasTape.cs | Reuse original shooting/stair sequence; reset walker animation; expose shot/exit/height diagnostics |
| Assets/Scripts/Office/StabCutscene.cs | Optional explicit recording stamp and runtime-only seamless playlist flags; default single-shot behavior unchanged |
| Assets/Scripts/Office/OfficeMinimap.cs | Hide the player room-name label along with the map during cutscene HUD suppression |
| Assets/Scripts/Office/OfficeEntryTape.cs (new) | Short existing-camera cutscene sequence for Mercer opening and entering Kessler's office |
| Assets/Scripts/Editor/OfficeCctvRevision.cs (new) | Idempotent scene wiring, visual-only room/door copies, existing pistol copies and camera labels |
| Assets/Scripts/Editor/OfficeV52Validation.cs | Verify all three recordings, actual stair ascent, doorway crossing and deferred Vane encounter; capture review frames |
| Assets/Generated/Meshy/MercerOfficeEntryCctv.controller (new) | Existing Mercer walk/idle clips for the recording actor |
| Assets/Scenes/OfficeSilo.unity | Playlist, isolated office recording stage, additional camera/overlay, carried pistol on the stair-exit actor |

New assets/scripts have Unity-generated .meta files. The baseline for this revision is in `Backups/CctvThreeRecordings-20261027`.

## Validation

Run `HellScape > Tests > Office V5.2 integration` in OfficeSilo. This uses actual Play Mode, existing cutscene playback and gun/checkpoint flows, with automated dialogue advancement and test warps.

The checks verify that Mercer fires, finishes his stair route with at least 0.5 m of upward travel, enters Kessler's office, and that the fire recording follows. They also verify that the Vane encounter does not start after either of the first two recordings.

Review frames are captured to `Library/OfficeCctvSiloShot.png`, `Library/OfficeCctvStairExit.png` and `Library/OfficeCctvKesslerEntry.png`. Final saved test results and reviewed frames are listed below after the final run.

Final results:

- **63 full office-flow checks passed, 0 failed:** [OfficeV52PlayMode.txt](validation/OfficeV52PlayMode.txt).
- **25 focused CCTV review checks passed, 0 failed:** [OfficeCctvReview.txt](validation/OfficeCctvReview.txt), rerun after applying pre-fire office materials, including checks for clean wall/carpet/ceiling and no burnt material slots.
- No game-script runtime errors were captured in either successful run.
- Reviewed frames: [silo shooting](media/cctv/mercer-shoots-tomas.png), [stair exit](media/cctv/mercer-stair-exit.png), [Kessler office entry](media/cctv/mercer-enters-kessler-office.png).
- Every pre-existing serialized scene object/component was retained. The visual-only recording stage and supporting camera/overlay/pistol copies add 494 serialized documents.
- Unity is left in Edit Mode. No new animation assets are required for these two recordings; the existing shoot, death, walking and door takes are used.

This does not claim a full manual-input playthrough or a new physical security-sabotage shot. Existing V5.2 animation/art placeholders outside these recordings remain documented in [OfficeV52Implementation.md](OfficeV52Implementation.md).

## Pre-fire entry materials (2026-10-28)

The second recording uses the clean Prologue office wall, carpet, ceiling and furniture materials. Supply props use their original clean KV materials; newspaper and ceiling fixtures use their original Perishables materials. The copied doorway also receives the Echo clean mapping. These assignments apply on every CCTV builder run, including an already-created entry stage. Only renderer slots in the isolated office-entry cast change; shared source assets, the present-day office and the third fire recording retain their materials. Backup: `Backups/CctvCleanOffice-20261028`. The updated entry frame was visually reviewed in playback.

## Vane execution setting (2026-10-28)

`StageVaneOffice` replaces the third recording's generic backdrop with copies of the actual Vane office meshes and doorway. The previous placeholder wall, floor and door are disabled for recovery; gameplay geometry remains intact. Mercer, the fixed camera and fire lights are positioned within the copied room. The recording stamp reads VANE OFFICE. Existing dialogue, fire rattle, gunshot and playlist progression are preserved. The recording door is sampled into its closed pose while inactive. Backup: `Backups/CctvVaneRoom-20261028`.

Vane-room playback frame: [Vane office execution](media/cctv/vane-office-execution.png). Focused playback validation: 25 passed, 0 failed; no game-script runtime errors.
