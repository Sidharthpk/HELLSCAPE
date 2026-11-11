# City surface cleanup — 20 October 2026

Both saved game scenes use dry variants of the seven demo asphalt/pavement materials in `Assets/Generated/CitySurfaces`. Source demo materials and lane-marking textures are preserved. The puddle mask was assigned to both specular and occlusion slots; those slots are now empty. Roads use a nonmetallic finish with 0.12 smoothness and no wet normal map. Pavement normal strength is reduced.

Seventy renderers per scene received the dry materials. An exact mesh, world transform and material comparison found two duplicate renderers in Prologue; those duplicates were disabled. Collision and gameplay components were preserved. This checks exact duplicates, not every possible partial mesh intersection.

Prologue uses a persistent, valid `PrologueBalanced` volume profile with Neutral tonemapping, restrained bloom and slightly reduced exposure. Sun intensity is 1 instead of 2.2; sky exposure is 0.75 instead of 1.2. Ambient fill retains shadow detail without the previous washed-out warm fog. Scene captures in `docs/media/city-cleanup` use scene lighting, with no temporary fill lights.

Backups before these edits: `Backups/CitySurfaces-20261020`.

Validation: both scenes reported zero missing scripts and seven dry materials with zero wet specular/occlusion maps. Final street captures were inspected. A full driving/story playthrough was not performed.
