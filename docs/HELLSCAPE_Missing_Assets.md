# HELLSCAPE: missing assets

Nothing below blocks play. Each item has a stand-in that is in the build now.

## Audio

| Needed | For | Stand-in today |
|---|---|---|
| Voice recordings, all 644 spoken rows | whole game | text + mumble blip (`General/Dialogue blip`) |
| Radio static burst | HS_0066 `[static]`, depot | none: the row is silent |
| Radio voice treatment | HS_0030, HS_0064 (the radio answering by itself) | plain subtitle, speaker plate "Radio" |
| Driver's scream | HS_0122 `[screams]`, the branding | the existing "Ending/Demon rises" sound and red flare; no scream |
| Phone disconnect click / dead tone | HS_0302 `[disconnect]` | narration line "The line goes dead." |
| Unexplained tap from the cargo bay | script 0.2 staging | none |
| Printer running with no power | script 3.1 staging | procedural rumble used by the false scares |
| CCTV tape audio (Tomas, Derek, Vane) | HS_0325-0335 | subtitles over the tape; the picture's own sound is the old stabbing clip |
| Telephone ring, receiver click, line going dead; 9 voice recordings | post-credits call, script 8.5 (Sound Manager > Ending > Phone ..., Post-credits voice 642-650) | built-in ring and click; the usual mumble |
| PA voice | all PA rows | procedural crackle, then subtitle |
| Sound Library slots still empty | "Demon Fight" music | none |

## Animation

| Needed | For | Stand-in today |
|---|---|---|
| Elias speaking / waiting before the stab | HS_0082-0086 | he holds his approach pose while the lines run |
| Elias kneeling through a second exchange | HS_0430-0434 (kill route) | he stays in the kneel he is already in |
| Sitting at a desk | colleagues, flashback | standing, bent over the desk |
| Lip movement for anyone | all conversations | none (PS1 style; may be left) |
| Tomas / Vane / Maya joined body | script 7.2 | Tillagemon boss model with `TomasAmalgamation` sinews |

## Visual

| Needed | For | Stand-in today |
|---|---|---|
| "Visual dislocation" when Maya appears in the Amalgamation after being rescued | script 7.2 and Appendix B | none. Tomas's HS_0506 ("I went back for the ones who knew") is the only signal. Needs a decision on the look. |
| Second label under the KEEP COLD label (T. ROURKE scraped off) | HS_0055-0056 | the box label reads KEEP COLD / VANE / V-0500; the name is only spoken |
| Mrs. Okafor's door on a chain | script 0.3 staging | she stands at the open door |
| People-shaped absences in windows | script, red street staging | none |
| Vane's car, depot phone, cleaner's bucket | script 0.5 | grey primitives built at runtime (`RevisedPlotDirector.SetupPrologue`) |
| KEEP COLD container, Vane's telephone, office phones, printer, desk photo | office | primitives built at runtime |
| Curly quotes and ellipsis glyphs in the pixel font | CSV text | `HS.Clean` turns them into straight quotes and three dots at load; the CSV keeps the originals |

## Not an asset, but not built

- A save file (see the audit).
- A way to reach karma +2 / -2 (both documents say to leave it).
