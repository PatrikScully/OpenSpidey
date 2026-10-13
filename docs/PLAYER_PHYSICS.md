# Ground and airborne player physics

`PlayerPhysicsController` separates ordinary movement from world queries and
surface response. The original `CPlayer::DoPhysics` entry point reads the four
executable policy cells and constructs the controller. The controller clears
frame collision/scratch state and remembers the previous support body before
dispatching web swinging or crawling, as the original did.

Ordinary updates have these phases:

1. Apply acceleration, friction and velocity dead zone.
2. Handle the collision-free zip animation window, or separate downward velocity
   from the motion sweep.
3. Compute displacement and apply the optional boss-arena radius constraint.
4. Sweep the carried object, body and shoulders; slide against encountered walls.
5. Recover velocity from the resolved displacement and apply permitted movement.
6. Update wall-probe direction and clip vertical travel.
7. Query falling/ground contact, respond, notify a support platform and normalize angles.

The zip window is state 0x40000 with animation 270 from frame 13 onward, or
animation 271. It intentionally uses full velocity for extra elapsed ticks,
where ordinary movement and crawling use half velocity. It returns before
collision probes and angle normalization.

`PlayerMotion` centralizes these timing policies, friction application,
velocity recovery, fall displacement and horizontal sliding. `CrawlMotion`
uses the same velocity/displacement implementation, retaining its original
zero-velocity behavior. `SweepGeometry` computes scaled lengths, adjusted reach,
body rays and shoulder offsets with explicit 32-bit wrapping and signed shifts.
Main body hits slide against a normalized horizontal normal; shoulder hits use
the raw normal. The loop permits two collision attempts. Carried-object hits
set flag 0x40, and a body/shoulder hit clears that flag and sets bit 1.

A no-horizontal-sweep update now initializes its hit pointer to null. The old
recovered code read an uninitialized `SLineInfo::pItem` when stationary or moving
only vertically, making false collision behavior possible. This is an intentional
correctness fix; undefined stack contents are not a compatibility contract.

Ground probing retains the original signed coordinate rules and classifications.
`GroundProbeFan` describes eight offset queries and the final center repeat,
which run only when the original wide-probe policy is enabled and the first ray
misses. The shipped policy is disabled. Floating coordinates retain the original
world-unit quantization.

`core/floating_word.h` preserves the old MSVC `_ftol` conversion: truncate to a
signed 64-bit x87 result, then keep its low 32 bits. Invalid or out-of-range
conversion yields the x87 indefinite integer, whose low word is zero. This
avoids undefined direct float-to-32-bit casts near coordinate limits.

The controller names verified player state for arena constraints, sweep reach,
wall-probe heading/direction and ground-contact grace updates. Original field
sizes/offsets and exported entry points remain validated. Physics policies borrow
references to the original cells, so each decision sees their current values.
The legacy boss pointer, world query routines and broader player-state codes
remain compatibility dependencies pending further migration.

## Verification

The fifteenth CTest case covers velocity/displacement, sliding, integer boundaries,
reach adjustment, sweep geometry, probe-fan ordering and float conversion. It
runs under ASan/UBSan. Existing crawl movement tests also use the shared math.

`player_motion_oracle.cpp` executes original `DoPhysics` at 0x466CE0 through the
collision-free animation path. Its position/velocity results match 10,000
full-range cases under GCC, Clang and sanitizers. The 10,000-case original crawl
movement oracle remains passing after sharing the implementation.

`floating_word_oracle.cpp` directly executes original `_ftol` at 0x529C7C and
compares 10,000 arbitrary float bit patterns, including large values and invalid
inputs, under GCC, Clang and sanitizers.

```sh
g++ -m32 -std=c++11 -I src -fno-pie -no-pie -O2 \
  tests/runtime/player_motion_oracle.cpp src/core/fixed_vector.cpp \
  -o /tmp/spidey-player-motion-oracle
/tmp/spidey-player-motion-oracle /path/to/original/SpideyPC.exe
```

`tests/runtime/player_physics.gdb` exercises actual loaded-level collision code:
stationary ground contact, vertical-only upward travel, falling motion, all
wide-probe offsets, collision-free zip movement, arena projection and wall-probe
heading changes. The arena case uses the player as a known reference position;
it does not validate a complete boss encounter. The existing crawl/swing,
actor/script, animation heap/zip, bomb/vault, pacing and widescreen pixel checks
remain passing.

The instruction oracles do not prove every sweep, landing or platform branch.
Full-mission player control, animation-state behavior and complete collision/
platform coverage remain part of the broader rewrite and playable validation.
