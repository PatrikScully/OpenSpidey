#pragma once

namespace spidey {
namespace application {

// Gameplay frame orchestration. Camera AI stays in the display phase, after
// world/menu updates, to preserve the original camera and trigger ordering.
class WorldSimulation {
public:
    void update();
    void updateCamera();

private:
    enum DebugFlag { SingleStep = 0x100, InputWithCamera = 0x200, InputOnly = 0x400 };
    void waitForDebugStep();
    void updateActiveWorld();
    void updateTriggersAndUi();
    void updateCameraMode();
};

} // namespace application
} // namespace spidey
