# HELLSCAPE: narrative integration report

8 October 2026. Companion files: `HELLSCAPE_Implementation_Audit.md`, `HELLSCAPE_Dialogue_Trigger_Map.md`,
`HELLSCAPE_Missing_Assets.md`, `HELLSCAPE_Testing_Checklist.md`.

## What was done

The rewritten script (647 rows) now drives every spoken line in the game, by id, through the systems that were already
there. No scene file was edited. No package was added or upgraded. The player controller and input were not touched.

- **Dialogue database.** `Assets/Resources/HellscapeDialogue.csv` is the supplied CSV, byte for byte.
  `Assets/Scripts/HS.cs` loads it and hands lines to the existing `DialogueBox` format. Code names rows, never text:
  `HS.S("20-31")`.
- **644 rows are spoken** at the events listed in the trigger map. **3 rows are sound cues only** (HS_0122 scream,
  HS_0302 disconnect, HS_0435 gunshot) and have no subtitle; HS_0066 (static) sits inside a spoken range and is
  skipped the same way.
- **Checker.** *HellScape > Tests > Dialogue CSV* fails if a row is missing, malformed, uses a glyph the pixel font
  lacks, or is not wired to anything.

## Files

New: `Assets/Scripts/HS.cs`, `Assets/Scripts/Editor/DialogueCsvCheck.cs`, `Assets/Resources/HellscapeDialogue.csv`,
and the five documents in `docs/`.

Changed (text source only unless noted):

| File | Change |
|---|---|
| `RevisedPlot.cs` | every field it writes now comes from CSV rows; the four colleagues' dead conversation text removed; note texts from the script's Appendix A |
| `RevisedPlotDirector.cs` | radio briefing, Tomas on the street, Echo entry, phone replay, evidence put to Vane (**now branches on holding the evidence**), player's account (**own speech per answer**), post-credits |
| `Office/OfficeWitness.cs` | colleague conversations rebuilt from rows; red-city talk opens with a greeting |
| `Office/GhostNPC.cs` | `opening` (lines before the first question); "You:" lines inside a ghost's reply stay yours |
| `DialogueBox.cs` | `You: text` parses as the player's line |
| `EndingSequence.cs` | verdict built from rows; void lines and three spaced whispers |
| `Office/OfficeHorrorDirector.cs` | PA pool, phones, printer, fuse / keypad / tape reactions |
| `DialogueRewrite.cs` | prop lines; `Starts` table for long saved texts |
| `Prologue/OfficeFlashback.cs` | five conversations |
| `Prologue/PrologueDirector.cs` | **new beat:** `stalkLines`, Elias speaks before the knife |
| `Office/SiloEncounter.cs` | **new beat:** `killTalkLine` then a second shot on the kill route; `mayaTurnsOnKill` switch (default = as built) |
| `Office/HordeChase.cs` | `clingLineAlone` when Sam is not in the van |
| `CarHealth.cs`, `Office/DemonBoss.cs`, `Office/OfficeEye.cs`, `Office/OfficeLurkers.cs`, `Office/KesslerChase.cs`, `Office/KesslerConfrontation.cs` | single barks |
| `Editor/StoryRegression.cs` | waits for the spoken opening before the first Echo question |

## Source conflicts and how each was settled

1. **Maya on the kill route.** Documents A and B: she goes into the flood with you and can be saved or lost. The build
   (and its regression test): she turns Damned on both routes. **Left as built**, because it was deliberate and tested,
   behind `SiloEncounter.mayaTurnsOnKill`. Until that is cleared, HS_0473-0482 and verdict rows 612 / 613 cannot play.
   **This is the one decision that needs you.**
2. **Your stated preference earlier today ("fewer lines, plain wording") against a 647-row rewrite.** The rewrite was
   given as the authority for dialogue, so it went in whole. Several beats are now long (Tomas on the street is 19
   lines, the bridge arrival 13). Every line can still be skipped with E.
3. **Where the big bridge conversation happens (7.2).** The script places it all at the bridge. In the game Tomas
   appears at the start of the van chase and again at the bridge end. Split: 506-509 at the office, 504-505 and 510-522
   at the bridge. No gameplay moved.
4. **Ending A after a denial.** Rows 617-618 and 621-622 say you "finally named" what you did. If you were damned for
   answering "None of this was my fault" they contradict the scene, so that path plays 619-620 and 623-627 only.
5. **Kill route order (5.3).** The script has a five-line exchange before the gunshot, but the click that chooses
   "kill" is itself a shot. Now: your click is a first shot, the exchange plays, a second shot ends it.
6. **Fuse scare timing (3.7).** The script puts "everybody's at their desks" after all three fuses; the game does it
   on the first fuse as part of the escalation. Kept on the first fuse; the breaker rows (268, 273-274) play on the third.
7. **Cleaning rota.** Appendix A lists the times in order; the game shuffles them because ordering them is the puzzle.
   Kept shuffled.
8. **Clinic order.** The script has the employee talk, then the optional inspection. In the game you can inspect
   before or instead of talking; taking the box unread adds HS_0058-0059 to the end of the talk. Mechanics unchanged.
9. **Narrated objectives and key prompts.** The script drops them. The ones a player needs were kept as short
   italic narration (listed at the end of the trigger map).
10. **CSV has no machine-readable conditions.** `effect` is blank on all rows; `trigger` and `condition` are section
    labels. Branch gating therefore lives in code, against the existing flags, as listed in the trigger map.

## Persistence

Unchanged, and checked by reading the code: `Judgement` (Elias, karma), `EvidenceJournal` (five items, container
inspected, Vane / player confessed), `Survivor.Rescued/Turned`, `GhostNPC.fate` and `Inventory` all survive death,
because a checkpoint respawn does not reload the scene. `SiloEncounter.RespawnFight` does nothing once the choice is
made, so kill and spare cannot both be set. The container flag crosses from the Prologue to the main scene. There is
no save to disk.

## Verified

- Dialogue CSV check: PASS, 647 rows, 644 wired, 3 sound cues.
- `StoryRegression` (Play mode, office act end to end): PASS, all checks, no runtime exceptions.
- `AmalgamationRegression` (Play mode, kill route flood, fusion cutscene, boss death, evidence put to Vane): PASS.
- Verdict text read back for kill + damned and spare + bound.
- Project compiles with no errors.

## Not verified (not played by a person or a robot in this pass)

- The whole Prologue with the new lines, including Elias's new exchange before the stab.
- The void whispers' timing, Tomas's 19-line street talk.
- Silo: the new second-shot kill sequence; the spare route with the new lines.
- The flood barks, the van chase barks, the "van wrecked" line.
- The final answer screen and both ending speeches on screen (their text was checked, not their staging).
- The post-credits scene.
- How the longer speeches sit against action: several are sent with `SayNow`, which cuts whatever was queued.

## Remaining

- Your call on conflict 1.
- A human playthrough against `HELLSCAPE_Testing_Checklist.md`.
- The assets in `HELLSCAPE_Missing_Assets.md`.
