# Office gameplay V5.2

The scene migration is saved in `Assets/Scenes/OfficeSilo.unity`. The existing office geometry, authored room doors, seven camera feeds, player, gun, Echo transitions and silo entry are reused. The task baseline (including pre-existing local changes) is in `Backups/OfficeV52-20261027`; this is not a clean Git baseline.

## Feature mapping

| Feature | Existing owner | V5.2 integration |
| --- | --- | --- |
| Patrol, detection, search, palm flash, choke and pursuit | VaneHunt | Explicit hold/release methods; original AI loop retained |
| Recovered recordings | CctvDesk, TomasTape, OfficeEntryTape, FireTape, StabCutscene, SequencePuzzle | Silo shooting/stair exit, Kessler-office entry, then fire/Vane film; one-time completion after all three |
| Red silhouette and alternate exit | Existing human Vane | Scene markers, local red backlight, temporary east-doorway blocker, west exit volume |
| Worker investigations | OfficeWitness, GhostNPC, WitnessLook, EvidenceProp | Existing evidence and journal; encountered/evidence/truth/released states; release after dialogue |
| Transformation | Existing Cry Wolf body on KesslerConfrontation | Temporarily controlled by VaneHunt; fade conceals form swap; no additional permanent enemy |
| Office access | Existing root Door/KesslerDoor | Existing Echo-only interaction, gated until transformation |
| Concealed entrance | Existing Armory door and Crowbar inventory item | Brief door-pry placeholder, then open collider and start timer |
| Locker | VaultDial and three existing secure-storage safes | Locker 03 uses L18/R07/L31; correct numbers with wrong directions fail |
| Weapon and keys | Existing Armory pistol, GunController, SiloKey | Separate physical interactions; existing gun equip event preserved |
| Nonlethal bullet response | GunController existing hit dispatch | OfficeVaneHit absorbs office hits before general enemy damage |
| Escape | Existing SiloDoor Teleporter | Requires pistol and SiloKey, then original tunnel transition and checkpoint |
| Progress/retry | Truth, SceneProgress, Checkpoint | Existing in-memory run persistence; dedicated armory retry point |

## Modified and added files

Paths below are relative to the project root. New scripts have their Unity-generated `.meta` files.

| File | Reason |
| --- | --- |
| Assets/Scripts/Office/OfficeV52Sequence.cs (new) | Scene-sequence state, cinematic holds, transformation, pry completion, breach timer, pickups, retry and cleanup |
| Assets/Scripts/Office/OfficeProgressGate.cs (new) | Check office progression before inventory is spent or existing UnityEvents run |
| Assets/Scripts/Office/OfficeVaneHit.cs (new) | Office-only damage interception; silo damage remains on its original dispatch |
| Assets/Scripts/Office/VaneHunt.cs | Add temporary sequence ownership, reuse existing Cry Wolf form, bullet stagger/cooldown and route refresh after opening the sealed room |
| Assets/Scripts/Office/CctvDesk.cs | Preserve live feeds and security-pass prerequisite; V5.2 recording branch and one-time completion |
| Assets/Scripts/Office/FireTape.cs | Optional personal-weapon dialogue trigger; repair missing-audio-source handling at the execution shot |
| Assets/Scripts/Office/SequencePuzzle.cs | Mark required recording completed without invoking the superseded office cutscene; route office terminal buttons through recording access |
| Assets/Scripts/Office/KesslerConfrontation.cs | Suppress legacy Arm/ResetFight only for the V5.2-controlled office encounter; retain its serialized references and subsequent Leave integration |
| Assets/Scripts/Office/OfficeWitness.cs | Progression gating, restrained CSV conversations, persistent release, evidence placement fallback |
| Assets/Scripts/Office/OfficeColleagues.cs | Prevent already released workers from being reactivated by the existing desk-appearance loop |
| Assets/Scripts/Office/Interactable.cs | Optional office progression gate before realm and inventory checks |
| Assets/Scripts/Office/VaultDial.cs | Optional per-entry direction validation, progress capture, cancel/retry handling; existing nondirectional locks retain defaults |
| Assets/Scripts/Office/GunController.cs | One office-specific receiver check before existing general damage dispatch |
| Assets/Scripts/Office/Checkpoint.cs | Close any open dial before restoring controls |
| Assets/Scripts/RevisedPlotDirector.cs | Initialize the scene sequence; suppress duplicate Vane ghost in the Echo; block realm input during authored sequences and the breach |
| Assets/Scripts/Objectives.cs | Read V5.2 objectives at office entrance/armory checkpoints |
| Assets/Scripts/Editor/OfficeV52Setup.cs (new) | Explicit, one-time OfficeSilo migration using existing props and references |
| Assets/Scripts/Editor/OfficeV52Validation.cs (new) | Automated scene/component integration test, with actual Play Mode recording and checkpoint flow |
| Assets/Scripts/Editor/DialogueCsvCheck.cs | Update expected row count from 250 to 288 for the 38 appended lines |
| Assets/Resources/HellscapeDialogueV5.csv | Append 10 V5.2 trigger groups; retain all existing row IDs and legacy dialogue |
| Assets/Scenes/OfficeSilo.unity | Serialized scene wiring, encounter volumes, local backlight, locker interactions and armory checkpoint |
| docs/OfficeV52Implementation.md (new) | Setup, limitations, changes and validation record |
| docs/validation/OfficeV52PlayMode.txt (new) | Saved final Play Mode check results |
| docs/validation/OfficeV52DialogueCsv.txt (new) | Saved existing dialogue workflow results |

