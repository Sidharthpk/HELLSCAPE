# HELLSCAPE: testing checklist

Automated first (Unity must not be in Play when you start them):

- [x] **HellScape > Tests > Dialogue CSV**: PASS expected, 650 rows. Run after any CSV or row-number edit.
- [x] **HellScape > Tests > Story regression (Play mode)**: result in `Library/StoryRegression.result`.
- [x] `AmalgamationRegression.Run()`: result in `Library/AmalgamationRegression.result`.

Ticked = passed on 8 October 2026. Everything below is a manual pass and has not been played yet. Row numbers are
HS ids; the trigger map says which script speaks them. Jump points: *HellScape > Play > Car Chase*, *Kessler Chase
(tunnel)*, *Elias Fight*; *HellScape > Debug > Play Bound Ending*.

## Prologue

- [ ] Flashback: Omar (1-4), Nadia (5-10), Priya (11-13), Ben (14-16), Maya on the radio (17-19); camera waits for each.
- [ ] Van: 20, then the radio (21-27), cut-out (28-31). No overlap with the flashback.
- [ ] Mrs. Okafor (32-42).
- [ ] Clinic, **inspect the label first** (54-57), then collect (43-53). 58-59 must NOT play.
- [ ] Clinic, **collect without inspecting**: 43-53 then 58-59.
- [ ] Depot (60-65), "7:00 AM", walk home.
- [ ] Flat: 67, 68, 69; TV (70-73), 74; body (75-77); letter 78, new letter text, 79-81.
- [ ] Elias: with and without turning round. He stops, 82-86 play, then the stab (87), 88-89, control returns.
- [ ] Stairs 90, 91; roof 92-96; grab 97; fall. Getting caught on the stairs still puts you back.

## Void and street

- [ ] 98-100 at the start; whispers three times, far apart; 107; the King (108-121) with plate "???"; brand; 123-125.
- [ ] Street: 126-128, then Tomas (129-147) and the key hint. You can walk while he talks.
- [ ] Van without keys: 146.

## Office

- [ ] Entry (148-150), light tutorial (151, 153 + key hint), PA (154-156).
- [ ] Each colleague, red city: first talk greets, then three questions; later talks go straight to the questions.
- [ ] Each colleague, Echo: opening (195-198) then the first question; the Echo clock holds while lines play.
  - [ ] Confess path (A or B, then "Tell me exactly what you did."): own confessed pair, journal gains testimony, 237 after.
  - [ ] Excuse path ("Tell me why you had to."): own damned pair, red flash, 238 after.
  - [ ] Denial at the first question, and at the second: own lost pair, 239 after.
  - [ ] Timeout: 240, and 240 again when spoken to.
- [ ] Tomas: three first answers (karma +1 only on family), three second answers, 262 after.
- [ ] Fuses: 263, 264, 265, 266 (locker without key), 267 (key); first fuse scare (269-272); third fuse (268, then 273-274).
- [ ] Desk note (275-276, note text, 277-278). Calendar (279-281).
- [ ] Keypad in the red city (282-283); in the Echo with power: 1304, then 284-285.
- [ ] First Echo entry (286-287). Vane (288-296). Telephone (297-301, 303-304, 314): journal gains the 5 AM call.
- [ ] Manifest (305-306), statement (307-308), ledger (309-311), rota (312-313): journal gains each.
- [ ] Pass (315); terminal without it (316); swipe (317); wrong order (318-319); right order (321, 322, 323-324);
      tape with 320 and 325-335 over it; 336-337; PA (338-339); Auditor hunts.
- [ ] Vane: ambush (340-343), both questions, every answer once across runs (karma -1 only on "Back off, Vane.");
      turn (360-364); key (365); stay too long (366).
- [ ] Ambient: three phone answers (367, 368, 369); printer before and after the breaker (375 / 376-377); chair (378);
      Eye (379, 380); body bag (383), bag twitch (384); first footsteps (385-386).
- [ ] Die in the office: respawn at the office checkpoint, evidence and colleague fates kept, no cutscene replays.

## Tunnel and Silo 2

- [ ] Tunnel: 387; door (388, 395); burst (396, 397); hits (398 / 399); Tomas (389, 390-393, 394); 400; 401-402.
- [ ] Silo chase: 403, 404-408, lift (409, 410-411), ambush (413-415), leap (412, 416), landing (417-418).
- [ ] Fight: 419-421, 422, 423-425, empty magazine (426), beaten (427-429).
- [ ] **KILL** (left mouse): 430-434, second shot, he falls, 436-437, keys, 438-440.
      As built Maya then turns (her lines 449, 451-453). With `mayaTurnsOnKill` off she must come into the flood.
- [ ] **SPARE** (Q): 441-446, Maya 447-455 and her turn, Elias 456, keys 457-459, flood.
- [ ] Die mid-fight: fight restarts. Die after the choice: the choice is not asked again.

## Flood

- [ ] 460-461, 462, cling lines (463-465, or 464-465 with Sam alone), low air.
- [ ] Sam rescued: 483-487 at the top. Sam lost: 488-489, cutscene (490, 492), 491.
- [ ] Maya rescued / lost (473-476 / 477-482): only reachable with `mayaTurnsOnKill` off.
- [ ] Out: 493-495. Drown: back to the silo floor, survivors reset.

## Escape and bridge

- [ ] Van: 496-497; first grab with Sam aboard (498-500) and without (499-500); road tunnel (501); bridge (502-503).
- [ ] Tomas: office (506-509), tunnel roof (527), bridge end (504-505), landing (510-517), fight start (518-522).
- [ ] Fight: stun (525), first charge (526), phase 2 (530-531), phase 3 (523-524), hearts (532-535), van wrecked (528-529).
- [ ] Evidence, holding all three and presenting: 536-539, 543-546, 550-553.
- [ ] Evidence, holding none (or choosing "Ask about what I witnessed"): 540-542, 547-549, 554-557.
- [ ] 558-576, it sinks (577-580). Die in the fight: bridge checkpoint, back in a repaired van.

## Judgement

- [ ] 581, 582-588, the choice. The label answer is offered only if the label was inspected in the Prologue.
- [ ] Each honest answer plays its own speech (590-594, 595-598, 599-602).
- [ ] **Bound**: spare Elias, save Sam, honest answer. Verdict 608, 610, Maya's row, 614, then 628-638; walk away (639-641).
- [ ] **Damned by score**: kill Elias, lose Sam, honest answer. 608, 609, 615, then 617-627.
- [ ] **Damned by denial**: any run, "None of this was my fault." 603-607, then 619-620 and 623-627 (no "you named it" rows).
- [ ] End card, black screen, telephone call (642-650; plate reads UNKNOWN until Kessler says "Derek"; Enter skips only after that), TO BE CONTINUED, Enter returns to the title as a new game. In Play: **HellScape > Tests > Post-Credits Call**.

## Across the game

- [ ] No two speeches overlap or cut each other where they should not (watch the longer ones during chases).
- [ ] Pause during any conversation and any timed question: clocks hold.
- [ ] No missing-glyph boxes in any line (the CSV check covers the characters; this is the eye check).
- [ ] Console free of errors after a full run.
