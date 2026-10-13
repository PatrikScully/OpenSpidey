#include "game/player/player_behavior_controller.h"
#include "game/player/player_action.h"
#include "game/player/spidey.h"

namespace spidey {
namespace player {


void PlayerBehaviorController::dispatchAction()
{
	switch (static_cast<PlayerAction>(static_cast<u32>(player_.mActionState))) {
	case PlayerAction::Idle: updateIdle(); return;
	case PlayerAction::Jumping: updateJumping(); return;
	case PlayerAction::Falling: updateFalling(); return;
	case PlayerAction::WallAttached: updateWallAttached(); return;
	case PlayerAction::GroundMovement: updateGroundMovement(); return;
	case PlayerAction::WallJumpWindup: updateWallJumpWindup(); return;
	case PlayerAction::Dead: updateDead(); return;
	case PlayerAction::SwingWindup: updateSwingWindup(); return;
	case PlayerAction::ScriptedStandCompletion: updateScriptedStandCompletion(); return;
	case PlayerAction::PickupReach: updatePickupReach(); return;
	case PlayerAction::Rolling: updateRolling(); return;
	case PlayerAction::KnockdownRecovery: updateKnockdownRecovery(); return;
	case PlayerAction::WebDomeTrapped: updateWebDomeTrapped(); return;
	case PlayerAction::Reserved40000000: updateReserved40000000(); return;
	case PlayerAction::SwitchPull: updateSwitchPull(); return;
	case PlayerAction::SwingRelease: updateSwingRelease(); return;
	case PlayerAction::Swinging: updateSwinging(); return;
	case PlayerAction::PunchCombo: updatePunchCombo(); return;
	case PlayerAction::SurfaceLanding: updateSurfaceLanding(); return;
	case PlayerAction::ZipDive: updateZipDive(); return;
	case PlayerAction::ZipDivePunch: updateZipDivePunch(); return;
	case PlayerAction::WebAttackWindup: updateWebAttackWindup(); return;
	case PlayerAction::WebFiring: updateWebFiring(); return;
	case PlayerAction::WebTugAttack: updateWebTugAttack(); return;
	case PlayerAction::ZipWebPull: updateZipWebPull(); return;
	case PlayerAction::SurfaceTransition: updateSurfaceTransition(); return;
	case PlayerAction::ObjectThrow: updateObjectThrow(); return;
	case PlayerAction::SmashKick: updateSmashKick(); return;
	case PlayerAction::ZipDiveKick: updateZipDiveKick(); return;
	case PlayerAction::Grabbed: updateGrabbed(); return;
	case PlayerAction::WebAttachWindup: updateWebAttachWindup(); return;
	default: return;
	}
}

} // namespace player
} // namespace spidey