## Inspector setup (already applied)

Select `OfficeAct/Office/Office V5.2 encounters`, component `OfficeV52Sequence`.

Required references:

- CCTV: `OfficeAct/Office/CCTV desk`; legacy encounter: `OfficeAct/Office/KesslerConfrontation`.
- Main office door: root `Door` at approximately (1003.10, 3.40, 888.58), with its existing animator, blocker, audio and interaction. The object named `OfficeAct/Office/KesslerDoor` actually gates Vane's office and is deliberately not used as Kessler's main door.
- Concealed entrance: `OfficeAct/Office/Armory door`. Requires existing inventory ID `Crowbar`, does not consume it. The old instant SetActive(false) callback alone was replaced with `OfficeV52Sequence.Pry`; unrelated callbacks were retained.
- Pistol: original `Armory pistol`, reparented below `Locker 03 contents`; original `GunController.Equip` event remains.
- Keys: original `Safe/SiloKey` pickup, reparented into the same contents. Inventory ID remains `SiloKey`, so the existing silo interaction remains compatible.
- Silo: original `OfficeAct/Office/SiloDoor`, preserving all original arrival callbacks, destination, transition and tunnel checkpoint.
- Street exit: original `ExitDoor` interaction remains available before the transformation; the office progression gate blocks that route afterward so escape uses the silo.
- Fader/player: existing scene objects, assigned in the Inspector.
- Armory checkpoint: `OfficeAct/Checkpoints/Checkpoint - Office armory`; spawn marker (1007.5, 4.25, 885), clear-zombie radius 0.

Authored positions and volumes:

| Object | World position | Box size / settings |
| --- | --- | --- |
| Vane red silhouette | 1005.1, 3.4, 906.4 | Feet marker beyond east security doorway |
| Vane blocked security exit | 1003.7, 4.9, 906.4 | 0.3 x 3 x 1.2; non-trigger, disabled outside encounter |
| Security alternate exit | 990, 4.5, 906.4 | 1.5 x 3 x 1.5; trigger volume on west side |
| Vane confrontation | 998.3, 3.4, 890 | Feet marker; placement checks nearby clear floor |
| Worker confrontation area | 998, 4.5, 895 | 4 x 3 x 3; final release arms this area; requires clear sight to Vane |
| Vane at Kessler door | 1001.8, 3.4, 888.58 | Exterior door-breach pose |
| Kessler interior | 1007.8, 4.5, 887.2 | 7.5 x 3 x 6.8; entry flag only |
| Vane red backlight | 1006, 5.2, 906.4 | Point; red, intensity 7, range 5; encounter only |

