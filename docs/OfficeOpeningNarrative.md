# Office opening and worker narrative

## Playable order

1. Arrive in the blackout. A nonblocking HUD instruction shows Q for the palm light and left click for the flash. No talking tutorial is attached to ordinary narration.
2. The Driver decides to restore electricity. The objective points to the electrical room in Maintenance.
3. Approach or inspect the breaker to discover three empty slots. Only then does the fuse objective appear. The first inspection consumes nothing. A contextual instruction explains V and the Echo contacts.
4. Leaving the electrical room triggers the watching thought and Vane running in over the existing collision-checked route grid. He accuses the Driver, interrupts the explanation, then notices the mark.
5. The player aims and uses the real palm flash. Vane is blinded through the existing hit/visibility checks; there is no automatic timed flash. He resumes roaming and periodically shouts while moving.
6. Find the fuses. The existing boardroom locker key, Tomas's padlocked locker, and Echo-only breaker remain part of the puzzle. CCTV requires all three fuses installed and the security pass.
7. Watch the existing three recordings, including the pre-fire Kessler-office entry and Vane-office execution. The post-CCTV stalking position and alternate exit follow.
8. Tell Priya, Ben, Nadia and Omar what the footage revealed: Kessler ordered the fire, Mercer disabled security and locked the exits. All four can be released in any order; their personal evidence is optional context rather than a further mandatory release gate.
9. After all four are free, returning through the office enables Vane's existing transformation. The objective now names Kessler's pistol and the crowbar in the cleaner's closet. An already-collected crowbar advances this instruction naturally.
10. Reach Kessler's office, locate the concealed armory and its combination clue, and pry it open. A visual copy of the actual crowbar levers at the door with the existing first-person reach take; the door shakes before opening. The existing breach timer starts after this action, followed by the locker, pistol, keys and silo escape.

## Talking tutorial

Looking at an available worker interaction presents F / E / numbered-answer instructions once. The full talking card is requested by the first worker conversation, rather than by the first line of office narration. Early talks remain available through the original colleague encounter system and in the Echo. They do not release workers before the recordings.

## Opening dialogue

### Electricity

**Driver:** Nothing's running. I need to get the electricity back on. There should be a breaker in the electrical room.

### Empty breaker

**Driver:** Three empty slots. Someone's taken the fuses out. I'll have to find them before any of this will work.

### Outside the electrical room

**Driver:** Those footsteps stopped when I stopped.

**Driver:** Someone's out here.

### Vane confrontation

**Vane:** You. I knew that walk.

**Driver:** Vane?

**Vane:** Mercer came into my office with your name in his mouth. Said you'd been talking. Said everything was coming down because of you.

**Driver:** He shot my brother. I was--

**Vane:** Don't. You don't get to give me your version and walk away. I've had nothing but your name to think about in here.

**Driver:** Listen to me. Whatever he told you--

**Vane:** I'm going to make sure you stay long enough to--

**Vane:** Your hand. Open it.

**Driver:** Stay back.

**Vane:** That mark wasn't there before. What did you bring in here?

### After the flash

**Vane:** Get that thing away from me!

**Driver:** It stopped him. Only for a moment. I need those fuses.

## Worker release conversations

### Priya

**Priya:** I kept telling them help was coming. They were looking at me.

**Driver:** I saw the recording. Kessler knew you were inside. He told Mercer to start the fire.

**Priya:** Then there was never anyone coming.

**Driver:** You tried to keep them calm. They used that to keep you there. You can stop waiting, Priya.

### Ben

**Ben:** I keep going back to the servers. If I can get the alarms online--

**Driver:** Ben, I watched Mercer shut them down. Kessler ordered it. You couldn't have brought them back.

**Ben:** So it was working. Before he touched it.

**Driver:** Yes. Your system worked. They killed it before they killed you. You don't have to fix it again.

### Nadia

**Nadia:** Electrical fault. Those are the words I remember. Why do they feel wrong?

**Driver:** Because it wasn't an accident. I saw Kessler give the order. Mercer locked you in and started the fire.

**Nadia:** The report was ready before it happened. They'd already decided what we were allowed to be.

**Driver:** You were a witness, Nadia. That's why they needed the lie. The recording is still there. Your name is still yours.

### Omar

**Omar:** I checked that exit. I pulled the handle myself.

**Driver:** You did. The recording shows Mercer locking it afterwards. Kessler knew all four of you were still inside.

**Omar:** I've been checking it ever since. I thought I missed something.

**Driver:** You didn't miss anything. They took the way out after you checked it. Put the keys down, Omar.

## Implementation and verification

`OfficeOpening` is initialized by the existing scene sequence and uses Truth/checkpoint state. It owns the new opening, contextual HUD hints, first flash handoff, roaming shouts, and procedural crowbar/reach action. `VaneHunt` retains collision-checked routes and blind/patrol/chase behaviour. The seven live CCTV feeds and three recovered recordings remain intact. Existing worker evidence and Echo memories are retained.

Dialogue source: `Assets/Resources/HellscapeDialogueV5.csv`. New rows retain existing row IDs and append new IDs. Backup: `Backups/OfficeOpeningFlow-20261028`.

The pry action reuses the existing crowbar mesh and reach animation with scripted levering; it does not add a new motion-captured hand-grip clip. The existing Vane transformation transition is retained.


The first-person controller now receives both the camera pitch and body heading when the confrontation hands look input back. Contextual mark lighting uses a forward spotlight, and the objective announcement waits until the first flash is complete so it cannot cover Vane's face.

## Verification

The actual Play Mode integration run covers the arrival controls, deferred soul tutorial, optional early talk, breaker discovery, Vane's run-up and actual PalmLight hit, key/locker/fuse search, Echo breaker, security power/pass prerequisites, all three recordings, worker releases in a non-default order, transformation, visible crowbar action, breach timing, pause/journal, death/retry, gun stagger and silo handoff. Saved report: [OfficeOpeningPlayMode.txt](validation/OfficeOpeningPlayMode.txt). Review images are stored under `docs/media/office-opening`.

Final result: **90 Play Mode checks passed, 0 failed; no game-script runtime errors.** Unity was returned to Edit Mode.

Reviewed frames: [palm controls](media/office-opening/palm-controls.png), [Vane confrontation](media/office-opening/vane-first-confrontation.png), [crowbar pry](media/office-opening/crowbar-pry.png).
