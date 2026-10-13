#include "game/player/player_behavior_controller.h"
#include "game/player/player_frame_preparation.h"
#include "game/player/player_frame_finalizer.h"
#include "compatibility/player_behavior_bridge.h"

namespace spidey {
namespace player {

void PlayerBehaviorController::update()
{
	verticalVelocityBeforePhysics_ = 0;
	completedAnimation_ = 0xFFFF;
	if (!prepareFrame()) return;
	resolveAnimationFollowOn();
	dispatchAction();
	finishFrame();
}

bool PlayerBehaviorController::prepareFrame()
{
    return PlayerFramePreparation(player_).update(verticalVelocityBeforePhysics_);
}

void PlayerBehaviorController::resolveAnimationFollowOn()
{
	CPlayer* pPlayer = &player_;
	// animation follow-on: when the anim that just finished has an entry in
	// the sorted follow-on table, chain straight into it.
	if (pPlayer->mAnimFinished != 0)
	{
		u16 anim = pPlayer->mAnim;

		if (gAnimFollowOnData[anim * 2] != 0)
		{
			completedAnimation_ = anim;
			pPlayer->PlaySingleAnim(gAnimFollowOnData[anim * 2 + 1], 0, -1);
		}
		else if (anim == 0)
		{
			completedAnimation_ = 0;
			pPlayer->PlaySingleAnim(0, 0, -1);
		}
		else if (anim == 0x32)
		{
			completedAnimation_ = 0x32;
			pPlayer->PlaySingleAnim(Rnd(2) != 0 ? 0x33 : 0x55, 0, -1);
		}
		else if (anim == 0x33 || anim == 0x55)
		{
			completedAnimation_ = anim;
			pPlayer->PlaySingleAnim(0x32, 0, -1);
		}
		else if (anim == 0x39)
		{
			completedAnimation_ = 0x39;
			pPlayer->PlaySingleAnim(pPlayer->mAnimDir == 1 ? 0x32 : 0x13, 0, -1);
		}
		else if (anim == 0x3A)
		{
			completedAnimation_ = 0x3A;
			pPlayer->PlaySingleAnim(pPlayer->mAnimDir == 1 ? 0x34 : 0x13, 0, -1);
		}
	}

}

void PlayerBehaviorController::finishFrame()
{
    PlayerFrameFinalizer(player_).update();
}

} // namespace player
} // namespace spidey
