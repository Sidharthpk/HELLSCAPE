# Tomas finale revision — 6 October 2026

This revision follows the user's new finale direction and supersedes the earlier Vane-only bridge boss and Tomas escape. Existing character names in scene paths and component types remain stable for UnityEvent references.

## Story

Maya exposed Tomas to Vane; Vane ordered his murder. Tomas first uses Elias to punish the people involved, then uses the marked driver to reach Vane. Helping the player through the tunnel also keeps Tomas close to his targets. His grievance is real; his later choices remain his responsibility.

After the silo and Elias confrontation, the office-exit reveal shows Tomas collecting Vane and Maya's ruined Damned forms. He binds those bodies to himself rather than letting either receive a hearing. Two bodies slide inward, stretch, rise and join him through exposed tissue before the existing three-headed monster unfolds. Tomas is the controlling voice; Vane and Maya remain separate voices inside it.

He wants the player's mark to reach Kessler in the living world. The van chase, tunnel, bridge arena, attack phases and heart weakness remain the existing gameplay. The boss is named **TOMAS — THE AMALGAMATION**.

Defeating the creature breaks Tomas's control. Vane gives his account; Maya acknowledges making the call; Tomas still claims ownership of their punishment. The King separates their accounts. The empty combined body sinks through the existing death sequence. Tomas does not then flee into the city. The player's judgement and the Kessler sequel lead remain.

## Branch consistency

The fixed three-person fusion requires Maya to turn on both Elias-choice routes. The mercy route retains its existing transformation. Ending Elias now also leads to Maya's refusal and transformation before the flood; it does not make a dead Elias stand up for the mercy cutscene. Sam remains eligible for rescue.

Defeating a Damned body does not itself erase its soul. Tomas reuses damaged forms, not resurrected living people. The finale dialogue distinguishes breaking the creature from absolution or judgement.

## Implementation and verification

`TomasAmalgamation` snapshots only renderable meshes, preserving the visual sources even when combat deletes the original corpse. Copies contain no AI, colliders or story callbacks. The authored final monster and its animations remain in use.

`HellScape > Tests > Tomas amalgamation (Play mode)` runs against disposable scene state. It checks the kill-Elias branch, Sam's rescue eligibility, three source bodies, the actual fusion/chase handoff, control restoration and final accounts. Results are written to `Library/AmalgamationRegression.result`; reveal captures go to `docs/media/finale/`.

The current fusion uses procedural movement, deformation and blood effects with the supplied models. Bespoke facial morphs and newly recorded three-voice audio are not part of these assets.
