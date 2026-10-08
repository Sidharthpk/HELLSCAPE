# Office horror and realm transition audit — 5 October 2026

The current runtime keeps the authored office, three puzzles, murder footage, service tunnel and silo. Scene references and the user’s existing art edits are preserved; the additional systems initialize when MainGameScene runs.

## Witness rules

- Red City: each colleague gives different evasive answers to questions about their actions, Vane and the next lead.
- Echo: the four colleagues use the latest ghostly soul appearance and desk poses. Their two-step conversations share the six-second visit budget; a question's deadline cannot exceed the remaining visit. Pause freezes both timers. Spoken replies consume visit time, so questioning is deliberately urgent.
- Denial leads to Lost, justification to Damned, and accepting personal responsibility to Confessed. Their appearance changes with the outcome. Confessed testimony is recorded in the journal.
- Timeout loses the memory, without invoking confession or story-completion callbacks. Required puzzle clues remain on physical notes.
- Vane and Tomas retain their authored main-story roles. Vane’s initial justification and later genuine confession remain distinct events.

## Realm crossing

Press V in the office: curved upper and lower eyelids close, the realm changes behind the closed view, and the eyes reopen. Movement, attacks and interaction are held for the blink and restored afterward. Later corruption adds a longer closed-eye pause and distorted audio. Pause freezes the animation. Checkpoint cancellation removes the eyelids and restores controls.

Each entry grants six seconds after the opening blink. The top-right countdown warns during the final two seconds, then another blink returns the player to Red City. Vane's conversation and ambush hold the clock; it resumes when he finishes. A running CCTV cut completes its cleanup before the return. Notes pause game time while open. On expiry, unfinished optional questioning loses that witness's memory. If the player occupies a closing doorway, the return moves them to their last clear position while the eyes are shut.

The burnt keypad, CCTV and breaker cannot consume items or solve in Red City. The Echo retains completed puzzle progress and unfinished keypad/CCTV inputs between visits. Leaving during the door animation cancels it; the Red City door stays shut. A doorway clearance check prevents closing a door onto the player. Vane’s final collapse releases the escape doorway so losing Echo access cannot trap the player.

## Escalation and presentation

The existing fuse stages drive bodies, phones, printer disturbances and the Auditor. The keypad corrupts the Echo. Evidence and witness outcomes now also provoke reactions. Actual captured images appear on the CCTV terminal; a body exists only in the captured feed, the second view shows a human double from behind, and the late feeds repeat the corridor. Existing camera-button names and the cleaning rota stay consistent.

The murder footage finishes before the breakdown takes control. The player has a short movement interval; then the Auditor withdraws so it cannot attack during Vane’s ambush. Vane stops when watched, approaches out of view, and uses floor, clearance and sightline checks for the close scare. If no valid scare position exists, he remains reachable for conversation instead of teleporting into a wall. The camera aims at his face; a restrained tint and lower caption leave it visible. Repeated Arm calls do not replay the encounter.

The transformation releases movement, drops the silo key, and becomes the existing pursuit. Exiting cancels the encounter. Respawn and interruption clean up the camera offset, character scale, materials, light, caption, controls and fade. Missing optional fade effects do not cause a null reference.

A one-time corrupted Echo corridor loop reuses a previously walked, collision-checked position. It does not remove an exit or change the puzzle route. The desk photograph changes between realms; the KEEP COLD box has the existing heartbeat/leak treatment. Evidence props use visible bounds, retain their original interactions, and use plain supported tables where appropriate.

## Asset scope

Props and portrait placeholders remain deliberately simple. The current effects use the supplied character meshes, intact/burnt materials, procedural particles and sound, rather than new sculpted facial injuries or recorded voice acting. The director supplies the gameplay escalation; bespoke headset injuries, fused-hand models and final animation/voice assets remain an art-production pass.

## Verification

`HellScape > Tests > Story regression (Play mode)` runs against disposable Play-mode state and never saves the scene. The checks cover conversation continuation, deduplication, pause, witness outcomes/timeouts, material restoration, realm puzzles, supported evidence, the actual CCTV-to-Vane handoff, transformation, escape key, pursuit and cleanup. The eyelid transition checks additionally verify closure before the swap, pause behavior and cancellation. Runtime exceptions fail the pass.

Latest results: `Library/StoryRegression.result`. Visual captures: `docs/media/office/`. The automated run supplements, rather than replaces, a full human pacing and navigation playthrough.
