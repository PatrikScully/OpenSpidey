# Building OpenSpidey

The active runtime uses Godot .NET 4.7.2, C# and the 64-bit .NET 10 SDK.
See [the migration guide](GODOT_MIGRATION.md) for setup, test and run commands.

```text
python tools/modern.py bootstrap
python tools/modern.py build
python tools/modern.py test
python tools/modern.py play --assets /path/to/original/game
python tools/modern.py story-smoke --assets /path/to/original/game --rendered
python tools/modern.py story-smoke --assets /path/to/original/game --scene data/L1A2_G.psx --rendered
python tools/modern.py story-smoke --assets /path/to/original/game --scene data/L1A3_G.psx --rendered
python tools/modern.py story-smoke --assets /path/to/original/game --scene data/L1A4_G.psx --rendered
python tools/modern.py story-transition-smoke --assets /path/to/original/game --rendered
python tools/modern.py story-transition-smoke --assets /path/to/original/game --scene data/L1A2_G.psx --rendered
```

`play` runs the authored Story scene. `story-smoke` verifies the original startup
and live source behavior for the selected mission, then closes the engine.
These commands prepare required voice and selected-mission cinema assets in the
private media cache. Original assets stay outside the repository.

CMake remains an optional convenience entry point that invokes the same managed
build; it does not compile or link the retired DirectX runtime.

The earlier C++ build guide is retained under `reference/retired-builds/` for
reference. Its binary-layout checks and injected DLL workflow do not apply to
the Godot port.
