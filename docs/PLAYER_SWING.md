# Player web swing

`SwingArc` names the original swing geometry and phase policy. The anchor minus
length times the down direction gives the player position. Phase starts at
2048, advances by elapsed ticks times phase speed, and completes at 4096. The
orientation samples cosine at the phase masked to 12 bits. Animation frames
scale `(phase - 2048)` with an arithmetic shift of 11; storing a frame retains
its low 16 bits. Integer calculations explicitly preserve original wrapping.

`CSwinger::currentPosition` uses typed length, anchor and direction fields. The
old free `CSwinger_GetCurrentParams` adapter remains available. Constructor and
AI share matrix-axis extraction and web-line updates. Rendering synchronizes
each dangling segment position with its base segment endpoint. `SetRenderEnd`
stores the difference from the previous final endpoint before that update.
`mPhase` and `mRenderEndOffset` replace recovered offset names without changing
the 0x190-byte layout. Additional offset validation covers anchor, length,
direction and phase speed.

`SwingCollisionResolver` borrows position, velocity, collision flags and a world
query interface. `WorldSwingProbe` adapts the player's existing `SLineInfo` to
original world collision calls. The player samples hook 2 before and after
following the swing arc, then passes those samples to the resolver.

The resolver tests these rays in order and stops at the first hit:

1. Hook movement extended by another horizontal step; its vertical component
   stays at the second hook's height. A normal y of -2600 or above sets contact
   bit 1; a lower value sets bit 2.
2. From the extended hook to y plus 0x80000. A hit sets bit 2.
3. From the same start to y minus 0x20000. A hit sets bit 1.

The signed offsets and classifications are original behavior. A hit places the
body at hit position plus normal times `mSurfaceClearance`, then zeros velocity.
The clearance is in world units (original default 96); it replaces the player's
verified field at 0xEA8. Existing collision flags remain set. When all rays miss,
velocity becomes the displacement from the previous player position.

## Verification

The thirteenth CTest case covers phase/geometry, animation mapping, all probe
outcomes, query order, normal threshold, contact flag preservation, stopping
and unblocked velocity. It also runs independently under ASan/UBSan.

The optional Linux/i386 fixture executes original `DoSwingingPhysics` at
0x467D20 and original geometry/animation/completion instructions. Only hook
sampling and world queries are replaced with deterministic callbacks. Across
10,000 full-range cases, the new resolver matches exact rays, position,
velocity, flags, animation frames and completion. GCC, Clang and ASan/UBSan
pass. This verifies collision response independently of original world tracing;
it does not prove all world geometry or swing input states.

```sh
g++ -m32 -std=c++11 -I src -fno-pie -no-pie -O2 \
  tests/runtime/player_swing_oracle.cpp \
  src/game/player/swing_collision_resolver.cpp src/core/fixed_vector.cpp \
  -o /tmp/spidey-swing-oracle
/tmp/spidey-swing-oracle /path/to/original/SpideyPC.exe
```

`tests/runtime/player_swing.gdb` additionally constructs a real swing object in
the loaded bank level. It verifies phase/orientation, synchronized segments,
actual player hook sampling and world probes, render-end offset, animation
mapping and completion. The deleting destructor restores the original bit
count and polyline list. The fixture initializes storage as the game's zeroing
allocator does and restores the player's borrowed action object and timer.
It runs in a private test process and does not alter assets or save settings.
Manual full-mission and complete player-state validation remain pending.
