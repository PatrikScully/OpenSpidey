# OpenSpidey

Spider-Man (2000) is being rewritten in **Godot 4.7.2, C# and .NET 10**, with
64-bit desktop targets for Linux, Windows and macOS. DirectX and the original
32-bit DLL runtime are retired targets.

The Godot port is **in progress**. Early builds support original textured maps,
character animation, traversal, melee and web actions, scripted actors, checkpoints,
story cameras, movies, speech, sound effects and a widescreen HUD. Animation import
covers 108 assets and 6,005 clips. Original game assets are read from an owner-supplied
installation and stay outside the repository.

The managed tests compare gameplay and import behavior with captured original
executable results. Native checks exercise real assets, rendering, audio and scene
ownership. These checks cover individual systems and early story sequences;
full campaign progression, bosses, the frontend and complete playthrough validation
remain unfinished.

The [2026-10-13 development checkpoint](docs/CHECKPOINT_2026-10-13.md) records the
latest build, verification results, outstanding integration work and resume commands.
Existing releases belong to the earlier SDL/C++ runtime.

## Build and inspect

```text
python tools/modern.py bootstrap
python tools/modern.py build
python tools/modern.py test
python tools/modern.py run --assets /path/to/original/game
python tools/modern.py play --assets /path/to/original/game
python tools/modern.py story-smoke --assets /path/to/original/game --rendered
python tools/modern.py checkpoint-smoke --assets /path/to/original/game --rendered
python tools/modern.py media --assets /path/to/original/game
python tools/modern.py voices --assets /path/to/original/game
python tools/modern.py cinema --assets /path/to/original/game --cinema 11
```

Use your own original `data.pkr` and `media.pkr`; no original game assets are
included. The importer reads files without executing `SpideyPC.exe`.
`run` opens the asset workbench; `play` opens the authored Story level session.
The first four mission startups, early checkpoint skipping and the first authored
transition have native verification. Full campaign playthrough remains in progress.
Use WASD or the left stick to move; click to capture the mouse for camera orbit,
and Escape to release it. Space jumps, holds for ceiling attachment and releases
crawl support; it also cancels an active pull. E zips along surface up.
Q or the left trigger controls lookaround targeting; R or the right trigger
selects swinging through the original target rules.
Punch input is J or the gamepad's left face button (Xbox X/Sony Square);
kick input is K or the right face button (Xbox B/Sony Circle). These bindings
provide held and pressed inputs to the source action controller. L or the top
face button (Xbox Y/Sony Triangle) fires webs. Directional web input selects
the original web variants; right plus Web creates a dome, and Jump releases it.
Punch plus Web selects ZipDive. The developing scene connects these original
actions and contacts; full enemy encounters and mission
progression are still being integrated. Checkpoint retry now retains the saved
source checkpoint across scene reloads; `checkpoint-smoke` exercises that real UI
and reload path on L1A1.
`media` prepares all 25 movies and six story covers using FFmpeg with Theora
and Vorbis encoders. `cinema` prepares and plays the selected cover/movie sequence;
Space, Enter or a gamepad's A/Start advances the cover and skips a movie after its
initial input guard. Converted assets stay outside the repository; select a cache
with `--media-cache`. Movie playback keeps the original video proportions within
the current viewport.

Read the [migration status and architecture](docs/GODOT_MIGRATION.md) for exact
coverage, remaining systems and asset validation commands. The recovered C++
source under `src/` remains a gameplay and file-format reference during the port.
The [previous rewrite progress](docs/REWRITE_PROGRESS.md) records the work that
preceded this engine migration.

## Credits

Based on [krystalgamer/spidey-decomp](https://github.com/krystalgamer/spidey-decomp).
The original game was made by Neversoft and ported to PC by LTI Gray Matter.
Spider-Man and the original game assets belong to their respective owners.
Godot and .NET are independent open-source projects; their licenses apply to
redistributed engine and framework components.
