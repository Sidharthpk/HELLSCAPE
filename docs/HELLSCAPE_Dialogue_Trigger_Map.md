# HELLSCAPE: dialogue trigger map

Every row of `Assets/Resources/HellscapeDialogue.csv` and the game event that speaks it. Rows are named in code as
`HS.S("20-31")` / `HS.T(160)` (`Assets/Scripts/HS.cs`). "RP" = `RevisedPlot.ApplyText`, which writes the text into the
existing component fields on every scene load; "RPD" = `RevisedPlotDirector`.

Check that nothing is missing or unwired: **HellScape > Tests > Dialogue CSV** (result in
`Library/DialogueCsvCheck.result`).

## Prologue (Prologue.unity)

| Rows | Event | Where |
|---|---|---|
| 1-4 | Flashback: you reach Omar in the hallway | `OfficeFlashback` |
| 5-10 | Flashback: Nadia's desk | `OfficeFlashback` |
| 11-13 | Flashback: Priya's desk | `OfficeFlashback` |
| 14-16 | Flashback: Ben at the vending machine | `OfficeFlashback` |
| 17-19 | Flashback: Maya's radio check as you walk out | `OfficeFlashback` |
| 20-23 | Harlow back door: you hand over the KEEP COLD box (Story V4: Mrs. Okafor's stop is cut) | RP > `DeliveryRun.stops[0].handOverLines` |
| 24 | Looking at the delivered box | RPD `SetupPrologue` |
| 25-31, 32-42 | Depot: Maya and Mercer, then Derek's phone call | RP > `DeliveryRun.depotLines` |
| 43 | Fridge note | `ReadableSetup` |
| 44 / 63 | Beach photo / your mug | RP > `PrologueDirector.photoLine`, `mugLine` |
| 45 | Calling out in the flat | RP > `callOut` |
| 46 | Locked bathroom door, shower running | RP > `bathroomLine` |
| 47-49 | TV news | RP > `newsLines` |
| 50 | Thud from the bedroom | RP > `noiseLines` |
| 51-52 / 53-55 | The bedroom door / Mercer turns round | RP > `witnessLine`, `caughtLine` |
| 56 | The front door's chain jams | RP > `chainLine` |
| 57-62 | Mercer kills you in the living room (no roof any more) | RP > `deathLines` |
| 64-97 | Free rows (`[unused]`, empty text) | - |

## Void, red street (MainGameScene.unity)

| Rows | Event | Where |
|---|---|---|
| 98-100 | Void walk begins | `EndingSequence.DeathVision` |
| 101-102, 103-104, 105-106 | Whispers at about 13 s, 26 s, 40 s of the 60 s walk | `EndingSequence.VoidWhispers` |
| 107 | The King stops you (plate reads ???) | `DeathVision` |
| 108-121 | The King's questioning | RP > `EndingSequence.deathLines` |
| 122 | [screams] cue: the brand | no line; see Missing Assets |
| 123-125, 653-655 | After the brand | RP > `deathBrandLine` |
| 126-128 | Waking on the street (128: a Lost whispers) | RP > `SleepSequence.fallWakeLine` |
| 129-147 | Cutscene: 129 over the Lost Souls, 130-145 on Tomas, 146-147 over the depot's door (the objective comes up there) | RPD `OpeningMark` |
| 146 | Trying the locked van | RP > `CarInteract.noKeysLine` |

## Office

| Rows | Event | Where |
|---|---|---|
| 148-150 | Arriving inside the office | `DialogueRewrite.Exact` (the entrance teleporter's line) |
| 151, 153 | First dark moment: the light tutorial | `DialogueRewrite` > `PalmLight.hint` |
| 152 / 381 / 382 | Light held too long / burnt out / flash not ready | `PalmLight.warnLine`, `burnLine`, `notReadyLine` |
| 154-156, 370 | PA, stage 0-1 | `OfficeHorrorDirector.Announcements[0]` |
| 371 / 372, 373 / 373, 374 | PA, stages 2 / 3 / 4 | `Announcements[1..3]` |
| 157-159, 168, 177, 186 | First red-city talk with Priya / Ben / Nadia / Omar | `OfficeWitness.RedTalk` (greeting) |
| 160-167, 169-176, 178-185, 187-194 | Their three questions and answers | `OfficeWitness.Red` |
| 195-198 | Echo questioning opens | `OfficeWitness` > `GhostNPC.opening` |
| 199 | First question on screen | node 0 |
| 200-201 / 202-203 | Choice A / B, both lead to the second question | node 0 |
| 204-206 + own LOST pair | Choice C: Lost | node 0 |
| 207-208 + own CONFESSED pair | "Tell me exactly what you did.": Confessed, testimony to the journal | node 1 |
| 209-210 + own DAMNED pair | "Tell me why you had to.": Damned | node 1 |
| 211-212 + own LOST pair | "Nothing happened.": Lost | node 1 |
| 213-218 / 219-224 / 225-230 / 231-236 | Priya / Ben / Nadia / Omar outcome pairs | `OfficeWitness.Initialize` |
| 237 / 238 / 239 | Talking again to a Confessed / Damned / Lost colleague | `OfficeWitness.Resolved` |
| 240 | Timed out, or talking to a faded memory | `GhostNPC.Fade`, `GhostNPC.Talk` |
| 241-262 | Tomas in the east wing (two questions; karma +1 on "Do you remember your family?") | RP `ConfigureGhost` |
| 263 | Fuse box, empty | `DialogueRewrite.Exact` |
| 264 / 265 | Fuse in the cake / under Derek's keyboard | `DialogueRewrite.Exact` |
| 266 / 267 | Locker without the key / taking the key | `DialogueRewrite.Starts` |
| 268 | Third fuse in | `OfficeHorrorDirector.Advance(3)` |
| 269-271, 272 | First fuse: everyone at their desks, then gone | `DesksEvent` |
| 273-274 | A moment after the breaker | `AuditorIntro` |
| 275-276 / 277-278 | Before / after the note on your desk | RP `ConfigureNote` |
| 279-281 | After the kitchen calendar | RP `ConfigureNote` |
| 282-283 | Keypad used in the red city | `DialogueRewrite` > `Keypad.burntLine` |
| 284-285 | Code accepted | `OfficeHorrorDirector.Advance(4)` |
| 286-287 | First time in the Echo | RPD `SetEcho` |
| 288-296 | Vane in the Echo | RP `ConfigureGhost` |
| 297-301, 303-304, 314 | Vane's telephone replayed in the Echo (collects the 5 AM call) | RPD `PhoneReplay` |
| 302 | [disconnect] cue | narration stands in; see Missing Assets |
| 305-306 | After the Harlow manifest | RPD (the KEEP COLD container's note) |
| 307-308 / 309-311 / 312-313 | After the statement / ledger / rota | RP `ConfigureNote` |
| 315 | Taking the security pass | `DialogueRewrite.Starts` |
| 316 | Terminal without the pass | `DialogueRewrite` (`SequencePuzzle.noPowerLine`, card reader locked line) |
| 317 | Pass swiped | `DialogueRewrite.Starts` |
| 318-319 | Wrong camera order | `SequencePuzzle.wrongLine` |
| 321 / 322 / 323-324 | Camera 1 / 2 / 3 correct | `OfficeHorrorDirector.OnTapeStep` |
| 320, 325-335 | Order complete: the tape and what its microphone caught | `OnTapeSolved` |
| 336-337 | Tape over | RP > `StabCutscene.afterLine` |
| 338-339 | PA after the tape (Auditor starts hunting) | `Breakdown` |
| 340-343 | Vane's ambush | RP > `KesslerConfrontation.scareLine` |
| 344-352 | First question to Vane (karma -1 on "Back off, Vane.") | RP `confrontNodes[0]` |
| 353-359 | Second question; any answer turns him | RP `confrontNodes[1]` |
| 360-364 | He turns | RP > `turnLine` |
| 365 / 366 | Key dropped / still there when he finishes turning | RP > `keyLine`, `afterTurnLine` |
| 367 / 368 / 369 | Answering the 1st / 2nd / later ringing phone | `OfficeHorrorDirector.Answer` |
| 375 / 376-377 | Printer before / after the breaker | `PrinterRead` |
| 378 | Your chair, stage 4 | `DeskGhost` |
| 379 / 380 | The Eye starts patrolling / sees you | `OfficeEye` |
| 383 / 384 | Body bag falls / a bag moves | `OfficeLurkers.DropBag`, `BodyBagScare` |
| 385-386 | First shadow-man footsteps | `OfficeLurkers.Rush` |

## Tunnel, Silo 2, flood

| Rows | Event | Where |
|---|---|---|
| 387 | Arriving in the tunnel | `DialogueRewrite.Exact` |
| 388, 395 | Banging on the door | RP > `KesslerChase.knockLine` |
| 396 / 397 | Door bursts / chase starts | `screamLine`, `runLine` |
| 398, 399 | Each time he hits you (alternating) | `hitLines` |
| 389 | Tomas seen at the tunnel mouth | `tomasLine` |
| 390-393 / 394 | Tomas holds him on the footbridge / loses him | `tomasHoldLine`, `tomasLostLine` |
| 400 | Inside the silo door | `insideLine` |
| 401-402 | The banging stops | `quietLine` |
| 403 | Jump scare on the first bridge | RP > `KillerCutscenes.scareLine` |
| 404-408 | Getting up, chase begins | `getUpLine` |
| 409 / 410-411 | Lift switch from outside / lift opens | RP > `Elevator` |
| 413-415 | Ambush on the lower bridge | `ambushLine` |
| 412, 416 | The stairs end | RP > `SiloEncounter.leapLine` |
| 417-418 | Landing | `landLine` |
| 419-421 | Elias drops in | `derekDeathLine` |
| 422 / 423-425 | Maya throws the rifle / you pick it up | `throwLine`, `gunLine` |
| 426 | Magazine empty | `outLine` |
| 427-429 | Elias beaten, on his knees (choice prompt follows) | `defeatedLine` |
| 430-434 | KILL: after the first shot | `killTalkLine` (new field) |
| 435 | [gunshot] cue: the second shot | the rifle's own shot clip |
| 436-437 | KILL: he falls | `killLine` |
| 438-440 | KILL: keys taken | `confessionLine` |
| 441-446 | SPARE | `spareLine` |
| 447-451 / 452-453 / 454-455 | SPARE: Maya snaps / says it again / turns | `mayaSnapLine`, `mayaRealiseLine`, `mayaTurnedLine` |
| 456 / 457-459 | SPARE: Elias tackles her / throws the keys | `eliasSaveLine`, `eliasKeysLine` |
| 460-461 | Blood through the grating | `floodLine`, `twistFloodLine` |
| 462 | First time under | RP > `BloodFlood.firstLine` |
| 463-465 (464-465 with Sam only) | Flood start, survivors cling | `clingLine`, `clingLineSam` |
| 466-469, 471-472 (466-467, 470-472 Sam only) | Air below 30% | `lowLine`, `lowLineSam` |
| 473-476 / 483-487 | Maya / Sam taken with you at the top | RP > `Survivor.joinLine` |
| 477-478 / 479-481 / 482 | Maya goes under / her turning cutscene / after | `mayaDrownLine`, `TurningCutscene.damnedSpeech`, `damnedAfter` |
| 488-489 / 490, 492 / 491 | Sam goes under / his turning cutscene / after | `samDrownLine`, `lostSpeech`, `lostLine` |
| 493-495 | Out of the silo | `DialogueRewrite.Exact` |

## Escape, bridge, judgement

| Rows | Event | Where |
|---|---|---|
| 496-497 | Getting in the van | RP > `HordeChase.introLine` |
| 498-500 (499-500 without Sam) | First Damned grabs the van | `clingLine`, `clingLineAlone` (new field) |
| 501 | Van enters the road tunnel | RP > `DemonBoss.tunnelInLine` |
| 502-503 | Horde stops at the bridge | `HordeChase.stopLine` |
| 506-509 | Tomas walks out of the office (chase start) | `DemonBoss.burstLines` |
| 527 | He comes off the tunnel roof | `tunnelOutLine` |
| 504-505 | Bridge end | `arriveLines` |
| 510-517 | He lands in your way | `nowhereLine` |
| 518-522 | Fight begins | `riseLines` |
| 525 | He is stunned (35%) | `DemonBoss.Stun` |
| 526 | His first charge | `DemonBoss.Charge` |
| 528-529 | Van wrecked | `CarHealth.TakeDamage` |
| 530-531 / 523-524 | Phase 2 / phase 3 | `phase2Lines`, `phase3Lines` |
| 532-535 | Three hearts seal | `heartsLines` |
| 536-539 or 540-542 | Statement: presented / not held | RPD `AmalgamationAccounts` |
| 543-546 or 547-549 | CCTV: presented / not held | RPD |
| 550-553 or 554-557 | Harlow invoice: presented / not held | RPD |
| 558-563, 564-569, 570-576 | Vane's confession, Maya, the King and Tomas | RPD |
| 577-580 | It sinks | `DemonBoss.afterLines` |
| 581 | End of the bridge | RP > `EndingSequence.arriveLine` |
| 582-588 + 589 | The King's question (589 is the choice prompt) | RPD `PlayerConfession` |
| 590-594 / 595-598 / 599-602 / 603-607 | Your answer: statement / roof / label (only if inspected) / denial | RPD |
| 608 | Verdict: always | `EndingSequence.VerdictLines` |
| 609 / 610 | Elias killed / spared | `VerdictLines` |
| 611 / 612 / 613 | Maya turned / rescued / lost | `VerdictLines` |
| 614 / 615 | Sam rescued / lost | `VerdictLines` |
| 616 | Karma -2 or lower (unreachable today) | `VerdictLines` |
| 617-627 | Damned, having confessed | `VerdictLines` |
| 619-620, 623-627 | Damned by denial (see conflict 4 in the report) | `VerdictLines` |
| 628-638 | Bound | `VerdictLines` |
| 639-641 | Walking back to the city | RP > `EndingSequence.boundLine` |
| 642-650 | After the end card, on black: the telephone call (second voice is UNKNOWN until 648 names him) | RPD `PostCredits`, timings on `EndingSequence` |

## Lines spoken that are not in the CSV (kept on purpose)

| Line | Why |
|---|---|
| `*6:38 AM.*`, `*6:50 AM. Harlow Clinic, back door.*`, `*7:00 AM.*`, `*Somewhere in the living city.*` | time and place cards |
| `Flat 2B. Second floor.` | wayfinding over the "where's home" camera |
| `*[Q] raises your hands...*`, `*In the office: [V] opens the Echo. [J] is your evidence.*`, `*The Echo. Six seconds...*`, `*Evidence recorded. [J] to review.*` | the script has no key prompts; without these the mechanics are unexplained |
| `*The line goes dead.*` | stands in for the [disconnect] sound |
| `*Tomas walks out of the office, dragging Vane and Maya.*`, `*The three bodies pull together into one.*` | describes placeholder animation |
| `Go.`, `*Break the three hearts.*`, `One down!`, `...not yet.` (respawn) | instruction or feedback in a fight |
| `Agh--`, `He's on me.`, `Move.` (Elias chase hits), `The door shut behind me. No way back.` | hit feedback / tells you the route is one-way |
| Vane's two prompts while evidence is shown ("I did what the business needed.", "That clinic was just another supplier.") | the choice panel needs a line from him |
| Prop reactions with no row: fuse 3 in the locker, shutter, lift, ladder, keypad "Dead. No power." / "Wrong code.", `CAM: {0}. He was here.`, mag throws by Maya, journal summaries | small functional text |

Old lines removed because the script replaces or drops them: "Night run again.", "Night, you two.", "A phone is
ringing.", "Something is standing at the end of the corridor.", "Something red is glowing down there.", "The Echo
flickered.", "Footsteps. Heavy. Run.", "Something just landed on the roof.", "He went into the service tunnel.",
"The mark is burning again.", Tomas's "NO--! They're mine!", the first phone call's breathing.
