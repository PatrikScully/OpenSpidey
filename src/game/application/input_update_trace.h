#pragma once

#include "engine/world/actor_list_updater.h"

namespace spidey {
namespace application {

// Optional game-input diagnostics. The world updater has no input dependency.
class InputUpdateTrace : public world::ActorUpdateObserver {
public:
    InputUpdateTrace() : upPressedBeforeUpdate_(false) {}
    void beginUpdate(const CBody& actor) override;
    void afterStage(const CBody& actor, world::ActorUpdateStage stage) override;

private:
    bool upPressedBeforeUpdate_;
};

} // namespace application
} // namespace spidey
