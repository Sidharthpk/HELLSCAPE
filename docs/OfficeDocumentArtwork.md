# Main office hall documents

Generated on 15 October 2026 with the built-in image generation tool. The images are final game textures, not mockups. The original generated outputs remain in the Codex image library; project copies live below.

| Asset | Project path | Use |
| --- | --- | --- |
| April calendar | `Assets/Resources/OfficeDocuments/AprilCalendar.png` | Calendar board and document reader |
| Witness statement | `Assets/Resources/OfficeDocuments/WitnessStatement.png` | Desk document and document reader |
| Elias's note | `Assets/Resources/OfficeDocuments/DeskNote.png` | Desk note and document reader |

`HellScape > Office > Place Readable Documents In Main Hall` rebuilds the placement without replacing the story interactables. The desk is at world `(1018, 3.4, 904)`, on `Floor_main-hall`. The office fit tool also reapplies this layout. The original object names remain `Calendar`, `IncidentReport`, and `YourDesk_Note`.

The reader uses the same image as the world prop. Scroll to zoom, drag to move the enlarged document, T to switch to large text, and F to close it. The plain-text versions preserve the puzzle clues and are updated in `ReadableSetup` and `RevisedPlot`.

## Final generation prompts

### April calendar

Final revision requested by the user: replace the large callouts with an ordinary office calendar. Only the 13th is circled in thin red pen, with a small blue handwritten "Tomas accident" inside that date's cell. The dates and headings stay clear. There is no enlarged accident headline, repeated date, or day/month instruction. Both the world texture and the reading view use this revised image. The original generation brief below is superseded by this edit.

Final edit prompt:

Edit this game calendar texture into an ordinary understated office wall calendar, keeping the ivory paper, portrait 2:3 format, KESSLER & VANE company heading, APRIL month heading, and correct Monday-first 30-day grid (day 1 Wednesday, day 13 Monday). Completely REMOVE the entire large bottom callout area: no large '13 APRIL', no 'TOMAS - LAST SHIFT', no 'ACCIDENT' headline, no 'DAY FIRST. MONTH SECOND.' footer and no puzzle instructions anywhere. Expand the regular calendar grid down the page so it uses most of the sheet like a normal office calendar, with generous empty space for handwritten appointments inside every date box. Print date numbers smaller in the upper-left of each cell rather than giant centered numbers. Keep 13 modestly circled with a thin red pen line ONLY around its number. In small natural dark-blue ballpoint handwriting, immediately beside or just below 13 WITHIN THE SAME date cell, write exactly 'Tomas accident' on two short lines. This handwritten note must be significantly smaller than the printed day number, a discreet personal appointment annotation readable only when examined, not a headline. Keep all other date cells plain and empty apart from their numbers. Use only these dates: first row blank blank 1 2 3 4 5; second 6 7 8 9 10 11 12; third 13 14 15 16 17 18 19; fourth 20 21 22 23 24 25 26; fifth 27 28 29 30 blank blank blank. No other red writing, no arrows or blood or dramatic emphasis. Ordinary office stationery, crisp correct typesetting and readable small annotation. Flat straight-on printable texture with no room, hands, perspective, or watermark.

Initial generation brief (superseded):

Create a finished, extremely readable portrait 2:3 game texture: a flat front-facing April wall calendar on slightly aged ivory paper, full bleed paper no desk or room, no perspective, no shadows across text. Corporate 1990s Kessler & Vane office, restrained black print and deep red annotations. Large sharp typography. Header exact: 'KESSLER & VANE' then 'APRIL'. Monday-first grid with labels MON TUE WED THU FRI SAT SUN. Thirty dates arranged EXACTLY: row1 blank blank 1 2 3 4 5; row2 6 7 8 9 10 11 12; row3 13 14 15 16 17 18 19; row4 20 21 22 23 24 25 26; row5 27 28 29 30 blank blank blank. The 13 is large and circled in red but entirely legible. Under the grid, generous bold separate notes exact '13 APRIL' then 'TOMAS - LAST SHIFT' with a thin red strike through only LAST SHIFT. Large red 'ACCIDENT' beneath. Footer exact 'DAY FIRST. MONTH SECOND.' All spelling and numbers correct. Light edge wear only, central paper clean. No tiny filler text, no decoration over text, no watermark. This is a final calendar image used both as world texture and full-screen reading image.

### Witness statement

Create a finished highly readable 2:3 portrait game document texture. Full bleed flat front-facing ivory office paper, no perspective no scene no hands no binder. Realistic lightly aged 1990s official form. Clean bold black typewriter/sans type at large comfortable reading size, restrained dark red stamp. Exact text, use line breaks and generous spacing: 'KESSLER & VANE LOGISTICS' / 'SHIFT WITNESS STATEMENT' / '13 APRIL - SILO 2' / 'T. ROURKE' / 'OFFICIAL ACCOUNT' / 'Accidental fall while unsupervised.' / 'Signing confirms that I witnessed this account.' / 'This is not an attendance sheet.' / 'WITNESS SIGNATURES' / 'MAYA' / 'SAM' / 'DEREK' / 'YOUR SIGNATURE: __________' / 'Filed by Vane.' Add short cursive signature strokes beside the three names and a dark handwritten signature stroke on the final underline, without obscuring the printed labels. Small stamp 'FILED'. No additional invented text. Keep all text inside wide margins with no overlaps, stains only edges. Final texture must be immediately legible.

### Desk note

Create a final square readable horror game note image, perfectly front-facing flat full-bleed pale yellow square paper, clean central writing area, faint paper fibers and lightly creased corners. No scene no perspective no shadows no hands. Minimal dried dark red smudge on bottom right edge occupying less than 5 percent, never on text. Very large crisp dark handwritten BLOCK CAPITALS, readable restrained lettering, exact text in generously separated centered lines: 'YOUR KEYS.' / 'SILO 2.' / 'BRING THE STATEMENT.' / 'READ THE LAST NAME.' / '- ELIAS'. First two lines especially bold. No other text or ornament. All text sharp and high contrast, with enough margin to read on an in-game desk and on a full-screen reading view.

## Verification

- Checked the generated lettering and all calendar numbers visually.
- Rendered the authored desk in the office and checked floor support and an unobstructed approach.
- Tested all three artwork assignments in an isolated play-mode scene.
- Checked that the plain-text views do not overflow their text areas.
- Checked pause/resume behavior and that runtime story setup retains the revised note.
- Restored the main scene and removed the temporary test scene.
