#pragma once

namespace spidey {
namespace application {

// Composes one gameplay/cutscene frame. HUD coordinates are enabled before
// submissions and restored before world rendering; presentation stays in
// LevelSession so frame pacing and buffer ownership have one owner.
class GameplayRenderer {
public:
    void render();

private:
    void prepareCamera();
    void drawHud();
    void beginWorld();
    void prepareEffectTextures();
    void drawWorldMessages();
    void drawActors();
    void drawSpecialActorEffects();
    void finishFrame();
    void countPrimitives();
    void recordBufferUsage();
};

} // namespace application
} // namespace spidey
