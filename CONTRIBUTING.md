# Contribute to OpenSpidey

Please read [CLAUDE.md](CLAUDE.md) before changing game code, and use
[the build guide](docs/BUILDING.md) to set up the checks.

## Report a game problem

Include the app version, operating system, map and difficulty, the last
input or action, and the game's log. The launcher shows the log path when
the game stops unexpectedly. Screenshots or a short recording help with
visual bugs.

Do not upload game archives, disc images or the original executable. A
small input script or description is enough to reproduce many problems.

## Change the code

- Use a branch for a focused change. Keep original game functions and new
  platform features separate where practical.
- Check game behavior against original machine code or the original game.
  Label intentional native-engine improvements clearly.
- Keep original game code compatible with the MSVC6 toolchain. Every C++
  function needs its primary tag. New non-game helpers use `@Bogus`.
- Preserve `@Matching` functions. Record existing instruction differences
  for touched `@Ok` functions and do not add new differences.
- Fix shared-global access one function at a time. Do not convert a whole
  global family without proving the complete call chain.
- Make one game-function change per commit. Use signed commits and short,
  plain English messages. Keep build output and game assets out of commits.
- Run the Linux build/layout check and `tools/dunno.py`. Run the relevant
  native, launcher or original-binary checks for your change.

Runtime parity and a matching decompilation are different checks. A
function should not receive `@Matching` from a gameplay smoke test alone.

## Test and document

Use a test that can fail for the bug being fixed. Record the exact build,
inputs and game state when a result depends on them. Loading a map or
waiting at spawn does not prove mission completion, later enemy spawns or
boss behavior.

Update [the changelog](CHANGELOG.md) for player-facing changes and the
[scenario inventory](tests/gameplay/scenarios.json) when coverage changes.
Separate completed checks from planned tests.

The project builds on
[krystalgamer/spidey-decomp](https://github.com/krystalgamer/spidey-decomp).
Preserve upstream credit and follow the maintainer's review scope for any
change proposed there. Upstream pull requests have stricter file rules than
OpenSpidey's native app and packaging work.
