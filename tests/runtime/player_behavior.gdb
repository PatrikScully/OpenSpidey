set pagination off
set confirm off
set env SPIDEY_SKIP_MOVIES 1
set env SPIDEY_SKIP fades
set env SPIDEY_QUIT_MS 20000
set env SPIDEY_KEYS 1000:enter,1000:enter,1000:enter
break SpideyMain
commands
silent
set *(int*)0x568fb0 = 2
continue
end
break PlayAway
run
disable breakpoints
set $player = (CPlayer*)*(int*)0x6a9038
set $player->mSynthesizedInputActive = 1
set *(unsigned char*)((char*)$player+0x553) = 1
set $player->mElapsedTicks = 2
set $player->mPos.vx=-375*4096
set $player->mPos.vy=-96*4096
set $player->mPos.vz=2900*4096
set $player->mVel.vx=0
set $player->mVel.vy=0
set $player->mVel.vz=0
set $player->mAcc.vx=0
set $player->mAcc.vy=0
set $player->mAcc.vz=0
set $player->mIsCrawling=0
set $player->field_E64=0
set $player->field_A8.vx=0
set $player->field_A8.vy=-4096
set $player->field_A8.vz=0
call $player->OrientToNormal(false,&ZeroVector)
python
import gdb
entries=[]
finalizer_entries=[]
heading_snapshots=[]
preparation_entries=[]
preparation_order=['updateCostumeVisibility','updateWaterEffect','updateDamageTimers',
                   'updateSenseEffect','updateFistBonus','initializeGroundContact',
                   'regenerateWebbing','updateSwingLine','prepareSupportAndCountdown',
                   'updateFramePhysics','selectSurfaceCamera','updateFriction',
                   'updateTargetingAndAirborneClock','updateLookaroundInput','updateMovementRamp']
class PreparationWatch(gdb.Breakpoint):
    def __init__(self,name):
        self.phase=name
        super().__init__('spidey::player::PlayerFramePreparation::'+name,internal=True)
    def stop(self):
        preparation_entries.append(self.phase)
        return False
preparation_watches=[PreparationWatch(name) for name in preparation_order]
finalizer_order=['updateCameraFacing','updateTorsoAim','updateHeadingAndGravity',
                 'updateMovementVelocity','rideSupportBody','updateVerticalImpulse',
                 'resolveActorContacts','collectPowerUps','separateStandingPlayerFromActors',
                 'sweepTriggerZones','buildPose','updateHeadTracking',
                 'updateAdditionalBodyParts','updateFlamingFists','updateAttachments',
                 'syncFistsAndFinishInput']
class FinalizerWatch(gdb.Breakpoint):
    def __init__(self,name):
        self.phase=name
        super().__init__('spidey::player::PlayerFrameFinalizer::'+name,internal=True)
    def stop(self):
        finalizer_entries.append(self.phase)
        if self.phase=='updateMovementVelocity':
            heading_snapshots.append(tuple(int(gdb.parse_and_eval('$player->'+name))
                for name in ('mAngles.vy','mHeadingDelta','mHeadingTurnTicksRemaining')))
        return False
finalizer_watches=[FinalizerWatch(name) for name in finalizer_order]
class PhaseWatch(gdb.Breakpoint):
    def __init__(self,name):
        self.phase=name
        super().__init__('spidey::player::PlayerBehaviorController::'+name,internal=True)
    def stop(self):
        entries.append(self.phase)
        return False
names=['prepareFrame','resolveAnimationFollowOn','dispatchAction','finishFrame',
       'updateIdle','updateReserved40000000','updateSwitchPull','updateFalling']
watches=[PhaseWatch(name) for name in names]
def set_field(name,value):
    gdb.execute('set $player->'+name+' = '+str(value))
def update(state):
    entries.clear()
    finalizer_entries.clear()
    heading_snapshots.clear()
    preparation_entries.clear()
    set_field('mActionState',state)
    set_field('field_E20',1)
    set_field('mAnimFinished',0)
    gdb.execute('call SpideyAI0($player)')
    suspended=int(gdb.parse_and_eval("$player->mBehaviorSuspended"))
    assert preparation_entries==(preparation_order[:9] if suspended else preparation_order),preparation_entries
    assert finalizer_entries==([] if suspended else finalizer_order),finalizer_entries
    return entries[:]
