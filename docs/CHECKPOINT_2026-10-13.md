# OpenSpidey development checkpoint — 2026-10-13

Branch: `checkpoint/godot-rewrite-2026-10-13`.

This is an incomplete development checkpoint of the 64-bit Godot/C# rewrite.
The user requested a checkpoint commit and push, closed the playable preview,
and cleared the automatic rewrite goal. Development agents have stopped.
The full campaign and final modern-only repository conversion are unfinished.

## Build and verification

The asset-inclusive managed suite passes **1,664,005 assertions**. The Godot
game and media importer build with zero warnings and zero errors. The managed
suite includes imported assets and gameplay comparisons with captured original
executable results; its assertion count is not a campaign completion percentage.

Native L1A3 startup passes 92 story checks, 45 screen-flash checks and the real
32-spark actor/allocation/render/destruction proof. Native L1A4 startup passes
53 story checks and the original camera-shake actor proof. Both runs exit with
zero errors and no native resource leaks. These probes exercise authored scripts
and actual assets, including bounded construction of specific actors; they do
not establish complete ordinary gameplay traversal of the campaign.

The complete native component regression also exits cleanly, covering the new
31 message-box checks, real C004/C005 music/fades/voice ownership, mission actor
death and source fire189's laser/explosion effects alongside the existing player,
combat, web, animation, environment, camera, audio and resource-lifetime checks.
The separate teardown run passes six scripted-death checks, 48 scene-destruction
checks and 27 start-orientation checks, then exits without errors or leaks.

## Included work

- Engine-independent archive, texture, animation, audio and story parsers;
  Godot scene, rendering, physics, input and playback adapters.
- Player traversal, ceiling zip, melee, web actions and effects, original HUD,
  fog/backdrops, scripted cameras, speech and movie playback.
- Original actor construction, mission scheduling, checkpoints, shared source
  lists, scene destruction and early mission transition support.
- Latest source-backed mission death commands, timed message boxes, actor
  camera shake, sparks, laser flashes, explosion effects and music scheduling.
- Scorpion countdown and progress rules with original-executable comparisons.
  Its native module code builds, but production registration and story/HUD
  integration remain disabled pending native verification.

## Resume work

1. Use the latest native results below to resolve any outstanding integration
   failure before extending the story checks.
2. Complete L1A4 → L2A1 progression through original end197 (`181 L2A1_T`,
   then `162`). L2A1 selects game level6 before the player exists, loads its
   world through restart128 after player construction, and needs the genuine
   Scorpion module/player hook/timer HUD composition. Do not preload the map
   or replace the authored startup program.
3. Continue complete campaign progression, remaining actor families, bosses,
   frontend/menu flow and cross-platform exports. Preserve gameplay/cutscene
   widescreen and the 4:3 menu layout.
4. Validate complete manual playthroughs, including the original overhang zip,
   end-of-first-level bomb pickup and visibly animated vault opening reports.
5. Remove recovered C++ and reference-dependent tooling only after the full
   modern game and its fidelity checks are complete.

## Reproduce

From the repository root, supply your own original game installation:

```text
python tools/modern.py bootstrap
python tools/modern.py build
python tools/modern.py test --assets /path/to/original/game
python tools/modern.py play --assets /path/to/original/game
python tools/modern.py story-smoke --assets /path/to/original/game --rendered
python tools/modern.py story-smoke --assets /path/to/original/game --scene data/L1A3_G.psx --rendered
python tools/modern.py story-smoke --assets /path/to/original/game --scene data/L1A4_G.psx --rendered
```

Original assets, converted media, toolchains and generated build output stay
outside the commit. The recovered C++ in `src/` and reference tooling remain
available for the unfinished port. See [the migration guide](GODOT_MIGRATION.md)
for architecture, detailed coverage and the original final delivery requirements.
