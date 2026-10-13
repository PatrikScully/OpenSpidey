#include "engine/world/actor_list_updater.h"
#include "engine/world/ob.h"
#include "core/distance.h"
#include "core/intrusive_list_walk.h"

namespace spidey {
namespace world {

namespace {
CBody* nextActor(const CBody& actor) {
    return static_cast<CBody*>(actor.mNextItem);
}
} // namespace

void ActorListUpdater::update(CBody** listHead, const CVector& referencePosition) {
    core::visitIntrusiveList(*listHead, nextActor,
        [this, listHead, &referencePosition](CBody& actor) {
            updateActor(actor, listHead, referencePosition);
        });
}

void ActorListUpdater::updateActor(CBody& actor, CBody** listHead,
                                   const CVector& referencePosition) {
    print_if_false((actor.mCBodyFlags & CBODY_SUSPENDED) == 0,
                   "Suspended flag illegally set");

    if (actor.mCBodyFlags & CBODY_ZOMBIE) {
        if (actor.mCBodyFlags & CBODY_TIMETODIE)
            delete &actor;
        else
            actor.mCBodyFlags |= CBODY_TIMETODIE;
        return;
    }

    actor.mPlayerDist = math::approximateDistance(referencePosition, actor.mPos);
    if ((actor.mCBodyFlags & CBODY_RADIALSUSPENSION)
            && actor.mPlayerDist > suspensionDistance_) {
        actor.Suspend(listHead);
        return;
    }

    if (observer_) observer_->beginUpdate(actor);
    const bool hasAnimation = (actor.mFlags & 2) != 0;
    actor.EveryFrame();
    if (hasAnimation) {
        notify(actor, ActorUpdateStage::AnimatedTiming);
        static_cast<CSuper&>(actor).UpdateFrame();
        notify(actor, ActorUpdateStage::Animation);
    }
    actor.AI();
    notify(actor, ActorUpdateStage::Behavior);

    if ((actor.mCBodyFlags & CBODY_HASSHADOW) || actor.mpShadow)
        actor.UpdateShadow();
}

void ActorListUpdater::resumeNearby(CBody* suspendedHead, const CVector& referencePosition) {
    core::visitIntrusiveList(suspendedHead, nextActor,
        [this, &referencePosition](CBody& actor) {
            if ((actor.mCBodyFlags & CBODY_RADIALSUSPENSION)
                    && math::approximateDistance(referencePosition, actor.mPos) <= suspensionDistance_)
                actor.UnSuspend();
        });
}

void ActorListUpdater::notify(const CBody& actor, ActorUpdateStage stage) {
    if (observer_) observer_->afterStage(actor, stage);
}

} // namespace world
} // namespace spidey

void Ob_AI(CBody** listHead, i32 legacyFlags) {
#ifdef SPIDEY_STANDALONE
    (void)legacyFlags;
    if (!*listHead) return;
    spidey::world::ActorListUpdater updater(SuspendedDistance);
    updater.update(listHead, G_MECHLIST->mPos);
#else
    typedef void (*OriginalUpdate)(CBody**, i32);
    reinterpret_cast<OriginalUpdate>(0x00460FC0)(listHead, legacyFlags);
#endif
}

void Ob_MaybeUnSuspendOrCull() {
#ifdef SPIDEY_STANDALONE
    if (!G_SUSPENDED_LIST) return;
    spidey::world::ActorListUpdater updater(SuspendedDistance);
    updater.resumeNearby(G_SUSPENDED_LIST, G_MECHLIST->mPos);
#else
    typedef void (*OriginalResume)();
    reinterpret_cast<OriginalResume>(0x00461160)();
#endif
}