The volumes are polled in player space; no new player trigger callbacks, input mappings or physics layers are required.

Locker 03 is the third original secure-storage safe. Its closed one-piece model is hidden on unlock, revealing a small local interior and the existing pickups. Locker 01 and 02 provide optional readable documents. The maintenance note is beside security and records `Executive storage combination` in the existing journal. Discover it before prying the armory. The dial requires directions [1, -1, 1] and code [18, 7, 31], with 40 notches. A turns left, D turns right, Space confirms, Backspace undoes, F/Escape cancels. Step away from the dial to consult J; the breach continues.

Timer: 60 seconds, starting after the 2-second pry completes. Pause stops it; dial and journal do not. At 45/30/15 seconds the local warning hooks and impact audio fire. At zero the original main door is released and pursuit resumes. Uncollected items and the puzzle stay accessible. Both mandatory items collected is the success condition; pursuit resumes with a short grace period.

The shared Cry Wolf instance's Zombie animator reference is aligned with the existing `KesslerConfrontation.damnedAnim`. Temporary AI/rigidbody settings are restored on handoff. After the breach, opening the locker or journal does not freeze office pursuit; if Vane reaches the player, that interface relinquishes control before the existing choke begins.

Stagger settings: duration 1.6 seconds, cooldown 3.5 seconds, palm flash enabled. A valid hit during the cooldown is absorbed without restarting the stagger. These settings only apply while the office sequence owns the transformed actor.

## Asset hooks and remaining art work

- `Pry Animator` is optional and currently unassigned. The existing crowbar interaction had no authored hand-pry animation; the implemented placeholder rattles/opens the existing concealed door. Assign the existing animator and a `Pry` state, or bind `On Pry Started/Completed` to supplied animation events.
- `Transformation Clip` is unassigned. The safe placeholder uses a short hold and the existing screen fade to reveal the existing Cry Wolf model. A dedicated morph animation is still needed for a continuous transformation.
- Door damage stages expose `On Frame Damage`, `On Partial Breach`, `On Final Warning` and `On Breach`. There are no supplied fractured-door meshes or partially broken-door animations assigned. The timer, impact audio, local frame rattle and final opened doorway work without them.
- `On Transformation`, `On Impact` and pry UnityEvents are optional artistic hooks, not prerequisites for progression.
- Existing FireTape includes Mercer aiming, execution presentation, a rattling exit and fire lights. The follow-up CCTV revision restores Mercer shooting Tomas and climbing the silo stairs, adds a separate shot entering Kessler's office, and plays this existing fire/Vane film third. There is still no bespoke physical security-panel sabotage shot; dialogue and the journal establish that action. FireTape and the prologue both use the Beretta from Assets/Models/Gun/pistol/source/beretta.fbx, as does the armory pickup. Shot framing still needs a final cinematic review.

## Validation

The automated test is available at `HellScape > Tests > Office V5.2 integration`. It writes `Library/OfficeV52Validation.txt`. It exercises components directly and warps the player between authored areas; it is not a complete manual-input playthrough.

Initial Play Mode attempt was blocked by a Unity Bee backend crash (exit -1073741819). A clean script compilation succeeded after retry/restart. The first actual run found FireTape's missing AudioSource access at the shot, which stalled playback; that handling was corrected before rerunning.

Final full-flow run after the three-recording revision: **63 automated Play Mode checks passed, 0 failed**, including actual `GunController` hitscan shots, existing dialogue CSV validation and no game-script runtime exceptions during the run. See `docs/validation/OfficeV52PlayMode.txt`. The existing CSV check also passed (665 legacy rows; 288 V5 rows with contiguous unique IDs).

Worker orders exercised were Omar/Priya/Nadia/Ben and Ben/Nadia/Priya/Omar. The final run temporarily holds Vane during isolated worker dialogue-order checks; patrol/encounter/escape ownership are checked separately. The earlier full run exposed stagger recovery being delayed by escape grace; a valid stagger now supersedes that grace, and the final recovery/immunity checks passed.

