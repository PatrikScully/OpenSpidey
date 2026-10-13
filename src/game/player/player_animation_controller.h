#pragma once

class CPlayer;

namespace spidey {
namespace player {

// Starts an animation together with its original frame-synchronized sound
// script. This service borrows a player; it adds no fields to its ABI layout.
class PlayerAnimationController {
public:
    explicit PlayerAnimationController(CPlayer& player) : player_(player) {}
    void play(int animationId, int startFrame = 0);

private:
    CPlayer& player_;
};

} // namespace player
} // namespace spidey
