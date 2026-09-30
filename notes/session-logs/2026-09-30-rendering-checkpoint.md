# Rendering and level checkpoint, 30 September 2026

The user asked me to finish the current work, save a checkpoint and stop before they switch off the PC. The full decompile and engine rewrite remain unfinished. Source checkpoint: `c5aa96e6`.

## Completed

- Fixed the captured stretched thug limbs. `M3dInit_ParsePSX` now resets the stitch index for each loaded model region, as the original does at `0x453654`. A stale index made lower-body limbs read empty transformed vertices. The moving-thug regression went from 43,086 zero-depth triangles to zero. `DCModel_RenderModel` also uses the original lighting buffer, stitched color indices and ambient channel order.
- Moved script-only actors into the original control list. They have no combat model and previously entered enemy collision tests. Constructor, destructor, real mouse punching and a level restart were checked. No type 203 actor remains in the enemy lists sampled by the level audit.
- Fixed scene cleanup's trigger link width and camera null handling. Fixed command `0x42B7` to set the original skippable-scene byte, and fixed commands that used a flags word as a trigger node. Real Escape now clears the Black Cat letterbox in the same frame, in Normal and Kid Mode.
- Added standalone playback of original Bink movies with sound, timed frames, volume, Escape skip and decoder cleanup. It uses the host `ffmpeg` executable. All 25 shipped movies decoded to their complete header frame counts; Treyarch also passed 173 frame pixel CRC comparisons. Twelve playback replacement cycles left no decoder children.
- Fixed `Spool_MaskFaceFlags` to read the count before the model table, rather than before the first model. Fixed the reversed region-pool assertion and the original access-release condition in `ClearRegion`. I did not convert the PSXRegion global family.
- Restored the hostage type virtual and police/SWAT model setup virtual. Fixed the helicopter physics loop to count down instead of looping almost four billion times. Its final instruction comparison has zero mnemonic differences.
- Fixed Rhino's shadow arguments and corners. A hardware watchpoint then proved that `CSimpleTexturedRibbon::Display` wrote coordinates over two scratch pointer globals. Its three reads now load the pointers. Rhino passed 1,808 ribbon calls and 452 quad calls with no pointer writes or crash.
- Restored the original `softspot` and `softeyes` filenames in `CSoftSpot`. The guessed `softspot_glow` name left six Mysterio objects at region 255 and crashed rendering. The corrected level now loads and renders.
- Fixed thug backpedal recoil to update both X and Z. The old code failed the corrected native fixture; the new code passed 100,000 comparisons of actor state and ordered calls.
- Added default 4x MSAA in the SDL backend, with `SPIDEY_MSAA=0` to disable it and a tested fallback if the requested window/context is unsupported. Native GL checks report four samples and no errors. The edge fixture has 469 partially covered pixels versus zero without MSAA.
- Restored eight police AI dependencies: BackpedalPlease, TakeHit, GetWhippedLikeTheWhoreYouAre, SetAnimMode, TooCloseToSpidey, DetermineFightState, SetAttackFlags and ProcessMessages. Each passed original-code fixtures. ProcessMessages passed 100,000 linked-message cases covering pose joints, allocation success/failure, shared globals and call order. Linux fixture SEH adaptation does not test Windows exception unwinding.

The standalone full-map rendering mode remains enabled by default. All environment items in the final level records have usable model regions.

## Verification and scope

- Final MSVC DLL, `cmake --build out -j8`, layout validator, SDL build and `tools/dunno.py` pass. The CD-check define is commented in committed source.
- All 34 story areas passed startup and ten continuous seconds of idle gameplay. Eight early-unlock scenes were rerun after correcting the timer to restart on relock/pause. All 23 training challenge variants and 11 extra maps passed, including both alternate story maps and all nine target stages.
- The final combined coverage has 14,025 gameplay Display calls and 687.1 seconds of idle gameplay. The retained story and extra-map samples include 1,080,321 actual polygon submissions after clipping, with zero detected projection anomalies. All environment regions were usable. These are smoke tests, not completed missions, complete spawn checks or boss victories.
- Raw triangles can have extreme coordinates while crossing the camera plane. Two training maps were checked after clipping: 95,728 submissions and 287,292 vertices had no anomalies. Do not treat every large pre-clip coordinate as a visible defect.
- Normal and Kid Mode Black Cat tests consumed real Escape and finished cleanup within 2 ms of input consumption, with no pause or remaining decoder children. FMV Escape was also checked when the original wrapper's skip argument was false.
- Windows `boottest.sh` reports PASS through the level and gameplay inputs. Its log also contains a late Wine stack-overflow line, so it is not evidence of long-term DLL gameplay stability. Preserve that follow-up; do not describe the Windows build as fully crash-free.

