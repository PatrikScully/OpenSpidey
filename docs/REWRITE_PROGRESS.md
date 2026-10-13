# Active target: full Godot rewrite

As of 2026-10-11, the user replaced the incremental C++ cleanup objective with
**rewriting the whole game in Godot**, using C#/.NET and 64-bit desktop targets.
See [Godot migration progress](GODOT_MIGRATION.md). The sections below record
completed work on the recovered reference runtime; they are not a claim that
the new engine port is complete.

The final reference-only trigger registry change passed the GCC headless build
and 17 unit tests before the engine pivot. Native/SDL runtime validation of
that particular change was not completed. The recovered runtime's previous
animation, camera, movement and event checks remain useful behavioral contracts.

# Rewrite progress

Goal: finish the rewrite process, preserving the playable native game and the
recent animation, bomb, vault and widescreen fixes.

## Completed

- Moved all 274 former root C++ files into 18 system directories.
- Added explicit CMake source manifests and an original/new path map.
- Updated source/test include paths, the compatibility MSVC project and CI
  version-header paths.
- Extracted SDL-independent `ViewportLayout`; tested menus, gameplay, portrait
  output, corner HUD positioning and invalid dimensions.
- Added `MemoryAllocator` and `RenderBufferPool`; integrated original `Db_*`
  allocation/release entry points and tested allocation order, duplicate
  allocation rejection, partial failure cleanup and repeated release.
- Headless, native validation and SDL builds pass. All nine CTest cases pass.
- Real-asset animation, bomb pickup and vault regressions pass after the moves
  and resource ownership changes. All six MSAA/output pixel checks pass.
- Added a module manifest/project include consistency checker.
- Extracted native startup orchestration into GameApplication.
- Extracted ZipWebController and PlayerAnimationController; both input paths
  share ray targeting and animation/sound transition behavior.
- Renamed verified player transform axes and interaction target position while
  preserving all member offsets. Native layout validation passes.
- Added and passed an asset-backed ceiling-to-ground zip roundtrip regression
  with rendered limb pose checks.
- Replaced recovered approximate-distance helper call sites with named math
  functions; retained original exports as compatibility adapters. Added tests
  for all axis orderings/signs, horizontal distance and fixed-point rounding.
- Split DXsound.cpp into input_devices, sound_channels and polygon_pipeline
  implementations with separate public contracts. Most callers now include
  only the relevant engine header; the old umbrella remains an adapter.
- Added ButtonState and keyboard/mouse/controller polling regression tests,
  including all 256 original state encodings.
- Replaced opaque transformed vertex members with ScreenVertex names in 368
  compiler-identified accesses; introduced named PolygonPacket members and
  migrated 100 accesses plus the legacy Windows draw branches. Layouts remain
  28 bytes and 0xF0 bytes respectively.
- Added PolygonQueue with injected bucket storage and tests preserving within-
  bucket ordering, reset, reuse and invalid-index handling.
- Named sound-channel loop/status members and channel function parameters;
  separated the legacy no-op logger from renderer declarations.
- Updated native source CI and contributing rules around structural checks,
  unit tests and layout/gameplay evidence rather than mandatory decompile tags.
- Native and SDL builds, six CTest cases, all six MSAA/output pixel checks,
  sound-bank tests and heap/zip/bomb/vault asset regressions pass for this stage.
- Replaced 365 source-only inline definitions with standard out-of-line
  definitions and normalized 25 header inline definitions. Removed the INLINE
  macro and GCC's `-fkeep-inline-functions` requirement. Corrected an unsigned
  texture-mask switch rejected by Clang. GCC and Clang both pass the native
  layout executable and unit tests; Clang also builds the headless game.
- Enabled both GCC and Clang in the main Linux CI compiler matrix.
- Extracted LevelSession with separate preparation, frame updates, frame-rate
  display, presentation and exit cleanup. PlayAway remains an export adapter.
