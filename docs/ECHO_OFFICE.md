# Echo office — 5 October 2026

Press V in the office to switch between the burnt red city and the clean memory. The Echo uses the existing clean wall, floor, ceiling and furniture materials. Leaving restores the exact material slots captured on scene startup.

The memory atmosphere blends over 0.85 seconds: blue lifted shadows, increased exposure, reduced contrast, bloom, cool vignette and Gaussian distance blur. Nearby objects remain sharper; screen-space dialogue is unaffected. A private runtime volume leaves the authored city volume assets unchanged.

Tune the material pairs, transition duration, exposure and tint in `Assets/Resources/EchoOfficeSettings.asset`. Rendering logic lives in `EchoOfficeMemory.cs`.

The obsolete named PushTrigger is disabled during SiloEncounter startup. The staircase leap and the separate floor fallback retain their existing wiring. This also applies to the currently unsaved scene without overwriting it.

Validation: Unity compiled successfully; 27 Play-mode regression assertions passed, including all four material groups, restoration of every original material slot, atmosphere blend-in/out, disabled shove trigger and active staircase leap. Actual office renders were inspected at `docs/media/echo/red-city.png` and `docs/media/echo/echo.png`.

This validates the switch and trigger states, not a complete traversal of every silo encounter.
