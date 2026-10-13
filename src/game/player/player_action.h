#pragma once
#include "core/types.h"
#include "core/wrapping_math.h"

namespace spidey {
namespace player {

// Exact one-hot action tags in the original 32-bit player record. The stored
// word remains i32 because existing systems also test groups with bit masks.
// Reserved40000000 has no verified semantic name yet.
enum class PlayerAction : u32 {
	Idle = 0x00000001u,
	Jumping = 0x00000002u,
	Falling = 0x00000004u,
	WallAttached = 0x00000008u,
	GroundMovement = 0x00000010u,
	WallJumpWindup = 0x00000040u,
	Dead = 0x00000080u,
	SwingWindup = 0x00000100u,
	SwingRelease = 0x00000200u,
	Swinging = 0x00000400u,
	PunchCombo = 0x00000800u,
	SurfaceLanding = 0x00001000u,
	SurfaceTransition = 0x00002000u,
	WebAttackWindup = 0x00004000u,
	WebFiring = 0x00008000u,
	WebAttachWindup = 0x00010000u,
	WebTugAttack = 0x00020000u,
	ZipWebPull = 0x00040000u,
	ScriptedStandCompletion = 0x00080000u,
	PickupReach = 0x00100000u,
	ObjectThrow = 0x00200000u,
	Rolling = 0x00400000u,
	KnockdownRecovery = 0x00800000u,
	SmashKick = 0x01000000u,
	ZipDive = 0x02000000u,
	ZipDivePunch = 0x04000000u,
	ZipDiveKick = 0x08000000u,
	Grabbed = 0x10000000u,
	WebDomeTrapped = 0x20000000u,
	Reserved40000000 = 0x40000000u,
	SwitchPull = 0x80000000u,
};

// Preserve the original signed storage word, including the high-bit action.
inline i32 actionWord(PlayerAction action) {
    return spidey::core::signedWord(static_cast<u32>(action));
}

} // namespace player
} // namespace spidey