- Added TickClock and FramePacer, preserving the two-tick native physics speed
  and existing SPIDEY_60FPS opt-in. Tests cover ticks already spent rendering,
  slow frames, delayed wake-up, the opt-in mode and counter rollover.
- Named CBody timing fields mLastUpdateTick, mElapsedTicks and
  mAccumulatedTicks at their verified original offsets. Migrated 210 typed
  member accesses across the actor/player/object code; unrelated structures'
  fields remain untouched. All 33 affected source/header files compile.
- Added an asset-backed level-session regression for the viewer-only return,
  three real Logic/Display/presentation frames, controlled exit and completed
  sound cleanup. It passes at both 30 and 60 FPS. The fixture resets shell
  viewer/abort flags because forced level entry bypasses the normal menu reset.
- Rechecked seven CTest cases, heap/zip/bomb/vault regressions and all six
  MSAA/output pixel cases after the compiler and frame-loop changes.
- Replaced SpideyMain's menu/load/play/shutdown gotos with GameFlow and an
  injected GameFlowActions effect contract. Original exports, startup/shutdown
  order, retry/restart paths, final completion and menu parameter consumption
  are preserved. The production adapter centralizes repeated exit cleanup and
  saved-level loading. LevelExitReason names all original numeric exit codes.
- Added game-flow tests covering all recognized exit codes, invalid exits,
  retry rejection without repeated cleanup, accepted retry, debug cycling,
  activation, restart, model preview, shell-free quitting and retained/consumed
  menu requests. All eight CTest cases pass under GCC and Clang.
- Named SSaveGame's mLevelName and mLevelCompletionCounts in 58 typed accesses
  and moved the record into engine/persistence/save_game_state.h. The 0xBC
  record's offsets are unchanged; unresolved fields remain documented.
- Added and passed a real reload/quit/shutdown regression and controlled final-
  area completion checks for counts 254 and 255. These prove application
  bookkeeping, not final-mission gameplay completion.
- Rechecked heap, ceiling/ground zip, bomb pickup, vault animation, 30/60 FPS
  level sessions and the new game-flow runtime branches with the updated SDL
  build. Native layout validation passes under both GCC and Clang.
- Extracted ActorListUpdater from the application file into engine/world.
  Deferred deletion, distance suspension, inclusive resumption, timing,
  animation, AI and shadow ordering are preserved. It borrows the mutable
  suspension threshold so commands can change it during a pass. Original Ob_
  adapters retain the compatibility forwarding behavior.
- Added visitIntrusiveList with next-link snapshots and borrowed visitors;
  tests cover moving/deleting current nodes, inserted heads, empty lists and
  stateful callbacks. Address/undefined-behavior sanitizer checks pass.
- Moved optional actor input tracing to InputUpdateTrace, injected as an
  observer; the engine updater no longer depends on game input state.
- Corrected the suspended-list macro spelling without changing exported
  storage. Actor sizes, offsets and virtual layouts remain unchanged.
- Added and passed an actual pickup lifecycle regression covering suspension,
  inclusive resumption, saved home lists and two-pass deletion through the
  original pickup destructor. Native layouts and all nine CTest cases pass
  under GCC and Clang; SDL gameplay regressions pass after integration.
- Extracted WorldSimulation from Logic, with named input/debug, active-world,
  trigger/UI and camera-mode phases. Camera AI remains in Display through the
  same simulation class. Original phase ordering and debug gates are preserved.
- Extracted PlayerFrameTrace from gameplay orchestration, named its local
  values and replaced environmental object byte offsets with verified fields.
  Its process-wide enable setting, record format and one-time dump are preserved.
- Added and passed a real frame-phase regression for normal/frozen worlds and
  input-only modes, including counter behavior. Tracing emits 61 player records
  and exactly one environment dump in the controlled fixture.
- GCC/Clang layout builds, nine CTest cases and SDL heap/zip/bomb/vault,
  lifecycle, 30/60 FPS sessions, reload/quit and completion regressions pass
  after the simulation extraction.