Static scene comparison confirms all **15,921 original serialized scene objects/components were retained**, with 577 added documents after the CCTV follow-up. No original serialized object was deleted. Unity is left in Edit Mode with the scene saved.

| Required test | Result and scope |
| --- | --- |
| 1. Original patrol before CCTV | PASS: original human actor active under VaneHunt, after rebuilding its normal route grid and placing it on a patrol route |
| 2. CCTV one-time playback | PASS: all three recordings finish in order; replay request does not roll again |
| 3. Scripted hold suspends roaming | PASS |
| 4. Blocked exit | PASS: east-doorway physics ray hits the encounter blocker |
| 5. Alternate exit | PASS: west-doorway player capsule sweep is clear and crossing completes the event |
| 6. Different worker orders | PASS: both orders listed above |
| 7. Each release persists once | PASS: released flags and hidden worker states checked |
| 8. Fourth release/transformation | PASS: all-four gate plus authored area and visibility condition |
| 9. Original/transformed AI ownership | PASS: existing Cry Wolf instance, active correct animator, legacy encounter unarmed |
| 10. Echo-only main door | PASS: shut in Red City, opened using existing Echo door behavior |
| 11. Echo timeout geometry | PASS: forced expiry returns to normal realm inside Kessler's office |
| 12. Existing crowbar | PASS: existing inventory item used through Interactable; no duplicate item |
| 13. Timer start point | PASS: not running on initial interaction, running after pry completion |
| 14. Dial/journal/pause timing | PASS: dial and journal leave timer running; pause stops it |
| 15. Combination | PASS: wrong directions rejected, L18/R07/L31 accepted |
| 16. Physical item collection | PASS: opening locker does not grant items; separate existing pickups equip gun and give key |
| 17. Pistol stagger/no permanent damage | PASS: actual gun fire invokes hit receiver and leaves Vane's health unchanged |
| 18. Stagger immunity | PASS: repeat hit does not extend stagger; pursuit recovers during immunity window |
| 19. Silo gate | Required-item/progression references inspected; successful keyed transition tested. Separate manual attempt at the door without keys not run |
| 20. Existing silo handoff | PASS through original transition to Kessler tunnel checkpoint; subsequent full silo/boss chapter not played |
| 21. Armory death retry | PASS: actual death and GameOverScreen retry restore armory checkpoint/timer; completed worker and transformation flags persist |
| 22. Outside-office gameplay | Shared actor damage override ends on handoff; existing scene objects retained. Full prologue/city/later-chapter regression not run |

## Integration status and remaining work

**Implemented and verified in component-driven Play Mode:** required CCTV flow, scripted red encounter, worker liberation, transformation handoff, Echo door/timeout, crowbar opening, breach timer and timeout recovery, directional dial, separate pickups, pistol stagger/immunity, armory checkpoint retry, existing silo transition and dialogue validation.

**Implemented but not separately verified through manual input:** the complete player-driven route from the three fuses onward, every evidence pickup location, UI interruption when the post-breach hunter catches the player, intermediate scene-unload/restore combinations, and full later chapters. These need a manual playthrough.

**Requires an asset/art pass:** dedicated crowbar hand-pry animation, continuous human-to-Cry-Wolf transformation, staged damaged-door models/animations, and an additional CCTV shot of physical security sabotage. Inspector hooks and functional placeholders are already connected as described above; these are not being claimed as finished cinematics.

**Blocked by missing project functionality:** no additional system is required for the implemented run/checkpoint flow. The project currently keeps this story state in memory; disk-save persistence would be a separate feature.

Persistence follows the project's existing in-memory run model. No new disk-save format is introduced. Quit/relaunch persistence beyond what the project already supports is not claimed.

## CCTV follow-up

The active desk now plays three recordings: Mercer shoots Tomas and climbs back up the silo stairs; Mercer enters Kessler's office; the existing fire/Vane execution recording. The red-light encounter waits for all three. See [OfficeCctvRecordings.md](OfficeCctvRecordings.md) for the added/modified scripts, recording references, camera stage, reviewed frames and latest focused test results. The seven live feeds are retained.
