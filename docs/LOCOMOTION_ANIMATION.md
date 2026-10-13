# Locomotion animation decisions

`LocomotionAnimationPolicy` contains the falling-animation and reverse-movement
selection rules. It is independent of player ownership, the world, audio, cameras
and animation playback. Callers apply the resulting plan in original order.
Animation numbers remain original asset identifiers; unknown semantic names are
not invented.

`FallAnimationPlan` separates three decisions: reset the fall reference, select
an animation, and consume a pending web-release animation. These decisions are
not equivalent. A velocity sign change can reset the reference while preserving
a flailing animation or an already selected fall loop. Zero velocity is on the
nonnegative side of the sign test, but release retargeting requires velocity
strictly greater than zero. A pending release selects 0x116 from swing animation
0x118, otherwise 0x114, except that flailing preserves the release flag.

`KeepAnimation` means no playback call. The policy does not run animation,
change the player record, or update time. The falling handler first runs its
original ordered landing/wall/ceiling/web checks, completed-animation and damage
responses, then applies the plan and its reference/flag changes. Shared frame
finalization still follows every handler exit.

Reverse-movement selection preserves the original mapping from 0x15 to 0x19,
0x34 to 0x5B, and 0x32/0x33 to 0x59. Unrecognized animations remain unchanged.
The surrounding heading window, velocity reset and rolling transition stay in
the ground-movement handler.

The player record now names mPendingWebReleaseAnimation (0x54D) and
mFallReferenceY (0xE38). Their size, type and offsets are preserved. Action
transitions in all four handler files use `PlayerAction` and `actionWord`, which
preserves the signed storage bits, including 0x80000000.

## Verification

The sixteenth CTest case covers sign boundaries, zero velocity, retained flail
state, pending release, fallback animation and reverse-movement selection.
`tests/runtime/locomotion_animation_oracle.cpp` runs the original machine-code
block from 0x4B2A15 through 0x4B2BBB. It covers every u16 animation identifier,
eight velocity boundary pairs and four release-byte values: 2,097,152 cases.

The fixture sets the original saved-velocity stack slot and replaces only
PlaySingleAnim with a capture callback. Original predicates, fall-reference and
release writes execute unchanged. A return at the block exit skips the unrelated
frame tail. The complete player bytes and number of playback calls match the
policy result under GCC, Clang and address/undefined-behavior sanitizers.
This proves the extracted decision block, not animation decoder or whole AI.

The real-level behavior fixture also forces upward-to-downward velocity reversal
and verifies the native falling handler updates its reference position/tick.
Existing rendered ceiling/floor, bomb, vault, actor/script, physics, swing,
frame-session and widescreen tests remain passing. Combat and full missions
still require broader regression coverage.
