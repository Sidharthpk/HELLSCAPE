# Story and interaction audit — 4 October 2026

Story authority: the supplied **HellScape Revised Plot.docx**. The earlier `HELLSCAPE_STORY_REBUILD.md` is an alternative proposal, not the current game canon.

## Fixes

- Dialogue requests are deduplicated while pending. Intentional repetitions within a speech and later conversations remain possible. An urgent interruption has its own generation, so completion of the old speech cannot corrupt the replacement's bookkeeping.
- Ghosts cannot restart a conversation while replying. Subsequent choice nodes wait until the reply finishes; previously they opened after 0.6 seconds and could block the reply's advance key.
- Checkpoint respawns cancel pending ghost continuations, choices, subtitles, and lock puzzles. Choice closure also stops its camera-turn coroutine.
- Lock puzzles retain ownership of their session: disabling another puzzle no longer closes the active one. Timers stop during pause/game-over. Escape cancellation cannot also open Pause on the same frame. Player interaction is blocked during puzzles and game-over.
- The silo defeat sequence rejects duplicate starts. Both outcomes retain the car-key route; the pickup handler ensures the key is in inventory. Story dialogue finishes before subsequent choice/confession beats.
- Lost/Damned transformations and ending speeches wait for dialogue instead of cutting it off at arbitrary reading deadlines. Maya's transformation no longer replaces her preceding speech before it can be read.
- Elias visibly hesitates in the apartment before the chase; the player can finish the associated dialogue.
- Echo activation uses Vane's actual office position. The `Office` container is at the origin while its rooms are around 1000 metres away. Exiting the Echo restores each ghost's previous active state instead of enabling everyone.
- J closes the evidence journal as advertised. The invoice response follows the selected evidence/witness option.
- Silo door setup can reuse existing interaction/teleport components; it no longer abandons initialization merely because an Interactable already exists.
- The legacy Apply Story Script editor command reapplies the revised canon before saving. EvidenceClue has its own Unity script file.

## Validation

Unity 6000.3.24f1 compiled the changed scripts without C# errors. The editor regression run passed 20 assertions covering scene initialization, duplicate/interrupt/drained dialogue, replay after completion, queue clearing, ghost reply-to-choice sequencing, repeated interaction, puzzle ownership/pause/cancellation, Echo state restoration, and Vane's identity.

Rerun with **HellScape → Tests → Story regression (Play mode)** while MainGameScene is open and Play mode is stopped. It uses disposable Play-mode state, exits automatically, and writes `Library/StoryRegression.result`; it does not save the scene. Script diffs pass whitespace checks.

This is a focused regression pass, not a complete controller-driven playthrough of the prologue, both silo outcomes, and every ending. Those cinematic paths still need an end-to-end visual/playability pass. Existing art, lighting, shader, and scene edits were preserved.