- Extracted GameplayRenderer from Display into named camera, HUD, world setup,
  texture, message, actor and final-overlay phases. Render-list indices and
  local actor names now describe their roles. Special actor effects and
  primitive/buffer accounting have separate methods; original ordering remains.
- Extracted BaddyFrameTrace from frame composition. Its enable setting and
  diagnostic format are preserved, with a bounded thirty-frame sample counter.
- Added a real rendering regression observing subsystem calls and checking HUD
  coordinate mode before panel submission and world mode before scene setup.
- GCC/Clang layouts and nine CTest cases pass after renderer integration.
  SDL render phases, animation heap, ceiling roundtrip, bomb/vault and both
  session pacing modes pass. Pixel tests pass at 640x480, 1280x720 and 1920x720
  with antialiasing disabled and enabled.
- Extracted ScriptedInputController from the player's bytecode VM, with named
  preparation, decoding, command update, input publication and completion phases.
  Teleport handling and button-register access are separate methods. Opcodes
  now have descriptive names, and animation holders reuse the animation service.
- Named six verified player script fields and updated compiler-identified input,
  UI and actor accesses. Added explicit layout checks for the elapsed clock,
  decoding byte and encoded cursor. Original field storage/layouts are retained.
- Centralized the verified virtual-slot-zero deletion adapter in compatibility.
  Unverified player/object types remain explicit rather than guessed.
- Documented bytecode, concurrent command, duration and input publication
  contracts in docs/SCRIPTED_PLAYER_INPUT.md. The real-player fixture passes
  delayed input, concurrent buttons, timer boundaries, next-frame resume,
  Start/Select, animation/sound pairing and signal-gated completion checks.
- Consolidated player/Black Cat/Spider-Man clone command ownership operations
  into ScriptCommandList with injected allocation policy. ScriptCommandView
  centralizes type, length, parameters and original 32-bit links. Legacy member
  exports delegate to the shared implementation without changing actor layouts.
- Replaced the player's decoded header/link/parameter offsets with typed record
  creation and borrowed command views. The NPC bytecode loops share link access.
- Fixed removal-by-type traversal reading a freed command block. The service
  also leaves existing lists intact on allocation failure and rejects records
  owned by a different list.
- Added a tenth CTest fixture for command allocation/linking, failure handling,
  head/middle/tail/consecutive removal, foreign ownership and borrowed lifetime.
  It passes with GCC/Clang and with 32-bit ASan/UBSan enabled.
- Consolidated Black Cat/Spider-Man clone bytecode into ActorScriptController,
  with borrowed state views, explicit behavior profiles and injected command
  allocation. Named decoding/preparation/update/completion phases preserve the
  actors' speed, animation, movement scaling, signal and checkpoint differences.
- Named thirteen verified actor script fields plus the clone's gravity
  acceleration. Existing offsets and exported methods remain unchanged.
- Added real-actor factory/cleanup coverage for delayed scripts, concurrent
  timers, signal selection, checkpoint restoration, walking speed/animations,
  movement scaling and arrival resets. The original actor list is restored.
- GCC/Clang layout checks and all ten CTest cases pass after actor-script
  integration. Existing player/renderer/gameplay regressions remain passing.
- Extracted Black Cat/clone movement into ActorMotionIntegrator over borrowed
  motion records. Separate two-tick/fixed-timestep methods preserve their timing
  policies, integration order, angular damping and rounding.
- Removed scalar-as-vector casts and scalar-vector construction from actor
  movement. Added wrapping math utilities preserving original 32/16-bit
  arithmetic, signed division and x86 variable shifts without undefined overflow.
- Named Black Cat gravity and the clone's elapsed-history/timestep fields;
  original actor sizes, offsets and exported physics entry points are preserved.
- Added an eleventh CTest fixture for trajectories, odd/zero steps, negative
  rounding, damping and overflow. A separate executable-instruction oracle
  matches all body bytes across 4,000 cases with GCC, Clang and ASan/UBSan.
