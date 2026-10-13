# Fixed-point vectors

`core/fixed_vector` contains vector arithmetic with the original 12-byte
`CVector` and 6-byte `CSVector` layouts. Coordinates generally use 12-bit fixed
point; short vectors generally hold angles in a 4096-unit turn. The legacy
class names, component names and exported pointer-returning assignment methods
remain ABI contracts.

Addition, subtraction and multiplication retain low 32 bits. Short results
retain low 16 bits. Left shifts use unsigned bits; right shifts explicitly
sign-extend. Variable shift counts use the original x86 mask of 31, including
friction counts. Division truncates toward zero and requires a nonzero divisor
and a representable signed result, as the original `idiv` does.

Friction subtracts each component shifted by its own damping count. `KillSmall`
zeros the inclusive range [-2048, 2048] for coordinates and [-1, 1] for angles.
`Mask` reduces angles to the low 12 bits. All constructors initialize all
components, including the single-axis constructor.

`SquaredLength` squares raw components with 32-bit wrapping. `LengthSquared`
and `Length` instead shift coordinates to world units and use shared GTE
emulation. These GTE operations and the original export hook registrations live
in `compatibility/fixed_vector_bridge`, outside the arithmetic implementation.

The original vector-by-vector multiplication at 0x4E77D0 is unusual: it reads
only `left.vx` and scales the entire right vector by that scalar. Its exported
symbol remains in the compatibility bridge. Ordinary game code uses explicit
scalar multiplication, including web/swing movement, shatter fragments and
Mysterio effects. Core headers do not expose that misleading overload.

## Verification

The `fixed-vector` CTest case covers independent components, signed boundaries,
short narrowing, masked shifts, damping, aliasing, dead zones and initialization.
The arithmetic source links independently of game globals and GTE emulation.

The optional Linux/i386 oracle executes the user's original PE code without
starting Windows or modifying assets. It compares 100,000 results across ten
original free operators, using full-range deterministic integer inputs. This
checks add/subtract, scalar multiplication, the legacy multiplication, division
and shifts, plus short-vector multiply/subtract/divide. It passes under GCC,
Clang and ASan/UBSan. The shared fixture loader also serves the actor-motion
oracle. It verifies the executable format, image base and method prologue;
other executable revisions require a separate address audit.

```sh
g++ -m32 -std=c++11 -I src -fno-pie -no-pie -O2 \
  tests/runtime/fixed_vector_oracle.cpp src/core/fixed_vector.cpp \
  -o /tmp/spidey-vector-oracle
/tmp/spidey-vector-oracle /path/to/original/SpideyPC.exe
```

The oracle does not validate all collision geometry, GTE state, actor AI or
entire missions. Gameplay regression fixtures exercise the integrated build;
manual playable validation remains part of the broader rewrite.
