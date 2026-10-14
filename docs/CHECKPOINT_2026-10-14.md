# OpenSpidey checkpoint — 2026-10-14 (afternoon)

Branch intended: `checkpoint/godot-rewrite-2026-10-14`

Parent before this commit: `1c2d8a7a` (Checkpoint 64-bit Godot rewrite).

This checkpoint saves the parallel-merge progress on the 64-bit Godot/C#
rewrite. The full campaign is unfinished. Recovered C++ remains behavior
reference. Original game archives, executable, media and local toolchains are
not in the repo (`~/.local/share/openspidey/game`).

## Verification (2026-10-14 ~16:09 +02)

| Gate | Result |
|------|--------|
| `python3 tools/modern.py build` | **0 Warning(s), 0 Error(s)** |
| `python3 tools/modern.py test --assets ~/.local/share/openspidey/game` | **Passed 1,771,668 assertions** |
| Story-smoke exit 0 | **l1a1, l1a2, l1a3, l1a4, l2a1, l2a2, l3a1, l4a1, l7a1, l8a1** |

Rendered L1A4 transition and play-smoke were verified earlier in the day on this
tree; re-run if you distrust the afternoon state.

## What landed since morning checkpoint

- **Camera:** Core `CameraAi` / placement / boss modes; `SourceHideItems`; Scorpion
  boss-camera adapter; item translucency / costume `0x800`.
- **Missions:** every campaign opcode has a handler (incl. chopper 203–212,
  Venom waits, SendKillLoudly 12). `story-smoke` accepts any `data/lXaY_g.psx`.
- **Actors / bosses:** idle NPCs, Torch, LizMan, Simby (+ vertex wobble + AI
  shell), Venom (walkabout/wait), Rhino (ChargePlayer / PlayXAPlease /
  GonnaHitWall), ElectroLines 409, Cop/Chopper **modules + Godot actors**,
  production **CScorpion AI** (L2A2 120 ticks).
- **Frontend Core:** save / high scores / cheats / menu / `CExpandingBox`. No
  full `Shell_DoShell` scene yet; `DrawMenuBox` still throws until nine-slice.

## Known walls (next session)

| Level / area | Blocker |
|--------------|---------|
| L5A1 | Actor script instruction word @ offset `0x796` (node 4) |
| L5A3 | Control actor effect `0x429B` |
| Cop unit tests | `CopTests` AI case 267 (`RunAppropriateAnim`); fixture in tree, **not** registered in `Program.cs` |
| Frontend | Shell scene, `PShell_DrawMenuBox`, interactive card UI |
| Later campaign | mysterio, turret, carnage, docock, level-script modules `l5a5lsc`…, combat leaves for Simby/Rhino/Venom |

Parallel worktrees under `~/Documents/spidey-work/par/` may hold unfinished agent
work (e.g. Cop AI fix). Mainline gates above are what this checkpoint claims.

## Resume

Start from `docs/RESUME_2026-10-14.md` and `docs/GODOT_MIGRATION.md`. Do not apply
MSVC6 byte-matching rules to managed code.