Final cinematic regression on the combined checkpoint build passed with MSAA4. All five startup/intro movies completed (319, 221, 173, 320 and 2,773 frames). L1M1 played for 184.911 seconds against 184.867 seconds in its header. The Black Cat scene finished naturally, followed by 10.134 seconds of stable gameplay. No movie or scene was skipped in this final run; the separate Normal/Kid Mode tests verify Escape. F12 exited with code zero and no decoder children.

Touched original functions did not gain instruction differences. Final checks include zero mnemonic differences for SetCopType, SetAnimMode, TooCloseToSpidey, CSoftSpot and DoChopperPhysics. ParsePSX improved 327 to 318; MaskFaceFlags improved 36 to 13; thug BackpedalPlease improved 76 to 45. New police methods stay `@NotOk` where matching is unfinished. No new police DLL hooks were added.

## Resume here

1. Restore `CCop::DoAISwitchLogic` at `0x42F810`, then missing movement, combat, web, fall/death and laser helpers. `CCop` still has no AI or Hit override, so the new leaves do not yet restore live police behavior. `Hit` at `0x429C60` first needs `SlideFromHit` at `0x4297D0`.
2. Finish matching the remaining new police methods. Check fixture inputs independently: earlier path/global/substate correlations were corrected before the final results. Avoid calling original MSVC vector-return functions through GCC struct-return casts.
3. Extend the smoke audit into mission progression, later enemy spawns, every boss encounter, saves/restarts and combat/web actions. Other idle NPCs observed during short runs can be waiting for triggers; inspect their real AI before diagnosing them.
4. Investigate the late Windows Wine stack overflow with a parent-launched SEH/stack-walk harness. Keep the one-global-at-a-time rule.

## Artifacts

- Latest standalone binary: `~/Documents/spidey-work/sa-run/new/spider_checkpoint_20260930`, SHA256 `10f90db1dd52949fdd6cba2c0f1ec61298a23beb103ca9e283b565006e91e97d`. Run with that directory in `LD_LIBRARY_PATH` and the Wine game directory as its argument. `ffmpeg` is required for movies.
- [Level coverage](../../out/level-audit-20260930/coverage.md), [CSV](../../out/level-audit-20260930/coverage.csv), [JSON](../../out/level-audit-20260930/coverage.json). The JSON records the exact binary for each run. Training uses audit3, alternate maps use audit4, and eight corrected story cases use the final checkpoint binary.
- `out/current-audit/`: final builds, tags, instruction comparisons, `control-test-audit4/` and `windows-boot/`.
- `~/Documents/spidey-work/wt/cop-behavior/out/cop-audit/checkpoint.txt`: remaining AI dependency addresses and native fixtures. `wt/cop-dispatch/out/cop-dispatch/`: exact ProcessMessages source fixture and comparisons. `wt/thug-backpedal/out/backpedal-check/`: original failing and corrected passing recoil fixtures.
- `~/Documents/spidey-work/wt/msaa-audit/out/msaa-check/`: sample-count, image, unsupported-context and failure-cleanup checks. Cinematic results are in `~/Documents/spidey-work/reports/cinematics-2026-09-30/verification.txt`.
- Original database: `~/Documents/spidey-work/idbs/new_idbs/SpideyPC.i64`; original function bytes remain under `tools/functions/`.

All work is split into signed commits. The unpublished movie helper commit was split into one-function commits with an identical final source tree. Earlier untracked files and unrelated worktree drafts were preserved. No PR or public comment was posted. All owned test processes are stopped.