- Layout builds, all eleven CTest cases and SDL actor/script/render, heap/zip,
  bomb/vault, both session rates and widescreen pixel checks pass.
- Rewrote vector arithmetic as independently linkable core code with explicit
  32/16-bit wrapping and masked shifts. Core vector/friction headers no longer
  include executable globals. All vector constructors initialize every axis.
- Isolated original GTE length methods, exports and the misleading vector-product
  overload in the compatibility bridge. Web/swing, fragments, player offsets and
  Mysterio effects now multiply by explicit scalars; binary layouts remain intact.
- Added the twelfth CTest case and shared original-executable fixture loader.
  Vector results match original instructions across 100,000 comparisons under
  GCC, Clang and ASan/UBSan. The 4,000-case actor-motion oracle still passes.
- All twelve CTest cases, GCC/Clang layout validation and the rebuilt SDL actor,
  script, render, heap/zip, bomb/vault, 30/60 FPS and widescreen pixel fixtures pass.
- Extracted web-swing phase/geometry/animation policy into SwingArc and ordered
  collision response into SwingCollisionResolver with a borrowed world probe.
  DoSwingingPhysics now samples hooks and delegates to the focused resolver.
- Added typed CSwinger position access, named phase/render-end offset and player
  surface clearance. Shared matrix-axis/line update methods replace duplication
  and raw segment strides; original sizes/offsets and exports remain intact.
- Added the thirteenth CTest case and a 10,000-case original-instruction swing
  oracle for rays, position/velocity, flags, animation frames and completion,
  passing under GCC, Clang and sanitizers. A real-object regression verifies
  world probes, line synchronization and cleanup restoring bit/list ownership.
- All thirteen CTest cases, GCC/Clang layout validation and rebuilt SDL gameplay,
  script/render, heap/zip, bomb/vault, pacing and widescreen checks pass.
- Rewrote surface crawling into CrawlPhysicsController phases for movement,
  forward/corner probes, missing-surface recovery, response/control transitions
  and support-body notification. DoCrawlingPhysics is an export adapter.
- Extracted independently linkable CrawlMotion arithmetic and optional
  CrawlFrameTrace diagnostics. Named eight verified player flags, orientation,
  transition-distance and support fields; inherited normal access uses a player
  method without changing the shared body layout.
- Added the fourteenth CTest case and 10,000-case original-instruction crawl
  movement comparison, passing under GCC, Clang and sanitizers. Real bank-level
  fixtures verify stationary/moving/missing-surface queries after an overhang
  roundtrip, contact flags and position recovery. Optional diagnostics pass.
- All fourteen CTest cases, GCC/Clang layout validation and rebuilt SDL gameplay,
  actor/script/swing/render, heap/zip, bomb/vault, pacing and widescreen checks pass.
- Rewrote ordinary player physics into PlayerPhysicsController phases for arena
  constraints, body/shoulder sweeps, deflection, vertical travel, ground queries,
  landing and support notification. Original exports and policy cells remain
  adapters; no horizontal sweep now explicitly means no hit, fixing a stack read.
- Shared velocity/displacement math with crawling via PlayerMotion. Added
  SweepGeometry/GroundProbeFan utilities and defined original x87 word conversion.
  Named six verified physics fields without changing layout.
- Added the fifteenth CTest case and original-instruction comparisons for 10,000
  collision-free movement updates and 10,000 floating-word conversions, passing
  under GCC, Clang and sanitizers. The shared crawl movement oracle still passes.
- Real-level regressions cover stationary contact, vertical-only motion, falls,
  wide-ground query ordering, zip bypass, arena projection and wall-probe heading.
  GCC/Clang layouts, all fifteen tests and existing SDL gameplay/widescreen checks pass.
- Extracted PlayerBehaviorController with explicit preparation, animation follow-on,
  action routing and shared finalization phases. SpideyAI0 is an export adapter.
- Named the player action word and introduced PlayerAction; all 31 actions have
  individual handlers. Kept loop exits, high-bit switch routing, unknown-action
  finalization and the suspension gate. Isolated address dependencies in a bridge.
