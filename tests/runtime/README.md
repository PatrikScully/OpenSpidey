The animation/render allocation regression needs the original game assets and
an unstripped standalone debug build. Run it on a private test display:

```sh
DISPLAY=:93 SDL_VIDEO_DRIVER=x11 SDL_AUDIODRIVER=dummy \
  LD_LIBRARY_PATH=/path/to/runtime \
  gdb -batch -x tests/runtime/animation_render_heap.gdb \
  --args /path/to/runtime/spider /path/to/game/assets
```

It enters the first level, verifies that persistent polygon buffers do not
overlap the compressed ceiling transition animation, renders eight frames
with the HUD enabled, and checks that the animation bytes remain unchanged.
It then decodes an airborne transition pose and checks the limb offsets.
Resetting the heap after `Db_Init` caused HUD polygons to overwrite this
animation; the allocation check catches that regression before rendering.
The fixture uses the original executable's fixed globals and animation 270.

The player-controller regression uses the same command and environment with
`-x tests/runtime/zip_web_roundtrip.gdb`. It places the player under the first
bank level's ceiling, issues one zip input in each direction, advances 120 AI
frames, renders the transitions, checks every limb pose and verifies that the
player reaches the ceiling and returns to the floor. It writes no screenshots.

The level session regression uses `-x tests/runtime/level_session.gdb`. It
checks the viewer-only return, then runs three complete gameplay frames with
real world updates and presentation before issuing a controlled level exit.
It verifies pacing and reaches the sound cleanup. Run it twice: once with
`SPIDEY_60FPS` unset and once with `SPIDEY_60FPS=1`. The fixture clears the
shell's viewer/abort flags after forcing a level, since forcing a level skips
the menu code that normally resets them. The frame-pacer unit test separately
checks slow frames, delayed wake-ups and tick-counter rollover.

`game_flow.gdb` runs a real level reload followed by a quit, and checks that
the application shuts down once and the process exits normally. Use the same
display, runtime and asset arguments as the other fixtures.

`game_flow_completion.gdb` exercises final-area bookkeeping against a loaded
bank scene. It sets the saved level name to the final area, issues a controlled
completion code and checks the completion counter and pending menu notification.
Run with `SPIDEY_COMPLETION_COUNT=254` and `255` to check increment and saturation.
This covers the application's completion branch; it does not prove that the
final mission or boss can be completed through gameplay. It stops before saving
or entering the menu.

`actor_lifecycle.gdb` uses actual pickups and the trigger factory. It suspends
multiple pickups, verifies their saved home list, checks the inclusive resume
boundary, then marks one for deletion. The first update preserves the actor and
sets its deletion-grace flag; the second invokes the real pickup destructor and
removes it from the active list. Other pickups remain suspended. This fixture
exercises list movement and lifetime management without replacing actor methods
with test implementations.

`world_simulation.gdb` checks the active-world/UI/camera-mode phase order. It
also verifies that training/water freeze flags leave UI and camera servicing
active, while input-only debug modes leave counters unchanged. It enables player
tracing and exercises its one-time environment dump using typed object fields.
The driver checks 61 player records and exactly one environment summary.

`gameplay_renderer.gdb` observes actual camera, panel, scene setup, target,
scene cleanup, particle and overlay calls through one loaded gameplay frame.
It checks that gameplay/HUD coordinate modes are enabled at panel submission
and that HUD mode is restored before scene setup. Observing subsystem calls
keeps this valid when optimized builds inline renderer phase methods. The
separate video raster fixture checks widescreen corner anchoring, fullscreen
coverage, square aspect ratios and the following menu frame's 4:3 viewport.

`scripted_player_input.gdb` feeds controlled bytecode into a real loaded player.
It checks delayed execution, concurrent digital/direction inputs, zero-inclusive
timer expiration, next-frame decoding resumption, checkpoint/flag updates,
Stop completion and Start/Select preservation. A second script exercises
animation/sound pairing, a concurrent player-value timer and input-signal
resumption. It uses the real command allocator/deleter and animation routines;
the fixture frees its bytecode allocation and writes no save data.

`actor_scripts.gdb` loads Black Cat model/animation data and creates Black Cat
and the Spider-Man clone through their actual factories. It checks delayed
instructions, concurrent animation/look timers, distinct completion signal
sources, movement speed policy, checkpoint/skip handling, walking animation
cycles and arrival resets. Module cleanup invokes real destructors and restores
the prior actor list. The fixture clears manually injected signal bits before
teardown. Black Cat's body-flags bit 0 overlaps the normal suspension flag;
this fixture directly exercises the VM rather than the full actor update loop.
It does not validate complete missions, graphics or actor physics.

`actor_motion_oracle.cpp` is an optional Linux/i386 test against the original
executable's two integer actor physics methods. It compares all actor bytes
across 4,000 seeded inputs, including overflow and timestep/friction extremes.
The fixture needs no display, DLLs or Windows startup. Compilation and method
scope are documented in [Actor motion](../../docs/ACTOR_MOTION.md).
