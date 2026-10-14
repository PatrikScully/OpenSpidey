# OpenSpidey checkpoint — 2026-10-14

Branch: `checkpoint/godot-rewrite-2026-10-14`

Parent: `1c2d8a7a62f74d3c88e0983b4e58e9fa8e902eb2`

This checkpoint saves the verified64-bit Godot/C# rewrite snapshot. The full
campaign rewrite is unfinished. Recovered C++ remains behavior reference;
original game archives, executable, media and local toolchains are excluded.

## Verification

- Asset-inclusive managed checks: **1,701,432 assertions**, process exit0.
- Godot game build: **zero warnings and errors**, process exit0.
- Native component suite and actual first-mission startup: process exit0,
  including both legal authored intro skips,31 live effect checks and teardown.
- Native Scorpion/Jonah constructor, primary tail, footprint and health panel
  proofs use genuine model/texture banks, reparenting, bank generations and
  resource destruction. These private proofs do not register production bosses.
- All first five mission startups and the fourth-to-fifth transition passed
  bounded native checks during this resume. Full traversal and completion of
  those missions have not been established.

The verification source was frozen independently of ongoing edits. The committed
modern source matches that frozen source byte for byte; build caches are omitted.
Counts from native frame loops vary with elapsed time and are not completeness
percentages.

## Saved changes

Story startup now selects actual autoexec/restart commands, keeps EndLevelNode
separate from GameLevel, and binds the player/environment after the authored
world load. Linked visibility, cinematic screen ownership, actor speech,
synthesized player input and the player-owned Scorpion approach module are
connected to their actual shared owners. HUD groups retain their source spacing
in widescreen gameplay.

Scorpion/Jonah construction, damage, outer AI, Jonah movement/contact and primary
tail geometry have named Core state and services with original executable
fixtures. The source resource table shares40 loaded/generated slots and preserves
generation ownership across failures and reloads. Boss health state, original
texture-name parsing and native panel rendering are included.

## Continue from here

The next encounter work needs source camera mode16 and focus timing, the remaining
Scorpion behavior leaves, genuine native Scorpion/Jonah actor factories, world
queries, procedure/message/Hit dispatch and production HUD ownership. Follow
original module189 and model-bank126 ordering before actor adoption. Remaining
campaign actors, frontend/save flow, full playthroughs and manual visual checks
are also required before the final modern-only repository cleanup.

Unverified work is retained in the local development workspace and excluded from
this buildable checkpoint: camera mode16 foundations, Scorpion target/intro/
environment selection, Jonah's explicit cached-transform shadow adapter update,
and additional palette/speedup story-command handlers. It must pass managed,
build and native gates before integration. No complete-game or final-rewrite
claim is made by this checkpoint.
