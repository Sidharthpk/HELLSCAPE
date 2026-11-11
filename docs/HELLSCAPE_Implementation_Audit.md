# HELLSCAPE: implementation audit

8 October 2026. Unity 6000.3.24f1, URP 17.3 (Forward+), old Input Manager (`activeInputHandler: 0`), Timeline package
installed but unused (cutscenes are coroutines), no Cinemachine, no ScriptableObject story data, no disk save.

Sources read: `HELLSCAPE_Game_Document.docx` (Desktop), `HELLSCAPE_Narrative_Rewrite_Unity.pdf`,
`HELLSCAPE_Dialogue_Unity.csv` (647 rows, HS_0001 to HS_0647, no gaps, `effect` column empty in every row).

The main finding: **the game document describes the build as it already was.** Every act, puzzle, branch and ending in
it existed before this pass. What the rewrite changes is the words, plus a handful of beats. So this pass replaced
text through the systems already there instead of building a second narrative framework beside them.

## Scenes

| Scene | Build index | Holds |
|---|---|---|
| `Assets/Scenes/MainGameScene.unity` | 0 | Title screen, void, red street, office, tunnel, Silo 2, flood, escape, bridge, endings |
| `Assets/Scenes/Prologue.unity` | 1 | Office flashback, delivery run, flat, roof |

Everything else under `Assets` (`_Recovery/*.unity`, asset-pack demo scenes) is not in the build.

## What the prompt's suggested architecture maps to

| Asked for | Already in the project | Action |
|---|---|---|
| DialogueManager, SubtitleController | `DialogueBox` (queue, speaker plates, typewriter, auto and manual advance, `SayNow` interruption, duplicate suppression) | reused |
| DialogueChoiceController | `ChoiceDialogue` (up to 4 answers, deadline timer) + `GhostNPC` (node graph, karma, fate) | reused |
| DialogueDatabase | none: text was string literals in about 20 scripts | **added** `HS.cs` + `Resources/HellscapeDialogue.csv` |
| NarrativeManager, NarrativeEventManager | `RevisedPlot` (text applied on every scene load) + `RevisedPlotDirector` (scripted beats) | reused |
| DialogueTrigger | `StoryTrigger`, `Interactable`, `Teleporter`, `ReadableNote` before/after lines | reused |
| StoryStateManager, JudgementManager | `Judgement` (static), `Survivor.Rescued/Turned`, `GhostNPC.fate` | reused |
| EvidenceManager | `EvidenceJournal` (static, five ids, journal on J) + `EvidenceClue` | reused |
| HorrorEventDirector | `OfficeHorrorDirector` (stages 0 to 5), `OfficeStalker`, `OfficeEye`, `OfficeLurkers`, `OfficeColleagues`, `OfficeBodies` | reused |
| CinematicSequenceController | `KillerCutscenes`, `TurningCutscene`, `StabCutscene`, `DoorTransition`, `CutsceneHUD`, `CutsceneSkip` | reused |
| Checkpoints | `Checkpoint` + `GameOverScreen` | reused |

## Already implemented (before this pass)

- Prologue: flashback walk, van delivery run with two stops, optional label inspection flag, depot, walk home, TV, body,
  letter, stabbing, stair chase, roof grab, fall.
- Void: sixty-second walk, eye sky, shadow figures, Demon King, branding.
- Red street: wake, Tomas, Damned on the street, locked van.
- Office: Eye, Auditor, shadow men, Damned spawners, body bag drop, four colleagues (reveal at name plate, red-city
  questions, Echo interrogation with confess / excuse / deny / timeout), three fuses + locker key, desk note, keypad 1304
  (Echo only, power only), security pass + four-camera order, five evidence items, Vane confrontation and turn.
- Service tunnel chase with Tomas's intervention. Silo 2 chase, rifle fight, kill or spare, Maya's turn on mercy.
- Flood: rising blood, air bar, clinging survivors, per-character drowning and transformation cutscenes.
- Escape: van, horde, Tomas the Amalgamation, three phases, heart weak point, three-heart seal, evidence put to Vane.
- Judgement: player's account, verdict built from flags, Damned and Bound endings, post-credits line.
- Checkpoints for every act; pause menu; robot regressions (`StoryRegression`, `AmalgamationRegression`).

## Partially implemented (finished in this pass)

| Item | Was | Now |
|---|---|---|
| Dialogue data | literals spread over scripts, no ids | all 647 CSV rows loaded by id; 644 spoken, 3 are sound cues |
| Evidence put to Vane | statement and tape gave the same lines whether or not you held them | each of the three has a "have it" and a "don't have it" branch (7.4 to 7.6) |
| Player's account | one shared speech for every honest answer | each answer has its own (8.1) |
| Verdict | names joined into generic sentences | per-person rows (8.2), separate Damned / Bound speeches (8.3, 8.4) |
| Elias before the knife | silent | he speaks first (0.7), new `PrologueDirector.stalkLines` |
| Taking the box unread | nothing said | HS_0058-0059 when you collect without inspecting |
| Colleague red-city talk | menu straight away, one-line answers | greeting first, then the three questions in their own words |

## Missing (not built, and not required by the two story documents)

- **Save to disk.** State lives in statics (`Judgement`, `EvidenceJournal`, `Inventory`) and scene objects. It survives
  death and checkpoints because a checkpoint respawn does not reload the scene. Quitting loses the run. Neither story
  document asks for a save file; not added.
- **Voice acting.** Lines are text with a mumble blip.
- **`effect` / next-id / priority columns.** The CSV has none (the `effect` column is blank on every row), so those
  fields were not invented. When and in what order a line plays is decided by the existing game code.

## Broken or inconsistent (found and fixed)

1. **Maya turns Damned on the kill route too. NOT changed; needs your decision.** `SiloEncounter.Flood()` turns her
   after Elias is shot ("both routes leave her Damned for Tomas's finale"), and `AmalgamationRegression` asserts it,
   so it was built on purpose. But the game document (section 9) and the rewritten script (5.3, flood outcomes,
   Appendix B) both say that on the kill route Maya and Sam go into the flood with you. As built, HS_0473-0482 and
   the verdict rows 612 / 613 are wired but can never play. The switch is one field: clear
   `SiloEncounter.mayaTurnsOnKill` (and update that regression check) to follow the documents.
2. **`PrologueDirector.confrontLines` was never spoken**; the roof used `roofLines`, which `RevisedPlot` did not set,
   so the roof still had old text. Both are now set.
3. **Karma thresholds (+2 / -2) cannot be reached**: one +1 and one -1 answer exist in the whole game. Both documents
   note it and say to leave it. Left.
4. `PlaytestDriver.RunSilo()` is out of date for the revised plot (known, unchanged).

## Requires new assets

See `HELLSCAPE_Missing_Assets.md`.

## Requires manual Unity Editor setup

None for this pass. No scene was edited or saved: all text is applied at load by `RevisedPlot.ApplyText`, as before.
To change a line, edit the CSV row (keep the id) or the row numbers in the script named in
`HELLSCAPE_Dialogue_Trigger_Map.md`, then run **HellScape > Tests > Dialogue CSV**.
