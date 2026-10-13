#include "game/player/player_behavior.h"
#include "game/player/player_behavior_controller.h"

// Original exported entry point (0x4B13F0).
void SpideyAI0(CPlayer* player)
{
	spidey::player::PlayerBehaviorController(*player).update();
}
