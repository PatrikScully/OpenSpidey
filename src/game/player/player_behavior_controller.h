#pragma once
#include "core/types.h"

class CPlayer;

namespace spidey {
namespace player {

// One controller borrows the player for a single update. Action exits return to
// the shared frame finalizer; suspension skips the rest of the update entirely.
class PlayerBehaviorController {
public:
	explicit PlayerBehaviorController(CPlayer& player) : player_(player) {}
	void update();

private:
	bool prepareFrame();
	void resolveAnimationFollowOn();
	void dispatchAction();
	void finishFrame();
	void updateIdle();
	void updateJumping();
	void updateFalling();
	void updateWallAttached();
	void updateGroundMovement();
	void updateWallJumpWindup();
	void updateDead();
	void updateSwingWindup();
	void updateScriptedStandCompletion();
	void updatePickupReach();
	void updateRolling();
	void updateKnockdownRecovery();
	void updateWebDomeTrapped();
	void updateReserved40000000();
	void updateSwitchPull();
	void updateSwingRelease();
	void updateSwinging();
	void updatePunchCombo();
	void updateSurfaceLanding();
	void updateZipDive();
	void updateZipDivePunch();
	void updateWebAttackWindup();
	void updateWebFiring();
	void updateWebTugAttack();
	void updateZipWebPull();
	void updateSurfaceTransition();
	void updateObjectThrow();
	void updateSmashKick();
	void updateZipDiveKick();
	void updateGrabbed();
	void updateWebAttachWindup();

	CPlayer& player_;
	// Captured before physics; falling compares this with the resulting velocity.
	i32 verticalVelocityBeforePhysics_;
	// 0xFFFF means no follow-on animation was resolved in this update.
	i32 completedAnimation_;
};

} // namespace player
} // namespace spidey