set_field('mBehaviorSuspended',1)
set_field('mBehaviorCountdownTicks',5)
assert update(1)==['prepareFrame'],entries
assert int(gdb.parse_and_eval('$player->mBehaviorCountdownTicks'))==3
set_field('mBehaviorCountdownTicks',1)
assert update(1)==['prepareFrame'],entries
assert int(gdb.parse_and_eval('$player->mBehaviorCountdownTicks'))==0
print('PASS suspended behavior skips animation, dispatch and shared finalizer')
set_field('mWebbing',0x100)
set_field('field_5D8',0)
set_field('field_5E8',0)
assert update(0)==['prepareFrame'],entries
assert int(gdb.parse_and_eval('$player->mWebbing'))==0x108
set_field('field_5D8',1)
assert update(0)==['prepareFrame'],entries
assert int(gdb.parse_and_eval('$player->mWebbing'))==0x108
set_field('field_5D8',0)
set_field('mWebbing',0x300)
assert update(0)==['prepareFrame'],entries
assert int(gdb.parse_and_eval('$player->mWebbing'))==0x302
set_field('mWebbing',0x555)
assert update(0)==['prepareFrame'],entries
assert int(gdb.parse_and_eval('$player->mWebbing'))==0x555
print('PASS countdown clamping and guarded web regeneration run before suspension')
set_field('mBehaviorSuspended',0)
common=['prepareFrame','resolveAnimationFollowOn','dispatchAction']
for state in (0,32):
    assert update(state)==common+['finishFrame'],entries
print('PASS unknown action words still run the shared finalizer')
set_field('mAnim',0x124)
assert update(1)==common+['updateIdle','finishFrame'],entries
print('PASS early idle handler return still reaches the shared finalizer')
set_field('field_EA6',99)
assert update(0x40000000)==common+['updateReserved40000000','finishFrame'],entries
assert int(gdb.parse_and_eval('$player->field_EA6'))==0
assert update(-2147483648)==common+['updateSwitchPull','finishFrame'],entries
assert int(gdb.parse_and_eval('$player->mActionState'))==-2147483648
print('PASS high-bit switch action and reserved action dispatch without signed ordering errors')
set_field('mHeadingTurnStep',100)
set_field('mHeadingTurnTicksRemaining',5)
set_field('mTargetHeading',700)
assert update(0)==common+['finishFrame'],entries
assert heading_snapshots[0][1:]==(200,3),heading_snapshots
set_field('mHeadingTurnTicksRemaining',1)
assert update(0)==common+['finishFrame'],entries
assert heading_snapshots[0][0]==700 and heading_snapshots[0][2]==0,heading_snapshots
print('PASS scripted heading applies elapsed ticks and snaps to its final target')
set_field('mPos.vy',-10000*4096)
set_field('mVel.vx',0)
set_field('mVel.vy',-2)
set_field('mVel.vz',0)
set_field('mAcc.vy',0xA000)
set_field('mAnim',0xD4)
set_field('mPendingWebReleaseAnimation',0)
set_field('mFallReferenceY',1234)
set_field('mFallReferenceTick',0)
set_field('mSupportBody',0)
for button in range(20):
    for byte in (0,1):
        gdb.execute('set *(unsigned char*)((char*)$player->field_E0C+'+str(button*16+byte)+') = 0')
assert update(4)==common+['updateFalling','finishFrame'],entries
assert int(gdb.parse_and_eval('$player->mVel.vy'))>0
assert int(gdb.parse_and_eval('$player->mAnim'))==0xD4
assert int(gdb.parse_and_eval('$player->mFallReferenceY'))==int(gdb.parse_and_eval('$player->mPos.vy'))
assert int(gdb.parse_and_eval('$player->mFallReferenceTick'))==int(gdb.parse_and_eval('*(int*)0x6B4CA8'))
print('PASS live falling action resets its reference when vertical velocity changes sign')
for watch in watches+finalizer_watches+preparation_watches: watch.delete()
print("PASS all 15 preparation and 16 finalization steps retain their original order")
end
quit
