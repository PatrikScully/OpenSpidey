# Player surface crawling

`CrawlPhysicsController` owns one crawl update over borrowed player state.
`DoCrawlingPhysics` remains the original exported entry point and delegates to
that controller. Its phases are movement, forward/corner probes, ground query,
missing-surface recovery, surface response, control transition and support-body
notification. `CrawlMotion` contains independently testable arithmetic, and
`CrawlFrameTrace` holds optional diagnostics outside the movement decisions.

Movement adds acceleration once, applies per-axis friction once and zeros the
inclusive [-2048, 2048] velocity dead zone. Elapsed time of two ticks or fewer
uses the resulting velocity as displacement. Larger elapsed time adds half that
velocity times `(elapsed - 2)`, preserving arithmetic-shift rounding and wrapping.
Zero elapsed time still applies the original single movement step. Zero velocity
skips motion probes, but still queries the surface below the body.

The controller samples player hook 2 after movement and tests these paths:

- Forward: 96 units opposite the forward axis, with the start pulled back by
  one quarter of that displacement. A hit with normal alignment above 3271 is
  rejected without running side-corner probes. An accepted wall hit restores
  the previous body position before checking crawlability.
- Side corners: only when the forward probe initially misses; right then left,
  16 units each. A side hit restores the previous body position.
- Exterior corner: starts 70 units below the original forward probe's end and
  travels 128 units along the forward axis. An accepted hit marks an exterior
  corner and records distance minus eight, or zero at distance 16 or below.
- Surface below the body: 40 units above to 140 units below, using the current
  up direction. The first query retains the original optional collision trace.

When that ground query misses, the controller marks contact bit 2, probes for
an exterior corner from 96 units behind and 70 below, restores body position,
then repeats the ground query. If the repeat misses, bit 2 clears. Recovery
returns immediately even when the repeat hits; ordinary surface response does
not run in that branch.

Ordinary surface response fixes the hit record, sets the ground-contact grace
counter to four and adjusts brightness. Non-crawlable faces either mark a fence
or restore position, then return. Crossing the normal-y threshold of 3400 calls
`HandleControlsForSurfaceTransition` with a temporarily replaced collision
normal. The original normal is restored afterward.

A moving body snaps to hit position plus normal times `mSurfaceClearance`.
A stationary body applies the same correction only when its GTE world-unit
squared length exceeds eight. The contacted normal and shadow position update
before a moving platform receives `NotifyTrodUpon`, `AdjustBruceHealth` and the
player's support-body pointer.

Verified player fields now describe crawling, hard transition animations,
exterior-corner/fence detection, transition distances, orientation reference and
support body. Original offsets and sizes remain validated. The inherited
`CBody::field_A8` also serves other actor collision calculations, so the player
exposes it as `crawlSurfaceNormal()` without adding another data member.
The corner flag and support pointer retain original persistence rules.

## Verification

The fourteenth CTest case covers movement timing, rounding, friction, signed
boundaries, zero velocity and normal alignment. It runs under ASan/UBSan.

The optional Linux/i386 oracle executes original `DoCrawlingPhysics` at
0x467FD0. A hook callback captures position and velocity after original movement;
world callbacks return no hits. Across 10,000 full-range cases, those captured
values match `CrawlMotion` under GCC, Clang and ASan/UBSan. This proves movement
arithmetic, not every collision-response branch or platform behavior.

```sh
g++ -m32 -std=c++11 -I src -fno-pie -no-pie -O2 \
  tests/runtime/crawl_motion_oracle.cpp src/core/fixed_vector.cpp \
  -o /tmp/spidey-crawl-oracle
/tmp/spidey-crawl-oracle /path/to/original/SpideyPC.exe
```

`tests/runtime/player_crawl.gdb` performs the actual bank overhang zip roundtrip
and checks rendered limb poses. It then exercises stationary, moving and
missing-surface crawling against the loaded world's real collision code,
checking probe order/coordinates, contact flags and position recovery. The
existing actor, script, swing, bomb/vault, pacing and widescreen regressions also
pass. Full-mission crawling, all corner transitions and moving-platform behavior
still require broader playable validation.
