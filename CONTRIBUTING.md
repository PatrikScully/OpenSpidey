# Contributing

The active project is the 64-bit Godot/C# rewrite under `modern/`. Read
[the migration guide](docs/GODOT_MIGRATION.md) before changing gameplay behavior.
Recovered C++ under `src/` is reference material, with regression fixtures that
help establish the original behavior.

Keep proprietary format decoding in `OpenSpidey.Assets`, game rules and mission
execution in `OpenSpidey.Core`, and Godot node/resource adapters in
`OpenSpidey.Game`. Domain code must not depend on Godot, platform APIs, native
addresses or the original executable. Prefer concrete, cohesive classes and
small interfaces at actual engine boundaries. Avoid abstractions with no
current consumer and avoid naming fields more specifically than the evidence
supports.

Preserve story node IDs, command IDs and event order. Report unported commands
with a node and source offset; do not silently skip them. Use named enums at
runtime and numeric IDs only at the asset boundary. Separate intentional bug
fixes from behavior ports and add focused regression cases for them.

Run `python tools/modern.py test` and `python tools/modern.py build` for managed
changes. With original assets available, run the archive validation and engine
smoke commands in the migration guide. Rendering and animation changes also
need visual checks. A parser pass does not prove mission completion.

Keep original assets, toolchain downloads and generated engine/build output
out of version control. Do not modify a user's game files or saves during an
import. Record port coverage honestly in the migration guide.
