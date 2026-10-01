Your friendly neighborhood PC rebuild now has a setup app.

Download the Linux or Windows app, extract the whole archive, open
**OpenSpidey**, choose your Spider-Man (2000) PC ISO or installed game folder,
and press **Start game**. Setup remembers your video, sound and controls.
Open the separate **Settings** app whenever you want to change them.

## What changed since v0.0.2

- **Setup and downloads:** first-run setup, ISO import with progress and
  cancellation, saved preferences, preserved saves, and native Linux and
  Windows CI packages with the launcher and runtime dependencies.
- **Graphics and animation:** distant loaded buildings stay visible;
  stretched thug limbs, sprite projection, texture perspective and camera
  roll are fixed. Add antialiasing and smoother distant world textures.
  Fix Spider-Man scripted animations and Black Cat head tracking.
- **Controls and gameplay:** WASD and mouse look, mouse attacks, sensitivity
  and inversion, restored melee combos and power-ups, many thug combat
  states, and fixes for native save paths, script actors, triggers and effect
  rendering.
- **Webs:** visible swing and tug webs, released strands, moving web balls,
  enemy hits, wall marks, impact rings and fragments.
- **Audio and scenes:** original packed dialogue, music and movies with
  sound, correct effect pitch, volume controls, Escape skip and decoder
  cleanup.

[The full changelog](https://github.com/PatrikScully/OpenSpidey/blob/main/CHANGELOG.md)
groups the rendering, gameplay, audio, platform and decompilation work since
the previous release.

## Downloads

- `openspidey-v0.0.3-linux-x86_64.tar.gz`: Linux app for 64 bit x86 desktops
  with glibc 2.35 or newer. Includes the 32 bit game runtime and a Mesa
  software rendering fallback. Hardware acceleration needs compatible
  32 bit graphics drivers.
- `openspidey-v0.0.3-windows-x86_64.zip`: native app for 64 bit Windows 10 or
  newer, with its setup launcher and runtime dependencies.
- `openspidey-v0.0.3-windows-binkw32.zip`: separate developer proxy DLL for
  the original Windows game.
- `openspidey-v0.0.3-source.tar.gz` and `.zip`: OpenSpidey source.
- `openspidey-v0.0.3-linux-runtime-source.tar.xz`: corresponding source for
  the bundled Linux runtime libraries.

You need your own supported original PC game data. No game assets are
included. The imported original executable supplies data only; its code
does not run in the native game.

## Tested scope

This is a **development preview**. The latest Linux gameplay checkpoint
loaded 68 map entries and 23 training configurations and checked short idle
gameplay. Separate checks covered attacks, web effects, camera movement,
audio and texture filtering. These checks do not mean the full campaign,
every boss or all later enemy spawns are complete. Police AI and other game
functions remain unfinished. Native Windows gameplay validation is in
progress; the legacy Windows/Wine proxy still has a known late stack-overflow
fault.

See [the recorded test results](https://github.com/PatrikScully/OpenSpidey/blob/main/tests/gameplay/checkpoints/2026-10-01.json).
Please report problems with the map, your last action and the game log.

OpenSpidey builds on
[krystalgamer/spidey-decomp](https://github.com/krystalgamer/spidey-decomp).
Thanks to krystalgamer and the original project's contributors for its
decompilation work and tools.
