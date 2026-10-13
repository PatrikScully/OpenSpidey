# Player behavior

`SpideyAI0` is now a compatibility entry point into `PlayerBehaviorController`.
The controller borrows the player record and owns only two values for an update:
vertical velocity before physics and the animation whose follow-on was resolved.
It neither owns the player nor allocates a second player record.

The frame pipeline is explicit:

1. `prepareFrame` updates timers, input, physics and orientation. The original
   suspension gate can stop the remaining phases; work before that gate still runs.
2. `resolveAnimationFollowOn` chains completed animations before action handling.
3. `dispatchAction` selects one handler from the current action word.
4. `finishFrame` performs shared camera, movement, contact, pose, effect and input
   work, including clearing the original input edge flags.

All 31 original actions have separate methods. `player_action_handlers.cpp`
contains routing; movement, web, combat and interaction methods live in
`player_locomotion_actions.cpp`, `player_web_actions.cpp`,
`player_combat_actions.cpp` and `player_interaction_actions.cpp`.
`PlayerAction` gives verified meanings to the action words. `mActionState` remains
an i32 at offset 0xE1C so existing masks and binary record layout remain compatible.
The dispatcher converts through u32, preserving the high-bit SwitchPull action.
The 0x40000000 action has no verified semantic name; it is explicitly reserved.
Changing state inside a handler selects an action on the next update, rather than
running another handler immediately. Unknown words still reach `finishFrame`.

The old case exits are method returns, followed by the common finalizer. Breaks
inside the fence and ground-recovery loops retain their loop targets. Idle's
local release-web label remains within its handler. WebAttachWindup (0x10000),
formerly buried in a default compare chain, is now an explicit routed action.

## Frame preparation

`PlayerFramePreparation` owns 15 pre-action steps. Status updates live in
`player_status_updates.cpp`; analog/lookaround handling lives in
`player_lookaround_input.cpp`; spawn, web regeneration, swing/support handling,
physics, surface camera, friction, targeting and the movement ramp live in
`player_frame_preparation.cpp`. `PlayerBehaviorController` is now a small frame
orchestrator with the animation-follow-on phase.

The suspension check follows nine preparation steps. Costume keys, timers,
status effects, the initial ground placement, web regeneration, swing-line
lifecycle and support/countdown handling still run while suspended. Physics,
surface camera selection, friction, targeting, analog/lookaround input, movement
ramping, animation follow-ons, action dispatch and finalization are skipped.
The vertical-velocity output is captured immediately before native physics.

Three verified fields replace untyped offset accesses without changing record
size or alignment: mBehaviorSuspended (0xE24), mBehaviorCountdownTicks (0xE28),
and mFallReferenceTick (0xE3C). The countdown expires at zero but does not itself
suspend updates. Fall reference time is refreshed outside the falling action
and at particular falling action transitions; it is not simply a grounded clock.
Web regeneration still requires both original effect flags to be clear, uses
four times elapsed ticks below 0x200 and elapsed ticks below 0x555.

## Shared frame work

`PlayerFrameFinalizer` owns the shared sequence as a separate borrowed-player
service. Camera decisions live in `player_camera_updates.cpp`; pose, head tracking,
body parts and attachments live in `player_presentation_updates.cpp`. Movement,
platform riding, impulses, actor contacts, pickups and trigger sweeps have focused
methods in `player_frame_finalizer.cpp`.

The sequence retains several details that affect behavior:

- Torso aiming captures the camera heading before heading/velocity updates and
  passes that exact value into movement, even if a later call changes the camera.
- Platform riding and trigger sweeping share the original query storage; trigger
  initialization still follows platform/contact processing.
- `PlayerHandHooks` holds the left/right hand positions for one update. Body-part
  queries fill them before effects run. A swinging render-end offset changes the
  selected hand before web and held-object positioning, as the original does.
- The previous trigger movement direction remains a function-local static across
  player frames and level sessions; a temporary controller does not reset it.
- Input edge clearing uses the pointer captured at the start of finalization,
  after all pose, attachment and camera work.

Verified heading fields now have names at their original offsets: mHeadingDelta
(0x548), mTargetHeading (0xDF0), mHeadingTurnStep (0xDF4), and
mHeadingTurnTicksRemaining (0xDF8). Turn completion uses the target's signed low
half-word, retaining the original 16-bit angle read.

`compatibility/player_behavior_bridge.h` keeps the original fixed-address cells,
untyped offset accesses and Windows member-call adapters separate from routing.
Its pointer constants address shared game storage; no mutable state is copied
between translation units. Standalone wrappers still call recovered methods.
These dependencies and many individual handler internals remain migration work.
`LocomotionAnimationPolicy` now owns falling and reverse-movement selection;
38 action transitions use named tags. [Locomotion animation](LOCOMOTION_ANIMATION.md)
records the decision contract and original-instruction comparisons. This does not
claim that every action has been semantically rewritten.

## Verification

`tests/runtime/player_behavior.gdb` loads a real level and verifies phase order,
suspended updates, unknown words, early idle exits, the reserved action and the
high-bit switch action. It also checks all 16 finalization steps in order and
scripted turn progression/completion. Preparation checks verify all 15 steps,
the nine steps before suspension, countdown clamping and guarded web regeneration.
The existing 120-update rendered zip roundtrip exercises
the shared pipeline through ceiling and floor transitions. Physics, swing,
actor/script, heap, bomb, vault, level-session and widescreen fixtures still pass.
GCC and Clang preserve the player layout; all 16 unit tests pass.

The tests do not exercise every combat action, held object or full mission.
The routing and phase extraction preserve the original instruction ordering,
but these behaviors still require broader game-level regression coverage.

## Original provenance

The original SpideyAI0 entry point is 0x4B13F0, with dispatch at 0x4B211F and
shared finalization at 0x4B6E8C..0x4B86B1. The original uses unsigned compare
chains over one-hot values. PC and Mac symbol ordering corroborate its identity.
Existing address comments at decision sites remain available during migration.
