# Changelog

## v0.0.3, OpenSpidey

This development preview includes the work since v0.0.2, released on
4 September 2026. The full campaign and decompilation remain unfinished.

### Setup and downloads

- Add an OpenSpidey setup app for the first launch, with saved preferences
  and a separate Settings app. Scale the layout to desktop font settings
  and keep controls reachable on small screens.
- Import the four required PC game files from a disc ISO without mounting
  it or running the original installer. Check the supported executable and
  archive indexes, show progress, allow cancellation and preserve saves.
- Offer windowed, borderless and fullscreen modes, output resolution,
  antialiasing, texture filtering, VSync, volume and mouse settings.
  Borderless fullscreen uses the desktop resolution and pixel density.
- Add native Windows build support and CI packaging for Linux and Windows,
  with the launcher and runtime dependencies. Keep the original-game DLL
  as a separate developer download.
- Enable PulseAudio in the Linux runtime and check audio startup failures.
- Use a working native 32 bit graphics stack when available, keeping its
  driver and libraries together. Retain the bundled software fallback.
- Consume the movie skip key before returning to gameplay, so holding
  Escape to skip a movie does not immediately open the pause menu.
- Process keyboard and window events during comic-cover waits. Reduce CPU
  use during standalone timer waits while preserving the original tick
  deadlines and game speed.
- Rename the app and repository to OpenSpidey, and document setup, builds,
  contributions and current test coverage.

### Graphics and animation

- Keep distant loaded map geometry visible by default. Correct frustum
  normals and extend the depth range while keeping the sky behind the map.
- Fix stretched thug limbs caused by stale model stitch indices. Restore
  stitched lighting colors and the original ambient color order.
- Fix texture perspective, billboard vertices, sprite depth and vertical
  projection. Correct scratch buffer pointers used by effect rendering.
- Fix the main-menu model reader's face stride, which caused a native
  menu crash.
- Restore the Kid Mode menu web effect to its original position beside
  Spider-Man's hand.
- Add MSAA with a fallback when the requested sample count is unsupported.
- Cover every edge of cinematic bars and flat overlays at scaled resolutions,
  including multisampled and high density fullscreen output.
- Add mipmaps and anisotropic filtering for suitable repeating world
  surfaces. Preserve character, sprite and HUD texture sampling.
- Fix Spider-Man's scripted animation transitions and completed joint
  branches. Correct Black Cat head tracking and part-angle calculations.
- Correct camera quaternion conversion so horizontal turns do not add roll.
- Fix Rhino shadows and textured ribbons, and restore Mysterio's model
  filenames.

### Controls and gameplay

- Add WASD movement, mouse look, mouse attacks, configurable sensitivity
  and vertical inversion. F1 releases or captures the mouse.
- Restore melee combo tracking and power-up effects in the native game.
- Fix native save paths and create the save folder, using relative paths
  instead of Windows separators and an encoded absolute folder name.
- Restore many thug states: patrol, chase, melee, aimed shooting, grenade
  throws, grabs, web reactions, hits, falling and death.
- Restore grenade motion, bullet tracers, ricochets and impact flashes.
- Fix thug recoil movement and animation sound flags.
- Continue police and SWAT restoration with model setup, guard and search
  animations, attack spacing, hit reactions, messages and sound cues.
  Full police behavior is still unfinished.
- Keep script-only actors on their control list, away from enemy combat
  collision tests. Fix trigger-node reads and scene cleanup.
- Fix platform command numbers, helicopter update loops, region-pool
  checks and model/access lookup paths.
- Use the original game's globals for the next Windows DLL model and
  animation calls, one fix at a time.

### Web effects

- Restore visible swing and tug webs, their endpoints, retraction and
  released strands.
- Restore tug release and broken web fragments, with cleanup that uses
  native code rather than jumping into the original Windows executable.
- Restore moving web balls, enemy damage, wall marks, impact rings and
  fragment animation and lifetimes.

### Audio and cinematics

- Decode original packed Bink dialogue and music into independent SDL
  streams, with volume, pause, completion and bounded buffering.
- Play the original Bink movies with sound and timed frames. Stop and clean
  up decoders when playback finishes, is replaced or the game exits.
- Use Escape to skip movies and eligible in-engine scenes, with the
  original scene flags and cleanup.
- Correct the original effect pitch conversion and restore the original
  sample rate when pitch is zero.
- Set native sound-bank readiness from loaded WAV buffers, so player attack
  sounds do not depend on an uninitialized Dreamcast allocation result.
- Add master, music/voice and effect volume controls, including cinematic
  audio mute.

### Verification and remaining work

- The 1 October Linux checkpoint passed loading and short idle gameplay
  checks for 68 map entries and 23 training configurations. These are
  smoke tests, not completed missions or objectives.
- Separate runtime checks covered web-ball hits, misses and wall impacts,
  tug release, left-mouse melee, camera sweeps and death-menu selection.
- Initial level-one player and NPC state matched the original game. Later
  spawn waves and complete routes remain to be compared.
- Checked decoded audio, pitch fixtures, pause/mute/cleanup, world texture
  filtering, character/HUD sampling and build/layout checks.
- Check native save-file creation, write/read, size and callbacks under
  normal, Unicode and long Linux working folders, and Unicode Windows
  working folders under Wine. Full in-game save and reload progression
  still needs testing.
- Test the frozen Windows setup, ISO import, saved settings, movie skip,
  main menu and short level-one movement and attack input under Wine.
  Test the Linux app in a clean Ubuntu 22.04 container without Python,
  Tkinter or a system 32 bit runtime.
- Full campaign routes, bosses, collectibles, save/reload flows, remaining
  NPC behavior and compiler matching still need work.
- Twelve voice-table references were absent from the available original
  installation. Missing assets are reported rather than replaced.
- The legacy Windows/Wine proxy still has a known late stack-overflow
  fault. The native Windows app is separate and still needs full mission
  and physical Windows testing.

See [the recorded checkpoint](tests/gameplay/checkpoints/2026-10-01.json)
and [scenario inventory](tests/gameplay/scenarios.json) for the exact scope.

## v0.0.2, 4 September 2026

- Restore keyboard input and fix the swapped mouse axes.
- Fix Black Cat's movement, aim and body matrix in the opening scene.
- Fix the difficulty menu, HUD shear, title screen color order and sky.
- Fix animation decoding, a wall collision seam and missing level-one
  buildings.
- Open at 1280x960 by default and keep the original 30 fps game timing.

## v0.0.1

First tagged standalone build, with native Linux rendering and the
original-game Windows proxy DLL.