- Added real-level phase/routing checks; GCC/Clang layouts, all fifteen unit tests,
  gameplay/zip/bomb/vault/session checks and six widescreen pixel checks pass.
- Extracted PlayerFrameFinalizer with 16 ordered methods for camera, heading,
  movement, platform riding, impulses, contacts, pickups, triggers and presentation.
  Camera/presentation implementations are separate; cached heading, query storage
  and mutable hand positions retain their original cross-phase relationships.
- Named four verified heading fields at unchanged offsets. Real-level checks
  verify phase order and scripted heading progression/completion, alongside all
  fifteen unit tests, GCC/Clang layouts and existing SDL/widescreen regressions.
- Extracted PlayerFramePreparation with 15 ordered methods; status effects and
  analog/lookaround input now have separate files. The central behavior controller
  is 73 lines, with explicit preparation, animation, dispatch and finalization.
- Named suspension, countdown and fall-reference fields at original offsets.
  Real-level coverage verifies the nine steps before suspension, all 15 active
  preparation steps, countdown clamping and guarded web regeneration. Native
  layouts, all fifteen unit tests and gameplay/widescreen regressions pass.
- Grouped the 31 action handlers into movement, web, combat and interaction files;
  the router is 48 lines. Named 38 action transitions with PlayerAction/actionWord.
- Extracted LocomotionAnimationPolicy for falling and reverse-movement decisions;
  reference reset, playback and pending-release consumption have explicit results.
  Named pending web-release and fall-reference position fields at original offsets.
- Added the sixteenth CTest case and 2,097,152 original-instruction comparisons
  covering every u16 animation, velocity boundaries and release-byte values under
  GCC, Clang and sanitizers. The actual falling handler and all existing gameplay,
  layout and widescreen regressions pass.
- PC sleep/idle inhibition is confirmed active.

## In progress

- Player/world behavior migration continues. Player action routing and shared
  finalization have named services; recovered handler internals remain. Application startup, per-level
  orchestration, top-level transitions and gameplay frame composition now have
  separate classes. Existing
  recovered world/player/actor implementations remain functional but have not
  yet been fully semantically rewritten.

## Pending

- Migrate shared world state into named systems. Native startup, level sessions,
  top-level transitions and Logic/Display frame orchestration are extracted.
- Continue player locomotion/animation-state migration. Ordinary, crawling and
  swing physics now have focused controllers/utilities; broader player AI/control
  handler internals and opaque fields remain to be rewritten.
- Migrate world collision, triggers and interactive object behavior.
- Continue replacing recovered graphics/audio state with system classes; the
  source split and named packet/vertex contracts are complete.
- Migrate the legacy MSVC6 DLL build/release jobs to a modern toolchain while
  preserving the compatibility artifact. They still target the old compiler
  and are not proven for the C++11 rewrite.
- Clean asset, sound, input, save, UI, cinematic and level implementations.
- Migrate actor implementations and verified globals; retain documented binary
  layouts and original export adapters where required.
- Broaden behavioral regression coverage and perform final playable validation.

The PC sleep/idle inhibitor is active: systemd-inhibit PID 287053, exec session
71351. Leave it active until the entire goal is achieved; then stop it.

Latest stage evidence is in /tmp/spidey-simulation-{sdl,native,clang}-build.log,
/tmp/spidey-simulation-{native,clang}-run.log and
/tmp/spidey-simulation-{phases,lifecycle,heap,zip,bomb,vault,session30,session60,reloadquit,completion}.log.
The sanitized list walker is /tmp/spidey-list-walk-sanitized.
Widescreen pixel evidence remains in /tmp/spidey-session-raster-{640,1280,1920}-{0,4}.log.
Private Xvfb :93 remains live (PID 290585). Use SDL_VIDEO_DRIVER=x11 with it.
User assets and launcher settings are unchanged. No commit/staging requested.

