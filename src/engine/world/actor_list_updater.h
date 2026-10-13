#pragma once

#include "core/types.h"

class CBody;
class CVector;

namespace spidey {
namespace world {

enum class ActorUpdateStage { AnimatedTiming, Animation, Behavior };

class ActorUpdateObserver {
public:
    virtual ~ActorUpdateObserver() {}
    virtual void beginUpdate(const CBody& actor) = 0;
    virtual void afterStage(const CBody& actor, ActorUpdateStage stage) = 0;
};

// Borrows the live suspension threshold: trigger commands may change it
// during an actor's AI. Positions are also borrowed so movement is visible to
// later actors. Does not own the lists or add fields to original actor types.
class ActorListUpdater {
public:
    explicit ActorListUpdater(const u32& suspensionDistance,
                              ActorUpdateObserver* observer = nullptr)
        : suspensionDistance_(suspensionDistance), observer_(observer) {}

    void update(CBody** listHead, const CVector& referencePosition);
    void resumeNearby(CBody* suspendedHead, const CVector& referencePosition);

private:
    void updateActor(CBody& actor, CBody** listHead, const CVector& referencePosition);
    void notify(const CBody& actor, ActorUpdateStage stage);

    const u32& suspensionDistance_;
    ActorUpdateObserver* observer_;
};

} // namespace world
} // namespace spidey

// Original entry points remain adapters for executable integration and tests.
EXPORT void Ob_AI(CBody** listHead, i32 legacyFlags);
EXPORT void Ob_MaybeUnSuspendOrCull();
