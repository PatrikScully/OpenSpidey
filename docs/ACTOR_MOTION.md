# Actor motion

`ActorMotionIntegrator` advances borrowed position, velocity, acceleration,
angle and angular-friction records. Black Cat and the Spider-Man clone retain
original `DoPhysics` entry points and unchanged binary layouts. Their gravity,
previous elapsed ticks and smoothed timestep now have verified names.

Black Cat performs one two-tick substep for each started pair of elapsed ticks.
Zero and negative elapsed ticks leave all motion records untouched; an odd tick
count still runs the last complete two-tick substep. Each substep computes
vertical acceleration as gravity minus velocity divided by sixteen, updates
velocity, then updates position using that new velocity and the acceleration.
It updates yaw and angular velocity before applying angular damping.

The clone uses a smoothed timestep with twelve fractional bits. Its velocity and
position products wrap at thirty-two bits before arithmetic right shifts.
Timestep squaring and angular products retain their original unsigned shifts.
The acceleration displacement is divided by two before its final right shift;
combining these operations changes rounding. A zero timestep still recomputes
acceleration, masks angles and damps angular velocity.

Both paths preserve signed division toward zero, low-sixteen-bit angular stores,
twelve-bit angle masks, variable x86 shift-count masking and retirement of angular
velocities between -1 and 1. `core/wrapping_math.h` defines the original word
arithmetic without signed overflow, negative left shifts or invalid object casts.
The previous single-word temporary vectors and clone scalar-vector construction
are no longer part of actor integration.

The independent unit test checks normal trajectories, odd/zero ticks, negative
velocity rounding, damping, angle wrapping and integer overflow. An optional
Linux/i386 fixture compares all body bytes after running the new implementation
and the original executable's actual physics instructions for 4,000 inputs.
Inputs include extreme positions/velocities/gravity, signed angles, all-byte
friction values and ordinary/edge fixed timesteps. It executes only the two pure
integer physics methods and their vector helpers; Windows startup is not run.

```sh
g++ -m32 -std=c++11 -I src -fno-pie -no-pie -O2 \
  tests/runtime/actor_motion_oracle.cpp \
  src/engine/world/actor_motion_integrator.cpp \
  -o /tmp/spidey-motion-oracle
/tmp/spidey-motion-oracle /path/to/game/SpideyPC.exe
```

This fixture uses the original PC executable's addresses 0x414B10 and 0x4B0D80.
It verifies PE architecture/image layout and method prologues before executing
those methods. The original binary and game assets are not redistributed.
GCC, Clang and ASan/UBSan runs produce identical results. This proves these motion
methods; complete missions, collision and other actor physics remain separate
validation work.