Renderer-stage evidence: /tmp/spidey-renderer-{headless,native,clang,sdl}-build.log,
/tmp/spidey-renderer-{native,clang}-run.log,
/tmp/spidey-renderer-{phases,heap,zip,vault,bomb,session30,session60}.log and
/tmp/spidey-renderer-raster-{640,1280,1920}-{0,4}.log.

Script-controller evidence: /tmp/spidey-script-{native,clang,sdl}-build.log,
/tmp/spidey-script-{native,clang}-run.log,
/tmp/spidey-script-{script,phases,heap,zip,vault,bomb,session30,session60}.log and
/tmp/spidey-script-raster-{640,1280,1920}-{0,4}.log.

Command-list evidence: /tmp/spidey-command-{headless,native,clang,sdl}-build.log,
/tmp/spidey-command-{native,clang}-run.log,
/tmp/spidey-command-{script,phases,heap,zip,vault,bomb,session30,session60}.log and
/tmp/spidey-command-raster-{640,1280,1920}-{0,4}.log.
The 32-bit sanitized command fixture is /tmp/spidey-command-list-sanitized;
its zero-error result is /tmp/spidey-command-list-sanitized.log.

Actor-script evidence: /tmp/spidey-actor-script-{headless,native,clang,sdl}-build.log,
/tmp/spidey-actor-script-{native,clang}-run.log,
/tmp/spidey-actor-script-{actors,script,phases,heap,zip,vault,bomb,session30,session60}.log
and /tmp/spidey-actor-script-raster-{640,1280,1920}-{0,4}.log.
Actor motion integration is now extracted and verified. Actor AI, shadows and
other recovered physics remain to be migrated.

Motion-stage evidence: /tmp/spidey-motion-{headless,native,clang,sdl}-build.log,
/tmp/spidey-motion-{native,clang}-run.log,
/tmp/spidey-motion-{actors,script,phases,heap,zip,vault,bomb,session30,session60}.log
and /tmp/spidey-motion-raster-{640,1280,1920}-{0,4}.log.
Oracle proof: /tmp/spidey-motion-oracle{,-clang,-sanitized}.log. The independent
sanitized unit fixture is /tmp/spidey-motion-sanitized with its corresponding log.

Vector-stage evidence: /tmp/spidey-vector-{headless,native,clang,sdl}-build.log,
/tmp/spidey-vector-{headless,native,clang}-tests.log,
/tmp/spidey-vector-{native,clang}-run.log,
/tmp/spidey-vector-{actors,script,phases,heap,zip,vault,bomb,session30,session60}.log
and /tmp/spidey-vector-raster-{640,1280,1920}-{0,4}.log.
Original-instruction evidence: /tmp/spidey-vector-oracle{,-clang,-sanitized}.log
and /tmp/spidey-vector-motion-oracle.log. The independent sanitized unit fixture
is /tmp/spidey-vector-sanitized with its corresponding zero-error log.

Swing-stage evidence: /tmp/spidey-swing-{headless,native,clang,sdl}-build.log,
/tmp/spidey-swing-{headless,native,clang}-tests.log,
/tmp/spidey-swing-{native,clang}-run.log,
/tmp/spidey-swing-{swing,integrated,actors,script,phases,heap,zip,vault,bomb,session30,session60}.log
and /tmp/spidey-swing-raster-{640,1280,1920}-{0,4}.log.
Original-instruction evidence: /tmp/spidey-swing-oracle{,-clang,-sanitized}.log.
The independent sanitized unit fixture is /tmp/spidey-swing-sanitized with its
corresponding zero-error log.

Crawl-stage evidence: /tmp/spidey-crawl-{headless,native,clang,sdl}-build.log,
/tmp/spidey-crawl-{headless,native,clang}-tests.log,
/tmp/spidey-crawl-{native,clang}-run.log,
/tmp/spidey-crawl-{crawl,swing,actors,script,phases,heap,zip,vault,bomb,session30,session60}.log
and /tmp/spidey-crawl-raster-{640,1280,1920}-{0,4}.log.
The final diagnostic cleanup is verified by /tmp/spidey-crawl-trace.log.
Original-instruction evidence: /tmp/spidey-crawl-oracle{,-clang,-sanitized}.log.
The independent sanitized unit fixture is /tmp/spidey-crawl-sanitized with its
corresponding zero-error log. General player physics and broader corner/platform
behavior remain pending; crawl movement evidence does not prove full missions.

Player-physics evidence: /tmp/spidey-player-physics-{headless,native,clang,sdl}-build.log,
/tmp/spidey-player-physics-{headless,native,clang}-tests.log,
/tmp/spidey-player-physics-{native,clang}-run.log,
/tmp/spidey-player-physics-{regular,integrated,crawl,swing,actors,script,phases,heap,zip,vault,bomb,session30,session60}.log
and /tmp/spidey-player-physics-raster-{640,1280,1920}-{0,4}.log.
Instruction evidence: /tmp/spidey-player-motion-oracle{,-clang,-sanitized}.log,
/tmp/spidey-floating-word-oracle{,-clang,-sanitized}.log and
/tmp/spidey-player-physics-crawl-oracle.log. The sanitized unit fixture is
/tmp/spidey-player-motion-sanitized with its zero-error log.

Behavior-stage evidence: /tmp/spidey-behavior-{headless,native,clang,sdl}-build.log,
/tmp/spidey-behavior-{headless,native,clang}-tests.log,
/tmp/spidey-behavior-dispatch.log, /tmp/spidey-behavior-runtime.log,
/tmp/spidey-behavior-{regular,crawl,swing,actors,script,phases,heap,zip,vault,bomb,session30,session60}.log
and /tmp/spidey-behavior-raster-{640,1280,1920}-{0,4}.log.
The extraction does not prove all 31 actions or full missions; address-based
services and recovered handler internals remain pending.

Finalizer-stage evidence: /tmp/spidey-finalizer-{headless,native,clang,sdl}-build.log,
/tmp/spidey-finalizer-{headless,native,clang,native-layout,clang-layout}-tests.log,
/tmp/spidey-finalizer-finalizer.log, /tmp/spidey-finalizer-runtime.log,
/tmp/spidey-finalizer-{regular,crawl,swing,actors,script,phases,heap,zip,vault,bomb,session30,session60}.log
and /tmp/spidey-finalizer-raster-{640,1280,1920}-{0,4}.log.
Finalization is separated and ordered, but remaining fixed-address dependencies,
individual action internals and full-mission coverage are still pending.

Preparation-stage evidence: /tmp/spidey-preparation-{headless,native,clang,sdl}-build.log,
/tmp/spidey-preparation-{headless,native,clang,native-layout,clang-layout}-tests.log,
/tmp/spidey-preparation-finalizer.log, /tmp/spidey-preparation-runtime.log,
/tmp/spidey-preparation-{regular,crawl,swing,actors,script,phases,heap,zip,vault,bomb,session30,session60}.log
and /tmp/spidey-preparation-raster-{640,1280,1920}-{0,4}.log.
The orchestrator and phase boundaries are clear; recovered conditionals,
address-based services and unverified fields remain ongoing rewrite work.

Locomotion-stage evidence: /tmp/spidey-locomotion-{headless,native,clang,sdl}-build.log,
/tmp/spidey-locomotion-{headless,native,clang,native-layout,clang-layout}-tests.log,
/tmp/spidey-locomotion-finalizer.log, /tmp/spidey-locomotion-runtime.log,
/tmp/spidey-locomotion-{regular,crawl,swing,actors,script,phases,heap,zip,vault,bomb,session30,session60}.log
and /tmp/spidey-locomotion-raster-{640,1280,1920}-{0,4}.log.
Original-instruction evidence: /tmp/spidey-locomotion-oracle.log and
/tmp/spidey-locomotion-oracle-{clang,sanitized}-build.log.
The policy is verified independently; other handler internals, fixed-address
services and full-mission behavior remain migration work.
